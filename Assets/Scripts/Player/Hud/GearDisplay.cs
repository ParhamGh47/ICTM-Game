using UnityEngine;
using UnityEngine.UI;

public class GearDisplay : MonoBehaviour
{
    [Header("References")]
    public EngineAudio engineAudio;
    public Text gearText;

    void Update()
    {
        if (engineAudio == null || gearText == null) return;

        int gear = engineAudio.CurrentGear;

        if (gear == 0)
            gearText.text = "R";
        else
            gearText.text = gear.ToString();
    }
}
