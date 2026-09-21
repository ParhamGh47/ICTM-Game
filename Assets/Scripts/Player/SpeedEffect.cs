using UnityEngine;

/// <summary>
/// The warp trails the truck throws off at speed: one particle system down each side, emitted from the art's
/// own "ahead" point and left behind in world space, so they sweep back past the truck and the camera.
///
/// It is driven by speed - nothing below <see cref="minSpeed"/>, everything by <see cref="maxSpeed"/>, and
/// nothing at all while the brakes are on - and the two systems come from the truck prefab, so the art stays
/// where it is and this only says when and how much.
///
/// Looking down from the Above camera, the same two emitters are not enough. Seen from above they are a narrow
/// band just in front of the truck, where a speed effect wants the whole picture moving. So while the camera
/// is tipped down this spreads the trails over a wide field instead: extra emitters, copies of the prefab's
/// own so nothing about the look is invented here, laid over the ground the camera can actually see - an
/// ellipse stretched along the road and pushed ahead of the truck, because from above most of the visible
/// ground is ahead of it. The distances are all in the truck's own ahead distance, which keeps the field in
/// proportion to the art rather than to a number guessed in metres, and means a truck modelled at a different
/// size, or a level that scales its player up at the root, still gets the same spread.
///
/// Each emitter carries little, on purpose: covering the frame is the spread's job, not the density's, so the
/// picture fills with scattered trails instead of a few heavy ones. The two prefab trails get a little busier
/// and their particle limits lift with them, since the prefab caps them low enough that the extra rate would
/// otherwise queue behind the cap.
///
/// <see cref="topDownFrom"/> / <see cref="topDownTo"/> are <see cref="CameraView"/>'s angles, the same ones
/// the speed blur uses, and everything eases in and out across them - the camera switch is a cut, and the
/// trails should not cut with it. Nothing is built until the camera actually looks down, and the ordinary
/// chase view is left exactly as it was.
/// </summary>
public class SpeedEffect : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The player truck. Wired up in the prefab; looked up the hierarchy if it is ever empty.")]
    public CarController car;

    [Tooltip("The trail down the truck's left side, from the prefab.")]
    public ParticleSystem leftEffect;

    [Tooltip("The trail down the truck's right side, from the prefab.")]
    public ParticleSystem rightEffect;

    [Header("Speed Settings")]
    [Tooltip("No trails below this speed (km/h), however hard the throttle is down, and the full rate by " +
             "maxSpeed.")]
    public float minSpeed = 30f;

    [Tooltip("The speed (km/h) at which the trails are at their full rate.")]
    public float maxSpeed = 150f;

    [Tooltip("Particles per second, per trail, at full speed. The prefab's own rate - the two trails in the " +
             "prefab are already tuned around it.")]
    public float maxParticles = 40f;

    [Header("Top-Down Camera")]
    [Tooltip("How far the camera has to tip from straight down before this counts as a normal chase view, " +
             "matching the speed blur's own angles. Between the two, the widening fades in.")]
    [Range(0f, 90f)]
    public float topDownFrom = 48f;

    [Tooltip("Looking down by less than this and it is fully a top-down view. The level's Above camera looks " +
             "about 40 degrees down, and the chase cameras about 75.")]
    [Range(0f, 90f)]
    public float topDownTo = 68f;

    [Tooltip("How long the widening takes to settle after a camera switch. The camera itself cuts, so this is " +
             "what keeps the trails from cutting too.")]
    public float modeSmoothTime = 0.35f;

    [Tooltip("How many extra emitters to spread over the ground while the camera is looking down. They are " +
             "copies of the prefab's own trail, so this is only about how much of the picture is covered - " +
             "each one carries little on its own, and it is the spread that covers the frame rather than the " +
             "density.")]
    public int topDownEmitters = 10;

    [Tooltip("How far to the sides those emitters reach, as a multiple of the truck's own ahead distance - " +
             "the distance the artists put the trails in front of it, which is the unit the art is already " +
             "built in. 1 puts the widest ones level with the prefab's own trails.")]
    public float topDownSpread = 1.5f;

    [Tooltip("How far up and down the road they reach, in the same unit. Larger than the sideways spread on " +
             "purpose: from above, most of the visible ground lies ahead of the truck, so the field is " +
             "stretched along the road rather than laid out as a circle.\n\n" +
             "It is the sideways distance that decides how much of the picture is covered.")]
    public float topDownDepth = 2.2f;

    [Tooltip("How far ahead of the truck the middle of the field sits, in the same unit, for the same " +
             "reason - the frame is several times taller above the truck than below it.")]
    public float topDownLead = 0.6f;

    [Tooltip("Every other emitter sits at this fraction of the spread, so the field reads as two scattered " +
             "shells of trails rather than as one tidy ring.")]
    [Range(0f, 1f)]
    public float topDownInnerSpread = 0.55f;

    [Tooltip("How much busier the prefab's two trails are in the top-down view.")]
    [Range(1f, 5f)]
    public float topDownRate = 1.5f;

    [Tooltip("How much of the prefab's rate each extra emitter gets. They share the load rather than each " +
             "carrying all of it, so the frame fills with scattered trails instead of a few heavy ones.")]
    [Range(0f, 2f)]
    public float topDownFieldRate = 0.25f;

    [Tooltip("Live particle limit for each of the prefab's own trails in the top-down view. The prefab caps " +
             "them low, so raising the rate alone would only queue up behind the cap.")]
    public int topDownMaxParticles = 40;

    [Tooltip("Live particle limit for each extra emitter.")]
    public int topDownFieldMaxParticles = 24;

    [Tooltip("How much bigger the trails are in the top-down view, where they are seen from further away.")]
    [Range(0.5f, 3f)]
    public float topDownSize = 1.15f;

    [Tooltip("How much longer they live in the top-down view. A streak that lives longer travels further, " +
             "which is what carries the field out across the picture.")]
    [Range(0.5f, 4f)]
    public float topDownLifetime = 1.5f;

    private ParticleSystem.EmissionModule leftEm;
    private ParticleSystem.EmissionModule rightEm;

    private ParticleSystem.MainModule leftMain;
    private ParticleSystem.MainModule rightMain;

    // What the prefab asked for, so the top-down view can only ever add to it.
    private int leftOwnLimit;
    private int rightOwnLimit;
    private float leftOwnSize;
    private float rightOwnSize;
    private float leftOwnLife;
    private float rightOwnLife;

    // The extra emitters. Built the first time the camera looks down, because a level played from behind
    // should not carry them at all, and sized to the truck they were built around.
    private ParticleSystem[] field;
    private ParticleSystem.EmissionModule[] fieldEm;
    private Transform fieldSource;
    private CarController fieldTruck;

    // 0 - 1, how much of a top-down view this is, eased. The two trails and the field all follow it.
    private float topDown;

    // The top-down amount the slowly changing settings were last written for: writing module values every
    // frame is a native call per system, and these only move while the camera is moving.
    private float shapedFor = -1f;

    private Camera view;
    private float nextViewSearch;

    private void Start()
    {
        if (car == null) car = GetComponentInParent<CarController>();

        Cache(leftEffect, ref leftEm, ref leftMain, ref leftOwnLimit, ref leftOwnSize, ref leftOwnLife);
        Cache(rightEffect, ref rightEm, ref rightMain, ref rightOwnLimit, ref rightOwnSize, ref rightOwnLife);
    }

    /// <summary>
    /// Holds on to a trail's modules, and to the settings the prefab gave it so the top-down view can lift
    /// them without ever losing them.
    /// </summary>
    private void Cache(ParticleSystem system, ref ParticleSystem.EmissionModule emission,
                       ref ParticleSystem.MainModule main, ref int ownLimit, ref float ownSize, ref float ownLife)
    {
        if (system == null) return;

        emission = system.emission;
        main = system.main;

        ownLimit = main.maxParticles;
        ownSize = main.startSizeMultiplier;
        ownLife = main.startLifetimeMultiplier;
    }

    private void OnDestroy()
    {
        DestroyField();
    }

    private void Update()
    {
        if (car == null) car = GetComponentInParent<CarController>();

        // The camera can be rebuilt with a level, and looking it up by tag every frame is not free.
        if (view == null && Time.unscaledTime >= nextViewSearch)
        {
            nextViewSearch = Time.unscaledTime + 0.5f;
            view = CameraView.Gameplay();
        }

        if (car == null)
        {
            DestroyField();
            return;
        }

        topDown = CameraView.Follow(topDown, CameraView.LookingDown(view, topDownFrom, topDownTo),
                                    modeSmoothTime);

        float rate = RateFor(car);

        EnsureField();
        UpdateField(rate);
        Apply(rate);
    }

    /// <summary>
    /// The emission the speed is asking for, in particles per second per trail, before the top-down view has
    /// its say.
    /// </summary>
    private float RateFor(CarController truck)
    {
        // Braking throws the weight forward, and trails pouring off the sides through a brake would read as
        // acceleration.
        if (truck.throttleInput < 0f) return 0f;

        float t = Mathf.InverseLerp(minSpeed, maxSpeed, truck.currentSpeedKPH);

        return Mathf.Lerp(0f, maxParticles, t);
    }

    /// <summary>
    /// Drives the prefab's two trails: the rate every frame, and the settings that only move as the camera
    /// tips written when they actually change.
    /// </summary>
    private void Apply(float rate)
    {
        SetRate(leftEffect, leftEm, rate * Mathf.Lerp(1f, topDownRate, topDown));
        SetRate(rightEffect, rightEm, rate * Mathf.Lerp(1f, topDownRate, topDown));

        if (Mathf.Abs(topDown - shapedFor) <= 0.01f) return;

        shapedFor = topDown;

        Shape(leftEffect, leftMain, leftOwnLimit, leftOwnSize, leftOwnLife);
        Shape(rightEffect, rightMain, rightOwnLimit, rightOwnSize, rightOwnLife);
    }

    private static void SetRate(ParticleSystem system, ParticleSystem.EmissionModule emission, float value)
    {
        if (system == null) return;

        // The module is a handle on the system rather than a copy of its settings, but Unity's own example
        // takes it into a local before writing, so this does the same.
        var em = emission;
        em.rateOverTime = value;
    }

    /// <summary>
    /// The particle count, size and life for a trail in the top-down view, all measured from what the prefab
    /// asked for rather than replacing it - so easing the camera back to a chase view restores it exactly.
    /// </summary>
    private void Shape(ParticleSystem system, ParticleSystem.MainModule main, int ownLimit, float ownSize,
                       float ownLife)
    {
        if (system == null) return;

        var settings = main;

        settings.maxParticles = Mathf.Max(ownLimit,
            Mathf.RoundToInt(Mathf.Lerp(ownLimit, topDownMaxParticles, topDown)));
        settings.startSizeMultiplier = ownSize * Mathf.Lerp(1f, topDownSize, topDown);
        settings.startLifetimeMultiplier = ownLife * Mathf.Lerp(1f, topDownLifetime, topDown);
    }

    /// <summary>
    /// Builds the extra emitters the first time the camera looks down, and rebuilds them around a new truck -
    /// a respawn, or a level that builds its player after the scene loads.
    /// </summary>
    private void EnsureField()
    {
        if (topDown <= 0.05f) return;

        if (field != null && fieldTruck == car) return;

        DestroyField();

        // Both prefab trails are the same piece of art mirrored, so copying either of them gives the same
        // look; the left one is used so every emitter in the field is identical rather than half mirrored.
        ParticleSystem source = leftEffect != null ? leftEffect : rightEffect;

        if (source == null) return;

        fieldTruck = car;
        fieldSource = source.transform;
        field = new ParticleSystem[Mathf.Max(1, topDownEmitters)];
        fieldEm = new ParticleSystem.EmissionModule[field.Length];

        for (int i = 0; i < field.Length; i++)
        {
            field[i] = Instantiate(source);
            field[i].gameObject.name = source.gameObject.name + " (top-down " + (i + 1) + ")";

            // A sibling of the original rather than a loose object: it inherits the same transform chain, the
            // same scaling and the same rigidbody the emitter velocity comes from, so the extra trails behave
            // exactly like the ones the prefab came with.
            field[i].transform.SetParent(source.transform.parent, true);

            var main = field[i].main;
            main.maxParticles = Mathf.Max(main.maxParticles, topDownFieldMaxParticles);
            main.startSizeMultiplier = main.startSizeMultiplier * topDownSize;
            main.startLifetimeMultiplier = main.startLifetimeMultiplier * topDownLifetime;

            var em = field[i].emission;
            em.rateOverTime = 0f;

            // A copy plays on awake, and would otherwise spit one frame of the prefab's rate out wherever it
            // happened to be instantiated.
            field[i].Clear();

            fieldEm[i] = field[i].emission;
        }
    }

    /// <summary>
    /// Places the field over the ground around the truck and keeps it there, and gives it its share of the
    /// emission. The layout is worked out in the truck's own frame, so the field drives, turns and stops with
    /// it rather than being left behind in the world.
    /// </summary>
    private void UpdateField(float rate)
    {
        if (field == null || car == null || fieldSource == null) return;

        Transform truck = car.transform;

        // The truck's own "ahead" distance: how far in front of it the prefab puts the trails, measured in the
        // truck's frame so a scaled-up player truck gets a field in the same proportion. That the art is
        // already built in is the unit here - it is what puts the extra trails where the eye expects the
        // streaks to come from.
        Vector3 offset = truck.InverseTransformPoint(fieldSource.position);
        float ahead = Mathf.Max(0.001f, new Vector2(offset.x, offset.z).magnitude);

        float each = rate * topDownFieldRate * topDown;

        for (int i = 0; i < field.Length; i++)
        {
            if (field[i] == null) continue;

            // Evenly around the truck, offset by half a step so none of them sits dead ahead or dead behind,
            // and alternating between two shells so the field is scattered rather than a tidy ring. The
            // shape is an ellipse stretched along the road and pushed ahead of the truck, because that is
            // where the ground the camera can actually see is - which covers more of the picture with fewer
            // trails than a circle centred on the player would.
            float angle = (i + 0.5f) / field.Length * Mathf.PI * 2f;
            float shell = (i % 2 == 0) ? 1f : topDownInnerSpread;

            Vector3 place = new Vector3(Mathf.Sin(angle) * ahead * topDownSpread * shell,
                                        offset.y,
                                        Mathf.Cos(angle) * ahead * topDownDepth * shell + ahead * topDownLead);

            field[i].transform.SetPositionAndRotation(truck.TransformPoint(place), fieldSource.rotation);

            var em = fieldEm[i];
            em.rateOverTime = each;
        }
    }

    private void DestroyField()
    {
        if (field == null) return;

        for (int i = 0; i < field.Length; i++)
        {
            if (field[i] != null) Destroy(field[i].gameObject);
        }

        field = null;
        fieldEm = null;
        fieldSource = null;
        fieldTruck = null;
    }
}
