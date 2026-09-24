using UnityEngine;
using UnityEngine.UI;

public class KillDisplay : MonoBehaviour
{
    [Header("UI References")]
    public Text targetKillText;
    public Text currentKillText;

    [Header("Kill Settings")]
    [Tooltip("The number of targets the level is built to ask for. This is the Medium requirement: the " +
             "difficulty the player has set takes its share off it (see GameDifficulty), and the target shown " +
             "on the HUD is the number really asked for.")]
    public int targetKills = 10;
    private int currentKills = 0;

    // The requirement at the difficulty the level started with. Read once, in Start, so a difficulty chosen
    // while the level is running cannot change it - it lands on the next level, or on this one when it is
    // restarted.
    private int requiredKills = 0;

    
    void Start()
    {
        requiredKills = GameDifficulty.KillTarget(targetKills);

        UpdateDisplay();
    }

    public void IncrementKills()
    {
        currentKills++;
        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        if (targetKillText != null)
            targetKillText.text = requiredKills.ToString();

        if (currentKillText != null)
            currentKillText.text = currentKills.ToString();
    }

    public void CheckGameOver()
    {
        if (currentKills != 0 && currentKills < requiredKills)
        {
            gameOver.Instance.ShowGameOver();
        }
    }
}
