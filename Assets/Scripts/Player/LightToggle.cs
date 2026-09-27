using UnityEngine;

public class LightToggle : MonoBehaviour
{
    [Header("Headlights")]
    public Light[] headlights;
    public AudioSource toggleSound;

    [Header("Emitting Lights")]
    public Renderer emissionObject;

    [Tooltip("Which material slot of the emission object is the lamp itself. The headlight is one mesh " +
             "carrying more than one material - the lens that lights up, and the rings around it - and the " +
             "lens is the second slot. The paint system reads this too, so a player recolours the lamp and " +
             "leaves the trim around it alone.")]
    public int emissionMaterialIndex = 1;

    public bool startOn = false;
    private bool headlightsOn;

    public float toggleCooldown = 0.5f;
    private float lastToggleTime = -1f;

    private Material targetMat;

    // Whether the lamps have been knocked off the truck. Set by the damage system. A lamp that is still
    // on the truck works exactly as it always did, however battered it looks; this only says whether
    // there is still a lamp there to light. Nothing here touches the truck's driving.
    private bool lampDead;

    void Awake()
    {
        headlightsOn = startOn;

        Material lens = LensMaterial();
        if (lens != null)
        {
            targetMat = lens;
        }

        ApplyLamp();
    }

    /// <summary>
    /// The material slot of the emission object that is the lamp itself. Public so the paint system can
    /// recolour exactly the lens the truck lights up, rather than every light-ish slot on the mesh.
    /// </summary>
    public int LensMaterialIndex { get { return emissionMaterialIndex; } }

    /// <summary>
    /// The lamp's own material, taken through <c>materials</c> so this truck gets its own copy instead of
    /// switching the glow on the model's shared material - which would light every truck at once.
    /// </summary>
    private Material LensMaterial()
    {
        if (emissionObject == null)
        {
            Debug.LogWarning("Emission object missing on '" + name + "'.");
            return null;
        }

        Material[] materials = emissionObject.materials;
        if (emissionMaterialIndex < 0 || materials.Length <= emissionMaterialIndex)
        {
            Debug.LogWarning("Emission object on '" + name + "' has no material " + emissionMaterialIndex + ".");
            return null;
        }

        return materials[emissionMaterialIndex];
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

            ApplyLamp();

            lastToggleTime = Time.time;
        }
    }

    /// <summary>
    /// The lamps are gone - knocked clean off the truck. They never light again, however the driver works
    /// the switch. While there is still a lamp on the truck it lights exactly as it always did, however
    /// badly bent the rest of the truck is.
    /// </summary>
    public void KillLamp()
    {
        lampDead = true;

        ApplyLamp();
    }

    /// <summary>Whether the lamps have been knocked off the truck.</summary>
    public bool LampDead { get { return lampDead; } }

    /// <summary>Whether the lamps are lit now - the switch, and whether there is still a lamp to light.</summary>
    private bool LampShouldBeOn
    {
        get { return headlightsOn && !lampDead; }
    }

    /// <summary>Puts the switch and the damage together onto the lamps and the lens.</summary>
    private void ApplyLamp()
    {
        SetHeadlights(LampShouldBeOn);
        UpdateEmission();
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
        if (emissionMaterialIndex < 0 || materials.Length <= emissionMaterialIndex) return;
        if (materials[emissionMaterialIndex] == null) return;

        targetMat = materials[emissionMaterialIndex];
        UpdateEmission();
    }

    private void UpdateEmission()
    {
        if (targetMat == null) return;

        if (LampShouldBeOn)
        {
            targetMat.EnableKeyword("_EMISSION");
        }
        else
        {
            targetMat.DisableKeyword("_EMISSION");
        }
    }
}
