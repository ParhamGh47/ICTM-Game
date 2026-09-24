using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The tips scene: a static, non-interactive list of hints for the player, one row per entry.
///
/// Like <see cref="CreditsScreen"/> and <see cref="LoadingScreen"/> the whole UI is built in code, so the
/// scene file only holds a camera, an EventSystem and this component. The look reuses the loading
/// screen's palette (dark navy background, orange accent, light labels).
///
/// Add or reword entries in the <see cref="tips"/> array - they are numbered automatically.
/// Escape, the gamepad's B button, or a click on BACK returns to the main menu. The scene's
/// <see cref="MenuNavigation"/> gives BACK the keyboard and pad highlight, so the one button here can be
/// reached without a mouse.
/// </summary>
[DisallowMultipleComponent]
public class TipsScreen : MonoBehaviour
{
    // ---------------------------------------------------------------- content

    private static readonly string[] DefaultTips =
    {
        "Lorem ipsum dolor sit amet, consectetur adipiscing elit.",
        "Sed do eiusmod tempor incididunt ut labore et dolore magna aliqua.",
        "Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris.",
        "Excepteur sint occaecat cupidatat non proident, sunt in culpa qui officia.",
        "Neque porro quisquam est, qui dolorem ipsum quia dolor sit amet.",
    };

    [Header("Content")]
    [Tooltip("Each entry becomes one numbered row. Leave empty to use the built-in placeholder list.")]
    [TextArea(2, 5)]
    public string[] tips = DefaultTips;

    [Header("Art (optional)")]
    [Tooltip("Falls back to the project's default TMP font when empty.")]
    public TMP_FontAsset font;

    // ---------------------------------------------------------------- look

    [Header("Colours (the loading screen palette)")]
    public Color backgroundColor = new Color(0.012f, 0.024f, 0.055f, 1f);
    public Color accentColor = new Color(1f, 0.64f, 0.16f, 1f);
    public Color labelColor = new Color(0.86f, 0.89f, 0.95f, 1f);

    [Header("Layout (reference resolution is 1920x1080)")]
    public float contentWidth = 1180f;
    public float titleSize = 76f;
    public float bodySize = 34f;
    public float rowGap = 42f;
    [Tooltip("Distance from the top of the screen down to the first tip.")]
    public float listTopY = -250f;

    // ---------------------------------------------------------------- transition

    [Header("Transition")]
    public string menuSceneName = "Menu";
    public string titleText = "TIPS";
    public string hintText = "ESC  TO  GO  BACK";
    public float fadeTime = 0.45f;

    // ---------------------------------------------------------------- state

    private CanvasGroup group;
    private bool leaving;

    private void Start()
    {
        Build();
        StartCoroutine(FadeGroup(1f, fadeTime));
    }

    private void Update()
    {
        // Escape on the keyboard, B / circle on a gamepad - the project's "Cancel" axis carries both - so a
        // pad has the same way out as the keys, and it leaves through the fade rather than cutting the
        // scene the way EscBack does elsewhere.
        if (leaving) return;
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetButtonDown("Cancel")) Leave();
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
        float startAlpha = group != null ? group.alpha : 1f;
        float time = 0f;

        while (time < fadeTime)
        {
            time += Time.unscaledDeltaTime;
            if (group != null)
                group.alpha = Mathf.Lerp(startAlpha, 0f, Mathf.Clamp01(time / Mathf.Max(0.0001f, fadeTime)));
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
        GameObject canvasGo = new GameObject("Tips Canvas",
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

        Transform root = canvasGo.transform;

        Image background = CreateImage("Background", root, backgroundColor);
        Stretch(background.rectTransform);

        // Title.
        TextMeshProUGUI title = CreateText("Title", root, titleSize, accentColor, TextAlignmentOptions.Center);
        title.fontStyle = FontStyles.Bold;
        title.text = string.IsNullOrEmpty(titleText) ? "TIPS" : titleText;
        title.characterSpacing = 6f;

        RectTransform titleRect = title.rectTransform;
        titleRect.anchorMin = new Vector2(0.5f, 1f);
        titleRect.anchorMax = new Vector2(0.5f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.sizeDelta = new Vector2(contentWidth, titleSize * 1.4f);
        titleRect.anchoredPosition = new Vector2(0f, -96f);

        // Thin accent rule under the title, the same orange as the loading bar's fill.
        Image rule = CreateImage("Rule", root, new Color(accentColor.r, accentColor.g, accentColor.b, 0.5f));
        RectTransform ruleRect = rule.rectTransform;
        ruleRect.anchorMin = new Vector2(0.5f, 1f);
        ruleRect.anchorMax = new Vector2(0.5f, 1f);
        ruleRect.pivot = new Vector2(0.5f, 1f);
        ruleRect.sizeDelta = new Vector2(240f, 4f);
        ruleRect.anchoredPosition = new Vector2(0f, -196f);

        // The list itself. Rows are stacked below each other inside this centred container.
        string[] items = (tips == null || tips.Length == 0) ? DefaultTips : tips;
        string numberHex = ColorUtility.ToHtmlStringRGB(accentColor);

        RectTransform list = CreateRect("List", root);
        list.anchorMin = new Vector2(0.5f, 1f);
        list.anchorMax = new Vector2(0.5f, 1f);
        list.pivot = new Vector2(0.5f, 1f);

        float cursor = 0f;
        for (int i = 0; i < items.Length; i++)
        {
            string entry = string.IsNullOrEmpty(items[i]) ? "-" : items[i].Trim();
            string line = "<color=#" + numberHex + "><b>" + (i + 1) + ".</b></color>   " + entry;

            TextMeshProUGUI row = CreateText("Tip " + (i + 1), list, bodySize, labelColor, TextAlignmentOptions.TopLeft);
            row.text = line;
            row.lineSpacing = 8f;

            float height = Mathf.Max(bodySize * 1.6f, row.GetPreferredValues(line, contentWidth, 0f).y);

            RectTransform rowRect = row.rectTransform;
            rowRect.anchorMin = new Vector2(0.5f, 1f);
            rowRect.anchorMax = new Vector2(0.5f, 1f);
            rowRect.pivot = new Vector2(0.5f, 1f);
            rowRect.sizeDelta = new Vector2(contentWidth, height);
            rowRect.anchoredPosition = new Vector2(0f, cursor);

            cursor -= height + rowGap;
        }

        float listHeight = Mathf.Abs(cursor) - rowGap;
        if (listHeight < 0f) listHeight = 0f;

        list.sizeDelta = new Vector2(contentWidth, listHeight);

        // The list starts a fixed distance below the title and grows downwards from there. It used to be
        // centred on screen, which pushed its top edge back up into the title as soon as a tip wrapped
        // onto a second line - anchoring from the top makes the gap independent of the content.
        list.anchoredPosition = new Vector2(0f, listTopY);

        // Bottom-right BACK button, kept simple: a flat accent plate with dark text.
        Button back = CreateButton("Back", root, "BACK");
        RectTransform backRect = back.GetComponent<RectTransform>();
        backRect.anchorMin = new Vector2(1f, 0f);
        backRect.anchorMax = new Vector2(1f, 0f);
        backRect.pivot = new Vector2(1f, 0f);
        backRect.sizeDelta = new Vector2(220f, 74f);
        backRect.anchoredPosition = new Vector2(-60f, 52f);
        back.onClick.AddListener(Leave);

        // Hint line, bottom-left.
        TextMeshProUGUI hint = CreateText("Hint", root, 26, new Color(labelColor.r, labelColor.g, labelColor.b, 0.65f), TextAlignmentOptions.Left);
        hint.text = hintText;

        RectTransform hintRect = hint.rectTransform;
        hintRect.anchorMin = new Vector2(0f, 0f);
        hintRect.anchorMax = new Vector2(0f, 0f);
        hintRect.pivot = new Vector2(0f, 0f);
        hintRect.sizeDelta = new Vector2(900f, 40f);
        hintRect.anchoredPosition = new Vector2(60f, 70f);
    }

    // ---------------------------------------------------------------- helpers

    private Button CreateButton(string name, Transform parent, string label)
    {
        RectTransform rect = CreateRect(name, parent);

        Image plate = rect.gameObject.AddComponent<Image>();
        plate.color = accentColor;
        plate.raycastTarget = true;

        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = plate;

        ColorBlock colours = button.colors;
        colours.normalColor = Color.white;
        colours.highlightedColor = new Color(0.92f, 0.92f, 0.92f, 1f);
        colours.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
        colours.selectedColor = Color.white;
        colours.fadeDuration = 0.1f;
        button.colors = colours;

        TextMeshProUGUI text = CreateText("Label", rect, 30f, backgroundColor, TextAlignmentOptions.Center);
        text.text = label;
        text.fontStyle = FontStyles.Bold;
        text.characterSpacing = 4f;
        Stretch(text.rectTransform);

        return button;
    }

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

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
    }
}
