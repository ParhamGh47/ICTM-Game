using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>How much of the picture the game is allowed to spend.</summary>
public enum GraphicsPreset
{
    Low = 0,
    Medium = 1,
    High = 2,
}

/// <summary>
/// The game's graphics settings: three presets, what each one changes, and where they are kept.
///
/// The project runs with everything turned up (a level can hold well over a hundred lights, and its terrain
/// draws grass, trees and full detail far out past the player), which is what makes a level look the way it
/// does - and what makes the frame time spike on anything but a fast machine. A frame that takes too long
/// shows up as a stagger in the truck rather than as a low average frame rate, which is why this exists: each
/// preset takes a slice off the expensive parts and leaves the picture recognisably the same one.
///
/// What each preset sets (see <see cref="Presets"/>, which is the one place to change any of it):
///
///  * global - per-pixel lights, shadows (kind, distance, cascades, resolution), anisotropy, anti-aliasing,
///    LOD bias, soft particles and vegetation, reflection probes, the particle raycast budget, and frame
///    pacing. Applied before the first scene loads, so nothing is ever drawn at the wrong setting.
///  * each level - the terrain's grass detail (both how dense it is and how far it is drawn), which is the
///    biggest per-frame cost while driving and is thinned rather than switched off, so the ground keeps its
///    colour and its tufts. The painted ground texture and the heightmap's accuracy come in with it on the
///    smallest preset. Trees are left alone at every preset: the levels already billboard them at 50 m and
///    cap how many are drawn in full, so there is nothing there worth taking away.
///  * the effects - the rain's particle count, the tire smoke, and the speed blur's taps. None of them is
///    ever switched off by a preset: the blur still blurs and the rain still rains, just with less work
///    behind it. <see cref="Shadows"/> and <see cref="MotionBlur"/> are the player's own switches on top.
///
/// The choice is kept in PlayerPrefs and applies to the whole game, so it survives restarts. High is the
/// default, so the game runs exactly as it was built until somebody picks something else, and whatever they
/// picked is what it keeps.
/// </summary>
public static class GraphicsQuality
{
    // ---------------------------------------------------------------- what a preset is

    /// <summary>Everything the presets change. Edit <see cref="Presets"/> to retune, nothing else.</summary>
    private struct Settings
    {
        public string name;

        // global
        public int pixelLights;
        public ShadowQuality shadows;
        public ShadowResolution shadowResolution;
        public float shadowDistance;
        public int shadowCascades;
        public AnisotropicFiltering anisotropic;
        public int antiAliasing;
        public float lodBias;
        public int maximumLODLevel;
        public bool softParticles;
        public bool softVegetation;
        public bool reflectionProbes;
        public int particleRaycastBudget;
        public int vSyncCount;
        public int targetFrameRate;

        // the terrain of every level
        public float detailDistance;
        public float detailDensity;
        public float basemapDistance;
        public float heightmapPixelError;

        // the effects: multiplied into whatever the component was authored with, so hand tuning still counts
        public float particleScale;         // rain and tire smoke
        public float blurSampleScale;       // the speed blur's taps
        public float blurLengthScale;       // and how long its streaks get
    }

    /// <summary>
    /// The presets. High is the game exactly as it was before any of this existed, so choosing it is the same
    /// as never having chosen anything, and the other two are High with slices taken off the expensive ends.
    /// </summary>
    private static readonly Settings[] Presets =
    {
        // Low - for a machine that cannot hold the frame rate. Shadows stay on (short, hard, one cascade) and
        // the blur still runs, because a racing game without either does not look like this game; what goes is
        // the distance. Grass is nearly gone, trees pull in close, and only one light per object is per-pixel.
        new Settings
        {
            name = "Low",
            pixelLights = 1,
            shadows = ShadowQuality.HardOnly,
            shadowResolution = ShadowResolution.Low,
            shadowDistance = 50f,        // still reaches the truck from the chase and the above camera alike
            shadowCascades = 0,          // one shadow map, which is all a 50 m distance needs
            anisotropic = AnisotropicFiltering.Disable,
            antiAliasing = 0,
            lodBias = 0.6f,
            maximumLODLevel = 1,
            softParticles = false,
            softVegetation = false,
            reflectionProbes = false,
            particleRaycastBudget = 64,
            vSyncCount = 0,
            targetFrameRate = 60,
            detailDistance = 30f,
            detailDensity = 0.35f,
            basemapDistance = 250f,
            heightmapPixelError = 10f,
            particleScale = 0.35f,
            blurSampleScale = 0.5f,
            blurLengthScale = 0.75f,
        },
        // Medium - the middle: the grass and trees keep their shape but close in, shadows gain cascades, and
        // the particles come back. This is where a machine that is fine at 60 most of the time should sit.
        new Settings
        {
            name = "Medium",
            pixelLights = 2,
            shadows = ShadowQuality.All,
            shadowResolution = ShadowResolution.Medium,
            shadowDistance = 90f,
            shadowCascades = 2,
            anisotropic = AnisotropicFiltering.Enable,
            antiAliasing = 2,
            lodBias = 1.5f,
            maximumLODLevel = 0,
            softParticles = true,
            softVegetation = true,
            reflectionProbes = true,
            particleRaycastBudget = 512,
            vSyncCount = 1,
            targetFrameRate = -1,
            detailDistance = 55f,
            detailDensity = 0.7f,
            basemapDistance = 500f,
            heightmapPixelError = 7f,
            particleScale = 0.65f,
            blurSampleScale = 0.85f,
            blurLengthScale = 0.9f,
        },
        // High - the project as authored, and the default. Every number here is what the scene and the quality
        // settings already say, so a game nobody has touched runs exactly like one from before any of this
        // existed.
        new Settings
        {
            name = "High",
            pixelLights = 4,
            shadows = ShadowQuality.All,
            shadowResolution = ShadowResolution.High,
            shadowDistance = 150f,
            shadowCascades = 4,
            anisotropic = AnisotropicFiltering.ForceEnable,   // as the project's own quality level has it
            antiAliasing = 2,
            lodBias = 5f,
            maximumLODLevel = 0,
            softParticles = true,
            softVegetation = true,
            reflectionProbes = true,
            particleRaycastBudget = 4096,
            vSyncCount = 1,
            targetFrameRate = -1,
            detailDistance = 80f,
            detailDensity = 1f,
            basemapDistance = 1000f,
            heightmapPixelError = 5f,
            particleScale = 1f,
            blurSampleScale = 1f,
            blurLengthScale = 1f,
        },
    };

    // ---------------------------------------------------------------- player prefs keys

    private const string PresetKey = "Graphics.Preset";          // 0 / 1 / 2, defaults to High
    private const string ShadowsKey = "Graphics.Shadows";        // 0 / 1, defaults on
    private const string MotionBlurKey = "Graphics.MotionBlur";  // 0 / 1, defaults on

    // ---------------------------------------------------------------- state

    /// <summary>Raised whenever the settings change, so anything showing them can refresh and anything
    /// already in a level can re-apply. Nothing is delivered on load; the values are applied before then.</summary>
    public static event Action Changed;

    /// <summary>The preset in force.</summary>
    public static GraphicsPreset Current { get; private set; } = GraphicsPreset.High;

    /// <summary>Whether anything casts shadows at all. On for every preset; this is the player's switch.</summary>
    public static bool Shadows { get; private set; } = true;

    /// <summary>Whether the speed blur runs. On for every preset; this is the player's switch.</summary>
    public static bool MotionBlur { get; private set; } = true;

    /// <summary>Rain and tire smoke are emitted at this share of their authored rate.</summary>
    public static float ParticleScale => Presets[(int)Current].particleScale;

    /// <summary>The speed blur keeps this share of its taps, and streaks this share of their length.</summary>
    public static float BlurSampleScale => Presets[(int)Current].blurSampleScale;

    public static float BlurLengthScale => Presets[(int)Current].blurLengthScale;

    /// <summary>The preset's own name, for anything that has to print it.</summary>
    public static string CurrentName => Presets[(int)Current].name;

    // ---------------------------------------------------------------- startup

    /// <summary>
    /// Reads what the player chose and puts it into effect before the first scene loads, so the very first
    /// frame is drawn at the right setting rather than being corrected a moment later.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Boot()
    {
        if (!Application.isPlaying) return;

        Load();
        ApplyGlobal();

        // Levels that load later get their terrain and their weather put straight, and one that is already
        // open - starting play from a level scene rather than from the menu - is handled here too.
        SceneManager.sceneLoaded += OnSceneLoaded;
        ApplyScene(SceneManager.GetActiveScene());
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyScene(scene);
    }

    // ---------------------------------------------------------------- the player's choice

    /// <summary>Picks a preset and remembers it.</summary>
    public static void Choose(GraphicsPreset preset)
    {
        Current = preset;

        Save();
        ApplyGlobal();
        ApplyScene(SceneManager.GetActiveScene());

        Changed?.Invoke();
    }

    /// <summary>Turns shadows on or off, on top of whatever the preset says.</summary>
    public static void SetShadows(bool on)
    {
        Shadows = on;

        Save();
        ApplyGlobal();
        ApplyScene(SceneManager.GetActiveScene());

        Changed?.Invoke();
    }

    /// <summary>Turns the speed blur on or off, on top of whatever the preset says.</summary>
    public static void SetMotionBlur(bool on)
    {
        MotionBlur = on;

        Save();

        // The blur is a component on the level's camera, so it is told about this rather than re-applied with
        // the rest: nothing else about the picture changes when it is switched off.
        SpeedMotionBlur.RefreshAll(SceneManager.GetActiveScene());

        Changed?.Invoke();
    }

    // ---------------------------------------------------------------- loading and saving

    private static void Load()
    {
        Current = Clamp(PlayerPrefs.GetInt(PresetKey, (int)GraphicsPreset.High));

        Shadows = PlayerPrefs.GetInt(ShadowsKey, 1) != 0;
        MotionBlur = PlayerPrefs.GetInt(MotionBlurKey, 1) != 0;
    }

    private static void Save()
    {
        PlayerPrefs.SetInt(PresetKey, (int)Current);
        PlayerPrefs.SetInt(ShadowsKey, Shadows ? 1 : 0);
        PlayerPrefs.SetInt(MotionBlurKey, MotionBlur ? 1 : 0);
        PlayerPrefs.Save();
    }

    private static GraphicsPreset Clamp(int value)
    {
        if (value < 0) return GraphicsPreset.Low;
        if (value > 2) return GraphicsPreset.High;
        return (GraphicsPreset)value;
    }

    /// <summary>Back to High and the toggles on. For a "restore defaults" button.</summary>
    public static void ResetToDefaults()
    {
        PlayerPrefs.DeleteKey(PresetKey);
        PlayerPrefs.DeleteKey(ShadowsKey);
        PlayerPrefs.DeleteKey(MotionBlurKey);
        PlayerPrefs.Save();

        Load();
        ApplyGlobal();
        ApplyScene(SceneManager.GetActiveScene());

        Changed?.Invoke();
    }

    // ---------------------------------------------------------------- applying

    /// <summary>
    /// Puts the global settings into Unity's quality settings. Safe to call at any time - it is what the
    /// options screen does as soon as a preset is picked.
    /// </summary>
    private static void ApplyGlobal()
    {
        Settings settings = Presets[(int)Current];

        QualitySettings.pixelLightCount = settings.pixelLights;

        // The lights themselves are never touched, only how many of them each object is lit by per pixel.
        // A level keeps every lamp, sign and street light it was built with at every preset.
        QualitySettings.shadows = Shadows ? settings.shadows : ShadowQuality.Disable;
        QualitySettings.shadowResolution = settings.shadowResolution;
        QualitySettings.shadowDistance = settings.shadowDistance;
        QualitySettings.shadowCascades = settings.shadowCascades;

        QualitySettings.anisotropicFiltering = settings.anisotropic;
        QualitySettings.antiAliasing = settings.antiAliasing;
        QualitySettings.lodBias = settings.lodBias;
        QualitySettings.maximumLODLevel = settings.maximumLODLevel;
        QualitySettings.softParticles = settings.softParticles;
        QualitySettings.softVegetation = settings.softVegetation;
        QualitySettings.realtimeReflectionProbes = settings.reflectionProbes;
        QualitySettings.particleRaycastBudget = settings.particleRaycastBudget;

        // Pacing. At High and Medium the display sets the rhythm, which is the smoothest thing to follow on a
        // machine that can keep up. At Low the machine cannot, and letting the frame rate float leaves the
        // truck moving at one rate on a good frame and another on a bad one - so it is capped where it can
        // still be held, and tearing is the price.
        QualitySettings.vSyncCount = settings.vSyncCount;
        Application.targetFrameRate = settings.targetFrameRate;
    }

    /// <summary>
    /// Puts the per-scene parts into effect: the terrain of a level, and a re-apply for anything in it that
    /// reads these settings for itself (the rain, the speed blur, the tire smoke).
    ///
    /// Terrains are found through the scene's root objects rather than with a scene-wide search, so this costs
    /// one walk per scene load and nothing per frame.
    /// </summary>
    private static void ApplyScene(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded) return;

        Settings settings = Presets[(int)Current];

        GameObject[] roots = scene.GetRootGameObjects();

        for (int i = 0; i < roots.Length; i++)
        {
            ApplyTerrain(roots[i], settings);
        }

        // The effects take their own share. They are asked rather than set from here, because each one knows
        // what it can afford to give up - the rain thins its drops, the blur keeps fewer taps - and both are
        // idempotent, so a scene that has already applied them costs nothing on the way in.
        RainSystem[] rain = UnityEngine.Object.FindObjectsOfType<RainSystem>();

        for (int i = 0; i < rain.Length; i++)
        {
            if (rain[i] != null && rain[i].gameObject.scene == scene) rain[i].ApplyQualityScale();
        }

        SpeedMotionBlur.RefreshAll(scene);
    }

    private static void ApplyTerrain(GameObject root, Settings settings)
    {
        Terrain[] terrains = root.GetComponentsInChildren<Terrain>(true);

        for (int i = 0; i < terrains.Length; i++)
        {
            Terrain terrain = terrains[i];
            if (terrain == null) continue;

            // Grass detail is the first thing to go: it is drawn in patches around the camera and costs most
            // while the truck is moving, which is exactly when a dropped frame is felt. Lowering the density
            // thins it rather than removing it, so the ground keeps its colour and its tufts.
            //
            // Each value is written only when it actually differs. Assigning one the terrain already has still
            // marks its rendering dirty, and a terrain that is rebuilt once the level is on screen draws what
            // it is made of - its trees above all - a moment late, which reads as them arriving after the level
            // has started. At High nothing here differs, so a level is left entirely untouched.
            if (!Mathf.Approximately(terrain.detailObjectDistance, settings.detailDistance))
                terrain.detailObjectDistance = settings.detailDistance;

            if (!Mathf.Approximately(terrain.detailObjectDensity, settings.detailDensity))
                terrain.detailObjectDensity = settings.detailDensity;

            // Trees are deliberately left exactly as the level paints them. They are already cheap here - the
            // levels switch a tree to a billboard at 50 m and never draw more than fifty as full meshes - so
            // there is nothing worth taking off them, and pulling their distance in would only thin out the
            // horizon the levels were built around.

            // The two terrain costs that are worth having, and only on the smallest preset: how far the painted
            // ground texture stays sharp (the whole terrain otherwise, which is most of what fills the screen),
            // and how closely the terrain mesh follows its heightmap.
            if (!Mathf.Approximately(terrain.basemapDistance, settings.basemapDistance))
                terrain.basemapDistance = settings.basemapDistance;

            if (!Mathf.Approximately(terrain.heightmapPixelError, settings.heightmapPixelError))
                terrain.heightmapPixelError = settings.heightmapPixelError;
        }
    }

    // Every effect takes this preset's share off its own numbers rather than being handed new ones, so the
    // value on the component in the scene stays the one to tune.

    /// <summary>A count - particles, taps - as this preset's share of what the component was authored with.</summary>
    public static int ScaleCount(int authored, float scale, int minimum)
    {
        return Mathf.Max(minimum, Mathf.RoundToInt(authored * scale));
    }

    /// <summary>The same for a rate, which is continuous rather than a count.</summary>
    public static float ScaleRate(float authored, float scale, float minimum)
    {
        return Mathf.Max(minimum, authored * scale);
    }
}
