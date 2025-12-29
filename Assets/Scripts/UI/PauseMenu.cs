using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseMenu : MonoBehaviour
{
    [Header("Pause Panel")]
    public GameObject pausePanel;

    [Header("Pause Music")]
    public AudioSource pauseMusic;

    private bool isPaused = false;

    void Start()
    {
        pausePanel.SetActive(false);
        Time.timeScale = 1f;
        AudioListener.pause = false;

        if (pauseMusic != null)
        {
            pauseMusic.Stop();
            pauseMusic.ignoreListenerPause = true; // ⭐ KEY LINE
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isPaused)
                ResumeGame();
            else
                PauseGame();
        }
    }

    void PauseGame()
    {
        isPaused = true;
        pausePanel.SetActive(true);
        Time.timeScale = 0f;

        // Pause all gameplay audio
        AudioListener.pause = true;

        // Play pause menu music
        if (pauseMusic != null && !pauseMusic.isPlaying)
            pauseMusic.Play();
    }

    public void ResumeGame()
    {
        isPaused = false;
        pausePanel.SetActive(false);
        Time.timeScale = 1f;

        // Resume gameplay audio
        AudioListener.pause = false;

        // Stop pause music
        if (pauseMusic != null && pauseMusic.isPlaying)
            pauseMusic.Stop();
    }

    public void OpenControls()
    {
        ResumeGame();
        SceneManager.LoadScene("Option");
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
