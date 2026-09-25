using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The options screen: what the game's controls are, and the settings - one tab each, over a single page. The
/// settings are one page rather than several: what the game looks like and what it sounds like are both some
/// setting or other, and the tab row is for the two things a player comes here to see, not for three screens.
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
/// The settings tab is the second page, and the only place anything is chosen. It holds the three groups in
/// the order they matter: the graphics settings (<see cref="GraphicsQuality"/> - the three presets and the
/// two switches that sit on top of them), the difficulties (<see cref="GameDifficulty"/> - what a level's time
/// limit and its kill requirement become), and the sound (<see cref="SoundSettings"/> - one row per channel,
/// plus the switch that keeps the engine in the same place in the mix whichever camera the player drives
/// from). A choice takes effect and is saved as soon as it is made - only the ground detail of a level waits
/// for that level to load, and a difficulty lands on the next level - so a level that is open when either
/// changes asks to be restarted rather than saying so in advance (see <see cref="PauseOptionsPanel"/>).
/// Nothing here describes what the presets do: the picture is the description, and a list of numbers beside
/// it would be the implementation rather than the choice.
///
/// The sound rows are built from <see cref="SoundSettings"/> - the channels, their names, the steps, where the
/// choice is kept - so this page and the pause menu's options are the same rows, and a channel added there
/// appears here without this file changing. A row lights up the step the channel is really playing at, which
/// means the master's own row moves the rest: put the master on LOW and every other row reads LOW with it,
/// because that is what they are now playing at.
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
    [Tooltip("The plate beside the settings. Says how a choice is taken, not what the presets do.")]
    [TextArea(2, 6)]
    public string settingsNoteText =
        "Your choice is saved and applied straight away.";

    // ---------------------------------------------------------------- the sound rows

    [Header("Sound")]
    public string soundCaption = "SOUND";
    public string cameraMixText = "CAMERA MIX";
    [Tooltip("The line beside the camera mix switch, saying what it does in one line.")]
    [TextArea(2, 6)]
    public string cameraMixNoteText =
        "Keeps the engine at the same level whichever camera you drive from.";

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
    public Vector2 presetButtonSize = new Vector2(230f, 58f);
    public float presetGap = 16f;
    public Vector2 switchSize = new Vector2(400f, 54f);
    public float switchGap = 12f;
    [Tooltip("Where the note plates sit, and how wide they are. One under the other, beside the left column.")]
    public float noteX = 1120f;
    public float noteWidth = 740f;
    public float noteGap = 32f;
    [Tooltip("How far under a section's content the next section's heading starts. The three groups are told " +
             "apart by this and by the rule under each heading, since the rows themselves are alike.")]
    public float sectionGap = 28f;
    [Tooltip("The accent rule drawn under a section heading, matching the one under the screen's title.")]
    public float sectionRuleWidth = 240f;
    public float sectionRuleHeight = 4f;

    [Header("Layout - the sound rows")]
    public Vector2 soundButtonSize = new Vector2(170f, 46f);
    public float soundButtonGap = 12f;
    public float soundRowGap = 8f;
    [Tooltip("How much room the channel's own name is given, before its steps start.")]
    public float soundLabelWidth = 400f;
    public float soundFirstButtonX = 440f;
    [Tooltip("How far the camera mix's one-line note sits to the right of its switch.")]
    public float cameraMixNoteGap = 30f;

    // ---------------------------------------------------------------- transition

    [Header("Transition")]
    [Tooltip("Where BACK goes. Must be in Build Settings.")]
    public string backSceneName = "Menu";
    public float fadeInTime = 0.35f;

    // ---------------------------------------------------------------- state

    private sealed class Tab
    {
        public Page shows;
        public TextMeshProUGUI label;
        public Image activeBar;
        public Button button;
    }

    /// <summary>One choice of a row: a preset, a difficulty, or one step of a sound channel.</summary>
    private sealed class ChoiceRow
    {
        public int index;
        public Button button;
        public TextMeshProUGUI label;
        public Image activeBar;
    }

    /// <summary>One sound channel: its name on the left, and the steps it can be on to the right.</summary>
    private sealed class ChannelRow
    {
        public int channel;
        public TextMeshProUGUI label;
        public ChoiceRow[] steps;
    }

    /// <summary>Which of the screen's two pages is on show. Only ever one of them.</summary>
    private enum Page
    {
        Controls = 0,
        Settings = 1,
    }

    private readonly List<Tab> tabs = new List<Tab>();
    private readonly List<ChoiceRow> presetRows = new List<ChoiceRow>();
    private readonly List<ChoiceRow> difficultyRows = new List<ChoiceRow>();
    private readonly List<ChannelRow> soundRows = new List<ChannelRow>();

    private CanvasGroup group;
    private RectTransform controlsPage;
    private RectTransform settingsPage;

    private Button controlsTab;
    private Button settingsTab;
    private Button backButton;
    private Button shadowsButton;
    private Button blurButton;
    private Button cameraMixButton;

    private TextMeshProUGUI shadowsLabel;
    private TextMeshProUGUI blurLabel;
    private TextMeshProUGUI cameraMixLabel;

    private Page page = Page.Controls;

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
        SoundSettings.Changed += Refresh;
    }

    private void Start()
    {
        ShowPage(Page.Controls);
        Refresh();

        StartCoroutine(FadeIn());
    }

    private void OnDestroy()
    {
        GraphicsQuality.Changed -= Refresh;
        GameDifficulty.Changed -= Refresh;
        SoundSettings.Changed -= Refresh;
    }

    // ---------------------------------------------------------------- tabs

    /// <summary>
    /// Puts one page on screen. The other is switched off rather than hidden, so its buttons are not merely
    /// unseen but unselectable - which is what keeps a keyboard or a gamepad from wandering onto them.
    /// </summary>
    private void ShowPage(Page show)
    {
        page = show;

        if (controlsPage != null) controlsPage.gameObject.SetActive(show == Page.Controls);
        if (settingsPage != null) settingsPage.gameObject.SetActive(show == Page.Settings);

        RefreshTabs();
    }

    private void RefreshTabs()
    {
        for (int i = 0; i < tabs.Count; i++)
        {
            bool active = tabs[i].shows == page;

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

    private void ChooseLevel(int channel, int step)
    {
        SoundSettings.SetLevel(channel, (SoundLevel)step);
    }

    private void ToggleCameraMix()
    {
        SoundSettings.SetCameraMix(!SoundSettings.CameraMix);
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

        // The step lit is the one the channel is really playing at, not the one the player chose for it: the
        // master is a ceiling, so its row moving caps the rest and they say so here. Each channel's own choice
        // is still there underneath, and comes back to the bar the moment the master lets it.
        for (int i = 0; i < soundRows.Count; i++)
        {
            ChannelRow row = soundRows[i];
            int level = (int)SoundSettings.EffectiveLevel(row.channel);

            row.label.color = level > 0 ? labelColor : dimLabelColor;

            for (int s = 0; s < row.steps.Length; s++)
            {
                bool active = s == level;

                row.steps[s].activeBar.enabled = active;
                row.steps[s].label.color = active ? accentColor : labelColor;
            }
        }

        if (cameraMixLabel != null)
        {
            cameraMixLabel.text = cameraMixText + "   " + (SoundSettings.CameraMix ? onText : offText);
            cameraMixLabel.color = SoundSettings.CameraMix ? labelColor : dimLabelColor;
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
        controlsTab = CreateTab(root, "Controls", controlsTabText, 0, Page.Controls);
        settingsTab = CreateTab(root, "Settings", settingsTabText, 1, Page.Settings);
    }

    private Button CreateTab(Transform root, string name, string label, int index, Page shows)
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

        button.onClick.AddListener(() => ShowPage(shows));

        tabs.Add(new Tab { shows = shows, label = text, activeBar = bar, button = button });

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

        BuildSettingsNotes(page);

        y = BuildSectionHeading(page, graphicsCaption, y);

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

        y -= switchSize.y + sectionGap;

        // The difficulty, under everything the picture is made of: it is the one choice here that changes what
        // a level asks of the player rather than what it looks like.
        y = BuildSectionHeading(page, difficultyCaption, y);

        BuildChoiceRow(page, difficultyLabels, y, difficultyRows, ChooseDifficulty);

        y -= presetButtonSize.y + sectionGap;

        // The sound is the last group on this page rather than a page of its own: it is a setting like the
        // other two, and the tab row is what the player is choosing between - what the game looks like and
        // what it sounds like, not three separate screens.
        BuildSoundGroup(page, y);
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
    /// The plate beside the settings: the one thing the player needs to know about a choice - that it lands
    /// straight away - and nothing about what each preset does, because the picture in front of them is the
    /// description and a list of numbers beside it would be the implementation.
    ///
    /// It is out of the left column's way for the whole length of the page, so no row below has to stop short
    /// of it.
    /// </summary>
    private void BuildSettingsNotes(RectTransform page)
    {
        BuildNotePlate(page, settingsNoteText, "Settings Note", noteX, contentTopY, noteWidth);
    }

    /// <summary>
    /// A paragraph on its own plate, tall enough for the words in it however they wrap. Returns the height it
    /// took, so the caller knows where the next thing may start.
    /// </summary>
    private float BuildNotePlate(Transform page, string text, string name, float x, float y, float width)
    {
        float pad = 26f;
        float inner = width - pad * 2f;

        TextMeshProUGUI body = CreateText(name, page, noteSize, Fade(labelColor, 0.75f), TextAlignmentOptions.TopLeft);
        body.text = text;
        body.lineSpacing = 8f;

        float bodyHeight = Mathf.Max(noteSize * 2f, body.GetPreferredValues(text, inner, 0f).y);
        float height = pad + bodyHeight + pad;

        RectTransform panel = CreateRect(name + " Plate", page);
        PlaceTopLeft(panel, x, y, width, height);

        Image plate = panel.gameObject.AddComponent<Image>();
        plate.color = panelColor;
        plate.raycastTarget = false;

        // The text is moved onto the plate now that it exists, so it is drawn on top of it.
        body.transform.SetParent(panel, false);
        PlaceTopLeft(body.rectTransform, pad, -pad, inner, bodyHeight);

        return height;
    }

    /// <summary>A paragraph with no plate of its own, for one that sits beside something already plated.</summary>
    private void BuildSideNote(Transform page, string text, float x, float y, float width, float height)
    {
        TextMeshProUGUI body = CreateText("Note", page, noteSize, Fade(labelColor, 0.75f), TextAlignmentOptions.TopLeft);
        body.text = text;
        body.lineSpacing = 8f;

        PlaceTopLeft(body.rectTransform, x, y, width, height);
    }

    /// <summary>
    /// The sound group of the settings page: a caption with the camera mix switch beside it, then one row per
    /// channel. Returns where the group ends, should anything ever be built under it.
    ///
    /// The rows are built from <see cref="SoundSettings"/> rather than written out here, which is what makes
    /// this page and the pause menu's the same rows - the same channels, the same steps, the same where they
    /// are kept - and what makes a channel added there turn up in both.
    /// </summary>
    private float BuildSoundGroup(RectTransform page, float y)
    {
        // The heading carries the camera mix as well: it is not a volume, it is the one tweak on top of what
        // the ENGINE row says, so it reads as part of the group's heading rather than as a sixth channel - and
        // it costs the rows below it no room at all. The heading's rule is short enough to stop well short of
        // the switch, the way the screen's own title rule does.
        float headingTop = y;
        float captionHeight = captionSize * 1.5f;

        // The heading's own lettering stops before the switch beside it; the rule under it is short enough not
        // to reach it either.
        y = BuildSectionHeading(page, soundCaption, y, soundLabelWidth - 20f);

        // Centred on the heading's own line, so the two read as one row.
        float switchY = headingTop + Mathf.Max(0f, (switchSize.y - captionHeight) * 0.5f);

        cameraMixButton = CreateButton("Camera Mix", page, cameraMixText, switchSize);
        PlaceTopLeft((RectTransform)cameraMixButton.transform, sideMargin + soundLabelWidth, switchY,
            switchSize.x, switchSize.y);
        cameraMixLabel = cameraMixButton.GetComponentInChildren<TextMeshProUGUI>();
        cameraMixButton.onClick.AddListener(ToggleCameraMix);

        // The note is on the caption's line, to the switch's right, where nothing else is drawn and the text
        // fits on one line however narrow the gap is made.
        float mixNoteX = sideMargin + soundLabelWidth + switchSize.x + cameraMixNoteGap;

        BuildSideNote(page, cameraMixNoteText, mixNoteX, switchY + (switchSize.y - noteSize * 1.6f) * 0.5f,
            ContentWidth - (mixNoteX - sideMargin), switchSize.y);

        // Whatever this heading row takes - the switch is taller than the lettering and its rule - the channels
        // start below all of it.
        y = Mathf.Min(y, switchY - switchSize.y) - 12f;

        for (int channel = 0; channel < SoundSettings.ChannelCount; channel++)
            y = BuildSoundRow(page, channel, y);

        return y;
    }

    /// <summary>
    /// One channel of the sound group: its name on the left, then the game's steps, with the one in force lit.
    /// Returns where the row below it starts.
    /// </summary>
    private float BuildSoundRow(RectTransform page, int channel, float y)
    {
        // The name is a label rather than a button: it heads the row, and the steps beside it want every pixel
        // of the width. It is nudged down to sit on the steps' centre line rather than their top edge.
        TextMeshProUGUI name = CreateLabel(page, "Channel - " + SoundSettings.ChannelNames[channel],
            SoundSettings.ChannelNames[channel], bodySize, labelColor, sideMargin, y, soundLabelWidth,
            TextAlignmentOptions.Left, true);

        name.rectTransform.anchoredPosition = new Vector2(sideMargin,
            y - (soundButtonSize.y - bodySize * 1.5f) * 0.5f);

        ChannelRow row = new ChannelRow
        {
            channel = channel,
            label = name,
            steps = new ChoiceRow[SoundSettings.StepCount],
        };

        for (int step = 0; step < SoundSettings.StepCount; step++)
        {
            int chosen = step;              // captured, not the loop variable, for the callback

            Button button = CreateButton(SoundSettings.ChannelNames[channel] + " " + SoundSettings.StepNames[step],
                page, SoundSettings.StepNames[step], soundButtonSize);

            PlaceTopLeft((RectTransform)button.transform,
                sideMargin + soundFirstButtonX + step * (soundButtonSize.x + soundButtonGap), y,
                soundButtonSize.x, soundButtonSize.y);

            TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>();
            label.characterSpacing = 2f;

            Image bar = CreateImage("Selected", button.transform, accentColor);
            RectTransform barRect = bar.rectTransform;
            barRect.anchorMin = new Vector2(0f, 0f);
            barRect.anchorMax = new Vector2(1f, 0f);
            barRect.pivot = new Vector2(0.5f, 0f);
            barRect.sizeDelta = new Vector2(0f, 5f);
            barRect.anchoredPosition = Vector2.zero;

            button.onClick.AddListener(() => ChooseLevel(channel, chosen));

            row.steps[step] = new ChoiceRow { index = step, button = button, label = label, activeBar = bar };
        }

        soundRows.Add(row);

        return y - soundButtonSize.y - soundRowGap;
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

        // Where the settings page is entered from: the middle preset, the way the sound rows are entered from
        // the middle step of the first channel. The tab row is the one place the nearest-neighbour guess reads
        // badly.
        Button middleFirstSound = null;

        if (soundRows.Count > 0)
            middleFirstSound = soundRows[0].steps[SoundSettings.StepCount / 2].button;

        SetNavigation(controlsTab, null, settingsTab, backButton, null);
        SetNavigation(settingsTab, controlsTab, null, middlePreset, controlsTab);

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

            SetNavigation(difficultyRows[i].button, left, right, cameraMixButton, blurButton);
        }

        // The sound rows are a grid of twenty small buttons, which is exactly the shape a
        // nearest-neighbour guess gets wrong once the rows are this narrow - so the columns and the rows are
        // wired as the table they look like. Up from the top row is the camera mix above it, which is the
        // group's heading; down from the last row is BACK.
        Button middleLastSound = null;

        for (int i = 0; i < soundRows.Count; i++)
        {
            ChannelRow row = soundRows[i];

            for (int s = 0; s < row.steps.Length; s++)
            {
                Button left = s > 0 ? row.steps[s - 1].button : null;
                Button right = s < row.steps.Length - 1 ? row.steps[s + 1].button : null;
                Button above = i > 0 ? soundRows[i - 1].steps[s].button : cameraMixButton;
                Button below = i < soundRows.Count - 1 ? soundRows[i + 1].steps[s].button : backButton;

                SetNavigation(row.steps[s].button, left, right, below, above);
            }

            if (i == soundRows.Count - 1) middleLastSound = row.steps[SoundSettings.StepCount / 2].button;
        }

        if (cameraMixButton != null)
            SetNavigation(cameraMixButton, null, null,
                middleFirstSound != null ? middleFirstSound : backButton,
                middleDifficulty != null ? middleDifficulty : blurButton);

        if (backButton != null)
            SetNavigation(backButton, null, null, null,
                middleLastSound != null ? middleLastSound : middleDifficulty);
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

    /// <summary>A heading inside the controls table. Returns the y the group's rows start at.</summary>
    private float BuildCaption(Transform parent, string text, float y, Color colour, float size)
    {
        CreateLabel(parent, "Caption", text, size, colour, sideMargin, y, ContentWidth, TextAlignmentOptions.TopLeft, true);

        return y - size * 1.5f - 10f;
    }

    /// <summary>
    /// A section heading: its name in the accent colour with a short accent rule under it, the way the screen's
    /// own title has one. This is what tells the three groups apart - the rows themselves are alike enough
    /// that a heading alone leaves them reading as one long list - and it returns the y the section's content
    /// starts at, so the rule's height and the gap under it are decided in one place.
    /// </summary>
    private float BuildSectionHeading(Transform parent, string text, float y)
    {
        return BuildSectionHeading(parent, text, y, ContentWidth);
    }

    /// <summary>The same, for a heading that shares its line with something else and so must stop short of it.</summary>
    private float BuildSectionHeading(Transform parent, string text, float y, float width)
    {
        CreateLabel(parent, "Caption", text, captionSize, accentColor, sideMargin, y, width,
            TextAlignmentOptions.TopLeft, true);

        float ruleY = y - captionSize * 1.5f - 4f;

        Image rule = CreateImage("Section Rule", parent, Fade(accentColor, 0.45f));
        PlaceTopLeft(rule.rectTransform, sideMargin, ruleY, sectionRuleWidth, sectionRuleHeight);

        return ruleY - sectionRuleHeight - 12f;
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
