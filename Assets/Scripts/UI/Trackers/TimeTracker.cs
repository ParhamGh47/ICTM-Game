using UnityEngine;
using UnityEngine.UI;

public class TimeTracker : MonoBehaviour
{
    [Header("UI References")]
    public Text targetTimeText;
    public Text currentTimeText;

    [Header("Target Time Settings")]
    [Tooltip("Target time in seconds (e.g. 200 = 3 minutes 20 seconds)")]
    public float targetTimeSeconds = 200f; // 3:20

    private float elapsedTime = 0f;
    private bool isRunning = true;
    
    

    void Start()
    {
        if (targetTimeText != null)
        {
            targetTimeText.text = FormatTargetTime(targetTimeSeconds);
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

        if (elapsedTime >= targetTimeSeconds)
        {
            isRunning = false;
            GameOver.Instance.ShowGameOver();
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
