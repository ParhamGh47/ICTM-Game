using UnityEngine;

public class CameraController : MonoBehaviour
{
    public GameObject[] Cameras;
    // 0 = Low Speed
    // 1 = Main
    // 2 = Braking
    // 3 = Reverse
    // 4 = Above (transition camera)

    public CarController car;
    public ReverseBeep reverseSound;

    private int currentCamIndex = 1;
    private bool isTransitioning = false;
    public float transitionDuration = 0.2f;

    private void Awake()
    {
        ActivateCamera(1);
        currentCamIndex = 1;
    }

    private void Update()
    {
        if (!isTransitioning)
            UpdateCameraBasedOnCar();
    }

    private void UpdateCameraBasedOnCar()
    {
        float speed = car.currentSpeedKPH;
        float throttle = car.throttleInput;

        int targetCam = DetermineTargetCamera(speed, throttle);

        // If camera doesn't need to change, do nothing
        if (targetCam == currentCamIndex) return;

        // Check if transition with Above camera is needed
        bool reversingToForward = currentCamIndex == 3 && targetCam != 3;
        bool forwardToReversing = currentCamIndex != 3 && targetCam == 3;

        if (reversingToForward || forwardToReversing)
        {
            StartCoroutine(TransitionThroughAbove(targetCam));
        }
        else
        {
            ActivateCamera(targetCam);
        }
    }

    // Camera decision logic preserved
    private int DetermineTargetCamera(float speed, float throttle)
    {
        if (throttle < -0.8f)
        {
            reverseSound.SoundReverse();
            return 3; // reverse camera
        }
        else
        {
            reverseSound.StopReverse();
        }

        bool hardBrake = throttle < -0.1f && speed > 30f;
        if (hardBrake) return 2;

        if (speed < 30f) return 0;

        return 1;
    }

    private System.Collections.IEnumerator TransitionThroughAbove(int finalCam)
    {
        isTransitioning = true;

        ActivateCamera(4); // Above camera
        yield return new WaitForSeconds(transitionDuration);

        ActivateCamera(finalCam);
        isTransitioning = false;
    }

    public void ActivateCamera(int index)
    {
        currentCamIndex = index;

        for (int i = 0; i < Cameras.Length; i++)
        {
            Cameras[i].SetActive(i == index);
        }
    }
}
