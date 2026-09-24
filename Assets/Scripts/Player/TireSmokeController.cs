using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// The smoke that comes off the rear tyres when the truck is thrown into a hard turn.
///
/// It is driven by <see cref="TireSkidController"/>, which already works out how hard each rear tyre is
/// sliding in order to draw the mark on the road: the smoke takes that same 0 - 1 slide and turns it into
/// a rate, so smoke appears exactly where and when the truck starts leaving marks and never when it is not.
/// Braking hard smokes too, because that is a slide as well.
///
/// Nothing has to be placed in a scene. The component installs itself on every player truck as each scene
/// loads - the same way <see cref="TruckPaint"/> reapplies the paint job - and builds its two particle
/// systems around the wheels it finds. Assign <see cref="leftSmoke"/> / <see cref="rightSmoke"/> to use
/// hand-made systems instead, and the rest of the settings either way.
///
/// The puffs are measured against the width of the tyre mark rather than in fixed metres: the truck prefabs
/// are modelled small and scaled up at the root, so a hard-coded size would be 50 times out on one of them.
/// The mark is the one real-world width the rig reports for a contact patch (something over 10 cm on the
/// player truck), so the settings read as multiples of it - <c>sizeFactor 2</c> is a puff a couple of times
/// wider than the tyre, grown to about six tyre-widths by the time it has faded.
///
/// The plume is thrown to the side of the turn, not just left behind: the emitter is carried out toward the
/// side the truck is steering into (<see cref="sideReach"/>) and each puff is given a drift that way
/// (<see cref="sideSpray"/>), so a hard left puts smoke along the truck's left flank as well as at its
/// tail. Nothing about the plume is uniform either - launch speed, direction, size, life and spin are all
/// ranges, and a turbulence field curls the puffs as they rise, because a cloud whose particles all leave
/// at one speed in one direction reads as a straight line rather than as smoke.
/// </summary>
[DisallowMultipleComponent]
public class TireSmokeController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Filled in automatically when the component installs itself.")]
    public CarController car;

    [Tooltip("The skid marks to follow. Filled in automatically.")]
    public TireSkidController skid;

    [Tooltip("Left rear smoke. Left empty, one is built at the tyre.")]
    public ParticleSystem leftSmoke;

    [Tooltip("Right rear smoke. Left empty, one is built at the tyre.")]
    public ParticleSystem rightSmoke;

    [Header("When it smokes")]
    [Tooltip("How hard a tyre must slide before any smoke shows at all, 0 - 1. The marks start earlier than " +
             "the smoke on purpose: a tyre squealing into life does not yet spit smoke.")]
    [Range(0f, 1f)] public float smokeThreshold = 0.3f;

    [Tooltip("No smoke below this speed (km/h), however hard the tyres are sliding, and full smoke by 1.6x " +
             "it. A tyre can be made to scrub while barely moving, and a cloud rolling off a crawling truck " +
             "would look wrong.")]
    public float minSpeed = 22f;

    [Tooltip("How quickly the smoke follows the slide. Higher is more immediate.")]
    public float response = 9f;

    [Header("Amount")]
    [Tooltip("Particles per second, per tyre, at a full slide.")]
    public float maxRate = 52f;

    [Tooltip("How long a puff lives, in seconds. Longer lingers, shorter reads as a sharper, thinner cloud.")]
    public float lifetime = 1.3f;

    [Tooltip("Upper limit on live particles per tyre.")]
    public int maxParticles = 140;

    [Header("Look")]
    [Tooltip("Puff size at birth, as a multiple of the width of the tyre mark: a tyres width is 1.")]
    public float sizeFactor = 1.9f;

    [Tooltip("How much bigger a puff gets by the end of its life.")]
    public float growth = 3f;

    [Tooltip("How fast the smoke rises, as a multiple of the mark width per second. The mark is roughly the " +
             "tyre's width, so 5 is half a metre per second on the player truck.")]
    public float riseFactor = 5f;

    [Tooltip("How far the smoke drifts off the tyre, as a multiple of the mark width per second.")]
    public float spreadFactor = 2.8f;

    [Header("Sideways")]
    [Tooltip("How strongly a hard turn throws the smoke out to that side, as a multiple of the mark " +
             "width per second. The side follows the steering, so a left turn puts it on the left.")]
    public float sideSpray = 3.2f;

    [Tooltip("How far the emitter itself is carried out toward the side of the truck during a hard turn, " +
             "as a multiple of the mark width. This is what puts the plume alongside the truck rather " +
             "than only behind it.")]
    public float sideReach = 1.3f;

    [Tooltip("How quickly the plume swings to the side the truck is turning into. Higher is snappier.")]
    public float sideResponse = 4f;

    [Tooltip("How much each puff's direction is randomised, 0 - 1. A plume whose puffs all leave at the " +
             "same speed and angle reads as a straight line rather than as smoke.")]
    [Range(0f, 1f)] public float directionRandomness = 0.7f;

    [Tooltip("How much turbulence the puffs get as they travel, as a multiple of the mark width per " +
             "second. This is what makes the cloud curl instead of drifting in straight lines.")]
    public float turbulence = 1.1f;

    public Color smokeColour = new Color(0.87f, 0.86f, 0.84f, 0.3f);

    [Header("Softening")]
    [Tooltip("Smoke is hidden for this long after a scene loads, so a truck spawned mid-slide does not " +
             "start the level in a cloud.")]
    public float startDelay = 0.6f;

    private float leftAmount;
    private float rightAmount;
    private float turnSide;              // -1 turning hard left, +1 hard right, smoothed

    private Transform leftContact;
    private Transform rightContact;
    private float reference = 0.05f;      // the mark width, in world units
    private float lift;
    private Material smokeMaterial;
    private Texture2D smokeTexture;
    private float readyTime;

    // ---------------------------------------------------------------- bootstrap

    /// <summary>
    /// Puts the smoke on every player truck, in every scene. A truck that is placed by hand in a level is a
    /// fresh instance of the prefab on each load, which is exactly why this has to happen that often.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (!Application.isPlaying) return;

        SceneManager.sceneLoaded += OnSceneLoaded;
        InstallInActiveScene();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        InstallInActiveScene();
    }

    private static void InstallInActiveScene()
    {
        // CarController is what the player's truck runs; the traffic runs a different controller, so a
        // passing car never gets tyre smoke of its own.
        CarController[] cars = Object.FindObjectsOfType<CarController>();

        for (int i = 0; i < cars.Length; i++)
        {
            if (cars[i] == null) continue;
            if (cars[i].gameObject.GetComponent<TireSmokeController>() != null) continue;

            cars[i].gameObject.AddComponent<TireSmokeController>();
        }
    }

    // ---------------------------------------------------------------- lifecycle

    private void Awake()
    {
        readyTime = Time.time + startDelay;

        // Before any system is built, so the smoke this truck makes is already the right amount. The presets
        // thin the plume rather than switching it off: a hard corner and a hard stop are exactly when the
        // effect is wanted, so it stays at every setting - the machine it is running on decides how thick it
        // is, not whether it exists. The values above stay the ones to tune; this only takes a share off them.
        maxRate = GraphicsQuality.ScaleRate(maxRate, GraphicsQuality.ParticleScale, 4f);
        maxParticles = GraphicsQuality.ScaleCount(maxParticles, GraphicsQuality.ParticleScale, 8);

        if (car == null) car = GetComponentInChildren<CarController>(true);
        if (skid == null) skid = GetComponentInChildren<TireSkidController>(true);

        if (skid == null)
        {
            Debug.LogWarning("[TireSmoke] No TireSkidController on '" + name + "', so there are no rear " +
                             "tyre contact points to smoke from. Tyre smoke is off for this truck.");
            return;
        }

        leftContact = skid.LeftContact;
        rightContact = skid.RightContact;

        if (leftContact == null || rightContact == null)
        {
            Debug.LogWarning("[TireSmoke] The skid controller on '" + name + "' has no rear trails " +
                             "assigned, so tyre smoke has nowhere to come from.");
            return;
        }

        // The mark is the only real-world width to hand on any of the truck prefabs, so everything scales
        // off it: what is a puff a few times wider than the mark the tyre is leaving.
        reference = Mathf.Max(0.01f, MeasureReference());

        lift = reference * 0.4f;

        if (leftSmoke == null || rightSmoke == null)
        {
            smokeTexture = CreatePuffTexture();
            smokeMaterial = CreatePuffMaterial(smokeTexture);

            if (leftSmoke == null) leftSmoke = BuildSmoke("Tyre Smoke (L)", leftContact);
            if (rightSmoke == null) rightSmoke = BuildSmoke("Tyre Smoke (R)", rightContact);
        }
    }

    /// <summary>
    /// The width everything else is measured against, in metres. The tyre mark is the best real-world width
    /// the truck offers, being the contact patch itself, and it has to be converted out of the trail's own
    /// local units by the trail's world scale - the truck prefabs are modelled small and scaled up, so a
    /// raw widthMultiplier would be wrong by whatever the rig happens to be. The wheel's radius stands in
    /// for a truck whose trails are not set up.
    /// </summary>
    private float MeasureReference()
    {
        TrailRenderer trail = skid.rearLeftTrail;

        if (trail != null)
        {
            float width = trail.widthMultiplier * WidestOf(trail.widthCurve);
            width *= Mathf.Abs(trail.transform.lossyScale.x);

            if (width > 0.001f) return width;
        }

        WheelPhysics wheel = GetComponentInChildren<WheelPhysics>(true);
        if (wheel != null && wheel.wheelRadius > 0.001f) return wheel.wheelRadius * 2f;

        return reference;
    }

    /// <summary>The widest point of a width curve, which is the part of the mark the smoke should match.</summary>
    private static float WidestOf(AnimationCurve curve)
    {
        if (curve == null || curve.length == 0) return 1f;

        float widest = 0f;
        for (int i = 0; i <= 8; i++) widest = Mathf.Max(widest, curve.Evaluate(i / 8f));

        return widest > 0.0001f ? widest : 1f;
    }

    private void OnDestroy()
    {
        if (smokeMaterial != null) Destroy(smokeMaterial);
        if (smokeTexture != null) Destroy(smokeTexture);

        // The sceneLoaded subscription is deliberately left in place: it belongs to the one-off bootstrap,
        // not to this instance, and a truck torn down on the way to the next level must not take the hook
        // that installs the smoke on the truck waiting there.
    }

    // ---------------------------------------------------------------- per frame

    private void Update()
    {
        if (skid == null || leftSmoke == null || rightSmoke == null) return;

        // A truck that starts a level already sliding (a reset onto a steep shoulder, say) must not begin in
        // a cloud of smoke.
        if (Time.time < readyTime)
        {
            Fade();
            return;
        }

        float gate = SpeedGate();

        leftAmount = Mathf.MoveTowards(leftAmount, Slide(skid.LeftSkid) * gate, Time.deltaTime * response);
        rightAmount = Mathf.MoveTowards(rightAmount, Slide(skid.RightSkid) * gate, Time.deltaTime * response);

        // Which side the smoke is thrown to follows the steering, and only while a tyre is actually
        // sliding: -1 is full left and +1 is full right. It is swept rather than snapped, so the plume
        // crosses the truck as the wheel goes over instead of jumping from one side to the other.
        float steer = car != null ? car.steerInput : 0f;
        bool sliding = Mathf.Max(leftAmount, rightAmount) > 0.01f;
        float targetSide = sliding ? Mathf.Clamp(steer, -1f, 1f) : 0f;

        turnSide = Mathf.MoveTowards(turnSide, targetSide, Time.deltaTime * sideResponse);

        Apply(leftSmoke, leftContact, leftAmount);
        Apply(rightSmoke, rightContact, rightAmount);
    }

    private void Fade()
    {
        leftAmount = Mathf.MoveTowards(leftAmount, 0f, Time.deltaTime * response);
        rightAmount = Mathf.MoveTowards(rightAmount, 0f, Time.deltaTime * response);

        Apply(leftSmoke, leftContact, leftAmount);
        Apply(rightSmoke, rightContact, rightAmount);
    }

    /// <summary>Turns a tyre's slide into how much smoke it should be making, 0 - 1.</summary>
    private float Slide(float skidAmount)
    {
        if (skidAmount <= smokeThreshold) return 0f;

        return Mathf.InverseLerp(smokeThreshold, 1f, skidAmount);
    }

    /// <summary>
    /// The low-speed guard. Braking counts as a slide in the skid controller from the first press, so without
    /// this a truck stopping gently at a junction would disappear in a cloud of its own smoke.
    /// </summary>
    private float SpeedGate()
    {
        if (car == null) return 1f;

        return Mathf.InverseLerp(minSpeed, minSpeed * 1.6f, car.currentSpeedKPH);
    }

    private void Apply(ParticleSystem system, Transform contact, float amount)
    {
        if (system == null || contact == null) return;

        // The truck's own axes, so the smoke is thrown to a side of this truck rather than to world right.
        Vector3 right = car != null ? car.transform.right : transform.right;

        // A hard turn carries the emitter out toward the side it is turning into, so the smoke comes off
        // the corner of the truck rather than only off the tyres. The reach is a multiple of the mark
        // width, like everything else here, so it is the same shape whatever scale a rig is modelled at.
        float side = turnSide * amount;
        Vector3 offset = right * (side * sideReach * reference);

        // The emitter rides on the tyre, but the puffs themselves are simulated in world space so they are
        // left behind by the truck instead of travelling with it.
        system.transform.position = contact.position + Vector3.up * lift + offset;

        ParticleSystem.EmissionModule emission = system.emission;
        emission.rateOverTime = maxRate * amount;

        // A spread of launch speeds rather than one speed: puffs that all leave at exactly the same
        // velocity are what makes a plume read as a straight line.
        ParticleSystem.MainModule main = system.main;
        main.startSpeed = new ParticleSystem.MinMaxCurve(reference * spreadFactor * 0.2f,
                                                        reference * spreadFactor * (1f + 0.3f * amount));

        // Drift, randomised per particle around the sideways push: a plume with no two puffs travelling
        // the same way is what reads as smoke rather than as a beam.
        float drift = reference * sideSpray * side;
        float jitter = reference * directionRandomness * (0.5f + amount);

        ParticleSystem.VelocityOverLifetimeModule velocity = system.velocityOverLifetime;
        velocity.x = new ParticleSystem.MinMaxCurve(drift * right.x - jitter, drift * right.x + jitter);
        velocity.y = new ParticleSystem.MinMaxCurve(reference * riseFactor * 0.55f,
                                                    reference * riseFactor * (0.9f + 0.5f * amount));
        velocity.z = new ParticleSystem.MinMaxCurve(drift * right.z - jitter, drift * right.z + jitter);
    }

    // ---------------------------------------------------------------- building

    /// <summary>
    /// A soft, grainy puff of smoke, made here rather than shipped as an asset so the effect needs nothing
    /// set up in any scene or prefab.
    /// </summary>
    private static Texture2D CreatePuffTexture()
    {
        const int size = 64;

        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "Tyre Smoke Puff";

        Color[] pixels = new Color[size * size];
        float centre = (size - 1) * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float nx = (x - centre) / centre;
                float ny = (y - centre) / centre;
                float r = Mathf.Sqrt(nx * nx + ny * ny);

                float alpha = Mathf.Clamp01(1f - r);
                alpha = alpha * alpha * (3f - 2f * alpha);          // soft edge, no hard disc

                // A little grain so a puff does not read as a perfect circle.
                alpha *= Mathf.Lerp(0.7f, 1f, Mathf.PerlinNoise(x * 0.17f, y * 0.17f));

                pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();

        return texture;
    }

    private ParticleSystem BuildSmoke(string objectName, Transform anchor)
    {
        GameObject go = new GameObject(objectName);
        go.transform.SetParent(transform, false);
        go.transform.localRotation = Quaternion.identity;

        // Cancels the rig's own scale (the player truck's root sits at 0.02) so a metre of smoke is a metre.
        Vector3 parentScale = transform.lossyScale;
        go.transform.localScale = new Vector3(InverseOrOne(parentScale.x),
                                             InverseOrOne(parentScale.y),
                                             InverseOrOne(parentScale.z));

        go.transform.position = anchor.position;

        ParticleSystem system = go.AddComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = system.main;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.7f, lifetime * 1.25f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(reference * spreadFactor * 0.2f, reference * spreadFactor);
        main.startSize = new ParticleSystem.MinMaxCurve(reference * sizeFactor * 0.7f,
                                                        reference * sizeFactor * 1.25f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(smokeColour, Fade(smokeColour, 0.7f));
        main.gravityModifier = -0.012f;                     // a puff rises as it cools
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Local;
        main.maxParticles = Mathf.Max(4, maxParticles);
        main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;

        ParticleSystem.EmissionModule emission = system.emission;
        emission.rateOverTime = new ParticleSystem.MinMaxCurve(0f);

        // A half-sphere, so a puff spreads off the tyre and upwards. A full sphere would throw half the
        // smoke down into the road.
        ParticleSystem.ShapeModule shape = system.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Hemisphere;
        shape.radius = reference * 1.4f;

        ParticleSystem.VelocityOverLifetimeModule velocity = system.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.y = new ParticleSystem.MinMaxCurve(reference * riseFactor * 0.55f, reference * riseFactor);

        // Turbulence, so the cloud curls and mixes as it rises rather than drifting in straight lines.
        ParticleSystem.NoiseModule noise = system.noise;
        noise.enabled = true;
        noise.strength = new ParticleSystem.MinMaxCurve(reference * turbulence * 0.5f, reference * turbulence);
        noise.frequency = 0.6f;
        noise.damping = true;
        noise.octaveCount = 2;
        noise.quality = ParticleSystemNoiseQuality.Medium;

        // A slow tumble per puff, so the billboards do not all stay at the angle they were born at.
        ParticleSystem.RotationOverLifetimeModule spin = system.rotationOverLifetime;
        spin.enabled = true;
        spin.z = new ParticleSystem.MinMaxCurve(-0.9f, 0.9f);

        // Grows and thins out over its life, which is what makes smoke read as smoke rather than as dots.
        ParticleSystem.SizeOverLifetimeModule sizeOverLife = system.sizeOverLifetime;
        sizeOverLife.enabled = true;
        sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, GrowCurve());

        ParticleSystem.ColorOverLifetimeModule colourOverLife = system.colorOverLifetime;
        colourOverLife.enabled = true;
        colourOverLife.color = new ParticleSystem.MinMaxGradient(FadeGradient());

        ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
        {
            renderer.material = smokeMaterial;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortMode = ParticleSystemSortMode.Distance;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        system.Play();

        return system;
    }

    private AnimationCurve GrowCurve()
    {
        AnimationCurve curve = new AnimationCurve();
        curve.AddKey(0f, 1f);
        curve.AddKey(0.35f, growth * 0.8f);
        curve.AddKey(1f, growth);
        return curve;
    }

    private Gradient FadeGradient()
    {
        Gradient gradient = new Gradient();

        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f),
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.75f, 0.18f),
                new GradientAlphaKey(0.35f, 0.65f),
                new GradientAlphaKey(0f, 1f),
            });

        return gradient;
    }

    private static Color Fade(Color colour, float scale)
    {
        colour.a *= scale;
        return colour;
    }

    private static float InverseOrOne(float value)
    {
        return Mathf.Abs(value) > 0.0001f ? 1f / value : 1f;
    }

    /// <summary>
    /// A material for the puffs, found the same way the rain painter finds its particle shader.
    ///
    /// The order matters in a build rather than in the editor: a shader fetched by name at runtime is only
    /// there if something put it in the build, and <c>Sprites/Default</c> is one Unity always includes (it is
    /// in the default Always Included Shaders). It also happens to be exactly right - unlit, alpha blended,
    /// and it honours the per-particle vertex colour the fade comes from - so it leads the list and the
    /// particle shaders are only a fallback.
    /// </summary>
    private static Material CreatePuffMaterial(Texture2D texture)
    {
        string[] candidates =
        {
            "Sprites/Default",
            "Mobile/Particles/Alpha Blended",
            "Legacy Shaders/Particles/Alpha Blended",
            "Particles/Standard Unlit",
            "Unlit/Transparent",
        };

        Shader shader = null;
        for (int i = 0; i < candidates.Length; i++)
        {
            shader = Shader.Find(candidates[i]);
            if (shader != null) break;
        }

        Material material = new Material(shader);
        material.name = "Tyre Smoke (Runtime)";
        material.mainTexture = texture;

        if (material.HasProperty("_TintColor")) material.SetColor("_TintColor", Color.white);
        if (material.HasProperty("_Color")) material.SetColor("_Color", Color.white);

        // The Standard particle shader is opaque until its blend keywords are set; the legacy and mobile
        // ones are already alpha blended.
        if (material.HasProperty("_SrcBlend"))
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (material.HasProperty("_DstBlend"))
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (material.HasProperty("_ZWrite")) material.SetInt("_ZWrite", 0);
        if (material.HasProperty("_Cull"))
            material.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);

        material.DisableKeyword("_ALPHATEST_ON");
        material.EnableKeyword("_ALPHABLEND_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");

        material.SetOverrideTag("RenderType", "Transparent");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

        return material;
    }
}
