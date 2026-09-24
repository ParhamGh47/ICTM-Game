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

    [Header("Underneath")]
    [Tooltip("Hitting something with the truck's body is what this sound is for; going over something is " +
             "not. A contact that faces up into the truck - the road, a kerb, a rumble strip, the ground a " +
             "jump lands on - comes from underneath it, and is left silent. In degrees: how far a contact's " +
             "surface may lean towards the truck's own up and still count as underneath. 0 switches it off.")]
    [Range(0f, 89f)]
    public float undersideAngle = 55f;

    [Header("Targets")]
    [Tooltip("Tag of the things the truck is meant to run into. A target's own effect is the show when it " +
             "is hit, so no impact particle is spawned for one. The tag is looked for up the collider's " +
             "parents, because a target carries it on the prefab's root and its colliders on its parts.")]
    public string noParticleTag = "Adamak";

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
        float impactStrength = 0f;

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

        // The dust still kicks up for a contact from underneath - that is what makes a landing land - but
        // the thud does not: it is the truck going over something rather than into it. The cooldown is
        // deliberately not spent on a silent one, so the crash that follows a landing still speaks.
        if (!ComesFromUnderneath(collision))
        {
            audioSource.pitch = Random.Range(minPitch, maxPitch);
            audioSource.PlayOneShot(chosenClip, chosenVolume);
        }

        if (impactParticlePrefab != null && collision.contacts.Length > 0 &&
            !CarriesTag(collision.collider.transform, noParticleTag))
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


    /// <summary>
    /// Whether the collision came up at the truck from underneath - the road, a kerb, a rumble strip, the
    /// ground a jump lands on - rather than squarely into its nose or its side.
    ///
    /// A contact's normal points out of the surface the truck touched, so a surface below the truck pushes
    /// roughly straight up into it. That is the angle measured here, against the truck's own up rather than
    /// the world's, so a truck that is leant over, on its side or in mid-air still reads correctly. Every
    /// contact has to be underneath for the hit to count as one, so a single square-on contact at the bumper
    /// is still a real hit.
    /// </summary>
    private bool ComesFromUnderneath(Collision collision)
    {
        if (undersideAngle <= 0f || collision.contacts.Length == 0)
            return false;

        Vector3 up = transform.up;

        for (int i = 0; i < collision.contacts.Length; i++)
        {
            if (Vector3.Angle(collision.contacts[i].normal, up) > undersideAngle)
                return false;
        }

        return true;
    }


    /// <summary>
    /// Whether anything up this collider's parents carries the tag. A target keeps its tag on the prefab's
    /// root and its colliders on its own parts, so the collider the truck actually touched never has it.
    /// </summary>
    private static bool CarriesTag(Transform part, string tag)
    {
        if (string.IsNullOrEmpty(tag) || part == null)
            return false;

        Transform current = part;
        while (current != null)
        {
            if (current.CompareTag(tag)) return true;
            current = current.parent;
        }

        return false;
    }
}
