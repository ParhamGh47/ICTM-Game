using UnityEngine;
using UnityEngine.SceneManagement;

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
        if (Input.GetKeyDown(KeyCode.Escape))
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

        if (pausePanel != null)
            pausePanel.SetActive(true);
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
        if (controlsPanel != null)
            controlsPanel.SetActive(true);
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
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void ExitToMenu()
    {
        ResumeGame();
        SceneManager.LoadScene("Menu");
    }
}