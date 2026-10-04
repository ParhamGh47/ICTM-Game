using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The truck customization screen. The player picks one of the truck's parts on the left, and a colour
/// and a finish for it on the right, while the truck in the middle turns so the change can be seen from
/// every side. The colour can be chosen three ways - a preset, the picker, or by typing the numbers - and
/// all three are the same choice: see <see cref="BuildNumbers"/> for the last of them.
///
/// Like <see cref="CreditsScreen"/> and <see cref="TipsScreen"/> the whole UI is built in code, so the
/// scene file only holds a camera, a light, an EventSystem and this component. The look reuses the
/// loading screen's palette (dark navy, orange accent, light labels), so it belongs to the same family
/// as the rest of the menus.
///
/// The preview is the real player truck prefab, not a stand-in model, so what is shown is exactly what
/// the levels will show. It is made safe to display by assembling it under an inactive parent and
/// switching every gameplay script off before anything of it can run - see <see cref="BuildPreview"/>.
///
/// The choices themselves live in <see cref="TruckPaint"/>, which saves them and repaints the player's
/// truck in every level, so this screen only has to draw them and hand changes over.
///
/// The column on the right is four different shapes stacked - a finish grid, a palette, a picker and a row
/// of boxes - which is what a nearest-neighbour guess reads worst, so the whole screen is wired by hand in
/// <see cref="WireNavigation"/>. The picker's field is the one control that keeps the four directions for
/// itself, so a pad can move the cursor inside it rather than only the highlight around it.
///
/// Escape and the gamepad's B button come from the scene's <see cref="EscBack"/> component.
/// </summary>
[DisallowMultipleComponent]
public class CustomizeScreen : MonoBehaviour
{
    // ---------------------------------------------------------------- content

    [Header("Content")]
    [Tooltip("The player truck prefab. The preview uses it, so the preview always matches the truck in " +
             "the levels. Assign Assets/Prefabs/Utils/Truck (Player).prefab.")]
    public GameObject truckPrefab;

    public string titleText = "CUSTOMIZE";
    public string hintText = "ESC  TO  GO  BACK";
    public string noteText = "Your paint is saved and used in every level.";
    [Tooltip("Caption on the swatch that puts a part back to the colour the model came with.")]
    public string defaultCaption = "DEF";
    public string finishCaption = "FINISH";
    public string colourCaption = "COLOUR";
    public string customCaption = "YOUR COLOUR";

    [Tooltip("The caption over each of the three colour channel boxes, in order: red, green, blue.")]
    public string[] channelCaptions = { "R", "G", "B" };

    [Tooltip("The caption over the hex box.")]
    public string hexCaption = "HEX";

    [Header("Art (optional)")]
    [Tooltip("Falls back to the project's default TMP font when empty.")]
    public TMP_FontAsset font;

    // ---------------------------------------------------------------- look

    [Header("Colours (the loading screen palette)")]
    public Color backgroundColor = new Color(0.012f, 0.024f, 0.055f, 1f);
    public Color accentColor = new Color(1f, 0.64f, 0.16f, 1f);
    public Color labelColor = new Color(0.86f, 0.89f, 0.95f, 1f);
    public Color panelColor = new Color(0.055f, 0.082f, 0.145f, 1f);
    public Color frameColor = new Color(0.42f, 0.47f, 0.58f, 1f);
    [Tooltip("Flat ambient light for the preview, so the paint reads clearly on every side.")]
    public Color ambientColor = new Color(0.34f, 0.37f, 0.44f, 1f);
    [Tooltip("The faux sky the preview reflects. Metallic, chrome and glass take their brightness from " +
             "what is around them, and this stands in for a level's own sky. It is never seen, because " +
             "the preview camera clears to the background colour.")]
    public Color skyTint = new Color(0.52f, 0.62f, 0.80f, 1f);
    public Color groundTint = new Color(0.22f, 0.21f, 0.20f, 1f);
    [Tooltip("The bright spot in that sky, which a polished finish catches.")]
    public Color sunTint = new Color(1f, 0.95f, 0.85f, 1f);
    [Tooltip("How large that bright spot is. Higher is a tighter, hotter highlight.")]
    public float sunSize = 22f;

    // ---------------------------------------------------------------- layout

    [Header("Layout (reference resolution is 1920x1080)")]
    public float titleSize = 54f;
    public float labelSize = 26f;
    public float columnWidth = 420f;
    public float partHeight = 48f;
    public float partGap = 10f;
    [Tooltip("Distance from the top of the screen down to the first part button.")]
    public float partTopY = -156f;
    public float captionSize = 22f;
    [Tooltip("Distance from the top of the screen down to the first row of the colour column.")]
    public float columnTopY = -200f;
    public float swatchSize = 68f;
    public float swatchGap = 10f;
    public int paletteColumns = 5;
    public float sideMargin = 60f;
    public float styleHeight = 46f;
    public float styleGap = 10f;
    public int styleColumns = 3;
    public float styleTextSize = 19f;
    [Tooltip("Space between the caption of one section and the section above it.")]
    public float sectionGap = 24f;
    public float captionGap = 8f;
    public float pickerFieldHeight = 120f;
    public float pickerHueHeight = 22f;
    public float pickerGap = 12f;
    public float previewSwatchHeight = 30f;

    [Tooltip("The row of colour boxes under the picker: three channels and a hex, which say the colour " +
             "as numbers and can be typed into.")]
    public float numberRowGap = 8f;
    public float numberCaptionSize = 15f;
    public float numberFieldHeight = 34f;
    public float numberFieldGap = 8f;
    public float numberFieldSize = 17f;
    public float hexFieldWidth = 140f;

    // ---------------------------------------------------------------- preview

    [Header("Preview")]
    [Tooltip("Degrees per second the truck turns. 0 keeps it still.")]
    public float spinSpeed = 28f;
    [Tooltip("How much of the screen's height the truck should span.")]
    public float previewHeight = 0.72f;
    [Tooltip("How much of the screen's width the truck may span when it turns broadside. Keep this " +
             "inside the gap the parts list and the colour column leave between them.")]
    public float previewWidth = 0.46f;
    [Tooltip("Where the truck sits on screen, as a fraction of the viewport. It is off centre because " +
             "the parts list and the colour column take the two sides.")]
    public Vector2 previewCentre = new Vector2(0.513f, 0.5f);
    [Tooltip("Camera height above the truck, in degrees.")]
    public float previewPitch = 17f;
    [Tooltip("Camera angle around the truck, in degrees.")]
    public float previewYaw = -28f;

    // ---------------------------------------------------------------- transition

    [Header("Transition")]
    [Tooltip("Where BACK and Escape go. Must be in Build Settings.")]
    public string levelsSceneName = "Levels";
    public float fadeInTime = 0.35f;

    // ---------------------------------------------------------------- state

    private sealed class PartRow
    {
        public TruckPart part;
        public TextMeshProUGUI label;
        public Image activeBar;
        public Button button;
    }

    private sealed class Swatch
    {
        public Color colour;                     // the preset it stands for; ignored by the Default swatch
        public bool isDefault;
        public Image activeBar;
        public Button button;
    }

    private sealed class StyleRow
    {
        public int style;
        public TextMeshProUGUI label;
        public Image activeBar;
        public Button button;
    }

    private readonly List<PartRow> partRows = new List<PartRow>();
    private readonly List<Swatch> swatches = new List<Swatch>();
    private readonly List<StyleRow> styleRows = new List<StyleRow>();

    // The three channel boxes, then the hex box. Kept so the values can be written back into them
    // whenever the colour changes somewhere else - the palette, the picker, another part.
    private readonly List<TMP_InputField> numberFields = new List<TMP_InputField>();
    private TMP_InputField hexField;

    private Camera previewCamera;
    private Transform previewRoot;
    private Transform previewPivot;
    private TruckPaintApplier applier;
    private CanvasGroup group;
    private ColourPicker picker;
    private Image previewSwatch;
    private Button resetButton;
    private Button backButton;
    private Cubemap previewEnvironment;

    private Bounds previewBounds;
    private float columnBottom;
    private bool hasPreviewBounds;

    private TruckPart selectedPart = TruckPart.Body;

    // ---------------------------------------------------------------- lifecycle

    private void Start()
    {
        SetUpRendering();
        BuildPreview();
        BuildUi();
        FramePreview();
        Refresh();

        StartCoroutine(FadeIn());
    }

    private void Update()
    {
        CloseEditedBoxOnCancel();

        if (previewPivot == null || Mathf.Approximately(spinSpeed, 0f)) return;

        // Turned about the world's up axis, so the truck spins on the spot whatever rotation this object
        // happens to have - and on unscaled time, so a leftover pause (timeScale 0) cannot freeze it.
        previewPivot.Rotate(0f, spinSpeed * Time.unscaledDeltaTime, 0f, Space.World);
    }

    /// <summary>
    /// Lets the pad's B button out of a colour box the way Escape already lets the keyboard out.
    ///
    /// A focused text field keeps the arrow keys for its own caret and watches for Escape itself, so it has a
    /// way out on a keyboard on its own. On a pad there is no Escape: B is the project's Cancel axis, which a
    /// text field knows nothing about, so without this a controller could open a box and never leave it. Escape
    /// is left to the field, which also puts the text back the way it found it.
    /// </summary>
    private void CloseEditedBoxOnCancel()
    {
        if (!Input.GetButtonDown("Cancel")) return;

        TMP_InputField editing = null;

        if (hexField != null && hexField.isFocused) editing = hexField;

        for (int i = 0; editing == null && i < numberFields.Count; i++)
            if (numberFields[i].isFocused) editing = numberFields[i];

        if (editing != null) editing.DeactivateInputField();
    }

    private void OnDisable()
    {
        // A colour drag writes many times a second; the values are already in memory, so this is the
        // moment worth putting them on disk.
        TruckPaint.Save();
    }

    private void OnDestroy()
    {
        if (previewEnvironment != null) Destroy(previewEnvironment);
    }

    // ---------------------------------------------------------------- rendering

    private void SetUpRendering()
    {
        previewCamera = Camera.main;
        if (previewCamera == null) previewCamera = FindObjectOfType<Camera>();
        if (previewCamera == null) previewCamera = CreateCamera();

        // A showroom rather than a level: even ambient light from every direction so the paint is legible
        // on the sides facing away from the key light, and no fog to wash it out.
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = ambientColor;
        RenderSettings.fog = false;

        SetUpEnvironment();
    }

    /// <summary>
    /// Gives the preview a sky to reflect. A metallic or glass part is lit mostly by its surroundings,
    /// and a showroom with nothing around it leaves those finishes as a black slab with a hint of the
    /// paint in it - so a small sky is made here and stood in for the level's own.
    ///
    /// It is a handful of colours painted into a cubemap rather than a skybox: it only has to be bright
    /// and have a sun in it, and making it directly means no shader to find and no sky to look at. The
    /// camera clears to a solid colour, so the only thing it is used for is reflections.
    /// </summary>
    private void SetUpEnvironment()
    {
        // A scene that already brings its own environment keeps it.
        if (RenderSettings.defaultReflectionMode == UnityEngine.Rendering.DefaultReflectionMode.Custom &&
            RenderSettings.customReflectionTexture != null)
            return;

        // The sun sits where the preview camera is looking from, so a polished surface always has a
        // highlight to catch on the side the player is watching.
        Vector3 sunDirection = Quaternion.Euler(previewPitch, previewYaw, 0f) * Vector3.forward;

        previewEnvironment = CreateSkyCubemap(16, skyTint, groundTint, sunTint, sunSize, sunDirection);

        RenderSettings.defaultReflectionMode = UnityEngine.Rendering.DefaultReflectionMode.Custom;
        RenderSettings.customReflectionTexture = previewEnvironment;

        // Rebuilds the probe the reflections are read from, so the finishes see the new sky.
        DynamicGI.UpdateEnvironment();
    }

    /// <summary>
    /// Paints a sky into a cubemap: a colour for above, a colour for below, and one bright spot where the
    /// sun is. It is small and soft on purpose - it is a light source for polished surfaces, not a view.
    /// </summary>
    private static Cubemap CreateSkyCubemap(int size, Color sky, Color ground, Color sun, float sunSize,
                                            Vector3 sunDirection)
    {
        Cubemap cubemap = new Cubemap(size, TextureFormat.RGBA32, true);
        cubemap.name = "Customize Sky";
        cubemap.hideFlags = HideFlags.HideAndDontSave;
        cubemap.wrapMode = TextureWrapMode.Clamp;
        cubemap.filterMode = FilterMode.Bilinear;

        Vector3 sunAxis = sunDirection.normalized;

        for (int face = 0; face < 6; face++)
        {
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector3 direction = CubemapDirection((CubemapFace)face, x, y, size);

                    // Up is sky, down is ground, and the horizon between them is the mix of the two.
                    Color colour = Color.Lerp(ground, sky, Mathf.Clamp01(direction.y * 0.5f + 0.5f));

                    float glow = Mathf.Pow(Mathf.Clamp01(Vector3.Dot(direction, sunAxis)), sunSize);
                    colour = Color.Lerp(colour, sun, glow);

                    colour.a = 1f;
                    cubemap.SetPixel((CubemapFace)face, x, y, colour);
                }
            }
        }

        cubemap.Apply(true, false);
        return cubemap;
    }

    /// <summary>The direction a cubemap texel points at, which is what the sky is painted along.</summary>
    private static Vector3 CubemapDirection(CubemapFace face, int x, int y, int size)
    {
        float u = 2f * (x + 0.5f) / size - 1f;
        float v = 2f * (y + 0.5f) / size - 1f;

        switch (face)
        {
            case CubemapFace.PositiveX: return new Vector3(1f, -v, -u);
            case CubemapFace.NegativeX: return new Vector3(-1f, -v, u);
            case CubemapFace.PositiveY: return new Vector3(u, 1f, v);
            case CubemapFace.NegativeY: return new Vector3(u, -1f, -v);
            case CubemapFace.PositiveZ: return new Vector3(u, -v, 1f);
            default: return new Vector3(-u, -v, -1f);
        }
    }

    private Camera CreateCamera()
    {
        GameObject go = new GameObject("Customize Camera", typeof(Camera), typeof(AudioListener));
        go.tag = "MainCamera";

        return go.GetComponent<Camera>();
    }

    // ---------------------------------------------------------------- the preview

    /// <summary>
    /// Instantiates the player truck and leaves it standing still, scripts asleep. Everything here exists
    /// so the real prefab can be shown in a menu without any of its gameplay running.
    ///
    /// A failure here costs the preview and nothing else: the parts list, the palette, the picker, RESET
    /// and BACK are all built afterwards and all work without a truck in the middle of the screen.
    /// </summary>
    private void BuildPreview()
    {
        if (truckPrefab == null)
        {
            Debug.LogWarning("[Customize] No truck prefab is assigned, so there is nothing to preview. " +
                             "Assign the player truck prefab in the Inspector.");
            return;
        }

        // A reference is only checked at the moment it is used, so a field that holds something that is
        // not a GameObject - which is what a wrong file id in the scene file produces - throws from inside
        // Instantiate's own cast rather than when the scene loads. That must not take the screen down with
        // it, so the build is guarded, reported, and whatever it managed to create is cleaned up after.
        try
        {
            BuildPreviewTruck();
        }
        catch (System.Exception exception)
        {
            Debug.LogError("[Customize] The truck preview could not be built, so this screen will show " +
                           "no truck. Reassign 'Truck Prefab' on the Customize object with " +
                           "Assets/Prefabs/Utils/Truck (Player).prefab. " + exception.Message);

            if (previewRoot != null) Destroy(previewRoot.gameObject);
            previewRoot = null;
            previewPivot = null;
            hasPreviewBounds = false;
        }
    }

    /// <summary>Builds the preview itself. Only ever called by <see cref="BuildPreview"/>.</summary>
    private void BuildPreviewTruck()
    {
        // The clone is built under an inactive root. A GameObject created under an inactive parent has not
        // run Awake yet, so every script on the truck can be switched off before any of it executes: this
        // is what makes it safe to show a drivable truck in a screen that has no road, no input and no
        // physics. It is also why the prefab itself never has to be modified.
        previewRoot = new GameObject("Truck Preview").transform;
        previewRoot.SetParent(transform, false);
        previewRoot.gameObject.SetActive(false);

        GameObject truck = Instantiate(truckPrefab, previewRoot);
        truck.name = "Truck";

        MakeInert(truck);

        // Nothing has run yet and every part is in place, so this is the first moment the truck's real
        // size can be measured.
        previewRoot.gameObject.SetActive(true);
        Bounds bounds = ComputeBounds(truck);

        // The model's own pivot is wherever the artist left it, which is rarely the middle of the truck -
        // so it turns about an empty parent placed at the centre of what it actually occupies.
        previewPivot = new GameObject("Pivot").transform;
        previewPivot.SetParent(previewRoot, false);
        previewPivot.position = bounds.center;

        truck.transform.SetParent(previewPivot, true);

        // The paint job is applied by a component added here rather than one saved in the prefab, so the
        // truck that every level places is untouched - and the preview's own material copies are destroyed
        // with the scene. AttachAndApply reuses an applier if there already is one, so this cannot end up
        // with a duplicate (TruckPaintApplier only allows one per truck).
        applier = TruckPaint.AttachAndApply(truck);

        previewBounds = bounds;
        hasPreviewBounds = true;
    }

    /// <summary>
    /// Takes the gameplay out of a truck that is only being looked at: no driving, no steering, no
    /// exhaust, no horn, no light - and no gravity, which would otherwise drop it out of the frame.
    /// </summary>
    private static void MakeInert(GameObject root)
    {
        // CarController drives it, the wheels steer, the boost and the meters animate, the horn plays.
        // None of that belongs in a menu, and all of it is a MonoBehaviour.
        MonoBehaviour[] behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
            if (behaviours[i] != null) behaviours[i].enabled = false;

        // Things that move, make noise or cast light on their own.
        ParticleSystem[] particles = root.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < particles.Length; i++)
        {
            ParticleSystem system = particles[i];
            if (system == null) continue;

            // A ParticleSystem is not a Behaviour, so it has no enabled flag: stopping it and switching
            // its renderer off is what takes it out of the picture.
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystemRenderer particleRenderer = system.GetComponent<ParticleSystemRenderer>();
            if (particleRenderer != null) particleRenderer.enabled = false;
        }

        TrailRenderer[] trails = root.GetComponentsInChildren<TrailRenderer>(true);
        for (int i = 0; i < trails.Length; i++)
            if (trails[i] != null) trails[i].enabled = false;

        AudioSource[] sources = root.GetComponentsInChildren<AudioSource>(true);
        for (int i = 0; i < sources.Length; i++)
            if (sources[i] != null) sources[i].enabled = false;

        // The scene's camera owns the one listener; a second one on the truck would only warn.
        AudioListener[] listeners = root.GetComponentsInChildren<AudioListener>(true);
        for (int i = 0; i < listeners.Length; i++)
            if (listeners[i] != null) listeners[i].enabled = false;

        Light[] lights = root.GetComponentsInChildren<Light>(true);
        for (int i = 0; i < lights.Length; i++)
            if (lights[i] != null) lights[i].enabled = false;

        // Nothing can touch the truck and nothing can fall: it is a display piece.
        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
            if (colliders[i] != null) colliders[i].enabled = false;

        Rigidbody[] bodies = root.GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < bodies.Length; i++)
        {
            Rigidbody body = bodies[i];
            if (body == null) continue;

            body.isKinematic = true;
            body.useGravity = false;
            body.detectCollisions = false;
            body.velocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }
    }

    /// <summary>The world-space box the truck's actual meshes occupy - its effects are not part of it.</summary>
    private static Bounds ComputeBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);

        bool found = false;
        Bounds bounds = new Bounds(root.transform.position, Vector3.one);

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null) continue;

            // A particle or trail box is not the truck and would otherwise dominate the framing.
            if (renderer is ParticleSystemRenderer || renderer is TrailRenderer) continue;
            if (renderer.bounds.size.sqrMagnitude <= 0f) continue;

            if (!found)
            {
                bounds = renderer.bounds;
                found = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return bounds;
    }

    /// <summary>
    /// Points the scene's camera at the truck and sizes the view so the truck fills the space the UI
    /// leaves for it. An orthographic camera is used because the preview is a diagram of the paint, not a
    /// photograph of it: nothing is distorted by perspective, and the framing is exact whatever the model
    /// turns out to be.
    /// </summary>
    private void FramePreview()
    {
        if (previewCamera == null) return;

        previewCamera.orthographic = true;
        previewCamera.clearFlags = CameraClearFlags.SolidColor;
        previewCamera.backgroundColor = backgroundColor;

        if (!hasPreviewBounds)
        {
            // No truck: just a clean backdrop behind the UI.
            previewCamera.orthographicSize = 5f;
            previewCamera.transform.position = new Vector3(0f, 0f, -10f);
            previewCamera.transform.rotation = Quaternion.identity;
            return;
        }

        Bounds bounds = previewBounds;
        float aspect = previewCamera.aspect > 0.01f ? previewCamera.aspect : 16f / 9f;

        // How big the truck looks: its height, plus however much of its length the camera's height adds,
        // and - because it turns - its full length across the screen at the broadside.
        float elevation = Mathf.Abs(previewPitch) * Mathf.Deg2Rad;
        float horizontal = Mathf.Sqrt(bounds.extents.x * bounds.extents.x + bounds.extents.z * bounds.extents.z);
        float halfHeight = bounds.extents.y * Mathf.Cos(elevation) + horizontal * Mathf.Sin(elevation);

        float sizeForHeight = halfHeight / Mathf.Clamp(previewHeight, 0.1f, 1f);
        float sizeForWidth = horizontal / (Mathf.Clamp(previewWidth, 0.1f, 1f) * aspect);

        previewCamera.orthographicSize = Mathf.Max(Mathf.Max(sizeForHeight, sizeForWidth), 0.05f);

        // The camera looks at the truck from above and to one side, so a turning truck shows its roof, its
        // flank and its front.
        Vector3 direction = Quaternion.Euler(previewPitch, previewYaw, 0f) * Vector3.forward;
        previewCamera.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);

        // The truck is placed where the UI leaves room for it, not in the middle: the parts list runs down
        // the left of the screen and the colours down the right, so the truck is nudged to sit between
        // them. Moving the camera moves what is on screen the opposite way, hence the minus signs.
        float distance = previewCamera.orthographicSize * 6f;
        Vector3 shift =
            previewCamera.transform.right * (-(previewCentre.x - 0.5f) * 2f * previewCamera.orthographicSize * aspect) +
            previewCamera.transform.up * (-(previewCentre.y - 0.5f) * 2f * previewCamera.orthographicSize);

        previewCamera.transform.position = bounds.center - direction * distance + shift;

        // Clipping planes that always bracket the truck, whatever its size.
        float reach = bounds.extents.magnitude;
        previewCamera.nearClipPlane = Mathf.Max(0.05f, distance - reach * 2f);
        previewCamera.farClipPlane = distance + reach * 4f;
    }

    // ---------------------------------------------------------------- the UI

    private void BuildUi()
    {
        GameObject canvasGo = new GameObject("Customize Canvas",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        canvasGo.transform.SetParent(transform, false);

        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;                 // below the loading screen and the transition fade

        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        group = canvasGo.GetComponent<CanvasGroup>();
        group.alpha = 0f;

        Transform root = canvasGo.transform;

        // No background image: the camera clears to the same navy, so the preview shows through the
        // canvas and the two read as one surface.
        BuildHeader(root);
        BuildPartList(root);
        BuildColourColumn(root);
        BuildActions(root);
    }

    private void BuildHeader(Transform root)
    {
        TextMeshProUGUI title = CreateText("Title", root, titleSize, accentColor, TextAlignmentOptions.TopLeft);
        title.fontStyle = FontStyles.Bold;
        title.text = titleText;
        title.characterSpacing = 6f;

        RectTransform titleRect = title.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(0f, 1f);
        titleRect.pivot = new Vector2(0f, 1f);
        titleRect.sizeDelta = new Vector2(columnWidth + 320f, titleSize * 1.4f);
        titleRect.anchoredPosition = new Vector2(60f, -44f);

        // Thin accent rule under the title, the same orange as the loading bar's fill.
        Image rule = CreateImage("Rule", root, Fade(accentColor, 0.5f));
        RectTransform ruleRect = rule.rectTransform;
        ruleRect.anchorMin = new Vector2(0f, 1f);
        ruleRect.anchorMax = new Vector2(0f, 1f);
        ruleRect.pivot = new Vector2(0f, 1f);
        ruleRect.sizeDelta = new Vector2(240f, 4f);
        ruleRect.anchoredPosition = new Vector2(60f, -124f);

        // Hint line, top-right, kept out of the way of the truck and the colours.
        TextMeshProUGUI hint = CreateText("Hint", root, 26f, Fade(labelColor, 0.65f), TextAlignmentOptions.TopRight);
        hint.text = hintText;

        RectTransform hintRect = hint.rectTransform;
        hintRect.anchorMin = new Vector2(1f, 1f);
        hintRect.anchorMax = new Vector2(1f, 1f);
        hintRect.pivot = new Vector2(1f, 1f);
        hintRect.sizeDelta = new Vector2(700f, 40f);
        hintRect.anchoredPosition = new Vector2(-sideMargin, -56f);
    }

    private void BuildPartList(Transform root)
    {
        RectTransform list = CreateRect("Parts", root);
        list.anchorMin = new Vector2(0f, 1f);
        list.anchorMax = new Vector2(0f, 1f);
        list.pivot = new Vector2(0f, 1f);
        list.sizeDelta = new Vector2(columnWidth, 0f);
        list.anchoredPosition = new Vector2(60f, partTopY);

        for (int i = 0; i < TruckPaint.PartCount; i++)
        {
            TruckPart part = (TruckPart)i;
            partRows.Add(CreatePartRow(list, part, -i * (partHeight + partGap)));
        }
    }

    private PartRow CreatePartRow(RectTransform list, TruckPart part, float y)
    {
        // Named after the part, because MenuNavigation starts on "Body" by that name.
        RectTransform rect = CreateRect(TruckPaint.PartLabels[(int)part], list);
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = new Vector2(columnWidth, partHeight);
        rect.anchoredPosition = new Vector2(0f, y);

        Image plate = rect.gameObject.AddComponent<Image>();
        plate.color = panelColor;
        plate.raycastTarget = true;

        Button button = rect.gameObject.AddComponent<Button>();
        StyleButton(button, plate);

        // The selected part is shown by these two, never by the plate: ButtonFocusEffect already owns the
        // plate's colour, and two things writing to it would fight.
        Image bar = CreateImage("Selected", rect, accentColor);
        RectTransform barRect = bar.rectTransform;
        barRect.anchorMin = new Vector2(0f, 0f);
        barRect.anchorMax = new Vector2(0f, 1f);
        barRect.pivot = new Vector2(0f, 0.5f);
        barRect.sizeDelta = new Vector2(6f, 0f);
        barRect.anchoredPosition = Vector2.zero;

        TextMeshProUGUI label = CreateText("Label", rect, labelSize, labelColor, TextAlignmentOptions.Left);
        label.text = TruckPaint.PartLabels[(int)part];

        RectTransform labelRect = label.rectTransform;
        Stretch(labelRect);
        labelRect.offsetMin = new Vector2(22f, 0f);

        button.onClick.AddListener(() => SelectPart(part));

        return new PartRow { part = part, label = label, activeBar = bar, button = button };
    }

    /// <summary>
    /// The right-hand column: the finish of the selected part, the colour presets, and the picker for
    /// anything the presets do not cover. It is one stack built downwards from <see cref="columnTopY"/>,
    /// so the sections cannot drift apart.
    /// </summary>
    private void BuildColourColumn(Transform root)
    {
        float y = columnTopY;

        y = BuildCaption(root, finishCaption, y);
        y = BuildFinishRow(root, y);

        y -= sectionGap;
        y = BuildCaption(root, colourCaption, y);
        y = BuildPalette(root, y);

        y -= sectionGap;
        y = BuildCustomRow(root, y);
        BuildPicker(root, y);

        // The picker is followed by the boxes that say the same colour as numbers: the field's own height,
        // the hue bar under it, then the row.
        float numbersTop = y - (pickerFieldHeight + pickerGap + pickerHueHeight + numberRowGap);
        BuildNumbers(root, numbersTop);

        // The boxes are the last thing in the column, so their bottom edge is where the note goes.
        columnBottom = numbersTop - (numberCaptionSize * 1.35f + numberFieldHeight);
    }

    /// <summary>Draws a section heading and returns the y the section's own content starts at.</summary>
    private float BuildCaption(Transform root, string text, float y)
    {
        TextMeshProUGUI caption = CreateText("Caption", root, captionSize, Fade(labelColor, 0.7f),
            TextAlignmentOptions.BottomLeft);
        caption.text = text;
        caption.fontStyle = FontStyles.Bold;
        caption.characterSpacing = 3f;

        RectTransform rect = caption.rectTransform;
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.sizeDelta = new Vector2(ColumnWidth, captionSize * 1.5f);
        rect.anchoredPosition = new Vector2(-sideMargin, y);

        return y - rect.sizeDelta.y - captionGap;
    }

    private float BuildFinishRow(Transform root, float y)
    {
        int columns = Mathf.Clamp(styleColumns, 1, TruckPaint.StyleCount);
        int rows = Mathf.CeilToInt(TruckPaint.StyleCount / (float)columns);
        float width = (ColumnWidth - (columns - 1) * styleGap) / columns;

        for (int i = 0; i < TruckPaint.StyleCount; i++)
        {
            int column = i % columns;
            int row = i / columns;

            RectTransform rect = CreateRect(TruckPaint.StyleLabels[i], root);
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.sizeDelta = new Vector2(width, styleHeight);
            rect.anchoredPosition = new Vector2(
                -sideMargin - (columns - 1 - column) * (width + styleGap),
                y - row * (styleHeight + styleGap));

            Image plate = rect.gameObject.AddComponent<Image>();
            plate.color = panelColor;
            plate.raycastTarget = true;

            Button button = rect.gameObject.AddComponent<Button>();
            StyleButton(button, plate);

            Image bar = CreateImage("Selected", rect, accentColor);
            RectTransform barRect = bar.rectTransform;
            barRect.anchorMin = new Vector2(0f, 0f);
            barRect.anchorMax = new Vector2(1f, 0f);
            barRect.pivot = new Vector2(0.5f, 0f);
            barRect.sizeDelta = new Vector2(0f, 5f);
            barRect.anchoredPosition = Vector2.zero;

            TextMeshProUGUI label = CreateText("Label", rect, styleTextSize, labelColor, TextAlignmentOptions.Center);
            label.text = TruckPaint.StyleLabels[i];
            label.fontStyle = FontStyles.Bold;
            label.characterSpacing = 2f;
            Stretch(label.rectTransform);

            int style = i;
            button.onClick.AddListener(() => SelectStyle(style));

            styleRows.Add(new StyleRow { style = i, label = label, activeBar = bar, button = button });
        }

        return y - rows * styleHeight - (rows - 1) * styleGap;
    }

    private float BuildPalette(Transform root, float y)
    {
        int columns = Mathf.Clamp(paletteColumns, 1, TruckPaint.PaletteCount + 1);
        int total = TruckPaint.PaletteCount + 1;                 // the colours, plus Default
        int rows = Mathf.CeilToInt(total / (float)columns);

        RectTransform grid = CreateRect("Palette", root);
        grid.anchorMin = new Vector2(1f, 1f);
        grid.anchorMax = new Vector2(1f, 1f);
        grid.pivot = new Vector2(1f, 1f);
        grid.sizeDelta = new Vector2(ColumnWidth, rows * swatchSize + (rows - 1) * swatchGap);
        grid.anchoredPosition = new Vector2(-sideMargin, y);

        for (int i = 0; i < total; i++)
        {
            bool isDefault = i >= TruckPaint.PaletteCount;

            int column = i % columns;
            int row = i / columns;

            RectTransform cell = CreateRect(isDefault ? "Default" : TruckPaint.PaletteNames[i], grid);
            cell.anchorMin = new Vector2(0f, 1f);
            cell.anchorMax = new Vector2(0f, 1f);
            cell.pivot = new Vector2(0f, 1f);
            cell.sizeDelta = new Vector2(swatchSize, swatchSize);
            cell.anchoredPosition = new Vector2(column * (swatchSize + swatchGap), -row * (swatchSize + swatchGap));

            // Intentionally not the colour the part came with: this swatch stands for "as the model is",
            // and letting its colour follow the selected part would make the swatch itself a moving target.
            Color colour = isDefault ? frameColor : TruckPaint.Palette[i];

            swatches.Add(CreateSwatch(cell, colour, isDefault));
        }

        return y - grid.sizeDelta.y;
    }

    private Swatch CreateSwatch(RectTransform cell, Color colour, bool isDefault)
    {
        // A frame with the colour inset inside it, so even Black and Graphite read as a swatch rather than
        // as a hole in the screen.
        Image plate = cell.gameObject.AddComponent<Image>();
        plate.color = Fade(frameColor, 0.55f);
        plate.raycastTarget = true;

        Image fill = CreateImage("Fill", cell, colour);

        RectTransform fillRect = fill.rectTransform;
        Stretch(fillRect);
        fillRect.offsetMin = new Vector2(3f, 3f);
        fillRect.offsetMax = new Vector2(-3f, -3f);

        Button button = cell.gameObject.AddComponent<Button>();
        StyleButton(button, fill);                 // the fill is what the focus effect brightens

        Image bar = CreateImage("Selected", cell, accentColor);
        RectTransform barRect = bar.rectTransform;
        barRect.anchorMin = new Vector2(0f, 0f);
        barRect.anchorMax = new Vector2(1f, 0f);
        barRect.pivot = new Vector2(0.5f, 0f);
        barRect.sizeDelta = new Vector2(0f, 6f);
        barRect.anchoredPosition = Vector2.zero;

        if (isDefault)
        {
            TextMeshProUGUI caption = CreateText("Caption", cell, 18f, backgroundColor, TextAlignmentOptions.Center);
            caption.text = defaultCaption;
            caption.fontStyle = FontStyles.Bold;
            caption.characterSpacing = 2f;
            Stretch(caption.rectTransform);
        }

        button.onClick.AddListener(() => SelectSwatch(colour, isDefault));

        return new Swatch { colour = colour, isDefault = isDefault, activeBar = bar, button = button };
    }

    /// <summary>The heading of the picker, carrying a live swatch of the colour it is standing on.</summary>
    private float BuildCustomRow(Transform root, float y)
    {
        TextMeshProUGUI caption = CreateText("Caption", root, captionSize, Fade(labelColor, 0.7f),
            TextAlignmentOptions.BottomLeft);
        caption.text = customCaption;
        caption.fontStyle = FontStyles.Bold;
        caption.characterSpacing = 3f;

        RectTransform rect = caption.rectTransform;
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.sizeDelta = new Vector2(ColumnWidth, captionSize * 1.5f);
        rect.anchoredPosition = new Vector2(-sideMargin, y);

        previewSwatch = CreateImage("Preview", root, Color.white);
        RectTransform swatchRect = previewSwatch.rectTransform;
        swatchRect.anchorMin = new Vector2(1f, 1f);
        swatchRect.anchorMax = new Vector2(1f, 1f);
        swatchRect.pivot = new Vector2(1f, 1f);
        swatchRect.sizeDelta = new Vector2(ColumnWidth * 0.45f, previewSwatchHeight);
        swatchRect.anchoredPosition = new Vector2(-sideMargin, y - (captionSize * 1.5f - previewSwatchHeight));

        return y - captionSize * 1.5f - captionGap;
    }

    private void BuildPicker(Transform root, float y)
    {
        RectTransform rect = CreateRect("Picker", root);
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.sizeDelta = new Vector2(ColumnWidth, 0f);
        rect.anchoredPosition = new Vector2(-sideMargin, y);

        picker = rect.gameObject.AddComponent<ColourPicker>();
        picker.cursorColor = labelColor;
        picker.cursorOutlineColor = Fade(backgroundColor, 0.9f);
        picker.fieldHeight = pickerFieldHeight;
        picker.hueHeight = pickerHueHeight;
        picker.gap = pickerGap;
        picker.onChanged += OnPickedColour;
        picker.onReleased += TruckPaint.Save;

        picker.Build(rect);
    }

    /// <summary>
    /// The colour as numbers: a box for each channel and one for the hex, which is the row a player uses
    /// when they already know the colour they want rather than going looking for it.
    ///
    /// It is a second view of the same choice rather than a second choice, so it is written back into
    /// whenever the colour changes anywhere else (see <see cref="SyncNumberFields"/>), and typing into any box
    /// paints with what is now in all of them. There is nothing to switch between the two: a player who
    /// types into the red box and looks up sees the picker and the truck already moved.
    /// </summary>
    private void BuildNumbers(Transform root, float y)
    {
        float captionHeight = numberCaptionSize * 1.35f;

        RectTransform row = CreateRect("Colour Numbers", root);
        row.anchorMin = new Vector2(1f, 1f);
        row.anchorMax = new Vector2(1f, 1f);
        row.pivot = new Vector2(1f, 1f);
        row.sizeDelta = new Vector2(ColumnWidth, captionHeight + numberFieldHeight);
        row.anchoredPosition = new Vector2(-sideMargin, y);

        int channels = Mathf.Max(1, channelCaptions.Length);
        float channelWidth = (ColumnWidth - hexFieldWidth - channels * numberFieldGap) / channels;

        float x = 0f;

        for (int i = 0; i < channels; i++)
        {
            numberFields.Add(BuildNumberBox(row, channelCaptions[i], x, channelWidth, false));
            x += channelWidth + numberFieldGap;
        }

        hexField = BuildNumberBox(row, hexCaption, x, hexFieldWidth, true);
    }

    /// <summary>One box of the number row: a caption, and an editable field under it.</summary>
    private TMP_InputField BuildNumberBox(RectTransform row, string caption, float x, float width, bool isHex)
    {
        float captionHeight = numberCaptionSize * 1.35f;

        RectTransform cell = CreateRect(isHex ? hexCaption : caption, row);
        cell.anchorMin = new Vector2(0f, 1f);
        cell.anchorMax = new Vector2(0f, 1f);
        cell.pivot = new Vector2(0f, 1f);
        cell.sizeDelta = new Vector2(width, captionHeight + numberFieldHeight);
        cell.anchoredPosition = new Vector2(x, 0f);

        TextMeshProUGUI label = CreateText("Caption", cell, numberCaptionSize, Fade(labelColor, 0.7f),
            TextAlignmentOptions.TopLeft);
        label.text = caption;
        label.fontStyle = FontStyles.Bold;
        label.characterSpacing = 2f;

        RectTransform labelRect = label.rectTransform;
        labelRect.anchorMin = new Vector2(0f, 1f);
        labelRect.anchorMax = new Vector2(0f, 1f);
        labelRect.pivot = new Vector2(0f, 1f);
        labelRect.sizeDelta = new Vector2(width, captionHeight);
        labelRect.anchoredPosition = Vector2.zero;

        RectTransform box = CreateRect("Box", cell);
        box.anchorMin = new Vector2(0f, 1f);
        box.anchorMax = new Vector2(0f, 1f);
        box.pivot = new Vector2(0f, 1f);
        box.sizeDelta = new Vector2(width, numberFieldHeight);
        box.anchoredPosition = new Vector2(0f, -captionHeight);

        // Built asleep and woken at the end, which is not tidiness: a TMP_InputField added to a live object
        // runs its own OnEnable at once, before there is a text component to point at - and that is the moment
        // it builds the caret it needs for typing. Without this the box is drawn and shows its value but
        // cannot be typed into at all, which is exactly what a player would call "there is no way to enter
        // the value". Letting it start life inactive means Awake and OnEnable both run with everything wired.
        box.gameObject.SetActive(false);

        Image plate = box.gameObject.AddComponent<Image>();
        plate.color = panelColor;
        plate.raycastTarget = true;

        // The text lives inside a masked viewport of its own, so a hex string that outgrows the box scrolls
        // under its edge instead of spilling over the box beside it.
        RectTransform viewport = CreateRect("Viewport", box);
        Stretch(viewport);
        viewport.offsetMin = new Vector2(9f, 3f);
        viewport.offsetMax = new Vector2(-6f, -3f);
        viewport.gameObject.AddComponent<RectMask2D>();

        TextMeshProUGUI text = CreateText("Text", viewport, numberFieldSize, labelColor, TextAlignmentOptions.Left);
        text.fontStyle = FontStyles.Bold;
        Stretch(text.rectTransform);

        TMP_InputField input = box.gameObject.AddComponent<TMP_InputField>();

        // The text component first: several of the setters below read it as they are applied, so it has to be
        // in place before any of them.
        input.textComponent = text;
        input.textViewport = viewport;
        input.targetGraphic = plate;
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.richText = false;
        input.characterLimit = isHex ? 7 : 3;
        input.contentType = isHex
            ? TMP_InputField.ContentType.Standard
            : TMP_InputField.ContentType.IntegerNumber;
        input.caretWidth = 2;
        input.customCaretColor = true;
        input.caretColor = accentColor;
        input.selectionColor = Fade(accentColor, 0.45f);

        // A selectable can only ever tint its own plate darker, never brighter, so the box at rest is the
        // dimmed one and the highlighted and selected states are the full plate colour. It is a small step,
        // the same kind the project's own buttons use, but it is what says a box holds the highlight - a box
        // with no text in it otherwise looks like every other box.
        ColorBlock colours = input.colors;
        colours.normalColor = new Color(0.7f, 0.7f, 0.7f, 1f);
        colours.highlightedColor = Color.white;
        colours.pressedColor = new Color(0.86f, 0.86f, 0.86f, 1f);
        colours.selectedColor = Color.white;
        colours.disabledColor = new Color(0.4f, 0.4f, 0.4f, 1f);
        colours.fadeDuration = 0.1f;
        input.colors = colours;

        // One handler for every way an edit can end - confirm, Escape, or the pointer going elsewhere -
        // because that is the point the value should be taken, and the point reached by all of them.
        if (isHex) input.onEndEdit.AddListener(OnHexEdited);
        else input.onEndEdit.AddListener(OnNumberEdited);

        box.gameObject.SetActive(true);

        return input;
    }

    private void BuildActions(Transform root)
    {
        resetButton = CreateButton("Reset", root, "RESET");
        RectTransform resetRect = resetButton.GetComponent<RectTransform>();
        resetRect.anchorMin = new Vector2(0f, 0f);
        resetRect.anchorMax = new Vector2(0f, 0f);
        resetRect.pivot = new Vector2(0f, 0f);
        resetRect.sizeDelta = new Vector2(200f, 54f);
        resetRect.anchoredPosition = new Vector2(60f, 46f);
        resetButton.onClick.AddListener(ResetPaint);

        backButton = CreateButton("Back", root, "BACK");
        RectTransform backRect = backButton.GetComponent<RectTransform>();
        backRect.anchorMin = new Vector2(0f, 0f);
        backRect.anchorMax = new Vector2(0f, 0f);
        backRect.pivot = new Vector2(0f, 0f);
        backRect.sizeDelta = new Vector2(200f, 54f);
        backRect.anchoredPosition = new Vector2(276f, 46f);
        backButton.onClick.AddListener(GoBack);

        // Note under the picker, so the screen says what is stored without a line of labels.
        TextMeshProUGUI note = CreateText("Note", root, 22f, Fade(labelColor, 0.5f), TextAlignmentOptions.TopRight);
        note.text = noteText;

        RectTransform noteRect = note.rectTransform;
        noteRect.anchorMin = new Vector2(1f, 1f);
        noteRect.anchorMax = new Vector2(1f, 1f);
        noteRect.pivot = new Vector2(1f, 1f);
        noteRect.sizeDelta = new Vector2(ColumnWidth + 200f, 30f);
        noteRect.anchoredPosition = new Vector2(-sideMargin, columnBottom - 26f);

        // Every piece of the screen is wired by hand: the column on the right is a finish grid, a palette, a
        // picker and a row of boxes stacked on each other, which is exactly the shape Unity's guess reads
        // worst, and a guess that lands on the wrong section is worse than no movement at all.
        WireNavigation();
    }

    /// <summary>
    /// Wires the whole screen as the shapes it is drawn as: the parts as a list, the finishes as three to a
    /// row, the palette as the grid it looks like, and the boxes as a row.
    ///
    /// Two places are not a shape at all. The picker's field keeps the four directions for itself so a pad can
    /// move the cursor inside it (see <see cref="ColourPicker.Field"/>), and the hue bar keeps left and right
    /// for its own value, which is what a slider is for - so the way out of the hue bar is up into the field
    /// or down into the boxes.
    /// </summary>
    private void WireNavigation()
    {
        int columns = Mathf.Clamp(paletteColumns, 1, TruckPaint.PaletteCount + 1);
        int paletteRows = Mathf.Max(1, Mathf.CeilToInt(swatches.Count / (float)columns));
        int styleCols = Mathf.Clamp(styleColumns, 1, Mathf.Max(1, styleRows.Count));
        int styleRowCount = Mathf.Max(1, Mathf.CeilToInt(styleRows.Count / (float)styleCols));

        Selectable field = picker != null ? picker.Field : null;
        Selectable bottomStyle = MiddleOfLastStyleRow(styleCols, styleRowCount);

        Selectable firstBox = numberFields.Count > 0 ? numberFields[0] : null;
        Selectable lastBox = hexField != null
            ? hexField
            : (numberFields.Count > 0 ? numberFields[numberFields.Count - 1] : null);

        // The parts list: a list, with the column beside it as its way right and BACK at the end of it.
        for (int i = 0; i < partRows.Count; i++)
        {
            Selectable up = i > 0 ? partRows[i - 1].button : null;
            Selectable down = i < partRows.Count - 1 ? partRows[i + 1].button : backButton;

            SetNavigation(partRows[i].button, null, SwatchBeside(i, paletteRows, columns), down, up);
        }

        // The finishes: three to a row, so left and right stay in the row and down leaves the grid.
        for (int i = 0; i < styleRows.Count; i++)
        {
            int column = i % styleCols;
            int row = i / styleCols;

            Selectable left = column > 0 ? styleRows[i - 1].button : null;
            Selectable right = column < styleCols - 1 && i + 1 < styleRows.Count ? styleRows[i + 1].button : null;
            Selectable below = row < styleRowCount - 1 && i + styleCols < styleRows.Count
                ? styleRows[i + styleCols].button
                : SwatchAt(columns / 2, columns);

            SetNavigation(styleRows[i].button, left, right, below, null);
        }

        // The palette: the grid it looks like, five across. Off the left of a row is the part that sits
        // beside it, so the two columns are each other's way in and out rather than dead ends.
        for (int i = 0; i < swatches.Count; i++)
        {
            int column = i % columns;
            int row = i / columns;

            Selectable left = column > 0 ? swatches[i - 1].button : PartBeside(row, paletteRows);
            Selectable right = column < columns - 1 && i + 1 < swatches.Count ? swatches[i + 1].button : null;
            Selectable above = row > 0 ? swatches[i - columns].button : bottomStyle;
            Selectable below = row < paletteRows - 1 && i + columns < swatches.Count
                ? swatches[i + columns].button
                : (field != null ? field : firstBox);

            SetNavigation(swatches[i].button, left, right, below, above);
        }

        // The field takes every direction itself, so it is not given any: confirm is what leaves it, and the
        // screen is where it is told what it leaves for.
        if (picker != null) picker.FieldExit = firstBox != null ? firstBox : lastBox;

        // The hue bar: up and down only, so left and right stay the hue. That is what a slider is for, and
        // with an explicit target on its sides the highlight would move instead of the handle.
        if (picker != null && picker.HueBar != null)
            SetNavigation(picker.HueBar, null, null, firstBox != null ? firstBox : lastBox,
                field != null ? field : bottomStyle);

        // The boxes: a row of their own, with the picker above them and BACK below.
        for (int i = 0; i < numberFields.Count; i++)
        {
            Selectable left = i > 0 ? numberFields[i - 1] : null;
            Selectable right = i < numberFields.Count - 1 ? numberFields[i + 1] : lastBox;

            SetNavigation(numberFields[i], left, right, backButton, field);
        }

        if (hexField != null)
        {
            Selectable left = numberFields.Count > 0 ? numberFields[numberFields.Count - 1] : null;

            SetNavigation(hexField, left, null, backButton, field);
        }

        // RESET and BACK, and the end of the parts list points at them rather than at nothing.
        SetNavigation(resetButton, null, backButton, null, PartAt(partRows.Count - 1));
        SetNavigation(backButton, resetButton, null, null, lastBox != null ? lastBox : PartAt(partRows.Count - 1));
    }

    /// <summary>The palette swatch a part row is beside, and the part row a palette row is beside.</summary>
    private Selectable SwatchBeside(int partIndex, int paletteRows, int columns)
    {
        if (swatches.Count == 0) return null;

        int row = Mathf.Clamp(partIndex * paletteRows / Mathf.Max(1, partRows.Count), 0, paletteRows - 1);
        int index = Mathf.Min(row * columns + columns / 2, swatches.Count - 1);

        return swatches[index].button;
    }

    private Selectable PartBeside(int paletteRow, int paletteRows)
    {
        if (partRows.Count == 0) return null;

        int index = Mathf.Clamp(
            Mathf.RoundToInt((paletteRow + 0.5f) * partRows.Count / Mathf.Max(1, paletteRows)),
            0, partRows.Count - 1);

        return partRows[index].button;
    }

    /// <summary>The middle of the finish row the palette climbs up onto - the row that sits just above it.</summary>
    private Selectable MiddleOfLastStyleRow(int styleCols, int styleRowCount)
    {
        if (styleRows.Count == 0) return null;

        int index = Mathf.Min((styleRowCount - 1) * styleCols + styleCols / 2, styleRows.Count - 1);

        return styleRows[index].button;
    }

    /// <summary>A swatch by grid position - column across, in row <paramref name="row"/>.</summary>
    private Selectable SwatchAt(int column, int columns, int row = 0)
    {
        if (swatches.Count == 0) return null;

        int index = Mathf.Min(Mathf.Max(0, row) * columns + Mathf.Max(0, column), swatches.Count - 1);

        return swatches[index].button;
    }

    private Selectable PartAt(int index)
    {
        return partRows.Count > 0 ? partRows[Mathf.Clamp(index, 0, partRows.Count - 1)].button : null;
    }

    private static void SetNavigation(Selectable target, Selectable left, Selectable right, Selectable down,
                                      Selectable up)
    {
        if (target == null) return;

        Navigation navigation = target.navigation;
        navigation.mode = Navigation.Mode.Explicit;
        navigation.selectOnLeft = left;
        navigation.selectOnRight = right;
        navigation.selectOnDown = down;
        navigation.selectOnUp = up;

        target.navigation = navigation;
    }

    // ---------------------------------------------------------------- interaction

    private void SelectPart(TruckPart part)
    {
        selectedPart = part;
        Refresh();
    }

    private void SelectStyle(int style)
    {
        TruckPaint.SetStyle(selectedPart, style);
        TruckPaint.Save();
        Refresh();
    }

    /// <summary>A preset, or - with <paramref name="isDefault"/> - the colour the model came with.</summary>
    private void SelectSwatch(Color colour, bool isDefault)
    {
        if (isDefault) TruckPaint.Clear(selectedPart);
        else TruckPaint.SetColour(selectedPart, colour);

        TruckPaint.Save();
        Refresh();
    }

    private void OnPickedColour(Color colour)
    {
        TruckPaint.SetColour(selectedPart, colour);
        Refresh();
    }

    /// <summary>The colour the selected part is wearing, whichever way it got there.</summary>
    private Color CurrentColour()
    {
        return TruckPaint.IsPainted(selectedPart) ? TruckPaint.ColourOf(selectedPart) : ColourOnModel();
    }

    /// <summary>
    /// Reads the channel boxes back and paints the selected part with what they say.
    ///
    /// Only a box holding a number counts. A box the player emptied keeps the channel's current value, which
    /// is what makes clearing one to retype it safe - the colour does not collapse to black on the way.
    /// </summary>
    private void OnNumberEdited(string value)
    {
        Color current = CurrentColour();
        float[] channels = { current.r * 255f, current.g * 255f, current.b * 255f };

        for (int i = 0; i < numberFields.Count && i < channels.Length; i++)
        {
            int typed;
            if (int.TryParse(numberFields[i].text, out typed))
                channels[i] = Mathf.Clamp(typed, 0, 255);
        }

        SetPickedColour(new Color(channels[0] / 255f, channels[1] / 255f, channels[2] / 255f));
    }

    /// <summary>Reads the hex box back and paints with it, or puts it back if it is not a colour at all.</summary>
    private void OnHexEdited(string value)
    {
        string text = (value ?? string.Empty).Trim();
        if (text.Length > 0 && text[0] != '#') text = "#" + text;

        Color parsed;
        if (!ColorUtility.TryParseHtmlString(text, out parsed))
        {
            // The box goes back to what the part is really wearing rather than being left showing something
            // that was refused.
            Refresh();
            return;
        }

        SetPickedColour(parsed);
    }

    /// <summary>Paints the selected part, the same way a swatch or the picker does.</summary>
    private void SetPickedColour(Color colour)
    {
        TruckPaint.SetColour(selectedPart, colour);
        TruckPaint.Save();
        Refresh();
    }

    /// <summary>
    /// Writes the colour into the boxes, so they always say what the part is actually wearing.
    ///
    /// The channel boxes take a whole number and the hex box the same colour as a string - the form a player
    /// most likely has to hand if they are copying a colour from somewhere else.
    /// </summary>
    private void SyncNumberFields(Color colour)
    {
        for (int i = 0; i < numberFields.Count; i++)
            SetBoxText(numberFields[i], Mathf.RoundToInt(Mathf.Clamp01(colour[i]) * 255f).ToString());

        SetBoxText(hexField, "#" + ColorUtility.ToHtmlStringRGB(colour));
    }

    private static void SetBoxText(TMP_InputField box, string text)
    {
        if (box == null || box.text == text) return;

        // Without notify, so writing the value back is never mistaken for the player having typed it.
        box.SetTextWithoutNotify(text);
    }

    private void ResetPaint()
    {
        TruckPaint.ResetAll();
        Refresh();
    }

    private void GoBack()
    {
        // No fade of its own: SceneLoader covers menu-to-menu hops with the screen fade, so the transition
        // is handled in one place for every scene.
        TruckPaint.Save();
        SceneLoader.Load(levelsSceneName);
    }

    /// <summary>Redraws every piece of state from <see cref="TruckPaint"/>, which is the one source of truth.</summary>
    private void Refresh()
    {
        bool painted = TruckPaint.IsPainted(selectedPart);
        Color colour = painted ? TruckPaint.ColourOf(selectedPart) : ColourOnModel();
        int style = TruckPaint.StyleOf(selectedPart);

        for (int i = 0; i < partRows.Count; i++)
        {
            PartRow row = partRows[i];
            bool active = row.part == selectedPart;

            row.activeBar.enabled = active;
            row.label.color = active ? accentColor : labelColor;
        }

        // A preset is shown as chosen only while the part actually wears that colour: once the player has
        // been through the picker the part is on no preset at all, and nothing is ticked.
        for (int i = 0; i < swatches.Count; i++)
        {
            Swatch swatch = swatches[i];

            swatch.activeBar.enabled = swatch.isDefault
                ? !painted
                : painted && SameColour(swatch.colour, colour);
        }

        // A finish does nothing until there is a colour under it, so the row says so rather than looking
        // like it is being ignored.
        for (int i = 0; i < styleRows.Count; i++)
        {
            StyleRow row = styleRows[i];
            bool active = painted && row.style == style;

            row.activeBar.enabled = active;
            row.label.color = painted ? (active ? accentColor : labelColor) : Fade(labelColor, 0.45f);
        }

        if (previewSwatch != null) previewSwatch.color = colour;

        // The boxes say the same colour as numbers, wherever the choice was made - a swatch, the picker, or
        // the boxes themselves on the way in.
        SyncNumberFields(colour);

        // The picker follows the selected part, so switching parts shows the colour that part wears.
        if (picker != null && !picker.IsDragging) picker.SetValue(colour, false);

        // The preview repaints itself from the same saved choice, so it never disagrees with the UI.
        if (applier != null) applier.Apply();
    }

    /// <summary>The colour the selected part has on the model, which is where the picker starts from.</summary>
    private Color ColourOnModel()
    {
        return applier != null ? applier.ModelColourOf(selectedPart) : TruckPaint.FallbackColour;
    }

    private static bool SameColour(Color a, Color b)
    {
        return Mathf.Abs(a.r - b.r) < 0.02f && Mathf.Abs(a.g - b.g) < 0.02f && Mathf.Abs(a.b - b.b) < 0.02f;
    }

    private float ColumnWidth
    {
        get
        {
            int columns = Mathf.Clamp(paletteColumns, 1, TruckPaint.PaletteCount + 1);
            return columns * swatchSize + (columns - 1) * swatchGap;
        }
    }

    private IEnumerator FadeIn()
    {
        if (group == null) yield break;

        float time = 0f;
        while (time < fadeInTime)
        {
            time += Time.unscaledDeltaTime;
            group.alpha = Mathf.Clamp01(time / Mathf.Max(0.0001f, fadeInTime));
            yield return null;
        }

        group.alpha = 1f;
    }

    // ---------------------------------------------------------------- helpers

    private RectTransform CreateRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.localScale = Vector3.one;
        return rect;
    }

    private Image CreateImage(string name, Transform parent, Color color)
    {
        Image image = CreateRect(name, parent).gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private TextMeshProUGUI CreateText(string name, Transform parent, float size, Color color, TextAlignmentOptions alignment)
    {
        TextMeshProUGUI text = CreateRect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        text.enableWordWrapping = true;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;
        return text;
    }

    private Button CreateButton(string name, Transform parent, string label)
    {
        RectTransform rect = CreateRect(name, parent);

        Image plate = rect.gameObject.AddComponent<Image>();
        plate.color = accentColor;
        plate.raycastTarget = true;

        Button button = rect.gameObject.AddComponent<Button>();
        StyleButton(button, plate);

        TextMeshProUGUI text = CreateText("Label", rect, 30f, backgroundColor, TextAlignmentOptions.Center);
        text.text = label;
        text.fontStyle = FontStyles.Bold;
        text.characterSpacing = 4f;
        Stretch(text.rectTransform);

        return button;
    }

    /// <summary>
    /// The same tint the rest of the menus use. It barely shows on its own - <see cref="ButtonFocusEffect"/>
    /// is what the player actually feels - but it keeps these buttons behaving like every other one.
    /// </summary>
    private static void StyleButton(Button button, Graphic target)
    {
        ColorBlock colours = button.colors;
        colours.normalColor = Color.white;
        colours.highlightedColor = new Color(0.92f, 0.92f, 0.92f, 1f);
        colours.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
        colours.selectedColor = Color.white;
        colours.fadeDuration = 0.1f;
        button.colors = colours;

        button.targetGraphic = target;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
    }

    private static Color Fade(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }
}
