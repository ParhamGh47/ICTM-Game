using UnityEngine;

/// <summary>
/// Drives one background car around the closed waypoint loop a painter built for it.
///
/// The car steers towards the next waypoint and is moved with <c>MovePosition</c>/<c>MoveRotation</c>, so
/// its path is exactly the loop and nothing the physics does can talk it out of it. On top of that it
/// has three reactions, all of them sideways offsets added to the waypoint it is aiming at - the loop
/// itself is never changed:
///
///  - <b>Getting around obstacles</b> (the Adamaks a level puts in the road): it looks a little way
///    ahead for anything carrying <see cref="obstacleTag"/> and commits to one side of it, so it goes
///    round a target instead of running it over.
///  - <b>Yielding to the horn</b>: when the player sounds the horn behind a car it pulls over towards the
///    shoulder - which is to its own right on both lanes of both directions - and lets the player past.
///    The horn reports itself through <see cref="ReportHorn"/> because it lives in the player's assembly,
///    which a car cannot reference.
///  - <b>Being knocked about</b>: once it is tipped past <see cref="uprightLimit"/> it stops being driven
///    and is left to the physics until it comes to rest. That is what stops the old kangaroo hop - the
///    drive used to keep forcing position and rotation onto a body lying on its side, so every collision
///    resolution threw it into the air again. When it has settled, a car that is still on its wheels and
///    has ground under it is put back on the nearest waypoint ahead of it and drives on - being shoved off
///    the road is not meant to be a life sentence - while one that has ended up on its roof, or resting on
///    nothing at all, is left standing where it lies.
/// </summary>    [RequireComponent(typeof(Rigidbody))]
public class AICarController : MonoBehaviour
{
    [Header("Waypoints")]
    public Transform waypointsRoot;

    // Which waypoint this car starts from. A car placed mid-path must start
    // at its own waypoint, otherwise it turns around and drives back to
    // waypoint[0] (ramming cars ahead of it). Default 0 keeps manually placed
    // cars behaving exactly as before.
    public int startingWaypoint = 0;

    private Transform[] waypoints;
    private int currentWaypoint;

    [Header("Movement")]
    [Tooltip("The lightest a car is allowed to be. A prefab's own rigidbody mass is what the car actually " +
             "weighs - that is the dial to turn for a heavier or lighter car - and this is only a floor under " +
             "it, so a prefab that was never given one does not behave like a shopping trolley.")]
    public float minimumMass = 800f;

    public float speedKPH = 60f;
    public float turnSpeed = 6f;
    public float maxSteerAngle = 35f;
    public float reachThreshold = 2f;

    [Header("Wheels (visual only)")]
    public Transform[] wheels;
    public float suspensionDistance = 0.4f;
    public LayerMask groundMask;

    [Header("Getting Around Obstacles")]
    [Tooltip("Tag whose objects a passing car steers around. The Adamak prefab's root carries it, " +
             "so the whole target is seen even though its colliders live on its ragdoll parts.")]
    public string obstacleTag = "Adamak";

    [Tooltip("How far up the road a car looks for something to go around. Longer means it commits " +
             "to one side earlier and the swerve is gentler.")]
    public float avoidLookAhead = 16f;

    [Tooltip("How far off its own line something has to be before a car leaves it alone, in metres - " +
             "roughly half a car plus half a target. This is what stops a car swerving at every target " +
             "standing on the edge of the road: only what is actually in its way counts.")]
    public float avoidCorridor = 1.3f;

    [Tooltip("How far off its line a car may pull to get around something, in metres. The passing " +
             "lane is 5 m wide and the car sits in its middle, so a metre or two stays on the asphalt.")]
    public float avoidStrength = 1.7f;

    [Tooltip("How quickly that pull builds and fades, in metres per second.")]
    public float avoidResponse = 1.2f;

    [Tooltip("Extra width around an obstacle, so a car clears it rather than clipping its edge.")]
    public float avoidProbeRadius = 2f;

    [Header("Yielding To The Horn")]
    [Tooltip("How far behind a car the player may be for the horn to be heard.")]
    public float hornYieldRange = 32f;

    [Tooltip("How long a car stays pulled over to the shoulder after being honked at.")]
    public float hornYieldDuration = 2.5f;

    [Tooltip("How far towards the shoulder a honked car pulls over, in metres.")]
    public float hornYieldStrength = 2.1f;

    [Header("Knocked Off Its Wheels")]
    [Tooltip("How far from upright a car may lean before it is treated as knocked over and stops " +
             "being driven. A car on a banked bend leans a little; a rolled one leans a lot.")]
    public float uprightLimit = 55f;

    [Tooltip("The most a car is allowed to be moving upwards while it is still being driven, so a " +
             "collision cannot leave it hopping down the road.")]
    public float maxVerticalDrift = 1.5f;

    [Tooltip("Below this speed, and this spin, a knocked-over car counts as having come to rest.")]
    public float settleSpeed = 0.6f;
    public float settleSpin = 1.2f;

    [Tooltip("How long it must stay that calm before it is frozen where it lies.")]
    public float settleTime = 0.75f;

    [Tooltip("A hard stop on the waiting: a knocked-over car that is somehow still rolling is looked at " +
             "anyway after this long, so it can never wander off down the level.")]
    public float settleTimeout = 8f;

    [Tooltip("How far below itself a settled car looks for ground before it is allowed to drive again. A " +
             "car that has come to rest with nothing underneath it - wedged in a tree, hung on a wall - has " +
             "nowhere to drive from, so it is left where it is.")]
    public float groundCheckDistance = 2.5f;

    [Tooltip("Stand a recovered car back up on its wheels - keeping the heading it ended up with, dropping " +
             "the lean the collision gave it - before driving it on.")]
    public bool levelOnRecovery = true;

    Rigidbody rb;
    float speedMS;

    // Metres the aim point is pulled sideways right now, and the side the car commits to when
    // something is dead ahead of it (where there is no side to be read off the obstacle itself).
    float avoidOffset;
    float avoidSide = -1f;

    // The horn, and how much of it this car has already acted on.
    float yieldUntil;
    float hornSeen = float.NegativeInfinity;

    // Set while the car is not being driven: tipped past the upright limit, or shoved off its line. Cleared
    // again once it has come to rest somewhere it can still drive from.
    bool knockedOver;

    // Set when it has settled somewhere it can never drive from - on its roof or side, or resting on nothing
    // at all. It stays there, and nothing is run on it again.
    bool stranded;

    float settleTimer;
    float knockedTimer;

    // Said once per session rather than once per car, because a scene can hold dozens of them.
    static bool reportedMissingPath;

    // Shared so the probe costs no allocation every step. Used and done with inside one call, so it is
    // safe to share between cars.
    private static readonly Collider[] obstacleHits = new Collider[32];
    private static readonly RaycastHit[] groundHits = new RaycastHit[8];

    // ---------------------------------------------------------------- the horn

    /// <summary>
    /// The last time the player sounded the horn, and where from.
    ///
    /// The player's horn lives outside the cars' assembly, so the player reports itself here and every
    /// car listens. One honk is heard by all of them, and each decides for itself whether it was aimed
    /// at it - see <see cref="ListenForHorn"/>.
    /// </summary>
    public static float LastHornTime { get; private set; } = float.NegativeInfinity;
    public static Transform LastHornSource { get; private set; }

    public static void ReportHorn(Transform source)
    {
        LastHornTime = Time.time;
        LastHornSource = source;
    }

    // -------------------------------------------------------------------- life

    void Awake()
    {
        rb = GetComponent<Rigidbody>();

        rb.isKinematic = false;

        // The mass is the car prefab's own - a 911 is a light car and a truck is not, and how a car is thrown
        // by a hit is most of what tells them apart. Only a car left at (or near) Unity's default is brought
        // up to something drivable, which is what keeps a prefab made in a hurry from flying off the road.
        if (rb.mass < minimumMass) rb.mass = minimumMass;

        rb.drag = 0.5f;
        rb.angularDrag = 5f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        rb.position += Vector3.up * 0.05f;

        speedMS = speedKPH / 3.6f;
        CacheWaypoints();
    }

    void CacheWaypoints()
    {
        if (!waypointsRoot)
        {
            // A car with no path is not driven at all - <see cref="MoveCar"/> has nothing to aim at - so it
            // stands where it was put, which is easy to mistake for a car that simply refuses to move. It is
            // what an earlier paint run leaves behind: painting rebuilds the waypoint paths, and the cars of
            // the run before are left pointing at objects that no longer exist.
            if (!reportedMissingPath)
            {
                reportedMissingPath = true;
                Debug.LogWarning(
                    $"{name} has no waypoint path, so it cannot be driven and will stand still. Cars left " +
                    "over from an earlier paint run look like this - painting again (Tools > Road Tools > " +
                    "Paint Passing Cars) replaces the whole batch and clears them up.", this);
            }

            return;
        }

        int count = waypointsRoot.childCount;
        waypoints = new Transform[count];

        for (int i = 0; i < count; i++)
            waypoints[i] = waypointsRoot.GetChild(i);

        currentWaypoint = Mathf.Clamp(startingWaypoint, 0, Mathf.Max(0, count - 1));
    }

    void FixedUpdate()
    {
        if (stranded) return;

        if (knockedOver)
        {
            Recover();
            return;
        }

        if (!IsOnItsWheels())
        {
            KnockOffLine();
            return;
        }

        MoveCar();
        UpdateWheelVisuals();
    }

    // ----------------------------------------------------------------- driving

    void MoveCar()
    {
        if (waypoints == null || waypoints.Length == 0) return;

        ListenForHorn();

        Transform target = waypoints[currentWaypoint];

        Vector3 toTarget = target.position - transform.position;
        toTarget.y = 0f;

        if (toTarget.magnitude < reachThreshold)
        {
            currentWaypoint = (currentWaypoint + 1) % waypoints.Length;
            return;
        }

        // Right of the direction it is actually travelling, which is what "pull over to the shoulder"
        // means on both lanes: each leg of the loop runs on the side of the road that makes its own
        // right-hand side the shoulder.
        Vector3 ahead = toTarget.normalized;
        Vector3 right = Vector3.Cross(Vector3.up, ahead);

        UpdateAvoidance(ahead, right);

        // Aim at the waypoint, pulled sideways by whatever the car is getting out of the way of. The aim
        // point is a whole waypoint away, so a metre of pull here is a gentle lean, not a swerve.
        Vector3 aim = target.position + right * avoidOffset;

        Vector3 desiredDir = aim - transform.position;
        desiredDir.y = 0f;

        if (desiredDir.sqrMagnitude < 0.0001f) return;
        desiredDir.Normalize();

        float angle = Vector3.SignedAngle(transform.forward, desiredDir, Vector3.up);
        angle = Mathf.Clamp(angle, -maxSteerAngle, maxSteerAngle);

        Quaternion steerRot = Quaternion.AngleAxis(angle, Vector3.up);
        Quaternion targetRot = rb.rotation * steerRot;

        rb.MoveRotation(
            Quaternion.Slerp(rb.rotation, targetRot, turnSpeed * Time.fixedDeltaTime)
        );

        Vector3 move = transform.forward * speedMS * Time.fixedDeltaTime;
        rb.MovePosition(rb.position + move);

        DampVerticalKick();
    }

    /// <summary>
    /// The sideways pull the car is applying right now: around an obstacle in its way, out to the
    /// shoulder for a horn behind it, or nothing at all.
    /// </summary>
    void UpdateAvoidance(Vector3 ahead, Vector3 right)
    {
        float wanted = 0f;

        float obstacleLateral;
        float obstacleHalfWidth;
        if (FindObstacleAhead(ahead, right, out obstacleLateral, out obstacleHalfWidth))
        {
            // Everything already clear of the car's own line is left alone: targets standing on the
            // edge of the road are passed, not swerved at. What is in the way gets a pull that grows
            // the nearer to its line it sits, so the car leans round it instead of snapping across.
            float corridor = avoidCorridor + obstacleHalfWidth;

            if (Mathf.Abs(obstacleLateral) < corridor)
            {
                // Commit to whichever side it is less far out of the way on. Taken from the obstacle's
                // own offset rather than from a fixed side, so a car goes round the near side of a
                // target instead of always swinging the same way across the road.
                if (Mathf.Abs(obstacleLateral) > 0.5f)
                    avoidSide = -Mathf.Sign(obstacleLateral);

                float urgency = Mathf.Max(0.35f, 1f - Mathf.Abs(obstacleLateral) / corridor);
                wanted += avoidSide * avoidStrength * urgency;
            }
        }

        if (Time.time < yieldUntil)
            wanted += hornYieldStrength;

        float limit = Mathf.Max(avoidStrength, hornYieldStrength);
        float target = Mathf.Clamp(wanted, -limit, limit);

        avoidOffset = Mathf.MoveTowards(avoidOffset, target, avoidResponse * Time.fixedDeltaTime);
    }

    /// <summary>
    /// The nearest thing in the car's way that is worth going around: how far off its centre it is -
    /// negative to the left of the car, positive to its right - and how wide it is. Anything behind it,
    /// or already beside it, is left alone.
    /// </summary>
    bool FindObstacleAhead(Vector3 ahead, Vector3 right, out float lateral, out float halfWidth)
    {
        lateral = 0f;
        halfWidth = 0f;

        Vector3 origin = transform.position;
        float probeRadius = avoidLookAhead * 0.5f + avoidProbeRadius;

        int count = Physics.OverlapSphereNonAlloc(
            origin + ahead * (avoidLookAhead * 0.5f), probeRadius, obstacleHits,
            ~0, QueryTriggerInteraction.Ignore);

        float nearest = float.MaxValue;
        bool found = false;

        for (int i = 0; i < count; i++)
        {
            Collider hit = obstacleHits[i];
            if (hit == null) continue;

            // Its own colliders, and those of the car behind it in the platoon.
            if (hit.transform.IsChildOf(transform)) continue;
            if (hit.GetComponentInParent<AICarController>() != null) continue;

            if (!CarriesTag(hit.transform, obstacleTag)) continue;

            Vector3 to = hit.bounds.center - origin;
            to.y = 0f;

            float distance = to.magnitude;
            if (distance < 0.01f) continue;

            // Only what is genuinely in front: a target the car has already drawn level with is not
            // something to swing away from.
            if (Vector3.Dot(to / distance, ahead) < 0.15f) continue;
            if (distance >= nearest) continue;

            Vector3 extents = hit.bounds.extents;

            nearest = distance;
            lateral = Vector3.Dot(to, right);
            // How wide it is across the car's own line, taken off its world bounds: the widest the
            // collider can possibly be seen as from here, so a car rounds a long target rather than
            // clipping an end of it.
            halfWidth = Mathf.Abs(right.x) * extents.x + Mathf.Abs(right.z) * extents.z;
            found = true;
        }

        return found;
    }

    /// <summary>
    /// Whether anything up this collider's parents carries the tag. The Adamak puts its tag on its root
    /// and its colliders on its ragdoll parts, so the part itself never has it.
    /// </summary>
    static bool CarriesTag(Transform part, string tag)
    {
        if (string.IsNullOrEmpty(tag)) return false;

        Transform current = part;
        while (current != null)
        {
            if (current.CompareTag(tag)) return true;
            current = current.parent;
        }

        return false;
    }

    /// <summary>
    /// Heard only once per honk, and only by the cars that were actually being honked at: the player has
    /// to be behind the car and within range. Each car pulls over to its own right, which is the shoulder
    /// for a car driving on either side of the road, and the player goes past on the inside.
    /// </summary>
    void ListenForHorn()
    {
        if (LastHornTime <= hornSeen) return;
        hornSeen = LastHornTime;

        Transform honker = LastHornSource;
        if (honker == null) return;

        Vector3 toHonker = honker.position - transform.position;
        if (toHonker.magnitude > hornYieldRange) return;

        toHonker.y = 0f;
        if (toHonker.sqrMagnitude < 0.001f) return;

        // Behind it, not beside or in front of it.
        if (Vector3.Dot(transform.forward, toHonker.normalized) > -0.2f) return;

        yieldUntil = Time.time + hornYieldDuration;
    }

    /// <summary>
    /// Keeps the drive itself from throwing the car. The car is steered by being moved to the place it
    /// is told to be, so a collision that shoves it upwards is fought by the next step and the car hops
    /// down the road. Capping the upward drift keeps the shove without the hop.
    /// </summary>
    void DampVerticalKick()
    {
        Vector3 velocity = rb.velocity;
        if (velocity.y <= maxVerticalDrift) return;

        velocity.y = maxVerticalDrift;
        rb.velocity = velocity;
    }

    // --------------------------------------------------------- knocked about

    bool IsOnItsWheels()
    {
        return Vector3.Angle(transform.up, Vector3.up) <= uprightLimit;
    }

    /// <summary>Stops driving the car and hands it to the physics, which is what makes the hit read.</summary>
    void KnockOffLine()
    {
        knockedOver = true;
        settleTimer = 0f;
        knockedTimer = 0f;
    }

    /// <summary>
    /// A car that is no longer being driven is left entirely to the physics until it has come to rest - and
    /// then it is looked at: one that is still the right way up with ground under it is put back on its path
    /// and drives on, while one that has ended up on its side or on nothing at all is left standing there.
    ///
    /// This is the one place the drive is ever re-attached, so it is deliberately slow to judge: the car has
    /// to be calm (or to have been rolling about for <see cref="settleTimeout"/> seconds) before anything is
    /// decided, which is what keeps the old kangaroo hop from coming back.
    /// </summary>
    void Recover()
    {
        if (rb.isKinematic) return;

        knockedTimer += Time.fixedDeltaTime;

        bool calm = rb.velocity.sqrMagnitude <= settleSpeed * settleSpeed
                    && rb.angularVelocity.sqrMagnitude <= settleSpin * settleSpin;

        settleTimer = calm ? settleTimer + Time.fixedDeltaTime : 0f;

        if (settleTimer < settleTime && knockedTimer < settleTimeout) return;

        if (!IsOnItsWheels() || !IsOnTheGround())
        {
            Strand();
            return;
        }

        ResumeDriving();
    }

    /// <summary>
    /// Leaves the car standing where it is for the rest of the level: on its roof or its side, or with
    /// nothing under it to drive on. Frozen so that nothing - not the drive, not a collision - moves it again.
    /// </summary>
    void Strand()
    {
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = true;
        stranded = true;
    }

    /// <summary>
    /// Puts a settled car back on its path: aim it at the nearest waypoint that is not behind it, stand it
    /// back up if a collision left it leaning, and let the drive take over again.
    /// </summary>
    void ResumeDriving()
    {
        knockedOver = false;
        settleTimer = 0f;
        knockedTimer = 0f;

        if (waypoints == null || waypoints.Length == 0)
        {
            // Nothing to drive along at all: better left standing than shoved around by a drive with no aim.
            Strand();
            return;
        }

        if (levelOnRecovery)
        {
            // Its heading is kept and the lean is dropped, so it carries on the way it was pointing rather
            // than driving on at an angle with its skirts in the road.
            rb.MoveRotation(Quaternion.Euler(0f, rb.rotation.eulerAngles.y, 0f));
        }

        currentWaypoint = NearestWaypointAhead();

        // Whatever it was going around before the hit is no longer its business.
        avoidOffset = 0f;
    }

    /// <summary>
    /// The waypoint a car that has been knocked off its line should head for: the nearest one, preferring a
    /// waypoint it is actually facing so a car that came to rest pointing along the road carries on along it
    /// instead of turning round. If every waypoint is behind it - it ended up facing the wrong way - the
    /// nearest one wins and the ordinary steering turns it back onto its path.
    /// </summary>
    int NearestWaypointAhead()
    {
        Vector3 forward = transform.forward;

        int nearest = currentWaypoint;
        int nearestFacing = -1;
        float nearestDistance = float.MaxValue;
        float nearestFacingDistance = float.MaxValue;

        for (int i = 0; i < waypoints.Length; i++)
        {
            Transform waypoint = waypoints[i];
            if (waypoint == null) continue;

            Vector3 to = waypoint.position - transform.position;
            to.y = 0f;

            float distance = to.sqrMagnitude;
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = i;
            }

            if (distance < nearestFacingDistance && Vector3.Dot(to.normalized, forward) > 0.2f)
            {
                nearestFacingDistance = distance;
                nearestFacing = i;
            }
        }

        return nearestFacing >= 0 ? nearestFacing : nearest;
    }

    /// <summary>
    /// Whether there is anything at all under the car to drive on - the road, the ground, a bridge. Its own
    /// colliders are ignored, and so is anything on a car (a car resting on another car's roof has nowhere
    /// useful to go).
    /// </summary>
    bool IsOnTheGround()
    {
        int count = Physics.RaycastNonAlloc(
            transform.position + Vector3.up * 0.5f, Vector3.down, groundHits, groundCheckDistance,
            ~0, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < count; i++)
        {
            Collider hit = groundHits[i].collider;
            if (hit == null) continue;
            if (hit.transform.IsChildOf(transform)) continue;
            if (hit.GetComponentInParent<AICarController>() != null) continue;

            return true;
        }

        return false;
    }

    void UpdateWheelVisuals()
    {
        if (wheels == null) return;

        foreach (var wheel in wheels)
        {
            if (Physics.Raycast(
                wheel.position + Vector3.up,
                Vector3.down,
                out RaycastHit hit,
                suspensionDistance * 2f,
                groundMask))
            {
                Vector3 pos = wheel.position;
                pos.y = hit.point.y + suspensionDistance;
                wheel.position = pos;
            }
        }
    }
}
