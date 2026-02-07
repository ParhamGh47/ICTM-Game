using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class AICarController : MonoBehaviour
{
    [Header("AI Targets (Waypoints)")]
    public Transform[] targets;
    private int currentTargetIndex = 0;

    [Header("Movement")]
    public float speedKPH = 60f;
    public float turnSpeed = 5f;
    public float reachThreshold = 2f;

    [Header("Wheels (visual only)")]
    public Transform[] wheels;
    public float suspensionDistance = 0.4f;
    public LayerMask groundMask;

    [Header("Physics Tuning")]
    public float forwardForceMultiplier = 500f;

    Rigidbody rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = false;
        rb.drag = 0.1f;
        rb.angularDrag = 2f;
        rb.mass = 2000f;

        rb.position += Vector3.up * 0.05f;
    }

    void FixedUpdate()
    {
        MoveTowardsTarget();
        UpdateWheelVisuals();
    }

    void MoveTowardsTarget()
    {
        if (targets == null || targets.Length == 0) return;

        Transform target = targets[currentTargetIndex];

        Vector3 toTarget = target.position - transform.position;
        toTarget.y = 0f;

        if (toTarget.magnitude < reachThreshold)
        {
            currentTargetIndex = (currentTargetIndex + 1) % targets.Length;
            target = targets[currentTargetIndex];
            toTarget = target.position - transform.position;
            toTarget.y = 0f;
        }

        Vector3 desiredDir = toTarget.normalized;

        Quaternion desiredRotation = Quaternion.LookRotation(desiredDir);
        rb.MoveRotation(Quaternion.Slerp(rb.rotation, desiredRotation, turnSpeed * Time.fixedDeltaTime));

        float speedMS = speedKPH / 3.6f;
        Vector3 velocityForward = transform.forward * speedMS;

        Vector3 velocityChange = velocityForward - new Vector3(rb.velocity.x, 0, rb.velocity.z);
        rb.AddForce(velocityChange * forwardForceMultiplier * Time.fixedDeltaTime, ForceMode.Acceleration);
    }

    void UpdateWheelVisuals()
    {
        if (wheels == null) return;

        foreach (var wheel in wheels)
        {
            if (Physics.Raycast(wheel.position + Vector3.up, Vector3.down, out RaycastHit hit, suspensionDistance * 2f, groundMask))
            {
                Vector3 pos = wheel.position;
                pos.y = hit.point.y + suspensionDistance;
                wheel.position = pos;
            }
        }
    }
}
