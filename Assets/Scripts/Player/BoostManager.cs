using UnityEngine;

/// <summary>
/// The milkshakes the truck is carrying, and what drinking one does.
///
/// A milkshake on the road is not an effect that fires the moment it is touched: it goes in the tank, and the
/// driver spends it when they want it - out of a corner, at the bottom of a jump, wherever the road ahead is
/// worth it. So this holds two separate things: how many are left, and the boost that is running right now.
///
/// The boost is a real one. The truck is pushed along its own heading, harder than the engine can manage at
/// that speed, up to a speed of its own - so a boost taken at a crawl is a big shove and one taken flat out
/// is a top-up past the 120 km/h the throttle alone gives. Nothing about the engine, the tyres, the grip or
/// the steering is touched: the truck still drives exactly the way it always did, it simply arrives at the
/// next corner carrying more speed. The push is applied to the truck itself rather than through the wheels,
/// so a boost works in the air as well as on the road, and it does not need four wheels on the ground or a
/// foot on the throttle to do anything at all.
///
/// The boost is spent as one unit: pressing the button uses one milkshake and runs for the whole of
/// <see cref="boostDuration"/>, however many are left. There is nothing to hold down.
///
/// The moments worth hearing - a milkshake going into the tank, one being spent, and the button being pressed
/// on an empty tank - are sounded by <see cref="BoostSounds"/>, so a level gets them by having a truck in it
/// and nothing else.
/// </summary>
public class BoostManager : MonoBehaviour
{
    [Header("What The Truck Is Carrying")]
    [Tooltip("How many boosts the truck starts a level with. The milkshakes on the road add to this as the " +
             "driver collects them.")]
    public int startingBoosts = 0;

    [Tooltip("The most the truck may carry at once. A milkshake collected while the tank is already full is " +
             "still collected and still disappears - the count simply stops here, so a long straight does " +
             "not become a stockpile. 0 leaves it unlimited.")]
    public int maximumBoosts = 5;

    [Header("The Boost")]
    [Tooltip("How long one boost lasts, in seconds. This is the whole of a boost: it is spent as one unit, " +
             "not held down.")]
    public float boostDuration = 2.5f;

    [Tooltip("How hard a boost pushes the truck along its own heading, in metres per second squared, on top " +
             "of whatever the engine is doing. This is the number to raise if a boost does not feel like one.")]
    public float boostAcceleration = 12f;

    [Tooltip("The speed a boost drives the truck towards, in km/h. The push fades away as the truck approaches " +
             "it, which is what keeps a boost from flinging the truck past what it can handle and what makes " +
             "a boost at a standstill a shove while one at speed is a top-up. The truck's own top speed is " +
             "120 km/h, so this is what the boost is worth on its own.")]
    public float boostTopSpeed = 170f;

    [Header("Boost Particles")]
    [Tooltip("The flames and smoke the boost throws while it is running. Left empty they are simply not " +
             "played - the boost itself is unaffected.")]
    public ParticleSystem particleA;
    public ParticleSystem particleB;
    public ParticleSystem particleC;
    public ParticleSystem particleD;

    /// <summary>
    /// How often the empty knock may be played. Short enough that a deliberate second press is answered, long
    /// enough that a key held down is not a machine gun of knocks - and long enough now that the sound, which
    /// runs for eight tenths of a second, is not cut off by the next one: it plays out and dies away before
    /// the button can answer again.
    /// </summary>
    private const float EmptySoundCooldown = 0.6f;

    private Rigidbody rb;
    private CarController car;
    private CameraController cameraController;

    // When the empty knock last played, so a held button is answered once.
    private float lastEmptySound = -999f;

    // The boost that is running right now, as a countdown in physics seconds so that a pause stops it dead
    // rather than leaving it to run out behind the pause menu.
    private bool boosting;
    private float boostTimeLeft;

    /// <summary>
    /// The truck's boost manager, for whatever needs to read it without being wired to the truck - the HUD
    /// counter in particular, which lives on a canvas that the level places independently.
    /// </summary>
    public static BoostManager Instance { get; private set; }

    /// <summary>How many boosts the truck is carrying, ready to be spent.</summary>
    public int Boosts { get; private set; }

    /// <summary>Whether a boost is being spent right now.</summary>
    public bool IsBoosting
    {
        get { return boosting; }
    }

    /// <summary>How much of the running boost is left, as a fraction of its length, for a bar or a flash.</summary>
    public float BoostFraction
    {
        get
        {
            if (!boosting || boostDuration <= 0f) return 0f;

            return Mathf.Clamp01(boostTimeLeft / boostDuration);
        }
    }

    private void Awake()
    {
        // The manager hangs off the truck's own objects, so its rigidbody and its controller are found up
        // the hierarchy rather than on this object.
        rb = GetComponentInParent<Rigidbody>();
        car = GetComponentInParent<CarController>();

        Boosts = Mathf.Max(0, startingBoosts);

        Instance = this;
    }

    private void OnDisable()
    {
        // A level that ends mid-boost, or a truck switched off, must not leave the flames running on the
        // truck's place in the next level or leave the car believing it is still boosting.
        EndBoost();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// Puts one more milkshake in the tank. Called by the pickup the truck drives over - and it is only ever
    /// a count and a sound: no boost fires here, which is the whole point of a pickup being something the
    /// driver spends later rather than something that fires where they found it.
    ///
    /// Both sounds are the milk going in; see <see cref="BoostSounds"/>. The sip is heard either way, because
    /// the milkshake is drunk whether or not there was room for it in the tank - but the jingle that says the
    /// count went up is only played when it did, since a tank that is already full has nothing to celebrate.
    /// </summary>
    public void Collect(int count = 1)
    {
        int carried = Boosts;
        int next = carried + Mathf.Max(1, count);

        Boosts = maximumBoosts > 0 ? Mathf.Min(next, maximumBoosts) : next;

        BoostSounds.PlayPickup();

        if (Boosts > carried)
            BoostSounds.PlayCollect();
    }

    private void Update()
    {
        // Read here rather than in FixedUpdate: the button is an edge, and a heavy frame runs FixedUpdate
        // more than once, which would spend two boosts for one press.
        if (PauseTracker.Instance != null && PauseTracker.Instance.isPaused)
            return;

        if (boosting || !GameInput.BoostPressed())
            return;

        if (Boosts > 0)
        {
            BeginBoost();

            return;
        }

        // Nothing in the tank, so the press is answered with the empty knock rather than with a boost. It is
        // played at most once every EmptySoundCooldown: a held key repeats, and the point of the sound is to
        // say the button was heard, not to keep saying it while it is held.
        if (Time.unscaledTime - lastEmptySound < EmptySoundCooldown)
            return;

        lastEmptySound = Time.unscaledTime;

        BoostSounds.PlayEmpty();
    }

    private void FixedUpdate()
    {
        if (!boosting)
            return;

        boostTimeLeft -= Time.fixedDeltaTime;

        if (boostTimeLeft <= 0f)
        {
            EndBoost();
            return;
        }

        if (rb == null)
            return;

        // Along the truck's own heading, flattened onto the ground: a boost pushes the truck the way it is
        // pointing without tipping it onto its nose when that is up a ramp, or driving it into a road that
        // climbs. Down a ramp it means the boost is a shove along the road rather than downwards.
        Vector3 forward = transform.forward;
        forward.y = 0f;

        if (forward.sqrMagnitude < 0.0001f)
            return;

        forward.Normalize();

        float speed = Vector3.Dot(rb.velocity, forward);

        float target = Mathf.Max(1f, boostTopSpeed) / 3.6f;
        float room = 1f - Mathf.Clamp01(Mathf.Max(0f, speed) / target);

        if (room <= 0f)
            return;

        // An acceleration rather than a force, so the boost is worth the same whatever the truck happens to
        // be carrying and no mass tuning is needed to go with it.
        rb.AddForce(forward * (boostAcceleration * room), ForceMode.Acceleration);
    }

    /// <summary>Spends one boost and starts it running: the push, the flames, the camera and the sound, together.</summary>
    private void BeginBoost()
    {
        if (boosting || Boosts <= 0)
            return;

        Boosts--;

        boosting = true;
        boostTimeLeft = Mathf.Max(0.01f, boostDuration);

        SetParticles(true);

        BoostSounds.PlayBoost();

        if (car != null)
            car.SetBoosting(true);

        if (cameraController == null)
            cameraController = FindObjectOfType<CameraController>();

        if (cameraController != null)
            cameraController.TriggerBoostCamera();
    }

    /// <summary>
    /// Ends the boost, wherever it ended from - the timer running out, the level being left, or the truck
    /// being destroyed. Nothing here is a count: the milkshake was already spent when the boost began.
    /// </summary>
    private void EndBoost()
    {
        if (!boosting)
            return;

        boosting = false;
        boostTimeLeft = 0f;

        SetParticles(false);

        if (car != null)
            car.SetBoosting(false);
    }

    private void SetParticles(bool state)
    {
        HandleParticle(particleA, state);
        HandleParticle(particleB, state);
        HandleParticle(particleC, state);
        HandleParticle(particleD, state);
    }

    private void HandleParticle(ParticleSystem ps, bool enable)
    {
        if (ps == null)
            return;

        if (enable)
            ps.Play();
        else
            ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }
}
