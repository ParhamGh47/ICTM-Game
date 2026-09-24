using UnityEngine;
using UnityEngine.UI;

public class TimeTracker : MonoBehaviour
{
    [Header("UI References")]
    public Text targetTimeText;
    public Text currentTimeText;

    [Header("Target Time Settings")]
    [Tooltip("The time the level is built with, in seconds (e.g. 200 = 3 minutes 20 seconds). This is the " +
             "Medium time: the difficulty the player has set takes its share off it (see GameDifficulty), and " +
             "the target shown on the HUD is the time that really counts down.")]
    public float targetTimeSeconds = 200f; // 3:20

    // The target at the difficulty the level started with. Read once, in Start, so a difficulty chosen while
    // the level is running cannot change it - it lands on the next level, or on this one when it is restarted.
    private float activeTargetTimeSeconds;

    private float elapsedTime = 0f;
    private bool isRunning = true;
    
    

    void Start()
    {
        activeTargetTimeSeconds = GameDifficulty.TimeLimit(targetTimeSeconds);

        if (targetTimeText != null)
        {
            targetTimeText.text = FormatTargetTime(activeTargetTimeSeconds);
        }
    }

    void Update()
    {
        if (!isRunning) return;

        elapsedTime += Time.deltaTime;

        if (currentTimeText != null)
        {
            currentTimeText.text = FormatElapsedTime(elapsedTime);
        }

        if (elapsedTime >= activeTargetTimeSeconds)
        {
            isRunning = false;
            gameOver.Instance.ShowGameOver();
        }
    }

    private string FormatTargetTime(float seconds)
    {
        int minutes = Mathf.FloorToInt(seconds / 60f);
        int secs = Mathf.FloorToInt(seconds % 60f);
        return string.Format("{0:0}:{1:00}", minutes, secs);
    }

    private string FormatElapsedTime(float seconds)
    {
        int minutes = Mathf.FloorToInt(seconds / 60f);
        int secs = Mathf.FloorToInt(seconds % 60f);
        int millis = Mathf.FloorToInt((seconds * 100f) % 100f); // two-digit ms
        return string.Format("{0:0}:{1:00}:{2:00}", minutes, secs, millis);
    }
}
