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
        if (ks != null)
        {
            ks.CheckGameOver();
            loader.LoadEnd();
        }
    }
}
