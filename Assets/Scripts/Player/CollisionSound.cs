using UnityEngine;

public class CollisionSound : MonoBehaviour
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
    public float cooldownTime = 0.5f;

    [Header("Particle Effects")]
    [Tooltip("Prefab with ParticleSystem component to instantiate on impact")]
    public GameObject impactParticlePrefab;

    [Tooltip("Minimum and maximum scale multiplier for the particle effect based on impact strength")]
    public Vector2 particleScaleRange = new Vector2(0.7f, 1.5f);

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
        if (collision.gameObject.CompareTag("Adamak") || collision.gameObject.CompareTag("Interactive"))
            return;

        float now = Time.time;
        if (now - lastPlayTime < cooldownTime)
            return;

        float impactSpeed = lastVelocity.magnitude;

        AudioClip chosenClip = null;
        float chosenVolume = 0f;
        float impactStrength = 0f; // 0 = soft, 0.5 = medium, 1 = hard

        if (impactSpeed > hardImpactThreshold)
        {
            chosenClip = hardHitClip;
            chosenVolume = hardVolume;
            impactStrength = 1f;
        }
        else if (impactSpeed > mediumImpactThreshold)
        {
            chosenClip = mediumHitClip;
            chosenVolume = mediumVolume;
            impactStrength = 0.5f;
        }
        else if (impactSpeed > softImpactThreshold)
        {
            chosenClip = softHitClip;
            chosenVolume = softVolume;
            impactStrength = 0f;
        }
        else
        {
            return;
        }

        if (chosenClip == null)
            return;

        audioSource.pitch = Random.Range(minPitch, maxPitch);
        audioSource.PlayOneShot(chosenClip, chosenVolume);

        if (impactParticlePrefab != null && collision.contacts.Length > 0)
        {
            ContactPoint contact = collision.contacts[0];
            GameObject particles = Instantiate(impactParticlePrefab, contact.point, Quaternion.LookRotation(contact.normal));

            float scaleMultiplier = Mathf.Lerp(particleScaleRange.x, particleScaleRange.y, impactStrength);
            particles.transform.localScale = Vector3.one * scaleMultiplier;

            ParticleSystem ps = particles.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                Destroy(particles, ps.main.duration + ps.main.startLifetime.constantMax);
            }
            else
            {
                Destroy(particles, 3f);
            }
        }

        lastPlayTime = now;
    }
}
