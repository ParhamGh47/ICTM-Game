using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The options screen: what the game's controls are, and the settings that change how it looks - one tab
/// each, over a single page.
///
/// Like <see cref="CreditsScreen"/>, <see cref="TipsScreen"/> and <see cref="CustomizeScreen"/> the whole UI
/// is built in code, so the scene file only holds a camera, an EventSystem and this component. The look is
/// the same family as those screens: the loading screen's palette (dark navy, orange accent, light labels),
/// a title with an accent rule under it, and rows on a faint plate. Nothing is drawn from a sprite, so there
/// is no art to keep in step with the controls it describes.
///
/// The controls table is data (<see cref="controls"/>), not layout: it was assembled from what the game
/// actually reads - <see cref="GameInput"/> - so the screen cannot describe a control the truck does not
/// have, and rewording or adding a row is an Inspector edit rather than a code change. The two binding
/// columns are the keyboard and the pad, and the pad is named the way an Xbox pad is because that is the
/// one every player can picture; a note under the table says the same buttons work on any pad.
///
/// The settings tab holds the game's two choices, each of which is a row of three with the one in force lit
/// up. The graphics settings live in <see cref="GraphicsQuality"/>: the three presets and the two switches
/// that sit on top of them. The difficulties live in <see cref="GameDifficulty"/>: what a level's time limit
/// and its kill requirement become. A choice takes effect and is saved as soon as it is made - only the
/// ground detail of a level waits for that level to load, and a difficulty lands on the next level - so a
/// level that is open when either changes asks to be restarted rather than saying so in advance (see
/// <see cref="PauseOptionsPanel"/>). Nothing here describes what the presets do: the picture is the
/// description, and a list of numbers beside it would be the implementation rather than the choice.
///
/// A gamepad or the keyboard drives all of it through the project's own menu navigation, which finds these
/// buttons on its own; the places where the nearest-neighbour guess is wrong - the tab row, the preset row -
/// are wired explicitly at the end of <see cref="BuildUi"/>. Escape and the pad's B button come from the
/// scene's <see cref="EscBack"/> component, as they do on the customize screen.
/// </summary>
[DisallowMultipleComponent]
public class OptionsScreen : MonoBehaviour
{
    // ---------------------------------------------------------------- content

    [Header("Content")]
    public string titleText = "OPTIONS";
    public string hintText = "ESC  TO  GO  BACK";
    public string controlsTabText = "CONTROLS";
    public string settingsTabText = "SETTINGS";
    public string backText = "BACK";

    [Tooltip("The controls table. Leave empty to use the built-in one.")]
    public ControlGroup[] controls = ControlBindings.All;

    [Tooltip("Small lines under the table, one row each. Leave empty to use the built-in ones.")]
    public string[] notes = ControlBindings.Notes;

    // ---------------------------------------------------------------- the settings tab

    [Header("Settings tab")]
    public string graphicsCaption = "GRAPHICS";
    [Tooltip("One label per preset, in the order GraphicsQuality defines them: Low, Medium, High.")]
    public string[] presetLabels = { "LOW", "MEDIUM", "HIGH" };
    public string shadowsText = "SHADOWS";
    public string motionBlurText = "MOTION BLUR";
    public string onText = "ON";
    public string offText = "OFF";
    public string difficultyCaption = "DIFFICULTY";
    [Tooltip("One label per difficulty, in the order GameDifficulty defines them: Easy, Medium, Hard.")]
    public string[] difficultyLabels = { "EASY", "MEDIUM", "HARD" };
    [Tooltip("The plate under the difficulties. Says what the choice changes, since it cannot be seen.")]
    [TextArea(2, 6)]
    public string difficultyNoteText =
        "How long a level gives you, and how many targets it asks for. It lands on the next level you start.";
    [Tooltip("The plate beside the settings. Says how a choice is taken, not what the presets do.")]
    [TextArea(2, 6)]
    public string settingsNoteText =
        "Your choice is saved and applied straight away.";

    // ---------------------------------------------------------------- look

    [Header("Art (optional)")]
    [Tooltip("Falls back to the project's default TMP font when empty.")]
    public TMP_FontAsset font;

    [Header("Colours (the loading screen palette)")]
    public Color backgroundColor = new Color(0.012f, 0.024f, 0.055f, 1f);
    public Color accentColor = new Color(1f, 0.64f, 0.16f, 1f);
    public Color labelColor = new Color(0.86f, 0.89f, 0.95f, 1f);
    public Color dimLabelColor = new Color(0.66f, 0.71f, 0.80f, 1f);
    public Color panelColor = new Color(0.055f, 0.082f, 0.145f, 1f);
    public Color buttonColor = new Color(0.13f, 0.17f, 0.25f, 1f);

    // ---------------------------------------------------------------- layout

    [Header("Layout (reference resolution is 1920x1080)")]
    public float titleSize = 54f;
    public float captionSize = 22f;
    public float bodySize = 26f;
    public float noteSize = 22f;
    public float sideMargin = 60f;
    public float titleTopY = -44f;
    public float ruleTopY = -124f;

    [Header("Layout - tabs")]
    public float tabTopY = -152f;
    public Vector2 tabSize = new Vector2(250f, 56f);
    public float tabGap = 14f;

    [Header("Layout - the controls table")]
    public float contentTopY = -224f;
    public float headerHeight = 32f;
    public float rowHeight = 44f;
    public float rowGap = 4f;
    public float groupGap = 16f;
    public float actionColumnWidth = 620f;
    public float keyboardColumnX = 700f;
    public float gamepadColumnX = 1280f;
    public float deviceColumnWidth = 560f;

    [Header("Layout - the settings tab")]
    public Vector2 presetButtonSize = new Vector2(230f, 64f);
    public float presetGap = 16f;
    public Vector2 switchSize = new Vector2(420f, 60f);
    public float switchGap = 14f;
    [Tooltip("Where the note plate sits, and how wide it is.")]
    public float noteX = 1120f;
    public float noteWidth = 740f;

    // ---------------------------------------------------------------- transition

    [Header("Transition")]
    [Tooltip("Where BACK goes. Must be in Build Settings.")]
    public string backSceneName = "Menu";
    public float fadeInTime = 0.35f;

    // ---------------------------------------------------------------- state

    private sealed class Tab
    {
        public bool showsControls;
        public TextMeshProUGUI label;
        public Image activeBar;
        public Button button;
    }

    /// <summary>One choice of a row: a preset, or a difficulty. Both rows are built and refreshed alike.</summary>
    private sealed class ChoiceRow
    {
        public int index;
        public Button button;
        public TextMeshProUGUI label;
        public Image activeBar;
    }

    private readonly List<Tab> tabs = new List<Tab>();
    private readonly List<ChoiceRow> presetRows = new List<ChoiceRow>();
    private readonly List<ChoiceRow> difficultyRows = new List<ChoiceRow>();

    private CanvasGroup group;
    private RectTransform controlsPage;
    private RectTransform settingsPage;

    private Button controlsTab;
    private Button settingsTab;
    private Button backButton;
    private Button shadowsButton;
    private Button blurButton;

    private TextMeshProUGUI shadowsLabel;
    private TextMeshProUGUI blurLabel;

    private bool showingControls = true;

    /// <summary>The width the table and the rows use, between the two margins.</summary>
    private float ContentWidth
    {
        get { return 1920f - sideMargin * 2f; }
    }

    // ---------------------------------------------------------------- lifecycle

    private void Awake()
    {
        BuildUi();

        GraphicsQuality.Changed += Refresh;
        GameDifficulty.Changed += Refresh;
    }

    private void Start()
    {
        ShowControls(true);
        Refresh();

        StartCoroutine(FadeIn());
    }

    private void OnDestroy()
    {
        GraphicsQuality.Changed -= Refresh;
        GameDifficulty.Changed -= Refresh;
    }

    // ---------------------------------------------------------------- tabs

    private void ShowControls(bool show)
    {
        showingControls = show;

        if (controlsPage != null) controlsPage.gameObject.SetActive(show);
        if (settingsPage != null) settingsPage.gameObject.SetActive(!show);

        RefreshTabs();
    }

    private void RefreshTabs()
    {
        for (int i = 0; i < tabs.Count; i++)
        {
            bool active = tabs[i].showsControls == showingControls;

            tabs[i].activeBar.enabled = active;
            tabs[i].label.color = active ? accentColor : labelColor;
        }
    }

    // ---------------------------------------------------------------- choices

    private void ChoosePreset(int index)
    {
        GraphicsQuality.Choose((GraphicsPreset)index);
    }

    private void ToggleShadows()
    {
        GraphicsQuality.SetShadows(!GraphicsQuality.Shadows);
    }

    private void ToggleMotionBlur()
    {
        GraphicsQuality.SetMotionBlur(!GraphicsQuality.MotionBlur);
    }

    private void ChooseDifficulty(int index)
    {
        GameDifficulty.Choose((DifficultyLevel)index);
    }

    private void GoBack()
    {
        // No fade of its own: SceneLoader covers menu-to-menu hops with the screen fade, so the transition
        // is handled in one place for every scene.
        SceneLoader.Load(backSceneName);
    }

    /// <summary>
    /// Brings every label back in line with the settings, so the screen always says what is actually in
    /// force - including when it was changed from somewhere else.
    /// </summary>
    private void Refresh()
    {
        for (int i = 0; i < presetRows.Count; i++)
        {
            ChoiceRow row = presetRows[i];
            bool active = (int)GraphicsQuality.Current == row.index;

            row.activeBar.enabled = active;
            row.label.color = active ? accentColor : labelColor;
        }

        for (int i = 0; i < difficultyRows.Count; i++)
        {
            ChoiceRow row = difficultyRows[i];
            bool active = (int)GameDifficulty.Current == row.index;

            row.activeBar.enabled = active;
            row.label.color = active ? accentColor : labelColor;
        }

        if (shadowsLabel != null)
        {
            shadowsLabel.text = shadowsText + "   " + (GraphicsQuality.Shadows ? onText : offText);
            shadowsLabel.color = GraphicsQuality.Shadows ? labelColor : dimLabelColor;
        }

        if (blurLabel != null)
        {
            blurLabel.text = motionBlurText + "   " + (GraphicsQuality.MotionBlur ? onText : offText);
            blurLabel.color = GraphicsQuality.MotionBlur ? labelColor : dimLabelColor;
        }
    }

    // ---------------------------------------------------------------- UI construction

    private void BuildUi()
    {
        GameObject canvasGo = new GameObject("Options Canvas",
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

        Image background = CreateImage("Background", root, backgroundColor);
        Stretch(background.rectTransform);

        BuildHeader(root);
        BuildTabs(root);

        // The two pages. They are full-screen rects stacked on top of each other, and only one is ever
        // switched on - so a button on the hidden tab is not merely invisible, it is not selectable either,
        // which is what keeps the highlight from wandering onto it.
        controlsPage = CreateRect("Controls Page", root);
        Stretch(controlsPage);
        BuildControlsPage(controlsPage);

        settingsPage = CreateRect("Settings Page", root);
        Stretch(settingsPage);
        BuildSettingsPage(settingsPage);

        BuildActions(root);
        WireNavigation();
    }

    private void BuildHeader(Transform root)
    {
        TextMeshProUGUI title = CreateText("Title", root, titleSize, accentColor, TextAlignmentOptions.TopLeft);
        title.fontStyle = FontStyles.Bold;
        title.text = titleText;
        title.characterSpacing = 6f;
        PlaceTopLeft(title.rectTransform, sideMargin, titleTopY, 900f, titleSize * 1.4f);

        Image rule = CreateImage("Rule", root, Fade(accentColor, 0.5f));
        PlaceTopLeft(rule.rectTransform, sideMargin, ruleTopY, 240f, 4f);

        TextMeshProUGUI hint = CreateText("Hint", root, 26f, Fade(labelColor, 0.65f), TextAlignmentOptions.TopRight);
        hint.text = hintText;
        PlaceTopRight(hint.rectTransform, sideMargin, titleTopY - 12f, 700f, 40f);
    }

    private void BuildTabs(Transform root)
    {
        // Named after what they show, because MenuNavigation starts on "Controls" by that name.
        controlsTab = CreateTab(root, "Controls", controlsTabText, 0, true);
        settingsTab = CreateTab(root, "Settings", settingsTabText, 1, false);
    }

    private Button CreateTab(Transform root, string name, string label, int index, bool showsControls)
    {
        Button button = CreateButton(name, root, label, tabSize);
        PlaceTopLeft((RectTransform)button.transform, sideMargin + index * (tabSize.x + tabGap), tabTopY, tabSize.x, tabSize.y);

        TextMeshProUGUI text = button.GetComponentInChildren<TextMeshProUGUI>();
        text.fontStyle = FontStyles.Bold;
        text.characterSpacing = 4f;

        // The chosen tab is shown by these two, never by the plate: ButtonFocusEffect already owns the
        // plate's colour, and two things writing to it would fight.
        Image bar = CreateImage("Selected", button.transform, accentColor);
        RectTransform barRect = bar.rectTransform;
        barRect.anchorMin = new Vector2(0f, 0f);
        barRect.anchorMax = new Vector2(1f, 0f);
        barRect.pivot = new Vector2(0.5f, 0f);
        barRect.sizeDelta = new Vector2(0f, 5f);
        barRect.anchoredPosition = Vector2.zero;

        button.onClick.AddListener(() => ShowControls(showsControls));

        tabs.Add(new Tab { showsControls = showsControls, label = text, activeBar = bar, button = button });

        return button;
    }

    private void BuildControlsPage(RectTransform page)
    {
        float y = contentTopY;

        // The two device columns are named once, at the top, rather than over every group.
        CreateLabel(page, "Keyboard Header", "KEYBOARD", captionSize, dimLabelColor, keyboardColumnX, y,
            deviceColumnWidth, TextAlignmentOptions.TopLeft, true);
        CreateLabel(page, "Gamepad Header", "GAMEPAD", captionSize, dimLabelColor, gamepadColumnX, y,
            deviceColumnWidth, TextAlignmentOptions.TopLeft, true);

        y -= headerHeight;

        ControlGroup[] groups = (controls == null || controls.Length == 0) ? ControlBindings.All : controls;

        for (int g = 0; g < groups.Length; g++)
        {
            y = BuildCaption(page, groups[g].title, y, accentColor, captionSize);
            y -= 4f;

            ControlBinding[] bindings = groups[g].bindings;
            if (bindings != null)
            {
                for (int i = 0; i < bindings.Length; i++)
                    y = BuildBindingRow(page, bindings[i], y);
            }

            y -= groupGap;
        }

        // The notes stop short of the right-hand corner, where BACK sits.
        string[] lines = (notes == null || notes.Length == 0) ? ControlBindings.Notes : notes;
        for (int i = 0; i < lines.Length; i++)
            y = BuildNote(page, lines[i], y, ContentWidth - 420f);
    }

    /// <summary>One line of the table: the action on the left, then what to press on each device.</summary>
    private float BuildBindingRow(Transform page, ControlBinding binding, float y)
    {
        RectTransform row = CreateRect("Row - " + binding.action, page);
        PlaceTopLeft(row, sideMargin, y, ContentWidth, rowHeight);

        Image plate = row.gameObject.AddComponent<Image>();
        plate.color = panelColor;
        plate.raycastTarget = false;

        CreateCell(row, "Action", binding.action, bodySize, labelColor, 22f, actionColumnWidth, TextAlignmentOptions.Left);
        CreateCell(row, "Keyboard", binding.keyboard, bodySize, dimLabelColor,
            keyboardColumnX - sideMargin, deviceColumnWidth, TextAlignmentOptions.Left);
        CreateCell(row, "Gamepad", binding.gamepad, bodySize, dimLabelColor,
            gamepadColumnX - sideMargin, deviceColumnWidth, TextAlignmentOptions.Left);

        return y - rowHeight - rowGap;
    }

    /// <summary>A cell of a row. The row is the parent, so the offsets are from the row's own left edge.</summary>
    private void CreateCell(Transform row, string name, string text, float size, Color colour,
        float x, float width, TextAlignmentOptions alignment)
    {
        TextMeshProUGUI label = CreateText(name, row, size, colour, alignment);
        label.text = text;

        RectTransform rect = label.rectTransform;
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.sizeDelta = new Vector2(width, rowHeight);
        rect.anchoredPosition = new Vector2(x, 0f);
    }

    private void BuildSettingsPage(RectTransform page)
    {
        float y = contentTopY;

        BuildSettingsNote(page);

        y = BuildCaption(page, graphicsCaption, y, accentColor, captionSize);

        // The presets, as a row of choices with the one in force lit up.
        BuildChoiceRow(page, presetLabels, y, presetRows, ChoosePreset);

        y -= presetButtonSize.y + groupGap;

        // The two switches, on top of the preset rather than part of it: turning shadows off at High, or the
        // blur on at Low, is allowed.
        shadowsButton = CreateButton("Shadows", page, "SHADOWS", switchSize);
        PlaceTopLeft((RectTransform)shadowsButton.transform, sideMargin, y, switchSize.x, switchSize.y);
        shadowsLabel = shadowsButton.GetComponentInChildren<TextMeshProUGUI>();
        shadowsButton.onClick.AddListener(ToggleShadows);

        y -= switchSize.y + switchGap;

        blurButton = CreateButton("Motion Blur", page, "MOTION BLUR", switchSize);
        PlaceTopLeft((RectTransform)blurButton.transform, sideMargin, y, switchSize.x, switchSize.y);
        blurLabel = blurButton.GetComponentInChildren<TextMeshProUGUI>();
        blurButton.onClick.AddListener(ToggleMotionBlur);

        y -= switchSize.y + groupGap;

        // The difficulty, under everything the picture is made of: it is the one choice here that changes what
        // a level asks of the player rather than what it looks like.
        y = BuildCaption(page, difficultyCaption, y, accentColor, captionSize);

        BuildChoiceRow(page, difficultyLabels, y, difficultyRows, ChooseDifficulty);

        BuildDifficultyNote(page, y + presetButtonSize.y + 12f);
    }

    /// <summary>
    /// A row of choices with the one in force lit up. The presets and the difficulties are the same row in
    /// every way but what they choose, so both are built by this rather than by two copies.
    ///
    /// The labels are the enum's own order - GraphicsQuality's presets, GameDifficulty's difficulties - so the
    /// index handed to the callback is the value to choose.
    /// </summary>
    private void BuildChoiceRow(RectTransform page, string[] labels, float y, List<ChoiceRow> rows, Action<int> choose)
    {
        // Three is as many as either setting has; a stray fourth label cannot be clicked into a value the
        // setting does not have.
        int count = Mathf.Min(labels != null ? labels.Length : 0, 3);

        for (int i = 0; i < count; i++)
        {
            int index = i;                          // captured, not the loop variable, for the callback

            Button button = CreateButton(labels[i], page, labels[i], presetButtonSize);
            PlaceTopLeft((RectTransform)button.transform, sideMargin + i * (presetButtonSize.x + presetGap), y,
                presetButtonSize.x, presetButtonSize.y);

            TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>();
            label.fontStyle = FontStyles.Bold;
            label.characterSpacing = 3f;

            Image bar = CreateImage("Selected", button.transform, accentColor);
            RectTransform barRect = bar.rectTransform;
            barRect.anchorMin = new Vector2(0f, 0f);
            barRect.anchorMax = new Vector2(1f, 0f);
            barRect.pivot = new Vector2(0.5f, 0f);
            barRect.sizeDelta = new Vector2(0f, 5f);
            barRect.anchoredPosition = Vector2.zero;

            button.onClick.AddListener(() => choose(index));

            rows.Add(new ChoiceRow { index = index, button = button, label = label, activeBar = bar });
        }
    }

    /// <summary>
    /// What the difficulties change, written under them. Unlike the graphics presets - where the picture in
    /// front of the player is the description - a time limit and a kill count cannot be seen from here, so
    /// this is the only place the choice can say what it does.
    /// </summary>
    private void BuildDifficultyNote(RectTransform page, float y)
    {
        TextMeshProUGUI note = CreateText("Difficulty Note", page, noteSize, Fade(labelColor, 0.75f), TextAlignmentOptions.TopLeft);
        note.text = difficultyNoteText;
        note.lineSpacing = 6f;

        // The full width between the margins: the plate beside the graphics settings ends well above this, so
        // there is nothing here to keep clear of.
        float height = Mathf.Max(noteSize * 1.6f, note.GetPreferredValues(difficultyNoteText, ContentWidth, 0f).y);

        PlaceTopLeft(note.rectTransform, sideMargin, y, ContentWidth, height);
    }

    /// <summary>
    /// The plate beside the settings. It carries the one thing the player needs to know about a choice - that
    /// it lands straight away - and nothing about what each preset does, because the picture in front of them
    /// is the description and a list of numbers beside it would be the implementation.
    /// </summary>
    private void BuildSettingsNote(RectTransform page)
    {
        float pad = 26f;
        float inner = noteWidth - pad * 2f;

        TextMeshProUGUI body = CreateText("Settings Note", page, noteSize, Fade(labelColor, 0.75f), TextAlignmentOptions.TopLeft);
        body.text = settingsNoteText;
        body.lineSpacing = 8f;

        float bodyHeight = Mathf.Max(noteSize * 2f, body.GetPreferredValues(settingsNoteText, inner, 0f).y);
        float height = pad + bodyHeight + pad;

        RectTransform panel = CreateRect("Settings Note Plate", page);
        PlaceTopLeft(panel, noteX, contentTopY, noteWidth, height);

        Image plate = panel.gameObject.AddComponent<Image>();
        plate.color = panelColor;
        plate.raycastTarget = false;

        // The text is moved onto the plate now that it exists, so it is drawn on top of it.
        body.transform.SetParent(panel, false);
        PlaceTopLeft(body.rectTransform, pad, -pad, inner, bodyHeight);
    }

    private void BuildActions(Transform root)
    {
        // Bottom-right, the way the tips screen puts its BACK: the controls table runs to the bottom of the
        // screen on the left, so the corner is the only place on this page that is always free.
        backButton = CreateButton("Back", root, backText, new Vector2(200f, 54f));
        PlaceBottomRight((RectTransform)backButton.transform, sideMargin, 46f, 200f, 54f);
        backButton.onClick.AddListener(GoBack);
    }

    /// <summary>
    /// Where the highlight goes at the places the nearest-neighbour guess gets wrong: off the tab row into
    /// the tab's own content, and through the preset row and the switches in the order they read.
    /// </summary>
    private void WireNavigation()
    {
        if (controlsTab == null || settingsTab == null) return;

        Button middlePreset = presetRows.Count > 1 ? presetRows[presetRows.Count / 2].button : null;
        Button middleDifficulty = difficultyRows.Count > 1 ? difficultyRows[difficultyRows.Count / 2].button : null;

        SetNavigation(controlsTab, null, settingsTab, backButton, null);
        SetNavigation(settingsTab, null, null, middlePreset, controlsTab);

        for (int i = 0; i < presetRows.Count; i++)
        {
            Button left = i > 0 ? presetRows[i - 1].button : null;
            Button right = i < presetRows.Count - 1 ? presetRows[i + 1].button : null;

            SetNavigation(presetRows[i].button, left, right, shadowsButton, settingsTab);
        }

        if (shadowsButton != null)
            SetNavigation(shadowsButton, null, null, blurButton, middlePreset);

        if (blurButton != null)
            SetNavigation(blurButton, null, null, middleDifficulty, shadowsButton);

        for (int i = 0; i < difficultyRows.Count; i++)
        {
            Button left = i > 0 ? difficultyRows[i - 1].button : null;
            Button right = i < difficultyRows.Count - 1 ? difficultyRows[i + 1].button : null;

            SetNavigation(difficultyRows[i].button, left, right, backButton, blurButton);
        }

        if (backButton != null)
            SetNavigation(backButton, null, null, null, middleDifficulty != null ? middleDifficulty : blurButton);
    }

    private static void SetNavigation(Button button, Button left, Button right, Button down, Button up)
    {
        if (button == null) return;

        Navigation navigation = button.navigation;
        navigation.mode = Navigation.Mode.Explicit;
        navigation.selectOnLeft = left;
        navigation.selectOnRight = right;
        navigation.selectOnDown = down;
        navigation.selectOnUp = up;

        button.navigation = navigation;
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

    /// <summary>A section heading in the accent colour. Returns the y the section's content starts at.</summary>
    private float BuildCaption(Transform parent, string text, float y, Color colour, float size)
    {
        CreateLabel(parent, "Caption", text, size, colour, sideMargin, y, ContentWidth, TextAlignmentOptions.TopLeft, true);

        return y - size * 1.5f - 10f;
    }

    /// <summary>A line of guidance under the table. Returns the y the next one starts at.</summary>
    private float BuildNote(Transform parent, string text, float y, float width)
    {
        TextMeshProUGUI note = CreateText("Note", parent, noteSize, Fade(labelColor, 0.6f), TextAlignmentOptions.TopLeft);
        note.text = text;
        note.lineSpacing = 6f;

        float height = Mathf.Max(noteSize * 1.6f, note.GetPreferredValues(text, width, 0f).y);

        PlaceTopLeft(note.rectTransform, sideMargin, y, width, height);

        return y - height - 8f;
    }

    private TextMeshProUGUI CreateLabel(Transform parent, string name, string text, float size, Color colour,
        float x, float y, float width, TextAlignmentOptions alignment, bool bold)
    {
        TextMeshProUGUI label = CreateText(name, parent, size, colour, alignment);
        label.text = text;
        label.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
        label.characterSpacing = bold ? 3f : 0f;

        PlaceTopLeft(label.rectTransform, x, y, width, size * 1.5f);

        return label;
    }

    /// <summary>A button: a plate with a label on it. The plate takes the click, and being an ordinary Button it gets the project's own hover and focus reaction for free.</summary>
    private Button CreateButton(string name, Transform parent, string label, Vector2 size)
    {
        RectTransform rect = CreateRect(name, parent);

        Image plate = rect.gameObject.AddComponent<Image>();
        plate.color = buttonColor;
        plate.raycastTarget = true;

        Button button = rect.gameObject.AddComponent<Button>();
        StyleButton(button, plate);

        TextMeshProUGUI text = CreateText("Label", rect, bodySize, labelColor, TextAlignmentOptions.Center);
        text.text = label;
        text.characterSpacing = 2f;
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

    /// <summary>Pins a rect to a point measured from the top-left corner of whatever holds it.</summary>
    private static void PlaceTopLeft(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(width, height);
    }

    /// <summary>Pins a rect to the top-right corner of whatever holds it, the way the hint line sits.</summary>
    private static void PlaceTopRight(RectTransform rect, float right, float top, float width, float height)
    {
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-right, top);
        rect.sizeDelta = new Vector2(width, height);
    }

    /// <summary>Pins a rect to the bottom-right corner of whatever holds it.</summary>
    private static void PlaceBottomRight(RectTransform rect, float right, float bottom, float width, float height)
    {
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(1f, 0f);
        rect.anchoredPosition = new Vector2(-right, bottom);
        rect.sizeDelta = new Vector2(width, height);
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
}
