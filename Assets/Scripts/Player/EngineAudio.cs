using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class EngineAudio : MonoBehaviour
{
    [Header("References")]
    public CarController car;
    private AudioSource src;

    [Header("Engine RPM Settings")]
    public float idleRPM = 100f;
    public float maxRPM = 800f;
    public float rpmSmooth = 4f;

    [Header("Granular Settings")]
    public float grainSize = 0.12f;
    public float grainJitter = 0.04f;
    public float pitchRange = 1.5f;
    public float loadBoost = 0.2f;

    private float currentRPM;
    private float grainPosition;

    private float loopLength;

    void Start()
    {
        src = GetComponent<AudioSource>();

        src.loop = false;
        src.playOnAwake = false;
        src.spatialBlend = 1f;
        src.volume = 0.7f;

        loopLength = src.clip.length;

        grainPosition = loopLength * 0.3f;
    }

    void Update()
    {
        if (!src.isPlaying)
            PlayNewGrain();

        UpdateRPM();
        UpdatePitch();
    }

    private void UpdateRPM()
    {
        float throttle = Mathf.Max(0, car.throttleInput);

        float targetRPM = Mathf.Lerp(idleRPM, maxRPM, throttle);

        currentRPM = Mathf.Lerp(currentRPM, targetRPM, Time.deltaTime * rpmSmooth);
    }

    private void UpdatePitch()
    {
        float rpmPercent = Mathf.InverseLerp(idleRPM, maxRPM, currentRPM);

        float pitch = Mathf.Lerp(1f, pitchRange, rpmPercent);

        if (car.throttleInput > 0.1f)
            pitch += loadBoost * car.throttleInput;

        src.pitch = pitch;
    }

    private void PlayNewGrain()
    {
        float jitter = Random.Range(-grainJitter, grainJitter);

        grainPosition += grainSize + jitter;

        if (grainPosition > loopLength)
            grainPosition -= loopLength;
        if (grainPosition < 0)
            grainPosition += loopLength;

        src.time = Mathf.Clamp(grainPosition, 0, loopLength - 0.05f);
        src.Play();
    }
}
