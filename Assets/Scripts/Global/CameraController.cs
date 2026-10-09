using UnityEngine;
using System.Collections;
using Cinemachine;

public class CameraController : MonoBehaviour
{

    public CinemachineBrain cmb;
    public GameObject[] Cameras;
    // 0 = Dynamic (Low Speed & Reverse) - MODE 1
    // 1 = Main - MODE 1
    // 2 = Boost - MODE 1
    // 3 = Dynamic - MODE 2
    // 4 = Above - MODE 3
    // 5 = Boost - Mode 3

    public GameObject[] mode2Pointers;

    public int mode;

    public bool isTransitioning = false;

    public CarController car;
    public ReverseBeep reverseSound;

    private int currentCam = 1;
    private bool boostActive = false;

    [Header("Reverse")]
    [Tooltip("How fast the truck must be rolling backwards, in km/h, before the view comes round on its own - " +
             "whatever put it there: the brake pedal, a shove from another car, or a slope. Below it a truck " +
             "drifting back a little is left alone rather than swinging the camera.")]
    public float reverseRollingSpeed = 2.5f;

    [Tooltip("The fastest the truck may still be rolling forward, in km/h, when reverse is asked for, before " +
             "the view comes round. This is what stops a hard stop at speed from ducking behind the truck while " +
             "it is still running forward - the view waits for the truck to stop making headway.")]
    public float reverseEnterSpeed = 4f;

    [Tooltip("Once the view is behind the truck it stays there until the truck is running forward faster " +
             "than this, in km/h, or reverse is let go. Higher than the entry speed on purpose: the gap " +
             "between the two is what keeps a truck sitting on the line from flapping the view back and forth.")]
    public float reverseExitSpeed = 10f;

    public bool isGoingReverse = false;

    // ---------------------------------------------------------------- the view the player chose

    // The camera the player last drove from, kept across levels and restarts. Nothing else in the game reads
    // it, so it lives here rather than in a settings class of its own.
    private const string ModePrefKey = "Camera.Mode";

    // ---------------------------------------------------------------- braking

    [Header("Brake Camera")]
    [Tooltip("How far in towards the truck the camera comes with the brakes fully on, from the top of the " +
             "speed range, in metres. The rig already steps to its low-speed camera on a hard stop; this is the " +
             "pull on top of that, so the stop is something the camera shows rather than only something the " +
             "truck does. It is deliberately small: this is a lean towards the truck, not a move. Applies to " +
             "the default third-person view only - the overhead and near views cannot pull in without giving " +
             "away the framing they exist for.")]
    public float brakeDollyIn = 0.85f;

    [Tooltip("How close to the truck the camera may be pulled, in metres. The rig's low-speed camera already " +
             "sits nearer than the one it shows at speed, so this is what stops a hard stop from putting the " +
             "view inside the truck.")]
    public float brakeDollyMinDistance = 1.2f;

    [Tooltip("How fast the camera closes on the truck once the brakes go on, in metres per second of camera " +
             "distance. A whole pull is about a second of this, and it is eased at both ends on top, so the " +
             "closing takes longer than the number suggests.")]
    public float brakeDollyInSpeed = 1.4f;

    [Tooltip("How fast the camera lets the truck back out once the brakes come off, in metres per second. " +
             "Slower than closing, which is what makes it read as the truck pulling away from you.")]
    public float brakeDollyOutSpeed = 0.9f;

    [Tooltip("The speed in km/h below which braking does not move the camera at all. A full stop from a " +
             "crawl has no drama to show, and this is where the pull starts from nothing. Low on purpose: the " +
             "speed the stop was made from is what the pull is really about, so as little of the range as " +
             "possible is left out of it.")]
    public float brakeDollySpeedFloor = 15f;

    [Tooltip("The speed in km/h at which braking from it has its full pull on the camera. Set at the top of " +
             "the truck's own speed range, so that the whole pull takes a stop from flat out.")]
    public float brakeDollyFullSpeed = 105f;

    [Tooltip("How long the brake has to be held before it counts as being pressed all the way, in seconds. " +
             "A tap is worth what it was pressed; a brake leaned on is a hard stop whatever the pedal's travel, " +
             "which is what a stop is - the pedal is only how it was started.")]
    public float brakeDollyHoldTime = 0.7f;

    [Header("Steering Camera")]
    [Tooltip("Let the default third-person view lag sideways while the truck is cornering, so a hard corner " +
             "shows a glimpse of the truck's flank the way a racing chase camera does. It is added to each of " +
             "the view's two cameras' own Horizontal Damping (Cinemachine's sideways damping on the follow " +
             "camera), so the framing it was authored at is what it returns to, and only this view takes it - " +
             "the overhead and near views are framed on fixed offsets for what they show.")]
    public bool steerDamping = true;

    [Tooltip("The most sideways lag it may add, in the same units as the cameras' own Horizontal Damping. " +
             "This is a glimpse rather than a lurch: the truck is allowed to turn a little within the frame, " +
             "not to present its whole side to the camera.")]
    public float steerDampingMax = 0.68f;

    [Tooltip("How long the steer has to be held before it counts in full, in seconds. This is what makes a " +
             "quick flick of the wheel leave the camera alone while a corner the driver settles into brings the " +
             "truck's flank into view, which is the way racing games read a corner.")]
    public float steerDampingHoldTime = 0.5f;

    [Tooltip("Speed in km/h below which steering does not move the camera at all. A truck being turned round " +
             "on the spot is not cornering, and the camera should not swing for it.")]
    public float steerDampingSpeedFloor = 12f;

    [Tooltip("Speed in km/h at which a corner is worth the whole lag. Above it the steering lock available " +
             "shrinks as the speed rises, so the corner itself is gentler and the lag needs no more room.")]
    public float steerDampingFullSpeed = 85f;

    [Tooltip("How fast the extra lag arrives and how fast it eases out again, per second of Horizontal " +
             "Damping. Slower out than in, so the truck comes back to the middle of the screen gradually " +
             "rather than snapping back the moment the wheel is straightened.")]
    public float steerDampingInSpeed = 2.4f;
    public float steerDampingOutSpeed = 1.3f;

    [Header("Boost Camera Settings")]
    public float boostCamDuration = 1.5f;

    private Cinemachine3rdPersonFollow dyncamicCam;

    private CinemachineVirtualCamera mode2Cam;
    private Cinemachine3rdPersonFollow dynamicCamMode2;
    private CinemachineComposer dynamicCamMode3;

    // The default view's two cameras and the distances they were authored with, so the brake pull is added
    // to those rather than to whatever a previous pull happened to leave behind.
    private Cinemachine3rdPersonFollow mainCam;
    private float dynamicCamBaseDistance;
    private float mainCamBaseDistance;

    // How far the camera is pulled in towards the truck right now, in metres of that pull.
    private float brakePull;

    // The speed in km/h the truck was doing when the brakes went on. This - rather than the speed left in it
    // as it slows - is what the pull is read off, so a stop keeps the strength it began with.
    private float brakeEntrySpeed;

    // How long the brakes have been on for, so that holding them counts as pressing them harder.
    private float brakeHold;

    // The default view's own Horizontal Damping, as the cameras were authored, so the steering lag is added
    // to that rather than to whatever the last steering input left behind.
    private Vector3 dynamicCamBaseDamping;
    private Vector3 mainCamBaseDamping;

    // How much extra sideways lag the steering has asked for, and how long the steer has been held, which is
    // the half that keeps a flick of the wheel from moving the camera.
    private float steerDampingNow;
    private float steerHold;

    private float nextSwitchCam = 0f;

    [Header("Focus Effect")]
    public CarController focusCar;

    public float normalFOV = 60f;
    public float focusFOV = 54f;
    public float focusZoomSpeed = 8f;


    private void Awake()
    {  
        var vcam = Cameras[0].GetComponent<CinemachineVirtualCamera>();
        mode2Cam = Cameras[3].GetComponent<CinemachineVirtualCamera>();
        var vcam3 = Cameras[4].GetComponent<CinemachineVirtualCamera>();

        dyncamicCam = vcam.GetCinemachineComponent<Cinemachine3rdPersonFollow>();
        dynamicCamMode2 = mode2Cam.GetCinemachineComponent<Cinemachine3rdPersonFollow>();
        dynamicCamMode3 = vcam3.GetCinemachineComponent<CinemachineComposer>();

        // Prepare All Modes:
        mode2Cam.Follow = mode2Pointers[0].transform;
        mode2Cam.LookAt = mode2Pointers[0].transform;
        dynamicCamMode3.m_ScreenY = 0.725f;

        // The default view's second camera, and what the two of them sit at when nothing is pulling them in.
        var vcam1 = Cameras[1].GetComponent<CinemachineVirtualCamera>();
        mainCam = vcam1 != null ? vcam1.GetCinemachineComponent<Cinemachine3rdPersonFollow>() : null;

        dynamicCamBaseDistance = dyncamicCam != null ? dyncamicCam.CameraDistance : 0f;
        mainCamBaseDistance = mainCam != null ? mainCam.CameraDistance : 0f;

        // The same for the sideways damping the steering works on: the value the cameras were framed with.
        dynamicCamBaseDamping = dyncamicCam != null ? dyncamicCam.Damping : Vector3.zero;
        mainCamBaseDamping = mainCam != null ? mainCam.Damping : Vector3.zero;

        // The view the player last drove from, so a level opens on it rather than always back on the default
        // one. Mode 1 owns two cameras - the one it shows at speed and the one it shows at rest - and the one
        // at rest is the closer of the two, so that is where it opens until the speed says otherwise.
        mode = Mathf.Clamp(PlayerPrefs.GetInt(ModePrefKey, 1), 1, 3);

        ActivateCamera(CameraForMode(mode));
    }

    /// <summary>The camera a view shows when it is first entered. Mode 1's own choice moves with the speed.</summary>
    private static int CameraForMode(int m)
    {
        switch (m)
        {
            case 2: return 3;   // Mode2 - the nearer dynamic shot
            case 3: return 4;   // Above - the overhead view
            default: return 0;  // Dynamic - the closer of the default view's two
        }
    }

    private void SaveMode()
    {
        PlayerPrefs.SetInt(ModePrefKey, mode);
        PlayerPrefs.Save();
    }

    private void Update()
    {
        // C on the keyboard, Y / triangle on a gamepad.
        if (GameInput.CameraPressed() && Time.time >= nextSwitchCam)
        {
            switch(mode)
            {
                case 1:
                    StartCoroutine(transitionToMode(3));              
                    mode = 2;
                    break;
                case 2:
                    mode = 3;
                    StartCoroutine(transitionToMode(4));
                    // ActivateCamera(5);
                    break;
                case 3:
                    mode = 1;
                    break;
                default:
                    mode = 1;
                    break;                
            }

            UpdateCameraBasedOnCar();
            SaveMode();
            nextSwitchCam = Time.time + 0.2f;
        }

        if (!boostActive && !isTransitioning)
            UpdateCameraBasedOnCar();

        UpdateBrakeCamera();
        UpdateSteeringCamera();
        UpdateFocusEffect();
    }

    /// <summary>
    /// Pulls the default third-person view in towards the truck while the brakes are on.
    ///
    /// The rig already changes to its nearer camera for a hard stop, but that is a step at one threshold and
    /// reads as a cut rather than as the truck closing on you. This is the same idea made continuous, and it
    /// is read off two things only: <em>how hard</em> the brake is being pressed, and the speed the truck was
    /// carrying <em>before</em> the brakes went on. Not the speed left in it as it slows - a stop is the thing
    /// the driver set up, and a long one should not quietly become a gentle one as the truck runs out of
    /// road. It is added to each camera's authored distance, so the view it was framed at is what it returns
    /// to.
    ///
    /// The speed is the one that decides how much this is worth, and it is asked for across nearly the whole
    /// range the truck can do. How hard the brake is being used shapes it, and that is read as the driver
    /// would mean it: a brake that is <em>held</em> counts as a brake pressed all the way, however far the
    /// pedal went. A tap is worth what was tapped; a stop made on a held brake was a hard one, whatever the
    /// travel of the pedal that started it.
    ///
    /// Only the default view takes it. The overhead and near views are framed at fixed distances for what
    /// they show, and closing them in would take that away rather than add to it.
    /// </summary>
    private void UpdateBrakeCamera()
    {
        if (car == null)
        {
            return;
        }

        float forwardSpeed = Vector3.Dot(car.rb.velocity, car.transform.forward) * 3.6f;
        float braking = Mathf.Clamp01(-car.throttleInput);

        // The brakes are on: the speed it was doing when they went on is what this stop is worth, held for as
        // long as they stay on (and taking the faster of the two, so braking later and harder counts for
        // what it is). Off: the memory follows the truck again, so the next stop is read off its own entry.
        brakeEntrySpeed = braking > 0.05f
            ? Mathf.Max(brakeEntrySpeed, forwardSpeed)
            : forwardSpeed;

        // What that entry speed is worth, as a share of the pull: nothing under the floor, all of it at the
        // top, and in proportion in between - so a stop from town speed is a fraction of the pull a stop from
        // flat out is, without ever rounding away to nothing.
        float speedShare = Mathf.InverseLerp(brakeDollySpeedFloor, brakeDollyFullSpeed, brakeEntrySpeed);

        // How hard the brake is being used. A pedal has travel and a key does not, so the two are read
        // together: what was pressed, and how long it has been pressed for. Holding it makes it count as a
        // brake pressed all the way, which is what a held brake is - the pedal is only how the stop started.
        brakeHold = braking > 0.05f ? brakeHold + Time.deltaTime : 0f;

        float held = brakeDollyHoldTime > 0f ? Mathf.Clamp01(brakeHold / brakeDollyHoldTime) : 1f;
        float hardness = Mathf.Max(braking, held);

        float target = brakeDollyIn * Ease(hardness) * speedShare;

        // The move itself is rate-limited in metres per second of distance, so nothing here can snap: the
        // pull takes about a second to arrive however hard it is asked for, and rather longer to let go.
        float rate = target > brakePull ? brakeDollyInSpeed : brakeDollyOutSpeed;
        brakePull = Mathf.MoveTowards(brakePull, target, rate * Time.deltaTime);

        // While the truck is going backwards the rig's own cameras are the ones that know what they are
        // doing - it puts them behind the truck rather than in front of it - so nothing here touches them.
        if (isGoingReverse)
        {
            return;
        }

        float pull = brakePull;

        // BOTH of the default view's cameras are pulled, not only the one on screen. They are the same view
        // at two distances and the rig steps between them as the speed changes, so a camera left at its
        // authored distance arrives a metre and a half further out than the one it replaced - the other half
        // of the pop.
        if (dyncamicCam != null)
        {
            dyncamicCam.CameraDistance = DistanceAfterPull(dynamicCamBaseDistance, pull);
        }

        if (mainCam != null)
        {
            mainCam.CameraDistance = DistanceAfterPull(mainCamBaseDistance, pull);
        }
    }

    /// <summary>
    /// Lets the default third-person view lag sideways while the truck is cornering, so a hard corner shows a
    /// glimpse of the truck's flank.
    ///
    /// The lag is Cinemachine's own sideways damping on the follow cameras - the Horizontal Damping of the
    /// default view - which is a smoothing time on the camera's position in the target's frame: the more of it,
    /// the further the camera is left behind as the truck turns, and the more of the truck's side is on screen.
    /// It is therefore added to the value each camera was framed with, and taken away again by the same route,
    /// so the view returns to exactly the framing it was authored at.
    ///
    /// Three things decide how much of it there is, and they are the three that make a corner read the way a
    /// racing game reads one:
    ///
    /// - the steer itself, scaled by <see cref="CarController.GetSpeedAdjustedSteer"/> - the lock the truck
    ///   actually has at the speed it is doing, rather than the stick's position - so a truck that can barely
    ///   turn at the top of its range does not swing the camera for a full-lock input;
    /// - the speed, because a lurch sideways is something a moving truck does: below the floor speed there is
    ///   none of this at all, which is what keeps a truck being turned round on the spot from swinging the
    ///   view, and it is worth its whole at the full speed;
    /// - how long the steer has been held, so a flick of the wheel leaves the camera alone while a corner the
    ///   driver settles into brings the flank into view.
    ///
    /// Only the default view's two cameras are touched. The overhead and near views are framed on fixed
    /// offsets for what they show, and this is about the third-person chase view.
    /// </summary>
    private void UpdateSteeringCamera()
    {
        float target = 0f;

        if (steerDamping && car != null)
        {
            float forwardSpeed = Vector3.Dot(car.rb.velocity, car.transform.forward) * 3.6f;
            float steer = Mathf.Abs(car.steerInput);

            // A flick is not a corner: the steer has to be held for the hold time before it is worth anything.
            steerHold = steer > 0.05f ? steerHold + Time.deltaTime : 0f;

            float held =
                steerDampingHoldTime > 0f ? Mathf.Clamp01(steerHold / steerDampingHoldTime) : 1f;

            float speedShare =
                Mathf.InverseLerp(steerDampingSpeedFloor, steerDampingFullSpeed, forwardSpeed);

            float corner =
                steer * car.GetSpeedAdjustedSteer() * speedShare;

            target = steerDampingMax * Mathf.Clamp01(corner * held);
        }
        else
        {
            steerHold = 0f;
        }

        float rate = target > steerDampingNow ? steerDampingInSpeed : steerDampingOutSpeed;

        steerDampingNow = Mathf.MoveTowards(steerDampingNow, target, rate * Time.deltaTime);

        if (dyncamicCam != null)
            dyncamicCam.Damping = dynamicCamBaseDamping + new Vector3(steerDampingNow, 0f, 0f);

        if (mainCam != null)
            mainCam.Damping = mainCamBaseDamping + new Vector3(steerDampingNow, 0f, 0f);
    }


    /// <summary>
    /// What a camera's distance becomes with the brake pull on it, never nearer than the floor.
    /// </summary>
    private float DistanceAfterPull(float baseDistance, float pull)
    {
        return Mathf.Max(brakeDollyMinDistance, baseDistance - pull);
    }

    /// <summary>
    /// A smooth start and a smooth finish for the pull, on the 0 - 1 amount the brakes have asked for:
    /// flat at both ends, which is what stops the camera changing distance with a jolt. 3x² - 2x³.
    /// </summary>
    private static float Ease(float amount)
    {
        amount = Mathf.Clamp01(amount);

        return amount * amount * (3f - 2f * amount);
    }

    private void UpdateCameraBasedOnCar()
    {
        float throttle = car.throttleInput;
        float forwardSpeed = Vector3.Dot(car.rb.velocity, car.transform.forward) * 3.6f;

        int targetCam = DetermineCamera(forwardSpeed, throttle);

        if (targetCam == currentCam)
            return;

        ActivateCamera(targetCam);
    }

    private void UpdateFocusEffect()
    {
        if (focusCar == null)
            return;

        bool focusActive = focusCar.IsFocusing();

        float targetFOV =
            focusActive
            ? focusFOV
            : normalFOV;

        float targetAmplitude =
            focusActive
            ? 0.25f
            : 0f;

        float targetFrequency =
            focusActive
            ? 1.8f
            : 0f;

        for (int i = 0; i < Cameras.Length; i++)
        {
            CinemachineVirtualCamera vcam =
                Cameras[i].GetComponent<CinemachineVirtualCamera>();

            if (vcam == null)
                continue;

            vcam.m_Lens.FieldOfView =
                Mathf.Lerp(
                    vcam.m_Lens.FieldOfView,
                    targetFOV,
                    Time.deltaTime * focusZoomSpeed);

            CinemachineBasicMultiChannelPerlin noise =
                vcam.GetCinemachineComponent<CinemachineBasicMultiChannelPerlin>();

            if (noise != null)
            {
                noise.m_AmplitudeGain =
                    Mathf.Lerp(
                        noise.m_AmplitudeGain,
                        targetAmplitude,
                        Time.deltaTime * 8f);

                noise.m_FrequencyGain =
                    Mathf.Lerp(
                        noise.m_FrequencyGain,
                        targetFrequency,
                        Time.deltaTime * 8f);
            }
        }
    }

    /// <summary>
    /// Whether the view should be behind the truck, which is what the player sees when the truck is going
    /// backwards.
    ///
    /// It used to be the brake pedal and nothing else - the throttle held past a stop - which is only half
    /// the story. That is the automatic gearbox's way backwards, and it misses two things: a truck that is
    /// rolling back because something pushed it or because it is on a slope, and a manual truck, whose way
    /// backwards is the reverse gear and the gas - a positive throttle, which the old test read as driving
    /// forwards and so never brought the camera round at all.
    ///
    /// So this reads the truck rather than the pedals: the view goes behind it when it is actually rolling
    /// backwards, whatever caused that, and when reverse is being asked for and the truck has stopped making
    /// headway. The second half is what covers the reverse gear standing still - the camera comes round when
    /// the lever does, before the truck has begun to move, which is what a gearbox does.
    /// </summary>
    private bool WantsReverse(float forwardSpeed, float throttle)
    {
        if (forwardSpeed < -reverseRollingSpeed) return true;

        bool asked = car != null && car.ReverseGearEngaged
            ? true                        // manual - the reverse gear is engaged
            : throttle < -0.8f;           // automatic - the brake is held past a stop

        float limit = isGoingReverse ? reverseExitSpeed : reverseEnterSpeed;

        return asked && forwardSpeed < limit;
    }

    /// <summary>Puts the rig behind the truck: the cameras swap to the markers that face the other way, and
    /// the reverse beep starts. Safe to call every frame it is needed - it only does the one-shot work once.</summary>
    private void EnterReverse()
    {
        if (!isGoingReverse)
        {
            reverseSound.SoundReverse();
            isGoingReverse = true;
        }

        dyncamicCam.CameraDistance = -2f;

        mode2Cam.Follow = mode2Pointers[1].transform;
        mode2Cam.LookAt = mode2Pointers[1].transform;
        dynamicCamMode2.CameraDistance = -0.7f;

        dynamicCamMode3.m_ScreenY = 0.525f;
    }

    /// <summary>Puts the rig back the way it was: facing the truck from behind it going forwards, the beep
    /// off. One-shot work guarded too.</summary>
    private void LeaveReverse()
    {
        reverseSound.StopReverse();

        dyncamicCam.CameraDistance = 2f;

        mode2Cam.Follow = mode2Pointers[0].transform;
        mode2Cam.LookAt = mode2Pointers[0].transform;
        dynamicCamMode2.CameraDistance = 0.7f;

        dynamicCamMode3.m_ScreenY = 0.725f;

        isGoingReverse = false;
    }

    private int DetermineCamera(float forwardSpeed, float throttle)
    {
        bool wantsReverse = WantsReverse(forwardSpeed, throttle);

        if (wantsReverse && !isGoingReverse)
        {
            EnterReverse();
            return CameraForMode(mode);
        }

        if (!wantsReverse && isGoingReverse)
        {
            LeaveReverse();
            return CameraForMode(mode);
        }

        if (isGoingReverse)
        {
            // Still reversing: keep the rig behind the truck, in case a level opened with it already set.
            EnterReverse();
            return CameraForMode(mode);
        }

        bool hardBrake = throttle < -0.1f && forwardSpeed > 30f;

        if (hardBrake)
        {
            switch(mode)
            {
                case 1:
                    return 0;
                case 2:
                    dynamicCamMode3.m_ScreenY = 0.725f;
                    return 3;
                case 3:
                    return 4;
                default:
                    return 0;
            }
        }
            
        if (forwardSpeed < 30f)
        {
            reverseSound.StopReverse();
            switch(mode)
            {
                case 1:
                    return 0;
                case 2:
                    return 3;
                case 3:
                    dynamicCamMode3.m_ScreenY = 0.725f;
                    return 4;
                default:
                    return 0;
            }
        }

        switch(mode)
        {
            case 1:
                return 1;
            case 2:
                return 3;
            case 3:
                dynamicCamMode3.m_ScreenY = 0.725f;
                return 4;
            default:
                return 1;
        }
    }

    public void TriggerBoostCamera()
    {
        if (!boostActive)
            StartCoroutine(BoostCameraRoutine());
    }

    private IEnumerator BoostCameraRoutine()
    {
        boostActive = true;
        cmb.m_DefaultBlend.m_Time = 1f;
        
        if (mode == 1)
        {
            ActivateCamera(2);
        }
        else if (mode == 3)
        {
            ActivateCamera(5);
        }
        
        yield return new WaitForSeconds(boostCamDuration);
        
        cmb.m_DefaultBlend.m_Time = 2f;
        boostActive = false;
    }

    private void ActivateCamera(int index)
    {
        currentCam = index;

        for (int i = 0; i < Cameras.Length; i++)
        {
            Cameras[i].SetActive(i == index);

            var vcam = Cameras[i].GetComponent<CinemachineVirtualCamera>();
            if (vcam != null)
                vcam.Priority = (i == index) ? 1000 : 0;
        }
    }

    private IEnumerator transitionToMode(int x)
    {
        cmb.m_DefaultBlend.m_Style = CinemachineBlendDefinition.Style.Cut;
        isTransitioning = true;
        ActivateCamera(x);
        yield return new WaitForSeconds(0.1f);
        isTransitioning = false;
        cmb.m_DefaultBlend.m_Style = CinemachineBlendDefinition.Style.EaseInOut;
        cmb.m_DefaultBlend.m_Time = 2f;
    }
}