using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The truck customization screen. The player picks one of the truck's parts on the left and a colour
/// for it on the right, and the truck in the middle turns so the change can be seen from every side.
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

    // ---------------------------------------------------------------- layout

    [Header("Layout (reference resolution is 1920x1080)")]
    public float titleSize = 54f;
    public float labelSize = 26f;
    public float columnWidth = 420f;
    public float partHeight = 48f;
    public float partGap = 10f;
    [Tooltip("Distance from the top of the screen down to the first part button.")]
    public float partTopY = -156f;
    public float swatchSize = 92f;
    public float swatchGap = 14f;
    public int paletteColumns = 4;
    public float paletteRightMargin = 60f;
    [Tooltip("Vertical offset of the middle of the palette from the middle of the screen.")]
    public float paletteOffsetY = -40f;

    // ---------------------------------------------------------------- preview

    [Header("Preview")]
    [Tooltip("Degrees per second the truck turns. 0 keeps it still.")]
    public float spinSpeed = 28f;
    [Tooltip("How much of the screen's height the truck should span.")]
    public float previewHeight = 0.72f;
    [Tooltip("How much of the screen's width the truck may span when it turns broadside. Keep this " +
             "inside the gap the parts list and the palette leave between them.")]
    public float previewWidth = 0.46f;
    [Tooltip("Where the truck sits on screen, as a fraction of the viewport. It is off centre because " +
             "the parts list and the palette take the two sides.")]
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
    }

    private sealed class Swatch
    {
        public int paletteIndex;                 // -1 for the Default swatch
        public Image activeBar;
    }

    private readonly List<PartRow> partRows = new List<PartRow>();
    private readonly List<Swatch> swatches = new List<Swatch>();

    private Camera previewCamera;
    private Transform previewRoot;
    private Transform previewPivot;
    private TruckPaintApplier applier;
    private CanvasGroup group;
    private TextMeshProUGUI colourLabel;

    private Bounds previewBounds;
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
        if (previewPivot == null || Mathf.Approximately(spinSpeed, 0f)) return;

        // Turned about the world's up axis, so the truck spins on the spot whatever rotation this object
        // happens to have - and on unscaled time, so a leftover pause (timeScale 0) cannot freeze it.
        previewPivot.Rotate(0f, spinSpeed * Time.unscaledDeltaTime, 0f, Space.World);
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
    /// A failure here costs the preview and nothing else: the parts list, the palette, RESET and BACK are
    /// all built afterwards and all work without a truck in the middle of the screen.
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
        // the left of the screen and the palette down the right, so the truck is nudged to sit between
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
        BuildPalette(root);
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

        // Hint line, top-right, kept out of the way of the truck and the palette.
        TextMeshProUGUI hint = CreateText("Hint", root, 26f, Fade(labelColor, 0.65f), TextAlignmentOptions.TopRight);
        hint.text = hintText;

        RectTransform hintRect = hint.rectTransform;
        hintRect.anchorMin = new Vector2(1f, 1f);
        hintRect.anchorMax = new Vector2(1f, 1f);
        hintRect.pivot = new Vector2(1f, 1f);
        hintRect.sizeDelta = new Vector2(700f, 40f);
        hintRect.anchoredPosition = new Vector2(-60f, -56f);
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

        return new PartRow { part = part, label = label, activeBar = bar };
    }

    private void BuildPalette(Transform root)
    {
        int columns = Mathf.Clamp(paletteColumns, 1, TruckPaint.PaletteCount + 1);
        int total = TruckPaint.PaletteCount + 1;                 // the colours, plus Default
        int rows = Mathf.CeilToInt(total / (float)columns);

        float gridWidth = columns * swatchSize + (columns - 1) * swatchGap;
        float gridHeight = rows * swatchSize + (rows - 1) * swatchGap;
        float halfGridHeight = gridHeight * 0.5f;

        // Which part, and what colour it currently has.
        colourLabel = CreateText("Colour", root, 34f, labelColor, TextAlignmentOptions.BottomRight);
        colourLabel.fontStyle = FontStyles.Bold;
        colourLabel.characterSpacing = 3f;

        RectTransform colourRect = colourLabel.rectTransform;
        colourRect.anchorMin = new Vector2(1f, 0.5f);
        colourRect.anchorMax = new Vector2(1f, 0.5f);
        colourRect.pivot = new Vector2(1f, 0f);
        colourRect.sizeDelta = new Vector2(gridWidth + 300f, 46f);
        colourRect.anchoredPosition = new Vector2(-paletteRightMargin, paletteOffsetY + halfGridHeight + 26f);

        RectTransform grid = CreateRect("Palette", root);
        grid.anchorMin = new Vector2(1f, 0.5f);
        grid.anchorMax = new Vector2(1f, 0.5f);
        grid.pivot = new Vector2(1f, 0.5f);
        grid.sizeDelta = new Vector2(gridWidth, gridHeight);
        grid.anchoredPosition = new Vector2(-paletteRightMargin, paletteOffsetY);

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

            swatches.Add(CreateSwatch(cell, isDefault ? TruckPaint.ChoiceNone : i, colour, isDefault));
        }

        TextMeshProUGUI note = CreateText("Note", root, 24f, Fade(labelColor, 0.5f), TextAlignmentOptions.TopRight);
        note.text = noteText;

        RectTransform noteRect = note.rectTransform;
        noteRect.anchorMin = new Vector2(1f, 0.5f);
        noteRect.anchorMax = new Vector2(1f, 0.5f);
        noteRect.pivot = new Vector2(1f, 1f);
        noteRect.sizeDelta = new Vector2(gridWidth + 300f, 34f);
        noteRect.anchoredPosition = new Vector2(-paletteRightMargin, paletteOffsetY - halfGridHeight - 26f);
    }

    private Swatch CreateSwatch(RectTransform cell, int paletteIndex, Color colour, bool isDefault)
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

        button.onClick.AddListener(() => SelectColour(paletteIndex));

        return new Swatch { paletteIndex = paletteIndex, activeBar = bar };
    }

    private void BuildActions(Transform root)
    {
        Button reset = CreateButton("Reset", root, "RESET");
        RectTransform resetRect = reset.GetComponent<RectTransform>();
        resetRect.anchorMin = new Vector2(0f, 0f);
        resetRect.anchorMax = new Vector2(0f, 0f);
        resetRect.pivot = new Vector2(0f, 0f);
        resetRect.sizeDelta = new Vector2(200f, 54f);
        resetRect.anchoredPosition = new Vector2(60f, 46f);
        reset.onClick.AddListener(ResetPaint);

        Button back = CreateButton("Back", root, "BACK");
        RectTransform backRect = back.GetComponent<RectTransform>();
        backRect.anchorMin = new Vector2(0f, 0f);
        backRect.anchorMax = new Vector2(0f, 0f);
        backRect.pivot = new Vector2(0f, 0f);
        backRect.sizeDelta = new Vector2(200f, 54f);
        backRect.anchoredPosition = new Vector2(276f, 46f);
        back.onClick.AddListener(GoBack);
    }

    // ---------------------------------------------------------------- interaction

    private void SelectPart(TruckPart part)
    {
        selectedPart = part;
        Refresh();
    }

    /// <summary><paramref name="paletteIndex"/> is an index into <see cref="TruckPaint.Palette"/>, or -1 for Default.</summary>
    private void SelectColour(int paletteIndex)
    {
        TruckPaint.SetChoice(selectedPart, paletteIndex);
        Refresh();
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
        SceneLoader.Load(levelsSceneName);
    }

    /// <summary>Redraws every piece of state from <see cref="TruckPaint"/>, which is the one source of truth.</summary>
    private void Refresh()
    {
        int choice = TruckPaint.GetChoice(selectedPart);

        for (int i = 0; i < partRows.Count; i++)
        {
            PartRow row = partRows[i];
            bool active = row.part == selectedPart;

            row.activeBar.enabled = active;
            row.label.color = active ? accentColor : labelColor;
        }

        for (int i = 0; i < swatches.Count; i++)
            swatches[i].activeBar.enabled = swatches[i].paletteIndex == choice;

        if (colourLabel != null)
            colourLabel.text = TruckPaint.PartLabels[(int)selectedPart].ToUpperInvariant() + "   " + NameOf(choice);

        // The preview repaints itself from the same saved choice, so it never disagrees with the UI.
        if (applier != null) applier.Apply();
    }

    private static string NameOf(int paletteIndex)
    {
        if (paletteIndex < 0 || paletteIndex >= TruckPaint.PaletteCount) return "DEFAULT";
        return TruckPaint.PaletteNames[paletteIndex].ToUpperInvariant();
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
