using UnityEngine;
using UnityEngine.UI;

public class TimeTracker : MonoBehaviour
{
    [Header("UI References")]
    public Text targetTimeText;
    public Text currentTimeText;

    [Header("Target Time Settings")]
    [Tooltip("The time the level allows on Easy, in seconds (e.g. 200 = 3 minutes 20 seconds). Each " +
             "difficulty has its own value, so a level can be made easier or harder on its own rather than " +
             "as a fixed fraction of Medium; the one the player has chosen is what the HUD counts down " +
             "(see GameDifficulty).")]
    public float easyTargetTimeSeconds = 250f;

    [Tooltip("The time the level allows on Medium - the game as authored.")]
    public float mediumTargetTimeSeconds = 200f;

    [Tooltip("The time the level allows on Hard.")]
    public float hardTargetTimeSeconds = 160f;

    // The target at the difficulty the level started with. Read once, in Start, so a difficulty chosen while
    // the level is running cannot change it - it lands on the next level, or on this one when it is restarted.
    private float activeTargetTimeSeconds;

    private float elapsedTime = 0f;
    private bool isRunning = true;
    
    

    void Start()
    {
        activeTargetTimeSeconds =
            GameDifficulty.TimeLimit(easyTargetTimeSeconds, mediumTargetTimeSeconds, hardTargetTimeSeconds);

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
