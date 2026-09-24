using TMPro;
using UnityEngine;

/// <summary>
/// The pause menu: the window, the music that plays behind it, and the two screens it holds - the pause
/// buttons themselves and the options.
///
/// The options are built in code and live inside this same window (see <see cref="PauseOptionsPanel"/>): the
/// pause panel's own buttons are put away while they are open and come back when they close, so there is no
/// second page and no second background, and nothing in the pause panel's artwork had to change to make room
/// for them. Whatever the pause window happens to be holding is what gets put away, so a button added to the
/// pause menu later is handled without touching this script.
///
/// The old keyboard-diagram panel is still in the prefab and still referenced here for the case where the
/// pause window itself is missing, but it is no longer what the pause menu's options button opens - the
/// options panel holds the same table, read from <see cref="ControlBindings"/> so it cannot drift from the
/// game.
/// </summary>
public class PauseMenu : MonoBehaviour
{
    [Header("Panels")]
    public GameObject pausePanel;

    [Tooltip("The pre-options keyboard diagram. Kept only as a fallback for a pause panel with no window to " +
             "build the options in; the options panel is what the button opens.")]
    public GameObject controlsPanel;

    [Header("Options")]
    [Tooltip("The font the options are written in. Leave empty for the project's default TMP font.")]
    public TMP_FontAsset optionsFont;

    [Header("Pause Music")]
    public AudioSource pauseMusic;

    private bool isPaused = false;
    private bool controlsOpen = false;

    private PauseOptionsPanel options;

    void Start()
    {
        if (pausePanel != null)
            pausePanel.SetActive(false);
        if (controlsPanel != null)
            controlsPanel.SetActive(false);

        Time.timeScale = 1f;
        AudioListener.pause = false;

        if (pauseMusic != null)
        {
            pauseMusic.Stop();
            pauseMusic.ignoreListenerPause = true;
        }

        if (PauseTracker.Instance != null)
            PauseTracker.Instance.isPaused = false;
    }

    void Update()
    {
        // Prevent pause menu input if game is over
        if (gameOver.Instance != null && gameOver.Instance.IsGameOver)
            return;

        // Escape on the keyboard, Start / Options on a gamepad. B is left alone here: in a level it
        // is the headlights, so it cannot also be the way out of the pause menu.
        if (GameInput.PausePressed())
        {
            if (controlsOpen)
            {
                // Out of the options first, the way a back button does, and out of the pause menu from there.
                CloseControls();
            }
            else if (isPaused)
            {
                ResumeGame();
            }
            else
            {
                PauseGame();
            }
        }
    }

    void PauseGame()
    {
        isPaused = true;
        controlsOpen = false;

        ShowPanel(pausePanel);

        if (controlsPanel != null)
            controlsPanel.SetActive(false);

        // If the pause menu was left open on the options, it always comes back to its own buttons.
        if (options != null)
            options.Close();

        Time.timeScale = 0f;
        AudioListener.pause = true;

        if (pauseMusic != null && !pauseMusic.isPlaying)
            pauseMusic.Play();

        if (PauseTracker.Instance != null)
            PauseTracker.Instance.isPaused = true;
    }

    public void ResumeGame()
    {
        isPaused = false;
        controlsOpen = false;

        if (options != null)
            options.Close();

        if (pausePanel != null)
            pausePanel.SetActive(false);
        if (controlsPanel != null)
            controlsPanel.SetActive(false);

        Time.timeScale = 1f;
        AudioListener.pause = false;

        if (pauseMusic != null && pauseMusic.isPlaying)
            pauseMusic.Stop();

        if (PauseTracker.Instance != null)
            PauseTracker.Instance.isPaused = false;
    }

    /// <summary>
    /// Opens the options inside the pause window. Named for the button that calls it rather than for what it
    /// shows, because the pause panel's own button still points at this method.
    /// </summary>
    public void OpenControls()
    {
        controlsOpen = true;

        if (pausePanel == null)
        {
            // No window to put them in - this scene's pause menu is the old shape. Show what it has.
            ShowPanel(controlsPanel);
            return;
        }

        if (options == null)
        {
            // Built against the window's own frame, so the layout is in the window's coordinates whatever the
            // canvas scales to.
            options = PauseOptionsPanel.Create(this, pausePanel.transform as RectTransform, optionsFont);
        }

        options.Open();
    }

    /// <summary>
    /// Shows a panel, and above the level's HUD rather than under it.
    ///
    /// The HUD - the target, the timer, the compass - is authored as a later sibling of these panels in
    /// the same canvas, and in a canvas a later sibling is drawn on top. Simply switching a panel on
    /// therefore puts it behind the HUD, which is not what the player asked for when they paused.
    /// Bringing the panel to the front as it opens keeps the screen that has the player's attention on
    /// top, whatever order the canvas happens to be authored in. The controls panel does the same, so it
    /// still comes up over the pause panel it was opened from.
    /// </summary>
    private static void ShowPanel(GameObject panel)
    {
        if (panel == null) return;

        panel.transform.SetAsLastSibling();
        panel.SetActive(true);
    }

    /// <summary>Closes the options, giving the pause window's own buttons back.</summary>
    public void CloseControls()
    {
        controlsOpen = false;

        if (options != null)
        {
            options.Close();
            return;
        }

        if (controlsPanel != null)
            controlsPanel.SetActive(false);
    }

    public void RestartLevel()
    {
        ResumeGame();
        SceneLoader.Reload();
    }

    public void ExitToMenu()
    {
        ResumeGame();
        SceneLoader.Load("Menu");
    }
}