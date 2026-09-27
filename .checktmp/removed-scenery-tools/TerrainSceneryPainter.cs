using System.Collections.Generic;
using RoadArchitect;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Editor tool that fills the land either side of the road with scenery, so a level stops being a track
/// running through nothing, and does it in the only way that is affordable at this scale: as terrain trees.
///
/// The terrain's own tree system is the part of Unity that is built for this. It draws its trees instanced,
/// culls them in patches, keeps only a handful at full mesh and turns the rest into billboards past its
/// billboard distance - so ten thousand of them cost what a few hundred GameObjects would. Everything painted
/// here is one of those: no colliders (the levels already wall the player onto the corridor, and nothing off
/// it needs to be solid), no shadows unless the prototype was built with them, and a single draw of a quad
/// once it is far enough away. What a level pays for is set by the graphics presets, which pull the trees'
/// distance and billboard distance in on the lower ones.
///
/// The placement is meant to look like somewhere rather than like noise, which is all about where things are
/// NOT put:
///
///  - the road corridor is kept clear, out to a radius rather than to a hard band, so nothing leans into a
///    corner the player takes at speed,
///  - ground steeper than a slope limit is left bare, which keeps the road's own cuttings and embankments
///    reading as cuttings and embankments,
///  - woods gather where they already are, because the terrain's existing trees are read back in and used as
///    a bias, and clearings stay clear,
///  - and the land is scattered in three bands - near, middle, far - with the average size growing with the
///    distance, which is what gives a level a horizon rather than a carpet.
///
/// How many get placed is worked out from the ground itself rather than from a per-square chance: the planner
/// reads every spot, keeps the ones that are usable, measures how many hectares each band actually has, and
/// then places density x hectares of them. A band that has the ground gets the count it was asked for, and
/// what was rejected is reported - in the corridor, too steep, off the terrain - so a plan can never come out
/// nearly empty without saying why.
///
/// Nothing is written until PAINT. The budget panel says what would go in before anything is.
///
/// Usage: open a level scene (Core-1 ... Core-4), then Tools > Road Tools > Paint Terrain Scenery. The first
/// run needs scenery prototypes, which the window offers to build from the level's own trees; Tools > Road
/// Tools > Paint Scenery In This Scene does the whole thing in one go.
/// </summary>
public class TerrainSceneryPainter : EditorWindow
{
    private const string SceneryRoot = "Assets/Prefabs/Scenery";
    private const string ObjectParentName = "Scenery";

    // ---------------------------------------------------------------- what gets painted

    /// <summary>One prefab, and the rules for using it.</summary>
    private class Family
    {
        public GameObject prefab;
        public bool enabled = true;

        [Tooltip("Which bands this belongs in. Big things belong far, small things near.")]
        public bool near = true;
        public bool mid = true;
        public bool far;

        [Tooltip("How much more or less likely this is than the others in the same band.")]
        public float weight = 1f;

        public Vector2 scale = new Vector2(0.85f, 1.2f);

        [Tooltip("Placed as a plain object rather than as a terrain tree. Costs a draw call each, so keep it " +
                 "for the handful of things that have to look solid rather than painted.")]
        public bool asObject;

        public int placed;
    }

    private class Placement
    {
        public Vector3 position;
        public int family;
        public int band;
        public float scale;
    }

    /// <summary>One spot of ground the plan is allowed to use, and what it is like.</summary>
    private class Cell
    {
        public Vector3 position;
        public int band;
        public bool wooded;
    }

    /// <summary>
    /// What the planner did and what it turned down, so "nothing happened" can always be answered.
    /// </summary>
    private class PlanReport
    {
        public int spots;                 // grid spots looked at
        public int offTerrain;            // outside the terrain
        public int corridor;              // inside the road corridor
        public int steep;                 // steeper than the slope limit
        public readonly int[] noFamily = new int[3];
        public readonly int[] spotsPerBand = new int[3];   // usable spots
        public readonly int[] wooded = new int[3];
        public readonly float[] hectares = new float[3];
        public readonly int[] wanted = new int[3];
        public readonly int[] placed = new int[3];

        public int Wanted { get { return wanted[0] + wanted[1] + wanted[2]; } }
        public int NoFamily { get { return noFamily[0] + noFamily[1] + noFamily[2]; } }
    }

    /// <summary>How far out each band reaches.</summary>
    private enum Band
    {
        Near = 0,
        Mid = 1,
        Far = 2,
    }

    // ---------------------------------------------------------------- the tool's state

    private Road road;                          // the road this window measures against, and the fallback
    private readonly List<Road> roads = new List<Road>();   // every road in the scene, kept clear of
    private Terrain terrain;

    private readonly List<Family> families = new List<Family>();

    // The corridor that is never touched, and the bands beyond it.
    private float clearRadius = 45f;
    private float nearTo = 140f;
    private float midTo = 400f;
    private Vector3 bandDensity = new Vector3(90f, 45f, 18f);     // instances per hectare: near, mid, far
    private Vector3 bandScale = new Vector3(1f, 1.25f, 2f);       // how much bigger things get with distance

    // The shape of the scatter.
    private float cellSize = 6f;
    private float jitter = 0.7f;
    private float clumpSize = 200f;                               // metres across one wood
    private float clumpWooded = 0.5f;                             // share of the map that is wooded
    private float openShare = 0.15f;                              // how much of the scatter lands in the open
    private float slopeLimit = 45f;
    private bool gatherWhereWoodsAlreadyAre = true;
    private int seed = 20260928;
    private int instanceCap = 60000;

    // The far distance: fog is what stops the eye finding the edge of the terrain.
    private bool fogFromSkybox = true;
    private Color fogColour = new Color(0.62f, 0.68f, 0.75f, 1f);
    private float fogStart = 260f;
    private float fogEnd = 900f;

    // The plan, and the preview of it.
    private readonly List<Placement> plan = new List<Placement>();
    private PlanReport report = new PlanReport();
    private bool showPreview;
    private bool planned;
    private string warning = "";
    private int existingTrees;

    // Every road, sampled once per plan so a candidate position can ask how far away the nearest is.
    private readonly List<Vector2> roadPoints = new List<Vector2>();
    private readonly Dictionary<long, List<int>> roadBuckets = new Dictionary<long, List<int>>();
    private const float RoadBucketSize = 120f;
    private const float RoadSampleStep = 6f;

    private Vector2 scroll;

    [MenuItem("Tools/Road Tools/Paint Terrain Scenery", false, 31)]
    private static void OpenWindow()
    {
        TerrainSceneryPainter window = GetWindow<TerrainSceneryPainter>("Scenery Painter");
        window.minSize = new Vector2(440f, 620f);
        window.Show();
    }

    /// <summary>
    /// The whole thing in one command: build the level's prototypes if it has none, plan, paint and set the
    /// fog. For a level that has never been dressed, this is the only menu item that needs running.
    /// </summary>
    [MenuItem("Tools/Road Tools/Paint Scenery In This Scene (one click)", false, 32)]
    private static void PaintScene()
    {
        Terrain active = Terrain.activeTerrain;

        if (active == null)
        {
            EditorUtility.DisplayDialog("Paint Scenery", "This scene has no terrain in it.", "OK");

            return;
        }

        TerrainSceneryPainter window = GetWindow<TerrainSceneryPainter>("Scenery Painter");

        window.terrain = active;
        window.CollectRoads();

        if (window.road == null)
        {
            EditorUtility.DisplayDialog("Paint Scenery",
                "This scene has no built road to keep clear of. Build the road, then run this again.", "OK");

            return;
        }

        if (window.families.Count == 0)
        {
            if (!SceneryPrototypeBuilder.BuildForActiveScene()) return;

            window.LoadFamilies();
        }

        if (window.families.Count == 0 || window.road == null)
        {
            EditorUtility.DisplayDialog("Paint Scenery",
                "No scenery to paint with. Run Tools > Road Tools > Build Scenery Prototypes in this scene " +
                "first, or drag prefabs into the painter's list.", "OK");

            return;
        }

        bool painted = window.HasPaintedScenery();
        string scene = SceneManager.GetActiveScene().name;

        if (!EditorUtility.DisplayDialog("Paint Scenery In " + scene,
                "This will:\n\n" +
                "  - fill the land either side of the road with this level's own scenery, as terrain trees,\n" +
                "  - keep the road corridor clear and leave anything steeper than " + window.slopeLimit + " degrees bare,\n" +
                (painted ? "  - replace the scenery already painted on this terrain, and\n" : "") +
                "  - set distance fog from the level's sky, which is a scene setting.\n\n" +
                "Save the scene afterwards.",
                "Paint It", "Cancel"))
            return;

        if (painted) window.Clear();

        window.MakePlan();

        if (window.plan.Count > 0) window.Paint();

        window.ApplyFog();

        window.Repaint();
    }

    private void OnEnable()
    {
        CollectRoads();

        if (terrain == null) terrain = Terrain.activeTerrain;

        if (families.Count == 0) LoadFamilies();

        CountExistingTrees();
    }

    /// <summary>
    /// Every built road in the scene. A level can hold more than one - connectors, ramps, a second stretch -
    /// and the corridor has to be kept clear of all of them, not just the one the player starts on.
    /// </summary>
    private void CollectRoads()
    {
        roads.Clear();

        Road[] found = FindObjectsOfType<Road>();

        for (int i = 0; i < found.Length; i++)
        {
            if (found[i] == null || found[i].spline == null) continue;
            if (found[i].spline.distance <= 0.01f) continue;

            roads.Add(found[i]);
        }

        if (road == null || road.spline == null) road = roads.Count > 0 ? roads[0] : null;
    }

    /// <summary>
    /// How many trees the level already has on it. Read once rather than while the window is drawn: asking a
    /// terrain for its instances hands back a copy of every one of them, which is a large array to throw away
    /// once a frame.
    /// </summary>
    private void CountExistingTrees()
    {
        existingTrees = terrain != null && terrain.terrainData != null ? terrain.terrainData.treeInstances.Length : 0;
    }

    /// <summary>Whether this terrain already holds trees from this tool, which a paint would add to.</summary>
    private bool HasPaintedScenery()
    {
        if (terrain == null || terrain.terrainData == null) return false;

        TerrainData data = terrain.terrainData;
        TreePrototype[] prototypes = data.treePrototypes;
        TreeInstance[] trees = data.treeInstances;

        for (int i = 0; i < trees.Length; i++)
        {
            int index = trees[i].prototypeIndex;

            if (index < 0 || index >= prototypes.Length) continue;
            if (prototypes[index] == null || prototypes[index].prefab == null) continue;

            string path = AssetDatabase.GetAssetPath(prototypes[index].prefab);

            if (!string.IsNullOrEmpty(path) && path.StartsWith(SceneryRoot)) return true;
        }

        return false;
    }

    private void OnDisable()
    {
        EditorUtility.ClearProgressBar();
    }

    // ---------------------------------------------------------------- the families

    /// <summary>
    /// Fills the list from the level's scenery prototypes - what <see cref="SceneryPrototypeBuilder"/> wrote -
    /// so the usual way to run this is: build the prototypes once, then paint.
    /// </summary>
    private void LoadFamilies()
    {
        families.Clear();

        // A plan holds an index into this list, so anything that renumbers it makes the plan stale.
        plan.Clear();
        planned = false;

        string folder = SceneryRoot + "/" + LevelFolder();

        if (!AssetDatabase.IsValidFolder(folder)) return;

        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { folder });

        for (int i = 0; i < guids.Length; i++)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guids[i]));

            if (prefab == null) continue;

            // Building the prototypes twice on top of itself names a clone "... (Scenery) (Scenery)". Those are
            // copies of copies and are left out, so a list that has been built over twice still reads clean.
            if (SceneryPrototypeBuilder.GenerationsIn(prefab.name) > 1) continue;

            Family family = new Family();
            family.prefab = prefab;

            // Left in every band on purpose. A band on its own is a wall, not a distance: a tree that cannot
            // stand within 140 m of the road leaves that whole strip to the bushes, and the land either side of
            // the road is exactly what the player looks at. Distance is what Bands' scale and density are for.
            family.near = true;
            family.mid = true;
            family.far = true;

            families.Add(family);
        }
    }

    /// <summary>The level this scene belongs to, as the asset folders name it: Core-3 is Level-3.</summary>
    private static string LevelFolder()
    {
        string scene = SceneManager.GetActiveScene().name;
        int dash = scene.LastIndexOf('-');

        if (dash >= 0 && int.TryParse(scene.Substring(dash + 1), out int number)) return "Level-" + number;

        return scene;
    }

    // ---------------------------------------------------------------- the window

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        try
        {
            GUILayout.Label("Paint Terrain Scenery", EditorStyles.boldLabel);

            EditorGUILayout.HelpBox(
                "Fills the land either side of the road with the level's scenery prototypes, as terrain trees. " +
                "The road corridor is left clear, steep ground is left bare, and woods gather where woods " +
                "already are. How much goes in is worked out from the ground each band has, and the budget " +
                "below says what was turned down and why. Nothing is written until PAINT.",
                MessageType.Info);

            if (GUILayout.Button("Build This Level's Prototypes First", GUILayout.Height(26f)))
                SceneryPrototypeBuilder.BuildForActiveScene();

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("References", EditorStyles.boldLabel);
            road = (Road)EditorGUILayout.ObjectField("Road", road, typeof(Road), true);
            terrain = (Terrain)EditorGUILayout.ObjectField("Terrain", terrain, typeof(Terrain), true);

            if (road == null || terrain == null || road.spline == null)
            {
                EditorGUILayout.HelpBox("Needs both a road and a terrain.", MessageType.Warning);

                return;
            }

            TerrainData data = terrain.terrainData;

            float roadLength = 0f;

            for (int i = 0; i < roads.Count; i++) roadLength += roads[i].spline.distance;

            EditorGUILayout.LabelField("  Roads In The Scene", roads.Count.ToString());
            EditorGUILayout.LabelField("  Road Length", $"{roadLength:F0} m");
            EditorGUILayout.LabelField("  Terrain Size", $"{data.size.x:F0} x {data.size.z:F0} m");
            EditorGUILayout.LabelField("  Trees Already Painted", existingTrees.ToString("N0"));

            EditorGUILayout.Space();

            DrawFamilies();
            DrawCorridor();
            DrawBands();
            DrawShape();
            DrawDistantLook();
            DrawBudget();

            EditorGUILayout.Space();

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("PLAN", GUILayout.Height(34f))) MakePlan();
            if (GUILayout.Button(showPreview ? "Hide Preview" : "Show Preview", GUILayout.Height(34f)))
            {
                showPreview = !showPreview;
                SceneView.RepaintAll();
            }

            EditorGUILayout.EndHorizontal();

            GUI.backgroundColor = Color.green;

            EditorGUI.BeginDisabledGroup(!planned || plan.Count == 0);

            if (GUILayout.Button("PAINT SCENERY", GUILayout.Height(40f)))
            {
                if (EditorUtility.DisplayDialog("Paint Scenery",
                        $"This will add {plan.Count:N0} scenery instances to '{terrain.name}'.\n\n" +
                        "Ctrl+Z usually takes it back; if it does not, Remove Painted Scenery is the sure way.",
                        "Paint", "Cancel"))
                {
                    Paint();
                }
            }

            EditorGUI.EndDisabledGroup();
            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space();

            GUI.backgroundColor = new Color(1f, 0.6f, 0.6f);

            if (GUILayout.Button("Remove Painted Scenery", GUILayout.Height(26f)))
            {
                if (EditorUtility.DisplayDialog("Remove Painted Scenery",
                        "Removes every scenery instance this tool painted on the terrain, and the objects it " +
                        "placed.\n\nTrees painted by the other tools are left alone. Can be undone.",
                        "Remove", "Cancel"))
                {
                    Clear();
                }
            }

            GUI.backgroundColor = Color.white;
        }
        finally
        {
            EditorGUILayout.EndScrollView();
        }
    }

    private void DrawFamilies()
    {
        EditorGUILayout.LabelField("Families (" + families.Count + ")", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Reload From This Level")) LoadFamilies();
        if (GUILayout.Button("All Bands")) { foreach (Family f in families) { f.near = f.mid = f.far = true; } }
        if (GUILayout.Button("Only Near")) { foreach (Family f in families) { f.near = true; f.mid = f.far = false; } }

        EditorGUILayout.EndHorizontal();

        if (families.Count == 0)
        {
            EditorGUILayout.HelpBox(
                "No scenery prototypes for this level yet, and without them there is nothing to paint. The " +
                "button above builds them from the level's own trees - collider-free copies - and the list " +
                "appears here.",
                MessageType.Warning);

            return;
        }

        int remove = -1;

        for (int i = 0; i < families.Count; i++)
        {
            Family family = families[i];
            PrefabCost cost = SceneryPrefabAudit.Of(family.prefab);

            EditorGUILayout.BeginHorizontal();

            family.enabled = EditorGUILayout.ToggleLeft("", family.enabled, GUILayout.Width(18f));
            family.prefab = (GameObject)EditorGUILayout.ObjectField(family.prefab, typeof(GameObject), false);

            if (cost.CarriesPhysics)
            {
                GUI.color = new Color(1f, 0.7f, 0.3f);
                GUILayout.Label("HAS COLLIDERS", EditorStyles.miniLabel, GUILayout.Width(110f));
                GUI.color = Color.white;
            }
            else
            {
                GUILayout.Label(cost.Triangles(), EditorStyles.miniLabel, GUILayout.Width(110f));
            }

            if (GUILayout.Button("x", GUILayout.Width(20f))) remove = i;

            EditorGUILayout.EndHorizontal();

            if (!family.enabled) continue;

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(24f);
            family.near = EditorGUILayout.ToggleLeft("Near", family.near, GUILayout.Width(60f));
            family.mid = EditorGUILayout.ToggleLeft("Mid", family.mid, GUILayout.Width(60f));
            family.far = EditorGUILayout.ToggleLeft("Far", family.far, GUILayout.Width(60f));
            family.asObject = EditorGUILayout.ToggleLeft("As Object", family.asObject, GUILayout.Width(90f));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(24f);
            family.weight = EditorGUILayout.Slider("Weight", family.weight, 0.05f, 4f);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(24f);
            EditorGUILayout.MinMaxSlider("Scale", ref family.scale.x, ref family.scale.y, 0.2f, 4f);
            EditorGUILayout.EndHorizontal();

            if (family.asObject)
            {
                EditorGUILayout.HelpBox(
                    "Placed as an object rather than a painted tree: it stays solid and casts the shadows it was " +
                    "built with, at the price of a draw call each - so keep the density low for these.",
                    MessageType.None);
            }
        }

        if (remove >= 0)
        {
            families.RemoveAt(remove);

            plan.Clear();
            planned = false;
        }
    }

    private void DrawCorridor()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Road Corridor", EditorStyles.boldLabel);
        clearRadius = EditorGUILayout.Slider("Keep Clear (m from road)", clearRadius, 5f, 200f);
        EditorGUILayout.LabelField("  Nothing is placed closer to any road than this.", EditorStyles.miniLabel);
    }

    private void DrawBands()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Bands", EditorStyles.boldLabel);

        nearTo = EditorGUILayout.Slider("Near Ends At (m)", nearTo, clearRadius + 10f, 400f);
        midTo = EditorGUILayout.Slider("Middle Ends At (m)", midTo, nearTo + 10f, 1500f);

        bandDensity.x = EditorGUILayout.Slider("Near Density (/ha)", bandDensity.x, 0f, 400f);
        bandDensity.y = EditorGUILayout.Slider("Middle Density (/ha)", bandDensity.y, 0f, 400f);
        bandDensity.z = EditorGUILayout.Slider("Far Density (/ha)", bandDensity.z, 0f, 400f);

        bandScale.x = EditorGUILayout.Slider("Near Scale", bandScale.x, 0.2f, 4f);
        bandScale.y = EditorGUILayout.Slider("Middle Scale", bandScale.y, 0.2f, 4f);
        bandScale.z = EditorGUILayout.Slider("Far Scale", bandScale.z, 0.2f, 4f);

        EditorGUILayout.LabelField("  Density is instances per hectare of usable ground in that band.",
            EditorStyles.miniLabel);
        EditorGUILayout.LabelField("  Far runs from the middle band to the edge of the terrain.",
            EditorStyles.miniLabel);
    }

    private void DrawShape()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Shape", EditorStyles.boldLabel);

        cellSize = EditorGUILayout.Slider("Spot Spacing (m)", cellSize, 4f, 40f);
        jitter = EditorGUILayout.Slider("Jitter", jitter, 0f, 1f);
        clumpSize = EditorGUILayout.Slider("Wood Size (m)", clumpSize, 30f, 800f);
        clumpWooded = EditorGUILayout.Slider("How Wooded", clumpWooded, 0.05f, 0.95f);
        openShare = EditorGUILayout.Slider("Open Ground Share", openShare, 0f, 1f);
        slopeLimit = EditorGUILayout.Slider("Slope Limit (deg)", slopeLimit, 0f, 70f);

        gatherWhereWoodsAlreadyAre = EditorGUILayout.ToggleLeft(
            "Gather where woods already are", gatherWhereWoodsAlreadyAre);

        seed = EditorGUILayout.IntField("Seed", seed);
        instanceCap = EditorGUILayout.IntField("Most Instances", instanceCap);

        EditorGUILayout.LabelField("  Spot spacing is the finest the scatter can be: one instance per spot.",
            EditorStyles.miniLabel);
    }

    private void DrawDistantLook()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Distant Look", EditorStyles.boldLabel);

        EditorGUILayout.HelpBox(
            "Fog is what makes distance read as distance instead of as an empty map, and it costs nothing. " +
            "It is a scene setting: apply it, then save the scene.",
            MessageType.None);

        fogFromSkybox = EditorGUILayout.ToggleLeft("Take the colour from this level's skybox", fogFromSkybox);
        fogColour = EditorGUILayout.ColorField("Fog Colour", fogColour);
        fogStart = EditorGUILayout.Slider("Fog Starts (m)", fogStart, 20f, 800f);
        fogEnd = EditorGUILayout.Slider("Fog Ends (m)", fogEnd, fogStart + 50f, 4000f);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Apply Fog")) ApplyFog();
        if (GUILayout.Button("Turn Fog Off")) RenderSettings.fog = false;
        EditorGUILayout.EndHorizontal();
    }

    private void DrawBudget()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Budget", EditorStyles.boldLabel);

        if (!planned)
        {
            EditorGUILayout.LabelField("  Press PLAN to work out what would be painted.", EditorStyles.miniLabel);

            return;
        }

        int objects = 0;

        for (int i = 0; i < plan.Count; i++)
        {
            if (families[plan[i].family].asObject) objects++;
        }

        EditorGUILayout.LabelField("  Instances", plan.Count.ToString("N0"));
        EditorGUILayout.LabelField("  Near / Middle / Far",
            $"{report.placed[0]:N0} / {report.placed[1]:N0} / {report.placed[2]:N0}  " +
            $"(asked for {report.wanted[0]:N0} / {report.wanted[1]:N0} / {report.wanted[2]:N0})");
        EditorGUILayout.LabelField("  Usable Ground Per Band",
            $"{report.hectares[0]:F1} ha / {report.hectares[1]:F1} ha / {report.hectares[2]:F1} ha");
        EditorGUILayout.LabelField("  Wooded Spots",
            $"{report.wooded[0] * 100 / Mathf.Max(1, report.spotsPerBand[0])}% / " +
            $"{report.wooded[1] * 100 / Mathf.Max(1, report.spotsPerBand[1])}% / " +
            $"{report.wooded[2] * 100 / Mathf.Max(1, report.spotsPerBand[2])}%");
        EditorGUILayout.LabelField("  Placed As Objects", objects.ToString("N0"));
        EditorGUILayout.LabelField("  Colliders Added", "0");
        EditorGUILayout.LabelField("  Drawn As Billboards Past", terrain.treeBillboardDistance + " m",
            EditorStyles.miniLabel);

        EditorGUILayout.LabelField("  Spots Looked At", report.spots.ToString("N0"), EditorStyles.miniLabel);
        EditorGUILayout.LabelField("    In The Road Corridor", report.corridor.ToString("N0"), EditorStyles.miniLabel);
        EditorGUILayout.LabelField("    Too Steep", report.steep.ToString("N0"), EditorStyles.miniLabel);
        EditorGUILayout.LabelField("    Off The Terrain", report.offTerrain.ToString("N0"), EditorStyles.miniLabel);
        EditorGUILayout.LabelField("    Band With No Family", report.NoFamily.ToString("N0"), EditorStyles.miniLabel);

        for (int i = 0; i < families.Count; i++)
        {
            if (families[i].placed == 0) continue;

            EditorGUILayout.LabelField("    " + families[i].prefab.name, families[i].placed.ToString("N0"),
                EditorStyles.miniLabel);
        }

        if (!string.IsNullOrEmpty(warning)) EditorGUILayout.HelpBox(warning, MessageType.Warning);
    }

    // ---------------------------------------------------------------- planning

    /// <summary>
    /// Works out every position that would be painted, without touching the terrain.
    ///
    /// It runs in two passes on purpose. The first reads every spot of ground and keeps the usable ones,
    /// which is what says how much land each band actually has. The second turns that into a count - density
    /// times hectares - and places exactly that many, so a band never comes out thinner than it was asked
    /// for because a chance rolled the wrong way. What the first pass turned down is kept in the report.
    /// </summary>
    private void MakePlan()
    {
        plan.Clear();

        for (int i = 0; i < families.Count; i++) families[i].placed = 0;

        planned = false;
        warning = "";
        showPreview = false;
        report = new PlanReport();

        if (road == null || road.spline == null || terrain == null) return;

        TerrainData data = terrain.terrainData;
        Vector3 origin = terrain.transform.position;

        // The roads, sampled once. Everything else asks this how far away the nearest one is.
        BuildRoadSamples();

        if (roadPoints.Count == 0)
        {
            warning = "No road could be sampled: build the road before painting scenery.";

            return;
        }

        // Where the level's own trees already are, so new woods can gather around them.
        HashSet<long> woods = gatherWhereWoodsAlreadyAre ? WoodsGrid(data) : null;

        List<Family>[] bandFamilies = new List<Family>[3];

        for (int b = 0; b < 3; b++)
        {
            bandFamilies[b] = new List<Family>();

            for (int i = 0; i < families.Count; i++)
            {
                Family family = families[i];

                if (!family.enabled || family.prefab == null) continue;
                if (FamilyInBand(family, b)) bandFamilies[b].Add(family);
            }
        }

        if (bandFamilies[0].Count == 0 && bandFamilies[1].Count == 0 && bandFamilies[2].Count == 0)
        {
            warning = "No family is switched on, or none is switched on for the band it would land in - " +
                      "nothing would be painted.";

            return;
        }

        int cellsX = Mathf.Max(1, Mathf.CeilToInt(data.size.x / cellSize));
        int cellsZ = Mathf.Max(1, Mathf.CeilToInt(data.size.z / cellSize));

        System.Random random = new System.Random(seed);

        float cosSlope = Mathf.Cos(slopeLimit * Mathf.Deg2Rad);
        float woodThreshold = Mathf.Clamp01(1f - clumpWooded);

        List<Cell>[] ground = { new List<Cell>(), new List<Cell>(), new List<Cell>() };

        // ---------------------------------------------------------- pass one: the ground

        for (int x = 0; x < cellsX; x++)
        {
            if (EditorUtility.DisplayCancelableProgressBar("Planning Scenery",
                    "Reading the ground... " + x + " of " + cellsX, x / (float)cellsX))
                break;

            for (int z = 0; z < cellsZ; z++)
            {
                report.spots++;

                Vector3 world = new Vector3(
                    origin.x + (x + 0.5f + (float)(random.NextDouble() - 0.5) * jitter) * cellSize,
                    0f,
                    origin.z + (z + 0.5f + (float)(random.NextDouble() - 0.5) * jitter) * cellSize);

                float u = (world.x - origin.x) / data.size.x;
                float v = (world.z - origin.z) / data.size.z;

                if (u < 0f || u > 1f || v < 0f || v > 1f) { report.offTerrain++; continue; }

                float distance = DistanceToRoad(world);

                if (distance < clearRadius) { report.corridor++; continue; }

                int band = distance < nearTo ? 0 : distance < midTo ? 1 : 2;

                if (bandFamilies[band].Count == 0) { report.noFamily[band]++; continue; }

                if (data.GetInterpolatedNormal(u, v).y < cosSlope) { report.steep++; continue; }

                // How wooded this spot is: somewhere the noise is high, or somewhere the level has already put
                // trees - so new trees thicken existing woods instead of standing beside them.
                float wooded = Fbm(world.x / clumpSize, world.z / clumpSize, seed);

                if (woods != null) wooded = Mathf.Max(wooded, WoodsAt(woods, world));

                Cell cell = new Cell();
                cell.position = new Vector3(world.x, data.GetInterpolatedHeight(u, v) + origin.y, world.z);
                cell.band = band;
                cell.wooded = wooded >= woodThreshold;

                ground[band].Add(cell);
                report.spotsPerBand[band]++;

                if (cell.wooded) report.wooded[band]++;
            }
        }

        EditorUtility.ClearProgressBar();

        // ---------------------------------------------------------- pass two: how many, and where

        for (int band = 0; band < 3; band++)
        {
            report.hectares[band] = report.spotsPerBand[band] * cellSize * cellSize / 10000f;
            report.wanted[band] = Mathf.RoundToInt(report.hectares[band] * BandDensity(band));
        }

        for (int band = 0; band < 3; band++)
        {
            if (ground[band].Count == 0 || report.wanted[band] <= 0) continue;

            int wanted = Mathf.Min(report.wanted[band], Mathf.Max(0, instanceCap - plan.Count));

            if (wanted <= 0) break;

            // Wooded ground is the likely place for something to stand, open ground the unlikely one - which is
            // what turns a uniform sprinkle into woods with clearings.
            float[] weight = new float[ground[band].Count];
            float total = 0f;

            for (int i = 0; i < ground[band].Count; i++)
            {
                weight[i] = ground[band][i].wooded ? 1f : Mathf.Max(0.0001f, openShare);
                total += weight[i];
            }

            for (int i = 0; i < wanted; i++)
            {
                float roll = (float)random.NextDouble() * total;
                int index = ground[band].Count - 1;

                for (int c = 0; c < weight.Length; c++)
                {
                    roll -= weight[c];

                    if (roll <= 0f) { index = c; break; }
                }

                Family family = Pick(bandFamilies[band], random);

                if (family == null) continue;

                Cell cell = ground[band][index];

                Placement placement = new Placement();
                placement.position = new Vector3(
                    cell.position.x + (float)(random.NextDouble() - 0.5) * cellSize * jitter * 2f,
                    cell.position.y,
                    cell.position.z + (float)(random.NextDouble() - 0.5) * cellSize * jitter * 2f);
                placement.family = families.IndexOf(family);
                placement.band = band;
                placement.scale = Mathf.Lerp(family.scale.x, family.scale.y, (float)random.NextDouble()) *
                                  BandScale(band);

                plan.Add(placement);
                report.placed[band]++;
                family.placed++;
            }
        }

        // ---------------------------------------------------------- what to say about it

        List<string> notes = new List<string>();

        if (plan.Count == 0)
        {
            notes.Add("Nothing would be painted. Of " + report.spots.ToString("N0") + " spots: " +
                      report.corridor.ToString("N0") + " inside the road corridor, " +
                      report.steep.ToString("N0") + " too steep, " +
                      report.offTerrain.ToString("N0") + " off the terrain, " +
                      report.NoFamily.ToString("N0") + " in a band with nothing switched on for it.");
        }

        if (plan.Count > 0 && report.Wanted > report.placed[0] + report.placed[1] + report.placed[2])
            notes.Add("Placed " + (report.placed[0] + report.placed[1] + report.placed[2]).ToString("N0") +
                      " of the " + report.Wanted.ToString("N0") + " asked for: the instance cap stopped it.");

        if (plan.Count >= instanceCap)
            notes.Add("Stopped at the instance cap (" + instanceCap.ToString("N0") + ").");

        int objectCount = 0;

        for (int i = 0; i < plan.Count; i++)
            if (families[plan[i].family].asObject) objectCount++;

        if (objectCount > 300)
            notes.Add(objectCount.ToString("N0") + " of these are objects rather than painted trees, which is a " +
                      "draw call each - lower their weight or density.");

        for (int i = 0; i < families.Count; i++)
        {
            if (!families[i].enabled || families[i].prefab == null) continue;

            if (SceneryPrefabAudit.Of(families[i].prefab).CarriesPhysics)
                notes.Add("'" + families[i].prefab.name + "' carries colliders: every instance would be solid. " +
                          "Build a scenery prototype of it instead.");
        }

        warning = string.Join("\n", notes);

        planned = true;
        showPreview = true;

        SceneView.RepaintAll();

        Debug.Log("[Scenery] Plan for " + LevelFolder() + ": " + plan.Count.ToString("N0") +
                  " instances over " + (report.hectares[0] + report.hectares[1] + report.hectares[2]).ToString("F1") +
                  " ha of usable ground (near " + report.hectares[0].ToString("F1") + " ha, middle " +
                  report.hectares[1].ToString("F1") + " ha, far " + report.hectares[2].ToString("F1") +
                  " ha); turned down " + report.corridor.ToString("N0") + " in the corridor, " +
                  report.steep.ToString("N0") + " too steep.");

        Repaint();
    }

    private static bool FamilyInBand(Family family, int band)
    {
        if (band == 0) return family.near;
        if (band == 1) return family.mid;

        return family.far;
    }

    private float BandDensity(int band)
    {
        if (band == 0) return bandDensity.x;
        if (band == 1) return bandDensity.y;

        return bandDensity.z;
    }

    private float BandScale(int band)
    {
        if (band == 0) return bandScale.x;
        if (band == 1) return bandScale.y;

        return bandScale.z;
    }

    /// <summary>One of a band's families, drawn by weight.</summary>
    private static Family Pick(List<Family> list, System.Random random)
    {
        float total = 0f;

        for (int i = 0; i < list.Count; i++) total += Mathf.Max(0.0001f, list[i].weight);

        if (total <= 0f) return null;

        float roll = (float)random.NextDouble() * total;

        for (int i = 0; i < list.Count; i++)
        {
            roll -= Mathf.Max(0.0001f, list[i].weight);

            if (roll <= 0f) return list[i];
        }

        return list[list.Count - 1];
    }

    // ---------------------------------------------------------------- painting

    /// <summary>
    /// Writes the plan: the terrain trees into the terrain's own tree list, the object families into a parent
    /// object of their own. Both are undoable in one step.
    ///
    /// A prototype is only added to the terrain for a family the plan actually uses, so a painter run never
    /// leaves the level carrying tree types nothing stands on.
    /// </summary>
    private void Paint()
    {
        TerrainData data = terrain.terrainData;

        Undo.RegisterCompleteObjectUndo(data, "Paint Terrain Scenery");

        int before = data.treeInstances.Length;

        // Read the level's own trees first, then swap the prototypes, then write both back: changing the
        // prototype list is not something to be doing while holding a copy of the instances.
        List<TreeInstance> trees = new List<TreeInstance>(data.treeInstances);
        List<TreePrototype> prototypes = new List<TreePrototype>(data.treePrototypes);

        Dictionary<GameObject, int> indices = new Dictionary<GameObject, int>();

        for (int i = 0; i < prototypes.Count; i++)
        {
            if (prototypes[i] != null && prototypes[i].prefab != null && !indices.ContainsKey(prototypes[i].prefab))
                indices.Add(prototypes[i].prefab, i);
        }

        // Which families the plan uses, and in what order each gets its prototype.
        List<GameObject> used = new List<GameObject>();

        for (int i = 0; i < plan.Count; i++)
        {
            GameObject prefab = families[plan[i].family].prefab;

            if (prefab == null || used.Contains(prefab)) continue;

            used.Add(prefab);
        }

        List<GameObject> added = new List<GameObject>();

        for (int i = 0; i < used.Count; i++)
        {
            if (indices.ContainsKey(used[i])) continue;

            TreePrototype prototype = new TreePrototype();
            prototype.prefab = used[i];
            prototype.bendFactor = 0.1f;

            prototypes.Add(prototype);
            indices.Add(used[i], prototypes.Count - 1);
            added.Add(used[i]);
        }

        GameObject parent = null;

        System.Random random = new System.Random(seed + 1);

        int painted = 0;
        int objects = 0;

        for (int i = 0; i < plan.Count; i++)
        {
            if (EditorUtility.DisplayCancelableProgressBar("Painting Scenery",
                    "Placing... " + i + " of " + plan.Count, i / (float)plan.Count))
                break;

            Placement placement = plan[i];
            Family family = families[placement.family];

            if (family.prefab == null) continue;

            Quaternion rotation = Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f);

            if (family.asObject)
            {
                if (parent == null)
                {
                    parent = new GameObject(ObjectParentName + " (" + LevelFolder() + ")");
                    Undo.RegisterCreatedObjectUndo(parent, "Paint Terrain Scenery");
                }

                // A plain clone rather than a prefab instance: an instance cannot be given static batching
                // flags, and batching is the whole reason these few are objects instead of painted trees.
                GameObject placed = Instantiate(family.prefab);
                placed.name = family.prefab.name;

                placed.transform.SetPositionAndRotation(placement.position, rotation);
                placed.transform.localScale = Vector3.one * placement.scale;
                placed.transform.SetParent(parent.transform, true);

                // Off the road and outside the walls, nothing needs to be solid - and a scenery prototype has
                // had its colliders taken off already, so this is only a guard against the originals.
                Collider[] colliders = placed.GetComponentsInChildren<Collider>(true);

                for (int c = 0; c < colliders.Length; c++)
                    if (colliders[c] != null)
                        DestroyImmediate(colliders[c]);

                GameObjectUtility.SetStaticEditorFlags(placed, StaticEditorFlags.BatchingStatic);
                Undo.RegisterCreatedObjectUndo(placed, "Paint Terrain Scenery");

                objects++;
            }
            else
            {
                TreeInstance tree = new TreeInstance();
                tree.position = new Vector3(
                    (placement.position.x - terrain.transform.position.x) / data.size.x,
                    (placement.position.y - terrain.transform.position.y) / data.size.y,
                    (placement.position.z - terrain.transform.position.z) / data.size.z);

                tree.prototypeIndex = indices[family.prefab];
                tree.widthScale = placement.scale;
                tree.heightScale = placement.scale;
                tree.rotation = (float)random.NextDouble() * Mathf.PI * 2f;
                tree.color = Color.white;
                tree.lightmapColor = Color.white;

                trees.Add(tree);
            }

            painted++;
        }

        EditorUtility.ClearProgressBar();

        // Prototypes first, then every instance - the level's own trees included, untouched.
        if (added.Count > 0) data.treePrototypes = prototypes.ToArray();

        // Snapping puts every tree on the ground it was planned over, so none of them float.
        data.SetTreeInstances(trees.ToArray(), true);

        EditorUtility.SetDirty(data);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        CountExistingTrees();

        Debug.Log("[Scenery] Painted " + painted.ToString("N0") + " instances in " +
                  SceneManager.GetActiveScene().name + " (" + objects.ToString("N0") + " as objects, " +
                  added.Count + " new tree types). The terrain holds " + existingTrees.ToString("N0") +
                  " trees now, " + (existingTrees - before).ToString("N0") + " more than before.");

        if (existingTrees - before < painted)
            Debug.LogWarning("[Scenery] Only " + (existingTrees - before) + " of the " + painted +
                             " painted instances reached the terrain. Check the console for an error above.");

        plan.Clear();
        planned = false;
        Repaint();
    }

    /// <summary>
    /// Takes back what this tool painted: the terrain trees whose prototype is one of the level's scenery
    /// prototypes, and the object parent. Trees painted by the other tools are whatever prototypes they use,
    /// so they are not touched.
    /// </summary>
    private void Clear()
    {
        TerrainData data = terrain.terrainData;
        TreePrototype[] prototypes = data.treePrototypes;

        List<int> scenery = new List<int>();

        for (int i = 0; i < prototypes.Length; i++)
        {
            if (prototypes[i] == null || prototypes[i].prefab == null) continue;

            string path = AssetDatabase.GetAssetPath(prototypes[i].prefab);

            if (!string.IsNullOrEmpty(path) && path.StartsWith(SceneryRoot)) scenery.Add(i);
        }

        Undo.RegisterCompleteObjectUndo(data, "Remove Painted Scenery");

        TreeInstance[] all = data.treeInstances;
        List<TreeInstance> keep = new List<TreeInstance>(all.Length);

        int removed = 0;

        for (int i = 0; i < all.Length; i++)
        {
            if (scenery.Contains(all[i].prototypeIndex)) { removed++; continue; }

            keep.Add(all[i]);
        }

        // The prototypes themselves go too, so a cleared level carries nothing of this tool.
        List<TreePrototype> kept = new List<TreePrototype>();

        for (int i = 0; i < prototypes.Length; i++)
        {
            if (scenery.Contains(i)) continue;

            kept.Add(prototypes[i]);
        }

        if (scenery.Count > 0)
        {
            // Re-point the surviving instances at the prototypes that are staying.
            int[] map = new int[prototypes.Length];
            int next = 0;

            for (int i = 0; i < prototypes.Length; i++)
            {
                map[i] = scenery.Contains(i) ? -1 : next++;
            }

            for (int i = 0; i < keep.Count; i++)
            {
                TreeInstance tree = keep[i];
                tree.prototypeIndex = map[tree.prototypeIndex];
                keep[i] = tree;
            }

            data.treePrototypes = kept.ToArray();
        }

        data.SetTreeInstances(keep.ToArray(), false);
        EditorUtility.SetDirty(data);

        GameObject parent = GameObject.Find(ObjectParentName + " (" + LevelFolder() + ")");

        int objects = 0;

        if (parent != null)
        {
            objects = parent.transform.childCount;
            Undo.DestroyObjectImmediate(parent);
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        Debug.Log("[Scenery] Removed " + removed.ToString("N0") + " scenery trees and " + objects +
                  " objects, and " + scenery.Count + " scenery tree types.");

        CountExistingTrees();

        plan.Clear();
        planned = false;
    }

    // ---------------------------------------------------------------- the far distance

    /// <summary>
    /// Puts linear fog on the level, coloured after its sky. This is the cheapest thing in the whole tool: it
    /// costs nothing per frame and it turns the far end of the terrain from "empty" into "far away".
    /// </summary>
    private void ApplyFog()
    {
        Color colour = fogColour;

        if (fogFromSkybox && RenderSettings.skybox != null)
        {
            Material sky = RenderSettings.skybox;

            string[] properties = { "_SkyTint", "_Tint", "_Color", "_SkyColor", "_GroundColor" };

            for (int i = 0; i < properties.Length; i++)
            {
                if (!sky.HasProperty(properties[i])) continue;

                Color fromSky = sky.GetColor(properties[i]);

                // A skybox property of pure black is the dark end of a gradient rather than its colour.
                if (fromSky.r + fromSky.g + fromSky.b < 0.05f) continue;

                colour = fromSky;
                break;
            }
        }

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = colour;
        RenderSettings.fogStartDistance = fogStart;
        RenderSettings.fogEndDistance = fogEnd;

        fogColour = colour;

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

        Debug.Log("[Scenery] Fog on: " + colour + " from " + fogStart + " m to " + fogEnd + " m. Save the scene to keep it.");
    }

    // ---------------------------------------------------------------- the roads, and where things are

    /// <summary>
    /// Samples every road in the scene into points a few metres apart and buckets them, so asking "how far is
    /// this spot from the nearest road" a hundred thousand times costs a handful of comparisons rather than a
    /// walk of the route.
    ///
    /// The samples are placed by distance along the spline rather than by parameter: on a road with long
    /// straights and tight corners those two are not the same thing, and the parameter would bunch the samples
    /// up in the corners.
    /// </summary>
    private void BuildRoadSamples()
    {
        roadPoints.Clear();
        roadBuckets.Clear();

        List<Road> sampled = new List<Road>();

        for (int i = 0; i < roads.Count; i++)
            if (roads[i] != null && roads[i].spline != null && roads[i].spline.distance > 0.01f)
                sampled.Add(roads[i]);

        if (road != null && road.spline != null && road.spline.distance > 0.01f && !sampled.Contains(road))
            sampled.Add(road);

        for (int r = 0; r < sampled.Count; r++)
        {
            SplineC spline = sampled[r].spline;
            float length = spline.distance;
            int count = Mathf.Clamp(Mathf.CeilToInt(length / RoadSampleStep), 32, 20000);

            bool byDistance = spline.RoadDefKeysArray != null && spline.RoadDefKeysArray.Length > 0;

            for (int i = 0; i <= count; i++)
            {
                float along = length * i / count;
                float param = byDistance ? spline.TranslateDistBasedToParam(along) : i / (float)count;

                Vector3 position, tangent;

                spline.GetSplineValueBoth(Mathf.Clamp01(param), out position, out tangent);

                roadPoints.Add(new Vector2(position.x, position.z));

                long key = BucketKey(position.x, position.z);

                if (!roadBuckets.TryGetValue(key, out List<int> list))
                {
                    list = new List<int>();
                    roadBuckets.Add(key, list);
                }

                list.Add(roadPoints.Count - 1);
            }
        }
    }

    private static long BucketKey(float x, float z)
    {
        int bx = Mathf.FloorToInt(x / RoadBucketSize);
        int bz = Mathf.FloorToInt(z / RoadBucketSize);

        return ((long)bx << 32) ^ (uint)bz;
    }

    /// <summary>
    /// How far a spot is from the nearest road. Points, not segments: the samples are close enough together
    /// that the difference is a metre or two, which is well inside the corridor radius.
    /// </summary>
    private float DistanceToRoad(Vector3 world)
    {
        Vector2 point = new Vector2(world.x, world.z);
        int bx = Mathf.FloorToInt(world.x / RoadBucketSize);
        int bz = Mathf.FloorToInt(world.z / RoadBucketSize);

        float best = float.MaxValue;

        for (int x = bx - 1; x <= bx + 1; x++)
        {
            for (int z = bz - 1; z <= bz + 1; z++)
            {
                if (!roadBuckets.TryGetValue(((long)x << 32) ^ (uint)z, out List<int> list)) continue;

                for (int i = 0; i < list.Count; i++)
                {
                    float distance = Vector2.SqrMagnitude(roadPoints[list[i]] - point);

                    if (distance < best) best = distance;
                }
            }
        }

        return best == float.MaxValue ? 100000f : Mathf.Sqrt(best);
    }

    /// <summary>Every cell of the map that already has one of the level's trees on it.</summary>
    private static HashSet<long> WoodsGrid(TerrainData data)
    {
        HashSet<long> used = new HashSet<long>();
        TreeInstance[] trees = data.treeInstances;

        const float cell = 12f;

        for (int i = 0; i < trees.Length; i++)
        {
            int x = Mathf.FloorToInt(trees[i].position.x * data.size.x / cell);
            int z = Mathf.FloorToInt(trees[i].position.z * data.size.z / cell);

            used.Add(((long)x << 32) ^ (uint)z);
        }

        return used;
    }

    /// <summary>How built up a spot already is, 0 to 1, from the cells around it.</summary>
    private static float WoodsAt(HashSet<long> woods, Vector3 world)
    {
        const float cell = 12f;

        int bx = Mathf.FloorToInt(world.x / cell);
        int bz = Mathf.FloorToInt(world.z / cell);

        int found = 0;

        for (int x = bx - 1; x <= bx + 1; x++)
        {
            for (int z = bz - 1; z <= bz + 1; z++)
            {
                if (woods.Contains(((long)x << 32) ^ (uint)z)) found++;
            }
        }

        return found / 9f;
    }

    // ---------------------------------------------------------------- noise

    /// <summary>
    /// A value noise that takes a seed, which <see cref="Mathf.PerlinNoise"/> does not - and a scenery plan
    /// that cannot be repeated with the same seed is a plan nobody can tune.
    /// </summary>
    private static float Fbm(float x, float z, int seed)
    {
        float total = 0f;
        float amplitude = 1f;
        float weight = 0f;

        for (int octave = 0; octave < 3; octave++)
        {
            total += ValueNoise(x * Mathf.Pow(2f, octave), z * Mathf.Pow(2f, octave), seed + octave * 7919) * amplitude;
            weight += amplitude;
            amplitude *= 0.5f;
        }

        return total / weight;
    }

    private static float ValueNoise(float x, float z, int seed)
    {
        int x0 = Mathf.FloorToInt(x);
        int z0 = Mathf.FloorToInt(z);

        float fx = Smooth(x - x0);
        float fz = Smooth(z - z0);

        float a = Hash(x0, z0, seed);
        float b = Hash(x0 + 1, z0, seed);
        float c = Hash(x0, z0 + 1, seed);
        float d = Hash(x0 + 1, z0 + 1, seed);

        return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fz);
    }

    private static float Smooth(float t)
    {
        return t * t * (3f - 2f * t);
    }

    private static float Hash(int x, int z, int seed)
    {
        unchecked
        {
            int h = x * 73856093 ^ z * 19349663 ^ seed * 83492791;

            h = (h ^ (h >> 13)) * 1274126177;
            h ^= h >> 16;

            return (h & 0x7fffffff) / (float)0x7fffffff;
        }
    }

    // ---------------------------------------------------------------- the preview

    private void OnSceneGUI(SceneView sceneView)
    {
        if (!showPreview || plan.Count == 0) return;

        for (int i = 0; i < plan.Count; i++)
        {
            Placement placement = plan[i];

            Color colour = placement.band == 0
                ? new Color(0.3f, 1f, 0.4f, 0.5f)
                : placement.band == 1 ? new Color(1f, 0.9f, 0.3f, 0.45f) : new Color(0.5f, 0.7f, 1f, 0.4f);

            Handles.color = colour;
            Handles.DrawWireDisc(placement.position, Vector3.up, placement.scale * 2f);
        }
    }
}
