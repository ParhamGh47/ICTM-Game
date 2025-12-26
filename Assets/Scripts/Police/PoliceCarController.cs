using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PoliceCarController : MonoBehaviour
{
    [Header("Target")]
    public Transform player;
    public Rigidbody playerRb;

    [Header("Speed")]
    public float baseSpeed = 38f; // m/s (~135 KPH)
    public float maxSpeed = 48f; // Catch-up speed
    public float acceleration = 9000f;
    public float deceleration = 12000f;

    [Header("Turning")]
    public float turnRate = 3.5f;
    public float minTurnSpeed = 1.5f;
    public float turnDampening = 0.015f;

    [Header("Follow Behavior")]
    public float followDistance = 7f;
    public float maxSideOffset = 1.2f; // Renamed for clarity
    public float predictionTime = 0.6f;

    [Header("Stability")]
    public float lateralGrip = 7f;
    public float downForce = 9000f;

    [Header("Ramming")]
    public float ramDistance = 3.5f;
    public float ramForce = 11000f;
    public float ramCooldown = 2.5f;

    private Rigidbody rb;
    private float lastRamTime;

    // Persistent side choice to prevent jitter
    private int currentSide = 1; // 1 = right, -1 = left
    private float lastSideChangeTime;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.mass = 1400f;
        rb.drag = 0.08f;
        rb.angularDrag = 6f;
        rb.centerOfMass = new Vector3(0f, -0.45f, 0f);
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        // Initial random side
        currentSide = Random.value > 0.5f ? 1 : -1;
        lastSideChangeTime = Time.time;
    }

    void FixedUpdate()
    {
        if (!player) return;

        Vector3 playerVelocity = playerRb ? playerRb.velocity : Vector3.zero;

        // ---------- TARGET POSITION ----------
        Vector3 predictedPos = player.position + playerVelocity * predictionTime;
        Vector3 behindPlayer = -player.forward * followDistance;

        // Dynamic side offset - only when reasonably close
        float distToPlayer = Vector3.Distance(transform.position, player.position);
        float offsetFactor = Mathf.Clamp01(1f - (distToPlayer / (followDistance * 2f))); // Fade out when far

        // Occasionally switch sides for natural intimidation (every 4-8 seconds when close)
        if (offsetFactor > 0.5f && Time.time - lastSideChangeTime > Random.Range(4f, 8f))
        {
            currentSide *= -1; // Switch side
            lastSideChangeTime = Time.time;
        }

        Vector3 sideOffset = player.right * (maxSideOffset * offsetFactor * currentSide);

        Vector3 targetPos = predictedPos + behindPlayer + sideOffset;
        Vector3 toTarget = targetPos - transform.position;
        toTarget.y = 0f;
        float distance = toTarget.magnitude;
        if (distance < 0.1f) return;

        Vector3 desiredDir = toTarget.normalized;

        // ---------- TURNING ----------
        float forwardSpeed = Vector3.Dot(rb.velocity, transform.forward);
        float speedDamp = 1f / (1f + forwardSpeed * turnDampening);
        float turnFactor = Mathf.Lerp(minTurnSpeed, 1f, Mathf.Clamp01(forwardSpeed / 10f)) * speedDamp;

        Quaternion targetRot = Quaternion.LookRotation(desiredDir, Vector3.up);
        rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRot, turnRate * turnFactor * Time.fixedDeltaTime));

        // ---------- SPEED CONTROL ----------
        float targetSpeed = baseSpeed;
        if (distance > followDistance * 1.6f)
            targetSpeed = maxSpeed;
        else if (distance < followDistance * 0.8f)
            targetSpeed = baseSpeed * 0.75f;

        if (forwardSpeed < targetSpeed)
        {
            rb.AddForce(transform.forward * acceleration * Time.fixedDeltaTime, ForceMode.Acceleration);
        }
        else if (forwardSpeed > targetSpeed + 1f)
        {
            rb.AddForce(-transform.forward * deceleration * Time.fixedDeltaTime, ForceMode.Acceleration);
        }

        // ---------- LATERAL GRIP ----------
        Vector3 localVel = transform.InverseTransformDirection(rb.velocity);
        localVel.x *= Mathf.Clamp01(1f - lateralGrip * Time.fixedDeltaTime);
        rb.velocity = transform.TransformDirection(localVel);

        // ---------- DOWNFORCE ----------
        rb.AddForce(Vector3.down * downForce, ForceMode.Force);

        // ---------- RAM ----------
        float timeSinceRam = Time.time - lastRamTime;
        if (distance < ramDistance && timeSinceRam > ramCooldown)
        {
            if (playerRb)
            {
                Vector3 ramDir = (player.position - transform.position).normalized;
                playerRb.AddForce(ramDir * ramForce, ForceMode.Impulse);
            }
            lastRamTime = Time.time;
            // Optional: switch side after ram for realism
            currentSide *= -1;
            lastSideChangeTime = Time.time;
        }
    }
}