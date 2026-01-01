using UnityEngine;
using UnityEngine.UI;

public class CheckpointIndicator : MonoBehaviour
{
    [Header("References")]
    public Transform player;

    public Image arrowImage;

    public Transform[] checkpoints;

    public Graphic wrongWayUI;

    public float wrongWayAngleThreshold = 100f;

    private int currentIndex = 0;

    void Update()
    {
        if (player == null || arrowImage == null || checkpoints == null || checkpoints.Length == 0)
            return;

        Transform nextCheckpoint = checkpoints[currentIndex];
        if (nextCheckpoint == null) return;

        Vector3 dir = nextCheckpoint.position - player.position;
        dir.y = 0f;

        if (dir.sqrMagnitude < 0.001f)
            return;

        float angle = Vector3.SignedAngle(player.forward, dir, Vector3.up);

        arrowImage.transform.rotation = Quaternion.Euler(0f, 0f, -angle);

        bool isWrongWay = Mathf.Abs(angle) > wrongWayAngleThreshold;
        if (wrongWayUI != null)
            wrongWayUI.gameObject.SetActive(isWrongWay);
    }

    public void NextCheckpoint()
    {
        if (checkpoints == null || checkpoints.Length == 0) return;

        currentIndex++;
        if (currentIndex >= checkpoints.Length)
        {
            currentIndex = checkpoints.Length - 1;
            Debug.Log("All checkpoints reached!");
        }
    }

    public void ResetCheckpoints()
    {
        currentIndex = 0;
    }

    public void SetCheckpointIndex(int index)
    {
        if (checkpoints == null || checkpoints.Length == 0) return;
        currentIndex = Mathf.Clamp(index, 0, checkpoints.Length - 1);
    }
}
