using System.Collections.Generic;
using RoadArchitect;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Editor tool that paints the level's Adamak targets along the road - where the player can actually
/// drive into them, and where they are not in the passing cars' way.
///
/// Where they go, and why:
///  - <b>On the asphalt, never beside it.</b> Each target is placed from the road's own spline, so it
///    sits on the road however the road bends and climbs, and the spot's distance from the centre line
///    is measured against the road's own width - the same geometry the cars are driven along.
///  - <b>Clear of the passing cars.</b> The cars of the passing-car tool drive down the middle of each
///    lane, which is at a fixed distance from the centre line that comes from the road's lane width and
///    lane count. That lane, plus <see cref="laneClearance"/> either side of it, is reserved: a target is
///    pushed outboard of it, and a spot that cannot fit a target outside it is simply skipped. So the
///    two systems cannot end up sharing a lane.
///  - <b>Road edge, or the divider.</b> <see cref="Band.RoadEdge"/> puts them a little inside the
///    asphalt's edge, on either side of the road; <see cref="Band.CentreDivider"/> puts them on the
///    centre line itself, on the strip between the two directions. Both read as deliberate placement
///    rather than something dropped in the lane.
///  - <b>Not inside a blinder.</b> Blinders are rigid bodies standing on the centre line through the
///    bends, so the divider band and the blinder line are the same line in a corner; spots too close to
///    an existing blinder are skipped so the two do not overlap.
///
/// The KillDisplay of the level is wired into every target by the tool, so the ones it places score
/// kills exactly like the hand-placed ones in Core-3.
///
/// Usage: open a level scene (Core-1 ... Core-4), check the road list, then
///   Tools > Road Tools > Paint Adamaks
/// </summary>
public class AdamakSpawner : EditorWindow
{
    private const string PrefabFolder = "Assets/Prefabs/Adamaks";
    private const string DefaultParentName = "Adamaks";
    private const string BlinderRootName = "Road Blinders";

    private enum Band
    {
        RoadEdge,
        CentreDivider,
    }

    private enum Sides
    {
        BothSides,
        RightSide,
        LeftSide,
    }

    private readonly List<RoadRouteEntry> roads = new List<RoadRouteEntry>();
    private GameObject[] prefabs = new GameObject[0];

    // ------------------------------------------------------------------ settings

    private Band band = Band.RoadEdge;
    private Sides sides = Sides.BothSides;

    private float spacing = 55f;
    private int maxCount = 90;
    private float startTrim = 30f;
    private float endTrim = 30f;

    private float edgeInset = 0.6f;
    private float laneClearance = 1.6f;
    private float lateralJitter = 0.35f;

    private float yawJitter = 12f;
    private bool faceRoad = true;
    private float lift = 0.05f;

    private float minBlinderGap = 6f;
    private bool randomType = true;

    private string parentName = DefaultParentName;
    private bool showPreview = true;

    // --------------------------------------------------------------------- state

    private Vector2 scroll;
    private List<PlanItem> cachedPlan;
    private bool planDirty = true;

    private int skippedLane;
    private int skippedBlinder;
    private int skippedOffRoad;

    private MonoBehaviour killDisplay;

    private class PlanItem
    {
        public GameObject prefab;
        public Vector3 position;
        public float yaw;
        public int side;
    }

    [MenuItem("Tools/Road Tools/Paint Adamaks")]
    static void OpenWindow()
    {
        AdamakSpawner window = GetWindow<AdamakSpawner>("Adamak Spawner");
        window.minSize = new Vector2(440, 660);
        window.Show();
    }

    void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
        UnityEditor.SceneManagement.EditorSceneManager.activeSceneChanged += OnActiveSceneChanged;

        if (roads.Count == 0)
            roads.AddRange(RoadRoute.FindRoadsInScene());

        FindPrefabs();
        FindKillDisplay();
        planDirty = true;
    }

    void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        UnityEditor.SceneManagement.EditorSceneManager.activeSceneChanged -= OnActiveSceneChanged;
        EditorUtility.ClearProgressBar();
    }

    private void OnActiveSceneChanged(Scene previous, Scene current)
    {
        roads.Clear();
        roads.AddRange(RoadRoute.FindRoadsInScene());
        FindKillDisplay();
        cachedPlan = null;
        planDirty = true;
        Repaint();
    }

    private void FindPrefabs()
    {
        List<GameObject> found = new List<GameObject>();
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { PrefabFolder });

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null && prefab.name.StartsWith("Adamak"))
                found.Add(prefab);
        }

        // Adamak2 before Adamak10, so the list matches the folder rather than the asset database's order.
        found.Sort((a, b) => NumberIn(a.name).CompareTo(NumberIn(b.name)));
        prefabs = found.ToArray();
    }

    private static int NumberIn(string name)
    {
        string digits = string.Empty;
        for (int i = 0; i < name.Length; i++)
            if (char.IsDigit(name[i])) digits += name[i];

        int value;
        return int.TryParse(digits, out value) ? value : int.MaxValue;
    }

    /// <summary>
    /// The level's kill counter, found by type name: the HUD lives in the gameplay assembly, which this
    /// editor assembly cannot reference, and a tool that has to be told where the HUD is would be a tool
    /// that silently stops scoring when someone renames an object.
    /// </summary>
    private void FindKillDisplay()
    {
        killDisplay = null;

        MonoBehaviour[] behaviours = Object.FindObjectsOfType<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] != null && behaviours[i].GetType().Name == "KillDisplay")
            {
                killDisplay = behaviours[i];
                return;
            }
        }
    }

    // ----------------------------------------------------------------------- GUI

    void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        GUILayout.Label("Paint Adamaks", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Places Adamak targets on the road as things for the player to hit, kept clear of the lane " +
            "the passing cars drive down and out of the blinders standing through the bends. " +
            "Every target it places is wired to the level's KillDisplay, so hitting one counts.",
            MessageType.Info);

        EditorGUI.BeginChangeCheck();

        EditorGUILayout.LabelField("Roads (in driving order)", EditorStyles.boldLabel);
        int usableRoads;
        if (RoadRoute.DrawRoadList(roads, out usableRoads))
            planDirty = true;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Where They Stand", EditorStyles.boldLabel);
        band = (Band)EditorGUILayout.EnumPopup(
            new GUIContent("Band", "Road Edge: a little inside the asphalt's edge, on one side of the " +
                                   "road. Centre Divider: on the centre line itself, on the strip " +
                                   "between the two directions"),
            band);
        sides = (Sides)EditorGUILayout.EnumPopup(
            new GUIContent("Sides", "Which side of the road they go on. Both Sides alternates at random"),
            sides);

        if (band == Band.RoadEdge)
        {
            edgeInset = EditorGUILayout.Slider(
                new GUIContent("Edge Inset (m)", "How far inside the asphalt's edge a target sits. " +
                                                 "Larger keeps them further out of harm's way"),
                edgeInset, 0.3f, 3f);
        }

        laneClearance = EditorGUILayout.Slider(
            new GUIContent("Lane Clearance (m)", "The reserve kept either side of the middle of a " +
                                                 "passing-car lane. The half-width of a passing car " +
                                                 "plus a margin; nothing is placed inside it"),
            laneClearance, 0.8f, 3f);

        lateralJitter = EditorGUILayout.Slider(
            new GUIContent("Lateral Jitter (m)", "Random offset either side of where a target would " +
                                                 "otherwise stand, so a row of them is not stamped out"),
            lateralJitter, 0f, 1.5f);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Along The Road", EditorStyles.boldLabel);
        spacing = EditorGUILayout.Slider(
            new GUIContent("Spacing (m)", "Average gap between targets along the route"),
            spacing, 5f, 200f);
        maxCount = EditorGUILayout.IntSlider(
            new GUIContent("Max Count", "Cap on how many are placed in one pass"),
            maxCount, 1, 400);
        startTrim = EditorGUILayout.Slider(
            new GUIContent("Trim Start (m)", "Nothing is placed this close to the start of the route"),
            startTrim, 0f, 200f);
        endTrim = EditorGUILayout.Slider(
            new GUIContent("Trim End (m)", "Nothing is placed this close to the end of the route"),
            endTrim, 0f, 200f);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("How They Stand", EditorStyles.boldLabel);
        faceRoad = EditorGUILayout.Toggle(
            new GUIContent("Face The Road", "Turn each target to face across the road, towards the " +
                                            "centre line, instead of looking along it"),
            faceRoad);
        yawJitter = EditorGUILayout.Slider(
            new GUIContent("Yaw Jitter (deg)", "Random turn off that facing"),
            yawJitter, 0f, 90f);
        lift = EditorGUILayout.Slider(
            new GUIContent("Lift (m)", "How far above the asphalt they are placed. They are rigid " +
                                       "bodies, so a small lift simply lets them settle"),
            lift, -0.2f, 1f);
        minBlinderGap = EditorGUILayout.Slider(
            new GUIContent("Blinder Gap (m)", "Spots this close to a blinder are skipped, so a target " +
                                              "never lands inside one on the centre line"),
            minBlinderGap, 0f, 30f);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Options", EditorStyles.boldLabel);
        randomType = EditorGUILayout.Toggle(
            new GUIContent("Mix The Types", "Pick a different Adamak for each spot. Off uses the first " +
                                            "prefab in the folder for all of them"),
            randomType);
        parentName = EditorGUILayout.TextField("Root Object Name", parentName);
        showPreview = EditorGUILayout.Toggle(
            new GUIContent("Scene Preview", "Draw the planned targets in the scene view"),
            showPreview);

        if (EditorGUI.EndChangeCheck())
            planDirty = true;

        EditorGUILayout.Space();
        DrawSummary(usableRoads);

        EditorGUILayout.Space();
        GUI.backgroundColor = Color.green;
        if (GUILayout.Button("PAINT ADAMAKS", GUILayout.Height(40f))) Paint();
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space();
        GUI.backgroundColor = Color.red;
        if (GUILayout.Button("Remove Painted Adamaks", GUILayout.Height(25f))) RemoveAll();
        GUI.backgroundColor = Color.white;

        EditorGUILayout.EndScrollView();
    }

    private void DrawSummary(int usableRoads)
    {
        List<RoadRouteSegment> segments = RoadRoute.BuildSegments(roads);
        float total = RoadRoute.TotalLength(segments);

        EditorGUILayout.LabelField("Summary", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("  Roads in the route: " + usableRoads +
                                   "   Route length: " + total.ToString("F1") + " m");
        EditorGUILayout.LabelField("  Adamak prefabs: " + prefabs.Length);

        if (prefabs.Length == 0)
        {
            EditorGUILayout.HelpBox("No Adamak prefabs found under " + PrefabFolder + ".", MessageType.Warning);
        }

        if (total <= 0f)
        {
            EditorGUILayout.HelpBox(
                "No usable roads. Add a RoadArchitect road with a built spline to the list.",
                MessageType.Warning);
            return;
        }

        Road reference = FirstUsableRoad();
        if (reference != null)
        {
            float half = reference.RoadWidth() * 0.5f;
            float laneCentre = LaneCentre(reference);
            EditorGUILayout.LabelField(
                $"  Road: {reference.RoadWidth():F0} m wide, {reference.laneAmount} lanes of " +
                $"{reference.laneWidth:F0} m   Asphalt half-width: {half:F1} m");
            EditorGUILayout.LabelField(
                $"  Passing cars drive at ±{laneCentre:F1} m; reserved band: " +
                $"{Mathf.Max(0f, laneCentre - laneClearance):F1} - {laneCentre + laneClearance:F1} m " +
                "from the centre line");
        }

        List<PlanItem> plan = GetPlan();
        if (plan == null || plan.Count == 0)
        {
            EditorGUILayout.HelpBox(
                "Nothing to place. Widen the lane clearance if every spot is landing in the passing " +
                "lane, add roads to the list, or reduce the trims.",
                MessageType.Warning);
            return;
        }

        EditorGUILayout.LabelField("  Targets to place: " + plan.Count);
        EditorGUILayout.LabelField("  Skipped: " + skippedLane + " in the passing lane, " +
                                   skippedBlinder + " at a blinder, " + skippedOffRoad + " with no road under them");
        EditorGUILayout.LabelField("  Root object: '" + parentName + "' (replaced on every paint)");
        EditorGUILayout.LabelField("  KillDisplay: " +
                                   (killDisplay != null ? killDisplay.gameObject.name : "none in this scene"));
    }

    private Road FirstUsableRoad()
    {
        for (int i = 0; i < roads.Count; i++)
            if (roads[i] != null && roads[i].road != null && roads[i].road.spline != null)
                return roads[i].road;

        return null;
    }

    /// <summary>
    /// How far from the centre line the passing cars drive: the middle of the outermost lane of one
    /// direction, worked out the same way the passing-car tool works it out, so the two tools cannot
    /// disagree about where the cars are.
    /// </summary>
    private static float LaneCentre(Road road)
    {
        if (road == null || road.laneAmount <= 0) return 2.5f;

        float lanesPerDirection = Mathf.Max(1f, road.laneAmount / 2f);
        return (lanesPerDirection - 0.5f) * road.laneWidth;
    }

    // ---------------------------------------------------------------------- plan

    private List<PlanItem> GetPlan()
    {
        if (planDirty || cachedPlan == null)
        {
            cachedPlan = BuildPlan();
            planDirty = false;
        }

        return cachedPlan;
    }

    private List<PlanItem> BuildPlan()
    {
        List<PlanItem> plan = new List<PlanItem>();

        List<RoadRouteSegment> segments = RoadRoute.BuildSegments(roads);
        float total = RoadRoute.TotalLength(segments);

        skippedLane = 0;
        skippedBlinder = 0;
        skippedOffRoad = 0;

        if (total <= 0f || prefabs.Length == 0) return plan;

        System.Random rng = new System.Random();
        List<Vector3> blinders = BlinderPositions();

        float lastSide = rng.Next(2) == 0 ? -1f : 1f;
        float distance = startTrim;

        while (distance <= total - endTrim && plan.Count < maxCount)
        {
            Vector3 centre;
            Vector3 tangent;
            RoadRouteSegment segment = RoadRoute.SampleAt(segments, distance, out centre, out tangent);

            if (segment == null) break;

            Road road = segment.road;
            float halfWidth = road != null ? road.RoadWidth() * 0.5f : 5f;
            float laneCentre = LaneCentre(road);

            float side = SideFor(rng, ref lastSide);
            float lateral;

            if (!ChooseLateral(halfWidth, laneCentre, side, rng, out lateral))
            {
                skippedLane++;
                distance += NextStep(rng);
                continue;
            }

            Vector3 across = RoadRoute.Across(tangent);
            Vector3 spot = centre + across * lateral;

            // Blinders and the divider run down the same line, so a target must not land inside one.
            if (NearBlinder(blinders, spot))
            {
                skippedBlinder++;
                distance += NextStep(rng);
                continue;
            }

            // The asphalt's own height, not the spline's: a road that is banked, cut into a hillside or
            // carried on a bank is not at the height of the line down its middle.
            // The drop is kept short so a target can only snap down to the road it is standing on: on a
            // viaduct the only thing below is the valley floor, and a long drop would plant the target
            // there instead.
            Vector3 surface = spot;
            bool found = RoadRoute.TryFindGround(spot, 1.5f, 2f, 0.6f, out surface);

            GameObject prefab = prefabs[randomType ? rng.Next(prefabs.Length) : 0];
            if (prefab == null)
            {
                distance += NextStep(rng);
                continue;
            }

            // Nothing under it means nothing to stand on: the only reason to accept that is a level
            // whose road has no collider at all, and then the spline's own height is the best guess.
            if (!found) skippedOffRoad++;

            Vector3 position = spot;
            position.y = surface.y - PrefabMeasure.Of(prefab).Bottom + lift;

            float facing = faceRoad
                ? RoadRoute.YawAlong(across * -side)
                : RoadRoute.YawAlong(tangent);

            PlanItem item = new PlanItem();
            item.prefab = prefab;
            item.position = position;
            item.side = side > 0f ? 1 : -1;
            item.yaw = facing + (float)(rng.NextDouble() * 2.0 - 1.0) * yawJitter;

            plan.Add(item);
            distance += NextStep(rng);
        }

        return plan;
    }

    private float NextStep(System.Random rng)
    {
        return spacing * (0.8f + (float)rng.NextDouble() * 0.4f);
    }

    private float SideFor(System.Random rng, ref float lastSide)
    {
        if (sides == Sides.RightSide) return 1f;
        if (sides == Sides.LeftSide) return -1f;

        // Both sides, with a bias towards changing sides: alternating reads as deliberate placement,
        // while a coin flip alone clusters runs of the same side.
        bool same = rng.NextDouble() < 0.3;
        float side = same ? lastSide : -lastSide;
        lastSide = side;
        return side;
    }

    /// <summary>
    /// Where along the road's width this spot's target stands, signed for the side it is on. False when
    /// there is nowhere on that side that clears both the passing lane and the edge of the asphalt.
    /// </summary>
    private bool ChooseLateral(float halfWidth, float laneCentre, float side, System.Random rng,
                              out float lateral)
    {
        lateral = 0f;

        float jitter = (float)rng.NextDouble() * lateralJitter;

        if (band == Band.CentreDivider)
        {
            // The strip between the two directions: clear of both lanes, and it has to be wide enough
            // for a target to stand in without being clipped by the cars.
            lateral = jitter;

            if (laneCentre - lateral < laneClearance) return false;

            lateral *= side;
            return true;
        }

        // Out at the edge of the asphalt, but never inside a passing car's lane: the band a target may
        // stand in on this side is everything between the lane's reserve and the edge of the road.
        float inside = laneCentre + laneClearance;
        float outside = halfWidth - 0.3f;

        // Too narrow on this road for a target to stand clear of the cars at all.
        if (inside > outside) return false;

        lateral = Mathf.Clamp(halfWidth - edgeInset, inside, outside);
        lateral = Mathf.Clamp(lateral + (float)(rng.NextDouble() * 2.0 - 1.0) * lateralJitter,
                              inside, outside);

        lateral *= side;
        return true;
    }

    private List<Vector3> BlinderPositions()
    {
        List<Vector3> positions = new List<Vector3>();

        GameObject root = RoadRoute.FindRootByName(BlinderRootName);
        if (root == null) return positions;

        for (int i = 0; i < root.transform.childCount; i++)
        {
            Transform child = root.transform.GetChild(i);
            if (child != null) positions.Add(child.position);
        }

        return positions;
    }

    private bool NearBlinder(List<Vector3> blinders, Vector3 spot)
    {
        if (minBlinderGap <= 0f || blinders.Count == 0) return false;

        float squared = minBlinderGap * minBlinderGap;

        for (int i = 0; i < blinders.Count; i++)
        {
            Vector3 delta = blinders[i] - spot;
            delta.y = 0f;

            if (delta.sqrMagnitude <= squared) return true;
        }

        return false;
    }

    // --------------------------------------------------------------------- paint

    private void Paint()
    {
        List<PlanItem> plan = GetPlan();
        if (plan == null || plan.Count == 0)
        {
            Debug.LogError("Adamak Spawner: nothing to place. Check the road list, the lane clearance " +
                           "and the trims.");
            return;
        }

        if (!EditorUtility.DisplayDialog("Paint Adamaks",
            "Place " + plan.Count + " Adamak targets along the route?\n\n" +
            "An existing root object named '" + parentName + "' will be replaced. Hitting one of these " +
            "counts a kill" + (killDisplay != null ? " on '" + killDisplay.gameObject.name + "'" : "") + ".\n\n" +
            "This action can be undone (Ctrl+Z).",
            "Paint", "Cancel"))
        {
            return;
        }

        RemoveAllSilently();

        GameObject parent = new GameObject(parentName);
        Undo.RegisterCreatedObjectUndo(parent, "Create Adamaks Root");

        int placed = 0;
        int wired = 0;

        try
        {
            for (int i = 0; i < plan.Count; i++)
            {
                EditorUtility.DisplayProgressBar("Painting Adamaks",
                    (i + 1) + " / " + plan.Count, (float)i / plan.Count);

                PlanItem item = plan[i];
                if (item.prefab == null) continue;

                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(item.prefab, parent.transform);
                if (instance == null) continue;

                Undo.RegisterCreatedObjectUndo(instance, "Create Adamak");

                instance.name = item.prefab.name + " " + (i + 1);
                instance.transform.rotation = Quaternion.Euler(0f, item.yaw, 0f);
                instance.transform.position = item.position;

                if (WireKillDisplay(instance)) wired++;
                placed++;
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        RoadRoute.MarkSceneDirty();

        Debug.Log("Adamak Spawner: placed " + placed + " targets (" + wired + " wired to the kill " +
                  "counter); skipped " + skippedLane + " spots in the passing lane, " + skippedBlinder +
                  " at a blinder, " + skippedOffRoad + " with no road under them.");
    }

    /// <summary>
    /// Points the target's own AdamakController at the level's kill counter.
    ///
    /// The controller is found by type name and its field through a SerializedObject, because both live
    /// in the gameplay assembly which this editor assembly cannot reference - the same wall RainPainter
    /// reaches past by reflection. This goes through serialized properties rather than reflection, so
    /// the change is a plain, undoable prefab override, exactly like the one the hand-placed targets in
    /// Core-3 already carry.
    /// </summary>
    private bool WireKillDisplay(GameObject instance)
    {
        if (killDisplay == null) return false;

        MonoBehaviour controller = null;
        MonoBehaviour[] behaviours = instance.GetComponentsInChildren<MonoBehaviour>(true);

        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] != null && behaviours[i].GetType().Name == "AdamakController")
            {
                controller = behaviours[i];
                break;
            }
        }

        if (controller == null) return false;

        SerializedObject serialized = new SerializedObject(controller);
        SerializedProperty property = serialized.FindProperty("killDisplay");
        if (property == null) return false;

        Undo.RecordObject(controller, "Wire Adamak Kill Display");

        property.objectReferenceValue = killDisplay;
        serialized.ApplyModifiedProperties();
        PrefabUtility.RecordPrefabInstancePropertyModifications(controller);

        return true;
    }

    private void RemoveAll()
    {
        GameObject existing = RoadRoute.FindRootByName(parentName);
        if (existing == null)
        {
            Debug.Log("Adamak Spawner: no root object named '" + parentName + "'.");
            return;
        }

        if (!EditorUtility.DisplayDialog("Remove Adamaks",
            "Remove the whole '" + parentName + "' hierarchy?", "Remove", "Cancel"))
        {
            return;
        }

        Undo.DestroyObjectImmediate(existing);
        RoadRoute.MarkSceneDirty();
        Debug.Log("Adamak Spawner: removed '" + parentName + "'.");
    }

    private void RemoveAllSilently()
    {
        GameObject existing = RoadRoute.FindRootByName(parentName);
        if (existing != null)
            Undo.DestroyObjectImmediate(existing);
    }

    // ---------------------------------------------------------------- scene view

    private void OnSceneGUI(SceneView view)
    {
        if (!showPreview) return;

        List<PlanItem> plan = GetPlan();
        if (plan == null || plan.Count == 0) return;

        List<RoadRouteSegment> segments = RoadRoute.BuildSegments(roads);
        float total = RoadRoute.TotalLength(segments);
        if (total <= 0f) return;

        // The reserved band, drawn as two lines down the route, so it is obvious where a target may not
        // go and why a spot was skipped.
        Road reference = FirstUsableRoad();
        if (reference != null)
        {
            float laneCentre = LaneCentre(reference);

            for (int side = -1; side <= 1; side += 2)
            {
                Handles.color = new Color(1f, 0.35f, 0.35f, 0.5f);

                Vector3 previous = Vector3.zero;
                bool started = false;

                for (float d = 0f; d <= total; d += RoadRoute.TurnSampleStep * 4f)
                {
                    Vector3 position;
                    Vector3 tangent;
                    if (RoadRoute.SampleAt(segments, d, out position, out tangent) == null) continue;

                    Vector3 point = position + RoadRoute.Across(tangent) * (side * laneCentre) +
                                    Vector3.up * 0.15f;

                    if (started) Handles.DrawAAPolyLine(2f, previous, point);
                    previous = point;
                    started = true;
                }
            }
        }

        for (int i = 0; i < plan.Count; i++)
        {
            PlanItem item = plan[i];

            Handles.color = item.side > 0
                ? new Color(0.4f, 1f, 0.55f, 0.95f)
                : new Color(0.45f, 0.75f, 1f, 0.95f);

            float size = HandleUtility.GetHandleSize(item.position) * 0.3f;
            Handles.DrawSolidDisc(item.position + Vector3.up * 0.03f, Vector3.up, size);

            Quaternion yaw = Quaternion.Euler(0f, item.yaw, 0f);
            Handles.DrawAAPolyLine(3f, item.position + Vector3.up * 0.1f,
                item.position + Vector3.up * 0.1f + yaw * Vector3.forward * 1.2f);
        }
    }
}
