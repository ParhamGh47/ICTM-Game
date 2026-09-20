using System.Collections.Generic;
using RoadArchitect;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor tool that puts the project's roadside signs and roadside obstacles along a route, each one where
/// it makes sense rather than evenly:
///
///  - A Stop sign belongs at a junction, so the tool finds junctions itself - places along the route that
///    come within a few metres of some other road's centre line - and stands a sign just before each one,
///    on the verge, facing the traffic that is about to reach it.
///  - A Share The Road sign is an ordinary verge sign: it turns up anywhere along the road, a little more
///    often where the level is built up.
///  - The mast (a traffic light base) is a tall structure, so it stands further off the asphalt and the
///    surroundings rule keeps it where there is something for it to belong to.
///  - The rumble strip is the odd one out: it goes ON the asphalt, flat along the lane, and only where the
///    level is built up - a strip of it before the building site, which is how Core-1 uses it by hand.
///
/// The models are imported Z-up turned Y-up, so in Unity a sign's face is its +-Z, its base sits on its own
/// pivot, and the rumble strip is already flat with its length along Z. That is what the yaw here is built
/// on: a verge sign is turned to face back down the road, an on-road strip to run along it. Each kind has a
/// 'Flip' tick for a model that comes out the wrong way round.
///
/// Usage: open a level scene (Core-1 ... Core-4), check the road list, then
///   Tools > Road Tools > Paint Roadside Signs
/// </summary>
public class RoadsideSignsPainter : EditorWindow
{
    private const string SignsFolder = "Assets/Prefabs/Signs/Signs/";
    private const string ObstaclesFolder = "Assets/Prefabs/Obstacles/";
    private const string DefaultParentName = "Roadside Signs";

    /// <summary>What the surroundings have to look like for a kind to be worth placing.</summary>
    private enum Where
    {
        Any,
        BuiltUp,
        Forest,
    }

    /// <summary>Where a kind sits relative to the road.</summary>
    private enum Placement
    {
        Verge,
        OnRoad,
        Junction,
    }

    private class SignKind
    {
        public string label;
        public GameObject prefab;
        public Placement placement = Placement.Verge;
        public Where where = Where.Any;
        public bool enabled = true;
        public float weight = 1f;

        [Tooltip("How much of its surroundings the spot needs before this kind may be used (0 - 1).")]
        public float minSurroundings;

        [Tooltip("How far past the asphalt edge it stands, for the kinds that stand beside the road.")]
        public Vector2 padRange = new Vector2(1f, 4f);

        [Tooltip("Uniform scale the placed object is multiplied by.")]
        public Vector2 scaleRange = new Vector2(1f, 1f);

        [Tooltip("Turn it the other way round if the model faces backwards.")]
        public bool flipFacing;

        public Color colour = Color.grey;
        public int placed;
    }

    private class PlanItem
    {
        public Vector3 position;
        public float yaw;
        public float scale;
        public SignKind kind;
        public bool junction;
        public float forest;
        public float town;
    }

    // ------------------------------------------------------------------ settings

    private readonly List<RoadRouteEntry> roads = new List<RoadRouteEntry>();
    private readonly List<SignKind> kinds = new List<SignKind>();

    private float vergeSpacing = 45f;
    private float spacingJitter = 0.5f;
    private float onRoadSpacing = 80f;
    private float minSeparation = 6f;
    private int maxItems = 120;
    private int sideMode;                               // 0 both, 1 right, 2 left
    private static readonly string[] SideNames = { "Both sides", "Right of travel", "Left of travel" };

    private float minPad = 0.5f;
    private float maxPad = 12f;
    private float lift = 0.03f;

    // Junctions
    private float junctionRadius = 10f;
    private Vector2 stopBefore = new Vector2(6f, 14f);
    private int junctionSigns = 1;
    private float junctionSeparation = 5f;
    private bool junctionBothSides;

    // Reading the surroundings
    private float treeRadius = 25f;
    private float treeFullCount = 10f;
    private float structureRadius = 30f;
    private float structureFullCount = 6f;

    // How they sit
    private float facingOffset = 0f;
    private float onRoadYaw = 0f;                       // 0 runs a strip along the lane, 90 across it
    private float groundProbeHeight = 5f;
    private float groundMaxDrop = 15f;
    private float minGroundSlope = 0.75f;

    // Options
    private float routeEndTrim = 20f;
    private string parentName = DefaultParentName;
    private bool showPreview = true;

    // ------------------------------------------------------------------ state

    private Vector2 scroll;
    private List<PlanItem> cachedPlan;
    private bool planDirty = true;
    private int rejectedSpots;
    private int junctionCount;
    private List<Vector2> trees;
    private List<Vector2> structures;
    private GameObject[] painterRoots = new GameObject[0];

    [MenuItem("Tools/Road Tools/Paint Roadside Signs")]
    static void OpenWindow()
    {
        RoadsideSignsPainter window = GetWindow<RoadsideSignsPainter>("Roadside Signs");
        window.minSize = new Vector2(500, 720);
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

    private void OnActiveSceneChanged(UnityEngine.SceneManagement.Scene previous,
                                      UnityEngine.SceneManagement.Scene current)
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
    /// The project's signs and roadside obstacles, each with where it belongs. The pads and surroundings
    /// rules are what keep a stop sign off a straight and a traffic light base out of a field.
    /// </summary>
    private void BuildDefaultKinds()
    {
        kinds.Clear();

        kinds.Add(MakeKind("Stop", SignsFolder + "Stop", Placement.Junction, Where.Any, 1f, 0f,
                           new Vector2(1.2f, 3.5f), new Vector2(1f, 1f), new Color(0.85f, 0.2f, 0.2f)));

        kinds.Add(MakeKind("Share The Road", SignsFolder + "ShareTheRoad", Placement.Verge, Where.Any, 0.9f, 0f,
                           new Vector2(1.5f, 4.5f), new Vector2(1f, 1f), new Color(0.95f, 0.75f, 0.25f)));

        kinds.Add(MakeKind("Mast", ObstaclesFolder + "Mast", Placement.Verge, Where.BuiltUp, 0.5f, 0.25f,
                           new Vector2(4f, 9f), new Vector2(1f, 1f), new Color(0.6f, 0.6f, 0.7f)));

        kinds.Add(MakeKind("Rumble", ObstaclesFolder + "Rumble", Placement.OnRoad, Where.BuiltUp, 1f, 0.25f,
                           Vector2.zero, new Vector2(0.35f, 0.6f), new Color(0.35f, 0.35f, 0.35f)));
    }

    private SignKind MakeKind(string label, string prefabPath, Placement placement, Where where, float weight,
                              float minSurroundings, Vector2 padRange, Vector2 scaleRange, Color colour)
    {
        SignKind kind = new SignKind();
        kind.label = label;
        kind.prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath + ".prefab");
        kind.placement = placement;
        kind.where = where;
        kind.weight = weight;
        kind.minSurroundings = minSurroundings;
        kind.padRange = padRange;
        kind.scaleRange = scaleRange;
        kind.colour = colour;
        return kind;
    }

    // ---------------------------------------------------------------- GUI ---

    void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        GUILayout.Label("Paint Roadside Signs", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Places the signs and roadside obstacles where they belong: Stop signs before junctions (the tool " +
            "finds them by looking for other roads crossing the route), Share The Road signs along the verge, " +
            "the mast further out where it is built up, and the rumble strip flat on the asphalt before a " +
            "building site. Nothing is ever placed on the road except a kind that asks for it.",
            MessageType.Info);

        EditorGUI.BeginChangeCheck();

        EditorGUILayout.LabelField("Roads (in driving order)", EditorStyles.boldLabel);
        int usableRoads;
        if (RoadRoute.DrawRoadList(roads, out usableRoads))
            planDirty = true;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Signs and obstacles", EditorStyles.boldLabel);

        for (int i = 0; i < kinds.Count; i++)
        {
            SignKind kind = kinds[i];

            EditorGUILayout.BeginHorizontal();
            kind.enabled = EditorGUILayout.ToggleLeft(kind.label, kind.enabled, GUILayout.Width(110f));
            GUI.enabled = kind.enabled;

            Color previous = GUI.color;
            GUI.color = kind.colour;
            GUILayout.Label("\u25A0", GUILayout.Width(16f));
            GUI.color = previous;

            kind.prefab = (GameObject)EditorGUILayout.ObjectField(kind.prefab, typeof(GameObject), false);
            GUILayout.Label("Place", GUILayout.Width(38f));
            kind.placement = (Placement)EditorGUILayout.EnumPopup(kind.placement, GUILayout.Width(78f));
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(130f);
            GUI.enabled = kind.enabled;

            GUILayout.Label("Where", GUILayout.Width(44f));
            kind.where = (Where)EditorGUILayout.EnumPopup(kind.where, GUILayout.Width(78f));
            GUILayout.Label("Weight", GUILayout.Width(46f));
            kind.weight = EditorGUILayout.FloatField(kind.weight, GUILayout.Width(36f));
            GUILayout.Label("Min", GUILayout.Width(26f));
            kind.minSurroundings = EditorGUILayout.Slider(kind.minSurroundings, 0f, 1f, GUILayout.Width(70f));

            GUI.enabled = kind.enabled && kind.placement != Placement.OnRoad;
            GUILayout.Label("Pad", GUILayout.Width(26f));
            kind.padRange = EditorGUILayout.Vector2Field(GUIContent.none, kind.padRange, GUILayout.Width(100f));
            GUI.enabled = kind.enabled;

            GUILayout.Label("Scale", GUILayout.Width(38f));
            kind.scaleRange = EditorGUILayout.Vector2Field(GUIContent.none, kind.scaleRange, GUILayout.Width(100f));
            kind.flipFacing = EditorGUILayout.ToggleLeft(
                new GUIContent("Flip", "Turn this kind the other way round"), kind.flipFacing, GUILayout.Width(52f));

            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Reload Project Signs"))
        {
            BuildDefaultKinds();
            trees = null;
            structures = null;
            planDirty = true;
        }
        EditorGUILayout.LabelField("Place: Verge stands beside the road, OnRoad lies on the asphalt, " +
                                   "Junction goes before a junction", EditorStyles.miniLabel);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Spacing", EditorStyles.boldLabel);
        vergeSpacing = EditorGUILayout.Slider(
            new GUIContent("Verge Spacing (m)", "Average gap between verge signs along the road"),
            vergeSpacing, 10f, 400f);
        onRoadSpacing = EditorGUILayout.Slider(
            new GUIContent("On-Road Spacing (m)", "Average gap between the strips that lie on the asphalt"),
            onRoadSpacing, 10f, 600f);
        spacingJitter = EditorGUILayout.Slider(
            new GUIContent("Spacing Jitter", "How uneven the gaps are (0.5 = halfway to double)"),
            spacingJitter, 0f, 0.9f);
        sideMode = EditorGUILayout.Popup(new GUIContent("Side", "Which verge to use"), sideMode, SideNames);
        minSeparation = EditorGUILayout.Slider(
            new GUIContent("Min Separation (m)", "Smallest gap between two placements, so they never overlap"),
            minSeparation, 1f, 40f);
        maxItems = EditorGUILayout.IntSlider(
            new GUIContent("Max Objects", "Upper limit on how many are placed in one paint"),
            maxItems, 5, 600);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Junctions (for the kinds set to Junction)", EditorStyles.boldLabel);
        junctionRadius = EditorGUILayout.Slider(
            new GUIContent("Junction Radius (m)", "How close another road has to come to the route to count " +
                                                  "as a junction. Around 10 m finds real junctions; raise it " +
                                                  "where two road ends meet across a gap"),
            junctionRadius, 3f, 50f);
        stopBefore = EditorGUILayout.Vector2Field(
            new GUIContent("Stop Before (m)", "How far before the junction the sign stands (a range is picked from)"),
            stopBefore);
        junctionSigns = EditorGUILayout.IntSlider(
            new GUIContent("Signs Per Junction", "How many signs to stand before each junction"),
            junctionSigns, 1, 4);
        junctionSeparation = EditorGUILayout.Slider(
            new GUIContent("Sign Gap (m)", "Spacing between those signs, furthest from the junction first"),
            junctionSeparation, 2f, 30f);
        junctionBothSides = EditorGUILayout.Toggle(
            new GUIContent("Both Sides", "Stand a sign on each verge at a junction instead of one"),
            junctionBothSides);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Where they stand", EditorStyles.boldLabel);
        minPad = EditorGUILayout.Slider(
            new GUIContent("Min Past Road Edge (m)", "A blanket limit: nothing stands closer to the asphalt " +
                                                      "than this, whatever its own Pad below asks for"),
            minPad, 0f, 20f);
        maxPad = EditorGUILayout.Slider(
            new GUIContent("Max Past Road Edge (m)", "And nothing stands further out than this. Each kind's own " +
                                                      "Pad is the one that actually places it, kept inside " +
                                                      "these two"),
            maxPad, 0f, 40f);
        minGroundSlope = EditorGUILayout.Slider(
            new GUIContent("Min Ground Flatness", "Steepest ground anything may stand on (1 is perfectly flat)"),
            minGroundSlope, 0.3f, 1f);
        groundMaxDrop = EditorGUILayout.Slider(
            new GUIContent("Ground Search (m)", "How far below a spot the tool looks for ground before giving up"),
            groundMaxDrop, 1f, 60f);
        lift = EditorGUILayout.Slider(
            new GUIContent("Lift (m)", "How far above the ground each one is placed. On-road strips need a " +
                                       "little of this so they do not fight with the asphalt"),
            lift, 0f, 0.5f);

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
        EditorGUILayout.LabelField("Facing", EditorStyles.boldLabel);
        facingOffset = EditorGUILayout.Slider(
            new GUIContent("Facing Offset (deg)", "Nudge every object's facing by this much if the models do not " +
                                                  "sit square to the road"),
            facingOffset, -180f, 180f);
        onRoadYaw = EditorGUILayout.Slider(
            new GUIContent("On-Road Strips (deg)", "0 lays a strip along the lane, 90 across it"),
            onRoadYaw, 0f, 180f);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Options", EditorStyles.boldLabel);
        routeEndTrim = EditorGUILayout.Slider(
            new GUIContent("Trim Route Ends (m)", "Nothing is placed this close to either end of the route"),
            routeEndTrim, 0f, 300f);
        parentName = EditorGUILayout.TextField("Root Object Name", parentName);
        showPreview = EditorGUILayout.Toggle(
            new GUIContent("Scene Preview", "Draw every planned placement in the scene view"), showPreview);

        if (EditorGUI.EndChangeCheck())
            planDirty = true;

        EditorGUILayout.Space();
        DrawSummary(usableRoads);

        EditorGUILayout.Space();
        GUI.backgroundColor = Color.green;
        if (GUILayout.Button("PAINT ROADSIDE SIGNS", GUILayout.Height(40f))) Paint();
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space();
        GUI.backgroundColor = Color.red;
        if (GUILayout.Button("Remove Painted Signs", GUILayout.Height(25f))) RemoveAll();
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

        if (total <= 0f)
        {
            EditorGUILayout.HelpBox(
                "No usable roads. Add a RoadArchitect road with a built spline to the list.",
                MessageType.Warning);
            return;
        }

        List<PlanItem> plan = GetPlan();

        if (plan == null || plan.Count == 0)
        {
            EditorGUILayout.HelpBox(
                "Nothing to place. Check the pads, the ground flatness and the surroundings rules - a kind set " +
                "to BuiltUp needs buildings nearby, one set to Forest needs trees.",
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

        EditorGUILayout.LabelField("  Junctions found: " + junctionCount);
        EditorGUILayout.LabelField("  Objects: " + plan.Count + "   (" + breakdown + ")");
        EditorGUILayout.LabelField("  Spots rejected (no ground / no room): " + rejectedSpots);
        EditorGUILayout.LabelField("  Root object: '" + parentName + "' (replaced on every paint)");
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
        junctionCount = 0;

        for (int i = 0; i < kinds.Count; i++)
            kinds[i].placed = 0;

        List<RoadRouteSegment> segments = RoadRoute.BuildSegments(roads);
        float total = RoadRoute.TotalLength(segments);
        if (total <= 0f) return plan;

        painterRoots = RoadRoute.PainterRoots();

        // Every painter's own work is left out of the surroundings reading, so a level that has already been
        // painted once cannot make the next pass believe a town has grown up around the tool's own bins.
        if (trees == null) trees = RoadRoute.TreePoints();
        if (structures == null) structures = RoadRoute.StructurePoints(painterRoots);

        float from = routeEndTrim;
        float to = total - routeEndTrim;
        if (to <= from) return plan;

        // 1. The verge signs, walking the route.
        if (AnyKind(Placement.Verge))
        {
            bool preferRight = Random.value < 0.5f;
            float distance = from;

            while (distance < to && plan.Count < maxItems)
            {
                distance += Mathf.Max(5f, vergeSpacing * Random.Range(1f - spacingJitter, 1f + spacingJitter));
                if (!TryVergeSpot(plan, segments, distance, 0, false, ref preferRight)) rejectedSpots++;
            }
        }

        // 2. The strips that lie on the asphalt.
        if (AnyKind(Placement.OnRoad))
        {
            float distance = from + onRoadSpacing * 0.5f;

            while (distance < to && plan.Count < maxItems)
            {
                distance += Mathf.Max(8f, onRoadSpacing * Random.Range(1f - spacingJitter, 1f + spacingJitter));
                if (!TryOnRoadSpot(plan, segments, distance)) rejectedSpots++;
            }
        }

        // 3. The junctions, each with its sign(s) a little before it.
        if (AnyKind(Placement.Junction))
        {
            List<float> junctions = FindJunctions(segments);
            junctionCount = junctions.Count;

            bool preferRight = Random.value < 0.5f;

            for (int i = 0; i < junctions.Count && plan.Count < maxItems; i++)
            {
                for (int s = 0; s < junctionSigns && plan.Count < maxItems; s++)
                {
                    float distance = junctions[i] - Random.Range(stopBefore.x, stopBefore.y) - s * junctionSeparation;
                    if (distance <= from) continue;

                    if (junctionBothSides)
                    {
                        TryVergeSpot(plan, segments, distance, 1, true, ref preferRight);
                        if (plan.Count < maxItems)
                            TryVergeSpot(plan, segments, distance, 2, true, ref preferRight);
                    }
                    else if (!TryVergeSpot(plan, segments, distance, 0, true, ref preferRight))
                    {
                        rejectedSpots++;
                    }
                }
            }
        }

        return plan;
    }

    private bool AnyKind(Placement placement)
    {
        for (int i = 0; i < kinds.Count; i++)
            if (kinds[i].enabled && kinds[i].prefab != null && kinds[i].placement == placement) return true;

        return false;
    }

    /// <summary>
    /// Places one spot on a verge: picks a side and a pad, finds the ground, reads the surroundings, picks
    /// a kind from them and checks there is room. <paramref name="side"/> is 0 to alternate, 1 right, 2 left.
    /// </summary>
    private bool TryVergeSpot(List<PlanItem> plan, List<RoadRouteSegment> segments, float distance, int side,
                              bool junction, ref bool preferRight)
    {
        Vector3 position;
        Vector3 tangent;
        RoadRouteSegment segment = RoadRoute.SampleAt(segments, distance, out position, out tangent);
        if (segment == null) return false;

        bool firstRight;
        if (side == 1) firstRight = true;
        else if (side == 2) firstRight = false;
        else firstRight = Random.value < 0.75f ? !preferRight : preferRight;

        // What is around the *road* is read first, because the kind decides how far out it wants to stand and
        // so has to be known before the spot exists. The reading is 25-30 m wide and the pads are a few
        // metres, so measuring at the centre line rather than at the finished spot changes nothing.
        Vector2 flat = new Vector2(position.x, position.z);
        float forest = RoadRoute.Density(trees, flat, treeRadius, treeFullCount);
        float town = RoadRoute.Density(structures, flat, structureRadius, structureFullCount);

        SignKind kind = PickKind(junction ? Placement.Junction : Placement.Verge, forest, town);
        if (kind == null) return false;

        for (int attempt = 0; attempt < 2; attempt++)
        {
            // If one verge has nothing to stand on, the other may still work.
            bool right = attempt == 0 ? firstRight : !firstRight;

            // The kind's own pad, kept inside the overall limits above so the sliders still mean something.
            float pad = Mathf.Clamp(
                Random.Range(Mathf.Min(kind.padRange.x, kind.padRange.y),
                             Mathf.Max(kind.padRange.x, kind.padRange.y)),
                Mathf.Min(minPad, maxPad), Mathf.Max(minPad, maxPad));

            Vector3 across = RoadRoute.Across(tangent);
            if (!right) across = -across;

            Vector3 spot = position + across * (RoadRoute.RoadEdge(segment.road) + pad);

            Vector3 ground;
            if (!RoadRoute.TryFindGround(spot, groundProbeHeight, groundMaxDrop, minGroundSlope, out ground))
                continue;

            if (!IsClear(ground)) return false;

            float scale = Random.Range(kind.scaleRange.x, kind.scaleRange.y);
            if (!HasRoom(plan, ground, kind, scale)) return false;

            PlanItem item = new PlanItem();
            item.position = ground + Vector3.up * lift;
            item.yaw = YawFor(kind, tangent, false);
            item.scale = scale;
            item.kind = kind;
            item.junction = junction;
            item.forest = forest;
            item.town = town;

            plan.Add(item);
            kind.placed++;
            preferRight = right;
            return true;
        }

        return false;
    }

    /// <summary>Places one spot on the asphalt itself: only kinds that ask for the road go here.</summary>
    private bool TryOnRoadSpot(List<PlanItem> plan, List<RoadRouteSegment> segments, float distance)
    {
        Vector3 position;
        Vector3 tangent;
        RoadRouteSegment segment = RoadRoute.SampleAt(segments, distance, out position, out tangent);
        if (segment == null) return false;

        float half = Mathf.Max(1.5f, segment.road.RoadWidth() * 0.5f);
        float lateral = Random.Range(-(half - 1f), half - 1f);

        Vector3 spot = position + RoadRoute.Across(tangent) * lateral;

        Vector3 ground;
        if (!TryFindRoadSurface(spot, out ground)) return false;

        Vector2 flat = new Vector2(ground.x, ground.z);
        float forest = RoadRoute.Density(trees, flat, treeRadius, treeFullCount);
        float town = RoadRoute.Density(structures, flat, structureRadius, structureFullCount);

        SignKind kind = PickKind(Placement.OnRoad, forest, town);
        if (kind == null) return false;

        float scale = Random.Range(kind.scaleRange.x, kind.scaleRange.y);
        if (!HasRoom(plan, ground, kind, scale)) return false;
        if (!IsClear(ground)) return false;

        PlanItem item = new PlanItem();
        item.position = ground + Vector3.up * lift;
        item.yaw = YawFor(kind, tangent, true);
        item.scale = scale;
        item.kind = kind;
        item.forest = forest;
        item.town = town;

        plan.Add(item);
        kind.placed++;
        return true;
    }

    /// <summary>
    /// True when the road itself is under the spot, which is what "on the asphalt" means here. The road is
    /// recognised by the RoadArchitect road the collider belongs to rather than by a tag, because the levels'
    /// roads are not tagged - Road1 and everything built under it, mesh colliders included, are untagged.
    /// </summary>
    private static bool TryFindRoadSurface(Vector3 point, out Vector3 ground)
    {
        ground = point;

        RaycastHit hit;
        if (!Physics.Raycast(point + Vector3.up * 6f, Vector3.down, out hit, 40f, ~0, QueryTriggerInteraction.Ignore))
            return false;

        if (hit.collider == null || hit.collider.GetComponentInParent<Road>() == null) return false;

        ground = hit.point;
        return true;
    }

    /// <summary>
    /// Picks the kind for a spot: only kinds that belong at that sort of spot and whose surroundings are
    /// present are eligible, and among those the pick is weighted - then jittered, so the same place is not
    /// always the same sign.
    /// </summary>
    private SignKind PickKind(Placement placement, float forest, float town)
    {
        List<SignKind> eligible = new List<SignKind>();
        List<float> scores = new List<float>();
        float totalWeight = 0f;

        for (int i = 0; i < kinds.Count; i++)
        {
            SignKind kind = kinds[i];
            if (!kind.enabled || kind.prefab == null || kind.placement != placement) continue;

            float context = 0f;
            if (kind.where == Where.Forest) context = forest;
            else if (kind.where == Where.BuiltUp) context = town;

            if (context < kind.minSurroundings) continue;

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

    /// <summary>
    /// Which way an object is turned. A sign faces back down the road so the traffic coming at it can read
    /// it; a strip on the asphalt runs along the lane instead. The model's own face is its +-Z, so a sign
    /// needs the half turn and a kind whose model is the other way round asks for 'Flip'.
    /// </summary>
    private float YawFor(SignKind kind, Vector3 tangent, bool onRoad)
    {
        float yaw = RoadRoute.YawAlong(tangent);

        if (onRoad) yaw += onRoadYaw;
        else yaw += 180f;

        if (kind.flipFacing) yaw += 180f;

        return yaw + facingOffset;
    }

    /// <summary>True when nothing else is already standing where this would go.</summary>
    private bool IsClear(Vector3 ground)
    {
        Collider[] hits = Physics.OverlapSphere(ground + Vector3.up * 0.6f, 0.5f, ~0,
                                                QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hits.Length; i++)
        {
            Collider hit = hits[i];
            if (hit == null) continue;

            // The road and the terrain are ground, not obstacles - and a kind set to OnRoad is asking for
            // the asphalt, so the road has to count as free or its own placement could never pass this.
            if (hit.GetComponentInParent<Road>() != null) continue;
            if (hit.GetComponentInParent<Terrain>() != null) continue;

            bool ours = false;
            for (int r = 0; r < painterRoots.Length; r++)
                if (painterRoots[r] != null && hit.transform.IsChildOf(painterRoots[r].transform)) ours = true;

            // The tool's own roots count as free space: a repaint replaces them anyway, and this is what
            // stops one sign from blocking the place the next one wants.
            if (ours) continue;

            return false;
        }

        return true;
    }

    private bool HasRoom(List<PlanItem> plan, Vector3 ground, SignKind kind, float scale)
    {
        float radius = Mathf.Max(0.3f, PrefabMeasure.Of(kind.prefab).Radius) * scale;

        for (int i = 0; i < plan.Count; i++)
        {
            float needed = minSeparation +
                           radius +
                           Mathf.Max(0.3f, PrefabMeasure.Of(plan[i].kind.prefab).Radius) * plan[i].scale;

            Vector3 delta = plan[i].position - ground;
            delta.y = 0f;
            if (delta.sqrMagnitude < needed * needed) return false;
        }

        return true;
    }

    // ----------------------------------------------------------- junctions ---

    /// <summary>
    /// Route distances at which the route meets another road. Every road that is not part of the route is
    /// sampled into a cloud of points, and any stretch of the route with one of those points within the
    /// junction radius is a junction - the whole stretch counts once, not once per sample.
    /// </summary>
    private List<float> FindJunctions(List<RoadRouteSegment> segments)
    {
        List<float> junctions = new List<float>();

        List<Vector2> others = OtherRoadPoints(segments);
        if (others.Count == 0) return junctions;

        float total = RoadRoute.TotalLength(segments);
        float step = Mathf.Max(2f, junctionRadius * 0.5f);
        float distance = routeEndTrim;

        while (distance < total - routeEndTrim)
        {
            Vector3 position;
            Vector3 tangent;
            RoadRoute.SampleAt(segments, distance, out position, out tangent);

            Vector2 flat = new Vector2(position.x, position.z);

            if (RoadRoute.CountWithin(others, flat, junctionRadius) > 0)
            {
                junctions.Add(distance);
                distance += junctionRadius * 2f;            // past this junction before looking again
            }
            else
            {
                distance += step;
            }
        }

        return junctions;
    }

    private List<Vector2> OtherRoadPoints(List<RoadRouteSegment> segments)
    {
        List<Vector2> points = new List<Vector2>();
        Road[] roadsInScene = FindObjectsOfType<Road>();

        for (int i = 0; i < roadsInScene.Length; i++)
        {
            Road road = roadsInScene[i];
            if (road == null || road.spline == null || road.spline.distance <= 0.01f) continue;
            if (OnRoute(segments, road)) continue;

            int steps = Mathf.Clamp(Mathf.CeilToInt(road.spline.distance / 4f), 2, 4000);

            for (int s = 0; s <= steps; s++)
            {
                Vector3 position;
                Vector3 tangent;
                road.spline.GetSplineValueBoth((float)s / steps, out position, out tangent);
                points.Add(new Vector2(position.x, position.z));
            }
        }

        return points;
    }

    private static bool OnRoute(List<RoadRouteSegment> segments, Road road)
    {
        for (int i = 0; i < segments.Count; i++)
            if (segments[i].road == road) return true;

        return false;
    }

    // --------------------------------------------------------------- paint ---

    private void Paint()
    {
        List<PlanItem> plan = GetPlan();
        if (plan == null || plan.Count == 0)
        {
            Debug.LogError("Roadside Signs: nothing to place. Check the road list, the pads and the " +
                           "surroundings rules.");
            return;
        }

        if (!EditorUtility.DisplayDialog("Paint Roadside Signs",
            "Place " + plan.Count + " signs and obstacles along the road?\n\n" +
            "An existing root object named '" + parentName + "' will be replaced.\n\n" +
            "This action can be undone (Ctrl+Z).",
            "Paint", "Cancel"))
        {
            return;
        }

        RemoveAllSilently();

        GameObject parent = new GameObject(parentName);
        Undo.RegisterCreatedObjectUndo(parent, "Create Roadside Signs Root");

        int placed = 0;
        try
        {
            for (int i = 0; i < plan.Count; i++)
            {
                PlanItem item = plan[i];
                EditorUtility.DisplayProgressBar("Painting Roadside Signs",
                    (i + 1) + " / " + plan.Count, (float)i / plan.Count);

                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(item.kind.prefab, parent.transform);
                if (instance == null) continue;

                Undo.RegisterCreatedObjectUndo(instance, "Create Roadside Sign");

                PrefabMeasure.Footprint footprint = PrefabMeasure.Of(item.kind.prefab);

                instance.name = item.kind.label + " " + (i + 1);
                if (!Mathf.Approximately(item.scale, 1f)) instance.transform.localScale *= item.scale;
                instance.transform.rotation = Quaternion.Euler(0f, item.yaw, 0f);

                // None of these prefabs is guaranteed to have its pivot at its feet, so each one is dropped
                // until the lowest point of its geometry rests on the ground.
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

        // What is around a spot changes once the signs are in the scene, so the next plan has to look again.
        trees = null;
        structures = null;

        Debug.Log("Roadside Signs: placed " + placed + " objects along the road (" + junctionCount +
                  " junctions, " + rejectedSpots + " spots rejected).");
    }

    private void RemoveAll()
    {
        GameObject existing = RoadRoute.FindRootByName(parentName);
        if (existing == null)
        {
            Debug.Log("Roadside Signs: no root object named '" + parentName + "'.");
            return;
        }

        if (!EditorUtility.DisplayDialog("Remove Roadside Signs",
            "Remove the whole '" + parentName + "' hierarchy?", "Remove", "Cancel"))
        {
            return;
        }

        Undo.DestroyObjectImmediate(existing);
        RoadRoute.MarkSceneDirty();
        Debug.Log("Roadside Signs: removed '" + parentName + "'.");
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
            PrefabMeasure.Footprint footprint = PrefabMeasure.Of(item.kind.prefab);

            Handles.color = item.kind.colour;
            float across = Mathf.Max(0.25f, footprint.Radius) * item.scale;
            float along = across;

            // A disc for the footprint, a line for which way the thing lies, and a stick for how tall it is.
            Handles.DrawWireDisc(item.position + Vector3.up * 0.05f, Vector3.up, Mathf.Min(across, 6f));

            float yaw = item.yaw * Mathf.Deg2Rad;
            Vector3 forward = new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));
            Handles.DrawAAPolyLine(3f, item.position - forward * along, item.position + forward * along);

            float height = Mathf.Max(0.4f, footprint.Height * item.scale);
            if (item.kind.placement != Placement.OnRoad)
                Handles.DrawAAPolyLine(2f, item.position, item.position + Vector3.up * height);

            Handles.Label(item.position + Vector3.up * (height + 0.6f),
                          item.kind.label + (item.junction ? " (junction)" : "") +
                          "\nforest " + item.forest.ToString("F2") + "  town " + item.town.ToString("F2"));
        }
    }
}
