using UnityEngine;

[RequireComponent(typeof(AudioSource))]
[RequireComponent(typeof(AudioLowPassFilter))]
public class EngineAudio : MonoBehaviour
{
    public CarController car;

    [Header("Current Gear")]
    [SerializeField] private int currentGearDisplay = 1;
    public int CurrentGear => currentGearDisplay;

    [Header("Gear Shift")]
    public AudioSource shiftSource;
    [Range(0f, 1f)] public float shiftVolume = 0.5f;
    public float shiftRPMDrop = 0.60f;
    public float shiftRPMFlare = 1.15f;
    public float shiftCutTime = 0.3f;

    [Header("Shift Timing - Make every shift feel heavy")]
    public float baseShiftDelay = 0.65f;
    public AnimationCurve shiftDelayCurve = new AnimationCurve(
        new Keyframe(0f,   1.1f),
        new Keyframe(0.25f, 1.0f),
        new Keyframe(0.5f,  0.8f),
        new Keyframe(0.75f, 0.6f),
        new Keyframe(1f,   0.45f)
    );

    [Header("Engine Volume Envelope")]
    [Range(0f, 1f)] public float baseVolume = 1f;
    [Range(0f, 1f)] public float dipPercent = 0.5f;
    public float dipDuration = 0.2f;
    public float recoveryDuration = 0.55f;

    [Header("Master Engine Volume Multiplier")]
    [Range(0f, 3f)] public float engineVolumeMultiplier = 3f;

    [Header("Engine RPM")]
    public float idleRPM = 800f;
    public float redlineRPM = 4200f;
    public float rpmInertia = 4f;
    public float throttleBlipAmount = 800f;

    [Header("Gears")]
    public float[] gearRatios = { 4.2f, 3.2f, 2f, 1f, 0.3f };
    public float finalDrive = 5f;
    public float shiftUpRPM = 4000f;
    public float shiftDownRPM = 2000f;

    [Header("Pitch & Tone")]
    public float pitchMin = 0.7f;
    public float pitchMax = 1.45f;
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
    private float engineRPM;
    private int currentGear = 0;
    private float shiftTimer = 0f;
    private float loopLength;
    private float lfoVolumePhase = 0f;
    private float lfoCutoffPhase = 0f;
    private float lastVariationTime = 0f;

    private enum EnvelopeState { Normal, Dipping, Recovering }
    private EnvelopeState envelopeState = EnvelopeState.Normal;
    private float envelopeTimer = 0f;
    private float volumeEnvelope = 1f;

    void Start()
    {
        engineSource = GetComponent<AudioSource>();
        lowpassFilter = GetComponent<AudioLowPassFilter>();
        
        engineSource.loop = true;
        engineSource.playOnAwake = false;
        engineSource.spatialBlend = 1f;
        engineSource.volume = baseVolume * engineVolumeMultiplier;
        engineSource.Play();

        loopLength = engineSource.clip.length;
        currentGear = 0;
        currentGearDisplay = 1;
        lastVariationTime = Time.time;
    }

    void Update()
    {
        HandleShifting();
        UpdateEngineRPM();
        UpdatePitchAndFilters();
        UpdateVolumeEnvelope();
        UpdateOrganicVariation();

        bool isReversing = Vector3.Dot(car.rb.velocity, transform.forward) < -0.5f;
        currentGearDisplay = isReversing ? 0 : currentGear + 1;
    }

    private void HandleShifting()
    {
        if (shiftTimer > 0f)
        {
            shiftTimer -= Time.deltaTime;
            return;
        }

        bool movingReverse = Vector3.Dot(car.rb.velocity, transform.forward) < -0.5f;
        if (movingReverse) return;

        float speedKPH = car.currentSpeedKPH;

        if (engineRPM > shiftUpRPM && currentGear < gearRatios.Length - 1 && speedKPH > 18f)
        {
            StartShift(currentGear + 1);
        }
        else if (engineRPM < shiftDownRPM && currentGear > 0 && speedKPH < 50f)
        {
            StartShift(currentGear - 1);
        }
    }

    private void StartShift(int newGear)
    {
        if (envelopeState != EnvelopeState.Normal) return;

        int oldGear = currentGear;
        currentGear = newGear;

        float t = (float)oldGear / (gearRatios.Length - 1);
        float multiplier = shiftDelayCurve.Evaluate(t);
        shiftTimer = baseShiftDelay * multiplier + 0.15f;

        envelopeState = EnvelopeState.Dipping;
        envelopeTimer = 0f;

        if (shiftSource != null)
        {
            shiftSource.pitch = Random.Range(0.94f, 1.06f);
            shiftSource.volume = shiftVolume;
            shiftSource.PlayOneShot(shiftSource.clip);
        }

        if (newGear > oldGear)
            engineRPM *= shiftRPMDrop;
        else
            engineRPM = Mathf.Min(engineRPM * shiftRPMFlare, redlineRPM);

        engineSource.time = (engineSource.time + Random.Range(0.1f, 0.3f)) % loopLength;
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

        // <-- APPLY MASTER VOLUME MULTIPLIER
        engineSource.volume = baseVolume * volumeEnvelope * engineVolumeMultiplier;
    }

    private void UpdateEngineRPM()
    {
        float wheelCircumference = 2f * Mathf.PI * 0.33f;
        float speedMS = car.rb.velocity.magnitude;
        float wheelRPM = (speedMS / wheelCircumference) * 60f;

        float currentRatio = gearRatios[currentGear] * finalDrive;
        float targetRPM = Mathf.Max(idleRPM, wheelRPM * currentRatio);

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
}
