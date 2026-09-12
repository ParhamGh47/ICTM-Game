using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Full-screen loading overlay shown during every scene transition (see <see cref="SceneLoader"/>).
///
/// The whole UI is built in code, so it needs no prefab, no scene setup and works in every level -
/// including ones added later. It is created once, kept alive with <c>DontDestroyOnLoad</c> and reused
/// for the rest of the session.
///
/// Art is optional and looked up in <c>Assets/Resources/Loading/</c>:
///   Logo.png      - the centred logo / game icon (copied from the project's splash art).
///   Backdrop.png  - optional full-screen background art, drawn dimmed behind everything.
/// If Logo.png is missing the overlay falls back to the product name as a title card.
/// </summary>
[DisallowMultipleComponent]
public class LoadingScreen : MonoBehaviour
{
    // ---------------------------------------------------------------- look

    [Header("Colours")]
    public Color backgroundColor = new Color(0.012f, 0.024f, 0.055f, 1f);
    public Color accentColor = new Color(1f, 0.64f, 0.16f, 1f);
    public Color labelColor = new Color(0.86f, 0.89f, 0.95f, 1f);

    [Header("Layout (reference resolution is 1920x1080)")]
    public float logoHeight = 300f;
    public float logoY = 110f;
    public float barWidth = 620f;
    public float barHeight = 10f;
    public float barY = -70f;

    [Header("Timing (unscaled seconds)")]
    public bool showOnStartup = true;
    public float fadeOutDuration = 0.25f;
    public float minimumDisplayTime = 0.70f;
    public float startupDisplayTime = 1.10f;

    [Header("Text")]
    public string statusText = "LOADING";

    // ---------------------------------------------------------------- state

    /// <summary>The single persistent instance, or null before <see cref="Ensure"/> has been called.</summary>
    public static LoadingScreen Instance { get; private set; }

    public static bool IsShowing => Instance != null && Instance.holds > 0;

    private Canvas canvas;
    private CanvasGroup group;
    private RectTransform pulseTarget;
    private Image barFill;
    private TextMeshProUGUI status;
    private float barFillWidth;
    private float displayedProgress;
    private float targetProgress;
    private int lastPercent = -1;
    private int holds;
    private Coroutine fade;

    // ---------------------------------------------------------------- bootstrap

    /// <summary>
    /// Creates the overlay as soon as the game starts and shows it briefly, so the game has a proper
    /// boot logo instead of jumping straight into the first scene.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (!Application.isPlaying) return;

        LoadingScreen screen = Ensure();

        if (screen.showOnStartup && screen.startupDisplayTime > 0f)
            screen.StartCoroutine(screen.StartupRoutine());
        else
            screen.StartCoroutine(screen.WarmUpRoutine());
    }

    /// <summary>
    /// Draws one invisible frame at startup, so the first real transition does not have to pay for
    /// first-time canvas, UI shader and font atlas work on top of the scene load.
    /// </summary>
    private IEnumerator WarmUpRoutine()
    {
        canvas.enabled = true;
        yield return null;

        if (holds == 0) canvas.enabled = false;
    }

    /// <summary>Returns the persistent overlay, creating it on first use.</summary>
    public static LoadingScreen Ensure()
    {
        if (Instance != null) return Instance;

        GameObject go = new GameObject("Loading Screen");
        return go.AddComponent<LoadingScreen>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        Build();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private IEnumerator StartupRoutine()
    {
        Show();
        SetProgress(1f, true);

        float elapsed = 0f;
        while (elapsed < startupDisplayTime && holds == 1)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        Release();
    }

    // ---------------------------------------------------------------- public API

    /// <summary>Shows the overlay. Every call must be matched by <see cref="Release"/>.</summary>
    public void Show()
    {
        holds++;
        if (canvas == null) Build();

        // Cancel any fade that is still running (usually the previous fade out), so it cannot keep
        // lowering the alpha behind our back.
        if (fade != null)
        {
            StopCoroutine(fade);
            fade = null;
        }

        // Snap fully visible instead of fading in: a fade only advances as frames are rendered, and
        // the frame that starts a load can stall for seconds (Core-1 is an 8 MB scene), which would
        // leave the player staring at a frozen scene instead of at the loading screen.
        canvas.enabled = true;
        group.alpha = 1f;
        group.blocksRaycasts = true;
    }

    /// <summary>Lets go of one hold; once no holds remain the overlay fades out and stops drawing.</summary>
    public void Release()
    {
        holds = Mathf.Max(0, holds - 1);
        if (holds == 0) FadeTo(0f, fadeOutDuration);
    }

    /// <summary>Sets the bar's target fill (0..1). The bar eases towards it, so the motion stays smooth.</summary>
    public void SetProgress(float value, bool instant = false)
    {
        targetProgress = Mathf.Clamp01(value);
        if (instant) displayedProgress = targetProgress;
    }

    /// <summary>Overrides the status line, e.g. "LOADING LEVEL 2".</summary>
    public void SetStatusText(string text)
    {
        statusText = string.IsNullOrEmpty(text) ? "LOADING" : text;
        lastPercent = -1;
    }

    // ---------------------------------------------------------------- UI construction

    private void Build()
    {
        GameObject canvasGo = new GameObject("Canvas",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        canvasGo.transform.SetParent(transform, false);

        canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30000;               // always above gameplay HUDs and menus

        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        group = canvasGo.GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = true;

        canvas.enabled = false;

        Transform root = canvasGo.transform;

        // Solid background plate (also swallows clicks meant for the scene underneath).
        Image background = CreateImage("Background", root, backgroundColor);
        Stretch(background.rectTransform);
        background.raycastTarget = true;

        // Optional full-screen art, drawn dimmed behind everything.
        Sprite backdropSprite = Resources.Load<Sprite>("Loading/Backdrop");
        if (backdropSprite != null)
        {
            Image backdrop = CreateImage("Backdrop", root, new Color(1f, 1f, 1f, 0.22f));
            backdrop.sprite = backdropSprite;
            backdrop.preserveAspect = true;
            Stretch(backdrop.rectTransform);
        }

        // Centred logo (or the product name when no art is available).
        Sprite logoSprite = Resources.Load<Sprite>("Loading/Logo");
        if (logoSprite != null)
        {
            Image logo = CreateImage("Logo", root, Color.white);
            logo.sprite = logoSprite;
            logo.preserveAspect = true;
            RectTransform rt = logo.rectTransform;
            rt.sizeDelta = new Vector2(logoHeight * 4f, logoHeight);   // preserveAspect fits the art inside
            rt.anchoredPosition = new Vector2(0f, logoY);
            pulseTarget = rt;
        }
        else
        {
            TextMeshProUGUI title = CreateText("Title", root, 64, labelColor, TextAlignmentOptions.Center);
            title.fontStyle = FontStyles.Bold;
            title.text = Application.productName;
            title.rectTransform.sizeDelta = new Vector2(1400f, 90f);
            title.rectTransform.anchoredPosition = new Vector2(0f, logoY);
            pulseTarget = title.rectTransform;
        }

        // Progress bar.
        RectTransform barRoot = CreateRect("Progress Bar", root);
        barRoot.sizeDelta = new Vector2(barWidth, barHeight);
        barRoot.anchoredPosition = new Vector2(0f, barY);

        Image barBackground = CreateImage("Track", barRoot, new Color(1f, 1f, 1f, 0.14f));
        Stretch(barBackground.rectTransform);

        barFill = CreateImage("Fill", barRoot, accentColor);
        RectTransform fillRect = barFill.rectTransform;
        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(0f, 1f);
        fillRect.pivot = new Vector2(0f, 0.5f);
        fillRect.anchoredPosition = Vector2.zero;
        fillRect.sizeDelta = new Vector2(0f, 0f);
        barFillWidth = barWidth;

        // Status line ("LOADING  42%").
        status = CreateText("Status", root, 28, labelColor, TextAlignmentOptions.Center);
        status.rectTransform.sizeDelta = new Vector2(1200f, 40f);
        status.rectTransform.anchoredPosition = new Vector2(0f, barY - 48f);
        RefreshStatus();

        SetProgress(0f, true);
    }

    private void Update()
    {
        if (canvas == null || !canvas.enabled) return;

        float deltaTime = Time.unscaledDeltaTime;

        // Ease towards the reported progress. Fast enough to settle well inside the minimum
        // on-screen time, slow enough that the bar sweeps instead of snapping between steps.
        displayedProgress = Mathf.MoveTowards(displayedProgress, targetProgress, 2.2f * deltaTime);

        if (barFill != null)
            barFill.rectTransform.sizeDelta = new Vector2(barFillWidth * displayedProgress, 0f);

        // Gentle breathing on the logo, so a stuck-at-90% load still feels alive.
        if (pulseTarget != null)
        {
            float pulse = 1f + 0.014f * Mathf.Sin(Time.unscaledTime * 1.7f);
            pulseTarget.localScale = new Vector3(pulse, pulse, 1f);
        }

        RefreshStatus();
    }

    private void RefreshStatus()
    {
        if (status == null) return;

        int percent = Mathf.RoundToInt(displayedProgress * 100f);
        if (percent == lastPercent && !string.IsNullOrEmpty(status.text)) return;

        lastPercent = percent;
        status.text = string.Format("{0}   {1}%", statusText, percent);
    }

    // ---------------------------------------------------------------- fading

    private void FadeTo(float targetAlpha, float duration)
    {
        if (fade != null) StopCoroutine(fade);
        fade = StartCoroutine(FadeRoutine(targetAlpha, duration));
    }

    private IEnumerator FadeRoutine(float targetAlpha, float duration)
    {
        if (group == null) yield break;

        float startAlpha = group.alpha;

        if (duration <= 0.0001f || Mathf.Approximately(startAlpha, targetAlpha))
        {
            group.alpha = targetAlpha;
        }
        else
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                group.alpha = Mathf.Lerp(startAlpha, targetAlpha, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }
            group.alpha = targetAlpha;
        }

        group.blocksRaycasts = targetAlpha > 0.001f;
        if (targetAlpha <= 0.001f && canvas != null) canvas.enabled = false;

        fade = null;
    }

    // ---------------------------------------------------------------- helpers

    private static RectTransform CreateRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        return rt;
    }

    private static Image CreateImage(string name, Transform parent, Color color)
    {
        Image image = CreateRect(name, parent).gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    // TextMeshPro rather than legacy Text: TMP falls back to the project's default font asset on its
    // own, so there is no font to resolve. The legacy path needed the built-in font, whose resource name
    // changed between Unity versions - looking up the wrong one logs an error that cannot be caught.
    private static TextMeshProUGUI CreateText(string name, Transform parent, int size, Color color, TextAlignmentOptions alignment)
    {
        TextMeshProUGUI text = CreateRect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Overflow;
        return text;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
    }
}
