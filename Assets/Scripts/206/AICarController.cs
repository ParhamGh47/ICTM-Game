using UnityEngine;

public class AICarController : MonoBehaviour
{
    [Header("Rigidbody & Wheels")]
    public Rigidbody rb;
    public Transform frontLeftWheel;
    public Transform frontRightWheel;
    public Transform rearLeftWheel;
    public Transform rearRightWheel;

    [Header("Wheel / Suspension")]
    public float wheelRadius = 0.06f;
    public float restLength = 0.25f;
    public float springTravel = 0.3f;
    public float springStiffness = 80000f;
    public float damperStiffness = 15000f;

    [Header("Steering")]
    public float maxSteerAngle = 25f;
    public float minSteerPercent = 0.2f;
    public float steerFadeSpeed = 40f;

    [Header("Engine / Grip")]
    public float topSpeed = 80f;
    public float engineForce = 5000f;
    public float brakeForce = 8000f;
    public float reverseForce = 1500f;
    public float forwardGrip = 1f;
    public float lateralGrip = 0.8f;
    public AnimationCurve torqueCurve = new AnimationCurve(
        new Keyframe(0f, 0.5f),
        new Keyframe(0.35f, 1f),
        new Keyframe(1f, 0f)
    );

    [Header("Drifting")]
    public float driftThreshold = 7f;
    public float driftMultiplier = 1.2f;
    public float driftRecovery = 5f;

    [Header("AI Settings")]
    public Transform target;
    public float desiredSpeed = 50f; // km/h
    public float slowDownAngle = 35f;
    public float stopDistance = 2f;

    [Header("Engine Response")]
    public float accelerationRate = 5f;
    public float decelerationRate = 12f;

    // Internal
    private float throttleInput;
    private float steerInput;
    private float engineResponse;
    private float currentSpeedKPH;

    private float minLen, maxLen;
    private float[] springLen = new float[4];
    private float[] lastLen = new float[4];

    void Start()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody>();

        rb.drag = 0.05f;
        rb.angularDrag = 5f;

        minLen = restLength - springTravel;
        maxLen = restLength + springTravel;

        for (int i = 0; i < 4; i++)
            springLen[i] = restLength;
    }

    void FixedUpdate()
    {
        if (target != null)
            ComputeAIInput();

        UpdateSpeed();

        HandleWheel(frontLeftWheel, 0, true);
        HandleWheel(frontRightWheel, 1, true);
        HandleWheel(rearLeftWheel, 2, false);
        HandleWheel(rearRightWheel, 3, false);
    }

    void ComputeAIInput()
    {
        Vector3 toTarget = target.position - transform.position;
        toTarget.y = 0f;

        float distance = toTarget.magnitude;
        if (distance < 0.1f)
        {
            throttleInput = 0f;
            steerInput = 0f;
            return;
        }

        Vector3 dir = toTarget.normalized;
        float angle = Vector3.SignedAngle(transform.forward, dir, Vector3.up);

        // Steering
        steerInput = Mathf.Clamp(angle / maxSteerAngle, -1f, 1f);
        steerInput *= GetSpeedAdjustedSteer();

        // Throttle
        float speed = rb.velocity.magnitude * 3.6f;
        throttleInput = 1f;

        if (Mathf.Abs(angle) > slowDownAngle)
            throttleInput = 0.5f;

        if (speed > desiredSpeed)
            throttleInput = 0f;

        if (distance < stopDistance)
            throttleInput = -0.3f;
    }

    float GetSpeedAdjustedSteer()
    {
        if (currentSpeedKPH <= steerFadeSpeed)
            return 1f;

        float t = Mathf.InverseLerp(steerFadeSpeed, topSpeed, currentSpeedKPH);
        return Mathf.Lerp(1f, minSteerPercent, t);
    }

    void UpdateSpeed()
    {
        currentSpeedKPH = rb.velocity.magnitude * 3.6f;
    }

    void HandleWheel(Transform wheel, int index, bool isFront)
    {
        if (!Physics.Raycast(wheel.position, -wheel.up, out RaycastHit hit, maxLen + wheelRadius))
            return;

        Vector3 springDir = wheel.up;
        Vector3 tireVel = rb.GetPointVelocity(wheel.position);

        // Suspension
        lastLen[index] = springLen[index];
        float rawLen = hit.distance - wheelRadius;
        springLen[index] = Mathf.Clamp(rawLen, minLen, maxLen);
        float springVel = (springLen[index] - lastLen[index]) / Time.fixedDeltaTime;

        float springForce = springStiffness * (restLength - springLen[index]);
        float damperForce = -damperStiffness * springVel;
        rb.AddForceAtPosition(springDir * (springForce + damperForce), wheel.position);

        // Lateral Grip / Drifting
        Vector3 lateralDir = wheel.right;
        float lateralVel = Vector3.Dot(lateralDir, tireVel);
        float slip = Mathf.Abs(lateralVel);
        float speedFactor = Mathf.Lerp(1f, 0.35f, rb.velocity.magnitude / topSpeed);

        float finalGrip = lateralGrip * speedFactor;
        if (slip > driftThreshold)
            finalGrip = Mathf.Lerp(lateralGrip * speedFactor, lateralGrip * driftMultiplier, (slip - driftThreshold) / driftThreshold);

        float desiredLatVelChange = -lateralVel * finalGrip;
        float desiredLatAccel = desiredLatVelChange / Time.fixedDeltaTime;
        rb.AddForceAtPosition(lateralDir * desiredLatAccel, wheel.position);

        if (slip > driftThreshold)
        {
            Vector3 stabilizingForce = -lateralDir * slip * driftRecovery;
            rb.AddForceAtPosition(stabilizingForce, wheel.position);
        }

        // Engine / Braking / Reverse
        Vector3 forwardDir = wheel.forward;
        float forwardVel = Vector3.Dot(forwardDir, tireVel);

        // Engine response smoothing
        if (throttleInput > 0f)
            engineResponse = Mathf.MoveTowards(engineResponse, throttleInput, accelerationRate * Time.fixedDeltaTime);
        else if (throttleInput == 0f)
            engineResponse = Mathf.MoveTowards(engineResponse, 0f, decelerationRate * Time.fixedDeltaTime);

        float normalizedSpeed = Mathf.Clamp01(Mathf.Abs(forwardVel) / topSpeed);
        float torqueMultiplier = torqueCurve.Evaluate(normalizedSpeed);

        // Forward / Brake / Reverse
        if (throttleInput > 0.01f)
            rb.AddForceAtPosition(forwardDir * engineForce * engineResponse * forwardGrip * torqueMultiplier, wheel.position);
        else if (throttleInput < -0.01f && forwardVel > 0.5f)
            rb.AddForceAtPosition(-forwardDir * brakeForce * -throttleInput, wheel.position);
        else if (throttleInput < -0.01f)
            rb.AddForceAtPosition(-forwardDir * reverseForce * -throttleInput, wheel.position);

        // Rolling friction
        if (Mathf.Abs(throttleInput) < 0.01f)
            rb.AddForceAtPosition(-forwardDir * forwardVel * 300f, wheel.position);

        // Drag
        rb.AddForceAtPosition(-forwardDir * forwardVel * Mathf.Abs(forwardVel) * 1.2f, wheel.position);

        // Steering visuals
        if (isFront)
            wheel.localRotation = Quaternion.Euler(0f, steerInput * maxSteerAngle, 0f);
    }
}
