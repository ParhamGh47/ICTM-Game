using UnityEngine;
using System.Collections;

public class CameraController : MonoBehaviour
{
    public GameObject[] Cameras;
    // 0 = Low Speed
    // 1 = Main
    // 2 = Braking
    // 3 = Reverse
    // 4 = Above (transition camera)
    // 5 = Boost

    public CarController car;
    public ReverseBeep reverseSound;

    private int currentCam = 1;
    private bool isTransitioning = false;
    private bool boostActive = false;

    [Header("Reverse Camera Settings")]
    public float reverseSpeedThreshold = 5f;
    public float forwardToReverseDelay = 1.5f;
    public float reverseToForwardDelay = 1f;

    [Header("Boost Camera Settings")]
    public float boostCamDuration = 1.5f;

    private void Awake()
    {
        ActivateCamera(1);
    }

    private void Update()
    {
        if (!isTransitioning && !boostActive)
            UpdateCameraBasedOnCar();
    }

    private void UpdateCameraBasedOnCar()
    {
        float speed = car.currentSpeedKPH;
        float throttle = car.throttleInput;

        int targetCam = DetermineCamera(speed, throttle);

        if (targetCam == currentCam)
            return;

        bool forwardToReverse = currentCam != 3 && targetCam == 3;
        bool reverseToForward = currentCam == 3 && targetCam != 3;

        if (forwardToReverse)
        {
            StartCoroutine(TransitionAbove(3, forwardToReverseDelay));
        }
        else if (reverseToForward)
        {
            StartCoroutine(TransitionAbove(targetCam, reverseToForwardDelay));
        }
        else
        {
            ActivateCamera(targetCam);
        }
    }

    private int DetermineCamera(float speed, float throttle)
    {
        if (throttle < -0.8f)
        {
            if (speed < reverseSpeedThreshold)
            {
                if (!isTransitioning || currentCam == 3)
                    reverseSound.SoundReverse();

                return 3;
            }
            else
            {
                if (currentCam != 3) reverseSound.StopReverse();
                return currentCam;
            }
        }
        else
        {
            if (currentCam == 3 && !isTransitioning)
                reverseSound.StopReverse();
        }

        bool hardBrake = throttle < -0.1f && speed > 30f;
        if (hardBrake) return 2;

        if (speed < 30f) return 0;

        return 1;
    }

    private IEnumerator TransitionAbove(int finalCam, float delay)
    {
        isTransitioning = true;

        ActivateCamera(4);
        yield return new WaitForSeconds(delay);

        ActivateCamera(finalCam);
        isTransitioning = false;

        if (finalCam == 3)
            reverseSound.SoundReverse();
        else
            reverseSound.StopReverse();
    }

    public void TriggerBoostCamera()
    {
        if (!boostActive)
            StartCoroutine(BoostCameraRoutine());
    }

    private IEnumerator BoostCameraRoutine()
    {
        boostActive = true;

        ActivateCamera(5);
        yield return new WaitForSeconds(boostCamDuration);

        boostActive = false;
    }

    private void ActivateCamera(int index)
    {
        currentCam = index;

        for (int i = 0; i < Cameras.Length; i++)
        {
            Cameras[i].SetActive(i == index);
        }
    }
}
