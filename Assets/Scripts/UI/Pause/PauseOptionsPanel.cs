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
/// The two tabs are the same two as the options screen: CONTROLS, which is the table from
/// <see cref="ControlBindings"/>; and SETTINGS, which holds everything that is chosen - <see cref="GraphicsQuality"/>,
/// <see cref="GameDifficulty"/>, and <see cref="SoundSettings"/> with its five channels and the switch that
/// keeps the engine in the same place in the mix whichever camera the player drives from. The sound rows are
/// built from that class rather than written out here, so this page and the main menu's are the same five rows
/// and a channel added there turns up in both. Sound is a group of the settings rather than a tab of its own,
/// here as there: what the game looks like and what it sounds like are both some setting or other.
///
/// The settings page carries more than the main menu's does - the window is small, and the sound rows have to
/// share it with the graphics and the difficulty - so its controls are a size down, and the restart prompt
/// lives just under the content box, in the room the plate's own border leaves. That keeps every row the same
/// height as the others and the prompt where it can always be seen, instead of the page having to be built
/// around a plate that is only sometimes there.
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
    public string soundCaption = "SOUND";
    public string cameraMixText = "CAMERA MIX";

    [Tooltip("The line beside the camera mix switch, saying what it does in one line.")]
    public string cameraMixNoteText = "Keeps the engine steady from any camera.";

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
    public string dismissButtonText = "KEEP PLAYING";

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
    public Vector2 safeSize = new Vector2(800f, 540f);
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
    public float contentTopY = 94f;

    [Header("Layout - the controls table")]
    public float columnHeaderHeight = 18f;
    public float captionHeight = 27f;
    public float rowHeight = 27f;
    public float rowGap = 2f;
    public float groupGap = 8f;
    public float rowInset = 10f;
    public float actionColumnWidth = 195f;
    public float keyboardColumnX = 205f;
    public float keyboardColumnWidth = 300f;
    public float gamepadColumnX = 515f;
    public float gamepadColumnWidth = 280f;

    [Header("Layout - the sound rows")]
    public Vector2 soundButtonSize = new Vector2(120f, 26f);
    public float soundButtonGap = 8f;
    public float soundRowGap = 3f;
    [Tooltip("How much room the channel's own name is given, before its steps start.")]
    public float soundLabelWidth = 200f;
    public float soundFirstButtonX = 220f;
    [Tooltip("The camera mix's switch on the sound caption's own line, and its note beside that.")]
    public Vector2 cameraMixSize = new Vector2(260f, 32f);
    public float cameraMixX = 130f;
    public float cameraMixNoteX = 400f;

    [Header("Layout - the settings tab")]
    public Vector2 presetButtonSize = new Vector2(155f, 38f);
    public float presetGap = 10f;
    public Vector2 switchSize = new Vector2(300f, 32f);
    public float switchGap = 4f;
    public float noteX = 505f;
    public float noteWidth = 285f;

    [Header("Layout - the restart prompt (centred on the window, over a scrim)")]
    [Tooltip("The plate the message and its two buttons sit on.")]
    public Vector2 promptSize = new Vector2(700f, 220f);
    public Vector2 reloadButtonSize = new Vector2(210f, 44f);
    public float promptButtonGap = 20f;
    [Tooltip("How dark the page goes behind the prompt. Enough that the rows underneath read as covered " +
             "rather than as competing with it.")]
    [Range(0f, 1f)] public float promptScrimAlpha = 0.62f;

    // ---------------------------------------------------------------- state

    private PauseMenu owner;
    private RectTransform window;
    private RectTransform content;

    private RectTransform controlsPage;
    private RectTransform settingsPage;

    private readonly List<Tab> tabs = new List<Tab>();
    private readonly List<ChoiceRow> presetRows = new List<ChoiceRow>();
    private readonly List<ChoiceRow> difficultyRows = new List<ChoiceRow>();
    private readonly List<ChannelRow> soundRows = new List<ChannelRow>();

    private Button controlsTab;
    private Button settingsTab;
    private Button backButton;
    private Button shadowsButton;
    private Button blurButton;
    private Button cameraMixButton;
    private Button reloadButton;
    private Button dismissButton;
    private GameObject promptScrim;

    /// <summary>Every button the panel owns apart from the prompt's own - the page the prompt covers.</summary>
    private readonly List<Selectable> pageButtons = new List<Selectable>();

    // Whether the restart prompt has been answered with "keep playing". Cleared when the setting goes back to
    // what the level already is, so the next change arms the prompt again, and when the panel is reopened.
    private bool promptDismissed;

    private TextMeshProUGUI shadowsLabel;
    private TextMeshProUGUI blurLabel;
    private TextMeshProUGUI cameraMixLabel;
    private TextMeshProUGUI promptLabel;

    private RectTransform reloadPrompt;

    private PausePage page = PausePage.Controls;
    private bool open;

    /// <summary>The plate the pause buttons are drawn with, borrowed from the window itself.</summary>
    private Sprite plateSprite;

    private readonly List<GameObject> hidden = new List<GameObject>();
    private readonly List<bool> hiddenState = new List<bool>();

    /// <summary>Which of the panel's two pages is on show. Only ever one of them.</summary>
    private enum PausePage
    {
        Controls = 0,
        Settings = 1,
    }

    private sealed class Tab
    {
        public PausePage shows;
        public TextMeshProUGUI label;
        public Image activeBar;
        public Button button;
    }

    /// <summary>
    /// One choice of a row: a preset, a difficulty, or one step of a sound channel. All three rows are built
    /// and refreshed alike, and they are lit the same way - the lettering darkens and the bar under it shows.
    /// </summary>
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
        SoundSettings.Changed += Refresh;

        // A reopened panel shows what is out of date again, whatever was answered last time.
        if (promptLabel != null) promptDismissed = false;

        Refresh();

        BeginFromTabs();
    }

    private void OnDisable()
    {
        GraphicsQuality.Changed -= Refresh;
        GameDifficulty.Changed -= Refresh;
        SoundSettings.Changed -= Refresh;
    }

    private void BeginFromTabs()
    {
        EventSystem events = EventSystem.current;
        if (events == null || controlsTab == null) return;

        ShowPage(PausePage.Controls);

        // A prompt that is already up is what the highlight belongs on: it is a dialog, and it is the only
        // thing on the panel that can be used while it is showing.
        bool promptUp = reloadPrompt != null && reloadPrompt.gameObject.activeSelf && reloadButton != null;

        // Clearing it first is deliberate: the pause panel's own MenuNavigation is still holding the button
        // this panel just took away, and letting go of it is what makes that component re-read the buttons
        // that are here now. Left alone it would go on protecting a selection that is no longer on screen.
        // It also leaves the highlight on the tab row, which is where its own guess lands too - the top-most
        // button is the tab row here, not the BACK button in the corner.
        events.SetSelectedGameObject(null);
        events.SetSelectedGameObject(promptUp ? reloadButton.gameObject : controlsTab.gameObject);

        ButtonFocusEffect.HoldHighlightOnSelection(0.35f);
    }

    // ---------------------------------------------------------------- tabs

    /// <summary>
    /// Puts one page on screen. The other is switched off rather than hidden, so its buttons are not merely
    /// unseen but unselectable - which is what keeps a keyboard or a gamepad from wandering onto them.
    /// </summary>
    private void ShowPage(PausePage show)
    {
        page = show;

        if (controlsPage != null) controlsPage.gameObject.SetActive(show == PausePage.Controls);
        if (settingsPage != null) settingsPage.gameObject.SetActive(show == PausePage.Settings);

        RefreshTabs();
    }

    private void RefreshTabs()
    {
        for (int i = 0; i < tabs.Count; i++)
        {
            bool active = tabs[i].shows == page;

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

    private void ChooseLevel(int channel, int step)
    {
        SoundSettings.SetLevel(channel, (SoundLevel)step);
    }

    private void ToggleCameraMix()
    {
        SoundSettings.SetCameraMix(!SoundSettings.CameraMix);
    }

    private void ReloadLevel()
    {
        if (owner != null) owner.RestartLevel();
    }

    /// <summary>
    /// Answers the prompt with "keep playing": it goes away, and the setting stays out of date until the
    /// level is started again - from the pause menu, from the level's own button, or by replaying it.
    /// </summary>
    private void DismissPrompt()
    {
        promptDismissed = true;

        ShowPrompt(false);
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

        // The sound rows are all live: a change is heard the moment it is made, which is what makes them worth
        // having in the pause menu at all - the engine can be turned down without leaving the level.
        // The step lit is the one the channel is really playing at, not the one the player chose for it: the
        // master caps the rest, so its row moving shows up on every row below it - and each channel's own
        // choice comes back the moment the master lets it.
        for (int i = 0; i < soundRows.Count; i++)
        {
            ChannelRow row = soundRows[i];
            int level = (int)SoundSettings.EffectiveLevel(row.channel);

            row.label.color = level > 0 ? inkColor : dimInkColor;

            for (int s = 0; s < row.steps.Length; s++)
            {
                bool active = s == level;

                row.steps[s].activeBar.enabled = active;
                row.steps[s].label.color = active ? inkColor : dimInkColor;
                row.steps[s].label.fontStyle = active ? FontStyles.Bold : FontStyles.Normal;
            }
        }

        if (cameraMixLabel != null)
        {
            cameraMixLabel.text = cameraMixText + "   " + (SoundSettings.CameraMix ? onText : offText);
            cameraMixLabel.color = SoundSettings.CameraMix ? inkColor : dimInkColor;
        }

        // Two choices here have to wait for a level to be started again before it can honour them: the amount
        // of ground detail its terrain draws, which it builds as it loads, and the difficulty, whose time
        // limit and kill requirement it reads as its HUD starts. The offer to restart appears exactly while
        // one of them really is out of date and goes away when the player picks the value the level already
        // has - including from the prompt itself, where choosing the value the level has makes it close.
        bool graphicsOutOfDate = GraphicsQuality.ActiveSceneNeedsReload();
        bool difficultyOutOfDate = GameDifficulty.ActiveSceneNeedsReload();

        if (promptLabel != null)
            promptLabel.text = PromptText(graphicsOutOfDate, difficultyOutOfDate);

        bool needed = graphicsOutOfDate || difficultyOutOfDate;

        // Nothing is out of date any more - the player may have chosen the value the level already has, from
        // the prompt itself - so the prompt is not being kept away, it is simply not needed.
        if (!needed) promptDismissed = false;

        ShowPrompt(needed && !promptDismissed);
    }

    /// <summary>
    /// Brings the restart prompt on or off. It is a dialog rather than a line in the page: it comes up in the
    /// middle of the window over a scrim, and while it is up it is the only thing on the panel that can be
    /// used - the rows behind it are switched off as well as covered, so nothing under the scrim can be
    /// clicked or landed on, and the pointer's click is kept off the page it is hiding.
    ///
    /// The selection is handed to the prompt's own button as it opens, and the panel's
    /// <see cref="MenuNavigation"/> - which re-reads the buttons it manages whenever the selection leaves its
    /// list - is what hands it back when the prompt closes and that button is switched off again.
    /// </summary>
    private void ShowPrompt(bool show)
    {
        if (reloadPrompt == null) return;
        if (reloadPrompt.gameObject.activeSelf == show) return;

        reloadPrompt.gameObject.SetActive(show);

        if (promptScrim != null) promptScrim.SetActive(show);

        // The page behind is switched off as well as dimmed. A dialog that can be clicked through is not one.
        for (int i = 0; i < pageButtons.Count; i++)
        {
            if (pageButtons[i] != null) pageButtons[i].interactable = !show;
        }

        if (!show) return;

        EventSystem events = EventSystem.current;
        if (events == null || reloadButton == null) return;

        events.SetSelectedGameObject(null);
        events.SetSelectedGameObject(reloadButton.gameObject);

        ButtonFocusEffect.HoldHighlightOnSelection(0.25f);
    }

    /// <summary>
    /// What the restart prompt says. Restarting is the answer whatever the reason - the two settings are both
    /// applied by the same restart - so the wording is the only thing that changes with it.
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

        // Everything the panel owns apart from the prompt's own buttons. The prompt switches these off while
        // it is up, which is what keeps the scrim from being something a player can click through - and what
        // makes the pause window's MenuNavigation hand the highlight to the prompt as it opens.
        pageButtons.Clear();

        foreach (Selectable selectable in content.GetComponentsInChildren<Selectable>(true))
            pageButtons.Add(selectable);
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
        float step = tabSize.x + tabGap;

        // Named after what they show, because the pause panel's MenuNavigation starts on a button by name.
        controlsTab = CreateTab(root, "Controls", controlsTabText, left, PausePage.Controls);
        settingsTab = CreateTab(root, "Settings", settingsTabText, left + step, PausePage.Settings);
    }

    private Button CreateTab(Transform root, string name, string label, float x, PausePage shows)
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

        button.onClick.AddListener(() => ShowPage(shows));

        tabs.Add(new Tab { shows = shows, label = text, activeBar = bar, button = button });

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

        BuildSettingsNotes(page);

        // Stops short of the notes beside it, so the two never share a line's worth of space.
        CreateLabel(page, "Caption - graphics", graphicsCaption, 18f, inkColor, 0f, y, noteX - 20f,
            TextAlignmentOptions.TopLeft, true);

        y += captionHeight + 6f;

        // The presets, as a row of choices with the one in force's lettering darkened.
        BuildChoiceRow(page, y, presetRows, PresetName, ChoosePreset);

        y += presetButtonSize.y + 16f;

        // The two switches sit on top of the preset rather than belonging to it: turning shadows off at HIGH,
        // or the blur on at LOW, is allowed.
        shadowsButton = CreatePlateButton("Shadows", page, shadowsText, switchSize, 16f);
        PlaceTop(page, (RectTransform)shadowsButton.transform, 0f, y, switchSize.x, switchSize.y);
        shadowsLabel = shadowsButton.GetComponentInChildren<TextMeshProUGUI>();
        shadowsButton.onClick.AddListener(ToggleShadows);

        y += switchSize.y + switchGap;

        blurButton = CreatePlateButton("Motion Blur", page, motionBlurText, switchSize, 16f);
        PlaceTop(page, (RectTransform)blurButton.transform, 0f, y, switchSize.x, switchSize.y);
        blurLabel = blurButton.GetComponentInChildren<TextMeshProUGUI>();
        blurButton.onClick.AddListener(ToggleMotionBlur);

        y += switchSize.y + 14f;

        // The difficulty, under everything the picture is made of: it is the one choice here that changes what
        // a level asks of the player rather than what it looks like.
        CreateLabel(page, "Caption - difficulty", difficultyCaption, 18f, inkColor, 0f, y, noteX - 20f,
            TextAlignmentOptions.TopLeft, true);

        y += captionHeight + 6f;

        BuildChoiceRow(page, y, difficultyRows, DifficultyName, ChooseDifficulty);

        y += presetButtonSize.y + 10f;

        // The sound is the last group rather than a page of its own: it is a setting like the other two, and
        // the tab row is for what the player came here to see, not for every kind of choice.
        BuildSoundGroup(page, y);

        BuildReloadPrompt();
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
    /// The line beside the settings: the one thing the player needs to know about a choice - that it lands
    /// straight away - rather than a description of what the presets do, which is what the picture in front
    /// of them is for. It is out of the left column's way for the whole length of the page, so no row below
    /// has to stop short of it.
    /// </summary>
    private void BuildSettingsNotes(RectTransform page)
    {
        TextMeshProUGUI body = CreateText("Settings Note", page, 16f, dimInkColor, TextAlignmentOptions.TopLeft);
        body.text = settingsNoteText;
        body.lineSpacing = 6f;
        PlaceTop(page, body.rectTransform, noteX, contentTopY, noteWidth, 44f);
    }

    /// <summary>
    /// The restart prompt: shown while the level that is open would be played differently if it were started
    /// again - it is drawing another amount of ground detail, or it was built with another difficulty - so
    /// that the setting is not quietly doing nothing until the player happens to replay the level.
    ///
    /// It is a dialog in the middle of the window rather than a line in the page: a plate on the pause menu's
    /// own artwork, over a scrim, with the two answers a player can give - restart now, or keep playing and
    /// let it land when the level is next started. The page behind is switched off while it is up (see
    /// <see cref="ShowPrompt"/>), so nothing under the scrim can be pressed or landed on by mistake.
    /// </summary>
    private void BuildReloadPrompt()
    {
        // The scrim goes down first, so the plate below is drawn - and clicked - over the top of it.
        Image scrim = CreateImage("Prompt Scrim", transform, new Color(0f, 0f, 0f, promptScrimAlpha));
        Stretch(scrim.rectTransform);
        scrim.raycastTarget = true;
        promptScrim = scrim.gameObject;
        promptScrim.SetActive(false);

        reloadPrompt = CreateRect("Reload Prompt", transform);
        reloadPrompt.anchorMin = new Vector2(0.5f, 0.5f);
        reloadPrompt.anchorMax = new Vector2(0.5f, 0.5f);
        reloadPrompt.pivot = new Vector2(0.5f, 0.5f);
        reloadPrompt.anchoredPosition = Vector2.zero;
        reloadPrompt.sizeDelta = promptSize;

        // The plate wears the pause window's own artwork, so the dialog reads as part of the pause menu rather
        // than as something that came from a different screen.
        Image plate = reloadPrompt.gameObject.AddComponent<Image>();
        plate.sprite = plateSprite;
        plate.type = Image.Type.Simple;
        plate.color = plateSprite != null ? Color.white : promptPlateColor;
        plate.raycastTarget = true;

        promptLabel = CreateText("Prompt", reloadPrompt, 19f, inkColor, TextAlignmentOptions.Center);
        promptLabel.text = reloadPromptText;
        promptLabel.lineSpacing = 8f;
        PlaceMiddle(reloadPrompt, promptLabel.rectTransform, 0f, 44f, promptSize.x - 80f, promptSize.y * 0.5f - 24f);

        // Keep playing on the left and restart on the right, the way a dialog's answers read - and the prompt
        // hands its highlight to restart as it opens (see ShowPrompt), which is the answer it is there to offer.
        float half = (reloadButtonSize.x + promptButtonGap) * 0.5f;
        float rowY = -(promptSize.y * 0.5f - reloadButtonSize.y * 0.5f - 34f);

        dismissButton = CreatePlateButton("Keep Playing", reloadPrompt, dismissButtonText, reloadButtonSize, 16f);
        PlaceMiddle(reloadPrompt, (RectTransform)dismissButton.transform, -half, rowY,
            reloadButtonSize.x, reloadButtonSize.y);
        dismissButton.onClick.AddListener(DismissPrompt);

        reloadButton = CreatePlateButton("Restart Level", reloadPrompt, reloadButtonText, reloadButtonSize, 16f);
        PlaceMiddle(reloadPrompt, (RectTransform)reloadButton.transform, half, rowY,
            reloadButtonSize.x, reloadButtonSize.y);
        reloadButton.onClick.AddListener(ReloadLevel);

        SetNavigation(dismissButton, null, reloadButton, null, null);
        SetNavigation(reloadButton, dismissButton, null, null, null);

        reloadPrompt.gameObject.SetActive(false);
    }

    /// <summary>
    /// The sound group of the settings page: a caption with the camera mix switch and its note beside it, then
    /// one row per channel.
    ///
    /// The rows are built from <see cref="SoundSettings"/> rather than written out here, which is what makes
    /// this page and the main menu's the same rows - the same channels, the same steps, the same where they are
    /// kept - and what makes a channel added there turn up in both.
    /// </summary>
    private void BuildSoundGroup(RectTransform page, float y)
    {
        // The caption's own line carries the camera mix as well: it is not a volume, it is the one tweak on top
        // of what the ENGINE row says, so it reads as part of the group's heading rather than as a sixth
        // channel - and it costs the rows below it no room at all on a page that has none to spare.
        float captionLineHeight = 18f * 1.5f;

        // The caption's own box stops short of the switch beside it, so the two never share a line's space.
        CreateLabel(page, "Caption - sound", soundCaption, 18f, inkColor, 0f, y, cameraMixX - 10f,
            TextAlignmentOptions.TopLeft, true);

        cameraMixButton = CreatePlateButton("Camera Mix", page, cameraMixText, cameraMixSize, 15f);
        PlaceTop(page, (RectTransform)cameraMixButton.transform, cameraMixX, y, cameraMixSize.x, cameraMixSize.y);
        cameraMixLabel = cameraMixButton.GetComponentInChildren<TextMeshProUGUI>();
        cameraMixButton.onClick.AddListener(ToggleCameraMix);

        TextMeshProUGUI note = CreateText("Camera Mix Note", page, 15f, faintInkColor, TextAlignmentOptions.TopLeft);
        note.text = cameraMixNoteText;
        note.lineSpacing = 4f;
        PlaceTop(page, note.rectTransform, cameraMixNoteX, y + (cameraMixSize.y - 15f * 1.5f) * 0.5f,
            safeSize.x - cameraMixNoteX, captionLineHeight);

        // Whatever this heading row takes - the switch is taller than the lettering - the channels start below
        // all of it.
        y += Mathf.Max(captionLineHeight, cameraMixSize.y) + 10f;

        for (int channel = 0; channel < SoundSettings.ChannelCount; channel++)
            y = BuildSoundRow(page, channel, y);
    }

    /// <summary>
    /// One channel of the sound group: its name on the left, then the game's steps, with the one in force's
    /// lettering darkened. Returns where the row below it starts.
    /// </summary>
    private float BuildSoundRow(Transform page, int channel, float y)
    {
        // The name is a label rather than a button: it heads the row, and the steps beside it want every pixel
        // of the width. It is nudged down to sit on the steps' centre line rather than their top edge.
        TextMeshProUGUI name = CreateLabel(page, "Channel - " + SoundSettings.ChannelNames[channel],
            SoundSettings.ChannelNames[channel], 18f, inkColor, 0f,
            y + (soundButtonSize.y - 18f * 1.5f) * 0.5f, soundLabelWidth, TextAlignmentOptions.Left);

        ChannelRow row = new ChannelRow
        {
            channel = channel,
            label = name,
            steps = new ChoiceRow[SoundSettings.StepCount],
        };

        for (int step = 0; step < SoundSettings.StepCount; step++)
        {
            int chosen = step;              // captured, not the loop variable, for the callback

            Button button = CreatePlateButton(SoundSettings.ChannelNames[channel] + " " + SoundSettings.StepNames[step],
                page, SoundSettings.StepNames[step], soundButtonSize, 15f);

            PlaceTop(page, (RectTransform)button.transform,
                soundFirstButtonX + step * (soundButtonSize.x + soundButtonGap), y,
                soundButtonSize.x, soundButtonSize.y);

            TextMeshProUGUI text = button.GetComponentInChildren<TextMeshProUGUI>();
            text.characterSpacing = 2f;

            Image bar = CreateImage("Selected", button.transform, inkColor);
            RectTransform barRect = bar.rectTransform;
            barRect.anchorMin = new Vector2(0f, 0f);
            barRect.anchorMax = new Vector2(1f, 0f);
            barRect.pivot = new Vector2(0.5f, 0f);
            barRect.sizeDelta = new Vector2(0f, 4f);
            barRect.anchoredPosition = Vector2.zero;

            button.onClick.AddListener(() => ChooseLevel(channel, chosen));

            row.steps[step] = new ChoiceRow { index = step, button = button, label = text, activeBar = bar };
        }

        soundRows.Add(row);

        return y + soundButtonSize.y + soundRowGap;
    }

    private void BuildActions(Transform root)
    {
        // The tab row is the one place Unity's own guess reads badly: the page below a tab is not where the
        // nearest button happens to be, so each tab is pointed at the first thing on the page it opens. Every
        // other button is wired as the list it looks like - the preset row, the two switches, the difficulty
        // row, the camera mix and the five sound rows - because these plates are small enough that a
        // nearest-neighbour guess lands on the wrong one. The controls page has nothing selectable on it - it
        // is a table, not a list of buttons - so down is left empty there and BACK is the one thing above.
        int middleStep = SoundSettings.StepCount / 2;
        Button middleFirstSound = soundRows.Count > 0 ? soundRows[0].steps[middleStep].button : null;
        Button middlePreset = presetRows.Count > 1 ? presetRows[presetRows.Count / 2].button : null;
        Button middleDifficulty = difficultyRows.Count > 1 ? difficultyRows[difficultyRows.Count / 2].button : null;

        SetNavigation(controlsTab, null, settingsTab, null, backButton);
        SetNavigation(settingsTab, controlsTab, null, middlePreset, backButton);

        for (int i = 0; i < presetRows.Count; i++)
        {
            Button left = i > 0 ? presetRows[i - 1].button : null;
            Button right = i < presetRows.Count - 1 ? presetRows[i + 1].button : null;

            SetNavigation(presetRows[i].button, left, right, shadowsButton, settingsTab);
        }

        SetNavigation(shadowsButton, null, null, blurButton, middlePreset);
        SetNavigation(blurButton, null, null, middleDifficulty, shadowsButton);

        for (int i = 0; i < difficultyRows.Count; i++)
        {
            Button left = i > 0 ? difficultyRows[i - 1].button : null;
            Button right = i < difficultyRows.Count - 1 ? difficultyRows[i + 1].button : null;

            SetNavigation(difficultyRows[i].button, left, right,
                middleFirstSound != null ? middleFirstSound : cameraMixButton, blurButton);
        }

        // The sound rows are a grid of twenty small plates, which is the shape a nearest-neighbour guess gets
        // wrong once the rows are this narrow - so the columns and rows are wired as the table they look like.
        // Up from the top row is the camera mix above it, and down from the last row is BACK.
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

            if (i == soundRows.Count - 1) middleLastSound = row.steps[middleStep].button;
        }

        SetNavigation(cameraMixButton, null, null,
            middleLastSound != null ? middleLastSound : backButton,
            middleDifficulty != null ? middleDifficulty : blurButton);
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

    /// <summary>Puts a rect on its parent's centre, shifted by (<paramref name="x"/>, <paramref name="offsetY"/>).</summary>
    private static void PlaceMiddle(Transform parent, RectTransform rect, float x, float offsetY, float width, float height)
    {
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(x, offsetY);
        rect.sizeDelta = new Vector2(width, height);
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
