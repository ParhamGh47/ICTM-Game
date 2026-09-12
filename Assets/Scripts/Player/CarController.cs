using UnityEngine;
using RoadArchitect;

public class CarController : MonoBehaviour
{
    public Rigidbody rb { get; private set; }

    [Header("Input")]
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
    public float minSteerPercent = 0.2f;
    public float steerFadeSpeed = 30f;

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

    private Road[] cachedRoads;
    private CheckpointIndicator cachedCompass;

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
    throttleInput = Input.GetAxis("Vertical");


    float playerSteerInput = Input.GetAxis("Horizontal");

    steerInput = playerSteerInput;


    if (Input.GetKey(KeyCode.Space))
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


    public float GetSpeedAdjustedSteer()
    {
        float s = currentSpeedKPH;

        if (s <= steerFadeSpeed)
            return 1f;

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

        return percent;
    }

    public bool IsFocusing()
    {
        return Input.GetKey(KeyCode.Space)
            && enableFocus
            && currentFocusTarget != null;
    }


    private void FixedUpdate()
    {
        if (Input.GetKeyDown(KeyCode.R))
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

            if (TryGetRoadResetPosition(resetPosition, yaw, out onRoad, out roadForward))
            {
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


    private bool TryGetRoadResetPosition(Vector3 from, float yaw, out Vector3 result, out Vector3 roadForward)
    {
        result = from;
        roadForward = Vector3.zero;

        if (cachedRoads == null || cachedRoads.Length == 0)
            cachedRoads = FindObjectsOfType<Road>();

        if (cachedRoads == null || cachedRoads.Length == 0)
            return false;

        Vector3 heading =
            Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;

        heading.y = 0f;

        if (heading.sqrMagnitude < 0.0001f)
            heading = Vector3.forward;
        else
            heading.Normalize();

        // Which way counts as progressing? The compass already knows - it points
        // at the next checkpoint the player has to reach. Without checkpoints
        // (playground scenes) fall back to the way the car is pointing.
        Transform nextCheckpoint = GetNextCheckpoint();
        Vector3 progressDirection = heading;

        if (nextCheckpoint != null)
        {
            Vector3 toNext = nextCheckpoint.position - from;
            toNext.y = 0f;

            if (toNext.sqrMagnitude > 1f)
                progressDirection = toNext.normalized;
        }

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