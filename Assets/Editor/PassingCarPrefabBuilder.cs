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
/// wrapped around an instance of the model. Everything that matters - where the wheels sit, how big the
/// collider is, how far the exhaust hangs off the back - is measured against the model, and the model's own
/// parts live inside the FBX, where the identifiers that address them are Unity's to hand out: they are not
/// in the file to be read, they exist once Unity has imported it. So this asks Unity, and gets them right
/// rather than guessing.
///
/// The template is a working car prefab (the 206 unless changed). Its parts are copied whole - the exhaust is
/// the same ParticleSystem, the lights are the same spotlights, the collider is the same box, and they stay
/// exactly where the template put them - and the new model is then scaled and moved so it fills the same
/// space the template's model fills. Fitting the model to the layout is the whole trick: every part was
/// placed against the template's model, so a model that occupies the same box is driven, lit, exhausted and
/// collided with exactly as the template's car is. The 911 shows the alternative, wearing the 206's numbers
/// verbatim, which only works when the two models happen to be the same size.
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

    /// <summary>
    /// Set once the car has been built, so deleting it means deleting it. Versioned because the first
    /// version of this builder could not find a model inside a car prefab at all and gave up: bumping the
    /// key lets the fixed builder have its one automatic go at the same model.
    /// </summary>
    private const string BuiltOnceKey = "PassingCarPrefabBuilder.BuiltOnce.v2";

    private GameObject template;
    private GameObject model;
    private string outputPath = DefaultOutputPath;
    private Material bodyMaterial;

    [Tooltip("Scale the model so it fills the same space the template's car fills. On, the wheels, collider, " +
             "lights and exhaust line up on the new model the way they line up on the template's - turn it off " +
             "only if you want the model at the size it was imported and the template's layout copied verbatim.")]
    private bool fitModel = true;

    [Tooltip("Some models are exported facing the other way down their own Z axis. Turn this on if the new " +
             "car comes out with its front where its back should be - it rotates the model 180 degrees before " +
             "it is fitted, so its front faces the way the template's does.")]
    private bool flipModel;

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
                                           true, false, true, out error);

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
            new GUIContent("Template Car", "The prefab to copy the parts and settings from. Has to be a car " +
                                           "prefab built like the 206 and 911 - its model sitting inside it."),
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

        fitModel = EditorGUILayout.Toggle(
            new GUIContent("Fit Model To The Layout",
                "Scale the model so it fills the same space the template's car fills, so the template's " +
                "wheels, collider, lights and exhaust all land on it. Off keeps the model at its imported " +
                "size and copies the template's layout verbatim."),
            fitModel);

        flipModel = EditorGUILayout.Toggle(
            new GUIContent("Model Faces Backwards", "Rotate the model 180 degrees before fitting it. Use it " +
                                                    "if the built car has its front where its back should be."),
            flipModel);

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
        GameObject built = BuildPrefab(template, model, outputPath, bodyMaterial, fitModel, flipModel,
                                       overwrite, out error);

        if (built == null)
        {
            EditorUtility.DisplayDialog("Create Car Prefab", error, "OK");
            return;
        }

        EditorGUIUtility.PingObject(built);
        Selection.activeObject = built;
    }

    /// <summary>
    /// Reports the size of both models, which is what the model is fitted by. Measured when asked rather than
    /// every repaint: it has to place an instance to see a template's model.
    /// </summary>
    void Measure()
    {
        if (template == null || model == null)
        {
            measurement = "Assign a template car and a car model to measure them.";
            return;
        }

        measurement = "Template's car " + MeasuredSize(template, true) + " m, this model " +
                      MeasuredSize(model, false) + " m - the model is scaled to fill the template's car.";
    }

    // ---------------------------------------------------------------- building

    /// <summary>
    /// Builds the prefab, returning it, or null with the reason in <paramref name="error"/>. Nothing is left
    /// in the scene: the two working instances are hidden from the start and destroyed at the end.
    /// </summary>
    public static GameObject BuildPrefab(GameObject template, GameObject model, string outputPath,
                                         Material bodyMaterial, bool fitModel, bool flipModel, bool overwrite,
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

        // Never visited by the undo system or by the scene's save: these are working copies, not scene
        // objects, and they are torn down in the finally below whatever happens.
        templateInstance.hideFlags = HideFlags.HideAndDontSave;
        modelInstance.hideFlags = HideFlags.HideAndDontSave;

        try
        {
            return BuildNow(templateInstance, modelInstance, outputPath, bodyMaterial, fitModel, flipModel,
                            ref error);
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
                                       Material bodyMaterial, bool fitModel, bool flipModel, ref string error)
    {
        if (templateInstance.GetComponent<AICarController>() == null)
        {
            error = "'" + templateInstance.name + "' is not a passing-car prefab - there is no AICarController on " +
                    "it, so there is no car to copy. Point the builder at one of the prefabs the painter already " +
                    "spawns (the 206 or the 911), not at a model.";
            return null;
        }

        GameObject templateModel = FindModelInstance(templateInstance);

        if (templateModel == null)
        {
            error = "No model could be found inside '" + templateInstance.name + "'. A passing-car prefab holds " +
                    "its model as a nested prefab instance - the 206 holds 206fullOption.fbx that way - and the " +
                    "parts are laid out around it. Point the builder at a car prefab built like the 206 and 911.";
            return null;
        }

        // Both are squared up on the origin first, so the two models can be measured against each other. The
        // finished prefab gets the template's own root transform back at the end, which is what the painter's
        // cars are expected to wear.
        Quaternion rootRotation = templateInstance.transform.rotation;
        Vector3 rootScale = templateInstance.transform.localScale;

        templateInstance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        modelInstance.transform.SetPositionAndRotation(
            Vector3.zero, flipModel ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.identity);

        Bounds car = BoundsOf(templateModel);
        Bounds model = BoundsOf(modelInstance);

        if (IsEmpty(car) || IsEmpty(model))
        {
            error = "One of the two measures as nothing - it may have no renderers.";
            return null;
        }

        GameObject root = new GameObject(Path.GetFileNameWithoutExtension(outputPath));
        Dictionary<Transform, Transform> pairs = new Dictionary<Transform, Transform>();
        int ported = 0;

        foreach (Transform child in templateInstance.transform)
        {
            if (child.gameObject == templateModel)
            {
                // The template's model is replaced by this one. Everything else in the prefab was placed
                // against the template's model, so the new model is scaled and moved to fill the same space
                // it filled: the wheels, collider, lights and exhaust then sit on the new model exactly where
                // they sat on the template's car.
                modelInstance.transform.SetParent(root.transform, false);
                modelInstance.hideFlags = HideFlags.None;
                FitModelTo(modelInstance.transform, model, car, fitModel);

                // Anything the artist added inside the template's model - the 206 keeps an extra lens mesh
                // there, which is where its headlight material lives - comes along in the same place relative
                // to the model. Fitting scales the model's own root, so a child left exactly where it was
                // stays on the part of the model it was put on.
                foreach (Transform extra in templateModel.transform)
                {
                    if (PrefabUtility.GetCorrespondingObjectFromSource(extra.gameObject) != null) continue;
                    if (PrefabUtility.GetCorrespondingObjectFromOriginalSource(extra.gameObject) != null) continue;

                    GameObject extraCopy = Instantiate(extra.gameObject);
                    extraCopy.name = extra.name;
                    extraCopy.transform.SetParent(modelInstance.transform, false);
                    MapHierarchy(extra, extraCopy.transform, pairs);
                }

                continue;
            }

            // The template's own objects - the wheels, the body collider, the exhaust, the lights - copied
            // whole, so their components and materials come with them, and left exactly where they were.
            GameObject copy = Instantiate(child.gameObject);
            copy.name = child.name;
            copy.transform.SetParent(root.transform, false);

            MapHierarchy(child, copy.transform, pairs);
            ported++;
        }

        if (modelInstance.transform.parent != root.transform)
        {
            error = "The template's model is not a child of the template prefab, so there is nowhere to put " +
                    "the new one.";
            return null;
        }

        // The root's own components: the rigid body and the AI controller, copied with their settings and
        // then pointed at the copies rather than at the template they came from.
        Component[] components = templateInstance.GetComponents<Component>();

        foreach (Component component in components)
        {
            if (component == null || component is Transform) continue;

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
        // the working copies.
        DestroyImmediate(root);

        if (!saved || prefab == null)
        {
            error = "Unity refused to save the prefab at " + outputPath + ".";
            return null;
        }

        EditorGUIUtility.PingObject(prefab);

        Debug.Log(string.Format(
            "[CarPrefab] {0}: built from '{1}' on the '{2}' layout - {3} objects ported, car {4} against the " +
            "template's {5}, {6} body slot(s) wearing {7}. The passing-car painter finds prefabs in " +
            "Assets/Prefabs/Cars by itself.",
            outputPath, modelInstance.name, templateInstance.name, ported, Size(car), Size(model), bodySlots,
            bodyMaterial != null ? bodyMaterial.name : "nothing"));

        return prefab;
    }

    /// <summary>
    /// Scales and moves the model so it occupies the same box the template's car occupies.
    ///
    /// Scaling the model's root scales everything inside it about its pivot, so its bounds grow by the same
    /// ratio, axis for axis: put the scaled bounds back over the template's car and the two line up. That is
    /// why the model is squared up on the origin first, and why it is fitted by its box rather than by its
    /// pivot - a model exported with its pivot at the rear axle and one exported with it in the middle still
    /// end up in the same place on the template's layout.
    /// </summary>
    private static void FitModelTo(Transform model, Bounds modelBounds, Bounds carBounds, bool fit)
    {
        if (!fit) return;

        Vector3 ratio = new Vector3(
            carBounds.size.x / modelBounds.size.x,
            carBounds.size.y / modelBounds.size.y,
            carBounds.size.z / modelBounds.size.z);

        model.localScale = Vector3.Scale(model.localScale, ratio);
        model.localPosition = carBounds.center - Vector3.Scale(ratio, modelBounds.center);
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

    /// <summary>A source transform and the copy of it, for everything under one copied object.</summary>
    private static void MapHierarchy(Transform source, Transform copy, Dictionary<Transform, Transform> pairs)
    {
        pairs[source] = copy;

        for (int i = 0; i < source.childCount && i < copy.childCount; i++)
        {
            MapHierarchy(source.GetChild(i), copy.GetChild(i), pairs);
        }
    }

    /// <summary>
    /// The instantiated model inside a car prefab. A car prefab holds its model as a nested prefab instance -
    /// the 206 holds 206fullOption.fbx, the 911 holds another model - so it is the child that came from an
    /// imported model file rather than from the prefab itself. Three ways to ask, from most to least direct,
    /// because the answer differs between a model nested in a prefab and one placed directly.
    /// </summary>
    private static GameObject FindModelInstance(GameObject root)
    {
        // 1. The child whose original source is a model file. The *original* source matters: for a model
        //    nested inside another prefab, the plain "from source" answer is the outer prefab, not the FBX.
        foreach (Transform child in root.transform)
        {
            if (IsModelObject(child.gameObject)) return child.gameObject;
        }

        // 2. The child that is itself a prefab instance, which is how a model sits in a prefab.
        foreach (Transform child in root.transform)
        {
            if (!PrefabUtility.IsAnyPrefabInstanceRoot(child.gameObject)) continue;
            if (AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(child.gameObject)) != "")
                return child.gameObject;
        }

        // 3. The child the car's own renderers hang off. The artist's parts - wheels, collider, exhaust,
        //    lights - carry no renderer of their own, so this still picks the model out.
        foreach (Transform child in root.transform)
        {
            if (HasRenderers(child.gameObject)) return child.gameObject;
        }

        return null;
    }

    private static bool IsModelObject(GameObject go)
    {
        Object[] sources =
        {
            PrefabUtility.GetCorrespondingObjectFromSource(go),
            PrefabUtility.GetCorrespondingObjectFromOriginalSource(go),
        };

        for (int i = 0; i < sources.Length; i++)
        {
            if (sources[i] == null) continue;

            string path = AssetDatabase.GetAssetPath(sources[i]);
            if (!string.IsNullOrEmpty(path) && path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static bool HasRenderers(GameObject go)
    {
        Renderer[] renderers = go.GetComponentsInChildren<Renderer>(true);

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] is ParticleSystemRenderer || renderers[i] is TrailRenderer) continue;
            return true;
        }

        return false;
    }

    private static bool IsEmpty(Bounds bounds)
    {
        return bounds.size.x < 0.0001f || bounds.size.y < 0.0001f || bounds.size.z < 0.0001f;
    }

    /// <summary>
    /// The box a model fills, as Unity would draw it: the exhaust and any trails are effects hanging off the
    /// car rather than the car itself, and anything switched off - a model often arrives with leftovers the
    /// artist disabled - is not part of what the car needs to fit into.
    /// </summary>
    private static Bounds BoundsOf(GameObject go)
    {
        Renderer[] renderers = go.GetComponentsInChildren<Renderer>(true);
        Bounds bounds = new Bounds();
        bool any = false;

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] is ParticleSystemRenderer || renderers[i] is TrailRenderer) continue;
            if (!renderers[i].gameObject.activeInHierarchy || !renderers[i].enabled) continue;

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
