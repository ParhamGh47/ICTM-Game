using UnityEngine;

public class CollisionSound : MonoBehaviour
{
    [Header("References")]
    public CarController car;                // your existing controller
    public AudioSource audioSource;          // AudioSource for collision SFX

    [Header("Collision Clips")]
    public AudioClip softHitClip;
    public AudioClip mediumHitClip;
    public AudioClip hardHitClip;

    [Header("Impact Settings")]
    public float softImpactThreshold = 2f;   // speed (m/s) for soft hits
    public float mediumImpactThreshold = 6f; // speed for medium hits
    public float hardImpactThreshold = 12f;  // speed for hard hits

    [Header("Volume Settings")]
    public float softVolume = 0.25f;
    public float mediumVolume = 0.45f;
    public float hardVolume = 0.8f;

    [Header("Pitch Randomization")]
    public float minPitch = 0.9f;
    public float maxPitch = 1.1f;

    [Header("Cooldown")]
    public float cooldownTime = 0.25f;       // prevents spamming collisions

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
        // store velocity BEFORE collision
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

        // determine type of hit
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
            // too small to play anything
            return;
        }

        if (chosenClip == null)
            return;

        audioSource.pitch = Random.Range(minPitch, maxPitch);
        audioSource.PlayOneShot(chosenClip, chosenVolume);

        lastPlayTime = now;
    }
}
