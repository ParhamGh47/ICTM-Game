using System.Collections.Generic;
using RoadArchitect;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Fills the land away from the road - the part of a level seen only from a distance - with scenery, cheaply.
///
/// The roadside painters put things where the player drives past them: a tree there is something to look at
/// from ten metres, and the level's own trees carry colliders and cast shadows because at that range both
/// matter. Out in the middle distance neither does. Nothing can be driven into that far from the road, and a
/// tree a hundred metres away casts a shadow nobody can see - but the terrain still pays for both, every one of
/// them, every frame. So this painter works in two halves:
///
/// <b>Light copies are the only thing it paints with.</b> It clones the level's own tree prefabs, takes the
/// colliders and the shadow casting off the clones, and registers those clones as tree prototypes; the level's
/// own types are never placed by this tool, whatever state the terrain is in. If the copies do not exist yet,
/// PAINT makes them before it plans anything. A collider-free tree is not merely cheaper to
/// simulate - it is not simulated at all, which is what turns a forest from a physics problem into a drawing
/// problem. The originals are never touched: the trees that already line the road keep the colliders they were
/// placed with.
///
/// <b>Terrain trees, not objects.</b> Everything placed is a tree instance on the terrain, because that is the
/// only way to draw thousands of trees in this project: instanced, culled by patch, and a single quad apiece
/// past the billboard distance. Ten thousand of them cost less than a hundred objects in the scene would.
///
/// Placement gathers where scenery already is - around the level's own woods and along the road corridor - and
/// leaves the corridor itself, and any steep ground, bare. It is seeded, so a plan can be compared with the
/// last one, and the plan is what the preview draws and what PAINT writes.
///
/// Usage: open a level scene (Core-1 ... Core-4), then
///   Tools &gt; Road Tools &gt; Paint Distant Scenery
/// </summary>
public class DistantSceneryPainter : EditorWindow
{
    /// <summary>Where the light copies of the level's tree prefabs are kept.</summary>
    private const string LightFolder = "Assets/Prefabs/Scenery/Light";

    /// <summary>One tree the plan intends to place.</summary>
    private struct PlannedTree
    {
        public Vector3 normalized;
        public Vector3 world;
        public int prototype;
        public float scale;
        public int band;
    }

    // ------------------------------------------------------------------ settings

    // 0 is the band the road's own verge already covers, so it is never painted; 1 is near, 2 is far
    private float corridor = 45f;
    private float nearOutTo = 160f;
    private float farOutTo = 1500f;

    private float nearDensity = 40f;        // trees per hectare
    private float farDensity = 10f;
    private float nearScale = 1f;
    private float farScale = 1.35f;

    // Clumping
    private float clumpScale = 0.004f;      // noise frequency: smaller means bigger woods
    private float clumpStrength = 0.75f;
    private float woodBias = 0.5f;          // how much existing woods attract more trees

    private float maxSlope = 32f;
    private float structureClearance = 12f;

    private int seed = 20260201;
    private int budget = 40000;

    // Preview
    private bool showPreview = true;
    private bool showCorridor = true;
    private int previewLimit = 4000;
    private int previewDrawn;
    private double lastPreviewDraw;

    // The plan
    private readonly List<PlannedTree> plan = new List<PlannedTree>();
    private bool planDirty = true;
    private int skippedCorridor;
    private int skippedSlope;
    private int skippedBudget;
    private int skippedStructures;
    private float nearHectares;
    private float farHectares;
    private int[] placedPerPrototype;

    private Vector2 scrollPos;

    [MenuItem("Tools/Road Tools/Paint Distant Scenery")]
    static void OpenWindow()
    {
        DistantSceneryPainter window = GetWindow<DistantSceneryPainter>("Distant Scenery");
        window.minSize = new Vector2(380, 620);
        window.Show();
    }

    void OnEnable()
    {
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

        GUILayout.Label("Paint Distant Scenery", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Fills the land beyond the road corridor with terrain trees, using light collider-free copies of " +
            "the level's own trees. Everything placed is an instance on the terrain, so thousands of them cost " +
            "about what the level's own trees cost.",
            MessageType.Info);

        Terrain terrain = Terrain.activeTerrain;

        if (terrain == null || terrain.terrainData == null)
        {
            EditorGUILayout.HelpBox("No terrain in this scene.", MessageType.Error);
            EditorGUILayout.EndScrollView();
            return;
        }

        EditorGUI.BeginChangeCheck();

        EditorGUILayout.LabelField("Light Copies", EditorStyles.boldLabel);

        int lightCount = CountLightPrototypes(terrain.terrainData);
        int sourceCount = CountSourcePrototypes(terrain.terrainData);

        EditorGUILayout.LabelField("  " + sourceCount + " tree type(s) in the level, " + lightCount +
                                   " light cop" + (lightCount == 1 ? "y" : "ies"));

        GUILayout.Label("There is no option to paint with the level's own trees here: those keep their " +
                        "colliders and their shadow casting, and out at this distance neither is ever seen - " +
                        "the colliders are simply simulated every frame. Everything this tool places is a " +
                        "terrain instance of a collider-free, shadow-free copy.", EditorStyles.miniLabel);

        if (lightCount == 0)
        {
            EditorGUILayout.HelpBox("No light copies yet. PAINT makes them first - it clones the level's tree " +
                                    "prefabs and strips their colliders and shadows.", MessageType.Warning);
        }

        if (GUILayout.Button("Make Light Copies (PAINT does this too)", GUILayout.Height(24f)))
        {
            MakeLightCopies(terrain);
            planDirty = true;
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Where", EditorStyles.boldLabel);
        corridor = EditorGUILayout.Slider(
            new GUIContent("Keep Road Corridor Clear (m)", "Nothing is placed within this of a road's edge: " +
                                                           "this is the band the roadside painters own, and it " +
                                                           "is also what keeps scenery out of the way of a " +
                                                           "level's jumps and bridges"),
            corridor, 20f, 200f);
        nearOutTo = EditorGUILayout.Slider(
            new GUIContent("Near Band Out To (m)", "Where the near band stops, measured from the road's edge"),
            nearOutTo, corridor + 10f, 600f);
        farOutTo = EditorGUILayout.Slider(
            new GUIContent("Far Band Out To (m)", "Where scenery stops altogether. Past the terrain's own edge " +
                                                  "nothing is placed whatever this says"),
            farOutTo, nearOutTo + 10f, 3000f);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("How Much", EditorStyles.boldLabel);
        nearDensity = EditorGUILayout.Slider(
            new GUIContent("Near Band Density (per ha)", "Trees per hectare in the near band. A hectare is a " +
                                                         "square 100 m on a side"),
            nearDensity, 1f, 200f);
        farDensity = EditorGUILayout.Slider("Far Band Density (per ha)", farDensity, 1f, 200f);
        nearScale = EditorGUILayout.Slider("Near Band Scale", nearScale, 0.2f, 3f);
        farScale = EditorGUILayout.Slider(
            new GUIContent("Far Band Scale", "Bigger for the far band makes the horizon read as distance rather " +
                                             "than as a smaller copy of the near land"),
            farScale, 0.2f, 3f);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Gathering", EditorStyles.boldLabel);
        clumpScale = EditorGUILayout.Slider(
            new GUIContent("Wood Size", "How large the woods are, as a frequency: lower means fewer, larger " +
                                        "woods"),
            clumpScale, 0.0005f, 0.01f);
        clumpStrength = EditorGUILayout.Slider("Wood Strength", clumpStrength, 0f, 1f);
        woodBias = EditorGUILayout.Slider(
            new GUIContent("Gather Where Trees Are", "How strongly the level's existing woods attract more " +
                                                     "trees, so the land thickens where it is already wooded " +
                                                     "instead of sprouting a new wood beside it"),
            woodBias, 0f, 1f);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Rules", EditorStyles.boldLabel);
        maxSlope = EditorGUILayout.Slider(
            new GUIContent("Max Slope (deg)", "Ground steeper than this is left bare: trees on a cliff face read " +
                                              "as trees hanging off it"),
            maxSlope, 0f, 60f);
        structureClearance = EditorGUILayout.Slider(
            new GUIContent("Clear Of Buildings (m)", "Nothing is placed within this of a building or prop"),
            structureClearance, 0f, 40f);
        seed = EditorGUILayout.IntField("Seed", seed);
        budget = EditorGUILayout.IntSlider(
            new GUIContent("Instance Budget", "The most this will place in one go. The plan is cut off here " +
                                              "rather than allowed to run away: terrain trees are cheap, but " +
                                              "they are not free"),
            budget, 500, 60000);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
        showPreview = EditorGUILayout.ToggleLeft("Show In Scene View", showPreview);
        GUI.enabled = showPreview;
        showCorridor = EditorGUILayout.ToggleLeft(
            new GUIContent("Outline The Corridor", "Draw the edge of the kept-clear zone down both sides of " +
                                                    "every road, in orange"),
            showCorridor);
        previewLimit = EditorGUILayout.IntSlider("Draw At Most", previewLimit, 100, 20000);
        GUI.enabled = true;

        if (EditorGUI.EndChangeCheck()) planDirty = true;

        EditorGUILayout.Space();

        // "No preview" and "nothing planned" look the same in the scene view, so the window says which of the
        // two it is looking at.
        if (!showPreview)
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
            EditorGUILayout.LabelField("  Drawing " + previewDrawn + " ring(s) in the scene view.",
                                       EditorStyles.miniLabel);
        }

        if (GUILayout.Button("Redraw The Scene View", GUILayout.Height(22f)))
        {
            planDirty = true;
            SceneView.RepaintAll();
        }

        DrawPlanSummary(terrain);

        EditorGUILayout.Space();
        GUI.backgroundColor = Color.green;

        if (GUILayout.Button("PAINT DISTANT SCENERY", GUILayout.Height(40f)))
        {
            // The light copies are made here rather than trusted to have been made: they are the whole reason
            // this is affordable at map scale, and a painter that quietly falls back to collider-bearing trees
            // is a painter that quietly ruins the frame rate.
            bool ready = EnsureLightCopies(terrain);

            // A plan built before those copies existed knows nothing about them - its tree types are the light
            // ones, and there were none - so it is worked out again from here.
            if (ready)
            {
                planDirty = true;
                RebuildPlan(terrain);
            }

            if (!ready || plan.Count == 0)
            {
                Debug.LogError("Distant Scenery: nothing to place. Check the corridor, the band limits and that " +
                               "the level's own trees could be copied.");
            }
            else if (EditorUtility.DisplayDialog("Paint Distant Scenery",
                "Add " + plan.Count + " tree instances to '" + terrain.terrainData.name + "' beyond " +
                corridor.ToString("F0") + " m of the road?\n\n" +
                "All of them are light collider-free, shadow-free copies. This can be undone (Ctrl+Z).",
                "Paint", "Cancel"))
            {
                Paint(terrain);
            }
        }

        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space();
        GUI.backgroundColor = new Color(1f, 0.7f, 0.4f);
        if (GUILayout.Button("Remove Painted Scenery", GUILayout.Height(24f)))
        {
            if (EditorUtility.DisplayDialog("Remove Painted Scenery",
                "Remove every tree instance that uses a light copy, and drop the light copies from the " +
                "terrain?\n\nThe level's own trees are left alone. This can be undone.",
                "Remove", "Cancel"))
            {
                RemovePainted(terrain);
            }
        }
        GUI.backgroundColor = Color.white;

        GUILayout.Label("The light copy prefabs themselves are left in " + LightFolder +
                        " for the next level to use.", EditorStyles.miniLabel);

        EditorGUILayout.EndScrollView();
    }

    private void DrawPlanSummary(Terrain terrain)
    {
        RebuildPlan(terrain);

        EditorGUILayout.LabelField("Plan", EditorStyles.boldLabel);

        if (plan.Count == 0)
        {
            EditorGUILayout.HelpBox("Nothing to place yet. Check the corridor, the band limits and that there " +
                                    "are tree types to copy.", MessageType.Warning);
            return;
        }

        EditorGUILayout.LabelField("  " + plan.Count + " trees over " + (nearHectares + farHectares).ToString("F1") +
                                   " ha of usable ground");
        EditorGUILayout.LabelField("  Near band: " + nearHectares.ToString("F1") + " ha at " +
                                   nearDensity.ToString("F0") + "/ha      Far band: " + farHectares.ToString("F1") +
                                   " ha at " + farDensity.ToString("F0") + "/ha");
        EditorGUILayout.LabelField("  Skipped: " + skippedCorridor + " inside the corridor, " + skippedSlope +
                                   " too steep, " + skippedStructures + " on buildings" +
                                   (skippedBudget > 0 ? ", " + skippedBudget + " over the budget" : ""));

        GUILayout.Label("  All " + plan.Count + " are terrain tree instances of light copies: no GameObjects, " +
                        "no colliders, no shadow casting. They cost about what the level's own trees cost, and " +
                        "the terrain billboards them past its billboard distance.", EditorStyles.miniLabel);

        if (skippedBudget > 0)
        {
            EditorGUILayout.HelpBox("The budget stopped this at " + budget + " trees. Raise it, or lower the " +
                                    "densities, and it will place what is left.", MessageType.Warning);
        }

        TerrainData data = terrain.terrainData;
        int total = data.treeInstances.Length;

        EditorGUILayout.LabelField("  Terrain holds " + total + " trees now, " + (total + plan.Count) +
                                   " once painted");
    }

    // ------------------------------------------------------------------ light copies

    /// <summary>
    /// Whether a prototype is one of this painter's light copies, recognised by where its prefab lives rather
    /// than by an index remembered from the last session: the terrain's prototype list is edited by hand in the
    /// inspector, and an index means nothing across two runs of the editor.
    /// </summary>
    private static bool IsLightPrototype(TreePrototype prototype)
    {
        if (prototype == null || prototype.prefab == null) return false;

        string path = AssetDatabase.GetAssetPath(prototype.prefab);

        return !string.IsNullOrEmpty(path) && path.StartsWith(LightFolder);
    }

    private static int CountLightPrototypes(TerrainData data)
    {
        int count = 0;

        for (int i = 0; i < data.treePrototypes.Length; i++)
            if (IsLightPrototype(data.treePrototypes[i])) count++;

        return count;
    }

    private static int CountSourcePrototypes(TerrainData data)
    {
        int count = 0;

        for (int i = 0; i < data.treePrototypes.Length; i++)
            if (!IsLightPrototype(data.treePrototypes[i])) count++;

        return count;
    }

    /// <summary>
    /// Makes sure the terrain has light copies to paint with. Returns true when there is at least one, and says
    /// what is missing in the console when there is not.
    /// </summary>
    private bool EnsureLightCopies(Terrain terrain)
    {
        TerrainData data = terrain.terrainData;

        if (CountLightPrototypes(data) > 0) return true;

        MakeLightCopies(terrain);

        if (CountLightPrototypes(data) > 0) return true;

        int sources = CountSourcePrototypes(data);

        Debug.LogError("Distant Scenery: there is nothing to paint with. The terrain has " + sources +
                       " tree type(s) of its own" +
                       (sources == 0
                           ? ", so add a tree prefab to it (or to another level's terrain) to copy first."
                           : ", but its light copies could not be made - see the message above."));

        return false;
    }

    /// <summary>
    /// Copies the level's own tree prefabs, one at a time, and takes the colliders and the shadow casting off
    /// each copy. The copy is what gets registered as a prototype; the level's own tree stays exactly as it was,
    /// so the trees that line the road keep the colliders and the shadows they were placed with.
    ///
    /// A copy that already exists is reused rather than made again, so this is safe to press twice.
    /// </summary>
    private void MakeLightCopies(Terrain terrain)
    {
        TerrainData data = terrain.terrainData;

        if (!AssetDatabase.IsValidFolder(LightFolder))
        {
            EnsureFolder("Assets/Prefabs", "Scenery");
            EnsureFolder("Assets/Prefabs/Scenery", "Light");
        }

        List<TreePrototype> prototypes = new List<TreePrototype>(data.treePrototypes);

        // Only the level's own types are copied, and only once: this list grows as copies are added, and a
        // second press of the button must not add a second copy of everything it added the first time.
        int sourceCount = prototypes.Count;

        HashSet<string> already = new HashSet<string>();

        for (int i = 0; i < prototypes.Count; i++)
        {
            if (IsLightPrototype(prototypes[i])) already.Add(prototypes[i].prefab.name);
        }

        int made = 0;
        int reused = 0;
        int failed = 0;

        try
        {
            for (int i = 0; i < sourceCount; i++)
            {
                TreePrototype prototype = prototypes[i];

                if (prototype == null || prototype.prefab == null) continue;
                if (IsLightPrototype(prototype)) continue;

                string path = LightFolder + "/" + prototype.prefab.name + " (Light).prefab";

                if (already.Contains(prototype.prefab.name + " (Light)"))
                {
                    reused++;
                    continue;
                }
                GameObject copy = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                if (copy != null)
                {
                    reused++;
                }
                else
                {
                    copy = MakeLightCopy(prototype.prefab, path);

                    if (copy == null)
                    {
                        failed++;
                        continue;
                    }

                    made++;
                }

                TreePrototype light = new TreePrototype();
                light.prefab = copy;
                light.bendFactor = prototype.bendFactor;
                light.navMeshLod = prototype.navMeshLod;

                prototypes.Add(light);
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        data.treePrototypes = prototypes.ToArray();

        EditorUtility.SetDirty(data);

        Debug.Log("Distant Scenery: " + made + " light cop" + (made == 1 ? "y" : "ies") + " made, " + reused +
                  " reused" + (failed > 0 ? ", " + failed + " could not be copied" : "") + " -> " +
                  prototypes.Count + " tree types on '" + data.name + "'. Colliders and shadow casting are off " +
                  "on the copies; the level's own trees are untouched.");
    }

    private static GameObject MakeLightCopy(GameObject source, string path)
    {
        GameObject instance = PrefabUtility.InstantiatePrefab(source) as GameObject;

        if (instance == null) return null;

        try
        {
            // A component that belongs to a nested prefab can only be removed from the prefab it came from, so
            // the copy is unpacked before anything is stripped off it. The original is not touched either way.
            if (PrefabUtility.IsPartOfPrefabInstance(instance))
                PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

            // Colliders first: a terrain tree's colliders are what the physics spends its time on, and there is
            // nothing out here for the player to hit.
            Collider[] colliders = instance.GetComponentsInChildren<Collider>(true);

            for (int i = 0; i < colliders.Length; i++) DestroyImmediate(colliders[i]);

            // Then shadows. A tree a hundred metres away casts a shadow that cannot be seen, from a shadow map
            // that has to be drawn and rendered every frame all the same.
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);

            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].shadowCastingMode = ShadowCastingMode.Off;
                renderers[i].receiveShadows = false;
            }

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(instance, path);

            return saved;
        }
        finally
        {
            DestroyImmediate(instance);
        }
    }

    private static void EnsureFolder(string parent, string child)
    {
        if (!AssetDatabase.IsValidFolder(parent + "/" + child))
            AssetDatabase.CreateFolder(parent, child);
    }

    /// <summary>Removes everything this painter placed, and the light copies it registered, and nothing else.</summary>
    private void RemovePainted(Terrain terrain)
    {
        TerrainData data = terrain.terrainData;

        List<int> light = new List<int>();

        for (int i = 0; i < data.treePrototypes.Length; i++)
            if (IsLightPrototype(data.treePrototypes[i])) light.Add(i);

        if (light.Count == 0)
        {
            Debug.LogWarning("Distant Scenery: no light copies on this terrain, so there is nothing to remove. " +
                             "The level's own trees are never touched by this tool.");
            return;
        }

        TreeInstance[] instances = data.treeInstances;
        List<TreeInstance> kept = new List<TreeInstance>(instances.Length);
        int removed = 0;

        for (int i = 0; i < instances.Length; i++)
        {
            if (light.Contains(instances[i].prototypeIndex)) removed++;
            else kept.Add(instances[i]);
        }

        // The copies go with them, and they are at the end of the list by construction, so removing them cannot
        // renumber anything else.
        List<TreePrototype> prototypes = new List<TreePrototype>(data.treePrototypes);

        for (int i = prototypes.Count - 1; i >= 0; i--)
            if (IsLightPrototype(prototypes[i])) prototypes.RemoveAt(i);

        Undo.RegisterCompleteObjectUndo(data, "Remove Distant Scenery");

        data.treeInstances = kept.ToArray();
        data.treePrototypes = prototypes.ToArray();

        EditorUtility.SetDirty(data);

        planDirty = true;

        Debug.Log("Distant Scenery: removed " + removed + " trees and " + light.Count + " light cop" +
                  (light.Count == 1 ? "y" : "ies") + " from '" + data.name + "'. " + kept.Count +
                  " trees left, which are the level's own.");
    }

    // ------------------------------------------------------------------ preview

    void OnSceneGUI(SceneView sceneView)
    {
        // Unity calls a method of this name on the focused window as well as through the subscription above,
        // so without this the rings would be drawn twice over while the window is in front.
        double now = EditorApplication.timeSinceStartup;
        if (now - lastPreviewDraw < 0.0005) return;
        lastPreviewDraw = now;

        if (!showPreview) return;

        Terrain terrain = Terrain.activeTerrain;
        if (terrain == null) return;

        RebuildPlan(terrain);

        if (showCorridor) DrawCorridor();

        if (plan.Count == 0)
        {
            previewDrawn = 0;
            return;
        }

        int step = Mathf.Max(1, Mathf.CeilToInt(plan.Count / (float)Mathf.Max(1, previewLimit)));
        int drawn = 0;

        for (int i = 0; i < plan.Count; i += step)
        {
            PlannedTree tree = plan[i];

            float radius = Mathf.Clamp(PrototypeRadius(terrain.terrainData, tree.prototype) * tree.scale, 2f, 12f);
            Vector3 at = tree.world + Vector3.up * 0.05f;

            // Wire rings rather than solid discs: this is the painter for when there are thousands of trees on
            // screen, and drawing them is the one part of it that is not free. Opaque, so a ring is not lost
            // against the ground at the sort of distance a whole level is looked at from.
            Handles.color = tree.band == 1 ? new Color(0.35f, 1f, 0.45f) : new Color(0.4f, 0.8f, 1f);

            Handles.DrawWireDisc(at, Vector3.up, radius);

            drawn++;
        }

        // One label, over the first tree the plan intends to place: it is the thing that proves the preview is
        // running at all, even when the rings are hard to pick out in a wide view.
        Handles.color = Color.white;
        Handles.Label(plan[0].world + Vector3.up * 8f,
                      "Distant Scenery: " + plan.Count + " tree(s) planned,\ndrawing " + drawn + " here");

        previewDrawn = drawn;
    }

    /// <summary>
    /// The corridor the plan keeps clear, drawn down each side of every road in the scene. It is the edge of
    /// what this tool will not touch, and it is the quickest thing in the preview to see.
    /// </summary>
    private void DrawCorridor()
    {
        List<RoadRouteEntry> roads = RoadRoute.FindRoadsInScene();

        Handles.color = new Color(1f, 0.5f, 0.2f);

        for (int r = 0; r < roads.Count; r++)
        {
            Road road = roads[r].road;
            if (road == null || road.spline == null) continue;
            if (road.spline.distance <= 0.01f) continue;

            SplineC spline = road.spline;
            int count = Mathf.Max(2, Mathf.CeilToInt(spline.distance / 10f));
            float edge = RoadRoute.RoadEdge(road) + corridor;

            for (int side = -1; side <= 1; side += 2)
            {
                Vector3[] line = new Vector3[count + 1];

                for (int i = 0; i <= count; i++)
                {
                    Vector3 position, tangent;
                    spline.GetSplineValueBoth(i / (float)count, out position, out tangent);

                    line[i] = position + RoadRoute.Across(tangent) * (side * edge) + Vector3.up * 0.2f;
                }

                Handles.DrawAAPolyLine(3f, line);
            }
        }
    }

    /// <summary>
    /// How wide a tree of that type is, from its prefab's own meshes - so the rings are the size of the trees
    /// that will stand there rather than a ring that means nothing at the scale of a level.
    /// </summary>
    private float PrototypeRadius(TerrainData data, int index)
    {
        if (data == null || index < 0 || index >= data.treePrototypes.Length) return 2f;

        GameObject prefab = data.treePrototypes[index].prefab;

        return prefab != null ? PrefabMeasure.Of(prefab).Radius : 2f;
    }

    // ------------------------------------------------------------------ plan

    private void RebuildPlan(Terrain terrain)
    {
        if (!planDirty) return;

        plan.Clear();
        skippedCorridor = 0;
        skippedSlope = 0;
        skippedBudget = 0;
        skippedStructures = 0;
        nearHectares = 0f;
        farHectares = 0f;
        planDirty = false;

        TerrainData data = terrain.terrainData;
        if (data == null || data.treePrototypes.Length == 0) return;

        List<int> types = LightPrototypes(data);
        if (types.Count == 0) return;

        Random.State previous = Random.state;
        Random.InitState(seed);

        Vector3 origin = terrain.transform.position;
        Vector3 size = data.size;

        // The corridor, the steep ground and the buildings are all asked as grids: a plan is thousands of
        // candidates and every one of them asks all three.
        RoadField roads = RoadField.ForScene(4f);
        PointGrid trees = new PointGrid(RoadRoute.TreePoints(), 12f);
        PointGrid structures = new PointGrid(RoadRoute.StructurePoints(RoadRoute.PainterRoots()), 12f);

        float nearFrom = Mathf.Max(5f, corridor);
        float nearTo = Mathf.Max(nearFrom + 1f, nearOutTo);
        float farTo = Mathf.Max(nearTo + 1f, farOutTo);

        CountUsableLand(terrain, roads, nearFrom, nearTo, farTo, ref nearHectares, ref farHectares);

        placedPerPrototype = new int[data.treePrototypes.Length];

        // A grid of candidates, sized so that one candidate is worth about one tree at the lower of the two
        // densities. The bands are then decided per candidate by how far out it is.
        float density = Mathf.Max(1f, Mathf.Min(nearDensity, farDensity));
        float cell = Mathf.Sqrt(10000f / density);

        int across = Mathf.CeilToInt(size.x / cell);
        int along = Mathf.CeilToInt(size.z / cell);

        for (int x = 0; x < across; x++)
        {
            for (int z = 0; z < along; z++)
            {
                if (plan.Count >= budget)
                {
                    skippedBudget++;
                    continue;
                }

                float wx = origin.x + (x + 0.5f) * cell + Random.Range(-cell * 0.5f, cell * 0.5f);
                float wz = origin.z + (z + 0.5f) * cell + Random.Range(-cell * 0.5f, cell * 0.5f);

                float nx = (wx - origin.x) / size.x;
                float nz = (wz - origin.z) / size.z;

                if (nx < 0f || nx > 1f || nz < 0f || nz > 1f) continue;

                Vector3 flat = new Vector3(wx, 0f, wz);

                float edge = roads.Clearance(flat);

                if (edge < nearFrom)
                {
                    skippedCorridor++;
                    continue;
                }

                Vector3 normal = data.GetInterpolatedNormal(nx, nz);

                if (Vector3.Angle(normal, Vector3.up) > maxSlope)
                {
                    skippedSlope++;
                    continue;
                }

                if (structures.Near(new Vector2(wx, wz), structureClearance) > 0)
                {
                    skippedStructures++;
                    continue;
                }

                int band = edge <= nearTo ? 1 : 2;
                if (edge > farTo) continue;

                float bandDensity = band == 1 ? nearDensity : farDensity;

                // A candidate is one tree's worth at the band's own density, so where the band is denser than
                // the grid the dice are rolled more than once.
                // How wooded this spot is: the noise, and how close the level's own woods are. This is the
                // shape of the woodland, and the density below is only how thick it is.
                float woods = Clump(wx, wz);
                float existing = Mathf.Clamp01(trees.Near(new Vector2(wx, wz), 40f) / 8f);
                float shape = Mathf.Clamp01(0.12f + clumpStrength * woods + woodBias * existing);

                // What this candidate is worth in trees: its own area in hectares at the band's density,
                // thinned by how wooded the spot is. The fraction is rounded by dice rather than thrown away,
                // which is what keeps a sparse band sparse instead of empty.
                float expected = bandDensity * (cell * cell / 10000f) * shape;

                int here = Mathf.FloorToInt(expected);
                if (Random.value < expected - here) here++;

                if (here <= 0) continue;

                float scale = band == 1 ? nearScale : farScale;

                for (int i = 0; i < here; i++)
                {
                    if (plan.Count >= budget)
                    {
                        skippedBudget++;
                        break;
                    }

                    // Each tree gets its own place inside the candidate, or a thick band comes out as trees
                    // standing on top of one another.
                    float tx = wx + Random.Range(-cell * 0.45f, cell * 0.45f);
                    float tz = wz + Random.Range(-cell * 0.45f, cell * 0.45f);

                    float tnx = (tx - origin.x) / size.x;
                    float tnz = (tz - origin.z) / size.z;

                    if (tnx < 0f || tnx > 1f || tnz < 0f || tnz > 1f) continue;

                    float y = data.GetInterpolatedHeight(tnx, tnz);

                    PlannedTree tree = new PlannedTree();
                    tree.normalized = new Vector3(tnx, y / size.y, tnz);
                    tree.world = new Vector3(tx, y, tz);
                    tree.prototype = types[Random.Range(0, types.Count)];
                    tree.scale = scale * Random.Range(0.75f, 1.25f);
                    tree.band = band;

                    plan.Add(tree);
                    placedPerPrototype[tree.prototype]++;
                }
            }
        }

        Random.state = previous;
    }

    /// <summary>
    /// How much ground each band has to work with, measured rather than guessed: the plan is built by area, so
    /// the count of trees the window shows is density times the hectares the land actually offers.
    /// </summary>
    private void CountUsableLand(Terrain terrain, RoadField roads, float nearFrom, float nearTo, float farTo,
                                 ref float nearHectares, ref float farHectares)
    {
        TerrainData data = terrain.terrainData;
        Vector3 origin = terrain.transform.position;
        Vector3 size = data.size;

        const float step = 12f;

        int across = Mathf.CeilToInt(size.x / step);
        int along = Mathf.CeilToInt(size.z / step);
        float area = step * step / 10000f;

        for (int x = 0; x < across; x++)
        {
            for (int z = 0; z < along; z++)
            {
                float wx = origin.x + (x + 0.5f) * step;
                float wz = origin.z + (z + 0.5f) * step;

                float nx = (wx - origin.x) / size.x;
                float nz = (wz - origin.z) / size.z;

                if (data.GetInterpolatedNormal(nx, nz).y < Mathf.Cos(maxSlope * Mathf.Deg2Rad)) continue;

                float edge = roads.Clearance(new Vector3(wx, 0f, wz));

                if (edge < nearFrom) continue;

                if (edge <= nearTo) nearHectares += area;
                else if (edge <= farTo) farHectares += area;
            }
        }
    }

    /// <summary>
    /// How wooded a spot is, from seeded value noise: two octaves, so a wood has one outline and its edges are
    /// ragged rather than round. The noise is this painter's own, so a plan with a seed in it repeats exactly.
    /// </summary>
    private float Clump(float x, float z)
    {
        float a = ValueNoise(x * clumpScale, z * clumpScale);
        float b = ValueNoise(x * clumpScale * 2.7f + 13.7f, z * clumpScale * 2.7f - 8.1f);

        return Mathf.Clamp01(a * 0.7f + b * 0.3f);
    }

    private static float ValueNoise(float x, float z)
    {
        int x0 = Mathf.FloorToInt(x);
        int z0 = Mathf.FloorToInt(z);

        float fx = x - x0;
        float fz = z - z0;

        // Smoothstep the fraction, or the woods come out in blocks
        fx = fx * fx * (3f - 2f * fx);
        fz = fz * fz * (3f - 2f * fz);

        float a = Hash01(x0, z0);
        float b = Hash01(x0 + 1, z0);
        float c = Hash01(x0, z0 + 1);
        float d = Hash01(x0 + 1, z0 + 1);

        return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fz);
    }

    private static float Hash01(int x, int z)
    {
        int h = x * 374761393 + z * 668265263;

        h = (h ^ (h >> 13)) * 1274126177;
        h = h ^ (h >> 16);

        return (h & 0x7fffffff) / (float)0x7fffffff;
    }

    /// <summary>
    /// The tree types this paints with: the light copies, and nothing else. The level's own types are never
    /// used here whatever happens, because a collider-bearing tree at this distance is paid for every frame and
    /// never seen - see <see cref="EnsureLightCopies"/>.
    /// </summary>
    private List<int> LightPrototypes(TerrainData data)
    {
        List<int> types = new List<int>();

        for (int i = 0; i < data.treePrototypes.Length; i++)
            if (IsLightPrototype(data.treePrototypes[i])) types.Add(i);

        return types;
    }

    // ------------------------------------------------------------------ paint

    private void Paint(Terrain terrain)
    {
        RebuildPlan(terrain);

        if (plan.Count == 0) return;

        TerrainData data = terrain.terrainData;

        TreeInstance[] existing = data.treeInstances;
        TreeInstance[] merged = new TreeInstance[existing.Length + plan.Count];

        System.Array.Copy(existing, merged, existing.Length);

        int written = 0;

        try
        {
            for (int i = 0; i < plan.Count; i++)
            {
                if (i % 500 == 0 &&
                    EditorUtility.DisplayCancelableProgressBar("Painting Distant Scenery",
                        "Placing scenery... " + i + " of " + plan.Count, i / (float)plan.Count))
                {
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
                written++;
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        if (written < plan.Count) System.Array.Resize(ref merged, existing.Length + written);

        Undo.RegisterCompleteObjectUndo(data, "Paint Distant Scenery");

        data.treeInstances = merged;

        EditorUtility.SetDirty(data);
        RoadRoute.MarkSceneDirty();

        Debug.Log("Distant Scenery: placed " + written + " trees beyond " + corridor.ToString("F0") +
                  " m of the road (" + nearHectares.ToString("F1") + " ha near at " + nearDensity.ToString("F0") +
                  "/ha, " + farHectares.ToString("F1") + " ha far at " + farDensity.ToString("F0") + "/ha; " +
                  skippedCorridor + " candidates inside the corridor, " + skippedSlope + " too steep, " +
                  skippedStructures + " on buildings" +
                  (skippedBudget > 0 ? ", " + skippedBudget + " over the budget" : "") + ").");
    }
}
