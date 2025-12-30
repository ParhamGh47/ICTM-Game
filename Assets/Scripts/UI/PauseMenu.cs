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
        pausePanel.SetActive(false);
        controlsPanel.SetActive(false);

        Time.timeScale = 1f;
        AudioListener.pause = false;

        if (pauseMusic != null)
        {
            pauseMusic.Stop();
            pauseMusic.ignoreListenerPause = true;
        }
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
        pausePanel.SetActive(true);
        controlsPanel.SetActive(false);

        Time.timeScale = 0f;
        AudioListener.pause = true;

        if (pauseMusic != null && !pauseMusic.isPlaying)
            pauseMusic.Play();
    }

    public void ResumeGame()
    {
        isPaused = false;
        controlsOpen = false;

        pausePanel.SetActive(false);
        controlsPanel.SetActive(false);

        Time.timeScale = 1f;
        AudioListener.pause = false;

        if (pauseMusic != null && pauseMusic.isPlaying)
            pauseMusic.Stop();
    }

    public void OpenControls()
    {
        controlsOpen = true;
        controlsPanel.SetActive(true);
    }

    public void CloseControls()
    {
        controlsOpen = false;
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
