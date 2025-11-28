using UnityEngine;

public class CameraController : MonoBehaviour
{
    public GameObject[] Cameras; 
    // 0 = Low Speed
    // 1 = Main Camera
    // 2 = Braking Camera
    // 3 = Reverse Camera

    public CarController car;

    private void Awake()
    {
        ActivateCamera(1);
    }

    private void Update()
    {
        UpdateCameraBasedOnCar();
    }

    private void UpdateCameraBasedOnCar()
    {
        float speed = car.currentSpeedKPH;
        float throttle = car.throttleInput;

        if (throttle < -0.8f)
        {
            ActivateCamera(3);
            return;
        }

        bool isBrakingHard = throttle < -0.1f && speed > 30f;

        if (isBrakingHard)
        {
            ActivateCamera(2);
            return;
        }

        if (speed < 30f)
        {
            ActivateCamera(0);
            return;
        }


        ActivateCamera(1);
    }

    public void ActivateCamera(int index)
    {
        for (int i = 0; i < Cameras.Length; i++)
        {
            Cameras[i].SetActive(i == index);
        }
    }
}
