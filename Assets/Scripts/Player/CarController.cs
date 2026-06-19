using UnityEngine;

public class CarController : MonoBehaviour
{
    public Rigidbody rb { get; private set; }

    [Header("Input")]
    public float throttleInput;
    public float steerInput;

    [Header("Engine Settings")]
    public float topSpeed = 120f;

    public AnimationCurve torqueCurve = new AnimationCurve(
        new Keyframe(0f, 0.5f, 0f, 1.5f),
        new Keyframe(0.35f, 1.0f, 1.5f, -1.5f),
        new Keyframe(1f, 0.0f, -1.5f, 0f)
    );

    [Header("Speed Readout")]
    public float currentSpeedKPH;

    [Header("Speed-Based Steering")]
    public float minSteerPercent = 0.2f;
    public float steerFadeSpeed = 30f;

    [Header("Brake Lights")]
    public Renderer brakeLightRenderer;
    public float brakeThreshold = -0.1f;

    private Material brakeMat;

    [Header("Reset Cooldown")]
    public float resetCooldown = 2f;
    private float lastResetTime = -999f;

    [Header("Focus System")]
    public bool enableFocus = true;

    [Tooltip("Tag used for focus targets")]
    public string focusTag = "Adamak";

    [Tooltip("Maximum distance the car can lock onto a target")]
    public float focusDistance = 10f;

    [Tooltip("Half-angle of the focus cone")]
    [Range(1f, 90f)]
    public float focusConeAngle = 45f;

    [Tooltip("How strongly the car steers toward the target")]
    public float focusSteerStrength = 3f;

    private Transform currentFocusTarget;

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


    float playerSteerInput = Input.GetAxis("Horizontal");

    steerInput = playerSteerInput;


    if (Input.GetKey(KeyCode.Space))
    {
        if (!enableFocus)
        {
            steerInput = 0f;
            currentFocusTarget = null;
        }
        else
        {
            currentFocusTarget = FindFocusTarget();


            if (currentFocusTarget != null)
            {
                Vector3 toTarget =
                    currentFocusTarget.position - transform.position;


                toTarget.y = 0f;


                float angle =
                    Vector3.SignedAngle(
                        transform.forward,
                        toTarget.normalized,
                        Vector3.up);



                steerInput =
                    Mathf.Clamp(
                        angle / focusConeAngle,
                        -1f,
                        1f)
                    * focusSteerStrength;
            }
            else
            {
                steerInput = 0f;
            }
        }
    }
    else
    {
        currentFocusTarget = null;
    }


    float mps = rb.velocity.magnitude;

    currentSpeedKPH =
        mps * 3.6f;


    UpdateBrakeLights();
}


    private Transform FindFocusTarget()
    {
        GameObject[] targets =
            GameObject.FindGameObjectsWithTag(focusTag);

        Transform bestTarget = null;

        float bestDistance = Mathf.Infinity;

        Vector3 origin = transform.position;
        Vector3 forward = transform.forward;


        foreach (GameObject obj in targets)
        {
            Vector3 toTarget =
                obj.transform.position - origin;

            float distance =
                toTarget.magnitude;

            if (distance > focusDistance)
                continue;

            float angle =
                Vector3.Angle(
                    forward,
                    toTarget);

            if (angle > focusConeAngle)
                continue;


            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestTarget = obj.transform;
            }
        }


        return bestTarget;
    }


    private void UpdateBrakeLights()
    {
        if (brakeMat == null)
            return;

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

        float t =
            Mathf.InverseLerp(
                steerFadeSpeed,
                topSpeed,
                s);

        float percent =
            Mathf.Lerp(
                1f,
                minSteerPercent,
                t);

        return percent;
    }

    public bool IsFocusing()
    {
        return Input.GetKey(KeyCode.Space)
            && enableFocus
            && currentFocusTarget != null;
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

        Vector3 uprightEuler =
            new Vector3(
                0f,
                transform.eulerAngles.y,
                2.6f);

        transform.rotation =
            Quaternion.Euler(uprightEuler);

        transform.position += Vector3.up * 1.6f;
    }


    private void OnDrawGizmosSelected()
    {
        if (!enableFocus)
            return;

        Vector3 origin = transform.position;
        Vector3 forward = transform.forward;

        Vector3 leftDir =
            Quaternion.Euler(
                0f,
                -focusConeAngle,
                0f)
            * forward;

        Vector3 rightDir =
            Quaternion.Euler(
                0f,
                focusConeAngle,
                0f)
            * forward;


        Gizmos.color = Color.yellow;


        Gizmos.DrawLine(
            origin,
            origin + leftDir * focusDistance);

        Gizmos.DrawLine(
            origin,
            origin + rightDir * focusDistance);


        int segments = 24;

        Vector3 previous =
            origin + leftDir * focusDistance;


        for (int i = 1; i <= segments; i++)
        {
            float t =
                i / (float)segments;

            float angle =
                Mathf.Lerp(
                    -focusConeAngle,
                    focusConeAngle,
                    t);

            Vector3 dir =
                Quaternion.Euler(
                    0f,
                    angle,
                    0f)
                * forward;


            Vector3 point =
                origin +
                dir *
                focusDistance;


            Gizmos.DrawLine(
                previous,
                point);

            previous = point;
        }


        if (currentFocusTarget != null)
        {
            Gizmos.color = Color.green;

            Gizmos.DrawLine(
                origin,
                currentFocusTarget.position);

            Gizmos.DrawSphere(
                currentFocusTarget.position,
                0.5f);
        }
    }
}