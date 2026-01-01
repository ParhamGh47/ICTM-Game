using UnityEngine;

[RequireComponent(typeof(Collider))]
public class ProgressTracker : MonoBehaviour
{
    [Header("Progress Settings")]
    [Tooltip("How much progress this trigger adds when activated")]
    public float progressAmount = 5f;

    [Header("References")]
    public ProgressDisplay progressDisplay;
    public CheckpointIndicator checkpointCompass;

    private bool triggered = false;

    void Start()
    {
        Collider col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (triggered) return;

        if (other.CompareTag("Player"))
        {
            if (progressDisplay != null)
            {
                checkpointCompass.NextCheckpoint();
                progressDisplay.AddProgress(progressAmount);
            }
            triggered = true;
        }
    }
}
