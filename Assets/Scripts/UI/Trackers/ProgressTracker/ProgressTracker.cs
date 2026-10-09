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

    /// <summary>
    /// The checkpoint this trigger belongs to, as the compass lists it - the parent object the trigger's box
    /// sits on. Left empty it is taken from the trigger's own place in the hierarchy, which is where the
    /// checkpoint prefab puts it.
    /// </summary>
    [Tooltip("The checkpoint object this trigger belongs to, as the compass lists it. Empty means the " +
             "trigger's own parent.")]
    public Transform checkpoint;

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
            // Which checkpoint was reached, not merely that one was: the compass uses it to step past any
            // checkpoint the player missed on the way here. See CheckpointIndicator.Reached.
            if (checkpointCompass != null)
            {
                checkpointCompass.Reached(checkpoint != null ? checkpoint : transform.parent);
            }

            if (progressDisplay != null)
            {
                progressDisplay.AddProgress(progressAmount);
            }

            triggered = true;
        }
    }
}
