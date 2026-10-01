using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The damage a passing car carries, for the look of it: a hit bends the panel it landed on, and enough of
/// them take it off the car to fall on the road as its own piece of wreckage.
///
/// It is the same idea as the player truck's damage - it is only ever the car's own model that is touched,
/// so nothing here can change how the car drives: the mesh a part is drawn as has no collider and no
/// controller reads it, and the car's own collision is a box on a separate object that is deliberately
/// never a part. What is different is what the car is: a passing car is meant to be knocked about - it is
/// the traffic the level is played through - so its own parts are set far more fragile than the player's,
/// and everything except the shell comes away. Every one of its panels is a breakable part; its <c>Body</c>
/// is not in the list, so the car keeps its shape and its collision however hard it is hit.
///
/// A part is found by name when the level starts, because the car's parts come from an imported model
/// rather than a hand-built hierarchy, so the name is the only handle there is - the same one the paint
/// system uses. The four cars name their parts 'Body', 'Bumper', 'Door', 'Headlights', 'Front Wheels' and
/// 'Rear Wheels', and are told apart by the list each prefab carries rather than by anything in this
/// script.
///
/// A hit is measured from the two bodies' relative speed at the contact rather than from the car's own
/// velocity. A passing car is driven by having its position set every physics step, so its own velocity is
/// the drive's, and every contact with the player would read as the car's own headlong pace however gently
/// it was nudged.
///
/// When the headlights are knocked off, the car's lights go out with them - see
/// <see cref="AICarController.KillHeadlights"/>. A car whose lamps have left it has nothing left to shine
/// through, and a pool of light travelling down the road ahead of a car with no lamps on it reads as a
/// car with its lights on, which is the one thing it does not have any more.
///
/// When a part comes off it stops being part of the car. It is given its own box collider worked out from
/// what it was drawn as, its own rigidbody, the speed the car had at the moment of the crash and some
/// spin, so it lands, slides and tumbles on the road like the wreckage it is. Collisions between the debris
/// and the car it came off are turned off, so the two never grind against each other, and the debris is
/// cleared after a while so a long level does not fill up with it.
///
/// Impacts that should not count are left alone by the same rules the crash effects use: the targets the
/// player is meant to hit, the roadside signs, and contacts that come up at the car from underneath - the
/// road, a kerb - rather than squarely into its nose or its side.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public class PassingCarDamage : MonoBehaviour
{
    [Header("Master")]
    [Tooltip("Whether a passing car can be dented, bent and broken at all. Off, the car is exactly the car " +
             "it was, however hard it is hit.")]
    public bool enableDamage = true;

    /// <summary>
    /// One breakable part: what it is called, and how much it takes.
    ///
    /// Toughness is the whole of "how breakable this is". Every impact divides by it, so a part twice as
    /// tough needs twice the crash to bend as far and twice the crash to come off. It is the one number to
    /// change to make a part stubborn or fragile, and the whole point of a passing car's list is that these
    /// numbers are a small fraction of the player's: the traffic is meant to come apart.
    /// </summary>
    [System.Serializable]
    public class BreakablePart
    {
        [Tooltip("Matched against the name of a child of the car - case-insensitive, and a substring is " +
                 "enough, so 'Wheels' finds both wheel meshes. The part has to be drawn with a mesh: an " +
                 "effect parented under a matching name is left alone, and so is the car's own collision " +
                 "box. A name that matches something drawn inside another part is left to that part.")]
        public string name;

        [Tooltip("How much punishment the part takes compared with the car's standard. 1 is the standard " +
                 "for a passing car - already a small fraction of the player's truck - so 0.5 comes off at " +
                 "half the crash and 2 at twice.")]
        [Range(0.05f, 5f)]
        public float toughness = 0.5f;

        [Tooltip("How much further this part bends and shifts than the standard, on the same hits. It does " +
                 "not make the part any easier to break - toughness does that.")]
        [Range(0f, 4f)]
        public float bendScale = 1f;

        [Tooltip("Other pieces of the model that are drawn on this part and should move and come off with " +
                 "it, matched by the same rules as the name above. The truck's plate lettering is a model of " +
                 "its own, so without this it would be left hanging in the air when the plate came away.")]
        public string[] attached;
    }

    [Header("Parts")]
    [Tooltip("Everything on a passing car that can be bent and broken, and how breakable each one is. Its " +
             "shell - the part named 'Body' - is deliberately not here: the traffic is meant to lose its " +
             "panels and its wheels, and to still be a car driving down the road once it has.")]
    public BreakablePart[] parts =
    {
        new BreakablePart { name = "Bumper",       toughness = 0.45f },
        new BreakablePart { name = "door",         toughness = 0.5f },
        new BreakablePart { name = "HeadLights",   toughness = 0.35f },   // the lamp lenses
        new BreakablePart { name = "Front Wheels", toughness = 0.8f, bendScale = 0.4f },
        new BreakablePart { name = "Rear Wheels",  toughness = 0.8f, bendScale = 0.4f },
    };

    [Header("Impacts")]
    [Tooltip("The slowest crash that does anything at all, in metres per second, measured along the " +
             "surface it hit rather than by how fast the two were closing: brushing along a wall at speed " +
             "is not a crash into it.")]
    public float minimumImpactSpeed = 2.5f;

    [Tooltip("How much of a part's health one metre per second of impact takes. Higher than the player's " +
             "truck, because a passing car is meant to be the thing that comes apart.")]
    public float damagePerSpeed = 0.07f;

    [Tooltip("A single crash this hard in metres per second takes a part off in one go, whatever it had " +
             "left. Well below the player's, for the same reason.")]
    public float breakImpactSpeed = 10f;

    [Tooltip("A crash longer than this in seconds is a different crash, so one prang cannot be counted " +
             "over and over by its many contacts.")]
    public float impactCooldown = 0.15f;

    [Tooltip("How far a contact's surface may lean towards the car's own up and still count as coming at " +
             "it from underneath - the road, a kerb, a ramp - rather than being a crash. In degrees, and " +
             "the same rule the crash effects use.")]
    [Range(0f, 89f)]
    public float undersideAngle = 55f;

    [Header("Bending")]
    [Tooltip("How far a part can bend, in degrees, from where the model put it - reached when it is one " +
             "hit away from coming off. The part's own Bend Scale multiplies it.")]
    public float maxBendDegrees = 16f;

    [Tooltip("How far a part can shift, in metres, from where the model put it - reached at the same point " +
             "as the bend. The part's own Bend Scale multiplies it.")]
    public float maxBendOffset = 0.04f;

    [Header("Knock-on Damage")]
    [Tooltip("The chance that a hit hard enough to hurt one part also shakes a neighbour, 0 to 1. The " +
             "neighbour takes a fraction of the damage, so the crash that bends a car's bumper can clip " +
             "the lamp beside it as well.")]
    [Range(0f, 1f)]
    public float knockOnChance = 0.4f;

    [Tooltip("How near a part has to be to the hit, in metres, to be shaken by it. Measured to the part's " +
             "own outline, so a part that shares an edge with the one that was hit is always in range.")]
    public float knockOnRadius = 0.6f;

    [Tooltip("How much of the damage a shaken part takes. Keep it below 1, so the part that was actually " +
             "hit is still the one that comes off first.")]
    [Range(0f, 1f)]
    public float knockOnFraction = 0.5f;

    [Header("Debris")]
    [Tooltip("How long a broken-off part is left lying on the road before it is cleared, in seconds. 0 " +
             "leaves it there for the rest of the level. Shorter than the player's: there are a great many " +
             "passing cars, and a level's road would otherwise collect all of them.")]
    public float debrisLifetime = 12f;

    [Tooltip("How much the debris is thrown, as a fraction of the car's own speed at the moment of the " +
             "crash. 1 throws it at the speed the car was doing.")]
    [Range(0f, 2f)]
    public float debrisThrow = 0.5f;

    [Tooltip("How hard a broken-off part is pushed away from the car, in metres per second. Small: it is " +
             "only there so a piece that comes off at the nose separates from the car rather than sitting " +
             "in its path.")]
    public float debrisKick = 2f;

    [Tooltip("The slowest and fastest spin the debris is thrown with, in degrees per second.")]
    public float debrisMinSpin = 90f;
    public float debrisMaxSpin = 420f;

    [Header("Logging")]
    [Tooltip("Whether to log what the model actually offers as breakable parts. Said once for the session " +
             "rather than once for every car on the road, because a level holds a thousand of them. It is " +
             "the quickest way to see a part name that matched nothing.")]
    public bool logDamage = true;

    [Tooltip("Whether every hit a part takes is logged as well - what it was, how hard the crash was, what " +
             "it did and whether that took it off. Off by default: a level's traffic takes a great many " +
             "hits, and they would bury the console. Worth turning on to tune one car.")]
    public bool logHits = false;

    // ---------------------------------------------------------------- a part

    private class Part
    {
        public Transform transform;

        // Where the model put it, so bending is always measured from there rather than from the last bend,
        // which would otherwise walk the part further out with every hit.
        public Quaternion homeRotation;
        public Vector3 homePosition;

        public Vector3 bendOffset;      // in the part's parent's space
        public float bendDegrees;       // about the impact axis
        public Vector3 bendAxis;        // in the part's parent's space

        public float toughness;
        public float bendScale;

        // Whether this part is the car's headlamp lens, whose loss puts the car's lights out.
        public bool headlampLens;

        public float damage;
        public bool broken;

        public Renderer[] renderers;
    }

    // How much punishment a part takes before it comes off, over every impact it has taken. 1 by design:
    // every unit of damage is <see cref="damagePerSpeed"/> metres per second of impact, divided by the
    // part's own toughness. It is not a setting because toughness is the dial that matters - it says how
    // hard this part is against every other part on the car, and against the player's truck.
    private const float Health = 1f;

    private readonly List<Part> foundParts = new List<Part>();

    // Names that matched something drawn inside a part already found, and were therefore left as part of
    // it. Kept only to be reported at startup, so a name that looks like it should be a part of its own
    // says what became of it.
    private readonly List<string> nestedMatches = new List<string>();

    private Rigidbody rb;
    private Collider[] ownColliders;
    private AICarController aiCar;

    private float lastImpactTime = -999f;

    // Said once for the session rather than once for each of a level's cars, which all find the same parts.
    private static bool reportedParts;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        ownColliders = GetComponentsInChildren<Collider>(true);
        aiCar = GetComponentInChildren<AICarController>(true);

        Discover();

        if (logDamage) LogParts();
    }

    // ---------------------------------------------------------------- lamps

    /// <summary>
    /// Puts the car's lights out when the lens they shine through leaves it. The car's own lights are the
    /// <see cref="AICarController"/>'s business, and it holds them itself, so this is all it takes.
    /// </summary>
    private void KillLamps(Part part)
    {
        if (part.headlampLens && aiCar != null) aiCar.KillHeadlights();
    }

    /// <summary>
    /// Says which of the settings above actually matched something on the model. A name that matches
    /// nothing is the usual reason a part never breaks, and it is not visible from the inspector.
    /// </summary>
    private void LogParts()
    {
        if (reportedParts) return;
        reportedParts = true;

        if (foundParts.Count == 0)
        {
            Debug.LogWarning("[PassingCarDamage] No breakable part was found on '" + name +
                             "'. Check the part names against the car model's own object names.", this);
            return;
        }

        System.Text.StringBuilder text = new System.Text.StringBuilder();
        text.Append("[PassingCarDamage] ").Append(foundParts.Count).Append(" breakable part(s) on a '")
            .Append(name).Append("':");

        for (int i = 0; i < foundParts.Count; i++)
        {
            Part part = foundParts[i];
            text.Append("\n    ").Append(part.transform != null ? part.transform.name : "?")
                .Append("  toughness ").Append(part.toughness.ToString("0.##"));
        }

        for (int i = 0; i < nestedMatches.Count; i++)
            text.Append("\n    (drawn inside its part, so not a part of its own: ").Append(nestedMatches[i])
                .Append(")");

        Debug.Log(text.ToString(), this);
    }

    // ---------------------------------------------------------------- discovery

    /// <summary>
    /// Finds the model's parts by name. They come from an imported file rather than a hand-built hierarchy,
    /// so the name is the only handle there is - the same one the paint system uses.
    /// </summary>
    private void Discover()
    {
        if (parts == null) return;

        Transform[] all = GetComponentsInChildren<Transform>(true);

        for (int n = 0; n < parts.Length; n++)
        {
            BreakablePart wanted = parts[n];
            if (wanted == null || string.IsNullOrEmpty(wanted.name)) continue;

            for (int i = 0; i < all.Length; i++)
            {
                Transform child = all[i];
                if (child == transform) continue;
                if (child.name.IndexOf(wanted.name, System.StringComparison.OrdinalIgnoreCase) < 0) continue;

                // Found already: the same object can match more than one name.
                if (AlreadyFound(child)) continue;

                // Drawn with a mesh, rather than an effect that happens to be named like a part: a car's
                // exhaust smoke is parented under a name that can contain 'exhaust', and a particle system
                // is not something the car can lose.
                if (!HasMesh(child.GetComponentsInChildren<Renderer>(true))) continue;

                // Anything carrying the car's own collision is left alone: the box the car is hit through
                // is not a thing that can come off, because moving it or throwing it away would change how
                // the car itself collides. A car's shell is a box on an object of its own, which this
                // catches as well as the name of the shell not being in the list above.
                if (child.GetComponent<Collider>() != null) continue;

                // Drawn inside a part that is already found, so it is a piece of that part rather than a
                // piece of its own: taken as well, the same geometry would bend twice - once with the part
                // it belongs to, and once more on its own pivot.
                Part around = PartAround(child);
                if (around != null)
                {
                    nestedMatches.Add(wanted.name + " matched '" + child.name + "', which is drawn inside '" +
                                      around.transform.name + "'");
                    continue;
                }

                // Whatever is drawn on this part is moved under it, so the two become one piece: a plate's
                // lettering bends with the plate and comes away with it. Done before the renderers are read
                // again, so the piece's outline includes what was moved onto it.
                Part part = new Part();
                part.transform = child;
                Attach(child, wanted.attached);
                part.toughness = Mathf.Max(0.05f, wanted.toughness);
                part.bendScale = Mathf.Max(0f, wanted.bendScale);
                part.renderers = child.GetComponentsInChildren<Renderer>(true);

                // The lens the car's lights shine through, which has no reference to be found by: the spot
                // lights that light it are the car's own. So the name is what says this part is the lamp.
                if (aiCar != null &&
                    child.name.IndexOf("headlight", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    part.headlampLens = true;

                foundParts.Add(part);
            }
        }

        Remember();
    }

    /// <summary>
    /// The part a candidate is drawn inside, if a part has already been found around it, rather than the
    /// candidate being a piece of its own.
    /// </summary>
    private Part PartAround(Transform candidate)
    {
        for (int i = 0; i < foundParts.Count; i++)
        {
            Transform part = foundParts[i].transform;

            if (part != null && candidate.IsChildOf(part)) return foundParts[i];
        }

        return null;
    }

    private static bool HasMesh(Renderer[] renderers)
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] is MeshRenderer) return true;
            if (renderers[i] is SkinnedMeshRenderer) return true;
        }

        return false;
    }

    /// <summary>
    /// Moves the pieces that belong to a part under it, so they are one piece from here on: they take the
    /// part's bend and come off with it. Their places in the world are kept, so nothing moves on screen.
    /// </summary>
    private void Attach(Transform part, string[] attached)
    {
        if (attached == null) return;

        Transform[] all = GetComponentsInChildren<Transform>(true);

        for (int n = 0; n < attached.Length; n++)
        {
            string wanted = attached[n];
            if (string.IsNullOrEmpty(wanted)) continue;

            for (int i = 0; i < all.Length; i++)
            {
                Transform child = all[i];

                if (child == null || child == part || child == transform) continue;
                if (child.IsChildOf(part)) continue;
                if (child.name.IndexOf(wanted, System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (AlreadyFound(child)) continue;

                child.SetParent(part, true);
            }
        }
    }

    private bool AlreadyFound(Transform candidate)
    {
        for (int i = 0; i < foundParts.Count; i++)
            if (foundParts[i].transform == candidate) return true;

        return false;
    }

    private void Remember()
    {
        for (int i = 0; i < foundParts.Count; i++)
        {
            foundParts[i].homePosition = foundParts[i].transform.localPosition;
            foundParts[i].homeRotation = foundParts[i].transform.localRotation;
        }
    }

    // ---------------------------------------------------------------- impacts

    private void OnCollisionEnter(Collision collision)
    {
        if (!enableDamage) return;
        if (rb == null || collision.contacts.Length == 0) return;

        // The targets the player is meant to hit and the roadside signs bring their own effect and are not
        // meant to damage a car, and a contact from underneath is the road or a kerb rather than a crash.
        if (collision.gameObject.CompareTag("Adamak")) return;
        if (collision.gameObject.CompareTag("Interactive")) return;
        if (collision.collider != null &&
            collision.collider.GetComponentInParent<NoImpactParticles>() != null) return;

        if (Time.time - lastImpactTime < impactCooldown) return;

        // How hard the two arrived, along the surface they met. Measured from the pair's relative speed
        // rather than from the car's own velocity, because a passing car is driven by having its position
        // set every step: its own velocity is the drive's, so its own headlong pace would be read as the
        // force of every contact with the player. Which of the two was moving is not the question, so it is
        // taken unsigned.
        float impactSpeed = 0f;
        int worst = -1;

        for (int i = 0; i < collision.contacts.Length; i++)
        {
            if (ComesFromUnderneath(collision.contacts[i].normal)) continue;

            float into = Mathf.Abs(Vector3.Dot(collision.relativeVelocity, collision.contacts[i].normal));

            if (into > impactSpeed)
            {
                impactSpeed = into;
                worst = i;
            }
        }

        if (worst < 0 || impactSpeed < minimumImpactSpeed) return;

        Part part = NearestPart(collision.contacts[worst].point);
        if (part == null) return;

        lastImpactTime = Time.time;

        // Toughness is what makes one part harder to hurt than another: every impact is divided by it, so a
        // part twice as tough needs twice the crash to bend as far and twice the crash to come away.
        float damage = ((impactSpeed - minimumImpactSpeed) * damagePerSpeed + damagePerSpeed) /
                       part.toughness;

        Apply(part, collision.contacts[worst], damage);

        // A single crash hard enough tears a part off whatever it had left, which is what makes a big one
        // read differently from a series of small ones - and the threshold scales with toughness.
        bool overwhelmed = impactSpeed >= breakImpactSpeed * part.toughness;
        bool breaking = overwhelmed || part.damage >= Health;

        if (breaking)
        {
            // Told before the part leaves the car, because putting the car's lights out is the last thing
            // that is done while the lamp is still something the car is wearing.
            KillLamps(part);
            Break(part, collision.contacts[worst]);
        }

        if (logHits)
            Debug.Log(string.Format(
                "[PassingCarDamage] {0} took {1:0.00} from a {2:0.0} m/s hit (toughness {3:0.##}) - now " +
                "{4:0.00} and bent {5:0.0} deg{6}. Hit '{7}'.",
                part.transform != null ? part.transform.name : "?", damage, impactSpeed, part.toughness,
                part.damage, part.bendDegrees, breaking ? " - BROKE OFF" : "",
                collision.gameObject.name), this);

        KnockOn(part, collision.contacts[worst], damage);
    }

    /// <summary>
    /// A hit can shake the parts around it as well as the one it landed on. One neighbour at most is picked
    /// - the nearest to the blow - and it takes a fraction of the damage, so a crash reads as one thing
    /// happening to one area of the car rather than to all of it at once.
    /// </summary>
    private void KnockOn(Part hit, ContactPoint contact, float damage)
    {
        if (knockOnChance <= 0f || knockOnFraction <= 0f) return;
        if (Random.value >= knockOnChance) return;

        Part neighbour = NearestPart(contact.point, hit, knockOnRadius);
        if (neighbour == null) return;

        float shaken = damage * knockOnFraction;

        Apply(neighbour, contact, shaken);

        bool breaking = neighbour.damage >= Health;

        if (breaking)
        {
            KillLamps(neighbour);
            Break(neighbour, contact);
        }

        if (logHits)
            Debug.Log(string.Format(
                "[PassingCarDamage] {0} was shaken by the hit on {1} and took {2:0.00} - now {3:0.00}{4}.",
                neighbour.transform != null ? neighbour.transform.name : "?",
                hit.transform != null ? hit.transform.name : "?",
                shaken, neighbour.damage, breaking ? " - BROKE OFF" : ""), this);
    }

    /// <summary>
    /// Whether the surface pushed up into the car - the road, a kerb, a ramp - rather than into its nose,
    /// its side or its roof. Every sign of it is left to the crash effects.
    /// </summary>
    private bool ComesFromUnderneath(Vector3 contactNormal)
    {
        if (undersideAngle <= 0f) return false;

        return Vector3.Angle(contactNormal, transform.up) <= undersideAngle;
    }

    /// <summary>
    /// The part closest to where the car was hit, so a crash bends what was actually struck rather than
    /// whatever happens to be first in the list.
    /// </summary>
    private Part NearestPart(Vector3 point, Part exclude = null, float maxDistance = 0f)
    {
        Part nearest = null;
        float nearestDistance = float.MaxValue;
        float limit = maxDistance > 0f ? maxDistance * maxDistance : 0f;

        for (int i = 0; i < foundParts.Count; i++)
        {
            Part part = foundParts[i];
            if (part.broken || part.transform == null || part == exclude) continue;

            for (int r = 0; r < part.renderers.Length; r++)
            {
                if (part.renderers[r] == null) continue;

                // Out to the part's own outline rather than to its centre, so a part that shares an edge
                // with the blow is next to nothing away from it - which is what lets a knock-on tell
                // neighbours from parts on the far side of the car.
                float distance = part.renderers[r].bounds.SqrDistance(point);

                if (limit > 0f && distance > limit) continue;

                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = part;
                }
            }
        }

        return nearest;
    }

    // ---------------------------------------------------------------- bending

    /// <summary>
    /// Bends a part towards the blow. The part is always worked out from where the model put it, so a part
    /// that has been hit twice is bent twice as far rather than walking off the car a hit at a time, and it
    /// never bends further than its limit.
    /// </summary>
    private void Apply(Part part, ContactPoint contact, float damage)
    {
        if (part.transform == null || part.transform.parent == null) return;

        part.damage += damage;

        float amount = Mathf.Clamp01(part.damage);

        Vector3 push = -contact.normal;
        if (push.sqrMagnitude < 0.0001f) push = transform.up;

        Vector3 axis = Vector3.Cross(Vector3.up, push);
        if (axis.sqrMagnitude < 0.0001f) axis = Vector3.right;
        axis.Normalize();

        part.bendAxis = axis;
        part.bendDegrees = maxBendDegrees * amount * part.bendScale;

        Vector3 localPush = part.transform.parent.InverseTransformDirection(push);
        part.bendOffset = localPush * (maxBendOffset * amount * part.bendScale);

        part.transform.localPosition = part.homePosition + part.bendOffset;
        part.transform.localRotation = part.homeRotation * Quaternion.AngleAxis(part.bendDegrees, part.bendAxis);
    }

    // ---------------------------------------------------------------- breaking

    /// <summary>
    /// Takes a part off the car. It keeps its place in the world, is given a box collider the size of what
    /// it was drawn as and a rigidbody, and is thrown with the speed of the crash - after which it is the
    /// road's problem rather than the car's.
    /// </summary>
    private void Break(Part part, ContactPoint contact)
    {
        Transform piece = part.transform;
        if (piece == null) return;

        Bounds bounds;
        if (!WorldBoundsOf(piece, out bounds)) return;

        part.broken = true;

        Vector3 velocity = rb != null ? rb.velocity : Vector3.zero;
        Vector3 push = -contact.normal;

        piece.SetParent(null, true);

        GameObject debris = piece.gameObject;

        // The shape it was drawn as. A box rather than the model's own mesh: it is what a piece of wreckage
        // reads as, it is far cheaper, and it cannot go wrong on a mesh with open edges.
        Vector3 scale = debris.transform.lossyScale;
        BoxCollider box = debris.AddComponent<BoxCollider>();
        box.center = debris.transform.InverseTransformPoint(bounds.center);
        box.size = new Vector3(
            Mathf.Abs(scale.x) > 0.0001f ? bounds.size.x / Mathf.Abs(scale.x) : bounds.size.x,
            Mathf.Abs(scale.y) > 0.0001f ? bounds.size.y / Mathf.Abs(scale.y) : bounds.size.y,
            Mathf.Abs(scale.z) > 0.0001f ? bounds.size.z / Mathf.Abs(scale.z) : bounds.size.z);

        Rigidbody body = debris.AddComponent<Rigidbody>();
        body.velocity = velocity * debrisThrow;
        body.angularVelocity = Random.onUnitSphere *
                               Random.Range(debrisMinSpin, debrisMaxSpin) * Mathf.Deg2Rad;

        // Pushed a little away from the car, so a piece that comes off at the nose separates from it rather
        // than sitting in its path.
        if (debrisKick > 0f)
            body.AddForce((push + Vector3.up * 0.4f).normalized * debrisKick, ForceMode.VelocityChange);

        // The car must never grind against its own wreckage: the debris still collides with the road and the
        // world, but not with the car it fell off.
        if (ownColliders != null)
        {
            for (int i = 0; i < ownColliders.Length; i++)
                if (ownColliders[i] != null) Physics.IgnoreCollision(box, ownColliders[i], true);
        }

        if (debrisLifetime > 0f) Destroy(debris, debrisLifetime);
    }

    private static bool WorldBoundsOf(Transform piece, out Bounds bounds)
    {
        Renderer[] renderers = piece.GetComponentsInChildren<Renderer>();
        bounds = new Bounds(piece.position, Vector3.zero);

        if (renderers.Length == 0) return false;

        bounds = renderers[0].bounds;

        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        return true;
    }
}
