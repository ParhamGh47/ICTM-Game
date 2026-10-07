using UnityEngine;
using System.Collections;

[RequireComponent(typeof(AudioSource))]
[RequireComponent(typeof(AudioLowPassFilter))]
public class EngineAudio : MonoBehaviour
{
    public CarController car;

    [Header("Current Gear")]
    [SerializeField] private int currentGearDisplay = 1;
    public int CurrentGear => currentGearDisplay;

    // ---------------------------------------------------------------- what the truck asks the gearbox

    /// <summary>
    /// Whether the player is changing gear for themselves (see <see cref="DriveSettings"/>).
    ///
    /// Read live rather than kept, so setting the gearbox to manual or back to automatic takes effect on
    /// the next frame - mid-level, mid-corner, no restart - and so the truck that is being driven is the
    /// only thing that has to know about it.
    /// </summary>
    public bool Manual { get { return DriveSettings.Manual; } }

    /// <summary>The gear engaged: -1 is the reverse gear, 0 to N-1 are first to top.</summary>
    public int GearIndex { get { return currentGear; } }

    /// <summary>Whether the reverse gear is the one engaged. Only ever true in manual - the automatic box
    /// has its own way backwards, which is the brake pedal.</summary>
    public bool ReverseEngaged { get { return Manual && currentGear < 0; } }

    /// <summary>
    /// What the engine's pull is worth right now, as a multiple of what it would be in top gear at its own
    /// best revs. Always exactly 1 in automatic, so the truck that is driven automatically is untouched by
    /// any of this; in manual it carries the gear's ratio, how far off its torque the engine is, and whether
    /// it has run out of revs (see <see cref="lowGearPunch"/>, <see cref="luggingTorque"/>,
    /// <see cref="revLimitStart"/>). The wheels multiply their drive by it - see <see cref="WheelPhysics"/>.
    /// </summary>
    public float PowerScale { get { return Manual ? powerScale : 1f; } }

    /// <summary>Whether the truck is out of gear this instant, which is what a manual change costs: for the
    /// length of the change there is no drive at all, rather than the long braking pause an automatic change
    /// is given.</summary>
    public bool ClutchCut { get { return Manual && Time.time < clutchOutUntil; } }

    /// <summary>Whether the engine is being asked to pull below its torque - the truck is in too tall a gear
    /// for the speed it is at. The exhaust reads this: a labouring engine smokes. Never true in automatic,
    /// whose box has already picked a sensible gear.</summary>
    public bool Lugging { get { return Manual && lugging; } }

    /// <summary>Whether the player is being told to change up: the engine is far enough up its rev range that
    /// the gear it is in has little left to give (see <see cref="shiftUpCue"/>). The HUD reads this to flash
    /// its gear number. Only ever true in manual - the automatic box would have changed for them.</summary>
    public bool ShiftUpSuggested { get { return Manual && shiftUpSuggested; } }

    /// <summary>Whether the player is being told to change down: the engine is labouring or has dropped so
    /// far down its rev range in a gear above first that there is little left to pull with (see
    /// <see cref="shiftDownCue"/>). Only ever true in manual.</summary>
    public bool ShiftDownSuggested { get { return Manual && shiftDownSuggested; } }

    /// <summary>Whether a change is due either way - what the HUD actually flashes on.</summary>
    public bool ShiftCue { get { return ShiftUpSuggested || ShiftDownSuggested; } }

    [Header("Gear Shift")]
    public AudioSource shiftSource;
    public AudioSource exhaustBurst;
    [Range(0f, 1f)] public float shiftVolume = 0.5f;
    public float shiftRPMDrop = 0.65f;
    public float shiftRPMFlare = 1.15f;
    public float shiftCutTime = 0.3f;

    [Header("Shift Timing - More Realistic & Heavier")]
    public float baseShiftDelay = 0.85f;
    public AnimationCurve shiftDelayCurve = new AnimationCurve(
        new Keyframe(0f,   1.25f),
        new Keyframe(0.25f, 1.10f),
        new Keyframe(0.5f,  0.90f),
        new Keyframe(0.75f, 0.75f),
        new Keyframe(1f,   0.60f)
    );

    [Header("Engine Volume Envelope")]
    [Range(0f, 1f)] public float baseVolume = 1f;
    [Range(0f, 1f)] public float dipPercent = 0.5f;
    public float dipDuration = 0.2f;
    public float recoveryDuration = 0.55f;

    [Header("Master Engine Volume Multiplier")]
    [Range(0f, 3f)] public float engineVolumeMultiplier = 3f;

    [Header("Camera Mix")]
    [Tooltip("How the engine sits in the mix when the camera is looking down on the race. From above, the " +
             "truck is a long way from the camera and its engine - a positional sound - arrives quieter than " +
             "the soundtrack, which plays at one volume wherever the player is. Lifting it here is what keeps " +
             "the engine in the same place in the mix from up there.")]
    [Range(0f, 3f)] public float aboveViewGain = 1.45f;

    [Tooltip("...and when the camera is behind or inside the truck, where the engine needs no help. Inside " +
             "the cab the source is almost on top of the listener, so it is eased down instead of being left " +
             "to sit over the music.")]
    [Range(0f, 3f)] public float nearViewGain = 0.85f;

    [Tooltip("The angle the above gain is reached at: how far off straight down this game's above camera " +
             "looks.")]
    public float aboveViewAngle = 40f;

    [Tooltip("...and the angle the near gain is reached at, about where the chase cameras sit.")]
    public float nearViewAngle = 75f;

    [Tooltip("Seconds for the mix to settle after a camera change. A camera switch is a cut, and the sound " +
             "should not cut with it.")]
    public float cameraMixSeconds = 0.6f;

    [Header("Engine RPM")]
    public float idleRPM = 850f;
    public float redlineRPM = 6500f;
    public float rpmInertia = 4f;
    public float throttleBlipAmount = 900f;

    [Header("Gears (Rebalanced - More Realistic)")]
    public float[] gearRatios = { 3.8f, 2.2f, 1.5f, 1.15f, 0.92f };
    [SerializeField] private float[] downshiftSpeedThresholds = new float[] { 0f, 20f, 45f, 75f, 100f };
    public float finalDrive = 3.7f;

    public float shiftUpRPM = 5200f;
    public float shiftDownRPM = 2800f;

    // ---------------------------------------------------------------- manual gearbox

    // With the gearbox set to manual (see DriveSettings) none of the automatic shifting above runs: the
    // truck holds the gear it is in until the player shifts it, and what follows is what makes those gears
    // worth choosing rather than merely different.

    [Tooltip("How long a hand-made change takes, in seconds: the truck is out of gear for it, so it is the " +
             "pause between the lever and the road. Much shorter than the automatic box's own change, which is " +
             "long on purpose - that one is the gearbox deciding, and the pause covers the decision.")]
    public float manualShiftCut = 0.28f;

    [Tooltip("How much harder the low gears pull than the top one. 0.45 means first gear puts about half " +
             "again as much down as fifth does at the same revs - the top gear keeps exactly the force the " +
             "truck has always had, so top speed and the feel of a full-throttle run are untouched, and the " +
             "ladder below it is what makes a gear worth being in.")]
    [Range(0f, 1f)] public float lowGearPunch = 0.45f;

    [Tooltip("The share of its torque the engine makes at a crawl. A manual truck is driven by revs rather " +
             "than by road speed, so this is what being in too tall a gear feels like: the engine is off its " +
             "torque and the truck pulls weakly until the revs come up. 1 turns the effect off.")]
    [Range(0f, 1f)] public float luggingTorque = 0.55f;

    [Tooltip("Revs at which the engine has all of its torque back. Below them it is coming on cam - see " +
             "Lugging Torque - and above them it pulls in full however fast the truck is going.")]
    public float luggingFullRPM = 2800f;

    [Tooltip("Where the rev limiter starts, as a share of the redline. A manual truck cannot be revved past " +
             "its redline, so holding a gear runs the engine out of revs and the pull fades away: that, rather " +
             "than the road, is what decides how fast each gear is good for, and it is the cue to change up. " +
             "1 turns the limiter off.")]
    [Range(0.5f, 1f)] public float revLimitStart = 0.96f;

    [Tooltip("The fastest the truck can be moving, in metres per second, for reverse to be selected. Reverse " +
             "is the gear below first, and changing into it at speed would be a gearbox full of neutrals.")]
    public float reverseEngageSpeed = 2f;

    [Tooltip("How fast reverse is good for, in km/h. Reverse has no ratio of its own - it borrows first's - " +
             "so this is what stops it being a second top gear pointed backwards.")]
    public float reverseTopSpeed = 20f;

    [Tooltip("How far up the rev range a hand-driven truck has to be before the HUD's gear number tells the " +
             "player to change up, as a share of the redline. 0.8 is about where the automatic box would have " +
             "shifted for them, so the cue arrives while there is still something to gain from it rather than " +
             "once the limiter has already taken the pull away.")]
    [Range(0.5f, 1f)] public float shiftUpCue = 0.80f;

    [Tooltip("How far up the rev range a hand-driven truck has to fall before the HUD tells the player to " +
             "change down, as a share of the redline. Being off the engine's torque in too tall a gear is " +
             "exactly the wrong gear to be in, and this is what makes the readout say so.")]
    [Range(0.05f, 0.6f)] public float shiftDownCue = 0.28f;

    [Header("Pitch & Tone")]
    public float pitchMin = 0.70f;
    public float pitchMax = 1.65f;
    public float lowpassCutoffMin = 3000f;
    public float lowpassCutoffMax = 10000f;

    [Header("Organic Variation")]
    [Range(0f, 0.15f)] public float lfoVolumeDepth = 0.08f;
    public float lfoVolumeSpeed = 0.4f;
    [Range(0f, 0.3f)] public float lfoCutoffDepth = 0.2f;
    public float lfoCutoffSpeed = 0.25f;
    public float variationJumpInterval = 12f;

    private AudioSource engineSource;
    private AudioLowPassFilter lowpassFilter;
    private Camera viewCamera;
    private float viewGain = 1f;
    private float engineRPM;
    private int currentGear = 0;
    private float shiftTimer = 0f;

    // Manual gearbox state: the window the truck is out of gear for (a time rather than a flag, so a
    // dropped frame cannot leave the clutch out for ever), what the gear in use and the revs are worth in
    // force, and whether the engine is labouring - which the exhaust reads (see ExhaustSmokeController).
    private float clutchOutUntil = -999f;
    private float powerScale = 1f;
    private bool lugging;
    private bool shiftUpSuggested;
    private bool shiftDownSuggested;
    private bool wasManual;
    private float loopLength;
    private float lfoVolumePhase = 0f;
    private float lfoCutoffPhase = 0f;
    private float lastVariationTime = 0f;

    private enum EnvelopeState { Normal, Dipping, Recovering }
    private EnvelopeState envelopeState = EnvelopeState.Normal;
    private float envelopeTimer = 0f;
    private float volumeEnvelope = 1f;

    [Header("Gear Exhaust Particles")]
    public float duration = 0.06f;
    public ParticleSystem orangePS;
    public ParticleSystem yellowPS;
    public ParticleSystem blackPS;
    public ParticleSystem orangePS2;
    public ParticleSystem yellowPS2;
    public ParticleSystem blackPS2;

    void Awake()
    {
        // The engine is one of the sounds whose volume this script works out for itself - it follows the RPM,
        // dips on every gear change - so the player's ENGINE setting is folded into these writes rather than
        // being handed to a SoundChannelSource, which would fight them. Telling the bus keeps it from giving
        // the engine one of its own; the shift and exhaust one-shots go with it, because they are the same
        // hand doing the same job.
        SoundBus.MarkHandled(GetComponent<AudioSource>());
        SoundBus.MarkHandled(shiftSource);
        SoundBus.MarkHandled(exhaustBurst);
    }

    void Start()
    {
        engineSource = GetComponent<AudioSource>();
        lowpassFilter = GetComponent<AudioLowPassFilter>();
        
        engineSource.loop = true;
        engineSource.playOnAwake = false;
        engineSource.spatialBlend = 1f;
        engineSource.volume = baseVolume * engineVolumeMultiplier * Mix();
        engineSource.Play();

        loopLength = engineSource.clip.length;
        currentGear = 0;
        currentGearDisplay = 1;
        lastVariationTime = Time.time;
    }

    void Update()
    {
        UpdateGearboxMode();
        HandleShifting();
        UpdateEngineRPM();
        UpdateDrivePower();
        UpdatePitchAndFilters();
        UpdateViewMix();
        UpdateVolumeEnvelope();
        UpdateOrganicVariation();

        currentGearDisplay = GearForDisplay();
    }

    /// <summary>
    /// Keeps the gear sensible when the gearbox setting itself changes, which can happen mid-level - it is
    /// the one setting that needs no restart.
    ///
    /// Changing to manual leaves the truck in the gear the automatic box had chosen, which is where a player
    /// taking the lever over would expect to find it. Changing back is the other way round: the automatic box
    /// has no reverse gear to be in - it reverses on the brake pedal - so a truck handed back while in manual's
    /// reverse is put into first, the gear it can use from the speed it is at.
    /// </summary>
    private void UpdateGearboxMode()
    {
        bool manual = Manual;

        if (manual == wasManual) return;

        wasManual = manual;

        // Handing the lever to the player: the truck stays in the gear the box had chosen.
        if (manual) return;

        // Taking it back from them, out of the one gear the automatic box has no use for.
        if (currentGear < 0) currentGear = 0;

        // Whatever the hand-driven box was doing is no longer happening: the clutch is not out and no change
        // is in progress, because the box that was making them is not the one driving the truck any more.
        clutchOutUntil = -999f;
        shiftTimer = 0f;
    }

    /// <summary>
    /// Which gear the HUD's readout shows. 0 is printed as R.
    ///
    /// Driven automatically the gear is the box's own and reverse is not a gear at all - the truck simply
    /// ends up rolling backwards - so the readout shows whichever of those is happening. Driven by hand it is
    /// whatever the player has engaged, whether or not the truck has caught up with it yet: a lever moved into
    /// reverse shows R before the truck has begun to move, which is what a gearbox does.
    /// </summary>
    private int GearForDisplay()
    {
        if (Manual)
            return currentGear < 0 ? 0 : currentGear + 1;

        bool isReversing = Vector3.Dot(car.rb.velocity, transform.forward) < -0.5f;

        return isReversing ? 0 : currentGear + 1;
    }

    private void HandleShifting()
    {
        if (shiftTimer > 0f)
        {
            shiftTimer -= Time.deltaTime;
            return;
        }

        // A hand-driven box shifts when it is told to and not before, so none of the decision-making below
        // runs at all. The reverse the automatic box gets from the brake pedal is not available either: in
        // manual the way backwards is the reverse gear and the gas, so the truck never rolls back under the
        // brake. See WheelPhysics for both.
        if (Manual) return;

        bool movingReverse = Vector3.Dot(car.rb.velocity, transform.forward) < -0.5f;
        if (movingReverse) return;

        float speedKPH = car.currentSpeedKPH;

        if (engineRPM > shiftUpRPM && currentGear < gearRatios.Length - 1 && speedKPH > 20f)
        {
            StartShift(currentGear + 1);
            StartCoroutine(carShiftUp());
        }
        else
        {
            float downshiftSpeedLimit = downshiftSpeedThresholds[currentGear];

            if (speedKPH < downshiftSpeedLimit && currentGear > 0)
            {
                StartShift(currentGear - 1);
                StartCoroutine(carShiftDown());
            }
        }
    }

    // ---------------------------------------------------------------- the player's own gear changes

    /// <summary>
    /// Shift up a gear, because the player asked. Ignored in automatic, where the box has the lever, and
    /// while a change is already in progress, so one press is one gear.
    /// </summary>
    public void ShiftUp()
    {
        if (!Manual || car == null) return;
        if (shiftTimer > 0f) return;

        // In reverse, up is first: the pair of gears the lever sits between at a standstill.
        if (currentGear >= gearRatios.Length - 1) return;

        ChangeGearByHand(currentGear + 1);
    }

    /// <summary>
    /// Shift down a gear, because the player asked. Below first is the reverse gear, which only goes in
    /// from a standstill: at speed that would be a gearbox full of neutrals, so the press is simply
    /// refused.
    /// </summary>
    public void ShiftDown()
    {
        if (!Manual || car == null) return;
        if (shiftTimer > 0f) return;

        if (currentGear > 0)
        {
            ChangeGearByHand(currentGear - 1);
            return;
        }

        if (currentGear < 0) return;

        if (car.rb.velocity.magnitude < reverseEngageSpeed)
            ChangeGearByHand(-1);
    }

    /// <summary>
    /// A gear change the player made, as opposed to one the box decided on.
    ///
    /// The gear goes in at once - the lever is mechanical - and what the truck feels for the next
    /// <see cref="manualShiftCut"/> seconds is the clutch: no drive at all, from whichever gear, and then
    /// the new gear's pull. The sound and the exhaust are the same cue an automatic change gets, so a hand
    /// shift and an automatic one are the same event as far as the player can hear, which is deliberate: the
    /// difference between the two is who chose, not what it sounds like.
    /// </summary>
    private void ChangeGearByHand(int newGear)
    {
        int oldGear = currentGear;
        currentGear = newGear;

        shiftTimer = manualShiftCut;
        clutchOutUntil = Time.time + manualShiftCut;

        PlayShiftCue();

        // Up drops the revs onto the next gear's, down flares them to meet it - the same nudge the
        // automatic box gets, and what keeps a hand shift from sounding like the engine skipped a step.
        if (newGear > oldGear)
            engineRPM *= shiftRPMDrop;
        else
            engineRPM = Mathf.Min(engineRPM * shiftRPMFlare, redlineRPM);
    }

    /// <summary>
    /// The moment of a change, whichever gearbox asked for it: the lever's clunk, the exhaust's puff, the
    /// dip in the engine's own volume and the nudge through the loop of the engine recording. Restarted
    /// rather than guarded, so a player who changes gear twice in quick succession hears both changes.
    /// </summary>
    private void PlayShiftCue()
    {
        envelopeState = EnvelopeState.Dipping;
        envelopeTimer = 0f;

        if (shiftSource != null)
        {
            shiftSource.pitch = Random.Range(0.94f, 1.06f);
            shiftSource.volume = shiftVolume * SoundSettings.Volume(SoundChannel.Engine);
            shiftSource.PlayOneShot(shiftSource.clip);
        }

        engineSource.time = (engineSource.time + Random.Range(0.1f, 0.3f)) % loopLength;

        StartCoroutine(exhaustParticle());
        exhaustBurst.PlayOneShot(exhaustBurst.clip, SoundSettings.Volume(SoundChannel.Engine));
    }

    private void StartShift(int newGear)
    {
        if (envelopeState != EnvelopeState.Normal) return;

        int oldGear = currentGear;
        currentGear = newGear;

        float t = (float)oldGear / (gearRatios.Length - 1);
        float multiplier = shiftDelayCurve.Evaluate(t);
        shiftTimer = baseShiftDelay * multiplier + 0.15f;

        PlayShiftCue();

        if (newGear > oldGear)
            engineRPM *= shiftRPMDrop;
        else
            engineRPM = Mathf.Min(engineRPM * shiftRPMFlare, redlineRPM);
    }

    private void UpdateVolumeEnvelope()
    {
        switch (envelopeState)
        {
            case EnvelopeState.Dipping:
                envelopeTimer += Time.deltaTime;
                float dipT = envelopeTimer / dipDuration;
                if (dipT >= 1f)
                {
                    dipT = 1f;
                    envelopeState = EnvelopeState.Recovering;
                    envelopeTimer = 0f;
                }
                volumeEnvelope = Mathf.Lerp(1f, dipPercent, Mathf.SmoothStep(0f, 1f, dipT));
                break;

            case EnvelopeState.Recovering:
                envelopeTimer += Time.deltaTime;
                float recT = envelopeTimer / recoveryDuration;
                if (recT >= 1f)
                {
                    recT = 1f;
                    envelopeState = EnvelopeState.Normal;
                }
                volumeEnvelope = Mathf.Lerp(dipPercent, 1f, Mathf.SmoothStep(0f, 1f, recT));
                break;

            default:
                volumeEnvelope = 1f;
                break;
        }

        engineSource.volume = baseVolume * volumeEnvelope * engineVolumeMultiplier * Mix();
    }

    /// <summary>
    /// The share of its authored volume the engine is heard at: the player's own ENGINE setting, and whatever
    /// the camera the player is driving from asks for.
    /// </summary>
    private float Mix()
    {
        return SoundSettings.Volume(SoundChannel.Engine) * viewGain;
    }

    /// <summary>
    /// Moves the engine's view gain towards what the camera in use asks for.
    ///
    /// A positional source is quieter the further away it is, and this game's cameras sit at very different
    /// distances: from above, the truck is a long way off and its engine competes badly with a soundtrack that
    /// plays at one volume everywhere; from inside the cab it is right on top of the listener. Rather than let
    /// the mix drift with the camera, the two ends are authored here (<see cref="aboveViewGain"/>,
    /// <see cref="nearViewGain"/>) and the view is read from the camera's own angle, so a blend between two
    /// cameras passes through the change. Eased, because a camera switch is a cut and the sound should not
    /// cut with it.
    /// </summary>
    private void UpdateViewMix()
    {
        if (viewCamera == null) viewCamera = CameraView.Gameplay();

        float above = CameraView.LookingDown(viewCamera, aboveViewAngle, nearViewAngle);

        float target = Mathf.Lerp(nearViewGain, aboveViewGain, above);

        viewGain = CameraView.Follow(viewGain, target, cameraMixSeconds);
    }

    /// <summary>
    /// The revs the wheels are turning the engine at through the gear that is engaged - the engine's speed
    /// before the throttle's own blip is added.
    ///
    /// Reverse has no ratio in this list, so it borrows first's: the truck's reverse gear is a short one, and
    /// the readout and the pull both want to agree with the gear the lever is in.
    /// </summary>
    private float WheelDrivenRPM()
    {
        float wheelCircumference = 2f * Mathf.PI * 0.33f;
        float wheelRPM = (car.rb.velocity.magnitude / wheelCircumference) * 60f;

        return Mathf.Max(idleRPM, wheelRPM * RatioOf(currentGear) * finalDrive);
    }

    /// <summary>The ratio of a gear: reverse takes first's, and anything out of range is clamped.</summary>
    private float RatioOf(int gear)
    {
        return gearRatios[Mathf.Clamp(gear, 0, gearRatios.Length - 1)];
    }

    /// <summary>How fast the reverse gear is good for, in metres per second - what the wheels read to know
    /// when reverse has run out of speed (see <see cref="WheelPhysics"/>).</summary>
    public float ReverseTopSpeedMS { get { return Mathf.Max(0.5f, reverseTopSpeed / 3.6f); } }

    /// <summary>
    /// What being in the gear in use is worth in force, and whether the engine is labouring in it.
    ///
    /// Three things make a manual gearbox feel like one, and all three are here:
    ///
    /// the gear's ratio, so a low gear pulls harder than a tall one at the same revs;
    /// the revs, so a tall gear at walking pace is off the engine's torque and pulls weakly - the truck can be
    /// bogged down rather than merely slow - and comes on cam as the revs build;
    /// the redline, so a low gear runs out of revs and its pull fades away, which is what makes each gear good
    /// for a certain speed and a change-up worth making.
    ///
    /// In automatic the answer is exactly 1 and nothing here bites: the box has already chosen the gear, which
    /// is what its own shift points are for, and the truck must go on driving exactly as it always has.
    /// </summary>
    private void UpdateDrivePower()
    {
        if (!Manual || car == null)
        {
            powerScale = 1f;
            lugging = false;
            shiftUpSuggested = false;
            shiftDownSuggested = false;
            return;
        }

        float topRatio = RatioOf(gearRatios.Length - 1);
        float firstRatio = RatioOf(0);
        float span = Mathf.Max(0.0001f, firstRatio - topRatio);

        float gearScale = 1f + lowGearPunch * (RatioOf(currentGear) - topRatio) / span;

        if (currentGear < 0)
        {
            // Reverse is a manoeuvring gear, short and flat: it wants to move the truck, not to be managed, so
            // the bogging model is left out of it and only its own top speed holds it back (see WheelPhysics).
            powerScale = gearScale;
            lugging = false;
            shiftUpSuggested = false;
            shiftDownSuggested = false;

            return;
        }

        float crankRPM = WheelDrivenRPM();
        float onCam = Mathf.InverseLerp(idleRPM, Mathf.Max(idleRPM + 1f, luggingFullRPM), crankRPM);
        float lugScale = Mathf.Lerp(luggingTorque, 1f, onCam);

        float outOfRevs = Mathf.InverseLerp(redlineRPM * revLimitStart, redlineRPM, engineRPM);
        float limitScale = 1f - outOfRevs;

        powerScale = gearScale * lugScale * limitScale;

        // Labouring: the player is asking for more than the engine can give where it is - which is what the
        // exhaust is for, and what makes being in the wrong gear visible as well as slow.
        lugging = car.throttleInput > 0.3f && onCam < 0.5f;

        // What the HUD's gear number flashes on. Up near the top of the rev range the gear in use has little
        // left to give, so a change up is worth making; down where the engine is off its torque - or merely
        // near idle in a gear above first - the gear is too tall, and a change down is. Reverse is left out of
        // updating to first and first has nothing below it but reverse, which only goes in from a standstill.
        shiftUpSuggested = currentGear < gearRatios.Length - 1 && engineRPM >= redlineRPM * shiftUpCue;
        shiftDownSuggested = currentGear > 0 && (lugging || engineRPM <= redlineRPM * shiftDownCue);
    }

    private void UpdateEngineRPM()
    {
        float targetRPM = WheelDrivenRPM();

        if (car.throttleInput > 0.05f)
            targetRPM += car.throttleInput * throttleBlipAmount;

        engineRPM = Mathf.Lerp(engineRPM, targetRPM, Time.deltaTime * rpmInertia);
        engineRPM = Mathf.Clamp(engineRPM, idleRPM, redlineRPM);
    }

    private void UpdatePitchAndFilters()
    {
        float rpmNorm = Mathf.InverseLerp(idleRPM, redlineRPM, engineRPM);
        engineSource.pitch = Mathf.Lerp(pitchMin, pitchMax, rpmNorm);
        lowpassFilter.cutoffFrequency = Mathf.Lerp(lowpassCutoffMin, lowpassCutoffMax, rpmNorm);
    }

    private void UpdateOrganicVariation()
    {
        lfoVolumePhase += Time.deltaTime * lfoVolumeSpeed;
        lfoCutoffPhase += Time.deltaTime * lfoCutoffSpeed;

        float volLfo = (Mathf.Sin(lfoVolumePhase * Mathf.PI * 2f) * 0.5f + 0.5f);
        engineSource.volume *= 1f + Mathf.Lerp(-lfoVolumeDepth, lfoVolumeDepth, volLfo);

        float cutoffLfo = (Mathf.Sin(lfoCutoffPhase * Mathf.PI * 2f) * 0.5f + 0.5f);
        lowpassFilter.cutoffFrequency *= 1f + Mathf.Lerp(-lfoCutoffDepth, lfoCutoffDepth, cutoffLfo);

        if (Time.time - lastVariationTime > variationJumpInterval)
        {
            float jump = Random.Range(-0.15f, 0.15f);
            engineSource.time = Mathf.Repeat((engineSource.time + jump), loopLength);
            lastVariationTime = Time.time;
        }
    }

    void OnValidate()
    {
        if (gearRatios != null && gearRatios.Length > 0)
            currentGear = Mathf.Clamp(currentGear, 0, gearRatios.Length - 1);
    }

    private IEnumerator carShiftUp()
    {
        car.isShiftingUp = true;
        yield return new WaitForSeconds(0.5f);
        car.isShiftingUp = false;
    }

    private IEnumerator carShiftDown()
    {
        car.isShiftingDown = true;
        yield return new WaitForSeconds(0.5f);
        car.isShiftingDown = false;
    }

    private IEnumerator exhaustParticle()
    {
        HandleParticle(orangePS, true);
        HandleParticle(blackPS, true);
        HandleParticle(yellowPS, true);
        HandleParticle(orangePS2, true);
        HandleParticle(blackPS2, true);
        HandleParticle(yellowPS2, true);

        yield return new WaitForSeconds(duration);

        HandleParticle(orangePS, false);
        HandleParticle(blackPS, false);
        HandleParticle(yellowPS, false);
        HandleParticle(orangePS2, false);
        HandleParticle(blackPS2, false);
        HandleParticle(yellowPS2, false);
    }

    private void HandleParticle(ParticleSystem ps, bool enable)
    {
        if (enable)
            ps.Play();
        else
            ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }
}
