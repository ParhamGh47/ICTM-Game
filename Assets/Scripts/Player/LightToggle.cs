using UnityEngine;

public class LightToggle : MonoBehaviour
{
    public Light headlight1;
    public Light headlight2;
    public AudioSource toggleSound;

    [Header("Emmiting Lights")]
    public Renderer emissionObject;  

    public bool startOn = false;
    private bool headlightsOn;

    public float toggleCooldown = 0.5f;
    private float lastToggleTime = -1f;

    private Material targetMat;

    void Awake()
    {
        headlightsOn = startOn;

        if (headlight1) headlight1.enabled = headlightsOn;
        if (headlight2) headlight2.enabled = headlightsOn;

        if (emissionObject != null && emissionObject.materials.Length > 1)
        {
            targetMat = emissionObject.materials[1];
            UpdateEmission();
        }
        else
        {
            Debug.LogWarning("Emission object missing or does not have at least 2 materials.");
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.L) && Time.time - lastToggleTime >= toggleCooldown)
        {
            if (toggleSound != null)
                toggleSound.Play();

            headlightsOn = !headlightsOn;

            if (headlight1) headlight1.enabled = headlightsOn;
            if (headlight2) headlight2.enabled = headlightsOn;

            UpdateEmission();

            lastToggleTime = Time.time;
        }
    }

    private void UpdateEmission()
    {
        if (targetMat == null) return;

        if (headlightsOn)
        {
            targetMat.EnableKeyword("_EMISSION");
        }
        else
        {
            targetMat.DisableKeyword("_EMISSION");
        }
    }
}
