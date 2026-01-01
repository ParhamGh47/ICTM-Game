using UnityEngine;
using UnityEngine.SceneManagement;

public class gameOver : MonoBehaviour
{
    public static gameOver Instance { get; private set; }

    [Header("Panels")]
    [SerializeField] private GameObject gameOverPanel;

    [Header("Pause Music (optional)")]
    [SerializeField] private AudioSource pauseMusic;

    private bool isGameOver = false;

    private void Awake()
    {
        
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
       
    }

    private void Start()
    {
        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);

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

    
    public void ShowGameOver()
    {
        if (isGameOver) return; 
        isGameOver = true;

        if (gameOverPanel != null)
            gameOverPanel.SetActive(true);

        Time.timeScale = 0f;
        AudioListener.pause = true;

        if (pauseMusic != null && !pauseMusic.isPlaying)
            pauseMusic.Play();

        if (PauseTracker.Instance != null)
            PauseTracker.Instance.isPaused = true;
    }

   
   

    public void RestartLevel()
    {   
        //for test
        SceneManager.LoadScene("Playground");
        //SceneManager.LoadScene("Levels");
    }

    public void ExitToMenu()
    {
        SceneManager.LoadScene("Menu");
    }
}
