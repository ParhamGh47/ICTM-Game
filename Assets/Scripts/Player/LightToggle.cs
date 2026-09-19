using UnityEngine;

public class LightToggle : MonoBehaviour
{
    [Header("Headlights")]
    public Light[] headlights;
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
        // L on the keyboard, B / circle on a gamepad.
        if (GameInput.LightsPressed() && Time.time - lastToggleTime >= toggleCooldown)
        {
            if (PauseTracker.Instance != null && PauseTracker.Instance.isPaused)
            return;

            if (toggleSound != null)
                toggleSound.Play();

            // The truck may have been repainted since the last press, and painting a part swaps its
            // material for a copy of the paint system's own: follow the lens that is on the truck now,
            // or the glow would be switched on a material nothing is drawn with.
            RefreshEmissionMaterial();

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

    /// <summary>
    /// Points the glow at the material the truck is actually wearing.
    ///
    /// The customize screen repaints the truck by swapping this slot for a copy, so the material
    /// grabbed at startup can end up orphaned - and a lens that no longer answers the light key. This
    /// takes the slot as it is rather than making yet another copy, because the copy may belong to the
    /// paint system, and stealing it would leave the truck painted with a material the paint system no
    /// longer knows about.
    /// </summary>
    public void RefreshEmissionMaterial()
    {
        if (emissionObject == null) return;

        Material[] materials = emissionObject.sharedMaterials;
        if (materials.Length < 2 || materials[1] == null) return;

        targetMat = materials[1];
        UpdateEmission();
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
