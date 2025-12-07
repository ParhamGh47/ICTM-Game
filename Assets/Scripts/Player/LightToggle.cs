using UnityEngine;

public class LightToggle : MonoBehaviour
{
    public Light headlight1;
    public Light headlight2;
    public AudioSource toggleSound;

    public bool startOn = false;
    private bool headlightsOn;

    public float toggleCooldown = 0.5f;
    private float lastToggleTime = -1f;

    void Awake()
    {
        headlightsOn = startOn;

        if (headlight1 != null)
            headlight1.enabled = headlightsOn;

        if (headlight2 != null)
            headlight2.enabled = headlightsOn;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.L) && Time.time - lastToggleTime >= toggleCooldown)
        {
            if (toggleSound != null)
            {
                toggleSound.Play();
            }

            headlightsOn = !headlightsOn;

            if (headlight1 != null)
                headlight1.enabled = headlightsOn;

            if (headlight2 != null)
                headlight2.enabled = headlightsOn;

            lastToggleTime = Time.time;
        }
    }
}
