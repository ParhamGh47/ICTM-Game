using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The truck's damage, for the look of it: a hard enough impact bends the part nearest the hit, and
/// enough of them break it off to fall on the road as its own piece of wreckage.
///
/// It is an experiment, and it is built so that an experiment cannot cost the driving. Every part it
/// touches is a piece of the model - the grille, the rear door and its glass, the ice cream, the front
/// wheel meshes - and none of them carries a collider or is read by the car controller. Moving one of them
/// therefore moves only what is drawn; it cannot change the truck's mass, its centre of gravity, its
/// inertia, its suspension, its grip or the shape of what it collides with. The chassis mesh is
/// deliberately not in the list for exactly that reason: the truck's own collision is built into it, so
/// denting or detaching it would change how the truck drives.
///
/// A part is found by name when the level starts, the same way the paint system finds the parts it
/// repaints, because the model is an imported file rather than a hand-built hierarchy. A part takes
/// damage in proportion to how hard it was hit, at the point it was hit: the nearest part to the contact
/// point bends, further hits bend it more, and past its limit it comes off. A single very hard hit can
/// take a part off outright.
///
/// The load on the roof is looser than the panels around it: it is the part that is meant to be seen
/// taking a knock: it leans further than the rest, and it is the one part that can be set to shake - a fast
/// spring that swings out, swings back smaller and is gone in about a second - rather than simply being
/// nudged. That shake is switched off, because it read as too much on the ice cream; it is one Wobble Scale
/// away from coming back. The roof load also takes a few hits to come away, so it leans for a while rather
/// than being lost on the first contact.
///
/// When a part comes off it stops being part of the truck. It is given its own box collider worked out
/// from what it was drawn as, its own rigidbody, the speed the truck had at the moment of the crash and
/// some spin - so it lands, slides and tumbles on the road like the wreckage it is. Collisions between
/// the debris and the truck it came off are turned off, so a driver never feels their own bumper under
/// their wheels; the debris still collides with the road and with everything else. Debris is cleared
/// after a while so a long level does not fill up with it.
///
/// Impacts that should not count are left alone, by the same rules the crash sound already uses: the
/// targets the driver is meant to hit, the roadside signs, and contacts that come up at the truck from
/// underneath - the road, a kerb, a landing off a jump - rather than squarely into its nose or its side.
/// </summary>
[DisallowMultipleComponent]
public class TruckDamage : MonoBehaviour
{
    [Header("Master")]
    [Tooltip("Whether the truck can be dented, bent and broken at all. Turn it off to drive exactly the " +
             "same truck with none of it.")]
    public bool enableDamage = true;

    /// <summary>
    /// One breakable part: what it is called, and how much it takes.
    ///
    /// Toughness is the whole of "how breakable this is". Every impact divides by it, so a part twice
    /// as tough needs twice the crash to bend as far and twice the crash to come off, and a flimsy one
    /// comes off at half. It is the one number to change to make a part stubborn or fragile.
    /// </summary>
    [System.Serializable]
    public class BreakablePart
    {
        [Tooltip("Matched against the name of a child of the truck - case-insensitive, and a substring is " +
                 "enough, so 'wheels' finds both wheel meshes. The part has to be drawn with a mesh: " +
                 "effects parented under a matching name are left alone.")]
        public string name;

        [Tooltip("How much punishment the part takes compared with the standard. 1 is the standard; 2 " +
                 "needs twice the crash to bend and to break; 0.5 bends and breaks at half.")]
        [Range(0.05f, 5f)]
        public float toughness = 1f;

        [Tooltip("Whether the part can come off at all. Off: it bends and stays bent, however hard it is " +
                 "hit.")]
        public bool breaksOff = true;

        [Tooltip("How much further this part bends and shifts than the standard, on the same hits. 1 is the " +
                 "standard; 2 moves it twice as far by the time it is about to come off. It does not make the " +
                 "part any easier to break - toughness does that.")]
        [Range(0f, 4f)]
        public float bendScale = 1f;

        [Tooltip("How much this part takes part in the quick shake a hit sets off. 0 - the standard for " +
                 "everything but the load on the roof - leaves the part with only its bend; 1 shakes it by " +
                 "the standard above, 2 by twice that.")]
        [Range(0f, 4f)]
        public float wobbleScale = 0f;

        [Tooltip("Other pieces of the model that are drawn on this part and should move and come off with " +
                 "it, matched by the same rules as the name above. The plate's lettering is a model of its " +
                 "own, so without this it would be left hanging in the air when the plate came away.")]
        public string[] attached;
    }

    [Header("Parts")]
    [Tooltip("Everything on the truck that can be bent and broken, and how breakable each one is. The " +
             "chassis is deliberately not here - its mesh carries the truck's own collision, so moving it " +
             "would change how the truck drives.")]
    public BreakablePart[] parts =
    {
        // The model's parts, from the truck's own file. Toughness says what each is made of: the plate
        // and the glass are flimsy and come away early, the lamps are thin, the body panels and exhausts are
        // ordinary, and the load on the roof is flimsy too - but it takes a few knocks now, because it is the
        // part the driver is meant to watch being knocked about.
        // exhausts are ordinary, the grille takes a real knock, and a wheel is the hardest thing on the
        // truck to lose: it barely shifts on its axle and only gives up after a real beating.
        new BreakablePart { name = "gelgir",      toughness = 1.4f },   // the front grille and its surround
        new BreakablePart { name = "plate",       toughness = 0.35f,    // the number plate
                            attached = new[] { "Text" } },             // and the lettering on it
        new BreakablePart { name = "lightFront",  toughness = 2.6f },   // the headlamps - thin, but bolted to the body
        new BreakablePart { name = "lightBack",   toughness = 1.8f },   // the tail and brake lamps
        new BreakablePart { name = "leftExhaust", toughness = 0.8f },
        new BreakablePart { name = "rightExhaust", toughness = 0.8f },
        new BreakablePart { name = "backDoor",    toughness = 1.0f },   // the rear door
        new BreakablePart { name = "backWindow",  toughness = 0.55f },  // the glass in it
        new BreakablePart { name = "iceCream",    toughness = 1.8f, bendScale = 2.2f },   // the load on the roof
        new BreakablePart { name = "cone",        toughness = 0.3f, bendScale = 2.2f },   // its cone, which leans with it
        new BreakablePart { name = "wheelsFront", toughness = 3.5f, bendScale = 0.35f },   // the front wheel meshes
        // The rear wheels - 'wheelsBack' in the model - are left out on purpose: the pair the truck is driven
        // and braked on should not shift on its axle or come away, because a wheel that has shifted, or left,
        // reads as the truck broken rather than as the truck battered. They were in the list before, and both
        // were being bent and thrown on every hit: only the front pair can be lost now, and only after a real
        // beating.


        // The roof load, in short: it takes about four times the beating it used to (toughness 0.45 -> 1.8),
        // and it leans twice as far as a panel (Bend Scale 2.2). It is the only part that would take part in
        // the quick shake a hit sets off, and that shake is switched off on it (Wobble Scale 0).

        // Bending, as a rule: the body panels and the lamps hold their shape (Bend Scale 1), the load on the
        // roof is loose on its base and rocks about more (2.2), and a wheel barely shifts at all (0.35), so
        // that it stays where the model put it until it finally gives up and leaves.
    };

    [Tooltip("How far a contact's surface may lean towards the truck's own up and still count as coming " +
             "at it from underneath - the road, a kerb, a ramp, the ground a jump lands on - rather than " +
             "being a crash. In degrees, and the same rule the crash sound uses.")]
    [Range(0f, 89f)]
    public float undersideAngle = 55f;

    [Header("Impacts")]
    [Tooltip("The slowest crash that does anything at all, in metres per second, measured along the " +
             "surface it hit rather than by how fast the truck was going: brushing along a wall at speed " +
             "is not a crash into it.")]
    public float minimumImpactSpeed = 3f;

    [Tooltip("How much of a part's health one metre per second of impact takes. Raise it and the truck " +
             "bends sooner and breaks sooner.")]
    public float damagePerSpeed = 0.055f;

    [Tooltip("A crash longer than this in seconds is a different crash, so one prang cannot be counted " +
             "over and over by its many contacts.")]
    public float impactCooldown = 0.15f;

    [Header("Bending")]
    [Tooltip("How far a part can bend, in degrees, from where the model put it - reached when it is one " +
             "hit away from coming off. The part's own Bend Scale multiplies it.")]
    public float maxBendDegrees = 16f;

    [Tooltip("How far a part can shift, in metres, from where the model put it - reached at the same " +
             "point as the bend. The part's own Bend Scale multiplies it.")]
    public float maxBendOffset = 0.04f;

    [Header("Wobble")]
    [Tooltip("Whether a hit throws the truck's loose parts into a quick shake as well as bending them. Only " +
             "parts with a Wobble Scale of their own take part in it, which is the load on the roof.")]
    public bool wobble = true;

    [Tooltip("How far the shake throws a part about, in metres at the peak of its first swing, per metre per " +
             "second of impact. A 15 m/s crash shakes the roof load about ten centimetres.")]
    public float wobblePerSpeed = 0.0045f;

    [Tooltip("How far the shake tilts a part, in degrees at the peak of its first swing.")]
    public float wobbleDegrees = 7f;

    [Tooltip("How quick the shake is, in swings per second. Higher is a tighter, more rapid jiggle.")]
    public float wobbleFrequency = 10f;

    [Tooltip("How long the shake takes to die down, in seconds - the time for it to fall to about a third of " +
             "its first swing. At 0.45 it is still moving a little a second after the hit.")]
    public float wobbleSettle = 0.45f;

    [Tooltip("The hardest a crash may shake the truck, in metres per second, so one enormous hit cannot throw " +
             "the roof load off its own springs.")]
    public float wobbleMaxSpeed = 25f;

    [Header("Breaking")]
    [Tooltip("How much of a part's health it takes to bend it to breaking point. It is 1 by design: " +
             "every unit of damage is damagePerSpeed metres per second of impact.")]
    public float health = 1f;

    [Tooltip("A single crash this hard in metres per second takes a part off in one go, whatever it had " +
             "left. Set it above any speed the truck can reach to make every part bend first.")]
    public float breakImpactSpeed = 16f;

    [Header("Knock-on Damage")]
    [Tooltip("The chance that a hit hard enough to hurt one part also shakes a neighbour, 0 to 1. The " +
             "neighbour takes a fraction of the damage, so the crash that bends the rear door can clip the " +
             "window in it as well.")]
    [Range(0f, 1f)]
    public float knockOnChance = 0.35f;

    [Tooltip("How near a part has to be to the hit, in metres, to be shaken by it. Measured to the part's " +
             "own outline, so a part that shares an edge with the one that was hit is always in range.")]
    public float knockOnRadius = 0.6f;

    [Tooltip("How much of the damage a shaken part takes. Keep it below 1, so the part that was actually " +
             "hit is still the one that comes off first.")]
    [Range(0f, 1f)]
    public float knockOnFraction = 0.5f;

    [Header("Debris")]
    [Tooltip("How long a broken-off part is left lying on the road before it is cleared, in seconds. 0 " +
             "leaves it there for the rest of the level.")]
    public float debrisLifetime = 14f;

    [Tooltip("How much the debris is thrown, as a fraction of the truck's own speed at the moment of the " +
             "crash. 1 throws it at the speed the truck was doing.")]
    [Range(0f, 2f)]
    public float debrisThrow = 0.6f;

    [Tooltip("How hard a broken-off part is pushed away from the truck, in metres per second. Small: it " +
             "is only there so a piece that comes off at the nose separates from the truck rather than " +
             "sitting in its path.")]
    public float debrisKick = 2f;

    [Tooltip("The slowest and fastest spin the debris is thrown with, in degrees per second.")]
    public float debrisMinSpin = 90f;
    public float debrisMaxSpin = 420f;

    [Header("Logging")]
    [Tooltip("Whether to log what the model actually offers as breakable parts, and every hit a part " +
             "takes - what it was, how hard the crash was, what it did to the part and whether that took " +
             "it off. Useful while tuning how breakable each part is.")]
    public bool logDamage = true;

    // ---------------------------------------------------------------- a part

    private class Part
    {
        public Transform transform;

        // Where the model put it, so bending is always measured from there rather than from the last
        // bend - which would otherwise walk the part further out with every hit.
        public Quaternion homeRotation;
        public Vector3 homePosition;

        public Vector3 bendOffset;      // in the part's parent's space
        public float bendDegrees;       // about the impact axis
        public Vector3 bendAxis;        // in the part's parent's space

        public float toughness;         // how much punishment it takes, from the part's own setting
        public bool breaksOff;          // whether it may come off at all
        public float bendScale;         // how much further than the standard it bends and shifts
        public float wobbleScale;       // how much it shakes when the truck is hit

        // The lamps this part is: the headlamp lens the light toggle switches on, or the brake lamps the
        // car controller lights. Worked out from the references those two already hold rather than from a
        // name, so a model that names its lamps something else is still wired up.
        public LightToggle headlamp;
        public bool brakeLamp;

        public int attachedCount;       // pieces moved under this part, for the startup log

        public float damage;
        public bool broken;

        public Renderer[] renderers;
    }

    // The parts the model actually has, found from the settings above. Kept separate from them so the
    // settings stay exactly as authored while a level runs.
    private readonly List<Part> foundParts = new List<Part>();

    private Rigidbody rb;
    private Collider[] ownColliders;

    // The car the brake lamps belong to, for telling it how broken they are.
    private CarController car;

    // The speed the truck was doing at the last physics step, for measuring an impact. Read before the
    // collision has been resolved, so it is the truck arriving rather than the truck rebounding.
    private Vector3 lastVelocity;

    private float lastImpactTime = -999f;

    // The shake a hit sets off, for the parts that take part in it: how hard the truck was hit (capped),
    // which way the part is being thrown, and how long ago it happened.
    private float shakeSpeed;
    private Vector3 shakeDirection = Vector3.up;
    private float shakeTime = -1f;

    // Whether any part on this truck takes part in the shake at all. With none, a hit costs nothing: the
    // whole thing is skipped rather than looped over and found empty.
    private bool anyWobble;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        ownColliders = GetComponentsInChildren<Collider>(true);

        car = GetComponentInChildren<CarController>(true);

        Discover();
        FindLamps();
        Remember();

        if (logDamage) LogParts();
    }

    // ---------------------------------------------------------------- lamps

    /// <summary>
    /// Works out which parts are the lamps, from the references the light toggle and the car controller
    /// already hold - the lens the toggle lights and the renderer the car brakes with. That way the model
    /// can call its lamps whatever it likes and the wiring still lands.
    /// </summary>
    private void FindLamps()
    {
        LightToggle toggle = GetComponentInChildren<LightToggle>(true);

        Renderer headlampLens = toggle != null ? toggle.emissionObject : null;
        Renderer brakeLens = car != null ? car.brakeLightRenderer : null;

        for (int i = 0; i < foundParts.Count; i++)
        {
            Part part = foundParts[i];

            if (toggle != null && Contains(part, headlampLens)) part.headlamp = toggle;
            if (Contains(part, brakeLens)) part.brakeLamp = true;
        }
    }

    private static bool Contains(Part part, Renderer renderer)
    {
        if (renderer == null || part.transform == null) return false;

        return renderer.transform == part.transform || renderer.transform.IsChildOf(part.transform);
    }

    /// <summary>
    /// Tells the lamps when there is no longer a lamp to light. A lamp that is still on the truck works
    /// exactly as it always did, however battered the truck around it looks - this is only about the part
    /// being gone.
    /// </summary>
    private void UpdateLamps(Part part, bool breaking)
    {
        if (!breaking) return;

        if (part.headlamp != null)
            part.headlamp.KillLamp();

        if (part.brakeLamp && car != null)
            car.KillBrakeLamp();
    }

    /// <summary>
    /// Says which of the settings above actually matched something on the model. A name that matches
    /// nothing is the usual reason a part never breaks, and it is not visible from the inspector.
    /// </summary>
    private void LogParts()
    {
        if (foundParts.Count == 0)
        {
            Debug.LogWarning("[TruckDamage] No breakable part was found on '" + name +
                             "'. Check the part names against the model's own object names.", this);
            return;
        }

        System.Text.StringBuilder text = new System.Text.StringBuilder();
        text.Append("[TruckDamage] ").Append(foundParts.Count).Append(" breakable part(s) on '").Append(name)
            .Append("':");

        for (int i = 0; i < foundParts.Count; i++)
        {
            Part part = foundParts[i];
            text.Append("\n    ").Append(part.transform != null ? part.transform.name : "?")
                .Append("  toughness ").Append(part.toughness.ToString("0.##"))
                .Append(part.breaksOff ? "  (can come off)" : "  (bends only)");

            if (part.attachedCount > 0)
                text.Append("  +").Append(part.attachedCount).Append(" attached");
        }

        Debug.Log(text.ToString(), this);
    }

    private void FixedUpdate()
    {
        if (rb != null) lastVelocity = rb.velocity;
    }

    /// <summary>
    /// Starts the shake a hit sets off. One shake at a time: a new hit replaces whatever was left of the
    /// last one, which keeps the motion reading as the crash it came from rather than as an accumulation.
    /// </summary>
    private void StartShake(float impactSpeed, Vector3 contactNormal)
    {
        Vector3 push = -contactNormal;
        if (!anyWobble) return;
        if (push.sqrMagnitude < 0.0001f) return;

        shakeSpeed = Mathf.Min(impactSpeed, Mathf.Max(1f, wobbleMaxSpeed));
        shakeDirection = push.normalized;
        shakeTime = 0f;
    }

    /// <summary>
    /// The quick shake a hit leaves behind in the parts that take part in it. It is a spring rather than a
    /// slower lean: it swings out fast, swings back smaller, and is over in about a second - so a knock on
    /// the roof load reads as something being knocked about rather than as the part being moved for good.
    ///
    /// It runs on the frame rather than the physics step on purpose: it moves what is drawn and nothing
    /// else, so the smoother it is the better, and nothing about the truck's driving depends on it.
    /// </summary>
    private void Update()
    {
        if (shakeTime < 0f) return;

        shakeTime += Time.deltaTime;

        float decay = Mathf.Exp(-shakeTime / Mathf.Max(0.02f, wobbleSettle));
        float swing = Mathf.Sin(shakeTime * wobbleFrequency * Mathf.PI * 2f) * decay;

        if (decay < 0.02f)
        {
            shakeTime = -1f;
            Settle();

            return;
        }

        for (int i = 0; i < foundParts.Count; i++)
        {
            Part part = foundParts[i];

            if (part.wobbleScale <= 0f || part.broken || part.transform == null) continue;

            // The throw is worked out in the world and brought into each part's own parent, so a part whose
            // parent is turned - or turned differently from its neighbour - is still thrown the same way as
            // the one beside it, and the two do not drift apart while they shake.
            Vector3 direction = part.transform.parent != null
                ? part.transform.parent.InverseTransformDirection(shakeDirection)
                : shakeDirection;

            part.transform.localPosition = part.homePosition + part.bendOffset +
                                           direction * (wobblePerSpeed * shakeSpeed * part.wobbleScale * swing);

            part.transform.localRotation = part.homeRotation * Quaternion.AngleAxis(
                part.bendDegrees + wobbleDegrees * part.wobbleScale * swing, part.bendAxis);
        }
    }

    /// <summary>Puts the parts that shake onto the pose their own damage gives them, the shake over.</summary>
    private void Settle()
    {
        for (int i = 0; i < foundParts.Count; i++)
        {
            Part part = foundParts[i];

            if (part.wobbleScale <= 0f || part.broken || part.transform == null) continue;

            part.transform.localPosition = part.homePosition + part.bendOffset;
            part.transform.localRotation = part.homeRotation * Quaternion.AngleAxis(part.bendDegrees, part.bendAxis);
        }
    }

    // ---------------------------------------------------------------- discovery

    /// <summary>
    /// Finds the model's parts by name. They come from an imported file rather than a hand-built
    /// hierarchy, so the name is the only handle there is - the same one the paint system uses.
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

                // Found already: the same object can match more than one name - a part called 'wheelsFront'
                // matches only one, but a model that nests them can match two names with one transform.
                if (AlreadyFound(child)) continue;

                // Drawn with a mesh, rather than an effect that happens to be named like a part: the
                // exhausts have their smoke parented under names that contain 'exhaust', and a particle
                // system is not something the truck can lose.
                if (!HasMesh(child.GetComponentsInChildren<Renderer>(true))) continue;

                // Whatever is drawn on this part is moved under it, so the two become one piece: the
                // lettering bends with the plate and comes away with it. Done before the renderers are
                // read again, so the piece's outline includes what was moved onto it.
                Part part = new Part();
                part.transform = child;
                part.attachedCount = Attach(child, wanted.attached);
                part.toughness = Mathf.Max(0.05f, wanted.toughness);
                part.breaksOff = wanted.breaksOff;
                part.bendScale = Mathf.Max(0f, wanted.bendScale);
                part.wobbleScale = Mathf.Max(0f, wanted.wobbleScale);
                part.renderers = child.GetComponentsInChildren<Renderer>(true);

                foundParts.Add(part);

                if (part.wobbleScale > 0f) anyWobble = true;
            }
        }
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
    private int Attach(Transform part, string[] attached)
    {
        if (attached == null) return 0;

        int moved = 0;
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

                // Nothing that is a breakable part in its own right is swallowed by another.
                if (AlreadyFound(child)) continue;

                child.SetParent(part, true);
                moved++;
            }
        }

        return moved;
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

        // The targets the driver is meant to hit and the roadside signs bring their own effect and are
        // not meant to damage the truck, and a contact from underneath is the road, a kerb or the ground
        // a jump lands on rather than a crash.
        if (collision.gameObject.CompareTag("Adamak")) return;
        if (collision.gameObject.CompareTag("Interactive")) return;
        if (collision.collider != null &&
            collision.collider.GetComponentInParent<NoImpactParticles>() != null) return;

        if (Time.time - lastImpactTime < impactCooldown) return;

        // How hard the truck arrived, along the surface it hit. A glancing blow at speed is a light
        // touch; a slower one straight into the same surface is the harder hit.
        float impactSpeed = 0f;
        int worst = -1;

        for (int i = 0; i < collision.contacts.Length; i++)
        {
            if (ComesFromUnderneath(collision.contacts[i].normal)) continue;

            float into = -Vector3.Dot(lastVelocity, collision.contacts[i].normal);
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

        // Toughness is what makes one part harder to hurt than another: every impact is divided by it, so
        // a part twice as tough needs twice the crash to bend as far and twice the crash to come away.
        float damage = ((impactSpeed - minimumImpactSpeed) * damagePerSpeed + damagePerSpeed) /
                       part.toughness;

        Apply(part, collision.contacts[worst], damage);

        // The quick shake the hit sets off, in the parts that take part in it.
        if (wobble) StartShake(impactSpeed, collision.contacts[worst].normal);

        // A single crash hard enough tears a part off whatever it had left, which is what makes a big
        // one read differently from a series of small ones - and the threshold scales with toughness, so
        // the same crash that takes a plate off leaves a wheel on.
        // the same crash that takes a plate off leaves a wheel on.
        bool overwhelmed = impactSpeed >= breakImpactSpeed * part.toughness;
        bool breaking = part.breaksOff && (overwhelmed || part.damage >= health);

        if (breaking)
            Break(part, collision.contacts[worst]);

        UpdateLamps(part, breaking);

        if (logDamage)
            Debug.Log(string.Format(
                "[TruckDamage] {0} took {1:0.00} from a {2:0.0} m/s hit (toughness {3:0.##}) - now " +
                "{4:0.00}/{5:0.##} and bent {6:0.0} deg{7}. Hit '{8}'.",
                part.transform != null ? part.transform.name : "?", damage, impactSpeed, part.toughness,
                part.damage, health, part.bendDegrees,
                breaking ? " - BROKE OFF" : (part.breaksOff ? "" : " - bends only"),
                collision.gameObject.name), this);

        KnockOn(part, collision.contacts[worst], damage);
    }

    /// <summary>
    /// A hit can shake the parts around it as well as the one it landed on. One neighbour at most is picked
    /// - the nearest to the blow - and it takes a fraction of the damage, so a crash reads as one thing
    /// happening to one area of the truck rather than to all of it at once.
    /// </summary>
    private void KnockOn(Part hit, ContactPoint contact, float damage)
    {
        if (knockOnChance <= 0f || knockOnFraction <= 0f) return;
        if (Random.value >= knockOnChance) return;

        Part neighbour = NearestPart(contact.point, hit, knockOnRadius);
        if (neighbour == null) return;

        float shaken = damage * knockOnFraction;

        Apply(neighbour, contact, shaken);

        bool breaking = neighbour.breaksOff && neighbour.damage >= health;

        if (breaking)
            Break(neighbour, contact);

        UpdateLamps(neighbour, breaking);

        if (logDamage)
            Debug.Log(string.Format(
                "[TruckDamage] {0} was shaken by the hit on {1} and took {2:0.00} - now {3:0.00}/{4:0.##}{5}.",
                neighbour.transform != null ? neighbour.transform.name : "?",
                hit.transform != null ? hit.transform.name : "?",
                shaken, neighbour.damage, health, breaking ? " - BROKE OFF" : ""), this);
    }

    /// <summary>
    /// Whether the surface pushed up into the truck - the road, a kerb, a ramp, the ground a jump lands
    /// on - rather than into its nose, its side or its roof. Every sign of it is left to the crash sound.
    /// </summary>
    private bool ComesFromUnderneath(Vector3 contactNormal)
    {
        if (undersideAngle <= 0f) return false;

        return Vector3.Angle(contactNormal, transform.up) <= undersideAngle;
    }

    /// <summary>
    /// The part closest to where the truck was hit, so a crash bends what was actually struck rather
    /// than whatever happens to be first in the list.
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
                // neighbours from parts on the far side of the truck.
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
    /// Bends a part towards the blow. The part is always worked out from where the model put it, so a
    /// part that has been hit twice is bent twice as far rather than walking off the truck a hit at a
    /// time, and it never bends further than its limit.
    /// </summary>
    private void Apply(Part part, ContactPoint contact, float damage)
    {
        if (part.transform == null || part.transform.parent == null) return;

        part.damage += damage;

        // The bend is read off the total damage rather than added to the last bend, so a part hit twice is
        // bent twice as far and a part hit many times is never bent past its limit.
        float amount = Mathf.Clamp01(part.damage / Mathf.Max(0.0001f, health));

        // The blow, in the part's own parent's space: the surface pushes the part along its normal.
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
    /// Takes a part off the truck. It keeps its place in the world, is given a box collider the size of
    /// what it was drawn as and a rigidbody, and is thrown with the speed of the crash - after which it
    /// is the road's problem rather than the truck's.
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

        // The shape it was drawn as. A box rather than the model's own mesh: it is what a piece of
        // wreckage reads as, it is far cheaper, and it cannot go wrong on a mesh with open edges.
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

        // Pushed a little away from the truck, so a piece that comes off at the nose separates from it
        // rather than sitting in its path.
        if (debrisKick > 0f)
            body.AddForce((push + Vector3.up * 0.4f).normalized * debrisKick, ForceMode.VelocityChange);

        // The player must never feel their own wreckage: the debris still collides with the road and the
        // world, but not with the truck it fell off.
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

    // ---------------------------------------------------------------- repair

    /// <summary>
    /// Puts the truck back the way the model has it, for a reset. Parts that have come off cannot be
    /// put back - they were let go of, and the truck that is reset is a fresh one - so what this does is
    /// bend everything back and forget the damage, and a level that wants its truck whole starts it
    /// whole.
    /// </summary>
    public void Repair()
    {
        for (int i = 0; i < foundParts.Count; i++)
        {
            Part part = foundParts[i];

            if (part.transform == null) continue;
            if (part.broken) continue;

            part.damage = 0f;
            part.bendDegrees = 0f;
            part.bendOffset = Vector3.zero;

            part.transform.localPosition = part.homePosition;
            part.transform.localRotation = part.homeRotation;
        }

        lastImpactTime = -999f;
        shakeTime = -1f;
    }

    /// <summary>The damage the truck is carrying, 0 to 1, over all of its parts. For a level or a HUD
    /// that wants to say how battered the truck is.</summary>
    public float Damage
    {
        get
        {
            if (foundParts.Count == 0) return 0f;

            float total = 0f;
            int counted = 0;

            for (int i = 0; i < foundParts.Count; i++)
            {
                total += foundParts[i].broken
                    ? 1f
                    : Mathf.Clamp01(foundParts[i].damage / Mathf.Max(0.0001f, health));

                counted++;
            }

            return counted > 0 ? total / counted : 0f;
        }
    }
}
