using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Takes the tree types whose prefab no longer exists out of a terrain, and with them every tree that was
/// placed using one of those types.
///
/// A terrain holds its trees as two lists: the types (a prefab and a bend factor apiece) and the instances,
/// each naming the type it is a tree of by its place in that first list. When a prefab a type points at is
/// deleted, the type stays behind pointing at nothing: nothing can be painted with it, and every tree that
/// uses it draws nothing at all - it is an invisible entry in the terrain's tree list that still costs the
/// level load time and a slot in the terrain's batching for nothing. Only the asset itself can be rid of
/// them, which is what this does.
///
/// The types that keep their prefab are left exactly as they were, and the trees that used them are carried
/// across to the type's new place in the list. That remapping is the part worth being careful about: removing
/// a type shifts the index of every type after it, so an instance left alone would come back as whatever tree
/// moved into its number.
///
/// Run from Tools &gt; Road Tools &gt; Clean Missing Tree Types, or from the Tree Painter window, which cleans
/// the terrain it is aimed at.
/// </summary>
public static class TreeTypeCleanup
{
    /// <summary>What one terrain's cleanup did, for the report.</summary>
    public struct Report
    {
        public bool changed;
        public int missingTypes;
        public int droppedTrees;
        public int keptTypes;
        public int keptTrees;
    }

    /// <summary>
    /// A request left where the editor will find it, so a cleanup can be asked for from outside: the file
    /// names the terrains to clean, one asset path per line, and it is deleted as it is read so it can only
    /// ever run once. This exists because the terrain assets are written by Unity and not by anything else -
    /// with the editor open, an edit made to the file behind its back is simply overwritten.
    /// </summary>
    private const string RequestFile = "Library/RoadRouteCleanTreeTypes.request";

    // ------------------------------------------------------------------ the work

    /// <summary>
    /// Cleans one terrain. <paramref name="undo"/> is for the menu and window buttons, where an undo entry is
    /// worth having; the outside-a-click path leaves it off, since there is no click for it to belong to.
    /// </summary>
    public static Report Clean(TerrainData data, bool undo)
    {
        Report report = new Report();

        if (data == null) return report;

        TreePrototype[] prototypes = data.treePrototypes;

        if (prototypes == null || prototypes.Length == 0) return report;

        List<TreePrototype> kept = new List<TreePrototype>();
        int[] moved = new int[prototypes.Length];

        for (int i = 0; i < prototypes.Length; i++)
        {
            if (prototypes[i].prefab == null)
            {
                moved[i] = -1;
                continue;
            }

            moved[i] = kept.Count;
            kept.Add(prototypes[i]);
        }

        report.missingTypes = prototypes.Length - kept.Count;
        report.keptTypes = kept.Count;

        if (report.missingTypes == 0)
        {
            TreeInstance[] untouched = data.treeInstances;
            report.keptTrees = untouched != null ? untouched.Length : 0;
            return report;
        }

        TreeInstance[] instances = data.treeInstances;
        List<TreeInstance> survivors = new List<TreeInstance>(instances != null ? instances.Length : 0);

        if (instances != null)
        {
            for (int i = 0; i < instances.Length; i++)
            {
                int type = instances[i].prototypeIndex;

                if (type < 0 || type >= moved.Length || moved[type] < 0)
                {
                    report.droppedTrees++;
                    continue;
                }

                TreeInstance tree = instances[i];
                tree.prototypeIndex = moved[type];
                survivors.Add(tree);
            }
        }

        if (undo) Undo.RegisterCompleteObjectUndo(data, "Remove Missing Tree Types");

        data.treePrototypes = kept.ToArray();
        data.treeInstances = survivors.ToArray();
        data.RefreshPrototypes();

        EditorUtility.SetDirty(data);

        report.changed = true;
        report.keptTrees = survivors.Count;

        return report;
    }

    /// <summary>Cleans one terrain asset, found by its path.</summary>
    public static Report CleanAsset(string assetPath)
    {
        TerrainData data = AssetDatabase.LoadAssetAtPath<TerrainData>(assetPath);

        if (data == null)
        {
            Debug.LogWarning("Tree Type Cleanup: '" + assetPath + "' is not a terrain asset - nothing was " +
                             "changed there.");
            return new Report();
        }

        Report report = Clean(data, false);

        Log(assetPath, report);

        return report;
    }

    // ------------------------------------------------------------------ the menu

    [MenuItem("Tools/Road Tools/Clean Missing Tree Types/In The Open Scenes", false, 300)]
    private static void CleanOpenScenes()
    {
        List<TerrainData> found = new List<TerrainData>();
        List<string> names = new List<string>();

        SceneTerrains(found, names);

        if (found.Count == 0)
        {
            Debug.Log("Tree Type Cleanup: no terrains are loaded in the open scenes.");
            return;
        }

        int types = 0;
        int trees = 0;
        int cleaned = 0;

        for (int i = 0; i < found.Count; i++)
        {
            Report report = Clean(found[i], true);

            if (report.changed) cleaned++;

            types += report.missingTypes;
            trees += report.droppedTrees;

            Log(names[i], report);
        }

        Debug.Log("Tree Type Cleanup: " + cleaned + " of " + found.Count + " terrain(s) had anything to " +
                  "clean; " + types + " tree type(s) with no prefab and " + trees + " invisible tree(s) were " +
                  "removed in total.");

        AssetDatabase.SaveAssets();
        RoadRoute.MarkSceneDirty();
    }

    [MenuItem("Tools/Road Tools/Clean Missing Tree Types/Everywhere In The Project", false, 301)]
    private static void CleanEverywhere()
    {
        string[] candidates = AssetDatabase.FindAssets("t:TerrainData");

        int types = 0;
        int trees = 0;
        int cleaned = 0;

        try
        {
            for (int i = 0; i < candidates.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(candidates[i]);

                if (i % 4 == 0)
                    EditorUtility.DisplayProgressBar("Clean Missing Tree Types", path, i / (float)candidates.Length);

                Report report = CleanAsset(path);

                if (report.changed) cleaned++;

                types += report.missingTypes;
                trees += report.droppedTrees;
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        AssetDatabase.SaveAssets();
        RoadRoute.MarkSceneDirty();

        Debug.Log("Tree Type Cleanup: looked at " + candidates.Length + " terrain asset(s); " + cleaned +
                  " had missing tree types. " + types + " type(s) with no prefab and " + trees +
                  " invisible tree(s) were removed in total.");
    }

    /// <summary>
    /// The whole project, for <c>-executeMethod</c>: this is what to run with the editor closed, since a
    /// terrain asset can only be written by Unity itself.
    /// </summary>
    public static void RunFromCommandLine()
    {
        Debug.Log("Tree Type Cleanup: running headless over the whole project.");
        CleanEverywhere();
    }

    // ------------------------------------------------------------------ the request

    /// <summary>
    /// Picks up a request left in the Library folder: a file naming the terrain assets to clean. Running this
    /// on load is what lets a cleanup be asked for from outside the editor, and the request is deleted before
    /// anything is done with it, so it can never run twice however it goes.
    /// </summary>
    [InitializeOnLoadMethod]
    private static void WatchForRequest()
    {
        if (!File.Exists(RequestFile)) return;

        EditorApplication.delayCall += RunRequest;
    }

    private static void RunRequest()
    {
        string[] paths;

        try
        {
            paths = File.ReadAllLines(RequestFile);
        }
        catch (IOException error)
        {
            Debug.LogError("Tree Type Cleanup: the request file could not be read (" + error.Message + ").");
            return;
        }

        // Gone before the work starts: whatever happens next, this request is spent.
        try
        {
            File.Delete(RequestFile);
        }
        catch (IOException error)
        {
            Debug.LogWarning("Tree Type Cleanup: " + RequestFile + " could not be deleted (" + error.Message +
                             "), so delete it by hand - it will be run again otherwise.");
        }

        int types = 0;
        int trees = 0;
        int cleaned = 0;

        for (int i = 0; i < paths.Length; i++)
        {
            string path = paths[i].Trim();

            if (path.Length == 0 || path.StartsWith("#")) continue;
            if (!path.StartsWith("Assets/")) path = "Assets/" + path;

            Report report = CleanAsset(path);

            if (report.changed) cleaned++;

            types += report.missingTypes;
            trees += report.droppedTrees;
        }

        AssetDatabase.SaveAssets();

        Debug.Log("Tree Type Cleanup: the request asked for " + paths.Length + " terrain(s); " + cleaned +
                  " had missing tree types. " + types + " tree type(s) with no prefab, along with " + trees +
                  " tree(s) placed with them, have been removed and saved.");
    }

    // ------------------------------------------------------------------ shared

    /// <summary>Every terrain loaded in the open scenes, with a name for each for the report.</summary>
    private static void SceneTerrains(List<TerrainData> data, List<string> names)
    {
        Terrain[] terrains = Object.FindObjectsOfType<Terrain>();

        for (int i = 0; i < terrains.Length; i++)
        {
            if (terrains[i] == null || terrains[i].terrainData == null) continue;
            if (data.Contains(terrains[i].terrainData)) continue;

            data.Add(terrains[i].terrainData);
            names.Add(terrains[i].terrainData.name + " (" + terrains[i].name + ")");
        }
    }

    private static void Log(string name, Report report)
    {
        if (!report.changed)
        {
            Debug.Log("Tree Type Cleanup: '" + name + "' has nothing missing - " + report.keptTypes +
                      " tree type(s), all still with their prefabs.");
            return;
        }

        Debug.Log("Tree Type Cleanup: '" + name + "' - removed " + report.missingTypes +
                  " tree type(s) whose prefab is gone, along with " + report.droppedTrees +
                  " tree(s) that were placed with them. " + report.keptTypes + " type(s) and " +
                  report.keptTrees + " tree(s) are left.");
    }
}
