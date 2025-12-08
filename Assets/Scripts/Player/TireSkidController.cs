using UnityEngine;

public class TireSkidController : MonoBehaviour
{
    [Header("References")]
    public CarController car;
    public TrailRenderer rearLeftTrail;
    public TrailRenderer rearRightTrail;

    [Header("Speed Thresholds")]
    public float steerSkidSpeed = 50f;
    public float hardTurnSpeed = 100f;

    [Header("Braking")]
    public float brakeSkidAmount = 0.1f;

    [Header("Steering")]
    public float steerSensitivity = 0.7f;

    [Header("Smoothing")]
    public float skidFadeSpeed = 8f;

    private float leftAlpha = 0f;
    private float rightAlpha = 0f;

    void Start()
    {
        SetTrailIntensity(rearLeftTrail, 0f);
        SetTrailIntensity(rearRightTrail, 0f);
    }

    void Update()
    {
        float speed = car.currentSpeedKPH;
        float steer = car.steerInput;
        float throttle = car.throttleInput;

        bool isBraking = throttle < -brakeSkidAmount;
        bool isSteering = Mathf.Abs(steer) > steerSensitivity;

        float targetLeft = 0f;
        float targetRight = 0f;

        if (isBraking)
        {
            targetLeft = Mathf.Max(targetLeft, 0.8f);
            targetRight = Mathf.Max(targetRight, 0.8f);
        }

        if (speed > steerSkidSpeed && isSteering)
        {
            float skidAmount = Mathf.InverseLerp(steerSkidSpeed, hardTurnSpeed, speed);

            targetLeft = Mathf.Max(targetLeft, skidAmount * Mathf.Abs(steer));
            targetRight = Mathf.Max(targetRight, skidAmount * Mathf.Abs(steer));
        }

        if (speed > hardTurnSpeed && isSteering)
        {
            if (steer > 0f)
            {
                targetLeft = 1f;
                targetRight = 0f;
            }
            else
            {
                targetRight = 1f;
                targetLeft = 0f;
            }
        }

        leftAlpha = Mathf.MoveTowards(leftAlpha, targetLeft, Time.deltaTime * skidFadeSpeed);
        rightAlpha = Mathf.MoveTowards(rightAlpha, targetRight, Time.deltaTime * skidFadeSpeed);

        SetTrailIntensity(rearLeftTrail, leftAlpha);
        SetTrailIntensity(rearRightTrail, rightAlpha);
    }

    private void SetTrailIntensity(TrailRenderer t, float amount)
    {
        if (t == null) return;

        Color c = t.startColor;
        c.a = amount;
        t.startColor = c;

        c = t.endColor;
        c.a = amount;
        t.endColor = c;
    }
}
