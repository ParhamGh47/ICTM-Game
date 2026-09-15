using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The credits scene. Builds its whole UI in code (same approach as <see cref="LoadingScreen"/>), so the
/// scene file only needs a camera, an EventSystem and this component - nothing else.
///
/// Look follows the loading screen: the same dark navy background, the same orange accent and the same
/// light label colour.
///
/// The roll starts below the screen and scrolls upward like a normal movie credits page. When it has
/// passed the top the scene waits a moment and returns to the main menu by itself. Escape or a left
/// click skips straight there.
///
/// Everything is a public field, so the text, the song, the speed and the colours can all be changed in
/// the Inspector without touching this file (or any scene).
/// </summary>
[DisallowMultipleComponent]
public class CreditsScreen : MonoBehaviour
{
    // ---------------------------------------------------------------- content

    private const string DefaultCredits =
        "Lorem ipsum dolor sit amet, consectetur adipiscing elit.\n\n" +
        "Sed do eiusmod tempor incididunt ut labore et dolore magna aliqua.\n\n" +
        "Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat.\n\n" +
        "Duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur.\n\n" +
        "Excepteur sint occaecat cupidatat non proident, sunt in culpa qui officia deserunt mollit anim id est laborum.\n\n" +
        "Sed ut perspiciatis unde omnis iste natus error sit voluptatem accusantium doloremque laudantium, totam rem aperiam.\n\n" +
        "Eaque ipsa quae ab illo inventore veritatis et quasi architecto beatae vitae dicta sunt explicabo.\n\n" +
        "Nemo enim ipsam voluptatem quia voluptas sit aspernatur aut odit aut fugit.\n\n" +
        "Neque porro quisquam est, qui dolorem ipsum quia dolor sit amet, consectetur, adipisci velit.\n\n" +
        "Ut enim ad minima veniam, quis nostrum exercitationem ullam corporis suscipit laboriosam.\n\n" +
        "Quis autem vel eum iure reprehenderit qui in ea voluptate velit esse quam nihil molestiae consequatur.\n\n" +
        "At vero eos et accusamus et iusto odio dignissimos ducimus qui blanditiis praesentium voluptatum.\n\n" +
        "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore.\n\n" +
        "Thanks for playing.";

    [Header("Content")]
    [TextArea(10, 60)]
    [Tooltip("Leave empty to use the built-in placeholder text.")]
    public string creditsText = DefaultCredits;

    [Header("Art (optional)")]
    [Tooltip("Falls back to the project's default TMP font when empty.")]
    public TMP_FontAsset font;
    [Tooltip("Falls back to Resources/Loading/Logo when empty.")]
    public Sprite logo;

    // ---------------------------------------------------------------- look

    [Header("Colours (the loading screen palette)")]
    public Color backgroundColor = new Color(0.012f, 0.024f, 0.055f, 1f);
    public Color accentColor = new Color(1f, 0.64f, 0.16f, 1f);
    public Color labelColor = new Color(0.86f, 0.89f, 0.95f, 1f);

    [Header("Layout (reference resolution is 1920x1080)")]
    public float logoHeight = 150f;
    public float contentWidth = 1200f;
    public float titleSize = 76f;
    public float bodySize = 34f;

    // ---------------------------------------------------------------- scroll

    [Header("Scroll")]
    [Tooltip("Reference pixels per second. Raise to scroll faster.")]
    public float scrollSpeed = 120f;
    [Tooltip("Seconds of stillness before the roll starts moving.")]
    public float startDelay = 1.2f;
    [Tooltip("Seconds to hold on the empty screen after the roll has passed.")]
    public float endHoldTime = 2.5f;

    // ---------------------------------------------------------------- audio

    [Header("Audio")]
    public AudioClip music;
    public bool loopMusic = true;
    [Range(0f, 1f)] public float musicVolume = 0.7f;

    // ---------------------------------------------------------------- transition

    [Header("Transition")]
    public string menuSceneName = "Menu";
    public string skipHint = "CLICK  OR  PRESS ESC TO SKIP";
    [Tooltip("The accent colour by default, so the hint reads as a control rather than as part of the roll.")]
    public Color hintColor = new Color(1f, 0.64f, 0.16f, 0.85f);
    public float fadeTime = 0.5f;

    // ---------------------------------------------------------------- state

    private RectTransform canvasRect;
    private RectTransform roll;
    private CanvasGroup group;
    private AudioSource source;

    private float rollHeight;
    private float startY;
    private float travel;
    private float scrollY;
    private float elapsed;
    private float holdTimer;
    private bool leaving;

    // ---------------------------------------------------------------- bootstrap

    private void Start()
    {
        // Background, title and hint go up right away so there is never a bare black frame.
        Build();

        if (music != null)
        {
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.clip = music;
            source.loop = loopMusic;
            source.volume = musicVolume;
            source.spatialBlend = 0f;      // 2D: the credits song is not positional
            source.Play();
        }

        StartCoroutine(RevealRoutine());
    }

    /// <summary>
    /// A screen-space canvas only reports its real rect once it has been through one update, and the
    /// roll's start and end positions are measured from that rect - so the roll is laid out a frame
    /// later than the background.
    /// </summary>
    private IEnumerator RevealRoutine()
    {
        yield return null;

        BuildRoll();

        yield return FadeGroup(1f, fadeTime);
    }

    private void Update()
    {
        if (leaving || roll == null) return;

        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(0))
        {
            Leave();
            return;
        }

        float deltaTime = Time.unscaledDeltaTime;

        if (elapsed < startDelay)
        {
            elapsed += deltaTime;
            return;
        }

        if (scrollY < travel)
        {
            // Linear, constant speed: the roll covers the same distance every second whatever the length.
            scrollY = Mathf.Min(travel, scrollY + scrollSpeed * deltaTime);
            roll.anchoredPosition = new Vector2(0f, startY + scrollY);
            return;
        }

        holdTimer += deltaTime;
        if (holdTimer >= endHoldTime) Leave();
    }

    // ---------------------------------------------------------------- leaving

    private void Leave()
    {
        if (leaving) return;
        leaving = true;
        StartCoroutine(LeaveRoutine());
    }

    private IEnumerator LeaveRoutine()
    {
        float startVolume = source != null ? source.volume : 0f;
        float startAlpha = group != null ? group.alpha : 1f;

        float time = 0f;
        while (time < fadeTime)
        {
            time += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(time / Mathf.Max(0.0001f, fadeTime));

            if (group != null) group.alpha = Mathf.Lerp(startAlpha, 0f, t);
            if (source != null) source.volume = Mathf.Lerp(startVolume, 0f, t);

            yield return null;
        }

        SceneLoader.Load(menuSceneName);
    }

    private IEnumerator FadeGroup(float target, float duration)
    {
        if (group == null) yield break;

        float startAlpha = group.alpha;
        float time = 0f;

        while (time < duration)
        {
            time += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(startAlpha, target, Mathf.Clamp01(time / Mathf.Max(0.0001f, duration)));
            yield return null;
        }

        group.alpha = target;
    }

    // ---------------------------------------------------------------- UI construction

    private void Build()
    {
        GameObject canvasGo = new GameObject("Credits Canvas",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        canvasGo.transform.SetParent(transform, false);

        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        group = canvasGo.GetComponent<CanvasGroup>();
        group.alpha = 0f;

        canvasRect = canvasGo.GetComponent<RectTransform>();
        Transform root = canvasGo.transform;
        roll = null;

        // Solid background plate - the loading screen's near-black navy.
        Image background = CreateImage("Background", root, backgroundColor);
        Stretch(background.rectTransform);

        // Fixed hint line, pinned to the bottom-left corner in the accent colour.
        TextMeshProUGUI hint = CreateText("Hint", root, 26, hintColor, TextAlignmentOptions.Left);
        hint.text = skipHint;

        RectTransform hintRect = hint.rectTransform;
        hintRect.anchorMin = new Vector2(0f, 0f);
        hintRect.anchorMax = new Vector2(0f, 0f);
        hintRect.pivot = new Vector2(0f, 0f);
        hintRect.sizeDelta = new Vector2(1200f, 40f);
        hintRect.anchoredPosition = new Vector2(60f, 50f);
    }

    /// <summary>Lays out the scrolling roll once the canvas has a real size.</summary>
    private void BuildRoll()
    {
        Canvas.ForceUpdateCanvases();

        Transform root = canvasRect;

        // The rolling content. Everything inside is centred, so the roll only ever moves vertically.
        roll = CreateRect("Roll", root);
        roll.anchorMin = new Vector2(0.5f, 0.5f);
        roll.anchorMax = new Vector2(0.5f, 0.5f);
        roll.pivot = new Vector2(0.5f, 0f);
        roll.SetSiblingIndex(1);      // behind the background's fixed siblings, so the hint stays readable

        float canvasHeight = canvasRect.rect.height;
        if (canvasHeight <= 1f) canvasHeight = 1080f;   // scaler reference height, if the rect is not ready yet
        float top = 0f;

        // Optional centred logo, the same art the loading screen shows.
        Sprite logoArt = logo != null ? logo : Resources.Load<Sprite>("Loading/Logo");
        if (logoArt != null)
        {
            Image logoImage = CreateImage("Logo", roll, Color.white);
            logoImage.sprite = logoArt;
            logoImage.preserveAspect = true;

            RectTransform logoRect = logoImage.rectTransform;
            logoRect.anchorMin = new Vector2(0.5f, 1f);
            logoRect.anchorMax = new Vector2(0.5f, 1f);
            logoRect.pivot = new Vector2(0.5f, 1f);
            logoRect.anchoredPosition = Vector2.zero;
            logoRect.sizeDelta = new Vector2(logoHeight * 4f, logoHeight);

            top -= logoHeight + 70f;
        }

        // Title.
        TextMeshProUGUI title = CreateText("Title", roll, titleSize, accentColor, TextAlignmentOptions.Center);
        title.fontStyle = FontStyles.Bold;
        title.text = "CREDITS";
        title.characterSpacing = 6f;
        float titleHeight = Mathf.Max(titleSize * 1.4f, title.GetPreferredValues(title.text, contentWidth, 0f).y);

        RectTransform titleRect = title.rectTransform;
        titleRect.anchorMin = new Vector2(0.5f, 1f);
        titleRect.anchorMax = new Vector2(0.5f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.sizeDelta = new Vector2(contentWidth, titleHeight);
        titleRect.anchoredPosition = new Vector2(0f, top);

        top -= titleHeight + 80f;

        // Body.
        string body = string.IsNullOrEmpty(creditsText) ? DefaultCredits : creditsText;
        TextMeshProUGUI text = CreateText("Body", roll, bodySize, labelColor, TextAlignmentOptions.Top);
        text.text = body;
        text.lineSpacing = 18f;

        float bodyHeight = Mathf.Max(bodySize * 2f, text.GetPreferredValues(body, contentWidth, 0f).y);

        RectTransform bodyRect = text.rectTransform;
        bodyRect.anchorMin = new Vector2(0.5f, 1f);
        bodyRect.anchorMax = new Vector2(0.5f, 1f);
        bodyRect.pivot = new Vector2(0.5f, 1f);
        bodyRect.sizeDelta = new Vector2(contentWidth, bodyHeight);
        bodyRect.anchoredPosition = new Vector2(0f, top);

        rollHeight = Mathf.Abs(top) + bodyHeight;

        RectTransform rollRect = roll;
        rollRect.sizeDelta = new Vector2(contentWidth, rollHeight);

        // Roll up from just below the screen to just above it.
        startY = -(canvasHeight * 0.5f + rollHeight);
        rollRect.anchoredPosition = new Vector2(0f, startY);
        travel = canvasHeight + rollHeight;
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

    // TextMeshPro rather than legacy Text, and the project's own font asset is used when one is set -
    // otherwise TMP falls back to its default font on its own.
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

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
    }
}
