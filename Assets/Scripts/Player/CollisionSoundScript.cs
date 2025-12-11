using UnityEngine;

public class DynamicCollisionSound : MonoBehaviour
{
    [Header("References")]
    public CarController car;
    public AudioSource audioSource;

    [Header("Collision Clips")]
    public AudioClip softHitClip;
    public AudioClip mediumHitClip;
    public AudioClip hardHitClip;

    [Header("Impact Settings")]
    public float softImpactThreshold = 2f;
    public float mediumImpactThreshold = 6f;
    public float hardImpactThreshold = 12f;

    [Header("Volume Settings")]
    public float softVolume = 0.25f;
    public float mediumVolume = 0.45f;
    public float hardVolume = 0.8f;

    [Header("Pitch Randomization")]
    public float minPitch = 0.9f;
    public float maxPitch = 1.1f;

    [Header("Cooldown")]
    public float cooldownTime = 0.25f;

    private float lastPlayTime = -999f;
    private Vector3 lastVelocity;

    void Start()
    {
        if (car == null)
            car = GetComponent<CarController>();

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        lastVelocity = Vector3.zero;
    }

    void FixedUpdate()
    {
        lastVelocity = car.rb.velocity;
    }

    void OnCollisionEnter(Collision collision)
    {
        float now = Time.time;
        if (now - lastPlayTime < cooldownTime)
            return;

        float impactSpeed = lastVelocity.magnitude;

        AudioClip chosenClip;
        float chosenVolume;

        if (impactSpeed > hardImpactThreshold)
        {
            chosenClip = hardHitClip;
            chosenVolume = hardVolume;
        }
        else if (impactSpeed > mediumImpactThreshold)
        {
            chosenClip = mediumHitClip;
            chosenVolume = mediumVolume;
        }
        else if (impactSpeed > softImpactThreshold)
        {
            chosenClip = softHitClip;
            chosenVolume = softVolume;
        }
        else
        {
            return;
        }

        if (chosenClip == null)
            return;

        audioSource.pitch = Random.Range(minPitch, maxPitch);
        audioSource.PlayOneShot(chosenClip, chosenVolume);

        lastPlayTime = now;
    }
}
