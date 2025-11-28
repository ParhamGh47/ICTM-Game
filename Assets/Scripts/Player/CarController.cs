using UnityEngine;

public class CarController : MonoBehaviour
{
    private Rigidbody rb;

    [Header("Input")]
    public float throttleInput;
    public float steerInput;

    [Header("Engine Settings")]
    public float topSpeed = 50f;

    public AnimationCurve torqueCurve = new AnimationCurve(
        new Keyframe(0f,    0.5f, 0f, 1.5f),
        new Keyframe(0.35f, 1.0f, 1.5f, -1.5f),
        new Keyframe(1f,    0.0f, -1.5f, 0f)
    );

    [Header("Speed Readout")]
    public float currentSpeedKPH;

    [Header("Speed-Based Steering")]
    public float minSteerPercent = 0.2f;
    public float steerFadeSpeed = 40f;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    void Update()
    {
        throttleInput = Input.GetAxis("Vertical");
        steerInput = Input.GetAxis("Horizontal");

        float mps = rb.velocity.magnitude;
        currentSpeedKPH = mps * 3.6f;
    }

    public float GetSpeedAdjustedSteer()
    {
        float s = currentSpeedKPH;

        if (s <= steerFadeSpeed)
            return 1f;

        float t = Mathf.InverseLerp(steerFadeSpeed, topSpeed, s);
        float percent = Mathf.Lerp(1f, minSteerPercent, t);

        return percent;
    }

    private void FixedUpdate()
    {
        if (Input.GetKeyDown(KeyCode.R))
            ResetCar();
    }

    private void ResetCar()
    {
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        Vector3 uprightEuler = new Vector3(0f, transform.eulerAngles.y, 0f);
        transform.rotation = Quaternion.Euler(uprightEuler);

        transform.position += Vector3.up * 0.75f;
    }
}
