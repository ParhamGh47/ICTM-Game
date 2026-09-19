using System.Collections.Generic;
using RoadArchitect;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Editor tool that scatters rigid roadside junk - logs, bins, barriers, crates - along the verge,
/// off the asphalt, and picks each one from what is actually around it.
///
/// The reasoning is deliberately loose rather than a fixed layout:
///  - every spot is scored on how wooded and how built up it is, by counting the terrain's own tree
///    instances and the real geometry (renderers that are not the road, the terrain or the props
///    themselves) inside a radius,
///  - a prop can be tagged as belonging to the forest, to a built up area, or to anywhere, and it is
///    only eligible where its own kind of surroundings are present,
///  - among the eligible ones the pick is weighted and jittered, so a wood is mostly logs but not only
///    logs, and a town is mostly bins and barriers but still has the odd crate in it.
///
/// Spots are found by walking the route: every 'Spacing' metres, either side, a pad beyond the asphalt
/// edge, then dropped onto whatever ground is there. A spot is thrown away if there is nothing to stand
/// on (a drop, a bank too steep to hold it), if the ground is the road itself, or if something else is
/// already there - so nothing floats, nothing lands on the tarmac, and nothing is buried in a wall.
///
/// Usage: open a level scene (Core-1 ... Core-4), check the road list, then
///   Tools > Road Tools > Paint Roadside Props
/// </summary>
public class RoadsidePropsPainter : EditorWindow
{
    private const string PropsFolder = "Assets/Prefabs/Obstacles/Rigids/";
    private const string DefaultParentName = "Roadside Props";

    /// <summary>What a prop's surroundings have to look like for it to be worth placing.</summary>
    private enum Surroundings
    {
        Any,
        Forest,
        BuiltUp,
    }

    private class PropKind
    {
        public string label;
        public GameObject prefab;
        public Surroundings surroundings = Surroundings.Any;
        public bool enabled = true;
        public float weight = 1f;

        [Tooltip("How much of its own surroundings the spot needs before this prop may be used (0 - 1).")]
        public float minSurroundings;

        public Vector2 scaleRange = new Vector2(0.9f, 1.15f);

        // Preview colour and how many of each were placed, for the window's summary.
        public Color colour = Color.grey;
        public int placed;
    }

    private class PlanItem
    {
        public Vector3 position;
        public float yaw;
        public float scale;
        public PropKind kind;
        public float forest;
        public float town;
    }

    // ------------------------------------------------------------------ settings

    private readonly List<RoadRouteEntry> roads = new List<RoadRouteEntry>();
    private readonly List<PropKind> kinds = new List<PropKind>();

    // Where the props go
    private float spacing = 28f;
    private float spacingJitter = 0.45f;
    private float minPad = 0.6f;
    private float maxPad = 4.5f;
    private float lift = 0.02f;
    private float minSeparation = 2.5f;
    private int maxProps = 250;

    private int sideMode;                              // 0 both, 1 right, 2 left
    private static readonly string[] SideNames = { "Both sides", "Right of travel", "Left of travel" };

    // How the surroundings are read
    private float treeRadius = 25f;
    private float treeFullCount = 10f;
    private float structureRadius = 30f;
    private float structureFullCount = 6f;

    // How they sit
    private bool randomYaw = true;
    private float yawJitter = 35f;
    private float groundProbeHeight = 5f;
    private float groundMaxDrop = 15f;
    private float minGroundSlope = 0.75f;              // normal.y, 1 is flat

    // Route & options
    private float routeEndTrim = 20f;
    private string parentName = DefaultParentName;
    private bool showPreview = true;

    // ------------------------------------------------------------------ state

    private Vector2 scroll;
    private List<PlanItem> cachedPlan;
    private bool planDirty = true;
    private int rejectedSpots;
    private List<Vector2> trees;
    private List<Vector2> structures;

    [MenuItem("Tools/Road Tools/Paint Roadside Props")]
    static void OpenWindow()
    {
        RoadsidePropsPainter window = GetWindow<RoadsidePropsPainter>("Roadside Props");
        window.minSize = new Vector2(470, 700);
        window.Show();
    }

    void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
        UnityEditor.SceneManagement.EditorSceneManager.activeSceneChanged += OnActiveSceneChanged;

        if (roads.Count == 0)
            roads.AddRange(RoadRoute.FindRoadsInScene());

        if (kinds.Count == 0)
            BuildDefaultKinds();

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
        cachedPlan = null;
        trees = null;
        structures = null;
        planDirty = true;
        Repaint();
    }

    /// <summary>
    /// The project's own roadside props, with the surroundings each one belongs in. The min
    /// surroundings values are what keep logs out of the middle of a town and bins out of a forest.
    /// </summary>
    private void BuildDefaultKinds()
    {
        kinds.Clear();

        kinds.Add(MakeKind("Log", "Log", Surroundings.Forest, 1.2f, 0.35f, 0.9f, 1.15f, new Color(0.55f, 0.38f, 0.2f)));
        kinds.Add(MakeKind("Garbage", "Garbage", Surroundings.BuiltUp, 1f, 0.2f, 0.9f, 1.15f, new Color(0.45f, 0.75f, 0.35f)));
        kinds.Add(MakeKind("Bin", "TrashContainer", Surroundings.BuiltUp, 0.6f, 0.3f, 0.95f, 1.05f, new Color(0.25f, 0.55f, 0.3f)));
        kinds.Add(MakeKind("Barrels", "Barells", Surroundings.BuiltUp, 0.7f, 0.15f, 0.95f, 1.05f, new Color(0.85f, 0.5f, 0.2f)));
        kinds.Add(MakeKind("Cone", "Cone", Surroundings.Any, 0.9f, 0f, 0.9f, 1.1f, new Color(1f, 0.55f, 0.15f)));
        kinds.Add(MakeKind("Crate", "box", Surroundings.Any, 0.8f, 0f, 0.85f, 1.15f, new Color(0.75f, 0.65f, 0.45f)));
    }

    private PropKind MakeKind(string label, string prefabName, Surroundings surroundings, float weight,
                              float minSurroundings, float minScale, float maxScale, Color colour)
    {
        PropKind kind = new PropKind();
        kind.label = label;
        kind.prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PropsFolder + prefabName + ".prefab");
        kind.surroundings = surroundings;
        kind.weight = weight;
        kind.minSurroundings = minSurroundings;
        kind.scaleRange = new Vector2(minScale, maxScale);
        kind.colour = colour;
        return kind;
    }

    // ---------------------------------------------------------------- GUI ---

    void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        GUILayout.Label("Paint Roadside Props", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Scatters the rigid obstacle prefabs along the verge, never on the asphalt. Each spot is " +
            "scored on how wooded and how built up it is - from the terrain's trees and from the real " +
            "geometry around it - and the prop is picked from that, so logs turn up in the woods and " +
            "bins and barriers where the buildings are.",
            MessageType.Info);

        EditorGUI.BeginChangeCheck();

        EditorGUILayout.LabelField("Roads (in driving order)", EditorStyles.boldLabel);
        int usableRoads;
        if (RoadRoute.DrawRoadList(roads, out usableRoads))
            planDirty = true;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Props", EditorStyles.boldLabel);

        for (int i = 0; i < kinds.Count; i++)
        {
            PropKind kind = kinds[i];

            EditorGUILayout.BeginHorizontal();
            kind.enabled = EditorGUILayout.ToggleLeft(kind.label, kind.enabled, GUILayout.Width(80f));
            GUI.enabled = kind.enabled;

            Color previous = GUI.color;
            GUI.color = kind.colour;
            GUILayout.Label("\u25A0", GUILayout.Width(16f));
            GUI.color = previous;

            GUILayout.Label("Where", GUILayout.Width(42f));
            kind.surroundings = (Surroundings)EditorGUILayout.EnumPopup(kind.surroundings, GUILayout.Width(80f));

            GUILayout.Label("Weight", GUILayout.Width(46f));
            kind.weight = EditorGUILayout.FloatField(kind.weight, GUILayout.Width(40f));

            GUILayout.Label("Min", GUILayout.Width(26f));
            kind.minSurroundings = EditorGUILayout.Slider(kind.minSurroundings, 0f, 1f, GUILayout.Width(80f));

            GUILayout.Label("Scale", GUILayout.Width(38f));
            kind.scaleRange = EditorGUILayout.Vector2Field(GUIContent.none, kind.scaleRange, GUILayout.Width(110f));

            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Reload Project Props"))
        {
            BuildDefaultKinds();
            trees = null;
            structures = null;
            planDirty = true;
        }
        EditorGUILayout.LabelField("Min = how much of that kind of surroundings the spot needs", EditorStyles.miniLabel);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Where they go", EditorStyles.boldLabel);
        spacing = EditorGUILayout.Slider(
            new GUIContent("Spacing (m)", "Average distance along the road between two spots"),
            spacing, 5f, 200f);
        spacingJitter = EditorGUILayout.Slider(
            new GUIContent("Spacing Jitter", "How uneven the gaps are between spots (0.5 = halfway to double)"),
            spacingJitter, 0f, 0.9f);
        minPad = EditorGUILayout.Slider(
            new GUIContent("Min Past Road Edge (m)", "Closest a prop may sit to the asphalt edge"),
            minPad, 0f, 10f);
        maxPad = EditorGUILayout.Slider(
            new GUIContent("Max Past Road Edge (m)", "Furthest it may sit from the asphalt edge"),
            maxPad, 0f, 30f);
        sideMode = EditorGUILayout.Popup(new GUIContent("Side", "Which verge to use"), sideMode, SideNames);
        minSeparation = EditorGUILayout.Slider(
            new GUIContent("Min Separation (m)", "Smallest gap between two props, so they never overlap"),
            minSeparation, 0.5f, 15f);
        maxProps = EditorGUILayout.IntSlider(
            new GUIContent("Max Props", "Upper limit on how many are placed in one paint"),
            maxProps, 10, 1000);
        routeEndTrim = EditorGUILayout.Slider(
            new GUIContent("Trim Route Ends (m)", "Nothing is placed this close to either end of the route"),
            routeEndTrim, 0f, 300f);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Reading the surroundings", EditorStyles.boldLabel);
        treeRadius = EditorGUILayout.Slider(
            new GUIContent("Tree Radius (m)", "How far around a spot the terrain's trees are counted"),
            treeRadius, 5f, 80f);
        treeFullCount = EditorGUILayout.Slider(
            new GUIContent("Trees = Full Forest", "Trees within that radius that count as a full forest"),
            treeFullCount, 1f, 60f);
        structureRadius = EditorGUILayout.Slider(
            new GUIContent("Structure Radius (m)", "How far around a spot buildings and other geometry are counted"),
            structureRadius, 5f, 100f);
        structureFullCount = EditorGUILayout.Slider(
            new GUIContent("Structures = Full Town", "Structures within that radius that count as built up"),
            structureFullCount, 1f, 60f);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("How they sit", EditorStyles.boldLabel);
        randomYaw = EditorGUILayout.Toggle(
            new GUIContent("Random Facing", "Face every prop a random way round instead of roughly along the road"),
            randomYaw);
        yawJitter = EditorGUILayout.Slider(
            new GUIContent("Yaw Jitter (deg)", "How far off the road direction a prop may face when random facing is off"),
            yawJitter, 0f, 180f);
        minGroundSlope = EditorGUILayout.Slider(
            new GUIContent("Min Ground Flatness", "Steepest ground a prop may stand on (1 is perfectly flat)"),
            minGroundSlope, 0.3f, 1f);
        groundMaxDrop = EditorGUILayout.Slider(
            new GUIContent("Ground Search (m)", "How far below a spot the tool looks for ground before giving up"),
            groundMaxDrop, 1f, 60f);
        lift = EditorGUILayout.Slider(
            new GUIContent("Lift (m)", "How far above the ground each prop is placed. They are rigid bodies, " +
                                       "so a small lift just lets them settle"),
            lift, -0.2f, 1f);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Options", EditorStyles.boldLabel);
        parentName = EditorGUILayout.TextField("Root Object Name", parentName);
        showPreview = EditorGUILayout.Toggle(
            new GUIContent("Scene Preview", "Draw every planned prop in the scene view, colour coded by kind"),
            showPreview);

        if (EditorGUI.EndChangeCheck())
            planDirty = true;

        EditorGUILayout.Space();
        DrawSummary(usableRoads);

        EditorGUILayout.Space();
        GUI.backgroundColor = Color.green;
        if (GUILayout.Button("PAINT ROADSIDE PROPS", GUILayout.Height(40f))) Paint();
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space();
        GUI.backgroundColor = Color.red;
        if (GUILayout.Button("Remove Painted Props", GUILayout.Height(25f))) RemoveAll();
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

        bool anyEnabled = false;
        for (int i = 0; i < kinds.Count; i++)
            if (kinds[i].enabled && kinds[i].prefab != null) anyEnabled = true;

        if (total <= 0f)
        {
            EditorGUILayout.HelpBox(
                "No usable roads. Add a RoadArchitect road with a built spline to the list.",
                MessageType.Warning);
            return;
        }

        if (!anyEnabled)
        {
            EditorGUILayout.HelpBox("No props are switched on.", MessageType.Warning);
            return;
        }

        List<PlanItem> plan = GetPlan();

        if (plan == null || plan.Count == 0)
        {
            EditorGUILayout.HelpBox(
                "Nothing to place. Widen the pads, lower the ground flatness, or check that the props' " +
                "surroundings rules can be met in this level (a forest prop needs trees nearby).",
                MessageType.Warning);
            return;
        }

        string breakdown = "";
        for (int i = 0; i < kinds.Count; i++)
        {
            if (kinds[i].placed <= 0) continue;
            if (breakdown.Length > 0) breakdown += ", ";
            breakdown += kinds[i].label + " " + kinds[i].placed;
        }

        EditorGUILayout.LabelField("  Props: " + plan.Count + "   (" + breakdown + ")");
        EditorGUILayout.LabelField("  Spots rejected (no ground / no room): " + rejectedSpots);
        EditorGUILayout.LabelField("  Root object: '" + parentName + "' (replaced on every paint)");
    }

    // ---------------------------------------------------------- surroundings ---

    /// <summary>
    /// World XZ of every tree the terrain paints. Trees are instances on the terrain data rather than
    /// objects in the scene, so this is the only way to know where the woods really are.
    /// </summary>
    private static List<Vector2> CollectTrees()
    {
        List<Vector2> points = new List<Vector2>();
        Terrain[] terrains = UnityEngine.Object.FindObjectsOfType<Terrain>();

        for (int i = 0; i < terrains.Length; i++)
        {
            TerrainData data = terrains[i].terrainData;
            if (data == null) continue;

            Vector3 origin = terrains[i].transform.position;
            TreeInstance[] trees = data.treeInstances;

            for (int t = 0; t < trees.Length; t++)
            {
                Vector3 world = origin + Vector3.Scale(trees[t].position, data.size);
                points.Add(new Vector2(world.x, world.z));
            }
        }

        return points;
    }

    /// <summary>
    /// World XZ of the level's real structures: renderers that are not the road, not the terrain and
    /// not a prop this tool placed. Density of these is what "built up" means here, so the tool does
    /// not have to know how any particular building is named.
    /// </summary>
    private List<Vector2> CollectStructures()
    {
        List<Vector2> points = new List<Vector2>();
        GameObject propsRoot = RoadRoute.FindRootByName(parentName);
        MeshRenderer[] renderers = UnityEngine.Object.FindObjectsOfType<MeshRenderer>();

        for (int i = 0; i < renderers.Length; i++)
        {
            MeshRenderer renderer = renderers[i];
            if (renderer == null) continue;

            if (renderer.GetComponentInParent<Road>() != null) continue;
            if (renderer.GetComponentInParent<Terrain>() != null) continue;
            if (propsRoot != null && renderer.transform.IsChildOf(propsRoot.transform)) continue;

            // Sky domes, water sheets and other one-mesh backdrops are wider than a road is; they are
            // scenery, not buildings.
            Vector3 size = renderer.bounds.size;
            if (size.x > 60f || size.z > 60f || size.y > 60f) continue;

            points.Add(new Vector2(renderer.bounds.center.x, renderer.bounds.center.z));
        }

        return points;
    }

    private static int CountWithin(List<Vector2> points, Vector2 origin, float radius)
    {
        if (points == null || points.Count == 0) return 0;

        float squared = radius * radius;
        int count = 0;

        for (int i = 0; i < points.Count; i++)
        {
            Vector2 delta = points[i] - origin;
            if (delta.sqrMagnitude <= squared) count++;
        }

        return count;
    }

    // ---------------------------------------------------------------- plan ---

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
        rejectedSpots = 0;

        for (int i = 0; i < kinds.Count; i++)
            kinds[i].placed = 0;

        List<RoadRouteSegment> segments = RoadRoute.BuildSegments(roads);
        float total = RoadRoute.TotalLength(segments);
        if (total <= 0f) return plan;

        if (trees == null) trees = CollectTrees();
        if (structures == null) structures = CollectStructures();

        GameObject propsRoot = RoadRoute.FindRootByName(parentName);
        float from = routeEndTrim;
        float to = total - routeEndTrim;
        if (to <= from) return plan;

        bool preferRight = Random.value < 0.5f;
        float distance = from;

        while (distance < to && plan.Count < maxProps)
        {
            float step = spacing * Random.Range(1f - spacingJitter, 1f + spacingJitter);
            step = Mathf.Max(1f, step);
            distance += step;

            if (TryPlaceSpot(plan, segments, total, distance, propsRoot, ref preferRight))
                continue;

            rejectedSpots++;
        }

        return plan;
    }

    /// <summary>
    /// Tries one spot: picks a side and a pad, finds the ground, scores the surroundings, picks a prop
    /// and checks there is room for it. Returns false when the spot is unusable.
    /// </summary>
    private bool TryPlaceSpot(List<PlanItem> plan, List<RoadRouteSegment> segments, float total,
                              float distance, GameObject propsRoot, ref bool preferRight)
    {
        Vector3 position;
        Vector3 tangent;
        RoadRouteSegment segment = RoadRoute.SampleAt(segments, distance, out position, out tangent);
        if (segment == null) return false;

        // Mostly alternating sides, with the odd repeat, so the two verges are not a mirror image.
        bool firstRight;
        if (sideMode == 1) firstRight = true;
        else if (sideMode == 2) firstRight = false;
        else firstRight = Random.value < 0.75f ? !preferRight : preferRight;

        for (int attempt = 0; attempt < 2; attempt++)
        {
            // If one verge has nothing to stand on, the other may still work.
            bool right = attempt == 0 ? firstRight : !firstRight;

            float pad = Random.Range(Mathf.Min(minPad, maxPad), Mathf.Max(minPad, maxPad));
            Vector3 across = RoadRoute.Across(tangent);
            if (!right) across = -across;

            Vector3 spot = position + across * (RoadRoute.RoadEdge(segment.road) + pad);

            Vector3 ground;
            if (!RoadRoute.TryFindGround(spot, groundProbeHeight, groundMaxDrop, minGroundSlope, out ground))
                continue;

            if (!IsClear(ground, propsRoot)) return false;

            // Surroundings at the spot: how wooded, how built up.
            Vector2 flat = new Vector2(ground.x, ground.z);
            float forest = Mathf.Clamp01(CountWithin(trees, flat, treeRadius) / Mathf.Max(1f, treeFullCount));
            float town = Mathf.Clamp01(CountWithin(structures, flat, structureRadius) / Mathf.Max(1f, structureFullCount));

            PropKind kind = PickKind(forest, town);
            if (kind == null) return false;

            float scale = Random.Range(kind.scaleRange.x, kind.scaleRange.y);
            float radius = Mathf.Max(0.2f, PrefabMeasure.Of(kind.prefab).Radius) * scale;

            for (int i = 0; i < plan.Count; i++)
            {
                float needed = minSeparation + radius + Mathf.Max(0.2f, PrefabMeasure.Of(plan[i].kind.prefab).Radius) * plan[i].scale;
                Vector3 delta = plan[i].position - ground;
                delta.y = 0f;
                if (delta.sqrMagnitude < needed * needed) return false;
            }

            float yaw = randomYaw
                ? Random.Range(0f, 360f)
                : RoadRoute.YawAlong(tangent) + Random.Range(-yawJitter, yawJitter);

            PlanItem item = new PlanItem();
            item.position = ground + Vector3.up * lift;
            item.yaw = yaw;
            item.scale = scale;
            item.kind = kind;
            item.forest = forest;
            item.town = town;

            plan.Add(item);
            kind.placed++;
            preferRight = right;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Picks the prop for a spot from its surroundings: only props whose kind of surroundings are
    /// present are eligible, and among those the pick is weighted - and then jittered, so the same
    /// place is not always the same prop.
    /// </summary>
    private PropKind PickKind(float forest, float town)
    {
        List<PropKind> eligible = new List<PropKind>();
        List<float> scores = new List<float>();
        float totalWeight = 0f;

        for (int i = 0; i < kinds.Count; i++)
        {
            PropKind kind = kinds[i];
            if (!kind.enabled || kind.prefab == null) continue;

            float context = 0f;
            if (kind.surroundings == Surroundings.Forest) context = forest;
            else if (kind.surroundings == Surroundings.BuiltUp) context = town;

            if (context < kind.minSurroundings) continue;

            // A prop is more likely where its own surroundings are, but never certain - and the
            // roll is taken again for every spot, so the same place does not always give the same prop.
            float score = kind.weight * (0.25f + context) * Random.Range(0.5f, 1.5f);
            if (score <= 0f) continue;

            eligible.Add(kind);
            scores.Add(score);
            totalWeight += score;
        }

        if (eligible.Count == 0) return null;

        float pick = Random.Range(0f, totalWeight);
        for (int i = 0; i < eligible.Count; i++)
        {
            pick -= scores[i];
            if (pick <= 0f) return eligible[i];
        }

        return eligible[eligible.Count - 1];
    }

    /// <summary>True when nothing else is already standing where the prop would go.</summary>
    private static bool IsClear(Vector3 ground, GameObject propsRoot)
    {
        Collider[] hits = Physics.OverlapSphere(ground + Vector3.up * 0.6f, 0.5f, ~0,
                                                QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hits.Length; i++)
        {
            Collider hit = hits[i];
            if (hit == null) continue;

            if (hit.CompareTag("Road")) continue;
            if (hit.GetComponentInParent<Terrain>() != null) continue;
            if (propsRoot != null && hit.transform.IsChildOf(propsRoot.transform)) continue;

            return false;
        }

        return true;
    }

    // --------------------------------------------------------------- paint ---

    private void Paint()
    {
        List<PlanItem> plan = GetPlan();
        if (plan == null || plan.Count == 0)
        {
            Debug.LogError("Roadside Props: nothing to place. Check the road list, the pads and the " +
                           "props' surroundings rules.");
            return;
        }

        if (!EditorUtility.DisplayDialog("Paint Roadside Props",
            "Place " + plan.Count + " props along the verge?\n\n" +
            "An existing root object named '" + parentName + "' will be replaced.\n\n" +
            "This action can be undone (Ctrl+Z).",
            "Paint", "Cancel"))
        {
            return;
        }

        RemoveAllSilently();

        GameObject parent = new GameObject(parentName);
        Undo.RegisterCreatedObjectUndo(parent, "Create Roadside Props Root");

        int placed = 0;
        try
        {
            for (int i = 0; i < plan.Count; i++)
            {
                PlanItem item = plan[i];
                EditorUtility.DisplayProgressBar("Painting Roadside Props",
                    (i + 1) + " / " + plan.Count, (float)i / plan.Count);

                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(item.kind.prefab, parent.transform);
                if (instance == null) continue;

                Undo.RegisterCreatedObjectUndo(instance, "Create Roadside Prop");

                PrefabMeasure.Footprint footprint = PrefabMeasure.Of(item.kind.prefab);

                instance.name = item.kind.label + " " + (i + 1);
                instance.transform.localScale *= item.scale;
                instance.transform.rotation = Quaternion.Euler(0f, item.yaw, 0f);

                // The prefabs do not all have their pivot at their feet, so each one is dropped until
                // the lowest point of its geometry rests on the ground.
                instance.transform.position = new Vector3(
                    item.position.x,
                    item.position.y - footprint.Bottom * item.scale,
                    item.position.z);

                placed++;
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        RoadRoute.MarkSceneDirty();

        // The new props are part of the scene now, so the next plan has to look again at what is
        // around it - otherwise the tool would happily stack a second prop on top of one it just placed.
        trees = null;
        structures = null;

        Debug.Log("Roadside Props: placed " + placed + " props along the roadside (" +
                  rejectedSpots + " spots rejected).");
    }

    private void RemoveAll()
    {
        GameObject existing = RoadRoute.FindRootByName(parentName);
        if (existing == null)
        {
            Debug.Log("Roadside Props: no root object named '" + parentName + "'.");
            return;
        }

        if (!EditorUtility.DisplayDialog("Remove Roadside Props",
            "Remove the whole '" + parentName + "' hierarchy?", "Remove", "Cancel"))
        {
            return;
        }

        Undo.DestroyObjectImmediate(existing);
        RoadRoute.MarkSceneDirty();
        Debug.Log("Roadside Props: removed '" + parentName + "'.");
    }

    private void RemoveAllSilently()
    {
        GameObject existing = RoadRoute.FindRootByName(parentName);
        if (existing != null)
            Undo.DestroyObjectImmediate(existing);
    }

    // ---------------------------------------------------------- scene view ---

    private void OnSceneGUI(SceneView view)
    {
        if (!showPreview) return;

        List<PlanItem> plan = GetPlan();
        if (plan == null) return;

        for (int i = 0; i < plan.Count; i++)
        {
            PlanItem item = plan[i];

            Handles.color = item.kind.colour;
            float radius = Mathf.Max(0.25f, PrefabMeasure.Of(item.kind.prefab).Radius) * item.scale;

            Handles.DrawSolidDisc(item.position + Vector3.up * 0.05f, Vector3.up, radius);
            Handles.DrawWireDisc(item.position + Vector3.up * 0.05f, Vector3.up, radius);

            // A stick the height of the prop, so the scene view shows roughly what will be there.
            float height = Mathf.Max(0.4f, PrefabMeasure.Of(item.kind.prefab).Height) * item.scale;
            Handles.DrawAAPolyLine(2f, item.position, item.position + Vector3.up * height);
            Handles.Label(item.position + Vector3.up * (height + 0.6f),
                          item.kind.label + "\nforest " + item.forest.ToString("F2") +
                          "\ntown " + item.town.ToString("F2"));
        }
    }
}
