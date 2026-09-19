using UnityEngine;

public class PauseMenu : MonoBehaviour
{
    [Header("Panels")]
    public GameObject pausePanel;
    public GameObject controlsPanel;

    [Header("Pause Music")]
    public AudioSource pauseMusic;

    private bool isPaused = false;
    private bool controlsOpen = false;

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

    public void OpenControls()
    {
        controlsOpen = true;
        ShowPanel(controlsPanel);
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

    public void CloseControls()
    {
        controlsOpen = false;
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