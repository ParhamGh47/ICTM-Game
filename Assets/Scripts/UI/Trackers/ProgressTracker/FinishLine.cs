using UnityEngine;

[RequireComponent(typeof(Collider))]
public class FinishLine : MonoBehaviour
{

    public ProgressDisplay progressDisplay;

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
                progressDisplay.FinishGame();
            }
            triggered = true;
        }
    }
}
