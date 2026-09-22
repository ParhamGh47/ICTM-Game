using UnityEngine;

public class gameOver : MonoBehaviour
{
    public static gameOver Instance { get; private set; }

    [Header("Panels")]
    [SerializeField] private GameObject gameOverPanel;

    [Header("Pause Music")]
    [SerializeField] private AudioSource pauseMusic;

    private bool isGameOver = false;
    public bool IsGameOver => isGameOver;

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
        {
            // Above the level's HUD, the same way the pause panel is brought forward as it opens. The HUD
            // is authored as a later sibling of these panels in the same canvas, and a later sibling draws
            // on top - so without this the game over panel opens underneath the target and timer.
            gameOverPanel.transform.SetAsLastSibling();
            gameOverPanel.SetActive(true);

            // The panel highlights its own Restart button as it opens. Saying so here as well makes the
            // first thing the player can do a restart, whatever clearing the selection got there first.
            MenuNavigation navigation = gameOverPanel.GetComponent<MenuNavigation>();
            if (navigation != null) navigation.SelectDefault();
        }

        Time.timeScale = 0f;
        AudioListener.pause = true;

        if (pauseMusic != null && !pauseMusic.isPlaying)
            pauseMusic.Play();

        if (PauseTracker.Instance != null)
            PauseTracker.Instance.isPaused = true;
    }

    public void RestartLevel()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;

        if (pauseMusic != null && pauseMusic.isPlaying)
            pauseMusic.Stop();

        if (PauseTracker.Instance != null)
            PauseTracker.Instance.isPaused = false;

        SceneLoader.Reload();
    }

    public void ExitToMenu()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;

        if (pauseMusic != null && pauseMusic.isPlaying)
            pauseMusic.Stop();

        if (PauseTracker.Instance != null)
            PauseTracker.Instance.isPaused = false;

        SceneLoader.Load("Menu");
    }

}
