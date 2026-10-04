using UnityEngine;

public class CollisionSound : MonoBehaviour
{
    [Header("References")]
    public CarController car;
    public AudioSource audioSource;

    [Header("Collision Clips - the world")]
    [Tooltip("Trees, buildings, billboards, barriers, kerbs, the ground: anything without an ImpactMaterial " +
             "of its own. The three are the same impact at three strengths, so they are normally the same " +
             "clip played at three volumes.")]
    public AudioClip softHitClip;
    public AudioClip mediumHitClip;
    public AudioClip hardHitClip;

    [Header("Collision Clips - other things")]
    [Tooltip("A thin light sheet: the blinder. Left empty, one is made at runtime - see ImpactClip.")]
    public AudioClip sheetHitClip;

    [Tooltip("A big flat sign, larger than a blinder: share-the-road, stop. Left empty, one is made at runtime " +
             "- see ImpactClip.")]
    public AudioClip panelHitClip;

    [Tooltip("A hollow container: a barrel, a crate, a cone. Left empty, one is made at runtime - see " +
             "ImpactClip.")]
    public AudioClip barrelHitClip;

    [Tooltip("Loose junk of some weight: a log, a waste bin, a trash container. The voice nearest the " +
             "world's own - left empty, one is made at runtime. See ImpactClip.")]
    public AudioClip debrisHitClip;

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
             "surface may lean towards the truck's own up and still count as underneath. 0 switches it off. " +
             "Anything carrying an ImpactMaterial is exempt from this - see OnCollisionEnter.")]
    [Range(0f, 89f)]
    public float undersideAngle = 55f;

    private float lastPlayTime = -999f;
    private Vector3 lastVelocity;

    void Start()
    {
        if (car == null)
            car = GetComponent<CarController>();
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        // The sounds that have no recording in the project are made here, the way the boost sounds are.
        // An inspector value still wins, so a real recording can be dropped in later without touching this.
        if (sheetHitClip == null) sheetHitClip = ImpactClip.Sheet;
        if (panelHitClip == null) panelHitClip = ImpactClip.Panel;
        if (barrelHitClip == null) barrelHitClip = ImpactClip.Barrel;
        if (debrisHitClip == null) debrisHitClip = ImpactClip.Debris;

        lastVelocity = Vector3.zero;
    }

    void FixedUpdate()
    {
        lastVelocity = car.rb.velocity;
    }

    void OnCollisionEnter(Collision collision)
    {
        // A target being killed is not an impact: the kill has its own sound and effect. Everything else
        // speaks - including the things tagged Interactive. The barrel and the cone are exactly the loose
        // objects a hit should be heard on, and that tag is about the truck not taking damage from them
        // (see TruckDamage), not about whether they make a noise.
        if (collision.gameObject.CompareTag("Adamak"))
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

        // And then the sound itself is chosen by what was hit. The strength and the volume above are the
        // same whatever it was - a barrel struck at 40 is a barrel struck at 40 - so this only ever exchanges
        // one clip for another of the same kind.
        //
        // What was hit is asked for once, and both the clip and the exemption below are decided from that one
        // answer. They used to be decided separately - the clip by looking the material up again, the
        // exemption by asking whether a clip came back - which quietly made the exemption depend on the clip
        // existing: a marked object whose voice had not been built yet fell back to the world's clip, was not
        // exempt, and if it was low enough to be struck from underneath it made no sound at all. Now the
        // marker alone is what exempts it, so a marked object is always heard.
        ImpactMaterial material = MaterialFor(collision.collider);
        AudioClip byMaterial = ClipFor(material);

        if (byMaterial != null)
            chosenClip = byMaterial;

        // A contact from underneath is the truck going *over* something rather than into it - the road, a
        // kerb, a rumble strip, the ground a jump lands on - and is left silent. The cooldown is deliberately
        // not spent on a silent one, so the crash that follows a landing still speaks.
        //
        // Anything carrying an ImpactMaterial is exempt, and that exemption is not a nicety: a log and a
        // waste bin are flat and low enough that the truck's own collider meets them from below, so the angle
        // rule read every one of those hits as the truck driving over them and they made no sound at all.
        // Whatever the player is meant to hear struck, they hear however low it was struck - the rule is
        // about the world the truck drives on, and that world carries no marker.
        bool underneath = ComesFromUnderneath(collision);

        if (!underneath || material != null)
        {
            audioSource.pitch = Random.Range(minPitch, maxPitch);
            audioSource.PlayOneShot(chosenClip, chosenVolume);

            // Only a sound that was actually played spends the cooldown. Going over a kerb is not a hit, and
            // it must not swallow the crash that follows it - which is what the note above says, and what
            // keeping this inside the branch is what makes true. It used to be set outside, on every
            // contact, silent or not.
            lastPlayTime = now;
        }

        // The dust is for the world - a kerb, a barrier, a landing - and not for things that bring their
        // own effect: a target dying, or the roadside signs the truck is not meant to feel crashing into.
        // Those carry NoImpactParticles, looked for up from the collider that was actually touched, so it
        // does not matter which part of them the truck caught.
        if (impactParticlePrefab != null && collision.contacts.Length > 0 &&
            collision.collider.GetComponentInParent<NoImpactParticles>() == null)
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
    }


    /// <summary>
    /// What the truck actually touched, or null if it is the world.
    ///
    /// The collider that reports the contact is not necessarily the object itself - a sign keeps its
    /// colliders on a child, and a car is hit on a door or a bumper - so the answer is looked for up the
    /// hierarchy, which is what <see cref="ImpactMaterial"/> is put on the root for.
    /// </summary>
    private static ImpactMaterial MaterialFor(Collider collider)
    {
        return collider == null ? null : collider.GetComponentInParent<ImpactMaterial>();
    }

    /// <summary>
    /// The clip for what was touched, or null to keep the world's own three.
    /// </summary>
    private AudioClip ClipFor(ImpactMaterial material)
    {
        if (material == null) return null;

        switch (material.kind)
        {
            case ImpactKind.Sheet: return sheetHitClip;
            case ImpactKind.Panel: return panelHitClip;
            case ImpactKind.Barrel: return barrelHitClip;
            case ImpactKind.Debris: return debrisHitClip;
            default: return null;
        }
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
    ///
    /// This is only ever asked about something with no <see cref="ImpactMaterial"/>: a log or a waste bin is
    /// low enough that the truck's collider meets it from below, and OnCollisionEnter exempts those. What is
    /// left for it to judge is the world - the road, a kerb, a rumble strip, the ground - which is the one
    /// thing the truck is meant to travel over rather than into.
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
}
