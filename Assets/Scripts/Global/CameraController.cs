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
    public float reverseSpeedThreshold = 20f;
    public bool isGoingReverse = false;

    [Header("Boost Camera Settings")]
    public float boostCamDuration = 1.5f;

    private Cinemachine3rdPersonFollow dyncamicCam;

    private CinemachineVirtualCamera mode2Cam;
    private Cinemachine3rdPersonFollow dynamicCamMode2;
    private CinemachineComposer dynamicCamMode3;

    private float nextSwitchCam = 0f;

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

        ActivateCamera(0);
        mode = 1;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.C) && Time.time >= nextSwitchCam)
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
            nextSwitchCam = Time.time + 0.2f;
        }

        if (!boostActive && !isTransitioning)
            UpdateCameraBasedOnCar();
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

    private void FixSoudReverseInMode3(float forwardSpeed, float throttle)
    {
        bool wantsReverse = throttle < -0.8f;
        bool shouldShowReverseCam = wantsReverse && Mathf.Abs(forwardSpeed) < reverseSpeedThreshold;

        if (!isGoingReverse && shouldShowReverseCam)
        {
            reverseSound.SoundReverse();
            isGoingReverse = true;
        }
        else
        {
            reverseSound.StopReverse();
            isGoingReverse = false;
        }

    }

    private int DetermineCamera(float forwardSpeed, float throttle)
    {
        bool wantsReverse = throttle < -0.8f;
        bool shouldShowReverseCam = wantsReverse && Mathf.Abs(forwardSpeed) < reverseSpeedThreshold;

        if (!isGoingReverse && shouldShowReverseCam)
        {
            reverseSound.SoundReverse();

            dyncamicCam.CameraDistance = -2f;

            mode2Cam.Follow = mode2Pointers[1].transform;
            mode2Cam.LookAt = mode2Pointers[1].transform;
            dynamicCamMode2.CameraDistance = -0.7f;

            dynamicCamMode3.m_ScreenY = 0.525f;

            isGoingReverse = true;

            switch(mode)
            {
                case 1:
                    return 0;
                case 2:
                    return 3;
                case 3:
                    return 4;
                default:
                    return 0;
            }
        }

        if (isGoingReverse && !shouldShowReverseCam && !wantsReverse)
        {
            reverseSound.StopReverse();

            dyncamicCam.CameraDistance = 2f;

            mode2Cam.Follow = mode2Pointers[0].transform;
            mode2Cam.LookAt = mode2Pointers[0].transform;
            dynamicCamMode2.CameraDistance = 0.7f;

            dynamicCamMode3.m_ScreenY = 0.725f;

            isGoingReverse = false;

            switch(mode)
            {
                case 1:
                    return 0;
                case 2:
                    return 3;
                case 3:
                    return 4;
                default:
                    return 0;
            }
        }

        if (isGoingReverse)
        {
            dyncamicCam.CameraDistance = -2f;

            mode2Cam.Follow = mode2Pointers[1].transform;
            mode2Cam.LookAt = mode2Pointers[1].transform;
            dynamicCamMode2.CameraDistance = -0.7f;

            dynamicCamMode3.m_ScreenY = 0.525f;

            switch(mode)
            {
                case 1:
                    return 0;
                case 2:
                    return 3;
                case 3:
                    return 4;
                default:
                    return 0;
            }
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