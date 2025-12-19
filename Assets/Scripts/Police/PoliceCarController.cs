using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PoliceCarController : MonoBehaviour
{
    public Rigidbody rb { get; private set; }

    [Header("Target")]
    public Transform player;

    [Header("Speed")]
    public float maxSpeedKPH = 150f;
    public float accelInput = 1f;

    [Header("Steering")]
    public float maxSteerAngle = 32f;
    public float steerResponsiveness = 2.8f;
    public float steerSpeedDamping = 0.02f;
    public float yawTorque = 20000f;

    [HideInInspector] public float throttleInput;
    [HideInInspector] public float steerAngle;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.mass = 1400f;
        rb.drag = 0.05f;
        rb.angularDrag = 8f;
        rb.centerOfMass = new Vector3(0f, -0.35f, 0f);
    }

    void FixedUpdate()
    {
        if (!player) return;

        Vector3 toPlayer = player.position - transform.position;
        toPlayer.y = 0f;

        if (toPlayer.sqrMagnitude < 1f)
            return;

        float speed = rb.velocity.magnitude;
        float speedKPH = speed * 3.6f;

        // ---------- Steering ----------
        float angle = Vector3.SignedAngle(transform.forward, toPlayer.normalized, Vector3.up);

        float speedDamp = 1f / (1f + speed * steerSpeedDamping);
        steerAngle = Mathf.Clamp(
            angle * steerResponsiveness * speedDamp,
            -maxSteerAngle,
            maxSteerAngle
        );

        rb.AddRelativeTorque(Vector3.up * angle * yawTorque * Time.fixedDeltaTime);

        // ---------- Throttle ----------
        throttleInput = accelInput;

        if (speedKPH > maxSpeedKPH)
            throttleInput = 0f;
    }
}
