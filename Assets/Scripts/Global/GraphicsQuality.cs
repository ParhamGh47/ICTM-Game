using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// How much of the picture the game is allowed to spend.
///
/// The numbers are the order the presets were added to the game, not the order they are offered in - Low,
/// Medium and High keep the values they have always had, so a choice saved before Potato and Ultra existed
/// still means what it meant. <see cref="GraphicsQuality.Ordered"/> is what the screens show.
/// </summary>
public enum GraphicsPreset
{
    Low = 0,
    Medium = 1,
    High = 2,
    Ultra = 3,
    Potato = 4,
}

/// <summary>
/// The game's graphics settings: five presets, what each one changes, and where they are kept.
///
/// The project runs with everything turned up: a level's terrain draws grass, trees and full detail far out
/// past the player, its roads are lined with hundreds of objects, and every car on them carries its own
/// lights - which is what makes a level look the way it does, and what makes the frame time spike on anything
/// but a fast machine. A frame that takes too long
/// shows up as a stagger in the truck rather than as a low average frame rate, which is why this exists: each
/// preset takes a slice off the expensive parts and leaves the picture recognisably the same one.
///
/// What each preset sets (see <see cref="Presets"/>, which is the one place to change any of it):
///
///  * global - per-pixel lights, shadows (kind, distance, cascades, resolution), anisotropy, anti-aliasing,
///    LOD bias, soft particles and vegetation, reflection probes, the particle raycast budget, and frame
///    pacing. Applied before the first scene loads, so nothing is ever drawn at the wrong setting.
///  * each level - the terrain: its grass detail (both how dense it is and how far it is drawn, which is the
///    biggest per-frame cost while driving, thinned rather than switched off so the ground keeps its colour
///    and its tufts), its trees (how far out they are drawn, when they become billboards, and how many are
///    drawn as full meshes), the painted ground texture, and the heightmap's accuracy. The trees are the one
///    set here that takes effect on the next frame, so they never need a level reload; the grass and the
///    ground do, which is what the restart prompt inside a level is about.
///  * the effects - the rain's particle count, the tire smoke, and the speed blur's taps. None of them is
///    ever switched off by a preset: the blur still blurs and the rain still rains, just with less work
///    behind it. <see cref="Shadows"/> and <see cref="MotionBlur"/> are the player's own switches on top.
///
/// The five of them run from <b>Potato</b> - every world texture at quarter resolution, shadows only around the
/// truck, grass nearly gone, the trees fading out 1.2 km away and the ground itself drawn coarse - through
/// Low, Medium and High, to <b>Ultra</b>, which spends more than the project itself was authored with: eight
/// lights per object, 4x anti-aliasing, shadows half again as far out at 4k, trees solid to 85 m, grass to
/// 120 m and the terrain mesh at its sharpest. Nothing that carries the game is ever switched off at any of
/// them: the truck still lights the road, the blur still streaks, and the rain still falls - the presets move
/// the distance, the density and the resolution of what is drawn, never whether it is.
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
        public int textureLimit;            // mipmap levels taken off every texture: 0 full, 1 half, 2 quarter
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
        public float treeDistance;
        public float treeBillboardDistance;
        public int treeFullLODCount;

        // the effects: multiplied into whatever the component was authored with, so hand tuning still counts
        public float particleScale;         // rain and tire smoke
        public float blurSampleScale;       // the speed blur's taps
        public float blurLengthScale;       // and how long its streaks get
    }

    /// <summary>
    /// The presets, in the enum's own value order - this array is indexed by the choice, so it cannot be in the
    /// order the screens show them in (that is <see cref="Ordered"/>). High is the game exactly as it was
    /// before any of this existed, so choosing it is the same as never having chosen anything; Low and Medium
    /// are High with slices taken off its expensive ends, Potato takes more off again, and Ultra spends more
    /// than the project itself was authored with.
    /// </summary>
    private static readonly Settings[] Presets =
    {
        // Low - for a machine that cannot hold the frame rate. Shadows stay on (short, hard, one cascade) and
        // the blur still runs, because a racing game without either does not look like this game; what goes is
        // the distance. Grass is nearly gone, trees pull in close, and only one light per object is per-pixel.
        // Potato, below it, is this with the distances cut again and the ground itself drawn coarser.
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
            textureLimit = 0,
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
            treeDistance = 2500f,
            treeBillboardDistance = 40f,
            treeFullLODCount = 25,
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
            textureLimit = 0,
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
            treeDistance = 4000f,
            treeBillboardDistance = 50f,
            treeFullLODCount = 50,
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
            textureLimit = 0,                                 // as it has it too: full resolution textures
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
            treeDistance = 5000f,      // every one of them the levels authored
            treeBillboardDistance = 50f,
            treeFullLODCount = 50,
            particleScale = 1f,
            blurSampleScale = 1f,
            blurLengthScale = 1f,
        },
        // Ultra - for a machine with room to spare, and the one preset that spends more than the game was
        // authored with. The things it buys are the ones that show while driving: twice as many lights are lit
        // per object, which is headlights on tarmac - every car on the road carries its own, switched on as it
        // drives, and the levels with a lot of them are the ones this tells on; trees stay solid to 85 m
        // instead of popping to a billboard at 50 and nearly twice as many are drawn in full, which is the tree
        // lines down both sides of the road; grass reaches 120 m instead of 80; the painted ground texture
        // stays sharp twice as far out; the terrain mesh follows its heightmap half again as closely, which is
        // what a hill's silhouette is made of; shadows reach 250 m at 4k; and the rain, the smoke and the speed
        // blur all get more than the scene says.
        new Settings
        {
            name = "Ultra",
            pixelLights = 8,
            shadows = ShadowQuality.All,
            shadowResolution = ShadowResolution.VeryHigh,
            shadowDistance = 250f,
            shadowCascades = 4,
            anisotropic = AnisotropicFiltering.ForceEnable,
            antiAliasing = 4,
            textureLimit = 0,
            lodBias = 8f,
            maximumLODLevel = 0,
            softParticles = true,
            softVegetation = true,
            reflectionProbes = true,
            particleRaycastBudget = 8192,
            vSyncCount = 1,
            targetFrameRate = -1,
            detailDistance = 120f,
            detailDensity = 1f,
            basemapDistance = 1500f,
            heightmapPixelError = 3f,
            treeDistance = 5000f,      // already every tree the level holds, so the gain is in the two below
            treeBillboardDistance = 85f,
            treeFullLODCount = 90,
            particleScale = 1.3f,
            blurSampleScale = 1.3f,
            blurLengthScale = 1.15f,
        },
        // Potato - for a machine that cannot hold even Low. Everything that carries the game is still there:
        // the shadows still fall (25 m of them, hard, at the lowest resolution, so the truck is still planted
        // on the road), the truck still lights it, the rain still falls and the blur still streaks. What has
        // gone is the resolution and the distance: every world texture is a quarter of the pixels it was, the
        // ground texture goes to its painted base map at 150 m, shadows and grass close in to around the
        // truck, the trees stop 1.2 km out and become billboards at 30 m, the terrain mesh is drawn at three
        // times the error (which is the hills' own shape, not anything added to it), and the rain, the smoke
        // and the blur all run at about a third of what Low asks. Every one of them is a number taken off a
        // count, a distance or a mip level; nothing is switched off, so a level still looks like itself, only
        // coarser and further away.
        new Settings
        {
            name = "Potato",
            pixelLights = 1,
            shadows = ShadowQuality.HardOnly,
            shadowResolution = ShadowResolution.Low,
            shadowDistance = 25f,
            shadowCascades = 0,
            anisotropic = AnisotropicFiltering.Disable,
            antiAliasing = 0,
            textureLimit = 1,                                 // every world texture at half size
            lodBias = 0.35f,
            maximumLODLevel = 1,
            softParticles = false,
            softVegetation = false,
            reflectionProbes = false,
            particleRaycastBudget = 16,
            vSyncCount = 0,
            targetFrameRate = 60,
            detailDistance = 12f,
            detailDensity = 0.12f,
            basemapDistance = 150f,
            heightmapPixelError = 15f,
            treeDistance = 1200f,
            treeBillboardDistance = 30f,
            treeFullLODCount = 12,
            particleScale = 0.15f,
            blurSampleScale = 0.3f,
            blurLengthScale = 0.6f,
        },
    };

    /// <summary>
    /// The presets in the order they are offered, fewest effects first. The screens build their row from this
    /// rather than from the enum's values, which are only the order the presets were added to the game.
    /// </summary>
    public static readonly GraphicsPreset[] Ordered =
    {
        GraphicsPreset.Potato,
        GraphicsPreset.Low,
        GraphicsPreset.Medium,
        GraphicsPreset.High,
        GraphicsPreset.Ultra,
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

    /// <summary>
    /// A value read from PlayerPrefs, as a preset. Anything outside the enum - an old save, a hand-edited
    /// preference, a version of the game this one does not know about - is High, which is the game as built.
    /// </summary>
    private static GraphicsPreset Clamp(int value)
    {
        if (value < (int)GraphicsPreset.Low) return GraphicsPreset.High;
        if (value > (int)GraphicsPreset.Potato) return GraphicsPreset.High;
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

        // Every world texture at full, half or a quarter of its size. This is the one lever here that costs
        // nothing to draw and changes what is plainly on screen - the road surface, the buildings, the ground -
        // so it is what Potato leans on hardest; every other preset writes the full-resolution 0 back, or the
        // choice would stick to the machine after the setting is moved up again. (The project's own 'Very Low'
        // quality level does the same, and the menus are not drawn from mipmapped textures, so their text and
        // their pictures stay sharp at every preset.)
        QualitySettings.masterTextureLimit = settings.textureLimit;

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

    /// <summary>
    /// Whether the level that is open now would look different if it were loaded again.
    ///
    /// Everything a preset changes about the picture - the lights, the shadows, the anti-aliasing, the rain,
    /// the blur - is put into effect the moment it is chosen. The one thing that cannot be is a level's ground
    /// detail: the terrain builds it as the level loads and has no reason to build it again, so its share of a
    /// preset waits for the next load. That is a real, visible difference - grass at 30 m against grass at
    /// 80 m - so a screen that changes the setting while a level is open needs to be able to say so, and offer
    /// the restart that applies it (see <see cref="PauseOptionsPanel"/>).
    ///
    /// Answered by looking at the terrain itself rather than by remembering that something changed, so it is
    /// true exactly while the level really is drawn with the wrong amount of detail - including after the
    /// player changes their mind and picks the preset the level was already using, which needs no restart.
    /// </summary>
    public static bool ActiveSceneNeedsReload()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded) return false;

        Settings settings = Presets[(int)Current];
        GameObject[] roots = scene.GetRootGameObjects();

        for (int i = 0; i < roots.Length; i++)
        {
            Terrain[] terrains = roots[i].GetComponentsInChildren<Terrain>(true);

            for (int t = 0; t < terrains.Length; t++)
                if (TerrainDiffers(terrains[t], settings)) return true;
        }

        return false;
    }

    /// <summary>Whether a terrain draws something other than what this preset asks of it.</summary>
    private static bool TerrainDiffers(Terrain terrain, Settings settings)
    {
        if (terrain == null) return false;

        return !Mathf.Approximately(terrain.detailObjectDistance, settings.detailDistance)
            || !Mathf.Approximately(terrain.detailObjectDensity, settings.detailDensity)
            || !Mathf.Approximately(terrain.basemapDistance, settings.basemapDistance)
            || !Mathf.Approximately(terrain.heightmapPixelError, settings.heightmapPixelError);
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

            // Trees, which are what the levels are mostly made of. Every level paints its own onto the terrain
            // and leaves all three of these at Unity's own defaults - five kilometres of them, billboards from
            // 50 m out, at most fifty drawn as full meshes at once - so there is real room at both ends: the two
            // small presets bring the horizon in and let more of what is left be a billboard, and Ultra keeps a
            // tree a solid tree to 85 m and draws nearly twice as many of them in full, which is what the tree
            // lines down both sides of the road are made of. None of them is ever off: a level keeps every tree
            // its terrain holds at every preset.
            //
            // Changing these three only re-culls the next frame, so unlike the grass they need no reload.
            if (!Mathf.Approximately(terrain.treeDistance, settings.treeDistance))
                terrain.treeDistance = settings.treeDistance;

            if (!Mathf.Approximately(terrain.treeBillboardDistance, settings.treeBillboardDistance))
                terrain.treeBillboardDistance = settings.treeBillboardDistance;

            if (terrain.treeMaximumFullLODCount != settings.treeFullLODCount)
                terrain.treeMaximumFullLODCount = settings.treeFullLODCount;

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
