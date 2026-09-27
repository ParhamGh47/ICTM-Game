using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// What a prefab costs to have on the map a thousand times over, worked out by looking at it rather than at a
/// budget sheet. Used by the scenery painter to warn before it paints, and by the builder below to say what it
/// took off.
/// </summary>
public class PrefabCost
{
    public int colliders;
    public int renderers;
    public int lodGroups;
    public int wastedLodGroups;     // LOD groups whose levels all draw the same things, so they do nothing
    public int maxLodLevels;
    public int materials;           // distinct shared materials - what the renderer has to switch between
    public int triangles;

    /// <summary>One line for a tooltip or a log, in the order the numbers matter.</summary>
    public string Summary()
    {
        string text = renderers + (renderers == 1 ? " renderer" : " renderers") +
                      ", " + materials + (materials == 1 ? " material" : " materials") +
                      ", " + Triangles();

        if (colliders > 0) text += ", " + colliders + (colliders == 1 ? " COLLIDER" : " COLLIDERS");
        if (wastedLodGroups > 0) text += ", " + wastedLodGroups + " useless LOD group" + (wastedLodGroups == 1 ? "" : "s");
        else if (maxLodLevels > 0) text += ", " + maxLodLevels + "-level LOD";

        return text;
    }

    /// <summary>Whether the prefab would add physics to every instance of itself. Always worth saying out loud.</summary>
    public bool CarriesPhysics { get { return colliders > 0; } }

    public string Triangles()
    {
        if (triangles < 1000) return triangles + " tris";
        if (triangles < 100000) return (triangles / 1000f).ToString("0.#") + "k tris";

        return (triangles / 1000000f).ToString("0.#") + "M tris";
    }
}

/// <summary>
/// Reads a prefab and says what it costs. Nothing is changed: this only counts what is there, so it is safe to
/// call from a window that redraws every frame.
/// </summary>
public static class SceneryPrefabAudit
{
    public static PrefabCost Of(GameObject prefab)
    {
        PrefabCost cost = new PrefabCost();
        if (prefab == null) return cost;

        HashSet<Material> materials = new HashSet<Material>();
        HashSet<int> seen = new HashSet<int>();

        Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>(true);

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null || !seen.Add(renderers[i].GetInstanceID())) continue;

            cost.renderers++;

            Material[] shared = renderers[i].sharedMaterials;

            for (int m = 0; m < shared.Length; m++)
                if (shared[m] != null) materials.Add(shared[m]);

            MeshFilter filter = renderers[i].GetComponent<MeshFilter>();

            if (filter != null && filter.sharedMesh != null)
                cost.triangles += filter.sharedMesh.triangles.Length / 3;
        }

        cost.materials = materials.Count;
        cost.colliders = prefab.GetComponentsInChildren<Collider>(true).Length;

        LODGroup[] groups = prefab.GetComponentsInChildren<LODGroup>(true);
        cost.lodGroups = groups.Length;

        for (int g = 0; g < groups.Length; g++)
        {
            if (groups[g] == null) continue;

            LOD[] levels = groups[g].GetLODs();
            cost.maxLodLevels = Mathf.Max(cost.maxLodLevels, levels.Length);

            if (LooksTheSame(levels)) cost.wastedLodGroups++;
        }

        return cost;
    }

    /// <summary>
    /// Whether an LOD group's levels all draw the same renderers - which is to say the group never changes what
    /// is drawn, at any distance, and so is worth nothing. The project's tree prefabs are all like this.
    /// </summary>
    private static bool LooksTheSame(LOD[] levels)
    {
        if (levels.Length < 2) return false;

        for (int i = 1; i < levels.Length; i++)
        {
            if (levels[i].renderers == null || levels[0].renderers == null) return false;
            if (levels[i].renderers.Length != levels[0].renderers.Length) return false;

            for (int r = 0; r < levels[i].renderers.Length; r++)
            {
                bool found = false;

                for (int o = 0; o < levels[0].renderers.Length; o++)
                    if (levels[i].renderers[r] == levels[0].renderers[o]) { found = true; break; }

                if (!found) return false;
            }
        }

        return true;
    }
}

/// <summary>
/// Editor tool that turns the level's own trees, bushes and rocks into <b>scenery prototypes</b>: the same
/// look with the two things that would otherwise be paid for on every instance taken off.
///
/// The first is physics. The project's tree prefabs carry box colliders by their roots - two or three each,
/// for a trunk and its branches - and the level terrain has tree colliders enabled, so every painted tree is
/// a body the physics engine has to keep. That is affordable for the few hundred the levels paint along the
/// road and ruinous for the tens of thousands a filled map wants, and nothing off the road needs to be solid
/// at all: the levels already wall the player onto the corridor with invisible walls.
///
/// The second is the LOD group. Every one of the project's tree prefabs has a group whose levels draw the same
/// renderers, so it cannot save anything at any distance - it is a component that asks a question and always
/// gets the same answer. Removing it leaves the terrain a plain tree, which is the case its own Tree Billboard
/// Distance setting is written for ("for SpeedTree trees this parameter is controlled by the LOD group
/// settings"): past that distance, which the levels set to 50 m, the tree is one quad.
///
/// The clones are saved under Assets/Prefabs/Scenery/, one folder per level, and are ordinary prefabs - the
/// painter paints them, and editing the originals means running this again.
///
/// Running it twice is safe: this tool's own output is never taken as a source, so a level built over does not
/// gather copies of copies. It is also what the painter builds for itself when it has nothing to paint with.
///
/// Usage: open a level scene, then Tools > Road Tools > Build Scenery Prototypes.
/// </summary>
public class SceneryPrototypeBuilder : EditorWindow
{
    private const string RootFolder = "Assets/Prefabs/Scenery";
    private const string CloneSuffix = " (Scenery)";

    private Terrain targetTerrain;
    private string outputFolder = RootFolder + "/Level-1";

    // The prefabs to clone. Seeded from the terrain's own tree prototypes - the trees the level already
    // paints - and from the level's vegetation folders, then editable.
    private readonly List<GameObject> sources = new List<GameObject>();

    private bool stripColliders = true;
    private bool removeWastedLodGroups = true;
    private bool castShadows = false;

    private Vector2 scroll;

    [MenuItem("Tools/Road Tools/Build Scenery Prototypes", false, 30)]
    private static void OpenWindow()
    {
        SceneryPrototypeBuilder window = GetWindow<SceneryPrototypeBuilder>("Scenery Prototypes");
        window.minSize = new Vector2(420f, 520f);
        window.Show();
    }

    private void OnEnable()
    {
        if (targetTerrain == null) targetTerrain = Terrain.activeTerrain;

        if (sources.Count == 0) Seed();
    }

    // ---------------------------------------------------------------- what to clone

    /// <summary>
    /// The starting list: everything the terrain already paints (its tree prototypes are the level's own
    /// vegetation, whichever folder they live in), plus whatever the level's own vegetation folders hold, so a
    /// level whose trees have not been painted yet can still be filled.
    /// </summary>
    private void Seed()
    {
        sources.Clear();

        if (targetTerrain != null && targetTerrain.terrainData != null)
        {
            TreePrototype[] prototypes = targetTerrain.terrainData.treePrototypes;

            for (int i = 0; i < prototypes.Length; i++)
            {
                if (prototypes[i] == null || prototypes[i].prefab == null) continue;
                if (sources.Contains(prototypes[i].prefab)) continue;

                // Once the painter has run, the terrain names the scenery prototypes among its tree types - so
                // without this the tool would offer to clone its own clones, and a level built twice would end
                // up with "Tree-1 (Scenery) (Scenery)" in it.
                if (IsSceneryClone(prototypes[i].prefab)) continue;

                sources.Add(prototypes[i].prefab);
            }
        }

        if (sources.Count > 0) return;

        string level = LevelFolder();
        AddFolder(sources, RootFolder.Replace("Scenery", "Environments/Trees") + "/" + level);
        AddFolder(sources, "Assets/Prefabs/DetailsTerrain/" + level);
        AddFolder(sources, "Assets/Prefabs/DetailsTerrain/Utils");
    }

    /// <summary>Every prefab in a folder, without touching anything else in the project.</summary>
    private static void AddFolder(List<GameObject> into, string folder)
    {
        if (!AssetDatabase.IsValidFolder(folder)) return;

        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { folder });

        for (int i = 0; i < guids.Length; i++)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guids[i]));

            if (prefab == null || into.Contains(prefab)) continue;
            if (IsSceneryClone(prefab)) continue;

            into.Add(prefab);
        }
    }

    /// <summary>Whether a prefab is one of this tool's own output, which is never a source for more output.</summary>
    private static bool IsSceneryClone(GameObject prefab)
    {
        string path = AssetDatabase.GetAssetPath(prefab);

        return !string.IsNullOrEmpty(path) && path.StartsWith(RootFolder);
    }

    /// <summary>
    /// How many generations of cloning a name shows: an original is 0, a clone 1, a clone of a clone 2. Only the
    /// first generation is worth having.
    /// </summary>
    public static int GenerationsIn(string name)
    {
        int count = 0;
        int at = 0;

        while ((at = name.IndexOf(CloneSuffix, at, System.StringComparison.Ordinal)) >= 0)
        {
            count++;
            at += CloneSuffix.Length;
        }

        return count;
    }

    /// <summary>The level this scene belongs to, as the asset folders name it: Core-3 is Level-3.</summary>
    private static string LevelFolder()
    {
        string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
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
            GUILayout.Label("Build Scenery Prototypes", EditorStyles.boldLabel);

            EditorGUILayout.HelpBox(
                "Clones the level's own vegetation with its colliders off and its dead LOD groups removed, so " +
                "the scenery painter can fill the map with it cheaply. Run it again after the originals change.",
                MessageType.Info);

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("References", EditorStyles.boldLabel);
            targetTerrain = (Terrain)EditorGUILayout.ObjectField("Terrain", targetTerrain, typeof(Terrain), true);
            outputFolder = EditorGUILayout.TextField("Output Folder", outputFolder);

            if (GUILayout.Button("Use This Level's Folder")) outputFolder = RootFolder + "/" + LevelFolder();

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("What To Take Off", EditorStyles.boldLabel);
            stripColliders = EditorGUILayout.ToggleLeft("Remove colliders (off-road scenery needs none)", stripColliders);
            removeWastedLodGroups = EditorGUILayout.ToggleLeft("Remove LOD groups that never change anything", removeWastedLodGroups);
            castShadows = EditorGUILayout.ToggleLeft("Let the clones cast shadows", castShadows);

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Sources (" + sources.Count + ")", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Re-read From Terrain")) { Seed(); }
            if (GUILayout.Button("Clear")) sources.Clear();
            EditorGUILayout.EndHorizontal();

            int remove = -1;

            for (int i = 0; i < sources.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                sources[i] = (GameObject)EditorGUILayout.ObjectField(sources[i], typeof(GameObject), false);

                PrefabCost cost = SceneryPrefabAudit.Of(sources[i]);

                if (cost.CarriesPhysics)
                {
                    GUI.color = new Color(1f, 0.7f, 0.3f);
                    GUILayout.Label(cost.Summary(), EditorStyles.miniLabel, GUILayout.Width(260f));
                    GUI.color = Color.white;
                }
                else
                {
                    GUILayout.Label(cost.Summary(), EditorStyles.miniLabel, GUILayout.Width(260f));
                }

                if (GUILayout.Button("x", GUILayout.Width(20f))) remove = i;
                EditorGUILayout.EndHorizontal();
            }

            if (remove >= 0) sources.RemoveAt(remove);

            if (sources.Count == 0)
                EditorGUILayout.HelpBox("No sources yet. Paint some trees onto the terrain, or drag prefabs in.", MessageType.Warning);

            EditorGUILayout.Space();

            if (GUILayout.Button("Remove Copies Of Copies", GUILayout.Height(22f))) RemoveCopiesOfCopies();

            GUI.backgroundColor = Color.green;
            if (GUILayout.Button("BUILD PROTOTYPES", GUILayout.Height(38f))) Build();
            GUI.backgroundColor = Color.white;
        }
        finally
        {
            EditorGUILayout.EndScrollView();
        }
    }

    // ---------------------------------------------------------------- building

    /// <summary>
    /// Builds the prototypes for the scene that is open, with the defaults, without anything being set up by
    /// hand - for the scenery painter's one-click pass, which needs the prototypes before it can paint.
    /// </summary>
    public static bool BuildForActiveScene()
    {
        SceneryPrototypeBuilder window = CreateInstance<SceneryPrototypeBuilder>();

        window.targetTerrain = Terrain.activeTerrain;
        window.outputFolder = RootFolder + "/" + LevelFolder();
        window.Seed();

        // A level whose terrain has no trees painted on it yet has nothing to clone from it, so the fallback
        // is the level's own vegetation folders - the same list the window would have offered.
        if (window.sources.Count == 0)
        {
            AddFolder(window.sources, "Assets/Prefabs/Environments/Trees/" + LevelFolder());
            AddFolder(window.sources, "Assets/Prefabs/DetailsTerrain/" + LevelFolder());
        }

        bool built = window.Build();

        DestroyImmediate(window);

        return built;
    }

    /// <summary>
    /// Writes one clone per source. Every clone is unpacked from its originals first, so a component can be
    /// taken off it at all: a component that belongs to a nested prefab cannot be removed from the instance,
    /// only from the prefab it came from - which is the file this tool exists to leave alone.
    /// </summary>
    private bool Build()
    {
        if (sources.Count == 0)
        {
            EditorUtility.DisplayDialog("Build Scenery Prototypes", "There is nothing to clone yet.", "OK");

            return false;
        }

        EnsureFolder(outputFolder);

        List<string> written = new List<string>();
        string report = "";

        for (int i = 0; i < sources.Count; i++)
        {
            GameObject source = sources[i];

            if (source == null) continue;

            if (EditorUtility.DisplayCancelableProgressBar("Building Scenery Prototypes",
                    source.name, i / (float)sources.Count))
                break;

            PrefabCost before = SceneryPrefabAudit.Of(source);

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(source);

            if (instance == null) continue;

            // Detach from the original, so components can be taken off freely. OutermostRoot rather than
            // Completely: the meshes inside a level's tree prefab are still a nested prefab of their own, and
            // unpacking those as well would bury the clone in copies of the model.
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);

            int collidersRemoved = 0;
            int groupsRemoved = 0;

            if (stripColliders)
            {
                Collider[] colliders = instance.GetComponentsInChildren<Collider>(true);

                for (int c = 0; c < colliders.Length; c++)
                {
                    if (colliders[c] == null) continue;

                    DestroyImmediate(colliders[c]);
                    collidersRemoved++;
                }
            }

            if (removeWastedLodGroups)
            {
                LODGroup[] groups = instance.GetComponentsInChildren<LODGroup>(true);

                for (int g = 0; g < groups.Length; g++)
                {
                    if (groups[g] == null) continue;
                    if (!IsWasted(groups[g])) continue;

                    DestroyImmediate(groups[g]);
                    groupsRemoved++;
                }
            }

            if (!castShadows)
            {
                Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);

                for (int r = 0; r < renderers.Length; r++)
                    if (renderers[r] != null)
                        renderers[r].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            string path = AssetDatabase.GenerateUniqueAssetPath(outputFolder + "/" + source.name + CloneSuffix + ".prefab");

            GameObject clone = PrefabUtility.SaveAsPrefabAsset(instance, path);
            DestroyImmediate(instance);

            if (clone == null) continue;

            written.Add(path);

            PrefabCost after = SceneryPrefabAudit.Of(clone);

            report += "\n  " + source.name + ":  " + before.Summary() + "  ->  " + after.Summary() +
                      (collidersRemoved > 0 || groupsRemoved > 0
                          ? "   (" + collidersRemoved + " collider" + (collidersRemoved == 1 ? "" : "s") +
                            ", " + groupsRemoved + " LOD group" + (groupsRemoved == 1 ? "" : "s") + " removed)"
                          : "   (unchanged)");
        }

        EditorUtility.ClearProgressBar();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[Scenery] Built " + written.Count + " scenery prototype" + (written.Count == 1 ? "" : "s") +
                  " in " + outputFolder + ":" + report);

        return true;
    }

    /// <summary>Whether an LOD group's levels all draw the same renderers, which makes it a no-op.</summary>
    private static bool IsWasted(LODGroup group)
    {
        LOD[] levels = group.GetLODs();
        if (levels.Length < 2) return false;

        for (int i = 1; i < levels.Length; i++)
        {
            if (levels[i].renderers == null || levels[0].renderers == null) return false;
            if (levels[i].renderers.Length != levels[0].renderers.Length) return false;

            for (int r = 0; r < levels[i].renderers.Length; r++)
            {
                bool found = false;

                for (int o = 0; o < levels[0].renderers.Length; o++)
                    if (levels[i].renderers[r] == levels[0].renderers[o]) { found = true; break; }

                if (!found) return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Deletes the clones of clones an earlier run left behind - the ones that name themselves
    /// "... (Scenery) (Scenery)". Only this tool's own output under its own folder is ever removed, and only
    /// second-generation copies of it, so nothing anyone made by hand is touched.
    /// </summary>
    private void RemoveCopiesOfCopies()
    {
        List<string> doomed = new List<string>();

        if (AssetDatabase.IsValidFolder(RootFolder))
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { RootFolder });

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);

                if (GenerationsIn(System.IO.Path.GetFileNameWithoutExtension(path)) > 1) doomed.Add(path);
            }
        }

        if (doomed.Count == 0)
        {
            EditorUtility.DisplayDialog("Scenery Prototypes", "There are no copies of copies to remove.", "OK");

            return;
        }

        if (!EditorUtility.DisplayDialog("Scenery Prototypes",
                "Remove " + doomed.Count + " prefab" + (doomed.Count == 1 ? "" : "s") + " that are copies of " +
                "earlier copies?\n\nThey are this tool's own output, made by building over a level that had " +
                "already been built - the painter leaves them out, and this deletes them.",
                "Remove", "Cancel"))
            return;

        for (int i = 0; i < doomed.Count; i++) AssetDatabase.DeleteAsset(doomed[i]);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[Scenery] Removed " + doomed.Count + " copies of copies from " + RootFolder + ".");
    }

    /// <summary>Makes a folder and every folder above it, so the output path always exists.</summary>
    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;

        string[] parts = folder.Split('/');
        string path = parts[0];

        for (int i = 1; i < parts.Length; i++)
        {
            string next = path + "/" + parts[i];

            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(path, parts[i]);

            path = next;
        }
    }
}
