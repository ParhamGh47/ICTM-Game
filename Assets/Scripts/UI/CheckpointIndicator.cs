using UnityEngine;
using UnityEngine.UI;

public class CheckpointIndicator : MonoBehaviour
{
    [Header("References")]
    public Transform player;

    public Image arrowImage;

    public Transform[] checkpoints;

    // public Graphic wrongWayUI;

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
        // if (wrongWayUI != null)
        //     wrongWayUI.gameObject.SetActive(isWrongWay);
    }

    /// <summary>
    /// The checkpoint the player is currently being sent to. Null when no
    /// checkpoints are wired up yet.
    /// </summary>
    public Transform GetNextCheckpoint()
    {
        if (checkpoints == null || checkpoints.Length == 0)
            return null;

        return checkpoints[Mathf.Clamp(currentIndex, 0, checkpoints.Length - 1)];
    }

    public int GetCurrentIndex()
    {
        return currentIndex;
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

    /// <summary>
    /// Where a checkpoint sits in this level's list, or -1 when it is not one of them.
    /// </summary>
    public int IndexOf(Transform checkpoint)
    {
        if (checkpoint == null || checkpoints == null) return -1;

        for (int i = 0; i < checkpoints.Length; i++)
        {
            if (checkpoints[i] == checkpoint) return i;
        }

        return -1;
    }

    /// <summary>
    /// The player drove through this checkpoint.
    ///
    /// The compass is sent to the one <em>after</em> the one that was actually reached, rather than simply to
    /// the next entry in the list. That is what makes a missed checkpoint stay behind: the arrow points at the
    /// first checkpoint the player has not been through, and a checkpoint they went past without touching - a
    /// wide line round the outside of it, a hop over the bend it sits on - would otherwise be left at the head
    /// of the list and pointed at again from further down the level, sending the player back up the road for a
    /// line they already crossed.
    ///
    /// Passing a checkpoint further back than the one the compass is showing is not a step backwards either:
    /// a checkpoint that has already been reached leaves the arrow where it is.
    /// </summary>
    public void Reached(Transform checkpoint)
    {
        int index = IndexOf(checkpoint);

        // Not one of the wired checkpoints - a stray trigger, or a level whose compass has not been filled in
        // yet - so fall back to the plain one-step advance this used to be.
        if (index < 0)
        {
            NextCheckpoint();
            return;
        }

        if (index + 1 <= currentIndex) return;

        SetCheckpointIndex(index + 1);
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
