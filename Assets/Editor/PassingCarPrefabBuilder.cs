using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

/// <summary>
/// Builds a passing-car prefab out of a car model - the same parts the hand-made 206 prefab has, in the same
/// places - so a newly imported car can join the fleet the passing-car painter spawns.
///
/// Why a builder rather than a prefab written out by hand: a passing car is a Rigidbody, an
/// <see cref="AICarController"/>, a collider, four wheel transforms, two headlights and an exhaust, all
/// wrapped around an instance of the model, and everything about it that matters - where the wheels sit, how
/// big the collider is, how far the exhaust hangs off the back - is measured against the model. The model's
/// own parts live inside the FBX, and the identifiers that address them are Unity's to hand out: they are not
/// in the file to be read, they exist once Unity has imported it. So this asks Unity, and gets them right
/// rather than guessing.
///
/// The template is a working car prefab (the 206 unless changed). Its own objects are copied whole, so the
/// exhaust is the same ParticleSystem, the lights are the same spotlights and the collider is the same box;
/// they are then placed against the *new* model, each object moved to the same relative spot inside the
/// model's bounds and the layout scaled by how much bigger the new model is. That matters because the
/// artist's numbers were never a match for the 206 to begin with - the 911 wears the 206's collider
/// verbatim - so scaling the relationship is what carries the intent across, not the numbers.
///
/// The car model in Assets/Objects/Cars is built on first load (once ever, remembered in EditorPrefs), and
/// can be rebuilt from Tools > Road Tools > Create Car Prefab From Model. The result goes in
/// Assets/Prefabs/Cars, which is where <see cref="PassingCarsSpawner"/> looks, so it joins the fleet on its
/// own.
/// </summary>
public class PassingCarPrefabBuilder : EditorWindow
{
    private const string DefaultTemplatePath = "Assets/Prefabs/Cars/206/206.prefab";
    private const string DefaultModelPath = "Assets/Objects/Cars/car.fbx";
    private const string DefaultOutputPath = "Assets/Prefabs/Cars/Car/Car.prefab";
    private const string DefaultBodyMaterialPath = "Assets/Prefabs/Cars/CarColor.mat";

    /// <summary>Set once the car has been built, so deleting it means deleting it.</summary>
    private const string BuiltOnceKey = "PassingCarPrefabBuilder.BuiltOnce";

    private GameObject template;
    private GameObject model;
    private string outputPath = DefaultOutputPath;
    private Material bodyMaterial;

    [Tooltip("Grow the layout with the model: a car bigger than the template gets a bigger collider and its " +
             "wheels further apart, in proportion. Off copies the template's numbers exactly.")]
    private bool scaleToModel = true;

    [Tooltip("Overwrite an existing prefab at the output path instead of stopping.")]
    private bool overwrite = true;

    private Vector2 scroll;
    private string measurement = "";

    [MenuItem("Tools/Road Tools/Create Car Prefab From Model")]
    public static void OpenWindow()
    {
        var window = GetWindow<PassingCarPrefabBuilder>("Car Prefab Builder");
        window.minSize = new Vector2(420, 470);
        window.Show();
    }

    /// <summary>
    /// Builds the car for the model sitting in Assets/Objects/Cars the first time the project is loaded with
    /// this script in it, so there is a prefab to paint without anyone having to go and make one. It happens
    /// once ever: the flag is remembered, which means a car that is deleted on purpose stays deleted.
    /// </summary>
    [InitializeOnLoadMethod]
    private static void BuildOnceOnLoad()
    {
        if (EditorPrefs.GetBool(BuiltOnceKey, false)) return;

        EditorApplication.delayCall += () =>
        {
            if (EditorPrefs.GetBool(BuiltOnceKey, false)) return;

            if (AssetDatabase.LoadAssetAtPath<GameObject>(DefaultOutputPath) != null)
            {
                EditorPrefs.SetBool(BuiltOnceKey, true);
                return;
            }

            GameObject templateAsset = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultTemplatePath);
            GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultModelPath);

            // Nothing to build from yet - a project that has not imported the model keeps this for later.
            if (templateAsset == null || modelAsset == null) return;

            EditorPrefs.SetBool(BuiltOnceKey, true);

            string error;
            GameObject built = BuildPrefab(templateAsset, modelAsset, DefaultOutputPath,
                                           AssetDatabase.LoadAssetAtPath<Material>(DefaultBodyMaterialPath),
                                           true, true, out error);

            if (built != null) Debug.Log("[CarPrefab] Built '" + DefaultOutputPath +
                                         "' for the '" + modelAsset.name + "' model - the passing cars can " +
                                         "wear it from now on. Rebuild or change the layout from Tools > " +
                                         "Road Tools > Create Car Prefab From Model.");
            else Debug.LogWarning("[CarPrefab] Could not build '" + DefaultOutputPath + "': " + error);
        };
    }

    void OnEnable()
    {
        if (template == null) template = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultTemplatePath);
        if (model == null) model = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultModelPath);
        if (bodyMaterial == null) bodyMaterial = AssetDatabase.LoadAssetAtPath<Material>(DefaultBodyMaterialPath);

        Measure();
    }

    // ---------------------------------------------------------------- window

    void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        GUILayout.Label("Create Car Prefab From Model", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        EditorGUILayout.HelpBox(
            "Copies a working car prefab's parts - wheels, collider, exhaust, headlights - onto a new model " +
            "and saves the result under Assets/Prefabs/Cars, where the passing-car painter finds it.",
            MessageType.Info);

        EditorGUILayout.Space();

        template = (GameObject)EditorGUILayout.ObjectField(
            new GUIContent("Template Car", "The prefab to copy the parts and settings from."),
            template, typeof(GameObject), false);

        model = (GameObject)EditorGUILayout.ObjectField(
            new GUIContent("Car Model", "The model to build the prefab around - the FBX itself."),
            model, typeof(GameObject), false);

        EditorGUILayout.Space();

        EditorGUILayout.BeginHorizontal();
        outputPath = EditorGUILayout.TextField(
            new GUIContent("Save As", "Where the prefab goes. It has to end in .prefab."), outputPath);
        if (GUILayout.Button("...", GUILayout.Width(28)))
        {
            string picked = EditorUtility.SaveFilePanelInProject(
                "Save Car Prefab", Path.GetFileNameWithoutExtension(outputPath), "prefab",
                "Where should the prefab go?", Path.GetDirectoryName(outputPath));

            if (!string.IsNullOrEmpty(picked)) outputPath = picked;
        }
        EditorGUILayout.EndHorizontal();

        bodyMaterial = (Material)EditorGUILayout.ObjectField(
            new GUIContent("Body Material", "The material the painter randomises. Assigning it here is what " +
                                            "makes the car come out in a random colour, the way the 206 does."),
            bodyMaterial, typeof(Material), false);

        EditorGUILayout.Space();

        scaleToModel = EditorGUILayout.Toggle(
            new GUIContent("Scale Layout To Model",
                "Grow the collider, wheels, lights and exhaust in proportion to the new model's size."),
            scaleToModel);

        overwrite = EditorGUILayout.Toggle(
            new GUIContent("Overwrite Existing", "Replace a prefab that is already at the output path."),
            overwrite);

        EditorGUILayout.Space();

        if (GUILayout.Button("Measure Both Models")) Measure();
        if (!string.IsNullOrEmpty(measurement)) EditorGUILayout.LabelField(measurement, EditorStyles.miniLabel);

        EditorGUILayout.Space();

        GUI.backgroundColor = Color.green;
        if (GUILayout.Button("BUILD CAR PREFAB", GUILayout.Height(40))) BuildFromWindow();
        GUI.backgroundColor = Color.white;

        EditorGUILayout.EndScrollView();
    }

    void BuildFromWindow()
    {
        string error;
        GameObject built = BuildPrefab(template, model, outputPath, bodyMaterial, scaleToModel, overwrite, out error);

        if (built == null)
        {
            EditorUtility.DisplayDialog("Create Car Prefab", error, "OK");
            return;
        }

        EditorGUIUtility.PingObject(built);
        Selection.activeObject = built;
    }

    /// <summary>
    /// Reports the size of both models, which is what the layout is scaled by. Measured when asked rather
    /// than every repaint: it has to place an instance to see a template's model.
    /// </summary>
    void Measure()
    {
        if (template == null || model == null)
        {
            measurement = "Assign a template car and a car model to measure them.";
            return;
        }

        measurement = "Template's model " + MeasuredSize(template, true) + " m, this model " +
                      MeasuredSize(model, false) + " m - the layout is scaled by the difference.";
    }

    // ---------------------------------------------------------------- building

    /// <summary>
    /// Builds the prefab, returning it, or null with the reason in <paramref name="error"/>. Nothing is left
    /// in the scene: the two measuring instances are hidden from the start and destroyed at the end.
    /// </summary>
    public static GameObject BuildPrefab(GameObject template, GameObject model, string outputPath,
                                         Material bodyMaterial, bool scaleToModel, bool overwrite,
                                         out string error)
    {
        error = null;

        if (template == null || model == null)
        {
            error = "Assign both a template car and a car model.";
            return null;
        }

        outputPath = (outputPath ?? "").Replace('\\', '/');

        if (!outputPath.EndsWith(".prefab") || !outputPath.StartsWith("Assets/"))
        {
            error = "The output path has to be inside Assets and end in .prefab.";
            return null;
        }

        if (!overwrite && AssetDatabase.LoadAssetAtPath<GameObject>(outputPath) != null)
        {
            error = "Something is already at " + outputPath + ".";
            return null;
        }

        GameObject templateInstance = PrefabUtility.InstantiatePrefab(template) as GameObject;
        GameObject modelInstance = PrefabUtility.InstantiatePrefab(model) as GameObject;

        if (templateInstance == null || modelInstance == null)
        {
            if (templateInstance != null) DestroyImmediate(templateInstance);
            if (modelInstance != null) DestroyImmediate(modelInstance);

            error = "One of the two is not an asset Unity can place in a scene - the template has to be a " +
                    "prefab and the model an imported model.";
            return null;
        }

        // Never visited by the undo system or by the scene's save: these are measuring sticks, not scene
        // objects, and they are torn down in the finally below whatever happens.
        templateInstance.hideFlags = HideFlags.HideAndDontSave;
        modelInstance.hideFlags = HideFlags.HideAndDontSave;

        try
        {
            return BuildNow(templateInstance, modelInstance, outputPath, bodyMaterial, scaleToModel, ref error);
        }
        finally
        {
            // The model ends up inside the built prefab, whose root is destroyed on the way out, so either of
            // these may already be gone - Unity's "!= null" is false for a destroyed object.
            if (modelInstance != null) DestroyImmediate(modelInstance);
            if (templateInstance != null) DestroyImmediate(templateInstance);
        }
    }

    private static GameObject BuildNow(GameObject templateInstance, GameObject modelInstance, string outputPath,
                                       Material bodyMaterial, bool scaleToModel, ref string error)
    {
        GameObject templateModel = FindModelInstance(templateInstance);

        if (templateModel == null)
        {
            error = "The template prefab does not contain a model, so there is nothing to measure where its " +
                    "wheels and collider belong.";
            return null;
        }

        // Both are squared up on the origin first, so the two models can be measured against each other. The
        // finished prefab gets the template's own root transform back at the end, which is what the painter's
        // cars are expected to wear.
        Quaternion rootRotation = templateInstance.transform.rotation;
        Vector3 rootScale = templateInstance.transform.localScale;

        templateInstance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        modelInstance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

        Bounds from = BoundsOf(templateModel);
        Bounds to = BoundsOf(modelInstance);

        if (from.size.x < 0.0001f || from.size.y < 0.0001f || from.size.z < 0.0001f ||
            to.size.x < 0.0001f || to.size.y < 0.0001f || to.size.z < 0.0001f)
        {
            error = "One of the two models measures as nothing - it may have no renderers.";
            return null;
        }

        Vector3 ratio = scaleToModel
            ? new Vector3(to.size.x / from.size.x, to.size.y / from.size.y, to.size.z / from.size.z)
            : Vector3.one;

        GameObject root = new GameObject(Path.GetFileNameWithoutExtension(outputPath));

        // 1. The model itself, as an instance of the FBX - the way the 206 prefab holds its model. Squared up
        //    on the root, but left the size Unity imported it at: the FBX root carries the exporter's own
        //    scale, and that is what makes the car the size it is.
        modelInstance.transform.SetParent(root.transform, false);
        modelInstance.transform.localPosition = Vector3.zero;
        modelInstance.transform.localRotation = Quaternion.identity;
        modelInstance.hideFlags = HideFlags.None;

        // 2. The template's own objects - the wheels, the body collider, the exhaust, the lights - copied
        //    whole, so their components and materials come with them, then laid out against this model.
        Dictionary<Transform, Transform> pairs = new Dictionary<Transform, Transform>();
        int ported = 0;

        foreach (Transform child in templateInstance.transform)
        {
            if (child.gameObject == templateModel) continue;

            GameObject copy = Instantiate(child.gameObject);
            copy.name = child.name;
            copy.transform.SetParent(root.transform, false);

            pairs[child] = copy.transform;
            PortSubtree(child, copy.transform, from, to, ratio, pairs);
            ported++;
        }

        // 3. The root's own components: the rigid body and the AI controller, copied with their settings and
        //    then pointed at the copies rather than at the template they came from.
        Component[] components = templateInstance.GetComponents<Component>();

        foreach (Component component in components)
        {
            if (component is Transform) continue;

            ComponentUtility.CopyComponent(component);
            ComponentUtility.PasteComponentAsNew(root);
        }

        AICarController oldController = templateInstance.GetComponent<AICarController>();
        AICarController newController = root.GetComponent<AICarController>();

        if (oldController != null && newController != null)
        {
            newController.waypointsRoot = null;         // the painter assigns the path it builds
            newController.startingWaypoint = 0;

            if (oldController.wheels != null)
            {
                List<Transform> wheels = new List<Transform>();

                foreach (Transform wheel in oldController.wheels)
                {
                    Transform mapped;
                    if (wheel != null && pairs.TryGetValue(wheel, out mapped) && mapped != null) wheels.Add(mapped);
                }

                newController.wheels = wheels.ToArray();
            }
        }

        int bodySlots = PaintBody(modelInstance, bodyMaterial);

        root.transform.rotation = rootRotation;
        root.transform.localScale = rootScale;

        EnsureFolder(Path.GetDirectoryName(outputPath));

        bool saved;
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, outputPath, out saved);
        AssetDatabase.SaveAssets();

        // The instance root is not the asset, and only the asset is wanted: the instance goes with the rest of
        // the measuring sticks.
        DestroyImmediate(root);

        if (!saved || prefab == null)
        {
            error = "Unity refused to save the prefab at " + outputPath + ".";
            return null;
        }

        EditorGUIUtility.PingObject(prefab);

        Debug.Log(string.Format(
            "[CarPrefab] {0}: built from '{1}' on the '{2}' layout - {3} objects ported, model {4} against the " +
            "template's {5}, {6} body slot(s) wearing {7}. The passing-car painter finds prefabs in " +
            "Assets/Prefabs/Cars by itself.",
            outputPath, modelInstance.name, templateInstance.name, ported, Size(to), Size(from), bodySlots,
            bodyMaterial != null ? bodyMaterial.name : "nothing"));

        return prefab;
    }

    /// <summary>
    /// Lays a copied object out against the new model: its position moves to the same relative spot inside the
    /// model's bounds, and anything carrying a size of its own - a collider, a renderer, the exhaust - is
    /// scaled by how much the new model differs from the template's.
    ///
    /// Every object in the subtree is moved, the wheel transforms included, so the wheels land in the same
    /// places on the new model. Sizes are scaled only on objects that have one: scaling a parent as well
    /// would move its children twice.
    /// </summary>
    private static void PortSubtree(Transform source, Transform copy, Bounds from, Bounds to, Vector3 ratio,
                                    Dictionary<Transform, Transform> pairs)
    {
        copy.position = MapPoint(source.position, from, to);

        if (HasSizeOfItsOwn(copy)) copy.localScale = Vector3.Scale(source.localScale, ratio);

        for (int i = 0; i < source.childCount && i < copy.childCount; i++)
        {
            pairs[source.GetChild(i)] = copy.GetChild(i);
            PortSubtree(source.GetChild(i), copy.GetChild(i), from, to, ratio, pairs);
        }
    }

    private static bool HasSizeOfItsOwn(Transform transform)
    {
        return transform.GetComponent<Collider>() != null
            || transform.GetComponent<Renderer>() != null
            || transform.GetComponent<ParticleSystem>() != null
            || transform.GetComponent<Light>() != null;
    }

    /// <summary>The same place inside one box as a point occupies in another, as a fraction of each axis.</summary>
    private static Vector3 MapPoint(Vector3 point, Bounds from, Bounds to)
    {
        Vector3 fraction;
        fraction.x = (point.x - from.min.x) / from.size.x;
        fraction.y = (point.y - from.min.y) / from.size.y;
        fraction.z = (point.z - from.min.z) / from.size.z;

        return new Vector3(
            to.min.x + fraction.x * to.size.x,
            to.min.y + fraction.y * to.size.y,
            to.min.z + fraction.z * to.size.z);
    }

    /// <summary>
    /// Puts the paintable material on the model's body slots and returns how many were found. The body is
    /// whichever slot the model calls its body - the 206's painter looks for CarColor, the police model calls
    /// its own body "BODY" - and the fallback is the biggest mesh's first slot, which is what a car body is.
    /// </summary>
    private static int PaintBody(GameObject modelInstance, Material material)
    {
        if (material == null) return 0;

        List<Renderer> renderers = new List<Renderer>(modelInstance.GetComponentsInChildren<Renderer>(true));
        int painted = 0;

        for (int r = 0; r < renderers.Count; r++)
        {
            if (renderers[r] is ParticleSystemRenderer || renderers[r] is TrailRenderer) continue;

            Material[] materials = renderers[r].sharedMaterials;
            bool changed = false;

            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] == null) continue;
                if (!IsBody(materials[i])) continue;

                materials[i] = material;
                changed = true;
                painted++;
            }

            if (changed) renderers[r].sharedMaterials = materials;
        }

        if (painted > 0) return painted;

        Renderer biggest = null;
        float biggestSize = 0f;

        for (int r = 0; r < renderers.Count; r++)
        {
            float size = renderers[r].bounds.size.sqrMagnitude;
            if (size > biggestSize) { biggestSize = size; biggest = renderers[r]; }
        }

        if (biggest == null) return 0;

        Material[] slots = biggest.sharedMaterials;
        if (slots.Length == 0) return 0;

        slots[0] = material;
        biggest.sharedMaterials = slots;

        return 1;
    }

    /// <summary>Whether a material is the car's body paint rather than its glass, lights or engine.</summary>
    private static bool IsBody(Material material)
    {
        string name = material.name;

        return name.IndexOf("body", System.StringComparison.OrdinalIgnoreCase) >= 0
            || name.IndexOf("carc", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    // ---------------------------------------------------------------- helpers

    /// <summary>
    /// The instantiated model inside a car prefab, found by what it came from: the one child whose source
    /// asset is an imported model.
    /// </summary>
    private static GameObject FindModelInstance(GameObject root)
    {
        foreach (Transform child in root.transform)
        {
            Object source = PrefabUtility.GetCorrespondingObjectFromSource(child.gameObject);
            if (source == null) continue;

            string path = AssetDatabase.GetAssetPath(source);
            if (!string.IsNullOrEmpty(path) && path.EndsWith(".fbx")) return child.gameObject;
        }

        return null;
    }

    private static Bounds BoundsOf(GameObject go)
    {
        Renderer[] renderers = go.GetComponentsInChildren<Renderer>(true);
        Bounds bounds = new Bounds();
        bool any = false;

        for (int i = 0; i < renderers.Length; i++)
        {
            // The exhaust and any trails are effects hanging off the car, not the car itself.
            if (renderers[i] is ParticleSystemRenderer || renderers[i] is TrailRenderer) continue;

            if (!any) { bounds = renderers[i].bounds; any = true; }
            else bounds.Encapsulate(renderers[i].bounds);
        }

        return bounds;
    }

    private static string Size(Bounds bounds)
    {
        return string.Format("{0:F2} x {1:F2} x {2:F2}", bounds.size.x, bounds.size.y, bounds.size.z);
    }

    /// <summary>
    /// The size of a prefab - or, for a car prefab, of the model inside it - measured on a throwaway
    /// instance, which is the only way to see a template's model and all of that model's own children.
    /// </summary>
    private static string MeasuredSize(GameObject asset, bool modelInside)
    {
        GameObject instance = PrefabUtility.InstantiatePrefab(asset) as GameObject;
        if (instance == null) return "?";

        instance.hideFlags = HideFlags.HideAndDontSave;

        GameObject target = instance;

        if (modelInside)
        {
            GameObject inner = FindModelInstance(instance);
            if (inner != null) target = inner;
        }

        string size = Size(BoundsOf(target));
        DestroyImmediate(instance);

        return size;
    }

    private static void EnsureFolder(string folder)
    {
        if (string.IsNullOrEmpty(folder)) return;

        folder = folder.Replace('\\', '/');
        if (folder == "Assets" || AssetDatabase.IsValidFolder(folder)) return;

        string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
    }
}
