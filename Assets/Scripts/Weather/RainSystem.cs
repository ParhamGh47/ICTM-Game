using UnityEngine;

/// <summary>
/// Progress driven rain for a level: it starts as a light drizzle and builds up
/// to a heavy downpour by the time the player is half-way down the road.
///
/// The system is made of three particle systems that <see cref="Rebuild"/> creates
/// as children of this object:
///  - "Rain Far"  : a huge, thin sheet of stretched rain particles riding with the
///                  player, so the whole view stays filled.
///  - "Rain Near" : a much smaller, thicker and faster copy right around the car,
///                  which gives the rain some depth.
///  - "Rain Splashes" : small impacts on the ground under the player.
///
/// Install the object with  Tools > Road Tools > Paint Rain , which also creates
/// the rain textures/materials as real assets. Rates, sizes, wind, colours and the
/// intensity profile are applied live every frame, so they can be tweaked on this
/// component while the level plays. Anything that changes the emitter volume (the
/// layer toggles, areas, heights, width/aspect and materials) needs
/// "Rebuild Particle Systems" from the component's context menu.
///
/// Intensity: progress 0 gives <see cref="lightIntensity"/>, then it eases up
/// between <see cref="rampStart"/> and <see cref="rampEnd"/> of the level (20% to
/// 50% by default) and stays at <see cref="heavyIntensity"/> from there on.
/// Progress is read from the level's own progress source, so the rain always
/// matches the percentage the checkpoints show on screen.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Weather/Rain System")]
public class RainSystem : MonoBehaviour
{
    public enum ProgressSource
    {
        Auto,
        ProgressDisplay,
        Checkpoints,
        DistanceTravelled,
        Manual
    }

    [Header("Intensity profile (light at the start, heavy by half-way)")]
    [Tooltip("How hard it rains at the very start of the level (1 = the heavy setting).")]
    [Range(0f, 1f)] public float lightIntensity = 0.15f;

    [Tooltip("How hard it rains once the ramp is over.")]
    [Range(0f, 1f)] public float heavyIntensity = 1f;

    [Tooltip("Level progress (0-1) where the rain starts building up. 0.2 = 20%.")]
    [Range(0f, 1f)] public float rampStart = 0.2f;

    [Tooltip("Level progress (0-1) where the rain reaches its heaviest. 0.5 = half-way.")]
    [Range(0f, 1f)] public float rampEnd = 0.5f;

    [Tooltip("Shapes the ramp: 1 is a straight line, above 1 stays light for longer and then climbs quickly.")]
    [Range(0.25f, 4f)] public float rampEase = 1.2f;

    [Tooltip("How quickly the rain reacts to progress changes (per second).")]
    [Range(0.2f, 12f)] public float intensitySmoothing = 3.5f;

    [Header("Where the progress comes from")]
    public ProgressSource progressSource = ProgressSource.Auto;

    [Tooltip("Only used when the level has no progress display or compass: how many metres the rain ramps over.")]
    public float distanceRampMeters = 1500f;

    [Tooltip("Progress used when Progress Source is set to Manual (editor preview / testing).")]
    [Range(0f, 1f)] public float manualProgress;

    [Header("Follows the player")]
    [Tooltip("Anchor of the rain volume. Left empty it uses the player's car, then the main camera.")]
    public Transform followTarget;

    [Tooltip("Layers searched for the ground under the player (used by the splashes).")]
    public LayerMask groundMask = ~0;

    [Tooltip("How far down to look for the ground under the car.")]
    public float groundRaycastDistance = 80f;

    [Tooltip("How often the ground under the car is re-sampled (seconds).")]
    public float groundRefreshInterval = 0.25f;

    [Header("Rain far layer (fills the whole view)")]
    public bool farLayer = true;

    [Tooltip("Height of the emitter sheet above the player. Rain falls from here.")]
    public float farHeight = 22f;

    [Tooltip("Width / depth of the emitting sheet in metres.")]
    public Vector2 farArea = new Vector2(120f, 120f);

    [Tooltip("Particles per second at full intensity.")]
    public float farRate = 9000f;

    public int farMaxParticles = 20000;

    [Tooltip("Width of one streak in metres (its length comes from the aspect and speed).")]
    public float farWidth = 0.05f;

    [Tooltip("Length of a streak compared to its width, before velocity stretching.")]
    public float farStreakAspect = 10f;

    [Tooltip("Extra stretching per metre/second of speed.")]
    public float farVelocityStretch = 0.05f;

    [Header("Rain near layer (thicker streaks around the car)")]
    public bool nearLayer = true;

    public float nearHeight = 13f;

    [Range(0.05f, 1f)] public float nearAreaScale = 0.25f;

    [Range(0.05f, 1f)] public float nearRateScale = 0.25f;

    [Range(1f, 4f)] public float nearWidthScale = 1.8f;

    [Range(0.5f, 3f)] public float nearStreakScale = 1.4f;

    [Header("Speed and wind")]
    [Tooltip("How fast the drops fall, in metres per second.")]
    public float fallSpeed = 30f;

    [Tooltip("Random +/- spread on the fall speed, so the rain is not perfectly uniform.")]
    [Range(0f, 0.5f)] public float fallSpeedVariation = 0.18f;

    [Tooltip("Constant sideways drift of the rain (x / z).")]
    public Vector2 wind = new Vector2(2.5f, 1.5f);

    [Tooltip("How much the player's own speed slants the rain, so it looks like it rushes past.")]
    [Range(0f, 1f)] public float motionWind = 0.35f;

    [Tooltip("Clamp on the wind taken from the player's speed (metres per second).")]
    public float maxMotionWind = 14f;

    [Header("Splashes on the ground")]
    public bool splashLayer = true;

    public Vector2 splashArea = new Vector2(40f, 40f);

    [Tooltip("Impacts per second at full intensity.")]
    public float splashRate = 2000f;

    public int splashMaxParticles = 3000;

    public float splashSize = 0.28f;

    [Tooltip("Height of the splash sheet above the ground.")]
    public float splashHeight = 0.03f;

    [Header("Look")]
    public Color streakTint = new Color(0.78f, 0.83f, 0.92f, 0.30f);

    public Color splashTint = new Color(0.85f, 0.90f, 0.98f, 0.35f);

    [Tooltip("Created by the Rain Painter. Left empty the layer keeps Unity's default particle material.")]
    public Material streakMaterial;

    public Material splashMaterial;

    [Header("Rain ambience")]
    [Tooltip("The looping bed of rain sound that goes with the falling drops. Left empty, one is made as a " +
             "child of this object, so a level only has to have rain for it to be heard.")]
    public AudioSource ambience;

    [Tooltip("The loop to play. Left empty it is looked for in Resources: Assets/Resources/Audio/rain-light, and " +
             "failing that one is made (see RainAmbienceClip) - so rain is never silent just because nobody " +
             "wired a clip to it.")]
    public AudioClip ambienceClip;

    [Tooltip("Make a light-rain loop when there is no clip to play, so a level that has just been given rain " +
             "has rain sound with it. Off means the drops are silent until a clip is assigned.")]
    public bool generateAmbienceIfMissing = true;

    [Tooltip("Volume of the bed at full intensity, before the player's own ENVIRONMENT setting. The rain is a " +
             "bed under everything else rather than a thing of its own, so this sits under the engine even at " +
             "full strength.")]
    [Range(0f, 1f)] public float ambienceVolume = 0.5f;

    [Tooltip("How much of that the lightest rain is worth. Rain that can be heard raging on screen and not at " +
             "all in the mix would read as broken, so a drizzle keeps a share of it.")]
    [Range(0f, 1f)] public float lightRainAmbience = 0.4f;

    [Header("Built systems (created by Rebuild)")]
    public ParticleSystem streaksFar;

    public ParticleSystem streaksNear;

    public ParticleSystem splashes;

    [Header("State")]
    [Tooltip("The rain strength that is currently applied (0-1).")]
    [Range(0f, 1f)] public float currentIntensity;

    // ------------------------------------------------------------------
    //  private state
    // ------------------------------------------------------------------

    private const string FarLayerName = "Rain Far";
    private const string NearLayerName = "Rain Near";
    private const string SplashLayerName = "Rain Splashes";

    // Where a level's rain loop is looked for when the component has none of its own. A level that wants its
    // own sound assigns Ambience Clip instead; this is so rain is never silent just because nobody wired it.
    private const string AmbienceResourcePath = "Audio/rain-light";

    private readonly RaycastHit[] groundHits = new RaycastHit[16];

    // The rain as it was authored, before any preset took its share - kept so a change of preset scales from
    // the original rather than from the last reduced value.
    private bool qualityApplied;
    private int authoredFarMaxParticles;
    private float authoredFarRate;
    private int authoredSplashMaxParticles;
    private float authoredSplashRate;

    private float intensity;
    private float splashGroundY = float.NaN;
    private float nextGroundSample;
    private float nextProgressSearch;
    private bool haveStartPosition;
    private bool haveLastTargetPosition;
    private Vector3 startPosition;
    private Vector3 lastTargetPosition;
    private Vector3 targetVelocity;
    private Transform playerRoot;
    private ProgressDisplay progressDisplay;
    private CheckpointIndicator compass;

    /// <summary>Current rain strength, 0 = off, 1 = the heavy setting.</summary>
    public float Intensity
    {
        get { return intensity; }
    }

    // ------------------------------------------------------------------
    //  Unity messages
    // ------------------------------------------------------------------

    private void Awake()
    {
        RefreshProgressReferences();

        if (streaksFar == null && streaksNear == null && splashes == null)
            Rebuild();

        ApplyQualityScale();
        BuildAmbience();

        intensity = EvaluateIntensity(EvaluateProgress());
        currentIntensity = intensity;

        FollowTarget();
        ApplyIntensity(intensity);
    }

    private void Update()
    {
        float target = EvaluateIntensity(EvaluateProgress());

        // Smooth, so the 5%-at-a-time checkpoint steps still feel like a gradual build up.
        float blend = 1f - Mathf.Exp(-intensitySmoothing * Mathf.Max(Time.deltaTime, 0.0001f));
        intensity = Mathf.Lerp(intensity, target, blend);

        FollowTarget();
        RefreshSplashGround();
        ApplyIntensity(intensity);
    }

    private void OnValidate()
    {
        if (rampEnd < rampStart)
            rampEnd = rampStart;
        if (rampStart > rampEnd)
            rampStart = rampEnd;
    }

    // ------------------------------------------------------------------
    //  Building the particle systems
    // ------------------------------------------------------------------

    /// <summary>
    /// (Re)creates the three particle systems from the values on this component.
    /// Existing children of the same name are removed first.
    /// </summary>
    [ContextMenu("Rebuild Particle Systems")]
    public void Rebuild()
    {
        DestroyBuiltSystems();

        if (farLayer)
        {
            streaksFar = BuildStreakLayer(FarLayerName, farHeight, farArea, farWidth,
                farStreakAspect, farVelocityStretch);
        }

        if (nearLayer)
        {
            streaksNear = BuildStreakLayer(NearLayerName, nearHeight, farArea * nearAreaScale,
                farWidth * nearWidthScale, farStreakAspect * nearStreakScale,
                farVelocityStretch * nearStreakScale);
        }

        if (splashLayer)
            splashes = BuildSplashLayer();

        // Keep whatever strength is already showing, otherwise take it from the
        // level progress so an edit time rebuild looks right straight away.
        ApplyIntensity(intensity > 0f ? intensity : EvaluateIntensity(EvaluateProgress()));
    }

    private ParticleSystem BuildStreakLayer(string layerName, float height, Vector2 area, float width,
        float aspect, float velocityStretch)
    {
        ParticleSystem system = CreateLayer(layerName, new Vector3(0f, height, 0f));

        ParticleSystem.MainModule main = system.main;
        main.loop = true;
        main.prewarm = true;
        main.playOnAwake = true;
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(width * 0.7f, width * 1.3f);
        main.startColor = new ParticleSystem.MinMaxGradient(streakTint, WithAlpha(streakTint, streakTint.a * 0.65f));
        main.gravityModifier = 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
        main.maxParticles = farMaxParticles;

        // The drops fall this far in their lifetime, so give them just enough of it.
        float life = Mathf.Max(0.35f, height / Mathf.Max(1f, fallSpeed));
        main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.9f, life * 1.6f);

        ParticleSystem.EmissionModule emission = system.emission;
        emission.enabled = true;
        emission.rateOverTime = new ParticleSystem.MinMaxCurve(0f);

        ParticleSystem.ShapeModule shape = system.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(Mathf.Max(1f, area.x), 1f, Mathf.Max(1f, area.y));
        shape.position = Vector3.zero;
        shape.rotation = Vector3.zero;

        ParticleSystem.VelocityOverLifetimeModule velocity = system.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;

        ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
        {
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.lengthScale = aspect;
            renderer.velocityScale = velocityStretch;
            renderer.cameraVelocityScale = 0f;
            renderer.sortMode = ParticleSystemSortMode.Distance;
            renderer.maxParticleSize = 1.5f;
            ApplyMaterial(renderer, streakMaterial);
        }

        return system;
    }

    private ParticleSystem BuildSplashLayer()
    {
        ParticleSystem system = CreateLayer(SplashLayerName, Vector3.zero);

        ParticleSystem.MainModule main = system.main;
        main.loop = true;
        main.prewarm = true;
        main.playOnAwake = true;
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(splashSize * 0.6f, splashSize * 1.4f);
        main.startColor = new ParticleSystem.MinMaxGradient(splashTint, WithAlpha(splashTint, splashTint.a * 0.5f));
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.12f, 0.35f);
        main.gravityModifier = 0.35f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
        main.maxParticles = splashMaxParticles;

        ParticleSystem.EmissionModule emission = system.emission;
        emission.enabled = true;
        emission.rateOverTime = new ParticleSystem.MinMaxCurve(0f);

        ParticleSystem.ShapeModule shape = system.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(Mathf.Max(1f, splashArea.x), 0.05f, Mathf.Max(1f, splashArea.y));
        shape.position = Vector3.zero;
        shape.rotation = Vector3.zero;

        ParticleSystem.VelocityOverLifetimeModule velocity = system.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = RandomBetween(-0.35f, 0.35f);
        velocity.z = RandomBetween(-0.35f, 0.35f);
        velocity.y = RandomBetween(0.5f, 1.7f);

        ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
        {
            renderer.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            renderer.sortMode = ParticleSystemSortMode.Distance;
            renderer.maxParticleSize = 3f;
            ApplyMaterial(renderer, splashMaterial);
        }

        return system;
    }

    private ParticleSystem CreateLayer(string layerName, Vector3 localPosition)
    {
        Transform existing = transform.Find(layerName);
        if (existing != null)
            DestroyObject(existing.gameObject);

        GameObject go = new GameObject(layerName);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = localPosition;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;

        ParticleSystem system = go.AddComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        return system;
    }

    private static void ApplyMaterial(ParticleSystemRenderer renderer, Material material)
    {
        // No material means the painter was not used; Unity's default particle
        // material is used instead, which still reads as rain.
        if (material == null)
            return;

        renderer.material = material;
        renderer.trailMaterial = material;
    }

    private void DestroyBuiltSystems()
    {
        if (streaksFar != null) DestroyObject(streaksFar.gameObject);
        if (streaksNear != null) DestroyObject(streaksNear.gameObject);
        if (splashes != null) DestroyObject(splashes.gameObject);

        streaksFar = null;
        streaksNear = null;
        splashes = null;
    }

    private static void DestroyObject(GameObject go)
    {
        if (go == null)
            return;

        if (Application.isPlaying)
            Destroy(go);
        else
            DestroyImmediate(go);
    }

    // ------------------------------------------------------------------
    //  Runtime intensity
    // ------------------------------------------------------------------

    /// <summary>Applies a rain strength between 0 (off) and 1 (heavy).</summary>
    public void ApplyIntensity(float value)
    {
        intensity = Mathf.Clamp01(value);
        currentIntensity = intensity;

        Vector2 windVector = wind + MotionWind();

        if (streaksFar != null)
        {
            ApplyStreakLayer(streaksFar, intensity, farRate, farMaxParticles, farWidth, windVector, 1f);
        }

        if (streaksNear != null)
        {
            ApplyStreakLayer(streaksNear, intensity, farRate * nearRateScale,
                Mathf.RoundToInt(farMaxParticles * nearRateScale), farWidth * nearWidthScale,
                windVector, 1.15f);
        }

        if (splashes != null)
            ApplySplashLayer(splashes, intensity);

        ApplyAmbience(intensity);
    }

    // ------------------------------------------------------------------
    //  Rain ambience
    // ------------------------------------------------------------------

    /// <summary>
    /// Makes the sound that goes with the falling drops.
    ///
    /// The bed is 2D on purpose: rain is all around the player rather than somewhere out on the road, and a
    /// positional loop would swing around the cab as the truck turns. Its volume is worked out here, from the
    /// rain's own strength and the player's ENVIRONMENT setting, so it is one of the sounds the bus must leave
    /// alone (see <see cref="SoundBus.MarkHandled"/>).
    ///
    /// The clip is the component's own if it has one, and otherwise whatever is at
    /// Assets/Resources/Audio/rain-light - so a level with rain has rain sound without anyone wiring it, and a
    /// level that wants something else can point Ambience Clip somewhere else. With neither, a loop is made
    /// (see <see cref="RainAmbienceClip"/>), which is what Core-4's light rain is heard through.
    /// </summary>
    private void BuildAmbience()
    {
        if (ambience == null)
        {
            GameObject go = new GameObject("Rain Ambience");
            go.transform.SetParent(transform, false);

            ambience = go.AddComponent<AudioSource>();
        }

        ambience.playOnAwake = false;
        ambience.loop = true;
        ambience.spatialBlend = 0f;
        ambience.dopplerLevel = 0f;

        if (ambience.clip == null)
            ambience.clip = ambienceClip != null ? ambienceClip : Resources.Load<AudioClip>(AmbienceResourcePath);

        // Nothing assigned and nothing in Resources: make one, rather than let a level rain in silence.
        if (ambience.clip == null && generateAmbienceIfMissing)
            ambience.clip = RainAmbienceClip.Shared;

        SoundBus.MarkHandled(ambience);

        if (ambience.clip != null && !ambience.isPlaying)
            ambience.Play();
    }

    /// <summary>
    /// Sets the bed's volume from how hard it is raining.
    ///
    /// The strength is measured against the heaviest rain this level has rather than against 1, because a
    /// level's rain is authored on a scale of its own: Core-4's is deliberately light and only ever reaches
    /// 0.2, so reading the raw number would leave its rain barely audible however heavy it looked on screen.
    ///
    /// The lightest rain keeps a share of the volume rather than fading to nothing, because drops that can be
    /// seen falling and cannot be heard at all read as a bug rather than as a light shower.
    /// </summary>
    private void ApplyAmbience(float value)
    {
        if (ambience == null) return;

        float heaviest = Mathf.Max(0.001f, heavyIntensity);
        float strength = Mathf.Lerp(lightRainAmbience, 1f, Mathf.Clamp01(value / heaviest));

        ambience.volume = ambienceVolume * strength * SoundSettings.Volume(SoundChannel.Environment);
    }

    /// <summary>Editor preview / debugging: forces the given strength until the next rebuild.</summary>
    public void PreviewIntensity(float value)
    {
        ApplyIntensity(value);
    }

    /// <summary>
    /// Takes the graphics preset's share off the rain.
    ///
    /// Rain is the heaviest thing this game draws - the far layer alone is built for twenty thousand drops, and
    /// they are all stretched billboards over the whole screen - so it is the first place a smaller machine
    /// needs help. What it does here is thin the drops and slow them down per second, not stop the rain: the
    /// streaks keep their size, their colour and their fall, so a rainy level still looks like a rainy level at
    /// every preset, with less work behind it.
    ///
    /// The values on the component are the ones to tune - this only scales them, and only once, so calling it
    /// again (a preset change, a scene load) never compounds the reduction.
    /// </summary>
    public void ApplyQualityScale()
    {
        if (!qualityApplied)
        {
            authoredFarMaxParticles = farMaxParticles;
            authoredFarRate = farRate;
            authoredSplashMaxParticles = splashMaxParticles;
            authoredSplashRate = splashRate;

            qualityApplied = true;
        }

        float scale = GraphicsQuality.ParticleScale;

        farMaxParticles = GraphicsQuality.ScaleCount(authoredFarMaxParticles, scale, 300);
        farRate = GraphicsQuality.ScaleRate(authoredFarRate, scale, 150f);
        splashMaxParticles = GraphicsQuality.ScaleCount(authoredSplashMaxParticles, scale, 150);
        splashRate = GraphicsQuality.ScaleRate(authoredSplashRate, scale, 150f);

        // The layers are updated from these fields every frame, so re-applying the strength is what moves the
        // live systems onto the new numbers - no rebuild, and therefore no visible break in the weather.
        ApplyIntensity(intensity);
    }

    private void ApplyStreakLayer(ParticleSystem system, float amount, float rate, int maxParticles,
        float width, Vector2 windVector, float tintScale)
    {
        float speedScale = Mathf.Lerp(0.85f, 1.15f, amount);
        float speed = fallSpeed * speedScale;
        float alpha = Mathf.Clamp01(streakTint.a * tintScale * Mathf.Lerp(0.7f, 1f, amount));

        ParticleSystem.MainModule main = system.main;
        main.maxParticles = Mathf.Max(50, Mathf.RoundToInt(maxParticles * Mathf.Lerp(0.3f, 1f, amount)));
        main.startSize = new ParticleSystem.MinMaxCurve(width * 0.7f, width * 1.3f);
        main.startColor = new ParticleSystem.MinMaxGradient(WithAlpha(streakTint, alpha),
            WithAlpha(streakTint, alpha * 0.6f));

        ParticleSystem.EmissionModule emission = system.emission;
        emission.rateOverTime = new ParticleSystem.MinMaxCurve(rate * amount);

        float slow = speed * (1f - fallSpeedVariation);
        float fast = speed * (1f + fallSpeedVariation);

        ParticleSystem.VelocityOverLifetimeModule velocity = system.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.x = RandomBetween(windVector.x * 0.7f, windVector.x * 1.3f);
        velocity.z = RandomBetween(windVector.y * 0.7f, windVector.y * 1.3f);
        velocity.y = RandomBetween(-fast, -slow);
    }

    private void ApplySplashLayer(ParticleSystem system, float amount)
    {
        float alpha = Mathf.Clamp01(splashTint.a * Mathf.Lerp(0.6f, 1f, amount));

        ParticleSystem.MainModule main = system.main;
        main.maxParticles = Mathf.Max(20, Mathf.RoundToInt(splashMaxParticles * Mathf.Lerp(0.25f, 1f, amount)));
        main.startColor = new ParticleSystem.MinMaxGradient(WithAlpha(splashTint, alpha),
            WithAlpha(splashTint, alpha * 0.5f));

        ParticleSystem.EmissionModule emission = system.emission;
        emission.rateOverTime = new ParticleSystem.MinMaxCurve(splashRate * amount * amount);
    }

    private Vector2 MotionWind()
    {
        if (motionWind <= 0f)
            return Vector2.zero;

        Vector2 drift = new Vector2(-targetVelocity.x, -targetVelocity.z) * motionWind;
        if (drift.magnitude > maxMotionWind)
            drift = drift.normalized * maxMotionWind;

        return drift;
    }

    // ------------------------------------------------------------------
    //  Progress
    // ------------------------------------------------------------------

    /// <summary>How far through the level the player is, 0 to 1.</summary>
    public float EvaluateProgress()
    {
        switch (progressSource)
        {
            case ProgressSource.Manual:
                return Mathf.Clamp01(manualProgress);

            case ProgressSource.ProgressDisplay:
                RefreshProgressReferences();
                return progressDisplay != null ? Mathf.Clamp01(progressDisplay.currentProgress / 100f) : 0f;

            case ProgressSource.Checkpoints:
                RefreshProgressReferences();
                return CheckpointProgress();

            case ProgressSource.DistanceTravelled:
                return DistanceProgress();

            default:
                RefreshProgressReferences();
                if (progressDisplay != null)
                    return Mathf.Clamp01(progressDisplay.currentProgress / 100f);
                if (compass != null && compass.checkpoints != null && compass.checkpoints.Length > 0)
                    return CheckpointProgress();
                return DistanceProgress();
        }
    }

    /// <summary>Turns a progress value into a rain strength using the ramp settings.</summary>
    public float EvaluateIntensity(float progress)
    {
        float t = rampEnd > rampStart
            ? Mathf.InverseLerp(rampStart, rampEnd, progress)
            : (progress >= rampEnd ? 1f : 0f);

        if (!Mathf.Approximately(rampEase, 1f))
            t = Mathf.Pow(t, rampEase);

        return Mathf.Lerp(lightIntensity, heavyIntensity, Mathf.Clamp01(t));
    }

    private float CheckpointProgress()
    {
        if (compass == null || compass.checkpoints == null || compass.checkpoints.Length == 0)
            return DistanceProgress();

        return Mathf.Clamp01(compass.GetCurrentIndex() / (float)compass.checkpoints.Length);
    }

    private float DistanceProgress()
    {
        if (!haveStartPosition)
        {
            startPosition = transform.position;
            haveStartPosition = true;
        }

        if (distanceRampMeters <= 0.01f)
            return 1f;

        Vector3 delta = transform.position - startPosition;
        delta.y = 0f;
        return Mathf.Clamp01(delta.magnitude / distanceRampMeters);
    }

    private void RefreshProgressReferences()
    {
        if (progressDisplay != null && compass != null)
            return;

        if (Time.realtimeSinceStartup < nextProgressSearch)
            return;

        nextProgressSearch = Time.realtimeSinceStartup + 1f;

        if (progressDisplay == null)
            progressDisplay = FindObjectOfType<ProgressDisplay>();
        if (compass == null)
            compass = FindObjectOfType<CheckpointIndicator>();
    }

    // ------------------------------------------------------------------
    //  Following the player
    // ------------------------------------------------------------------

    private void FollowTarget()
    {
        Transform target = ResolveFollowTarget();
        if (target == null)
            return;

        Vector3 position = target.position;

        if (haveLastTargetPosition && Time.deltaTime > 0.00001f)
        {
            Vector3 velocity = (position - lastTargetPosition) / Time.deltaTime;
            targetVelocity = Vector3.Lerp(targetVelocity, velocity, 0.35f);
        }

        lastTargetPosition = position;
        haveLastTargetPosition = true;

        transform.position = position;
        transform.rotation = Quaternion.identity;
        transform.localScale = Vector3.one;
    }

    private Transform ResolveFollowTarget()
    {
        if (followTarget != null)
        {
            playerRoot = followTarget;
            return playerRoot;
        }

        if (playerRoot != null)
            return playerRoot;

        CarController car = FindObjectOfType<CarController>();
        if (car != null)
        {
            playerRoot = car.transform;
            return playerRoot;
        }

        Camera camera = Camera.main;
        if (camera != null)
        {
            playerRoot = camera.transform;
            return playerRoot;
        }

        return null;
    }

    private void RefreshSplashGround()
    {
        if (splashes == null)
            return;

        if (float.IsNaN(splashGroundY) || Time.realtimeSinceStartup >= nextGroundSample)
        {
            nextGroundSample = Time.realtimeSinceStartup + Mathf.Max(0.02f, groundRefreshInterval);

            float groundY = SampleGroundY();
            if (!float.IsNaN(groundY))
                splashGroundY = groundY;
            else if (float.IsNaN(splashGroundY))
                splashGroundY = transform.position.y - 0.5f;
        }

        Vector3 local = splashes.transform.localPosition;
        local.y = splashGroundY + splashHeight - transform.position.y;
        splashes.transform.localPosition = local;
    }

    private float SampleGroundY()
    {
        Vector3 origin = transform.position + Vector3.up * 2f;
        int count = Physics.RaycastNonAlloc(origin, Vector3.down, groundHits, groundRaycastDistance,
            groundMask, QueryTriggerInteraction.Ignore);

        float best = float.NaN;

        for (int i = 0; i < count; i++)
        {
            Collider hit = groundHits[i].collider;
            if (hit == null || BelongsToPlayer(hit.transform))
                continue;

            float y = groundHits[i].point.y;
            if (float.IsNaN(best) || y > best)
                best = y;
        }

        return best;
    }

    private bool BelongsToPlayer(Transform candidate)
    {
        Transform root = playerRoot != null ? playerRoot : transform;

        while (candidate != null)
        {
            if (candidate == root)
                return true;
            candidate = candidate.parent;
        }

        return false;
    }

    // ------------------------------------------------------------------
    //  Helpers
    // ------------------------------------------------------------------

    private static ParticleSystem.MinMaxCurve RandomBetween(float a, float b)
    {
        return a <= b
            ? new ParticleSystem.MinMaxCurve(a, b)
            : new ParticleSystem.MinMaxCurve(b, a);
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = Mathf.Clamp01(alpha);
        return color;
    }
}
