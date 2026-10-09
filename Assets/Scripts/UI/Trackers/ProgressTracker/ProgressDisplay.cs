using UnityEngine;
using UnityEngine.UI;

public class ProgressDisplay : MonoBehaviour
{
    [Header("UI Reference")]
    public Text progressText;

    [Header("Loader")]
    public LevelLoader loader;

    [Header("Kills (optional)")]
    public KillDisplay ks;

    [Header("Progress Settings")]
    [Range(0, 100)]
    public float currentProgress = 0f;

    void Start()
    {
        UpdateDisplay();
    }

    public void AddProgress(float amount)
    {
        if (amount > currentProgress)
        {
            currentProgress = amount;
            currentProgress = Mathf.Clamp(currentProgress, 0f, 100f);
            UpdateDisplay();
        }
    }

    private void UpdateDisplay()
    {
        if (progressText != null)
        {
            progressText.text = $"{currentProgress}%";
        }
    }

    public void FinishGame()
    {
        // A level that asks for targets is not finished by crossing the line alone: the run must either hit
        // none of them or reach the whole number (see KillDisplay.CheckGameOver). Failing it shows the game
        // over screen and stops here, so the end scene is not entered and the level is not counted as
        // finished - crossing a line on a failed run must not open the next level.
        if (ks != null && ks.CheckGameOver())
            return;

        // Crossing the line is what finishes a level, so this is where the one after it opens - before the
        // end scene is entered, so it is entered with the progress already saved. A scene that is not part of
        // a level (the playground, the template) reports level 0 and finishes nothing.
        LevelProgress.CompleteCurrentScene();

        loader.LoadEnd();
    }
}
