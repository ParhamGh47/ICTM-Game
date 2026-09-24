using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// The options, inside the pause menu's own window.
///
/// The pause panel is its own design - a warm plate with plated buttons and dark lettering, where the main
/// menu is dark navy with orange accents - so this is the same content as <see cref="OptionsScreen"/> adapted
/// to it rather than a second copy of that screen: no page of its own and no background of its own, just the
/// pause window, with the pause buttons taken away while the options are open and put back when they close.
/// The plate sprite is not referenced from here at all - it is taken from the pause buttons that are already
/// in the window, so the options keep looking like the pause menu even if that artwork is ever changed.
///
/// Both tabs are the same two as the options screen: CONTROLS, which is the table from
/// <see cref="ControlBindings"/>, and SETTINGS, which is <see cref="GraphicsQuality"/> and
/// <see cref="GameDifficulty"/>.
///
/// The one thing this screen does that the options screen does not: when a setting is chosen that a level
/// cannot pick up until it is started again - the amount of ground detail its terrain draws, or the
/// difficulty its time limit and kill requirement were built with - the level that is open says so and offers
/// the restart that applies it, rather than the setting quietly doing nothing until the player happens to
/// play the level again.
///
/// The UI is built in code for the same reason the other screens build theirs: nothing has to be added to the
/// pause prefab, so the pause panel the game already had keeps working exactly as it did.
/// </summary>
[DisallowMultipleComponent]
public class PauseOptionsPanel : MonoBehaviour
{
    // ---------------------------------------------------------------- content

    [Header("Content")]
    public string titleText = "OPTIONS";
    public string controlsTabText = "CONTROLS";
    public string settingsTabText = "SETTINGS";
    public string backText = "BACK";
    public string graphicsCaption = "GRAPHICS";
    public string keyboardHeaderText = "KEYBOARD";
    public string gamepadHeaderText = "GAMEPAD";
    public string shadowsText = "SHADOWS";
    public string motionBlurText = "MOTION BLUR";
    public string onText = "ON";
    public string offText = "OFF";
    public string difficultyCaption = "DIFFICULTY";

    [Tooltip("The note beside the settings. Says how a choice is taken, not what the presets do.")]
    [TextArea(2, 6)]
    public string settingsNoteText = "Your choice is saved and applied straight away.";

    [Tooltip("Shown when the open level cannot show the chosen preset until it is loaded again.")]
    public string reloadPromptText = "The level's ground detail changed - restart to apply it.";
    [Tooltip("Shown when the difficulty was changed while a level is open: its time limit and kill " +
             "requirement were read as it started, so it is still being played at the old one.")]
    public string difficultyPromptText = "Difficulty changed - restart to play this level at the new one.";
    [Tooltip("Shown when both changed at once.")]
    public string bothPromptText = "Graphics and difficulty changed - restart to apply them.";
    public string reloadButtonText = "RESTART LEVEL";

    // There is deliberately nothing under the table explaining how the controls behave: the table is what the
    // player came for and the two lines this used to carry - holding FOCUS, and any pad working - are on the
    // options screen of the main menu, where there is room for them.

    [Header("Art (optional)")]
    [Tooltip("Falls back to the project's default TMP font when empty.")]
    public TMP_FontAsset font;

    [Header("Colours (the pause window's own lettering)")]
    [Tooltip("The dark grey the pause buttons and the PAUSE heading are written in.")]
    public Color inkColor = new Color(0.196f, 0.196f, 0.196f, 1f);
    public Color dimInkColor = new Color(0.196f, 0.196f, 0.196f, 0.62f);
    public Color faintInkColor = new Color(0.196f, 0.196f, 0.196f, 0.42f);
    [Tooltip("The faint white the table's rows sit on.")]
    public Color rowPlateColor = new Color(1f, 1f, 1f, 0.25f);
    [Tooltip("The warm orange the restart prompt is flagged with, taken from the pause buttons.")]
    public Color promptPlateColor = new Color(0.996f, 0.8f, 0.44f, 0.55f);

    // The window is 1300x850, but the plate drawn inside it - the pause panel's own artwork - covers about
    // 975x670 of that and is transparent around the edge. Everything is laid out in this box, which is inset
    // from the plate on all four sides, so nothing ever sits on the plate's border.
    [Header("Layout (centred on the window, inset from the plate's border)")]
    public Vector2 safeSize = new Vector2(830f, 570f);
    public float headingSize = 40f;
    public float headingTopY = 0f;
    public float headingHeight = 46f;

    // The BACK plate sits just below the tab row rather than at the foot of the window, which is where the
    // table needs its last rows. It is a few pixels lower than the tabs on purpose: the pause panel's own
    // MenuNavigation falls back on the top-most button, and the top-most button here should be a tab.
    public Vector2 backButtonSize = new Vector2(140f, 38f);
    public float backTopY = 56f;
    public float tabTopY = 52f;
    public Vector2 tabSize = new Vector2(160f, 38f);
    public float tabGap = 12f;
    public float contentTopY = 100f;

    [Header("Layout - the controls table")]
    public float columnHeaderHeight = 18f;
    public float captionHeight = 30f;
    public float rowHeight = 28f;
    public float rowGap = 2f;
    public float groupGap = 10f;
    public float rowInset = 10f;
    public float actionColumnWidth = 196f;
    public float keyboardColumnX = 216f;
    public float keyboardColumnWidth = 312f;
    public float gamepadColumnX = 538f;
    public float gamepadColumnWidth = 292f;

    [Header("Layout - the settings tab")]
    public Vector2 presetButtonSize = new Vector2(165f, 44f);
    public float presetGap = 12f;
    public Vector2 switchSize = new Vector2(320f, 42f);
    public float switchGap = 8f;
    public float noteX = 545f;
    public float noteWidth = 285f;
    public float promptTopY = 400f;
    public float promptHeight = 58f;
    public Vector2 reloadButtonSize = new Vector2(200f, 42f);

    // ---------------------------------------------------------------- state

    private PauseMenu owner;
    private RectTransform window;
    private RectTransform content;

    private RectTransform controlsPage;
    private RectTransform settingsPage;

    private readonly List<Tab> tabs = new List<Tab>();
    private readonly List<ChoiceRow> presetRows = new List<ChoiceRow>();
    private readonly List<ChoiceRow> difficultyRows = new List<ChoiceRow>();

    private Button controlsTab;
    private Button settingsTab;
    private Button backButton;
    private Button shadowsButton;
    private Button blurButton;
    private Button reloadButton;

    private TextMeshProUGUI shadowsLabel;
    private TextMeshProUGUI blurLabel;
    private TextMeshProUGUI promptLabel;

    private RectTransform reloadPrompt;

    private bool showingControls = true;
    private bool open;

    /// <summary>The plate the pause buttons are drawn with, borrowed from the window itself.</summary>
    private Sprite plateSprite;

    private readonly List<GameObject> hidden = new List<GameObject>();
    private readonly List<bool> hiddenState = new List<bool>();

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

    /// <summary>Whether the options are showing rather than the pause buttons.</summary>
    public bool IsOpen
    {
        get { return open; }
    }

    // ---------------------------------------------------------------- creation

    /// <summary>
    /// Builds the panel inside <paramref name="window"/> - the pause panel's own rect, so the options appear
    /// in the same window rather than on a page of their own. It starts switched off, so its UI is built the
    /// first time it is opened rather than while a level is starting.
    /// </summary>
    public static PauseOptionsPanel Create(PauseMenu owner, RectTransform window, TMP_FontAsset font)
    {
        GameObject go = new GameObject("Options", typeof(RectTransform));

        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(window, false);
        rect.localScale = Vector3.one;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;

        go.SetActive(false);

        PauseOptionsPanel panel = go.AddComponent<PauseOptionsPanel>();
        panel.owner = owner;
        panel.window = window;
        panel.font = font;

        return panel;
    }

    // ---------------------------------------------------------------- opening and closing

    /// <summary>
    /// Takes the pause window over: the buttons it was showing are put away and the options are shown in
    /// their place, on the same plate.
    /// </summary>
    public void Open()
    {
        if (open) return;

        open = true;

        HideWindowButtons();
        gameObject.SetActive(true);
    }

    /// <summary>Gives the pause window back, exactly as it was.</summary>
    public void Close()
    {
        if (!open) return;

        open = false;

        gameObject.SetActive(false);
        RestoreWindowButtons();
    }

    /// <summary>
    /// Puts away everything else the pause window holds - its buttons, its heading, and anything else that is
    /// added to it later - remembering what was switched on, so closing this gives the window back untouched.
    /// </summary>
    private void HideWindowButtons()
    {
        hidden.Clear();
        hiddenState.Clear();

        Transform root = transform.parent;
        if (root == null) return;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child == transform) continue;

            hidden.Add(child.gameObject);
            hiddenState.Add(child.gameObject.activeSelf);
            child.gameObject.SetActive(false);
        }
    }

    private void RestoreWindowButtons()
    {
        for (int i = 0; i < hidden.Count; i++)
        {
            if (hidden[i] != null) hidden[i].SetActive(hiddenState[i]);
        }

        hidden.Clear();
        hiddenState.Clear();
    }

    // ---------------------------------------------------------------- lifecycle

    private void Awake()
    {
        BuildUi();
    }

    private void OnEnable()
    {
        GraphicsQuality.Changed += Refresh;
        GameDifficulty.Changed += Refresh;

        Refresh();

        BeginFromTabs();
    }

    private void OnDisable()
    {
        GraphicsQuality.Changed -= Refresh;
        GameDifficulty.Changed -= Refresh;
    }

    private void BeginFromTabs()
    {
        EventSystem events = EventSystem.current;
        if (events == null || controlsTab == null) return;

        ShowControls(true);

        // Clearing it first is deliberate: the pause panel's own MenuNavigation is still holding the button
        // this panel just took away, and letting go of it is what makes that component re-read the buttons
        // that are here now. Left alone it would go on protecting a selection that is no longer on screen.
        // It also leaves the highlight on the tab row, which is where its own guess lands too - the top-most
        // button is the tab row here, not the BACK button in the corner.
        events.SetSelectedGameObject(null);
        events.SetSelectedGameObject(controlsTab.gameObject);

        ButtonFocusEffect.HoldHighlightOnSelection(0.35f);
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
            tabs[i].label.color = active ? inkColor : dimInkColor;
            tabs[i].label.fontStyle = active ? FontStyles.Bold : FontStyles.Normal;
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

    private void ReloadLevel()
    {
        if (owner != null) owner.RestartLevel();
    }

    /// <summary>
    /// Brings the labels back in line with the settings, and the restart prompt in line with what the open
    /// level is actually drawing.
    /// </summary>
    private void Refresh()
    {
        for (int i = 0; i < presetRows.Count; i++)
        {
            ChoiceRow row = presetRows[i];
            bool active = (int)GraphicsQuality.Current == row.index;

            row.activeBar.enabled = active;
            row.label.color = active ? inkColor : dimInkColor;
            row.label.fontStyle = active ? FontStyles.Bold : FontStyles.Normal;
        }

        for (int i = 0; i < difficultyRows.Count; i++)
        {
            ChoiceRow row = difficultyRows[i];
            bool active = (int)GameDifficulty.Current == row.index;

            row.activeBar.enabled = active;
            row.label.color = active ? inkColor : dimInkColor;
            row.label.fontStyle = active ? FontStyles.Bold : FontStyles.Normal;
        }

        if (shadowsLabel != null)
        {
            shadowsLabel.text = shadowsText + "   " + (GraphicsQuality.Shadows ? onText : offText);
            shadowsLabel.color = GraphicsQuality.Shadows ? inkColor : dimInkColor;
        }

        if (blurLabel != null)
        {
            blurLabel.text = motionBlurText + "   " + (GraphicsQuality.MotionBlur ? onText : offText);
            blurLabel.color = GraphicsQuality.MotionBlur ? inkColor : dimInkColor;
        }

        // Two choices here have to wait for a level to be started again before it can honour them: the amount
        // of ground detail its terrain draws, which it builds as it loads, and the difficulty, whose time
        // limit and kill requirement it reads as its HUD starts. The offer to restart appears exactly while
        // one of them really is out of date and goes away when the player picks the value the level already
        // has. Nothing has to be rewired when it comes and goes: every button here keeps Unity's own
        // navigation, which skips what is switched off.
        bool graphicsOutOfDate = GraphicsQuality.ActiveSceneNeedsReload();
        bool difficultyOutOfDate = GameDifficulty.ActiveSceneNeedsReload();

        if (reloadPrompt != null)
        {
            reloadPrompt.gameObject.SetActive(graphicsOutOfDate || difficultyOutOfDate);

            if (promptLabel != null)
                promptLabel.text = PromptText(graphicsOutOfDate, difficultyOutOfDate);
        }
    }

    /// <summary>
    /// What the restart prompt says. It is one plate with one button whatever the reason - the two settings
    /// are applied by the same restart - so the wording is the only thing that changes.
    /// </summary>
    private string PromptText(bool graphics, bool difficulty)
    {
        if (graphics && difficulty) return bothPromptText;

        return difficulty ? difficultyPromptText : reloadPromptText;
    }

    // ---------------------------------------------------------------- UI construction

    private void BuildUi()
    {
        plateSprite = FindPausePlateSprite();

        content = CreateRect("Safe Area", transform);
        content.anchorMin = new Vector2(0.5f, 0.5f);
        content.anchorMax = new Vector2(0.5f, 0.5f);
        content.pivot = new Vector2(0.5f, 0.5f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = safeSize;

        BuildHeading(content);
        BuildTabs(content);

        // Two pages stacked on each other, only one ever switched on, so the buttons of the hidden page are
        // not merely unseen but unselectable - which is what keeps the highlight from wandering onto them.
        controlsPage = CreateRect("Controls Page", content);
        Stretch(controlsPage);
        BuildControlsPage(controlsPage);

        settingsPage = CreateRect("Settings Page", content);
        Stretch(settingsPage);
        BuildSettingsPage(settingsPage);

        BuildActions(content);
    }

    /// <summary>
    /// The plate the pause buttons are drawn with. Taken from the buttons rather than referenced, so this
    /// screen cannot fall out of step with the pause menu's artwork.
    /// </summary>
    private Sprite FindPausePlateSprite()
    {
        if (window == null) return null;

        Image[] images = window.GetComponentsInChildren<Image>(true);

        for (int i = 0; i < images.Length; i++)
        {
            if (images[i].sprite == null) continue;
            if (images[i].GetComponent<Button>() == null) continue;
            if (images[i].transform == transform || images[i].transform.IsChildOf(transform)) continue;

            return images[i].sprite;
        }

        return null;
    }

    private void BuildHeading(Transform root)
    {
        TextMeshProUGUI title = CreateText("Title", root, headingSize, inkColor, TextAlignmentOptions.Center);
        title.enableWordWrapping = false;
        title.text = titleText;
        title.fontStyle = FontStyles.Bold;
        title.characterSpacing = 4f;
        PlaceTop(root, title.rectTransform, 0f, headingTopY, safeSize.x, headingHeight);

        // Top-right, beside the tabs: the foot of the window is where the table's last rows are, and this
        // keeps the whole height for them.
        backButton = CreatePlateButton("Back", root, backText, backButtonSize, 16f);
        PlaceTop(root, (RectTransform)backButton.transform, safeSize.x - backButtonSize.x, backTopY,
            backButtonSize.x, backButtonSize.y);
        backButton.onClick.AddListener(CloseFromButton);
    }

    private void CloseFromButton()
    {
        if (owner != null) owner.CloseControls();
    }

    private void BuildTabs(Transform root)
    {
        float totalWidth = tabSize.x * 2f + tabGap;
        float left = (safeSize.x - totalWidth) * 0.5f;

        // Named after what they show, because the pause panel's MenuNavigation starts on a button by name.
        controlsTab = CreateTab(root, "Controls", controlsTabText, left, true);
        settingsTab = CreateTab(root, "Settings", settingsTabText, left + tabSize.x + tabGap, false);
    }

    private Button CreateTab(Transform root, string name, string label, float x, bool showsControls)
    {
        Button button = CreatePlateButton(name, root, label, tabSize, 16f);
        PlaceTop(root, (RectTransform)button.transform, x, tabTopY, tabSize.x, tabSize.y);

        TextMeshProUGUI text = button.GetComponentInChildren<TextMeshProUGUI>();
        text.characterSpacing = 3f;

        // The chosen tab is shown by the bar and the lettering, never by the plate: ButtonFocusEffect already
        // owns the plate's brightness, and two things writing to it would fight.
        Image bar = CreateImage("Selected", button.transform, inkColor);
        RectTransform barRect = bar.rectTransform;
        barRect.anchorMin = new Vector2(0f, 0f);
        barRect.anchorMax = new Vector2(1f, 0f);
        barRect.pivot = new Vector2(0.5f, 0f);
        barRect.sizeDelta = new Vector2(0f, 4f);
        barRect.anchoredPosition = Vector2.zero;

        button.onClick.AddListener(() => ShowControls(showsControls));

        tabs.Add(new Tab { showsControls = showsControls, label = text, activeBar = bar, button = button });

        return button;
    }

    private void BuildControlsPage(RectTransform page)
    {
        // Laid out downwards: y is the top of the next row, and each builder returns where the row below it
        // starts. The whole table fits the safe box with room to spare - see the check in .checktmp.
        float y = contentTopY;

        // The two device columns are named once, at the top, rather than over every group.
        CreateLabel(page, "Keyboard Header", keyboardHeaderText, 16f, faintInkColor, keyboardColumnX, y,
            keyboardColumnWidth, TextAlignmentOptions.TopLeft);
        CreateLabel(page, "Gamepad Header", gamepadHeaderText, 16f, faintInkColor, gamepadColumnX, y,
            gamepadColumnWidth, TextAlignmentOptions.TopLeft);

        y += columnHeaderHeight;

        ControlGroup[] groups = ControlBindings.All;

        for (int g = 0; g < groups.Length; g++)
        {
            ControlGroup group = groups[g];
            CreateLabel(page, "Caption - " + group.title, group.title, 20f, inkColor, 0f, y, safeSize.x,
                TextAlignmentOptions.TopLeft, true);

            y += captionHeight;

            if (group.bindings != null)
            {
                for (int i = 0; i < group.bindings.Length; i++)
                    y = BuildBindingRow(page, group.bindings[i], y);
            }

            y += groupGap;
        }
    }

    /// <summary>One line of the table: the action on the left, then what to press on each device.</summary>
    private float BuildBindingRow(Transform page, ControlBinding binding, float y)
    {
        RectTransform row = CreateRect("Row - " + binding.action, page);
        PlaceTop(page, row, 0f, y, safeSize.x, rowHeight);

        Image plate = row.gameObject.AddComponent<Image>();
        plate.color = rowPlateColor;
        plate.raycastTarget = false;

        CreateCell(row, "Action", binding.action, 20f, inkColor, rowInset, actionColumnWidth, TextAlignmentOptions.Left);
        CreateCell(row, "Keyboard", binding.keyboard, 20f, dimInkColor,
            keyboardColumnX, keyboardColumnWidth, TextAlignmentOptions.Left);
        CreateCell(row, "Gamepad", binding.gamepad, 20f, dimInkColor,
            gamepadColumnX, gamepadColumnWidth, TextAlignmentOptions.Left);

        return y + rowHeight + rowGap;
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

        // Stops short of the note beside it, so the two never share a line's worth of space.
        CreateLabel(page, "Caption - graphics", graphicsCaption, 20f, inkColor, 0f, y, noteX - 20f,
            TextAlignmentOptions.TopLeft, true);

        y += captionHeight + 6f;

        // The presets, as a row of choices with the one in force's lettering darkened.
        BuildChoiceRow(page, y, presetRows, PresetName, ChoosePreset);

        y += presetButtonSize.y + 16f;

        // The two switches sit on top of the preset rather than belonging to it: turning shadows off at HIGH,
        // or the blur on at LOW, is allowed.
        shadowsButton = CreatePlateButton("Shadows", page, shadowsText, switchSize, 18f);
        PlaceTop(page, (RectTransform)shadowsButton.transform, 0f, y, switchSize.x, switchSize.y);
        shadowsLabel = shadowsButton.GetComponentInChildren<TextMeshProUGUI>();
        shadowsButton.onClick.AddListener(ToggleShadows);

        y += switchSize.y + switchGap;

        blurButton = CreatePlateButton("Motion Blur", page, motionBlurText, switchSize, 18f);
        PlaceTop(page, (RectTransform)blurButton.transform, 0f, y, switchSize.x, switchSize.y);
        blurLabel = blurButton.GetComponentInChildren<TextMeshProUGUI>();
        blurButton.onClick.AddListener(ToggleMotionBlur);

        y += switchSize.y + 14f;

        // The difficulty, under everything the picture is made of: it is the one choice here that changes what
        // a level asks of the player rather than what it looks like.
        CreateLabel(page, "Caption - difficulty", difficultyCaption, 20f, inkColor, 0f, y, noteX - 20f,
            TextAlignmentOptions.TopLeft, true);

        y += captionHeight + 6f;

        BuildChoiceRow(page, y, difficultyRows, DifficultyName, ChooseDifficulty);

        BuildReloadPrompt(page);
    }

    /// <summary>
    /// A row of choices with the one in force's lettering darkened. The presets and the difficulties are the
    /// same row in every way but what they choose and how they are named, so both are built by this rather
    /// than by two copies of it.
    /// </summary>
    private void BuildChoiceRow(Transform page, float y, List<ChoiceRow> rows, Func<int, string> nameOf,
        Action<int> choose)
    {
        for (int i = 0; i < 3; i++)
        {
            int index = i;                          // captured, not the loop variable, for the callback
            string label = nameOf(index);

            Button button = CreatePlateButton(label, page, label, presetButtonSize, 18f);
            PlaceTop(page, (RectTransform)button.transform, i * (presetButtonSize.x + presetGap), y,
                presetButtonSize.x, presetButtonSize.y);

            TextMeshProUGUI text = button.GetComponentInChildren<TextMeshProUGUI>();
            text.characterSpacing = 3f;

            Image bar = CreateImage("Selected", button.transform, inkColor);
            RectTransform barRect = bar.rectTransform;
            barRect.anchorMin = new Vector2(0f, 0f);
            barRect.anchorMax = new Vector2(1f, 0f);
            barRect.pivot = new Vector2(0.5f, 0f);
            barRect.sizeDelta = new Vector2(0f, 4f);
            barRect.anchoredPosition = Vector2.zero;

            button.onClick.AddListener(() => choose(index));

            rows.Add(new ChoiceRow { index = index, button = button, label = text, activeBar = bar });
        }
    }

    /// <summary>The preset's name, as GraphicsQuality defines it: Low, Medium, High.</summary>
    private static string PresetName(int index)
    {
        GraphicsPreset preset = (GraphicsPreset)Mathf.Clamp(index, 0, 2);
        return preset.ToString().ToUpperInvariant();
    }

    /// <summary>The difficulty's name, as GameDifficulty defines it: Easy, Medium, Hard.</summary>
    private static string DifficultyName(int index)
    {
        DifficultyLevel level = (DifficultyLevel)Mathf.Clamp(index, 0, 2);
        return level.ToString().ToUpperInvariant();
    }

    /// <summary>
    /// The plate beside the settings: the one thing the player needs to know about a choice - that it lands
    /// straight away - rather than a description of what the presets do, which is what the picture in front
    /// of them is for.
    /// </summary>
    private void BuildSettingsNote(RectTransform page)
    {
        TextMeshProUGUI body = CreateText("Settings Note", page, 16f, dimInkColor, TextAlignmentOptions.TopLeft);
        body.text = settingsNoteText;
        body.lineSpacing = 6f;
        PlaceTop(page, body.rectTransform, noteX, contentTopY, noteWidth, 44f);
    }

    /// <summary>
    /// The restart prompt: shown while the level that is open is drawing a different amount of ground detail
    /// from the one the chosen preset asks for, which is the only part of a preset a level cannot pick up
    /// while it is running.
    /// </summary>
    private void BuildReloadPrompt(Transform page)
    {
        reloadPrompt = CreateRect("Reload Prompt", page);
        PlaceTop(page, reloadPrompt, 0f, promptTopY, safeSize.x, promptHeight);

        Image plate = reloadPrompt.gameObject.AddComponent<Image>();
        plate.color = promptPlateColor;
        plate.raycastTarget = false;

        // Both of these are children of the prompt plate, so they go away with it.
        promptLabel = CreateText("Prompt", reloadPrompt, 16f, inkColor, TextAlignmentOptions.Left);
        promptLabel.text = reloadPromptText;
        PlaceTop(reloadPrompt, promptLabel.rectTransform, 18f, 0f, safeSize.x - reloadButtonSize.x - 60f, promptHeight);

        reloadButton = CreatePlateButton("Restart Level", reloadPrompt, reloadButtonText, reloadButtonSize, 16f);
        PlaceTop(reloadPrompt, (RectTransform)reloadButton.transform, safeSize.x - reloadButtonSize.x - 12f,
            (promptHeight - reloadButtonSize.y) * 0.5f, reloadButtonSize.x, reloadButtonSize.y);
        reloadButton.onClick.AddListener(ReloadLevel);

        reloadPrompt.gameObject.SetActive(false);
    }

    private void BuildActions(Transform root)
    {
        // The tab row is the one place Unity's own guess reads badly: the page below a tab is not where the
        // nearest button happens to be, so each tab is pointed at the first thing on the page it opens. Every
        // other button keeps automatic navigation, which skips whatever is switched off and so follows the
        // restart prompt in and out on its own. The controls page has nothing selectable below its tab - it is
        // a table, not a list of buttons - so down is left empty there and BACK is the one thing above.
        SetNavigation(controlsTab, null, settingsTab, null, backButton);
        SetNavigation(settingsTab, controlsTab, null, presetRows.Count > 0 ? presetRows[0].button : backButton, backButton);
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

    private TextMeshProUGUI CreateLabel(Transform parent, string name, string text, float size, Color colour,
        float x, float y, float width, TextAlignmentOptions alignment, bool bold = false)
    {
        TextMeshProUGUI label = CreateText(name, parent, size, colour, alignment);
        label.text = text;
        label.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;

        PlaceTop(parent, label.rectTransform, x, y, width, size * 1.5f);

        return label;
    }

    /// <summary>
    /// A button wearing the pause menu's own plate, so it reads as part of the window it sits in. Being an
    /// ordinary Button it gets the project's hover and focus reaction for free.
    /// </summary>
    private Button CreatePlateButton(string name, Transform parent, string label, Vector2 size, float fontSize)
    {
        RectTransform rect = CreateRect(name, parent);

        Image plate = rect.gameObject.AddComponent<Image>();
        plate.sprite = plateSprite;
        plate.type = Image.Type.Simple;
        plate.color = plateSprite != null ? Color.white : promptPlateColor;
        plate.raycastTarget = true;

        Button button = rect.gameObject.AddComponent<Button>();

        ColorBlock colours = button.colors;
        colours.normalColor = Color.white;
        colours.highlightedColor = new Color(0.94f, 0.94f, 0.94f, 1f);
        colours.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        colours.selectedColor = Color.white;
        colours.fadeDuration = 0.1f;
        button.colors = colours;
        button.targetGraphic = plate;

        TextMeshProUGUI text = CreateText("Label", rect, fontSize, inkColor, TextAlignmentOptions.Center);
        text.text = label;
        text.characterSpacing = 2f;
        Stretch(text.rectTransform);

        return button;
    }

    /// <summary>Pins a rect to a point measured from the top-left corner of whatever holds it.</summary>
    private static void PlaceTop(Transform parent, RectTransform rect, float x, float y, float width, float height)
    {
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
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
}
