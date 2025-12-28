using UnityEngine;

public class LightToggle : MonoBehaviour
{
    [Header("Headlights")]
    public Light[] headlights;          // Assign as many lights as you want
    public AudioSource toggleSound;

    [Header("Emitting Lights")]
    public Renderer emissionObject;  

    public bool startOn = false;
    private bool headlightsOn;

    public float toggleCooldown = 0.5f;
    private float lastToggleTime = -1f;

    private Material targetMat;

    void Awake()
    {
        headlightsOn = startOn;

        // Enable/disable all headlights at start
        SetHeadlights(headlightsOn);

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

            SetHeadlights(headlightsOn);
            UpdateEmission();

            lastToggleTime = Time.time;
        }
    }

    private void SetHeadlights(bool state)
    {
        if (headlights == null) return;

        foreach (Light l in headlights)
        {
            if (l != null)
                l.enabled = state;
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
