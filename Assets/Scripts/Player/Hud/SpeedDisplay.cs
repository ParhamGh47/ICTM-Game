using UnityEngine;
using UnityEngine.UI;

public class SpeedDisplay : MonoBehaviour
{
    [Header("References")]
    public CarController carController;
    public Text speedText;
    public Transform pointer;

    [Header("Pointer Settings")]
    public float minZRotation = -15f;
    public float maxZRotation = -130f;
    public float maxSpeed = 240f;

    void Update()
    {
        if (carController == null || speedText == null) return;

        int speedInt = Mathf.RoundToInt(carController.currentSpeedKPH);
        speedText.text = speedInt.ToString();

        if (pointer != null)
        {
            float t = Mathf.InverseLerp(0f, maxSpeed, speedInt);

            float zRotation = Mathf.Lerp(minZRotation, maxZRotation, t);

            pointer.localRotation = Quaternion.Euler(0f, 0f, zRotation);
        }
    }
}
