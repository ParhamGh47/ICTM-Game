using UnityEngine;

public class TireSkidController : MonoBehaviour
{
    public CarController car;
    public TrailRenderer rearLeftTrail;
    public TrailRenderer rearRightTrail;

    [Header("Speed Thresholds (km/h)")]
    public float steerSkidSpeed = 50f;
    public float hardTurnSpeed = 100f;

    [Header("Braking")]
    public float brakeSkidAmount = 0.1f;

    [Header("Steering")]
    public float steerSensitivity = 0.7f;

    [Header("Smoothing")]
    public float skidFadeSpeed = 8f;

    [Header("Ground Check")]
    public float groundCheckDistance = 0.3f;

    [Header("Trail Length")]
    public float maxTrailTime = 0.15f;

    float leftAlpha, rightAlpha;

    /// <summary>
    /// How hard the left rear tyre is sliding, 0 - 1: the same value the mark is drawn at. The tyre smoke
    /// reads these so it always comes off a tyre that is actually leaving a mark, rather than working the
    /// slide out for itself and slowly drifting away from the marks.
    /// </summary>
    public float LeftSkid { get { return leftAlpha; } }

    /// <summary>How hard the right rear tyre is sliding, 0 - 1.</summary>
    public float RightSkid { get { return rightAlpha; } }

    /// <summary>The two tyres' contact patches: where marks and smoke both belong.</summary>
    public Transform LeftContact { get { return rearLeftTrail != null ? rearLeftTrail.transform : null; } }
    public Transform RightContact { get { return rearRightTrail != null ? rearRightTrail.transform : null; } }

    void Start()
    {
        InitTrail(rearLeftTrail);
        InitTrail(rearRightTrail);
    }

    void Update()
    {
        if (car == null || car.rb == null)
            return;

        bool grounded =
            IsGrounded(rearLeftTrail.transform) &&
            IsGrounded(rearRightTrail.transform);

        if (!grounded)
        {
            FadeOut();
            return;
        }

        float steer = car.steerInput;
        float throttle = car.throttleInput;
        float speed = car.currentSpeedKPH;

        float forwardVel = Vector3.Dot(car.rb.velocity, car.transform.forward);
        bool movingForward = forwardVel > 0.5f;

        bool braking =
            (movingForward && throttle < -brakeSkidAmount) ||
            (!movingForward && throttle > brakeSkidAmount);

        bool steering = Mathf.Abs(steer) > steerSensitivity;

        float targetL = 0f;
        float targetR = 0f;

        if (braking)
        {
            targetL = targetR = 0.8f;
        }

        if (movingForward && speed > steerSkidSpeed && steering)
        {
            float t = Mathf.InverseLerp(steerSkidSpeed, hardTurnSpeed, speed);
            float skid = t * Mathf.Abs(steer);

            targetL = Mathf.Max(targetL, skid);
            targetR = Mathf.Max(targetR, skid);

            if (speed > hardTurnSpeed)
            {
                if (steer > 0f) { targetL = 1f; targetR = 0f; }
                else { targetR = 1f; targetL = 0f; }
            }
        }

        leftAlpha = Mathf.MoveTowards(leftAlpha, targetL, Time.deltaTime * skidFadeSpeed);
        rightAlpha = Mathf.MoveTowards(rightAlpha, targetR, Time.deltaTime * skidFadeSpeed);

        ApplyTrail(rearLeftTrail, leftAlpha);
        ApplyTrail(rearRightTrail, rightAlpha);
    }

    void FadeOut()
    {
        leftAlpha = Mathf.MoveTowards(leftAlpha, 0f, Time.deltaTime * skidFadeSpeed);
        rightAlpha = Mathf.MoveTowards(rightAlpha, 0f, Time.deltaTime * skidFadeSpeed);

        ApplyTrail(rearLeftTrail, leftAlpha);
        ApplyTrail(rearRightTrail, rightAlpha);
    }

    bool IsGrounded(Transform t)
    {
        return Physics.Raycast(t.position, Vector3.down, groundCheckDistance);
    }

    void InitTrail(TrailRenderer t)
    {
        if (!t) return;
        t.time = 0f;
        ApplyTrail(t, 0f);
    }

    void ApplyTrail(TrailRenderer t, float alpha)
    {
        if (!t) return;

        t.time = Mathf.Lerp(0f, maxTrailTime, alpha);

        Color s = t.startColor;
        Color e = t.endColor;
        s.a = e.a = alpha;
        t.startColor = s;
        t.endColor = e;
    }
}
