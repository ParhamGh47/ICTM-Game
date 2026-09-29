using System.Collections.Generic;
using RoadArchitect;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Paints the level's trees along the road: the band of woodland either side of the tarmac the player drives
/// through, for the terrain to draw.
///
/// They are terrain trees rather than objects, which is the whole reason a band this wide is affordable - the
/// terrain instances them, culls them by patch and billboards them past the billboard distance, where an object
/// apiece would be a draw call apiece.
///
/// <b>The preview is the plan.</b> Nothing here is "worked out again with a different random seed" when the
/// button is pressed: the positions, the tree each one gets and its size are decided once, from a seed, and
/// both the discs drawn in the scene and the instances written to the terrain come from that one list. What the
/// scene view shows is what PAINT will do - including which trees are skipped for sitting on a building, and
/// where the band begins and ends.
///
/// The painting is aimed at the verge, not at the land: it starts outside the asphalt and shoulders, keeps off
/// anything already built, and stops at the ceiling set here. Filling the rest of the map - the land away from
/// the road, which is seen only from a distance - is what
/// <see cref="DistantSceneryPainter"/> is for, and it uses light collider-free copies for it.
///
/// Usage: open a level scene (Core-1 ... Core-4), then
///   Tools &gt; Road Tools &gt; Paint Trees Along Road
/// </summary>
public class TerrainTreePainter : EditorWindow
{
    /// <summary>One tree the plan intends to place: everything PAINT needs and nothing else.</summary>
    private struct PlannedTree
    {
        public Vector3 normalized;      // the terrain-space position a TreeInstance wants
        public Vector3 world;           // where it stands, for the preview
        public int prototype;
        public float scale;
        public int side;                // -1 or 1, for colouring the preview
    }

    // ------------------------------------------------------------------ settings

    private Road targetRoad;
    private Terrain targetTerrain;

    // Section of road to paint (spline param 0-1)
    private float startParam = 0.15f;   // the beginning of a level already has trees of its own
    private float endParam = 1f;

    // How far from the road's edge the band runs
    private float minOffsetFromRoad = 12f;
    private float maxOffsetFromRoad = 80f;

    // Spacing
    private float spacingAlongRoad = 8f;
    private float spacingAcrossRoad = 12f;

    // Randomisation
    private float randomOffset = 3f;
    private float randomScaleMin = 0.8f;
    private float randomScaleMax = 1.2f;
    private int seed = 20260101;

    // What not to paint over
    private bool keepExistingTrees = true;
    private float keepTreeRadius = 5f;
    private bool avoidStructures = true;
    private float structureRadius = 8f;
    private float maxSlope = 35f;

    // Tree prototype selection
    private bool[] selectedPrototypes;

    // Preview. The toggle is deliberately not called showPreview: an EditorWindow's fields are saved and
    // restored across a recompile, and the older version of this window had a showPreview that defaulted to
    // off - restored state would silently switch this one off too.
    private bool drawPreview = true;
    private bool showBand = true;
    private bool solidFootprints = true;
    private int previewLimit = 5000;
    private float previewRadiusScale = 1f;
    private int previewDrawn;
    private double lastPreviewDraw;

    // The plan, and the bookkeeping needed to undo the last paint
    private readonly List<PlannedTree> plan = new List<PlannedTree>();
    private float[] prototypeRadius;
    private bool planDirty = true;
    private int skippedOnTrees;
    private int skippedOnBuildings;
    private int skippedOnSlope;
    private int lastPaintFrom = -1;
    private int lastPaintTo = -1;

    private Vector2 scrollPos;

    [MenuItem("Tools/Road Tools/Paint Trees Along Road")]
    static void OpenWindow()
    {
        TerrainTreePainter window = GetWindow<TerrainTreePainter>("Tree Painter");
        window.minSize = new Vector2(360, 560);
        window.Show();
    }

    void OnEnable()
    {
        if (targetRoad == null)
        {
            Road[] roads = FindObjectsOfType<Road>();
            if (roads.Length > 0) targetRoad = roads[0];
        }

        if (targetTerrain == null) targetTerrain = Terrain.activeTerrain;

        // The scene view is where the preview is drawn, and an EditorWindow's own OnSceneGUI only runs while
        // the window has focus - which is exactly not the case as soon as the user clicks into the scene to
        // look at the preview. Subscribing is what keeps the discs on screen while they are being looked at,
        // and it is the same pair of lines every other painter in this folder uses.
        SceneView.duringSceneGui -= OnSceneGUI;
        SceneView.duringSceneGui += OnSceneGUI;
    }

    void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        EditorUtility.ClearProgressBar();
    }

    // ------------------------------------------------------------------ window

    void OnGUI()
    {
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        // A settings change rebuilds the plan, and the scene view is showing the old one until it is told to
        // draw again - which is what makes the preview feel like it is not following along.
        bool wasDirty = planDirty;

        GUILayout.Label("Paint Trees Along Road", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Adds terrain trees in a band down both sides of the road. The preview in the scene view is the " +
            "plan itself: what you see is what PAINT writes.",
            MessageType.Info);

        EditorGUI.BeginChangeCheck();

        EditorGUILayout.LabelField("References", EditorStyles.boldLabel);
        targetRoad = (Road)EditorGUILayout.ObjectField("Road", targetRoad, typeof(Road), true);
        targetTerrain = (Terrain)EditorGUILayout.ObjectField("Terrain", targetTerrain, typeof(Terrain), true);

        if (targetRoad == null || targetTerrain == null)
        {
            EditorGUILayout.HelpBox("Assign a Road and a Terrain.", MessageType.Warning);
            EditorGUILayout.EndScrollView();
            return;
        }

        if (targetRoad.spline == null || targetRoad.spline.distance <= 0.01f)
        {
            EditorGUILayout.HelpBox("The road has no built spline.", MessageType.Warning);
            EditorGUILayout.EndScrollView();
            return;
        }

        TerrainData data = targetTerrain.terrainData;

        if (data.treePrototypes.Length == 0)
        {
            EditorGUILayout.HelpBox("The terrain has no tree prototypes. Add tree prefabs to Terrain Data > " +
                                    "Trees, or use the Distant Scenery painter to make light copies.",
                                    MessageType.Error);
            EditorGUILayout.EndScrollView();
            return;
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Road Info", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("  Asphalt: " + targetRoad.RoadWidth() + "m   Shoulders: " +
                                   targetRoad.shoulderWidth + "m each   Spline: " +
                                   targetRoad.spline.distance.ToString("F0") + "m");

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Road Section (spline 0-1)", EditorStyles.boldLabel);
        startParam = EditorGUILayout.Slider("Start %", startParam, 0f, 1f);
        endParam = EditorGUILayout.Slider("End %", endParam, 0f, 1f);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("The Band", EditorStyles.boldLabel);
        minOffsetFromRoad = EditorGUILayout.Slider(
            new GUIContent("From Road Edge (m)", "How far past the asphalt and shoulders the band starts. " +
                                                 "Its own lower bound is the edge itself, so trees can never " +
                                                 "be planted on the road"),
            minOffsetFromRoad, 1f, 60f);
        maxOffsetFromRoad = EditorGUILayout.Slider(
            new GUIContent("Out To (m)", "Where the band stops, measured the same way. Everything past this " +
                                         "belongs to the Distant Scenery painter"),
            maxOffsetFromRoad, minOffsetFromRoad + 1f, 300f);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Density", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Sparse")) { spacingAlongRoad = 15f; spacingAcrossRoad = 18f; }
        if (GUILayout.Button("Normal")) { spacingAlongRoad = 8f; spacingAcrossRoad = 12f; }
        if (GUILayout.Button("Dense")) { spacingAlongRoad = 4f; spacingAcrossRoad = 8f; }
        if (GUILayout.Button("Very Dense")) { spacingAlongRoad = 2f; spacingAcrossRoad = 5f; }
        EditorGUILayout.EndHorizontal();
        spacingAlongRoad = EditorGUILayout.Slider("Along Road Spacing", spacingAlongRoad, 1f, 30f);
        spacingAcrossRoad = EditorGUILayout.Slider("Across Road Spacing", spacingAcrossRoad, 2f, 30f);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Randomisation", EditorStyles.boldLabel);
        randomOffset = EditorGUILayout.Slider(
            new GUIContent("Position Jitter (m)", "How far a tree may wander from the grid it was worked " +
                                                  "out on, which is what stops the band reading as a grid"),
            randomOffset, 0f, 20f);
        randomScaleMin = EditorGUILayout.Slider("Scale Min", randomScaleMin, 0.1f, 3f);
        randomScaleMax = EditorGUILayout.Slider("Scale Max", randomScaleMax, 0.1f, 3f);
        seed = EditorGUILayout.IntField(
            new GUIContent("Seed", "The same seed gives the same trees in the same places, every time - so a " +
                                   "plan can be compared with the last one"),
            seed);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("What Not To Paint Over", EditorStyles.boldLabel);
        keepExistingTrees = EditorGUILayout.ToggleLeft(
            new GUIContent("Keep Existing Trees", "Skip anywhere a tree already stands, so painting twice " +
                                                  "thickens the road rather than doubling it where it already is"),
            keepExistingTrees);
        GUI.enabled = keepExistingTrees;
        keepTreeRadius = EditorGUILayout.Slider("  Clearance (m)", keepTreeRadius, 1f, 20f);
        GUI.enabled = true;

        avoidStructures = EditorGUILayout.ToggleLeft(
            new GUIContent("Avoid Buildings", "Skip anywhere a building or prop already stands, so trees do not " +
                                              "come up through a roof"),
            avoidStructures);
        GUI.enabled = avoidStructures;
        structureRadius = EditorGUILayout.Slider("  Clearance (m)", structureRadius, 1f, 30f);
        GUI.enabled = true;

        maxSlope = EditorGUILayout.Slider(
            new GUIContent("Max Slope (deg)", "Steep ground is left bare. Trees on a cliff face read as trees " +
                                              "floating off it"),
            maxSlope, 0f, 60f);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
        drawPreview = EditorGUILayout.ToggleLeft("Show In Scene View", drawPreview);
        GUI.enabled = drawPreview;
        showBand = EditorGUILayout.ToggleLeft("Outline The Band", showBand);
        solidFootprints = EditorGUILayout.ToggleLeft(
            new GUIContent("Solid Footprints", "Draw a disc the size of each tree rather than a fixed ring. " +
                                               "Thousands of discs cost more to draw than thousands of rings"),
            solidFootprints);
        previewRadiusScale = EditorGUILayout.Slider("Footprint Scale", previewRadiusScale, 0.1f, 2f);
        previewLimit = EditorGUILayout.IntSlider(
            new GUIContent("Draw At Most", "The dots are drawing cost: past a few thousand the scene view gets " +
                                           "sluggish, so the rest are left out of the picture and counted here"),
            previewLimit, 100, 20000);
        GUI.enabled = true;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Tree Types", EditorStyles.boldLabel);

        if (selectedPrototypes == null || selectedPrototypes.Length != data.treePrototypes.Length)
        {
            selectedPrototypes = new bool[data.treePrototypes.Length];
            for (int i = 0; i < selectedPrototypes.Length; i++) selectedPrototypes[i] = true;
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("All", GUILayout.Width(44f)))
            for (int i = 0; i < selectedPrototypes.Length; i++) selectedPrototypes[i] = true;
        if (GUILayout.Button("None", GUILayout.Width(50f)))
            for (int i = 0; i < selectedPrototypes.Length; i++) selectedPrototypes[i] = false;
        EditorGUILayout.EndHorizontal();

        for (int i = 0; i < data.treePrototypes.Length; i++)
        {
            string name = data.treePrototypes[i].prefab != null ? data.treePrototypes[i].prefab.name : "null";
            selectedPrototypes[i] = EditorGUILayout.ToggleLeft("  [" + i + "] " + name, selectedPrototypes[i]);
        }

        if (EditorGUI.EndChangeCheck()) planDirty = true;

        EditorGUILayout.Space();

        // What the scene view is doing with the plan, said out loud: "no preview" and "an empty plan" look
        // identical in the scene view, and this is the line that tells the two apart.
        if (!drawPreview)
        {
            EditorGUILayout.LabelField("  The preview is switched off.", EditorStyles.miniLabel);
        }
        else if (plan.Count > 0 && previewDrawn == 0)
        {
            EditorGUILayout.LabelField("  The scene view has not redrawn since the plan changed.",
                                       EditorStyles.miniLabel);
        }
        else
        {
            EditorGUILayout.LabelField("  Drawing " + previewDrawn + " disc(s) in the scene view.",
                                       EditorStyles.miniLabel);
        }

        if (GUILayout.Button("Redraw The Scene View", GUILayout.Height(22f)))
        {
            planDirty = true;
            SceneView.RepaintAll();
        }

        DrawPlanSummary();

        EditorGUILayout.Space();
        GUI.backgroundColor = Color.green;
        if (GUILayout.Button("PAINT TREES", GUILayout.Height(40f)))
        {
            RebuildPlan();

            if (plan.Count == 0)
            {
                Debug.LogError("Tree Painter: nothing to paint. Check the section, the band and the tree types.");
            }
            else if (EditorUtility.DisplayDialog("Paint Trees",
                "Add " + plan.Count + " tree instances to '" + data.name + "' between " +
                (Mathf.Min(startParam, endParam) * 100f).ToString("F0") + "% and " +
                (Mathf.Max(startParam, endParam) * 100f).ToString("F0") + "% of the road?\n\n" +
                "What the preview shows is what will be written. This can be undone (Ctrl+Z).",
                "Paint", "Cancel"))
            {
                PaintTrees();
            }
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space();
        GUI.enabled = lastPaintFrom >= 0;
        if (GUILayout.Button("Undo My Last Paint", GUILayout.Height(24f))) UndoLastPaint();
        GUI.enabled = true;

        GUILayout.Label("Nothing else is touched by that button: trees the level came with, and trees the " +
                        "Distant Scenery painter added, are left where they are.",
                        EditorStyles.miniLabel);

        EditorGUILayout.Space();
        GUI.backgroundColor = new Color(1f, 0.6f, 0.6f);
        if (GUILayout.Button("Remove ALL Trees From This Terrain", GUILayout.Height(24f)))
        {
            if (EditorUtility.DisplayDialog("Remove All Trees",
                "Remove every tree instance from '" + data.name + "', including the level's own?\n\n" +
                "This can be undone, but it is the whole terrain.",
                "Remove All", "Cancel"))
            {
                RemoveAllTrees();
            }
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.EndScrollView();

        if (wasDirty) SceneView.RepaintAll();
    }

    private void DrawPlanSummary()
    {
        RebuildPlan();

        EditorGUILayout.LabelField("Plan", EditorStyles.boldLabel);

        if (plan.Count == 0)
        {
            EditorGUILayout.HelpBox("Nothing to paint yet. Check the section, the band and the tree types.",
                                    MessageType.Warning);
            return;
        }

        EditorGUILayout.LabelField("  " + plan.Count + " trees, " + SelectedPrototypeCount() + " type(s)");
        EditorGUILayout.LabelField("  " + skippedOnTrees + " skipped over existing trees, " + skippedOnBuildings +
                                   " over buildings, " + skippedOnSlope + " on ground too steep");

        if (plan.Count > previewLimit && drawPreview)
            EditorGUILayout.LabelField("  Preview draws " + previewLimit + " of them: raise Draw At Most to see " +
                                       "the rest", EditorStyles.miniLabel);

        TerrainData data = targetTerrain.terrainData;
        int total = data.treeInstances.Length;

        EditorGUILayout.LabelField("  Terrain holds " + total + " trees now, " + (total + plan.Count) +
                                   " once painted");
        GUILayout.Label("  Trees are instances on the terrain, so this is what the terrain already draws - " +
                        "not new objects in the scene.",
                        EditorStyles.miniLabel);
    }

    // ------------------------------------------------------------------ preview

    /// <summary>
    /// Draws the plan in the scene view. Everything drawn here comes straight out of the plan the paint button
    /// will use, so a disc that is on screen is a tree that will be written and a gap that is on screen is a
    /// tree that has already been skipped.
    /// </summary>
    void OnSceneGUI(SceneView sceneView)
    {
        // Unity calls a method of this name on the window that has focus as well as through the subscription
        // above, so without this the discs would be drawn twice over while the window is in front.
        double now = EditorApplication.timeSinceStartup;
        if (now - lastPreviewDraw < 0.0005) return;
        lastPreviewDraw = now;

        if (!drawPreview) return;

        RebuildPlan();

        if (targetTerrain == null || plan.Count == 0)
        {
            previewDrawn = 0;
            return;
        }

        if (showBand && targetRoad != null && targetRoad.spline != null) DrawBand();

        int step = Mathf.Max(1, Mathf.CeilToInt(plan.Count / (float)Mathf.Max(1, previewLimit)));
        int drawn = 0;

        for (int i = 0; i < plan.Count; i += step)
        {
            PlannedTree tree = plan[i];

            float radius = Mathf.Max(1f, PrototypeRadius(tree.prototype) * tree.scale * previewRadiusScale);
            Vector3 at = tree.world + Vector3.up * 0.05f;

            Handles.color = tree.side < 0 ? new Color(0.3f, 1f, 0.4f) : new Color(0.35f, 0.75f, 1f);

            if (solidFootprints) Handles.DrawSolidDisc(at, Vector3.up, radius);
            else Handles.DrawWireDisc(at, Vector3.up, radius);

            drawn++;
        }

        // One label, over the first tree the plan intends to place: it is the thing that proves the preview
        // is running at all, even when the discs themselves are hard to pick out in a wide view.
        Handles.color = Color.white;
        Handles.Label(plan[0].world + Vector3.up * 5f,
                      "Tree Painter: " + plan.Count + " tree(s) planned,\ndrawing " + drawn + " here");

        previewDrawn = drawn;
    }

    /// <summary>
    /// The two lines the band runs between, down each side of the road: the edge trees may not come inside, and
    /// the outer limit they stop at.
    /// </summary>
    private void DrawBand()
    {
        SplineC spline = targetRoad.spline;
        float total = spline.distance;
        float edge = RoadRoute.RoadEdge(targetRoad);

        float from = Mathf.Clamp01(Mathf.Min(startParam, endParam));
        float to = Mathf.Clamp01(Mathf.Max(startParam, endParam));

        int count = Mathf.Max(2, Mathf.CeilToInt((to - from) * total / 10f));

        Handles.color = new Color(1f, 0.85f, 0.3f);

        for (int side = -1; side <= 1; side += 2)
        {
            Vector3[] inner = new Vector3[count + 1];
            Vector3[] outer = new Vector3[count + 1];

            for (int i = 0; i <= count; i++)
            {
                float param = Mathf.Lerp(from, to, i / (float)count);

                Vector3 position, tangent;
                spline.GetSplineValueBoth(param, out position, out tangent);

                Vector3 right = RoadRoute.Across(tangent) * side;

                inner[i] = position + right * (edge + minOffsetFromRoad) + Vector3.up * 0.1f;
                outer[i] = position + right * (edge + maxOffsetFromRoad) + Vector3.up * 0.1f;
            }

            Handles.DrawAAPolyLine(4f, inner);
            Handles.DrawAAPolyLine(2f, outer);
        }
    }

    // ------------------------------------------------------------------ plan

    /// <summary>
    /// Works the plan out from the seed, once: the same settings give the same plan every time, and the preview
    /// and the paint both read it rather than working anything out for themselves.
    /// </summary>
    private void RebuildPlan()
    {
        if (!planDirty) return;

        plan.Clear();
        skippedOnTrees = 0;
        skippedOnBuildings = 0;
        skippedOnSlope = 0;
        planDirty = false;

        if (targetRoad == null || targetRoad.spline == null || targetTerrain == null) return;
        if (targetRoad.spline.distance <= 0.01f) return;
        if (spacingAlongRoad <= 0f || spacingAcrossRoad <= 0f) return;

        TerrainData data = targetTerrain.terrainData;
        if (data == null || data.treePrototypes.Length == 0) return;

        List<int> types = SelectedPrototypes();
        if (types.Count == 0) return;

        // The plan is a rolling session: navigating away from the window and back must find the same plan.
        Random.State previous = Random.state;
        Random.InitState(seed);

        SplineC spline = targetRoad.spline;
        float total = spline.distance;
        float edge = RoadRoute.RoadEdge(targetRoad);
        Vector3 origin = targetTerrain.transform.position;
        Vector3 size = data.size;

        // What is already there, so the band is thickened rather than doubled up. Both are asked as grids, so
        // a plan over thousands of candidates stays quick.
        PointGrid trees = keepExistingTrees ? new PointGrid(RoadRoute.TreePoints(), 8f) : null;
        PointGrid structures = avoidStructures
            ? new PointGrid(RoadRoute.StructurePoints(RoadRoute.PainterRoots()), 8f)
            : null;

        float from = Mathf.Clamp01(Mathf.Min(startParam, endParam));
        float to = Mathf.Clamp01(Mathf.Max(startParam, endParam));

        float jitter = Mathf.Max(0f, randomOffset);
        float scaleLow = Mathf.Min(randomScaleMin, randomScaleMax);
        float scaleHigh = Mathf.Max(randomScaleMin, randomScaleMax);

        int steps = Mathf.Max(1, Mathf.CeilToInt((to - from) * total / spacingAlongRoad));

        for (int i = 0; i <= steps; i++)
        {
            float param = from + (i * spacingAlongRoad) / total;
            if (param > to) break;

            Vector3 position, tangent;
            spline.GetSplineValueBoth(Mathf.Clamp01(param), out position, out tangent);

            Vector3 across = RoadRoute.Across(tangent);

            if (across.sqrMagnitude < 0.0001f) continue;
            across.Normalize();

            for (int side = -1; side <= 1; side += 2)
            {
                for (float offset = minOffsetFromRoad; offset <= maxOffsetFromRoad; offset += spacingAcrossRoad)
                {
                    Vector3 world = position + across * (side * (edge + offset));

                    // The jitter is taken in the road's own frame, so a wandering tree still keeps its place in
                    // the band instead of being pushed across the verge and onto the asphalt.
                    float acrossJitter = Random.Range(-jitter, jitter);
                    float alongJitter = Random.Range(-jitter, jitter);

                    float lateral = edge + offset + acrossJitter;

                    if (lateral < edge + 1f) lateral = edge + 1f;

                    world = position + across * (side * lateral) + tangent.normalized * alongJitter;

                    float nx = (world.x - origin.x) / size.x;
                    float nz = (world.z - origin.z) / size.z;

                    if (nx < 0f || nx > 1f || nz < 0f || nz > 1f) continue;

                    Vector3 normal = data.GetInterpolatedNormal(nx, nz);
                    if (maxSlope < 89.9f && Vector3.Angle(normal, Vector3.up) > maxSlope)
                    {
                        skippedOnSlope++;
                        continue;
                    }

                    Vector2 flat = new Vector2(world.x, world.z);

                    if (trees != null && trees.Any(flat, keepTreeRadius))
                    {
                        skippedOnTrees++;
                        continue;
                    }

                    if (structures != null && structures.Any(flat, structureRadius))
                    {
                        skippedOnBuildings++;
                        continue;
                    }

                    float y = data.GetInterpolatedHeight(nx, nz);

                    PlannedTree tree = new PlannedTree();
                    tree.normalized = new Vector3(nx, y / size.y, nz);
                    tree.world = new Vector3(world.x, y, world.z);
                    tree.prototype = types[Random.Range(0, types.Count)];
                    tree.scale = Random.Range(scaleLow, scaleHigh);
                    tree.side = side;

                    plan.Add(tree);
                }
            }
        }

        Random.state = previous;
    }

    /// <summary>
    /// How big a tree of each type is, from the prefab's own bounds, so the preview draws a footprint rather
    /// than a ring that means nothing at the scale of the level.
    /// </summary>
    private float PrototypeRadius(int index)
    {
        TerrainData data = targetTerrain != null ? targetTerrain.terrainData : null;
        if (data == null) return 1f;

        if (prototypeRadius == null || prototypeRadius.Length != data.treePrototypes.Length)
            prototypeRadius = new float[data.treePrototypes.Length];

        if (prototypeRadius[index] > 0f) return prototypeRadius[index];

        float radius = 1f;
        GameObject prefab = data.treePrototypes[index].prefab;

        if (prefab != null)
        {
            Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>();

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;

                Vector3 size = renderers[i].localBounds.size;
                radius = Mathf.Max(radius, Mathf.Max(size.x, size.z) * 0.5f);
            }
        }

        prototypeRadius[index] = radius;

        return radius;
    }

    private List<int> SelectedPrototypes()
    {
        List<int> types = new List<int>();
        TerrainData data = targetTerrain.terrainData;

        for (int i = 0; i < data.treePrototypes.Length; i++)
        {
            if (selectedPrototypes != null && i < selectedPrototypes.Length && selectedPrototypes[i]) types.Add(i);
        }

        return types;
    }

    private int SelectedPrototypeCount()
    {
        int count = 0;

        if (selectedPrototypes != null)
            for (int i = 0; i < selectedPrototypes.Length; i++)
                if (selectedPrototypes[i]) count++;

        return count;
    }

    // ------------------------------------------------------------------ paint

    private void PaintTrees()
    {
        RebuildPlan();

        if (plan.Count == 0) return;

        TerrainData data = targetTerrain.terrainData;

        TreeInstance[] existing = data.treeInstances;
        TreeInstance[] merged = new TreeInstance[existing.Length + plan.Count];

        System.Array.Copy(existing, merged, existing.Length);

        try
        {
            for (int i = 0; i < plan.Count; i++)
            {
                if (i % 250 == 0 &&
                    EditorUtility.DisplayCancelableProgressBar("Painting Trees",
                        "Placing trees... " + i + " of " + plan.Count, i / (float)plan.Count))
                {
                    System.Array.Resize(ref merged, existing.Length + i);
                    break;
                }

                PlannedTree planned = plan[i];

                TreeInstance tree = new TreeInstance();
                tree.position = planned.normalized;
                tree.prototypeIndex = planned.prototype;
                tree.widthScale = planned.scale;
                tree.heightScale = planned.scale;
                tree.color = Color.white;
                tree.lightmapColor = Color.white;

                merged[existing.Length + i] = tree;
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        // The terrain data is an asset rather than a scene object, so it is the asset that is recorded: that is
        // what makes the Ctrl+Z this window promises actually work.
        Undo.RegisterCompleteObjectUndo(data, "Paint Trees");

        data.treeInstances = merged;

        lastPaintFrom = existing.Length;
        lastPaintTo = merged.Length;

        EditorUtility.SetDirty(data);
        RoadRoute.MarkSceneDirty();

        Debug.Log("Tree Painter: painted " + (merged.Length - existing.Length) + " trees along '" +
                  targetRoad.name + "' (params " + Mathf.Min(startParam, endParam).ToString("F2") + " - " +
                  Mathf.Max(startParam, endParam).ToString("F2") + ", " + skippedOnTrees + " skipped over trees, " +
                  skippedOnBuildings + " over buildings, " + skippedOnSlope + " on steep ground).");
    }

    /// <summary>
    /// Takes back the instances the last paint appended, and only those. Terrain trees are one flat list, so
    /// "the ones this window added" is remembered as the tail past the count it started from - which is exact
    /// as long as nothing else has painted the terrain since.
    /// </summary>
    private void UndoLastPaint()
    {
        if (targetTerrain == null || lastPaintFrom < 0) return;

        TerrainData data = targetTerrain.terrainData;
        TreeInstance[] instances = data.treeInstances;

        if (instances.Length != lastPaintTo)
        {
            Debug.LogWarning("Tree Painter: the terrain has changed since the last paint (" + instances.Length +
                             " trees, expected " + lastPaintTo + "), so the last paint cannot be picked out of " +
                             "it. Use Ctrl+Z instead.");
            return;
        }

        TreeInstance[] trimmed = new TreeInstance[lastPaintFrom];
        System.Array.Copy(instances, trimmed, lastPaintFrom);

        Undo.RegisterCompleteObjectUndo(data, "Undo Painted Trees");

        data.treeInstances = trimmed;

        EditorUtility.SetDirty(data);

        Debug.Log("Tree Painter: took back " + (lastPaintTo - lastPaintFrom) + " trees.");

        lastPaintFrom = -1;
        lastPaintTo = -1;
    }

    private void RemoveAllTrees()
    {
        if (targetTerrain == null) return;

        TerrainData data = targetTerrain.terrainData;

        Undo.RegisterCompleteObjectUndo(data, "Remove All Trees");

        data.treeInstances = new TreeInstance[0];

        EditorUtility.SetDirty(data);

        lastPaintFrom = -1;
        lastPaintTo = -1;

        Debug.Log("Tree Painter: removed every tree from '" + data.name + "'.");
    }
}
