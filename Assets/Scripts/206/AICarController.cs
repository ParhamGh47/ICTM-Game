using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class AICarController : MonoBehaviour
{
    [Header("Waypoints")]
    public Transform waypointsRoot;
    private Transform[] waypoints;
    private int currentWaypoint;

    [Header("Movement")]
    public float speedKPH = 60f;
    public float turnSpeed = 6f;
    public float maxSteerAngle = 35f;
    public float reachThreshold = 2f;

    [Header("Wheels (visual only)")]
    public Transform[] wheels;
    public float suspensionDistance = 0.4f;
    public LayerMask groundMask;

    Rigidbody rb;
    float speedMS;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();

        rb.isKinematic = false;
        rb.mass = 2000f;
        rb.drag = 0.5f;
        rb.angularDrag = 5f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        rb.position += Vector3.up * 0.05f;

        speedMS = speedKPH / 3.6f;
        CacheWaypoints();
    }

    void CacheWaypoints()
    {
        if (!waypointsRoot) return;

        int count = waypointsRoot.childCount;
        waypoints = new Transform[count];

        for (int i = 0; i < count; i++)
            waypoints[i] = waypointsRoot.GetChild(i);
    }

    void FixedUpdate()
    {
        MoveCar();
        UpdateWheelVisuals();
    }

    void MoveCar()
    {
        if (waypoints == null || waypoints.Length == 0) return;

        Transform target = waypoints[currentWaypoint];

        Vector3 toTarget = target.position - transform.position;
        toTarget.y = 0f;

        if (toTarget.magnitude < reachThreshold)
        {
            currentWaypoint = (currentWaypoint + 1) % waypoints.Length;
            return;
        }

        Vector3 desiredDir = toTarget.normalized;

        float angle = Vector3.SignedAngle(transform.forward, desiredDir, Vector3.up);
        angle = Mathf.Clamp(angle, -maxSteerAngle, maxSteerAngle);

        Quaternion steerRot = Quaternion.AngleAxis(angle, Vector3.up);
        Quaternion targetRot = rb.rotation * steerRot;

        rb.MoveRotation(
            Quaternion.Slerp(rb.rotation, targetRot, turnSpeed * Time.fixedDeltaTime)
        );

        Vector3 move = transform.forward * speedMS * Time.fixedDeltaTime;
        rb.MovePosition(rb.position + move);
    }

    void UpdateWheelVisuals()
    {
        if (wheels == null) return;

        foreach (var wheel in wheels)
        {
            if (Physics.Raycast(
                wheel.position + Vector3.up,
                Vector3.down,
                out RaycastHit hit,
                suspensionDistance * 2f,
                groundMask))
            {
                Vector3 pos = wheel.position;
                pos.y = hit.point.y + suspensionDistance;
                wheel.position = pos;
            }
        }
    }
}
