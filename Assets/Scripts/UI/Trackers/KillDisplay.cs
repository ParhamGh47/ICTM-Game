using UnityEngine;
using UnityEngine.UI;

public class KillDisplay : MonoBehaviour
{
    [Header("UI References")]
    public Text targetKillText;
    public Text currentKillText;

    [Header("Kill Settings")]
    [Tooltip("The number of targets the level asks for on Easy. Each difficulty has its own value, so a " +
             "level can be made easier or harder on its own rather than as a fixed fraction of Medium; the " +
             "one the player has chosen is what the HUD shows (see GameDifficulty).")]
    public int easyTargetKills = 7;

    [Tooltip("The number of targets the level asks for on Medium - the game as authored.")]
    public int mediumTargetKills = 10;

    [Tooltip("The number of targets the level asks for on Hard.")]
    public int hardTargetKills = 14;

    private int currentKills = 0;

    // The requirement at the difficulty the level started with. Read once, in Start, so a difficulty chosen
    // while the level is running cannot change it - it lands on the next level, or on this one when it is
    // restarted.
    private int requiredKills = 0;

    
    void Start()
    {
        requiredKills =
            GameDifficulty.KillTarget(easyTargetKills, mediumTargetKills, hardTargetKills);

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
