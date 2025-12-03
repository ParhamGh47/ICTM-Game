using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class EngineAudio : MonoBehaviour
{
    [Header("References")]
    public CarController car;
    private AudioSource src;

    [Header("Engine RPM Settings")]
    public float idleRPM = 800f;
    public float redlineRPM = 4200f;
    public float rpmInertia = 4f;

    [Header("Gears")]
    public float[] gearRatios = { 2.8f, 1.9f, 1.4f, 1.1f, 0.85f };
    public float finalDrive = 3.9f;
    public float shiftUpRPM = 3800f;
    public float shiftDownRPM = 1500f;
    public int currentGear = 1;

    private float engineRPM;
    private float wheelRPM;

    [Header("Audio")]
    public float pitchMin = 0.8f;
    public float pitchMax = 1.35f;

    [Header("Granular Synthesis")]
    public float grainSize = 0.12f;
    public float grainJitter = 0.03f;
    private float grainPos;
    private float loopLen;

    void Start()
    {
        src = GetComponent<AudioSource>();
        src.loop = false;
        src.spatialBlend = 1f;
        src.volume = 0.7f;

        loopLen = src.clip.length;
        grainPos = loopLen * 0.2f;
    }

    void Update()
    {
        HandleGears();
        UpdateEngineRPM();
        UpdatePitch();

        if (!src.isPlaying)
            PlayGrain();
    }

    private void HandleGears()
    {
        if (currentGear < gearRatios.Length - 1 && engineRPM > shiftUpRPM)
            currentGear++;

        if (currentGear > 0 && engineRPM < shiftDownRPM)
            currentGear--;
    }

    private void UpdateEngineRPM()
    {
        float speedMS = car.rb.velocity.magnitude;
        float wheelCircumference = 2f * Mathf.PI * 0.33f;

        wheelRPM = (speedMS / wheelCircumference) * 60f;

        float gearRatio = gearRatios[currentGear] * finalDrive;

        float targetRPM = Mathf.Max(idleRPM, wheelRPM * gearRatio);
        
        if (car.throttleInput > 0)
            targetRPM += car.throttleInput * 800f;

        engineRPM = Mathf.Lerp(engineRPM, targetRPM, Time.deltaTime * rpmInertia);

        engineRPM = Mathf.Clamp(engineRPM, idleRPM, redlineRPM);
    }

    private void UpdatePitch()
    {
        float t = Mathf.InverseLerp(idleRPM, redlineRPM, engineRPM);
        float pitch = Mathf.Lerp(pitchMin, pitchMax, t);
        src.pitch = pitch;
    }

    private void PlayGrain()
    {
        float jitter = Random.Range(-grainJitter, grainJitter);
        grainPos += grainSize + jitter;

        if (grainPos > loopLen)
            grainPos -= loopLen;

        src.time = grainPos;
        src.Play();
    }
}
