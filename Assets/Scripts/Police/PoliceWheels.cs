using UnityEngine;

public class PoliceWheels : MonoBehaviour
{
    [Header("References")]
    public Rigidbody carRb;
    public PoliceCarController car;

    [Header("Wheel Setup")]
    public bool isFrontWheel;
    public bool isDriveWheel;

    [Header("Suspension")]
    public float restLength = 0.35f;
    public float springTravel = 0.25f;
    public float springStrength = 52000f;
    public float damperStrength = 6000f;

    [Header("Wheel")]
    public float wheelRadius = 0.35f;

    [Header("Grip")]
    public float lateralGrip = 1.2f;
    public float maxLateralForce = 9000f;

    [Header("Engine")]
    public float engineForce = 18000f;
    public float brakeForce = 14000f;

    [Header("Drag")]
    public float rollingResistance = 0.02f;

    float minLen, maxLen, curLen, lastLen;
    float lastSpringForce;

    void Start()
    {
        minLen = restLength - springTravel;
        maxLen = restLength + springTravel;
        curLen = restLength;
    }

    void FixedUpdate()
    {
        if (!carRb || !car) return;

        if (!Physics.Raycast(transform.position, -transform.up,
            out RaycastHit hit, maxLen + wheelRadius))
        {
            lastSpringForce = 0f;
            return;
        }

        // -------- Suspension --------
        lastLen = curLen;
        curLen = Mathf.Clamp(hit.distance - wheelRadius, minLen, maxLen);

        float compression = restLength - curLen;
        float springVel = (curLen - lastLen) / Time.fixedDeltaTime;

        float springForce = compression * springStrength
                          - springVel * damperStrength;

        lastSpringForce = Mathf.Max(0f, springForce);

        carRb.AddForceAtPosition(transform.up * springForce, transform.position);

        // -------- Wheel velocity --------
        Vector3 wheelVel = carRb.GetPointVelocity(transform.position);

        Vector3 forward = transform.forward;
        Vector3 right = transform.right;

        if (isFrontWheel)
        {
            Quaternion steerRot = Quaternion.AngleAxis(car.steerAngle, transform.up);
            forward = steerRot * forward;
            right = Vector3.Cross(transform.up, forward).normalized;
        }

        float fwdVel = Vector3.Dot(forward, wheelVel);
        float latVel = Vector3.Dot(right, wheelVel);

        // -------- LATERAL GRIP (LOAD-BASED) --------
        float desiredLatForce = -latVel * lastSpringForce * lateralGrip;
        desiredLatForce = Mathf.Clamp(desiredLatForce, -maxLateralForce, maxLateralForce);

        carRb.AddForceAtPosition(right * desiredLatForce, transform.position);

        // -------- FORWARD FORCE (SLIP-AWARE) --------
        if (isDriveWheel)
        {
            float slip = Mathf.Abs(latVel);
            float traction = Mathf.Clamp01(1f - slip * 0.08f);

            float throttle = car.throttleInput * traction;

            if (throttle > 0f)
            {
                carRb.AddForceAtPosition(
                    forward * throttle * engineForce,
                    transform.position
                );
            }
            else if (throttle < 0f)
            {
                carRb.AddForceAtPosition(
                    forward * throttle * brakeForce,
                    transform.position
                );
            }
        }

        // -------- Rolling resistance --------
        carRb.AddForceAtPosition(
            -forward * fwdVel * Mathf.Abs(fwdVel) * rollingResistance,
            transform.position
        );
    }
}
