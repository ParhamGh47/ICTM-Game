using UnityEngine;

public class PauseTracker : MonoBehaviour
{
    public static PauseTracker Instance { get; private set; }

    [Header("Pause State")]
    public bool isPaused = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
            return;
        }

        Instance = this;
    }
}