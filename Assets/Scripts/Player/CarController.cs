using UnityEngine;
using RoadArchitect;

public class CarController : MonoBehaviour
{
    public Rigidbody rb { get; private set; }

    [Header("Input")]
    [Tooltip("Filled in every frame from the keyboard and the gamepad, both normalised to -1..1.")]
    public float throttleInput;
    public float steerInput;

    [Header("Engine Settings")]
    public float topSpeed = 120f;

    public AnimationCurve torqueCurve = new AnimationCurve(
        new Keyframe(0f, 0.5f, 0f, 1.5f),
        new Keyframe(0.35f, 1.0f, 1.5f, -1.5f),
        new Keyframe(1f, 0.0f, -1.5f, 0f)
    );

    [Header("Speed Readout")]
    public float currentSpeedKPH;

    [Header("Speed-Based Steering")]
    [Tooltip("How much of its steering the truck keeps at top speed. 0.2 = a fifth of the lock " +
             "available at a crawl, so a nudge at 120 km/h does not throw the truck across the road.")]
    public float minSteerPercent = 0.2f;

    [Tooltip("Speed (km/h) up to which the truck steers with its full lock. Above it the steering " +
             "fades away, reaching Min Steer Percent at top speed.")]
    public float steerFadeSpeed = 30f;

    [Tooltip("Extra steering the truck gets when it is barely moving, so it can be turned around on " +
             "the spot. Fades to 1 by the steer fade speed.")]
    public float lowSpeedSteerBoost = 1.6f;

    [Header("Brake Lights")]
    public Renderer brakeLightRenderer;
    public float brakeThreshold = -0.1f;

    private Material brakeMat;

    [Header("Reset Cooldown")]
    public float resetCooldown = 2f;
    private float lastResetTime = -999f;

    [Header("Reset Onto Road")]
    [Tooltip("Drop the car back onto the nearest road instead of leaving it wherever it ended up.")]
    public bool resetOntoRoad = true;

    [Tooltip("Only snap to a road closer than this. 0 = no limit.")]
    public float resetMaxRoadDistance = 0f;

    [Tooltip("How strongly a road is preferred for pointing the same way as the car " +
             "rather than being a crossing road (metres of lead).")]
    public float resetAlignmentWeight = 25f;

    [Tooltip("Face the car down the road towards the next checkpoint, so a reset " +
             "never leaves it pointing sideways across the tarmac.")]
    public bool resetFacesForward = true;

    [Tooltip("When the car is reset somewhere with no road under it - in mid air over the gap before a " +
             "jump, or down a bank - send it back to the last stretch of road it actually drove on " +
             "instead of onto whichever road happens to be nearest.")]
    public bool resetToLastRoad = true;

    [Tooltip("How far below the car to look for road when remembering the last place it was on one. " +
             "Bigger means a jump counts as \"over the road\" for longer before the car is treated as off it.")]
    public float roadCheckDistance = 2.5f;

    [Tooltip("How far along the level a reset may still land, in metres, before it counts as having skipped " +
             "ahead of the last place the car had road under it. A few metres covers the nearest point of a " +
             "road the car has wandered off the side of; a jump is longer than this.")]
    public float resetForwardTolerance = 6f;

    [Tooltip("When a reset sends the car back to the last road it drove on, how many road nodes back to put " +
             "it - and so on. The car is dropped a little way up the road it took off from rather than on its " +
             "last centimetre, where the next nudge tips it straight back over the edge.")]
    public int resetBackOffNodes = 2;

    [Tooltip("The furthest back that may put it, in metres. Deliberately generous: a level's own nodes are " +
             "15-90 m apart, so at three hundred metres this stops being the thing that decides where the car " +
             "lands and the node count above takes over - the reset goes properly back up the last road the " +
             "car was on instead of stopping one bend short of it.")]
    public float resetBackOffMetres = 300f;

    private Road[] cachedRoads;
    private CheckpointIndicator cachedCompass;

    // The last pose at which the car had road beneath it: what a reset in mid air goes back to.
    private Vector3 lastRoadPosition;
    private float lastRoadYaw;
    private bool hasLastRoadPose;

    // Shared so the check costs no allocation every frame. Several colliders can sit under the car at once
    // (the car's own body, the road, a bridge deck), so a few slots are needed, not one.
    private static readonly RaycastHit[] roadHits = new RaycastHit[8];

    [Header("Focus System")]
    public bool enableFocus = true;

    [Tooltip("Tag used for focus targets")]
    public string focusTag = "Adamak";

    [Tooltip("Maximum distance the car can lock onto a target")]
    public float focusDistance = 10f;

    [Tooltip("Half-angle of the focus cone")]
    [Range(1f, 90f)]
    public float focusConeAngle = 45f;

    [Tooltip("How strongly the car steers toward the target")]
    public float focusSteerStrength = 3f;

    private Transform currentFocusTarget;

    public bool isShiftingUp = false;
    public bool isShiftingDown = false;


    void Start()
    {
        rb = GetComponent<Rigidbody>();

        rb.drag = 0.05f;
        rb.angularDrag = 5f;

        if (brakeLightRenderer != null)
            brakeMat = brakeLightRenderer.material;
    }


void Update()
{
    if (IsPaused())
    {
        // The wheels take their throttle and steering from these two fields, so a paused car has to
        // stop asking for any: with a trigger held through the pause menu it would otherwise be
        // quietly building speed up behind the frozen truck.
        throttleInput = 0f;
        steerInput = 0f;
        currentFocusTarget = null;
        return;
    }

    throttleInput = GameInput.Throttle();
    steerInput = GameInput.Steer();


    if (GameInput.FocusHeld())
    {
        if (!enableFocus)
        {
            steerInput = 0f;
            currentFocusTarget = null;
        }
        else
        {
            currentFocusTarget = FindFocusTarget();


            if (currentFocusTarget != null)
            {
                Vector3 toTarget =
                    currentFocusTarget.position - transform.position;


                toTarget.y = 0f;


                float angle =
                    Vector3.SignedAngle(
                        transform.forward,
                        toTarget.normalized,
                        Vector3.up);



                steerInput =
                    Mathf.Clamp(
                        angle / focusConeAngle,
                        -1f,
                        1f)
                    * focusSteerStrength;
            }
            else
            {
                steerInput = 0f;
            }
        }
    }
    else
    {
        currentFocusTarget = null;
    }


    float mps = rb.velocity.magnitude;

    currentSpeedKPH =
        mps * 3.6f;


    UpdateBrakeLights();
}


    private Transform FindFocusTarget()
    {
        GameObject[] targets =
            GameObject.FindGameObjectsWithTag(focusTag);

        Transform bestTarget = null;

        float bestDistance = Mathf.Infinity;

        Vector3 origin = transform.position;
        Vector3 forward = transform.forward;


        foreach (GameObject obj in targets)
        {
            Vector3 toTarget =
                obj.transform.position - origin;

            float distance =
                toTarget.magnitude;

            if (distance > focusDistance)
                continue;

            float angle =
                Vector3.Angle(
                    forward,
                    toTarget);

            if (angle > focusConeAngle)
                continue;


            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestTarget = obj.transform;
            }
        }


        return bestTarget;
    }


    private void UpdateBrakeLights()
    {
        if (brakeLightRenderer == null)
            return;

        // The customize screen repaints the truck by swapping a part's material for a copy of its own,
        // which would leave the brake lens this car grabbed at startup orphaned - and a repainted truck
        // with brake lights that never come on. So the lens is taken from the renderer whenever it is
        // no longer the material this car is holding.
        if (brakeMat == null || brakeLightRenderer.sharedMaterial != brakeMat)
            brakeMat = brakeLightRenderer.sharedMaterial;

        if (brakeMat == null)
            return;

        if (throttleInput < brakeThreshold)
        {
            brakeMat.EnableKeyword("_EMISSION");
        }
        else
        {
            brakeMat.DisableKeyword("_EMISSION");
        }
    }


    /// <summary>
    /// How much of the wheels' lock the truck is allowed to use right now, as a multiplier on the
    /// wheel angle: a little more than 1 at a standstill so it can be turned around, 1 up to the
    /// steer fade speed, then progressively less as it goes faster.
    ///
    /// This is the whole of the speed based steering. The wheels are what turn the truck, so scaling
    /// their angle here is what makes a fast truck feel planted while a slow one stays nimble - it is
    /// the same for a keyboard, a stick and a trigger, because it is applied after the input.
    /// </summary>
    public float GetSpeedAdjustedSteer()
    {
        float s = currentSpeedKPH;

        float boost =
            Mathf.Lerp(
                lowSpeedSteerBoost,
                1f,
                Mathf.InverseLerp(
                    0f,
                    steerFadeSpeed,
                    s));

        if (s <= steerFadeSpeed)
            return boost;

        float t =
            Mathf.InverseLerp(
                steerFadeSpeed,
                topSpeed,
                s);

        float percent =
            Mathf.Lerp(
                1f,
                minSteerPercent,
                t);

        return boost * percent;
    }

    public bool IsFocusing()
    {
        return GameInput.FocusHeld()
            && enableFocus
            && currentFocusTarget != null;
    }


    private static bool IsPaused()
    {
        return PauseTracker.Instance != null && PauseTracker.Instance.isPaused;
    }


    private void FixedUpdate()
    {
        TrackLastRoadPose();

        if (GameInput.ResetPressed())
        {
            if (Time.time - lastResetTime >= resetCooldown)
            {
                ResetCar();

                lastResetTime = Time.time;
            }
        }
    }


    private void ResetCar()
    {
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        float yaw = transform.eulerAngles.y;

        // Land the car on the road instead of wherever it got stuck: find the
        // closest road that runs the way the level needs and drop it into the
        // matching lane. The lift keeps the old "reset from above" feel, it just
        // comes down over asphalt now, facing the way the level goes.
        Vector3 resetPosition = transform.position;

        if (resetOntoRoad)
        {
            Vector3 onRoad, roadForward;

            if (TryGetRoadResetPosition(transform.position, yaw, out onRoad, out roadForward))
            {
                // A reset must never hand the driver ground for free. Over the gap before a jump the
                // nearest tarmac is the landing road ahead, and dropping the car onto it would skip the
                // jump it was meant to clear - so when the car is off the road and the search has come up
                // with a spot further along the level than the last place it had tarmac under it, the
                // search is run again from that place instead, which finds the ramp it took off from.
                if (resetToLastRoad && hasLastRoadPose && !HasRoadBeneath() &&
                    IsAheadOfLastRoad(onRoad, yaw))
                {
                    Vector3 onRamp, rampForward;

                    if (TryGetRoadResetPosition(lastRoadPosition, lastRoadYaw, out onRamp,
                                                out rampForward, true))
                    {
                        onRoad = onRamp;
                        roadForward = rampForward;
                    }
                }

                resetPosition = onRoad;

                if (resetFacesForward && roadForward.sqrMagnitude > 0.0001f)
                    yaw = Quaternion.LookRotation(roadForward, Vector3.up).eulerAngles.y;
            }
        }

        Vector3 uprightEuler =
            new Vector3(
                0f,
                yaw,
                2.6f);

        transform.rotation =
            Quaternion.Euler(uprightEuler);

        transform.position = resetPosition + Vector3.up * 1.6f;
    }


    /// <summary>
    /// Whether a road position lies further along the level than the last stretch of road the car drove on.
    /// Ground like that is ground the driver has not earned: it is a landing ramp, or the far side of the
    /// jump, and a reset there is a free skip.
    ///
    /// The level's own direction is what is measured along - towards the next checkpoint - and the tolerance
    /// is what keeps an ordinary off-road reset honest, because the nearest point on a road the car has
    /// merely wandered off the side of is level with the car, not ahead of it.
    /// </summary>
    private bool IsAheadOfLastRoad(Vector3 candidate, float yaw)
    {
        Vector3 direction = ProgressDirection(lastRoadPosition, yaw);

        Vector3 step = candidate - lastRoadPosition;
        step.y = 0f;

        return Vector3.Dot(step, direction) > resetForwardTolerance;
    }


    /// <summary>
    /// Remembers the last pose at which the car had road under it: where a reset goes when the car is
    /// somewhere without any - in mid air over the gap before a jump, or down a bank.
    ///
    /// The pose is the car's own rather than a point on the road: it is the place the driver last remembers
    /// being, and the reset search turns it into a proper lane position and heading anyway.
    /// </summary>
    private void TrackLastRoadPose()
    {
        if (!resetOntoRoad || !resetToLastRoad) return;

        if (!HasRoadBeneath()) return;

        lastRoadPosition = transform.position;
        lastRoadYaw = transform.eulerAngles.y;
        hasLastRoadPose = true;
    }


    /// <summary>
    /// Whether the car has road directly below it.
    ///
    /// The car's own colliders are skipped by hand rather than by layer: a ray that starts inside a collider
    /// is only ignored for convex shapes, so which panels of the truck would have swallowed the ray is not
    /// something to rely on. Any road hit within the check distance will do - a bridge deck and the road
    /// under it are both road, and both mean the car is on it rather than off in the air.
    /// </summary>
    private bool HasRoadBeneath()
    {
        int count =
            Physics.RaycastNonAlloc(
                transform.position,
                Vector3.down,
                roadHits,
                Mathf.Max(0.5f, roadCheckDistance),
                ~0,
                QueryTriggerInteraction.Ignore);

        for (int i = 0; i < count; i++)
        {
            Collider collider = roadHits[i].collider;

            if (collider == null) continue;
            if (collider.transform.IsChildOf(transform)) continue;
            if (collider.GetComponentInParent<Road>() == null) continue;

            return true;
        }

        return false;
    }


    /// <summary>The way a heading points, flattened onto the ground plane.</summary>
    private static Vector3 HeadingOf(float yaw)
    {
        Vector3 heading =
            Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;

        heading.y = 0f;

        return heading.sqrMagnitude < 0.0001f ? Vector3.forward : heading.normalized;
    }


    /// <summary>
    /// Which way counts as progressing from a given point. The compass already knows - it points at the
    /// next checkpoint the player has to reach. Without checkpoints (playground scenes) fall back to the
    /// way the car is pointing.
    /// </summary>
    private Vector3 ProgressDirection(Vector3 from, float yaw)
    {
        Vector3 heading = HeadingOf(yaw);

        Transform nextCheckpoint = GetNextCheckpoint();
        if (nextCheckpoint == null) return heading;

        Vector3 toNext = nextCheckpoint.position - from;
        toNext.y = 0f;

        return toNext.sqrMagnitude > 1f ? toNext.normalized : heading;
    }


    /// <summary>
    /// Finds where on a road a reset from <paramref name="from"/> belongs, and which way it should face.
    ///
    /// <paramref name="backOff"/> is for the reset that sends a driver back to the road they last drove on:
    /// that pose is usually right at the lip of a jump, so the spot is walked a little further back up the
    /// road instead of being dropped on the edge.
    /// </summary>
    private bool TryGetRoadResetPosition(Vector3 from, float yaw, out Vector3 result,
                                         out Vector3 roadForward, bool backOff = false)
    {
        result = from;
        roadForward = Vector3.zero;

        if (cachedRoads == null || cachedRoads.Length == 0)
            cachedRoads = FindObjectsOfType<Road>();

        if (cachedRoads == null || cachedRoads.Length == 0)
            return false;

        Vector3 heading = HeadingOf(yaw);
        Vector3 progressDirection = ProgressDirection(from, yaw);

        Road bestRoad = null;
        float bestParam = 0f;
        float bestDistance = float.MaxValue;
        float bestScore = float.MaxValue;

        foreach (Road road in cachedRoads)
        {
            if (road == null || road.spline == null)
                continue;

            // Unbuilt / broken roads make GetClosestParam misbehave, skip them.
            if (road.spline.distance <= 0.01f || road.spline.GetNodeCount() < 2)
                continue;

            float param =
                road.spline.GetClosestParam(from, false, true);

            Vector3 roadPosition, roadTangent;
            road.spline.GetSplineValueBoth(param, out roadPosition, out roadTangent);

            roadTangent.y = 0f;

            if (roadTangent.sqrMagnitude < 0.0001f)
                continue;

            roadTangent.Normalize();

            float distance =
                Vector2.Distance(
                    new Vector2(from.x, from.z),
                    new Vector2(roadPosition.x, roadPosition.z));

            // |dot| ~ 1 means the road runs the way we need to go (either
            // direction), ~ 0 means it crosses it. Crossing roads are pushed
            // away so the car does not get parked on a road it was not on.
            float alignment =
                Mathf.Abs(Vector3.Dot(progressDirection, roadTangent));

            float score =
                distance + resetAlignmentWeight * (1f - alignment);

            if (score < bestScore)
            {
                bestScore = score;
                bestRoad = road;
                bestParam = param;
                bestDistance = distance;
            }
        }

        if (bestRoad == null)
            return false;

        if (resetMaxRoadDistance > 0f && bestDistance > resetMaxRoadDistance)
            return false;

        Vector3 position, tangent;
        bestRoad.spline.GetSplineValueBoth(bestParam, out position, out tangent);

        tangent.y = 0f;

        if (tangent.sqrMagnitude < 0.0001f)
            return false;

        tangent.Normalize();

        // Pick the side of the road that leads to the next checkpoint. If the
        // checkpoint sits almost straight across (hairpin), that chord is not
        // reliable, so keep the direction the car was already travelling.
        float alongRoad =
            Vector3.Dot(tangent, progressDirection);

        if (Mathf.Abs(alongRoad) < 0.25f)
            alongRoad = Vector3.Dot(tangent, heading);

        float direction =
            alongRoad >= 0f ? 1f : -1f;

        // A reset that has sent the driver back to the road they took off from should not put them on its
        // last few centimetres: walk the spot back up the road by whole nodes, so there is run-up between
        // them and the edge they fell off.
        if (backOff && resetBackOffNodes > 0)
        {
            float backed =
                BackOffAlongRoad(bestRoad, bestParam, direction, resetBackOffNodes, resetBackOffMetres);

            if (Mathf.Abs(backed - bestParam) > 0.00001f)
            {
                bestParam = backed;

                bestRoad.spline.GetSplineValueBoth(bestParam, out position, out tangent);

                tangent.y = 0f;

                if (tangent.sqrMagnitude < 0.0001f)
                    return false;

                tangent.Normalize();
            }
        }

        roadForward =
            tangent * direction;

        Vector3 right =
            new Vector3(tangent.z, 0f, -tangent.x);

        // Drive in the lane that matches the way the car will be facing.
        float laneOffset =
            GetLaneCenterOffset(bestRoad);

        result =
            position + right * (direction > 0f ? laneOffset : -laneOffset);

        return true;
    }


    /// <summary>
    /// Walks a point on a road back the way the car came: the given number of whole road nodes, but never
    /// further than the given distance in metres. Returns a spline param, and never past either end of the
    /// road.
    ///
    /// Node distances are in metres and increase along the spline, and a node's time span is its real length
    /// over the road's (that is how RoadArchitect builds them), so param times the road's length is the
    /// distance along it - which is what lets the metric cap and the node walk share one answer.
    ///
    /// Nodes are counted against the direction of travel, so "back" means back towards where the car has
    /// been rather than towards the next corner, and the spline's special end nodes are skipped: the road
    /// does not visibly have them, and stopping on one is how a car ends up parked on nothing.
    /// </summary>
    private static float BackOffAlongRoad(Road road, float param, float direction, int nodes, float metres)
    {
        SplineC spline = road != null ? road.spline : null;

        if (spline == null || spline.nodes == null || spline.nodes.Count < 2)
            return param;

        float length = Mathf.Max(1f, spline.distance);
        float travelled = Mathf.Clamp01(param) * length;

        int count = spline.nodes.Count;
        bool ascending = direction >= 0f;

        // The last node the car has reached, walking the way it is going.
        int at = -1;
        for (int i = 0; i < count; i++)
        {
            SplineN node = spline.nodes[i];
            if (node != null && node.dist <= travelled + 0.01f) at = i;
        }

        if (at < 0) at = 0;

        // Then the requested number of nodes further back, which is the opposite index direction to travel.
        // Against the way the car is going, `at` has just been passed and is already one node behind it;
        // the other way round it is still ahead, so the node a step further on is the one behind. The first
        // node behind therefore counts as one of the requested nodes, and the rest are walked from there.
        int step = ascending ? -1 : 1;
        int index = at;
        int remaining = Mathf.Max(0, nodes - (ascending ? 1 : 0));

        while (remaining > 0)
        {
            int next = index + step;
            while (next >= 0 && next < count && !spline.nodes[next].IsLegitimateGrade()) next += step;

            if (next < 0 || next >= count) break;

            index = next;
            remaining--;
        }

        float byNodes = spline.nodes[index].dist;
        float byMetres = ascending ? travelled - metres : travelled + metres;

        // The nearer of the two, so the node walk is what places the car and the metres are what stops it
        // going too far back on a road whose nodes are far apart.
        float target = ascending
            ? Mathf.Max(byNodes, byMetres)
            : Mathf.Min(byNodes, byMetres);

        target = Mathf.Clamp(target, 0f, length);

        return Mathf.Clamp01(param + (target - travelled) / length);
    }


    private Transform GetNextCheckpoint()
    {
        if (cachedCompass == null)
            cachedCompass = FindObjectOfType<CheckpointIndicator>(true);

        return cachedCompass != null ? cachedCompass.GetNextCheckpoint() : null;
    }


    // Centre of a driving lane, measured from the road centreline. Clamped so
    // the car can never be placed off the asphalt.
    private static float GetLaneCenterOffset(Road road)
    {
        float halfRoad =
            road.RoadWidth() * 0.5f;

        float offset =
            road.laneAmount >= 2
                ? (road.laneAmount / 2f - 0.5f) * road.laneWidth
                : 0f;

        return Mathf.Clamp(offset, 0f, Mathf.Max(0f, halfRoad - 0.5f));
    }


    private void OnDrawGizmosSelected()
    {
        if (!enableFocus)
            return;

        Vector3 origin = transform.position;
        Vector3 forward = transform.forward;

        Vector3 leftDir =
            Quaternion.Euler(
                0f,
                -focusConeAngle,
                0f)
            * forward;

        Vector3 rightDir =
            Quaternion.Euler(
                0f,
                focusConeAngle,
                0f)
            * forward;


        Gizmos.color = Color.yellow;


        Gizmos.DrawLine(
            origin,
            origin + leftDir * focusDistance);

        Gizmos.DrawLine(
            origin,
            origin + rightDir * focusDistance);


        int segments = 24;

        Vector3 previous =
            origin + leftDir * focusDistance;


        for (int i = 1; i <= segments; i++)
        {
            float t =
                i / (float)segments;

            float angle =
                Mathf.Lerp(
                    -focusConeAngle,
                    focusConeAngle,
                    t);

            Vector3 dir =
                Quaternion.Euler(
                    0f,
                    angle,
                    0f)
                * forward;


            Vector3 point =
                origin +
                dir *
                focusDistance;


            Gizmos.DrawLine(
                previous,
                point);

            previous = point;
        }


        if (currentFocusTarget != null)
        {
            Gizmos.color = Color.green;

            Gizmos.DrawLine(
                origin,
                currentFocusTarget.position);

            Gizmos.DrawSphere(
                currentFocusTarget.position,
                0.5f);
        }
    }
}