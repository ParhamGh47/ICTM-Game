using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Opens every level for testing, from inside the game rather than from the editor.
///
/// A level is only as far along as the player has played, which is right for a player and awkward for anyone
/// working through the levels: reaching level 4 means finishing 1, 2 and 3 first, every time a new build is made.
/// This puts a switch on the level list for that - bottom-left, out of the way of the tiles - which opens
/// everything at once, and puts the player's own progress back when it is switched off again.
///
/// It works entirely in a build: it needs no editor, no debug build and no code change, and it is remembered
/// between runs, so a testing session only has to be started once. The player's real progress is set aside
/// while it is on, not overwritten, so a build handed to somebody else is one switch away from normal.
///
/// What happens to progress while testing - a level finished with the switch on counts, and restoring
/// afterwards puts back the progress that was set aside, so that finish is left behind too. The switch is for
/// getting around, not for keeping anything.
///
/// The tab is built in code, like the graphics options panel, so no scene has to be edited for it to exist. To
/// take it out of a build entirely, delete this file, or set <see cref="Available"/> to false.
/// </summary>
[DisallowMultipleComponent]
public class LevelTestUnlock : MonoBehaviour
{
    /// <summary>The scene the switch lives on. The level list is where levels are chosen, so that is where it goes.</summary>
    public const string DefaultSceneName = "Levels";

    /// <summary>
    /// The switch, as a whole. False hides the tab and ignores the shortcut, which is the way to take this out
    /// of a build without deleting it.
    /// </summary>
    public static bool Available = true;

    // ---------------------------------------------------------------- storage

    // The player's own progress, put aside while the switch is on. Kept in PlayerPrefs beside the progress
    // itself so a build that is closed mid-testing still knows how to get back.
    private const string ActiveKey = "TestUnlock.Active";
    private const string SavedUnlockedKey = "TestUnlock.Saved.Unlocked";
    private const string SavedClearedKey = "TestUnlock.Saved.Cleared";

    // ---------------------------------------------------------------- settings

    [Header("Where it installs")]
    [Tooltip("Scene to put the switch in. Only one scene is the level list, so this is normally left alone.")]
    public string sceneName = DefaultSceneName;

    [Header("The tab")]
    [Tooltip("Placement, in 1920x1080 reference pixels, from the bottom-left corner of the screen.")]
    public Vector2 tabPosition = new Vector2(150f, 64f);
    public Vector2 tabSize = new Vector2(420f, 68f);

    [Tooltip("How far above the tab the status line sits.")]
    public float statusOffset = 8f;

    [Header("Keys")]
    [Tooltip("Toggle with the U key, or the top face button (Y / triangle) on a gamepad.")]
    public bool shortcutEnabled = true;

    [Header("Colours")]
    public Color tabColour = new Color(0.16f, 0.20f, 0.27f, 0.94f);
    public Color onColour = new Color(1f, 0.64f, 0.16f, 1f);
    public Color labelColour = new Color(0.88f, 0.90f, 0.95f, 1f);
    public Color statusColour = new Color(0.74f, 0.78f, 0.86f, 1f);

    private Button button;
    private TextMeshProUGUI buttonLabel;
    private TextMeshProUGUI statusLabel;

    // ---------------------------------------------------------------- install

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (!Application.isPlaying) return;

        SceneManager.sceneLoaded += OnSceneLoaded;

        Consider(SceneManager.GetActiveScene());
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Consider(scene);
    }

    private static void Consider(Scene scene)
    {
        if (!Available) return;
        if (!scene.IsValid()) return;

        if (!string.Equals(scene.name, DefaultSceneName, System.StringComparison.OrdinalIgnoreCase)) return;

        // A list that already carries one - put there by hand, with its own settings - is left alone.
        if (Object.FindObjectOfType<LevelTestUnlock>() != null) return;

        GameObject go = new GameObject("Level Test Unlock");
        SceneManager.MoveGameObjectToScene(go, scene);
        go.AddComponent<LevelTestUnlock>();
    }

    private void Awake()
    {
        Build();

        // A session that was left switched on comes back switched on, and the levels are made sure of -
        // something else may have put the progress back in between.
        if (IsActive) Apply(true);

        Refresh();
    }

    private void Update()
    {
        if (!Available || !shortcutEnabled) return;

        // The top face button is the one control a level list does not use for anything, so it cannot be
        // pressed by accident while moving around the tiles.
        bool pressed = Input.GetKeyDown(KeyCode.U) || Input.GetKeyDown(KeyCode.JoystickButton3);

        if (pressed) Toggle();
    }

    // ---------------------------------------------------------------- the switch

    /// <summary>True while every level is open for testing.</summary>
    private static bool IsActive
    {
        get { return PlayerPrefs.GetInt(ActiveKey, 0) != 0; }
    }

    private void Toggle()
    {
        Apply(!IsActive);
        Refresh();
    }

    /// <summary>
    /// Opens everything, or puts the progress that was set aside back.
    ///
    /// Switching on remembers what the player had reached; switching off hands it back. Both go through
    /// <see cref="LevelProgress"/>, so the level list's locks redraw themselves the moment this happens.
    /// </summary>
    private static void Apply(bool on)
    {
        if (on)
        {
            if (!IsActive)
            {
                PlayerPrefs.SetInt(SavedUnlockedKey, LevelProgress.HighestUnlocked);
                PlayerPrefs.SetInt(SavedClearedKey, LevelProgress.HighestCleared);
                PlayerPrefs.SetInt(ActiveKey, 1);
                PlayerPrefs.Save();
            }

            // Everything open, and what has been finished left as it is - the marks on the tiles are the
            // player's, and testing is about reaching the levels, not about pretending they were beaten.
            LevelProgress.SetProgress(LevelProgress.LevelCount, LevelProgress.HighestCleared);

            return;
        }

        if (IsActive)
        {
            LevelProgress.SetProgress(PlayerPrefs.GetInt(SavedUnlockedKey, 1),
                                      PlayerPrefs.GetInt(SavedClearedKey, 0));

            PlayerPrefs.DeleteKey(ActiveKey);
            PlayerPrefs.DeleteKey(SavedUnlockedKey);
            PlayerPrefs.DeleteKey(SavedClearedKey);
            PlayerPrefs.Save();

            return;
        }

        // Switched off when it was never on: nothing set aside, so there is nothing to put back.
        LevelProgress.SetProgress(LevelProgress.HighestUnlocked, LevelProgress.HighestCleared);
    }

    private void Refresh()
    {
        if (buttonLabel == null) return;

        bool active = IsActive;

        buttonLabel.text = active ? "RESTORE PROGRESS" : "UNLOCK ALL LEVELS";
        buttonLabel.color = active ? onColour : labelColour;

        statusLabel.text = active
            ? string.Format("Testing: all {0} levels open. {1}", LevelProgress.LevelCount, Shortcut())
            : Shortcut();
    }

    private string Shortcut()
    {
        return shortcutEnabled ? "U or the gamepad's top face button" : "";
    }

    // ---------------------------------------------------------------- UI construction

    private void Build()
    {
        GameObject canvasGo = new GameObject("Canvas",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);

        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 210;                  // over the level list's own canvas

        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        // The tab: a plate with the switch's name on it, pinned to the bottom-left corner.
        Image plate = CreateImage("Tab", canvasGo.transform, tabColour);
        plate.raycastTarget = true;

        RectTransform tab = plate.rectTransform;
        tab.anchorMin = Vector2.zero;
        tab.anchorMax = Vector2.zero;
        tab.pivot = Vector2.zero;
        tab.anchoredPosition = tabPosition;
        tab.sizeDelta = tabSize;

        button = plate.gameObject.AddComponent<Button>();
        button.targetGraphic = plate;
        button.onClick.AddListener(Toggle);

        buttonLabel = CreateText("Label", tab, 26, labelColour, TextAlignmentOptions.Center);
        Stretch(buttonLabel.rectTransform);

        // The status line sits above the tab and grows to the right from the same corner, so however long the
        // sentence is it stays on screen.
        statusLabel = CreateText("Status", canvasGo.transform, 22, statusColour, TextAlignmentOptions.Left);
        RectTransform status = statusLabel.rectTransform;
        status.anchorMin = Vector2.zero;
        status.anchorMax = Vector2.zero;
        status.pivot = Vector2.zero;
        status.anchoredPosition = new Vector2(tabPosition.x, tabPosition.y + tabSize.y + statusOffset);
        status.sizeDelta = new Vector2(900f, 30f);

        Debug.Log("[TestUnlock] The level list has an 'UNLOCK ALL LEVELS' switch in its bottom-left corner " +
                  "(the U key or the gamepad's top face button does the same). Delete LevelTestUnlock to " +
                  "remove it.");
    }

    private static TextMeshProUGUI CreateText(string name, Transform parent, int size, Color colour,
        TextAlignmentOptions alignment)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;

        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        text.fontSize = size;
        text.color = colour;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.enableWordWrapping = false;

        return text;
    }

    private static Image CreateImage(string name, Transform parent, Color colour)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);

        Image image = go.AddComponent<Image>();
        image.color = colour;
        image.raycastTarget = false;

        return image;
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
