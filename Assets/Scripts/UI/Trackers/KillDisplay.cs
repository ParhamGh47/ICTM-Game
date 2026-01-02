using UnityEngine;
using UnityEngine.UI;

public class KillDisplay : MonoBehaviour
{
    [Header("UI References")]
    public Text targetKillText;
    public Text currentKillText;

    [Header("Kill Settings")]
    public int targetKills = 10;
    private int currentKills = 0;

    
    void Start()
    {
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
            targetKillText.text = targetKills.ToString();

        if (currentKillText != null)
            currentKillText.text = currentKills.ToString();
    }

    public void CheckGameOver()
    {
        if (currentKills != 0 && currentKills < targetKills)
        {
            GameOver.Instance.ShowGameOver();
        }
    }
}
