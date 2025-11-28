using UnityEngine;

public class WheelPhysics : MonoBehaviour
{
    Rigidbody carRb;
    CarController car;

    [Header("Suspension")]
    public float restLength = 0.25f;
    public float springTravel = 0.3f;
    public float springStiffness = 80000f;
    public float damperStiffness = 15000f;

    float minLen, maxLen, lastLen, springLen;

    [Header("Wheel Setup")]
    public float wheelRadius = 0.06f;
    public bool isFrontLeft;
    public bool isFrontRight;
    public bool isRearLeft;
    public bool isRearRight;

    [Header("Steering")]
    public float maxSteerAngle = 25f;

    [Header("Grip & Handling")]
    public float lateralGrip = 0.5f;
    public float forwardGrip = 1.0f;
    public float tireMass = 50f;

    [Header("Engine / Brakes")]
    public float engineForce = 5000f;
    public float brakeForce = 8000f;
    public float reverseForce = 2500f;

    [Header("Engine Behavior")]
    public float accelerationRate = 5f;
    public float decelerationRate = 12f;
    private float engineResponse = 0f;

    [Header("Drifting")]
    public float driftThreshold = 3.0f;
    public float driftMultiplier = 0.5f;
    public float driftRecovery = 5f;   

    void Start()
    {
        carRb = GetComponentInParent<Rigidbody>();
        car = GetComponentInParent<CarController>();

        minLen = restLength - springTravel;
        maxLen = restLength + springTravel;
        springLen = restLength;
    }

    void Update()
    {
        if (isFrontLeft || isFrontRight)
        {
            float steer = car.steerInput * maxSteerAngle;
            transform.localRotation = Quaternion.Euler(0f, steer, 0f);
        }
    }

    void FixedUpdate()
    {
        if (!Physics.Raycast(transform.position, -transform.up, out RaycastHit hit, maxLen + wheelRadius))
            return;

        Vector3 springDir = transform.up;
        Vector3 tireVel = carRb.GetPointVelocity(transform.position);

        // - Suspension :
        lastLen = springLen;
        float rawLen = hit.distance - wheelRadius;
        springLen = Mathf.Clamp(rawLen, minLen, maxLen);
        float springVel = (springLen - lastLen) / Time.fixedDeltaTime;

        float springForce = springStiffness * (restLength - springLen);
        float damperForce = -damperStiffness * springVel;
        float suspensionForce = springForce + damperForce;

        carRb.AddForceAtPosition(springDir * suspensionForce, transform.position);

        //  - Drifting :
        Vector3 lateralDir = transform.right;
        float lateralVel = Vector3.Dot(lateralDir, tireVel);
        float speed = carRb.velocity.magnitude;

        // - Speed-based Grip loss :
        float speedGripFactor = Mathf.Lerp(1f, 0.35f, speed / car.topSpeed);

        // - Slip Amount :
        float slip = Mathf.Abs(lateralVel);

        float finalGrip;

        if (slip < driftThreshold)
        {
            // - Grip :
            finalGrip = lateralGrip * speedGripFactor;
        }
        else
        {
            // - Drifting Grip :
            float t = (slip - driftThreshold) / driftThreshold;
            finalGrip = Mathf.Lerp(lateralGrip * speedGripFactor, lateralGrip * driftMultiplier, t);
        }

        // - lateral Friction :
        float desiredLatVelChange = -lateralVel * finalGrip;
        float desiredLatAccel = desiredLatVelChange / Time.fixedDeltaTime;
        carRb.AddForceAtPosition(lateralDir * tireMass * desiredLatAccel, transform.position);

        // - Drift Stabilization :
        if (slip > driftThreshold)
        {
            Vector3 stabilizingForce = -lateralDir * slip * driftRecovery;
            carRb.AddForceAtPosition(stabilizingForce, transform.position);
        }

        // - Engine / Braking / Reverse :
        Vector3 forwardDir = transform.forward;
        float forwardVel = Vector3.Dot(forwardDir, tireVel);

        float throttle = car.throttleInput;

        // - Engine Ramping :
        if (throttle > 0f)
            engineResponse = Mathf.MoveTowards(engineResponse, throttle, accelerationRate * Time.fixedDeltaTime);
        else if (throttle == 0f)
            engineResponse = Mathf.MoveTowards(engineResponse, 0f, decelerationRate * Time.fixedDeltaTime);

        // - Torque Curve Integration -
        float normalizedSpeed = Mathf.Clamp01(Mathf.Abs(forwardVel) / car.topSpeed);
        float torqueMultiplier = car.torqueCurve.Evaluate(normalizedSpeed);

        // - Forward Acceleration :
        if (throttle > 0.01f)
        {
            float torque = engineForce * engineResponse * forwardGrip * torqueMultiplier;
            carRb.AddForceAtPosition(forwardDir * torque, transform.position);
        }

        // - Braking :
        if (throttle < -0.01f && forwardVel > 0.5f)
        {
            float brake = brakeForce * -throttle;
            carRb.AddForceAtPosition(-forwardDir * brake, transform.position);
        }

        // - Reverse :
        if (throttle < -0.01f && forwardVel <= 0.5f)
        {
            float reverse = reverseForce * -throttle;
            carRb.AddForceAtPosition(-forwardDir * reverse, transform.position);
        }

        // - Coasting :
        if (Mathf.Abs(throttle) < 0.01f)
        {
            Vector3 rolling = -forwardDir * forwardVel * 4f;
            carRb.AddForceAtPosition(rolling, transform.position);
        }

        // - Air Drag :
        Vector3 drag = -forwardDir * forwardVel * 20f;
        carRb.AddForceAtPosition(drag, transform.position);
    }
}
