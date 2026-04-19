using UnityEngine;

public class CarController : MonoBehaviour
{
    public Rigidbody rb { get; private set; }

    [Header("Input")]
    public float throttleInput;
    public float steerInput;

    [Header("Engine Settings")]
    public float topSpeed = 80f;

    public AnimationCurve torqueCurve = new AnimationCurve(
        new Keyframe(0f,    0.5f, 0f, 1.5f),
        new Keyframe(0.35f, 1.0f, 1.5f, -1.5f),
        new Keyframe(1f, 0.0f, -1.5f, 0f)
    );

    [Header("Speed Readout")]
    public float currentSpeedKPH;

    [Header("Speed-Based Steering")]
    public float minSteerPercent = 0.2f;
    public float steerFadeSpeed = 40f;

    [Header("Brake Lights")]
    public Renderer brakeLightRenderer;
    public float brakeThreshold = -0.1f;

    private Material brakeMat;

    [Header("Reset Cooldown")]
    public float resetCooldown = 2f;
    private float lastResetTime = -999f;

    public bool isShiftingUp = false;
    public bool isShiftingDown = false;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.drag = 0.05f;
        rb.angularDrag = 5f;

        if (brakeLightRenderer != null)
            brakeMat = brakeLightRenderer.material;
    }

    void Update()
    {
        throttleInput = Input.GetAxis("Vertical");
        steerInput = Input.GetAxis("Horizontal");

        float mps = rb.velocity.magnitude;
        currentSpeedKPH = mps * 3.6f;

        UpdateBrakeLights();
    }

    private void UpdateBrakeLights()
    {
        if (throttleInput < brakeThreshold)
        {
            brakeMat.EnableKeyword("_EMISSION");
        }
        else
        {
            brakeMat.DisableKeyword("_EMISSION");
        }
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
        {
            if (Time.time - lastResetTime >= resetCooldown)
            {
                ResetCar();
                lastResetTime = Time.time;
            }
        }
    }

    private void ResetCar()
    {
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        Vector3 uprightEuler = new Vector3(0f, transform.eulerAngles.y, 2.6f);
        transform.rotation = Quaternion.Euler(uprightEuler);

        transform.position += Vector3.up * 1.6f;
    }
}
