using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseMenu : MonoBehaviour
{
    [Header("Pause Panel")]
    public GameObject pausePanel;

    private bool isPaused = false;

    void Start()
    {
        pausePanel.SetActive(false);
        Time.timeScale = 1f;
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

    // Pause the game
    void PauseGame()
    {
        isPaused = true;
        pausePanel.SetActive(true);
        Time.timeScale = 0f;
    }

    // Resume the game
    public void ResumeGame()
    {
        isPaused = false;
        pausePanel.SetActive(false);
        Time.timeScale = 1f;
    }

    // Load Options / Controls scene
    public void OpenControls()
    {
        ResumeGame();
        SceneManager.LoadScene("Option");
    }

    // Restart current level
    public void RestartLevel()
    {
        ResumeGame();
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // Exit to main menu
    public void ExitToMenu()
    {
        ResumeGame();
        SceneManager.LoadScene("Menu");
    }
}
