using UnityEngine;

public class TireSkidController : MonoBehaviour
{
    [Header("References")]
    public CarController car;
    public TrailRenderer rearLeftTrail;
    public TrailRenderer rearRightTrail;

    [Header("Speed Thresholds (km/h)")]
    public float steerSkidSpeed = 50f;
    public float hardTurnSpeed = 100f;

    [Header("Braking")]
    public float brakeSkidAmount = 0.1f; // input threshold to consider braking

    [Header("Movement thresholds (m/s)")]
    public float forwardThreshold = 0.5f; // consider moving forward above this (m/s)
    public float reverseThreshold = 0.5f; // consider moving reverse below -this (m/s)

    [Header("Steering")]
    public float steerSensitivity = 0.7f;

    [Header("Smoothing")]
    public float skidFadeSpeed = 8f;

    [Header("Debug")]
    public bool showDebug = false;

    private float leftAlpha = 0f;
    private float rightAlpha = 0f;

    void Start()
    {
        // Defensive: make sure trails start invisible
        InitTrail(rearLeftTrail);
        InitTrail(rearRightTrail);
    }

    void Update()
    {
        // Safety checks
        if (car == null || car.rb == null)
        {
            if (showDebug) Debug.LogWarning("TireSkidController: assign CarController and ensure its rb is set.");
            return;
        }

        // get inputs/state
        float steer = car.steerInput;
        float throttle = car.throttleInput;
        float speedKPH = car.currentSpeedKPH;

        // forward velocity along car forward (m/s)
        float forwardVel = Vector3.Dot(car.rb.velocity, car.transform.forward);

        bool movingForward = forwardVel > forwardThreshold;
        bool movingReverse = forwardVel < -reverseThreshold;

        // braking detection relative to motion direction:
        // - if moving forward: braking input is negative (throttle < -brakeSkidAmount)
        // - if moving reverse: braking input is positive (throttle > brakeSkidAmount)
        bool brakingWhileForward = movingForward && (throttle < -brakeSkidAmount);
        bool brakingWhileReverse = movingReverse && (throttle > brakeSkidAmount);
        bool isBraking = brakingWhileForward || brakingWhileReverse;

        bool isSteering = Mathf.Abs(steer) > steerSensitivity;

        float targetLeft = 0f;
        float targetRight = 0f;

        // 1) braking skids (allowed when braking relative to movement direction)
        if (isBraking)
        {
            targetLeft = Mathf.Max(targetLeft, 0.8f);
            targetRight = Mathf.Max(targetRight, 0.8f);
        }

        // 2) steering skids (ONLY when moving forward)
        if (movingForward)
        {
            if (speedKPH > steerSkidSpeed && isSteering)
            {
                float skidAmount = Mathf.InverseLerp(steerSkidSpeed, hardTurnSpeed, speedKPH);
                float steerImpact = skidAmount * Mathf.Abs(steer);

                targetLeft = Mathf.Max(targetLeft, steerImpact);
                targetRight = Mathf.Max(targetRight, steerImpact);
            }

            // 3) Hard turn skid -> opposite tire only (outside tire)
            if (speedKPH > hardTurnSpeed && isSteering)
            {
                if (steer > 0f)
                {
                    // steering right -> left (outside) wheel skids heavily
                    targetLeft = 1f;
                    targetRight = 0f;
                }
                else
                {
                    targetRight = 1f;
                    targetLeft = 0f;
                }
            }
        }
        else
        {
            // Not moving forward: explicitly prevent steering-based skids while reversing or stopped.
            // Only braking skid remains if isBraking is true (see above).
        }

        // Smoothly move visible alpha toward target
        leftAlpha = Mathf.MoveTowards(leftAlpha, targetLeft, Time.deltaTime * skidFadeSpeed);
        rightAlpha = Mathf.MoveTowards(rightAlpha, targetRight, Time.deltaTime * skidFadeSpeed);

        ApplyTrailAlpha(rearLeftTrail, leftAlpha);
        ApplyTrailAlpha(rearRightTrail, rightAlpha);

        if (showDebug)
        {
            Debug.Log($"fVel={forwardVel:F2}m/s | movingFwd={movingForward} movingRev={movingReverse} | throttle={throttle:F2} steer={steer:F2} | L:{leftAlpha:F2} R:{rightAlpha:F2}");
        }
    }

    // Ensure trail exists and is initialized to invisible
    void InitTrail(TrailRenderer t)
    {
        if (t == null) return;
        Color sc = t.startColor;
        Color ec = t.endColor;
        sc.a = 0f; ec.a = 0f;
        t.startColor = sc;
        t.endColor = ec;
    }

    void ApplyTrailAlpha(TrailRenderer t, float alpha)
    {
        if (t == null) return;
        Color sc = t.startColor;
        Color ec = t.endColor;
        sc.a = Mathf.Clamp01(alpha);
        ec.a = Mathf.Clamp01(alpha);
        t.startColor = sc;
        t.endColor = ec;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (car == null || car.rb == null) return;
        Vector3 pos = transform.position;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(pos + Vector3.up * 0.2f, 0.05f);

        if (showDebug)
        {
            float forwardVel = Vector3.Dot(car.rb.velocity, car.transform.forward);
            UnityEditor.Handles.Label(transform.position + Vector3.up * 0.5f,
                $"fVel={forwardVel:F2} m/s\nspeedKPH={car.currentSpeedKPH:F1}\nL:{leftAlpha:F2} R:{rightAlpha:F2}");
        }
    }
#endif
}
