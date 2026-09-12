using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Editor tool that installs the progress driven rain into a level.
///
/// Rain in the game is a RainSystem component with three particle systems built
/// underneath it ("Rain Far", "Rain Near" and "Rain Splashes"). This window is
/// the one click way to get that whole setup into a scene:
///
///  - creates the rain textures and materials as real assets under
///    Assets/Materials/Weather (they can be opened and tweaked like any other
///    material, and the level references them normally),
///  - creates "Weather/Rain" with a RainSystem on it, configured with the values
///    below and wired to those materials,
///  - writes the intensity profile (light drizzle at the start, downpour from
///    the middle of the level) and the progress source used to drive it.
///
/// Once it is in the scene, tweak the object itself in the inspector while the
/// level plays: rates, sizes, wind, colours and the profile are applied live.
/// After changing anything that affects the emitter volume (layer toggles, areas,
/// heights, width/aspect or materials) use "Rebuild Particle Systems" on the
/// component's context menu, or the Rebuild button here.
///
/// Usage: open a level scene (Core-4 for example) and then
///   Tools > Road Tools > Paint Rain
/// </summary>
public class RainPainter : EditorWindow
{
    private const string RainTypeName = "RainSystem";
    private const string AssetFolder = "Assets/Materials/Weather";
    private const string StreakTexturePath = AssetFolder + "/RainStreak.png";
    private const string SplashTexturePath = AssetFolder + "/RainSplash.png";
    private const string StreakMaterialPath = AssetFolder + "/RainStreak.mat";
    private const string SplashMaterialPath = AssetFolder + "/RainSplash.mat";
    private const string RootName = "Rain";
    private const string ParentName = "Weather";
    private const string PrefsPrefix = "RainPainter.";

    private static readonly string[] ProgressSourceNames =
    {
        "Auto", "Progress Display", "Checkpoints", "Distance Travelled", "Manual"
    };

    // ------------------------------------------------------------------
    //  window state (persisted in EditorPrefs so the window comes back the
    //  same way, and synced from the scene whenever rain is already there)
    // ------------------------------------------------------------------

    private float lightIntensity = 0.15f;
    private float heavyIntensity = 1f;
    private float rampStart = 0.2f;
    private float rampEnd = 0.5f;
    private float rampEase = 1.2f;
    private float intensitySmoothing = 3.5f;

    private int progressSourceIndex = 0;
    private float distanceRampMeters = 1500f;
    private float manualProgress = 0.5f;

    private bool farLayer = true;
    private Vector2 farArea = new Vector2(120f, 120f);
    private float farHeight = 22f;
    private float farRate = 9000f;
    private float farWidth = 0.05f;
    private float farStreakAspect = 10f;
    private float farVelocityStretch = 0.05f;

    private bool nearLayer = true;
    private bool splashLayer = true;
    private float splashRate = 2000f;

    private float fallSpeed = 30f;
    private Vector2 wind = new Vector2(2.5f, 1.5f);
    private float motionWind = 0.35f;

    private Color streakTint = new Color(0.78f, 0.83f, 0.92f, 0.30f);
    private Color splashTint = new Color(0.85f, 0.90f, 0.98f, 0.35f);

    private bool regenerateAssets;
    private bool deleteAssetsOnRemove;

    private bool previewing;
    private float previewAmount = 0.5f;
    private double nextPreviewStep;
    private double lastPreviewStep;
    private bool syncedFromScene;

    private Vector2 scroll;

    [MenuItem("Tools/Road Tools/Paint Rain")]
    static void OpenWindow()
    {
        RainPainter window = GetWindow<RainPainter>("Rain Painter");
        window.minSize = new Vector2(430, 660);
        window.Show();
    }

    private void OnEnable()
    {
        LoadPrefs();
        EditorApplication.update += OnEditorUpdate;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        UnityEditor.SceneManagement.EditorSceneManager.activeSceneChanged += OnActiveSceneChanged;

        if (FindRainComponent() != null)
            SyncFromScene();
    }

    private void OnDisable()
    {
        EditorApplication.update -= OnEditorUpdate;
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        UnityEditor.SceneManagement.EditorSceneManager.activeSceneChanged -= OnActiveSceneChanged;

        StopPreview();
        SavePrefs();
    }

    private void OnActiveSceneChanged(Scene previous, Scene current)
    {
        syncedFromScene = false;
        StopPreview();
        if (FindRainComponent() != null)
            SyncFromScene();
        Repaint();
    }

    private void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (previewing)
            StopPreview();
    }

    // ---------------------------------------------------------------- GUI ---

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        GUILayout.Label("Paint Rain", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Adds progress driven rain to this level: it starts as a light drizzle and " +
            "builds up to a heavy downpour by the time the player is about half-way " +
            "down the road. Everything is read from the level's own progress, so the " +
            "rain matches the percentage the checkpoints show.",
            MessageType.Info);

        DrawSceneStatus();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Intensity profile", EditorStyles.boldLabel);
        EditorGUILayout.LabelField(
            $"light {lightIntensity * 100f:F0}% at the start  ->  heavy {heavyIntensity * 100f:F0}% " +
            $"from {rampEnd * 100f:F0}% of the level",
            EditorStyles.miniLabel);

        lightIntensity = EditorGUILayout.Slider(
            new GUIContent("Light Intensity", "Strength of the rain at the very start of the level"),
            lightIntensity, 0f, 1f);
        heavyIntensity = EditorGUILayout.Slider(
            new GUIContent("Heavy Intensity", "Strength of the rain once it has built up"),
            heavyIntensity, 0f, 1f);
        rampStart = EditorGUILayout.Slider(
            new GUIContent("Ramp Start", "Level progress where the rain starts building up (0.2 = 20%)"),
            rampStart, 0f, 1f);
        rampEnd = EditorGUILayout.Slider(
            new GUIContent("Ramp End", "Level progress where the rain reaches its heaviest (0.5 = half-way)"),
            rampEnd, 0f, 1f);
        rampEase = EditorGUILayout.Slider(
            new GUIContent("Ramp Ease", "1 = straight ramp, above 1 stays light for longer then climbs quickly"),
            rampEase, 0.25f, 4f);
        intensitySmoothing = EditorGUILayout.Slider(
            new GUIContent("Smoothing", "How quickly the rain reacts to progress changes (per second)"),
            intensitySmoothing, 0.2f, 12f);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Progress source", EditorStyles.boldLabel);
        progressSourceIndex = EditorGUILayout.Popup(
            new GUIContent("Source", "Where the level progress is read from. Auto uses the progress " +
                                      "display the checkpoints feed, then the compass, then distance."),
            progressSourceIndex, ProgressSourceNames);
        distanceRampMeters = EditorGUILayout.FloatField(
            new GUIContent("Distance Ramp (m)", "Only used when the level has no progress display or compass"),
            distanceRampMeters);
        manualProgress = EditorGUILayout.Slider(
            new GUIContent("Manual Progress", "Progress used while Source is Manual"),
            manualProgress, 0f, 1f);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Rain volume and look", EditorStyles.boldLabel);
        farLayer = EditorGUILayout.Toggle(
            new GUIContent("Far Layer", "Huge sheet of rain that fills the whole view"),
            farLayer);
        farArea = EditorGUILayout.Vector2Field(
            new GUIContent("Far Area (m)", "Width / depth of the sheet the rain spawns in"),
            farArea);
        farHeight = EditorGUILayout.FloatField(
            new GUIContent("Far Height (m)", "How high above the player the rain sheet sits"),
            farHeight);
        farRate = EditorGUILayout.FloatField(
            new GUIContent("Far Rate", "Rain particles per second at full intensity"),
            farRate);
        farWidth = EditorGUILayout.FloatField(
            new GUIContent("Streak Width (m)", "Thickness of one rain streak"),
            farWidth);
        farStreakAspect = EditorGUILayout.FloatField(
            new GUIContent("Streak Length x Width", "How many times longer than wide a streak is"),
            farStreakAspect);
        farVelocityStretch = EditorGUILayout.FloatField(
            new GUIContent("Velocity Stretch", "Extra stretching per metre/second of speed"),
            farVelocityStretch);

        nearLayer = EditorGUILayout.Toggle(
            new GUIContent("Near Layer", "Smaller, thicker and faster rain right around the car"),
            nearLayer);
        splashLayer = EditorGUILayout.Toggle(
            new GUIContent("Splashes", "Small impacts on the ground under the player"),
            splashLayer);
        splashRate = EditorGUILayout.FloatField(
            new GUIContent("Splash Rate", "Impacts per second at full intensity"),
            splashRate);

        fallSpeed = EditorGUILayout.FloatField(
            new GUIContent("Fall Speed (m/s)", "How fast the drops fall"),
            fallSpeed);
        wind = EditorGUILayout.Vector2Field(
            new GUIContent("Wind (x/z)", "Constant sideways drift of the rain"),
            wind);
        motionWind = EditorGUILayout.Slider(
            new GUIContent("Motion Wind", "How much the player's own speed slants the rain"),
            motionWind, 0f, 1f);

        streakTint = EditorGUILayout.ColorField(
            new GUIContent("Streak Tint", "Colour and opacity of the rain streaks"),
            streakTint);
        splashTint = EditorGUILayout.ColorField(
            new GUIContent("Splash Tint", "Colour and opacity of the ground splashes"),
            splashTint);

        EditorGUILayout.Space();
        regenerateAssets = EditorGUILayout.ToggleLeft(
            new GUIContent("Regenerate the rain textures/materials", "Recreate RainStreak.png, RainSplash.png " +
                                                                     "and their materials from scratch"),
            regenerateAssets);
        deleteAssetsOnRemove = EditorGUILayout.ToggleLeft(
            new GUIContent("Also delete the generated assets when removing", "Removes " + AssetFolder + " as well"),
            deleteAssetsOnRemove);

        EditorGUILayout.Space();
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button(FindRainComponent() == null ? "Install Rain in this Scene" : "Update Rain in this Scene",
                GUILayout.Height(26f)))
        {
            Install();
        }
        if (GUILayout.Button("Rebuild", GUILayout.Height(26f), GUILayout.Width(90f)))
        {
            Rebuild();
        }
        if (GUILayout.Button("Remove", GUILayout.Height(26f), GUILayout.Width(90f)))
        {
            RemoveRain();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();
        DrawPreviewControls();

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "Rates, sizes, wind, colours and the profile are applied while the level plays - no rebuild " +
            "needed. After changing the layer toggles, areas, heights, width/aspect or the materials, " +
            "press Rebuild (or use the same entry on the Rain System component's context menu).",
            MessageType.None);

        EditorGUILayout.EndScrollView();
    }

    private void DrawSceneStatus()
    {
        Component rain = FindRainComponent();

        EditorGUILayout.BeginHorizontal();

        if (rain == null)
        {
            EditorGUILayout.HelpBox("No rain in this scene yet.", MessageType.Warning);
        }
        else
        {
            EditorGUILayout.LabelField("Rain System in scene:", GUILayout.Width(130f));
            EditorGUILayout.ObjectField(rain, rain.GetType(), true);
        }

        EditorGUILayout.EndHorizontal();
    }

    private void DrawPreviewControls()
    {
        EditorGUILayout.LabelField("Preview (without playing)", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        if (previewing)
        {
            if (GUILayout.Button("Stop Preview"))
                StopPreview();
        }
        else
        {
            if (GUILayout.Button($"Preview at {previewAmount * 100f:F0}%"))
                StartPreview(previewAmount);
        }

        if (GUILayout.Button("Light"))
            StartPreview(Mathf.Max(0.05f, lightIntensity));
        if (GUILayout.Button("Half-way"))
            StartPreview(EvaluateWindowIntensityAt(0.5f));
        if (GUILayout.Button("Heavy"))
            StartPreview(heavyIntensity);

        EditorGUILayout.EndHorizontal();

        previewAmount = EditorGUILayout.Slider("Preview Strength", previewAmount, 0f, 1f);

        EditorGUILayout.LabelField(
            "Preview only runs in the editor: it steps the particles by hand so the Scene view shows the rain " +
            "without entering play mode.",
            EditorStyles.miniLabel);
    }

    private float EvaluateWindowIntensityAt(float progress)
    {
        float t = rampEnd > rampStart ? Mathf.InverseLerp(rampStart, rampEnd, progress) : 1f;
        if (!Mathf.Approximately(rampEase, 1f))
            t = Mathf.Pow(t, rampEase);
        return Mathf.Lerp(lightIntensity, heavyIntensity, Mathf.Clamp01(t));
    }

    // ------------------------------------------------------------ actions ---

    private void Install()
    {
        Type rainType = FindRainType();
        if (rainType == null)
        {
            EditorUtility.DisplayDialog("Paint Rain",
                "Could not find the RainSystem component (" + RainTypeName + ".cs).\n\n" +
                "Let Unity finish compiling and try again.", "OK");
            return;
        }

        Material streakMaterial = EnsureMaterial(StreakMaterialPath, StreakTexturePath, CreateStreakTexture,
            "RainStreak", regenerateAssets);
        Material splashMaterial = EnsureMaterial(SplashMaterialPath, SplashTexturePath, CreateSplashTexture,
            "RainSplash", regenerateAssets);

        Component rain = FindRainComponent();
        GameObject root;

        if (rain == null)
        {
            GameObject parent = GameObject.Find(ParentName);
            if (parent == null)
            {
                parent = new GameObject(ParentName);
                Undo.RegisterCreatedObjectUndo(parent, "Install Rain");
            }

            root = new GameObject(RootName);
            root.transform.SetParent(parent.transform, false);
            root.transform.position = Vector3.zero;
            Undo.RegisterCreatedObjectUndo(root, "Install Rain");

            rain = root.AddComponent(rainType);
        }
        else
        {
            root = rain.gameObject;
            Undo.RegisterCompleteObjectUndo(rain, "Update Rain");
        }

        ApplySettings(rain, streakMaterial, splashMaterial);
        InvokeRain(rain, "Rebuild");

        EditorSceneManager.MarkSceneDirty(root.scene);
        Selection.activeGameObject = root;
        syncedFromScene = true;

        Debug.Log("Rain installed in '" + root.scene.name + "': Weather/" + RootName +
                  " (light " + (lightIntensity * 100f).ToString("F0") + "% -> heavy " +
                  (heavyIntensity * 100f).ToString("F0") + "% by " + (rampEnd * 100f).ToString("F0") +
                  "% of the level).", root);
    }

    private void Rebuild()
    {
        Component rain = FindRainComponent();
        if (rain == null)
        {
            Debug.LogWarning("Paint Rain: there is no Rain System in this scene to rebuild.");
            return;
        }

        Undo.RegisterCompleteObjectUndo(rain, "Rebuild Rain");
        InvokeRain(rain, "Rebuild");
        EditorSceneManager.MarkSceneDirty(rain.gameObject.scene);
        Debug.Log("Rain particle systems rebuilt from the values on the Rain System component.", rain);
    }

    private void RemoveRain()
    {
        Component rain = FindRainComponent();
        if (rain == null)
        {
            Debug.LogWarning("Paint Rain: there is no Rain System in this scene to remove.");
            return;
        }

        Undo.DestroyObjectImmediate(rain.gameObject);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

        if (deleteAssetsOnRemove && AssetDatabase.IsValidFolder(AssetFolder))
            AssetDatabase.DeleteAsset(AssetFolder);

        Debug.Log("Rain removed from the scene.");
    }

    private void ApplySettings(Component rain, Material streakMaterial, Material splashMaterial)
    {
        SerializedObject serialized = new SerializedObject(rain);
        Missing.Clear();

        SetFloat(serialized, "lightIntensity", lightIntensity);
        SetFloat(serialized, "heavyIntensity", heavyIntensity);
        SetFloat(serialized, "rampStart", rampStart);
        SetFloat(serialized, "rampEnd", Mathf.Max(rampStart, rampEnd));
        SetFloat(serialized, "rampEase", rampEase);
        SetFloat(serialized, "intensitySmoothing", intensitySmoothing);
        SetEnumIndex(serialized, "progressSource", progressSourceIndex);
        SetFloat(serialized, "distanceRampMeters", distanceRampMeters);
        SetFloat(serialized, "manualProgress", manualProgress);

        SetBool(serialized, "farLayer", farLayer);
        SetVector2(serialized, "farArea", farArea);
        SetFloat(serialized, "farHeight", farHeight);
        SetFloat(serialized, "farRate", farRate);
        SetFloat(serialized, "farWidth", farWidth);
        SetFloat(serialized, "farStreakAspect", farStreakAspect);
        SetFloat(serialized, "farVelocityStretch", farVelocityStretch);

        SetBool(serialized, "nearLayer", nearLayer);
        SetBool(serialized, "splashLayer", splashLayer);
        SetFloat(serialized, "splashRate", splashRate);

        SetFloat(serialized, "fallSpeed", fallSpeed);
        SetVector2(serialized, "wind", wind);
        SetFloat(serialized, "motionWind", motionWind);

        SetColor(serialized, "streakTint", streakTint);
        SetColor(serialized, "splashTint", splashTint);

        SetObject(serialized, "streakMaterial", streakMaterial);
        SetObject(serialized, "splashMaterial", splashMaterial);

        serialized.ApplyModifiedProperties();

        if (Missing.Count > 0)
        {
            Debug.LogWarning("Paint Rain: these settings no longer exist on " + RainTypeName + ": " +
                             string.Join(", ", Missing.ToArray()), rain);
        }
    }

    /// <summary>Copies the values from the rain component that is already in the scene into this window.</summary>
    private void SyncFromScene()
    {
        Component rain = FindRainComponent();
        if (rain == null)
            return;

        SerializedObject serialized = new SerializedObject(rain);

        lightIntensity = GetFloat(serialized, "lightIntensity", lightIntensity);
        heavyIntensity = GetFloat(serialized, "heavyIntensity", heavyIntensity);
        rampStart = GetFloat(serialized, "rampStart", rampStart);
        rampEnd = GetFloat(serialized, "rampEnd", rampEnd);
        rampEase = GetFloat(serialized, "rampEase", rampEase);
        intensitySmoothing = GetFloat(serialized, "intensitySmoothing", intensitySmoothing);

        progressSourceIndex = GetEnumIndex(serialized, "progressSource", progressSourceIndex);
        distanceRampMeters = GetFloat(serialized, "distanceRampMeters", distanceRampMeters);
        manualProgress = GetFloat(serialized, "manualProgress", manualProgress);

        farLayer = GetBool(serialized, "farLayer", farLayer);
        farArea = GetVector2(serialized, "farArea", farArea);
        farHeight = GetFloat(serialized, "farHeight", farHeight);
        farRate = GetFloat(serialized, "farRate", farRate);
        farWidth = GetFloat(serialized, "farWidth", farWidth);
        farStreakAspect = GetFloat(serialized, "farStreakAspect", farStreakAspect);
        farVelocityStretch = GetFloat(serialized, "farVelocityStretch", farVelocityStretch);

        nearLayer = GetBool(serialized, "nearLayer", nearLayer);
        splashLayer = GetBool(serialized, "splashLayer", splashLayer);
        splashRate = GetFloat(serialized, "splashRate", splashRate);

        fallSpeed = GetFloat(serialized, "fallSpeed", fallSpeed);
        wind = GetVector2(serialized, "wind", wind);
        motionWind = GetFloat(serialized, "motionWind", motionWind);

        streakTint = GetColor(serialized, "streakTint", streakTint);
        splashTint = GetColor(serialized, "splashTint", splashTint);

        syncedFromScene = true;
        Repaint();
    }

    // ------------------------------------------------------------ preview ---

    private void StartPreview(float amount)
    {
        Component rain = FindRainComponent();
        if (rain == null)
        {
            Debug.LogWarning("Paint Rain: install the rain before previewing it.");
            return;
        }

        previewing = true;
        InvokeRain(rain, "PreviewIntensity", Mathf.Clamp01(amount));

        // Warm the systems up so the view is full of rain straight away.
        ParticleSystem[] systems = rain.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < 60; i++)
            StepSystems(systems, 0.02f);

        lastPreviewStep = EditorApplication.timeSinceStartup;
        nextPreviewStep = lastPreviewStep;
        SceneView.RepaintAll();
        Repaint();
    }

    private void StopPreview()
    {
        if (!previewing)
            return;

        previewing = false;

        Component rain = FindRainComponent();
        if (rain != null)
            InvokeRain(rain, "Rebuild");

        SceneView.RepaintAll();
    }

    private void OnEditorUpdate()
    {
        if (!previewing)
            return;

        double now = EditorApplication.timeSinceStartup;
        if (now < nextPreviewStep)
            return;

        float delta = Mathf.Clamp((float)(now - lastPreviewStep), 0.005f, 0.05f);
        lastPreviewStep = now;
        nextPreviewStep = now + 0.02;

        Component rain = FindRainComponent();
        if (rain == null)
        {
            previewing = false;
            return;
        }

        StepSystems(rain.GetComponentsInChildren<ParticleSystem>(true), delta);
        SceneView.RepaintAll();
    }

    private static void StepSystems(ParticleSystem[] systems, float delta)
    {
        for (int i = 0; i < systems.Length; i++)
        {
            if (systems[i] == null)
                continue;

            systems[i].Simulate(delta, false, false);
        }
    }

    // ------------------------------------------------- generated assets ---

    private Material EnsureMaterial(string materialPath, string texturePath,
        Func<Texture2D> textureFactory, string name, bool regenerate)
    {
        EnsureFolder();

        Material existing = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);

        if (!regenerate && existing != null && texture != null)
            return existing;

        if (regenerate)
        {
            if (existing != null)
                AssetDatabase.DeleteAsset(materialPath);
            if (texture != null)
                AssetDatabase.DeleteAsset(texturePath);
        }

        texture = SaveTexture(textureFactory(), texturePath);

        Material material = new Material(FindParticleShader());
        material.name = name;
        material.mainTexture = texture;
        ApplyAlphaBlend(material, Color.white);

        AssetDatabase.CreateAsset(material, materialPath);
        AssetDatabase.SaveAssets();

        return AssetDatabase.LoadAssetAtPath<Material>(materialPath);
    }

    private static void EnsureFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Materials"))
            AssetDatabase.CreateFolder("Assets", "Materials");
        if (!AssetDatabase.IsValidFolder(AssetFolder))
            AssetDatabase.CreateFolder("Assets/Materials", "Weather");
    }

    private static Texture2D SaveTexture(Texture2D texture, string path)
    {
        byte[] png = texture.EncodeToPNG();
        DestroyImmediate(texture);

        File.WriteAllBytes(path, png);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    /// <summary>A soft rain streak: bright at the leading end, fading up its tail.</summary>
    private static Texture2D CreateStreakTexture()
    {
        const int width = 16;
        const int height = 128;

        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[width * height];
        float centre = (width - 1) * 0.5f;

        for (int y = 0; y < height; y++)
        {
            float v = y / (float)(height - 1);
            float head = Mathf.SmoothStep(0f, 1f, Mathf.Pow(v, 1.7f));
            float along = Mathf.Lerp(0.4f, 1f, head);

            for (int x = 0; x < width; x++)
            {
                float d = Mathf.Abs(x - centre) / Mathf.Max(centre, 0.001f);
                float side = Mathf.Exp(-d * d * 7f);
                pixels[y * width + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(side * along));
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        return texture;
    }

    /// <summary>A small water ring for the ground splashes.</summary>
    private static Texture2D CreateSplashTexture()
    {
        const int size = 32;

        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[size * size];
        float centre = (size - 1) * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float nx = (x - centre) / centre;
                float ny = (y - centre) / centre;
                float r = Mathf.Sqrt(nx * nx + ny * ny);

                float ring = Mathf.Exp(-Mathf.Pow((r - 0.62f) * 3.2f, 2f));
                float core = Mathf.Exp(-r * r * 9f) * 0.45f;

                pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(ring * 0.85f + core));
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        return texture;
    }

    private static Shader FindParticleShader()
    {
        string[] candidates =
        {
            "Legacy Shaders/Particles/Alpha Blended",
            "Mobile/Particles/Alpha Blended",
            "Particles/Standard Unlit",
            "Sprites/Default",
            "Unlit/Transparent"
        };

        for (int i = 0; i < candidates.Length; i++)
        {
            Shader shader = Shader.Find(candidates[i]);
            if (shader != null)
                return shader;
        }

        return Shader.Find("Sprites/Default");
    }

    private static void ApplyAlphaBlend(Material material, Color tint)
    {
        if (material.HasProperty("_TintColor"))
            material.SetColor("_TintColor", tint);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", tint);
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", tint);

        // The Standard particle shader is opaque until its blend keywords are set;
        // the legacy and mobile ones are already alpha blended.
        if (material.HasProperty("_SrcBlend"))
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (material.HasProperty("_DstBlend"))
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (material.HasProperty("_ZWrite"))
            material.SetInt("_ZWrite", 0);
        if (material.HasProperty("_Cull"))
            material.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);

        material.DisableKeyword("_ALPHATEST_ON");
        material.EnableKeyword("_ALPHABLEND_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.SetOverrideTag("RenderType", "Transparent");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    }

    // ---------------------------------------------------- reflection glue ---

    /// <summary>
    /// RainSystem lives in the game scripts, which this editor assembly cannot
    /// reference, so it is looked up by name and driven through reflection.
    /// </summary>
    private static Type FindRainType()
    {
        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();

        for (int i = 0; i < assemblies.Length; i++)
        {
            Type type;
            try
            {
                type = assemblies[i].GetType(RainTypeName);
            }
            catch (Exception)
            {
                continue;
            }

            if (type != null && typeof(MonoBehaviour).IsAssignableFrom(type))
                return type;
        }

        return null;
    }

    private static Component FindRainComponent()
    {
        Type type = FindRainType();
        if (type == null)
            return null;

        return UnityEngine.Object.FindObjectOfType(type) as Component;
    }

    private static void InvokeRain(Component rain, string method)
    {
        MethodInfo info = rain.GetType().GetMethod(method, BindingFlags.Public | BindingFlags.Instance);
        if (info != null)
            info.Invoke(rain, null);
    }

    private static void InvokeRain(Component rain, string method, float argument)
    {
        MethodInfo info = rain.GetType().GetMethod(method, BindingFlags.Public | BindingFlags.Instance,
            null, new[] { typeof(float) }, null);
        if (info != null)
            info.Invoke(rain, new object[] { argument });
    }

    // --------------------------------------------------- serialized fields ---

    private static readonly System.Collections.Generic.List<string> Missing =
        new System.Collections.Generic.List<string>();

    private static SerializedProperty Find(SerializedObject serialized, string name)
    {
        SerializedProperty property = serialized.FindProperty(name);
        if (property == null)
            Missing.Add(name);
        return property;
    }

    private static void SetFloat(SerializedObject serialized, string name, float value)
    {
        SerializedProperty property = Find(serialized, name);
        if (property != null)
            property.floatValue = value;
    }

    private static void SetBool(SerializedObject serialized, string name, bool value)
    {
        SerializedProperty property = Find(serialized, name);
        if (property != null)
            property.boolValue = value;
    }

    private static void SetVector2(SerializedObject serialized, string name, Vector2 value)
    {
        SerializedProperty property = Find(serialized, name);
        if (property != null)
            property.vector2Value = value;
    }

    private static void SetColor(SerializedObject serialized, string name, Color value)
    {
        SerializedProperty property = Find(serialized, name);
        if (property != null)
            property.colorValue = value;
    }

    private static void SetEnumIndex(SerializedObject serialized, string name, int value)
    {
        SerializedProperty property = Find(serialized, name);
        if (property != null)
            property.enumValueIndex = value;
    }

    private static void SetObject(SerializedObject serialized, string name, UnityEngine.Object value)
    {
        SerializedProperty property = Find(serialized, name);
        if (property != null)
            property.objectReferenceValue = value;
    }

    private static float GetFloat(SerializedObject serialized, string name, float fallback)
    {
        SerializedProperty property = serialized.FindProperty(name);
        return property != null ? property.floatValue : fallback;
    }

    private static bool GetBool(SerializedObject serialized, string name, bool fallback)
    {
        SerializedProperty property = serialized.FindProperty(name);
        return property != null ? property.boolValue : fallback;
    }

    private static Vector2 GetVector2(SerializedObject serialized, string name, Vector2 fallback)
    {
        SerializedProperty property = serialized.FindProperty(name);
        return property != null ? property.vector2Value : fallback;
    }

    private static Color GetColor(SerializedObject serialized, string name, Color fallback)
    {
        SerializedProperty property = serialized.FindProperty(name);
        return property != null ? property.colorValue : fallback;
    }

    private static int GetEnumIndex(SerializedObject serialized, string name, int fallback)
    {
        SerializedProperty property = serialized.FindProperty(name);
        return property != null ? property.enumValueIndex : fallback;
    }

    // -------------------------------------------------------------- prefs ---

    private void LoadPrefs()
    {
        lightIntensity = EditorPrefs.GetFloat(PrefsPrefix + "lightIntensity", lightIntensity);
        heavyIntensity = EditorPrefs.GetFloat(PrefsPrefix + "heavyIntensity", heavyIntensity);
        rampStart = EditorPrefs.GetFloat(PrefsPrefix + "rampStart", rampStart);
        rampEnd = EditorPrefs.GetFloat(PrefsPrefix + "rampEnd", rampEnd);
        rampEase = EditorPrefs.GetFloat(PrefsPrefix + "rampEase", rampEase);
        intensitySmoothing = EditorPrefs.GetFloat(PrefsPrefix + "intensitySmoothing", intensitySmoothing);

        progressSourceIndex = EditorPrefs.GetInt(PrefsPrefix + "progressSourceIndex", progressSourceIndex);
        distanceRampMeters = EditorPrefs.GetFloat(PrefsPrefix + "distanceRampMeters", distanceRampMeters);
        manualProgress = EditorPrefs.GetFloat(PrefsPrefix + "manualProgress", manualProgress);

        farLayer = EditorPrefs.GetBool(PrefsPrefix + "farLayer", farLayer);
        farArea = new Vector2(
            EditorPrefs.GetFloat(PrefsPrefix + "farAreaX", farArea.x),
            EditorPrefs.GetFloat(PrefsPrefix + "farAreaY", farArea.y));
        farHeight = EditorPrefs.GetFloat(PrefsPrefix + "farHeight", farHeight);
        farRate = EditorPrefs.GetFloat(PrefsPrefix + "farRate", farRate);
        farWidth = EditorPrefs.GetFloat(PrefsPrefix + "farWidth", farWidth);
        farStreakAspect = EditorPrefs.GetFloat(PrefsPrefix + "farStreakAspect", farStreakAspect);
        farVelocityStretch = EditorPrefs.GetFloat(PrefsPrefix + "farVelocityStretch", farVelocityStretch);

        nearLayer = EditorPrefs.GetBool(PrefsPrefix + "nearLayer", nearLayer);
        splashLayer = EditorPrefs.GetBool(PrefsPrefix + "splashLayer", splashLayer);
        splashRate = EditorPrefs.GetFloat(PrefsPrefix + "splashRate", splashRate);

        fallSpeed = EditorPrefs.GetFloat(PrefsPrefix + "fallSpeed", fallSpeed);
        wind = new Vector2(
            EditorPrefs.GetFloat(PrefsPrefix + "windX", wind.x),
            EditorPrefs.GetFloat(PrefsPrefix + "windY", wind.y));
        motionWind = EditorPrefs.GetFloat(PrefsPrefix + "motionWind", motionWind);

        streakTint = LoadColor("streakTint", streakTint);
        splashTint = LoadColor("splashTint", splashTint);
    }

    private void SavePrefs()
    {
        EditorPrefs.SetFloat(PrefsPrefix + "lightIntensity", lightIntensity);
        EditorPrefs.SetFloat(PrefsPrefix + "heavyIntensity", heavyIntensity);
        EditorPrefs.SetFloat(PrefsPrefix + "rampStart", rampStart);
        EditorPrefs.SetFloat(PrefsPrefix + "rampEnd", rampEnd);
        EditorPrefs.SetFloat(PrefsPrefix + "rampEase", rampEase);
        EditorPrefs.SetFloat(PrefsPrefix + "intensitySmoothing", intensitySmoothing);

        EditorPrefs.SetInt(PrefsPrefix + "progressSourceIndex", progressSourceIndex);
        EditorPrefs.SetFloat(PrefsPrefix + "distanceRampMeters", distanceRampMeters);
        EditorPrefs.SetFloat(PrefsPrefix + "manualProgress", manualProgress);

        EditorPrefs.SetBool(PrefsPrefix + "farLayer", farLayer);
        EditorPrefs.SetFloat(PrefsPrefix + "farAreaX", farArea.x);
        EditorPrefs.SetFloat(PrefsPrefix + "farAreaY", farArea.y);
        EditorPrefs.SetFloat(PrefsPrefix + "farHeight", farHeight);
        EditorPrefs.SetFloat(PrefsPrefix + "farRate", farRate);
        EditorPrefs.SetFloat(PrefsPrefix + "farWidth", farWidth);
        EditorPrefs.SetFloat(PrefsPrefix + "farStreakAspect", farStreakAspect);
        EditorPrefs.SetFloat(PrefsPrefix + "farVelocityStretch", farVelocityStretch);

        EditorPrefs.SetBool(PrefsPrefix + "nearLayer", nearLayer);
        EditorPrefs.SetBool(PrefsPrefix + "splashLayer", splashLayer);
        EditorPrefs.SetFloat(PrefsPrefix + "splashRate", splashRate);

        EditorPrefs.SetFloat(PrefsPrefix + "fallSpeed", fallSpeed);
        EditorPrefs.SetFloat(PrefsPrefix + "windX", wind.x);
        EditorPrefs.SetFloat(PrefsPrefix + "windY", wind.y);
        EditorPrefs.SetFloat(PrefsPrefix + "motionWind", motionWind);

        SaveColor("streakTint", streakTint);
        SaveColor("splashTint", splashTint);
    }

    private static Color LoadColor(string key, Color fallback)
    {
        string packed = EditorPrefs.GetString(PrefsPrefix + key, string.Empty);
        if (string.IsNullOrEmpty(packed))
            return fallback;

        string[] parts = packed.Split('|');
        if (parts.Length != 4)
            return fallback;

        float r, g, b, a;
        if (float.TryParse(parts[0], out r) && float.TryParse(parts[1], out g) &&
            float.TryParse(parts[2], out b) && float.TryParse(parts[3], out a))
        {
            return new Color(r, g, b, a);
        }

        return fallback;
    }

    private static void SaveColor(string key, Color color)
    {
        EditorPrefs.SetString(PrefsPrefix + key,
            color.r.ToString("R") + "|" + color.g.ToString("R") + "|" +
            color.b.ToString("R") + "|" + color.a.ToString("R"));
    }
}
