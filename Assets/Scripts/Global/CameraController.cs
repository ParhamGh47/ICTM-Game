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
    [Tooltip("How much closer to the truck the camera comes with the brakes fully on at speed, in metres. " +
             "The rig already steps to its low-speed camera on a hard stop; this is the pull on top of that, " +
             "so the stop is something the camera shows rather than only something the truck does. Applies to " +
             "the default third-person view only - the overhead and near views cannot pull in without giving " +
             "away the framing they exist for.")]
    public float brakeDollyIn = 1.3f;

    [Tooltip("How close to the truck the camera may be pulled, in metres. The rig's low-speed camera already " +
             "sits nearer than the one it shows at speed, so this is what stops a hard stop from putting the " +
             "view inside the truck.")]
    public float brakeDollyMinDistance = 1.2f;

    [Tooltip("How fast the camera closes on the truck once the brakes go on, in metres per second.")]
    public float brakeDollyInSpeed = 6f;

    [Tooltip("How fast the camera lets the truck back out once the brakes come off, in metres per second. " +
             "Slower than closing, which is what makes it read as the truck pulling away from you.")]
    public float brakeDollyOutSpeed = 2.5f;

    [Tooltip("The speed in km/h below which braking does not move the camera at all. A full stop from a " +
             "crawl has no drama to show.")]
    public float brakeDollySpeedFloor = 25f;

    [Tooltip("The speed in km/h at which braking has its full pull on the camera.")]
    public float brakeDollyFullSpeed = 85f;

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

    // How much of the brake pull is in force right now, 0 to 1, eased.
    private float brakePull;

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
        UpdateFocusEffect();
    }

    /// <summary>
    /// Pulls the default third-person view in towards the truck while the brakes are on.
    ///
    /// The rig already changes to its nearer camera for a hard stop, but that is a step at one threshold and
    /// reads as a cut rather than as the truck closing on you. This is the same idea made continuous: the
    /// pull in is the brake pressure the driver is actually using, scaled by how fast the truck was going, so
    /// leaning on the brakes at speed is a deliberate closing in and a gentle stop from a crawl barely moves
    /// the camera at all. It is added to each camera's authored distance, so the view it was framed at is
    /// what it returns to.
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
        float speedShare = Mathf.InverseLerp(brakeDollySpeedFloor, brakeDollyFullSpeed, forwardSpeed);

        float target = braking * speedShare;

        float rate = target > brakePull ? brakeDollyInSpeed : brakeDollyOutSpeed;
        brakePull = Mathf.MoveTowards(brakePull, target, rate * Time.deltaTime);

        // While the truck is going backwards the rig's own cameras are the ones that know what they are
        // doing - it puts them behind the truck rather than in front of it - so nothing here touches them.
        if (isGoingReverse)
        {
            return;
        }

        float pull = brakeDollyIn * brakePull;

        // Only the camera being shown is pulled; the other is put back to what it was authored at, so the
        // view is right the moment the rig steps between them.
        if (dyncamicCam != null)
        {
            dyncamicCam.CameraDistance =
                DistanceAfterPull(dynamicCamBaseDistance, currentCam == 0, pull);
        }

        if (mainCam != null)
        {
            mainCam.CameraDistance =
                DistanceAfterPull(mainCamBaseDistance, currentCam == 1, pull);
        }
    }

    /// <summary>
    /// What a camera's distance becomes with the brake pull on it, never nearer than the floor.
    /// </summary>
    private float DistanceAfterPull(float baseDistance, bool isTheCameraOnShow, float pull)
    {
        return isTheCameraOnShow
            ? Mathf.Max(brakeDollyMinDistance, baseDistance - pull)
            : baseDistance;
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