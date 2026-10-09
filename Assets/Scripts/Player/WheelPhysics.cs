using UnityEngine;

public class WheelPhysics : MonoBehaviour
{
    Rigidbody carRb;
    CarController car;

    [Tooltip("Visual Front Wheels")]
    public Transform frontWheelsParent;

    [Header("Suspension")]
    public float restLength = 0.25f;
    public float springTravel = 0.3f;
    public float springStiffness = 80000f;
    public float damperStiffness = 15000f;

    float minLen, maxLen, lastLen, springLen;

    [Header("Wheel Setup")]
    public float wheelRadius = 0.06f;
    public bool isFrontLeft;
    public bool isFrontRight;
    public bool isRearLeft;
    public bool isRearRight;

    [Header("Steering")]
    public float maxSteerAngle = 25f;

    private Quaternion frontWheelsBaseRotation;

    [Header("Grip & Handling")]
    public float lateralGrip = 0.8f;
    public float forwardGrip = 1.0f;
    public float tireMass = 40f;

    [Header("Surface Friction")]
    public float roadFriction = 1.0f;
    public float offRoadFriction = 0.3f;
    private float currentSurfaceFriction = 1.0f;

    [Header("Engine / Brakes")]
    public float engineForce = 5000f;
    public float brakeForce = 8000f;
    public float reverseForce = 1500f;

    [Header("Engine Behavior")]
    public float accelerationRate = 5f;
    public float decelerationRate = 12f;
    private float engineResponse = 0f;

    [Header("Idle Hold")]
    [Tooltip("How fast the truck may still be rolling for the idle hold to apply, in metres per second. Below " +
             "this with no throttle at all the truck is settled into the spot it is standing on, which is what " +
             "keeps the start of a level on a slope from creeping away; above it the truck coasts exactly as it " +
             "always has, so driving is untouched.")]
    public float idleHoldSpeed = 1.5f;

    [Tooltip("How hard the hold pulls against what is left of the rolling, in newtons per metre per second of " +
             "it, per wheel. Rolling resistance alone cannot park a truck: it grows with speed while a grade " +
             "pushes with a force that does not, so the two meet at a speed the truck then creeps at - nearly " +
             "6 km/h backwards on a 10% grade. This is the stiffness that out-pulls the grade instead.")]
    public float idleHoldStiffness = 15000f;

    [Tooltip("The most the hold may pull with, in newtons per wheel. It is here so that the hold settles a " +
             "truck rather than stopping it like a wall: coming into it at the whole hold speed is about " +
             "5 m/s² - half of what the brakes do - which settles the truck in a third of a second over " +
             "23 cm. Even so it out-holds a 40% grade.")]
    public float idleHoldMax = 2000f;

    [Header("Drifting")]
    public float driftThreshold = 7.0f;
    public float driftMultiplier = 1.2f;
    public float driftRecovery = 5f;

    void Start()
    {
        carRb = GetComponentInParent<Rigidbody>();
        car = GetComponentInParent<CarController>();

        minLen = restLength - springTravel;
        maxLen = restLength + springTravel;
        springLen = restLength;

        if (frontWheelsParent != null)
            frontWheelsBaseRotation = frontWheelsParent.localRotation;
    }

    void Update()
    {
        // The wheels are what steer the truck, so how far they are allowed to turn is what makes a
        // fast truck feel planted and a slow one nimble. The car owns that curve (it holds the speed
        // based steering settings), and it is applied to whatever the input was - keyboard, stick or
        // trigger - so the handling is the same on every device.
        float steerAngle = car != null
            ? car.steerInput * maxSteerAngle * car.GetSpeedAdjustedSteer()
            : 0f;

        if (isFrontLeft || isFrontRight)
        {
            transform.localRotation = Quaternion.Euler(0f, steerAngle, 0f);
        }

        if (frontWheelsParent != null && (isFrontLeft || isFrontRight))
        {
            frontWheelsParent.localRotation = frontWheelsBaseRotation * Quaternion.Euler(0f, steerAngle * 0.8f, 0f);
        }
    }

    void FixedUpdate()
    {
        if (!Physics.Raycast(transform.position, -transform.up, out RaycastHit hit, maxLen + wheelRadius))
            return;

        if (hit.collider.CompareTag("Road"))
        {
            currentSurfaceFriction = roadFriction;
        }
        else if (hit.collider.CompareTag("!Road"))
        {
            currentSurfaceFriction = offRoadFriction;
        }
        else
        {
            currentSurfaceFriction = roadFriction;
        }

        Vector3 springDir = transform.up;
        Vector3 tireVel = carRb.GetPointVelocity(transform.position);

        // Suspension
        lastLen = springLen;
        float rawLen = hit.distance - wheelRadius;
        springLen = Mathf.Clamp(rawLen, minLen, maxLen);
        float springVel = (springLen - lastLen) / Time.fixedDeltaTime;

        float springForce = springStiffness * (restLength - springLen);
        float damperForce = -damperStiffness * springVel;
        float suspensionForce = springForce + damperForce;

        carRb.AddForceAtPosition(springDir * suspensionForce, transform.position);

        // Drifting
        Vector3 lateralDir = transform.right;
        float lateralVel = Vector3.Dot(lateralDir, tireVel);
        float speed = carRb.velocity.magnitude;

        float speedGripFactor = Mathf.Lerp(1f, 0.35f, speed / car.topSpeed);
        float slip = Mathf.Abs(lateralVel);

        float finalGrip;
        if (slip < driftThreshold)
        {
            finalGrip = lateralGrip * speedGripFactor * roadFriction;
        }
        else
        {
            float t = (slip - driftThreshold) / driftThreshold;
            finalGrip = Mathf.Lerp(lateralGrip * speedGripFactor * currentSurfaceFriction, lateralGrip * driftMultiplier * currentSurfaceFriction, t);
        }

        float desiredLatVelChange = -lateralVel * finalGrip;
        float desiredLatAccel = desiredLatVelChange / Time.fixedDeltaTime;
        carRb.AddForceAtPosition(lateralDir * tireMass * desiredLatAccel, transform.position);

        if (slip > driftThreshold)
        {
            Vector3 stabilizingForce = -lateralDir * slip * driftRecovery * roadFriction;
            carRb.AddForceAtPosition(stabilizingForce, transform.position);
        }

        // Engine / Braking / Reverse
        Vector3 forwardDir = transform.forward;
        float forwardVel = Vector3.Dot(forwardDir, tireVel);
        float throttle = car.throttleInput;

        if (throttle > 0f)
            engineResponse = Mathf.MoveTowards(engineResponse, throttle, accelerationRate * Time.fixedDeltaTime);    
        else if (throttle == 0f)
            engineResponse = Mathf.MoveTowards(engineResponse, 0f, decelerationRate * Time.fixedDeltaTime);

        float normalizedSpeed = Mathf.Clamp01(Mathf.Abs(forwardVel) / car.topSpeed);
        float torqueMultiplier = car.torqueCurve.Evaluate(normalizedSpeed);

        // What the gearbox is worth. Exactly 1 while the box shifts for itself, so a truck driven
        // automatically has the drive it has always had; in manual it is the gear's own ratio, the revs and
        // the redline, worked out by the engine (see EngineAudio.UpdateDrivePower).
        float power = car.GearPowerScale;

        // Whether the reverse gear - which only manual has - is the one engaged, and how much of its own short
        // range is left. Reverse borrows the first gear's ratio, so without a speed of its own it would simply
        // keep pulling to the truck's top speed, backwards.
        bool reverseGear = car.ReverseGearEngaged;
        float reverseEnvelope = torqueMultiplier;

        if (reverseGear)
        {
            float reverseNormalised = Mathf.Clamp01(Mathf.Abs(forwardVel) / car.ReverseTopSpeedMS);
            reverseEnvelope = car.torqueCurve.Evaluate(reverseNormalised);
        }

        if (throttle > 0.01f)
        {
            if (reverseGear)
            {
                // The gas drives the way the lever is set: in reverse, backwards, at the reverse gear's own
                // strength and speed - the same force the pedal used to reverse the truck automatically, since
                // reverse is the same gear either way.
                float reverseTorque = reverseForce * engineResponse * forwardGrip * reverseEnvelope * power * currentSurfaceFriction;
                carRb.AddForceAtPosition(-forwardDir * reverseTorque, transform.position);
            }
            else if (car.ClutchCut)
            {
                // A change the player is making: the truck is out of gear for it, so nothing drives the wheels
                // - and, unlike an automatic change, nothing brakes them either. The truck coasts through the
                // change at the speed it had, which is what a clutch does.
            }
            else if (car.isShiftingUp)
            {
                float brake = brakeForce * throttle * 0.65f * currentSurfaceFriction;
                carRb.AddForceAtPosition(-forwardDir * brake, transform.position);
            }  
            else
            {
                float torque = engineForce * engineResponse * forwardGrip * torqueMultiplier * power * currentSurfaceFriction;
                carRb.AddForceAtPosition(forwardDir * torque, transform.position);
            }
        }

        if (throttle < -0.01f && forwardVel > 0.5f)
        {
            if (car.isShiftingDown)
            {
                float torque = engineForce * engineResponse * forwardGrip * torqueMultiplier * power * currentSurfaceFriction;
                carRb.AddForceAtPosition(forwardDir * torque * 1f, transform.position);
            } 
            else
            {
                float brake = brakeForce * -throttle * currentSurfaceFriction;
                carRb.AddForceAtPosition(-forwardDir * brake, transform.position);
            }      
        }

        // Braking while the truck is rolling backwards: the brake stops it whichever way it is going, and in
        // manual that is the pedal's whole job - in reverse, and in a forward gear sliding back down a slope
        // alike. Automatic never reaches this: there the pedal is what drives the truck backwards, so holding
        // it is how reverse is done rather than something to be corrected.
        if (car.ManualGearbox && throttle < -0.01f && forwardVel < -0.5f)
        {
            float brake = brakeForce * -throttle * currentSurfaceFriction;
            carRb.AddForceAtPosition(forwardDir * brake, transform.position);
        }

        // Brake-then-reverse belongs to the automatic box, which has no lever to put into reverse. In manual
        // the pedal stops at stopping: the way backwards is the reverse gear, and the gas.
        if (throttle < -0.01f && forwardVel <= 0.5f && !car.ManualGearbox)
        {
            float reverse = reverseForce * -throttle * currentSurfaceFriction;
            carRb.AddForceAtPosition(-forwardDir * reverse, transform.position);
        }

        if (Mathf.Abs(throttle) < 0.01f)
        {
            if (car.isShiftingDown)
            {
                float torque = engineForce * engineResponse * forwardGrip * torqueMultiplier * power * currentSurfaceFriction;
                carRb.AddForceAtPosition(forwardDir * 1f, transform.position);
            }
            else if (car.ClutchCut)
            {
                // Out of gear, so the engine is not holding the truck back either: the wheels free-wheel
                // through the change rather than dragging the drivetrain along.
            }
            else
            {
                Vector3 rolling = -forwardDir * forwardVel * 300f * currentSurfaceFriction;
                carRb.AddForceAtPosition(rolling, transform.position);
            }

            // With no input at all, the truck is also held where it is standing - which is the one case the
            // rolling resistance above cannot do on its own. See ApplyIdleHold.
            ApplyIdleHold(forwardDir, forwardVel);
        }

        Vector3 drag = -forwardDir * forwardVel * Mathf.Abs(forwardVel) * 1.2f * currentSurfaceFriction;
        carRb.AddForceAtPosition(drag, transform.position);
    }


    /// <summary>
    /// Holds a truck that is not being driven where it is standing.
    ///
    /// This is the answer to a level that starts on a slope, which is where it showed: the truck is left
    /// alone on a grade and slides back down it. The reason is not a lack of friction but the shape of it.
    /// Rolling resistance is a force that grows with speed - 300 N per m/s here - while a grade pushes with a
    /// constant one, <c>m g sin(theta)</c>, so the two do not meet at a standstill, they meet at a speed: the
    /// truck settles into creeping backwards down the slope and stays there (nearly 6 km/h on a 10% grade,
    /// and on a 40% one nothing stops it at all).
    ///
    /// The hold is a stiffness that exists only in the last <see cref="idleHoldSpeed"/> of motion, and only
    /// with no throttle, no brake and no driving force of any kind - the caller reaches here only when the
    /// pedal is untouched. Above that speed it does not exist, so coasting and driving are exactly what they
    /// were; below it, the stiffness is what the grade is up against instead of the rolling resistance, and
    /// the truck stops. <see cref="idleHoldMax"/> caps it so it settles the truck rather than stopping it like
    /// a wall.
    ///
    /// It is deliberately not scaled by the surface: a truck parked on a verge should stay parked for the
    /// same reason one parked on tarmac does, and the strength of it is a feel decision rather than a friction
    /// one. It is per wheel, like every other force here, and is only applied where the wheel has found
    /// ground - the caller returns early when it has not, so a truck in the air is left to gravity. A boost
    /// stands it down, because a boost is the driver asking to be moved.
    /// </summary>
    private void ApplyIdleHold(Vector3 forwardDir, float forwardVel)
    {
        // A boost is the driver asking for the opposite of this, and it is the one thing that can be asked for
        // with the pedal untouched from a standstill. It out-pulls the hold anyway, being three times as hard,
        // but standing down for it keeps the two from being felt as one through the first third of a second.
        if (car != null && car.IsBoosting)
            return;

        if (Mathf.Abs(forwardVel) >= idleHoldSpeed)
            return;

        float hold =
            Mathf.Clamp(-forwardVel * idleHoldStiffness, -idleHoldMax, idleHoldMax);

        carRb.AddForceAtPosition(forwardDir * hold, transform.position);
    }
}
