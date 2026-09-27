using UnityEngine;

/// <summary>
/// A milkshake left on the road.
///
/// Driving over it puts one more boost in the truck's tank - see <see cref="BoostManager"/> - and the driver
/// spends it whenever they want it. Nothing fires where the milkshake was found, so a pickup passed at the
/// wrong moment is not wasted: it is still in the tank at the next corner.
///
/// One milkshake is worth one boost however much of the truck touches it. That needs saying because the truck
/// carries two Player-tagged colliders - the low chassis box and the body volume above it - and Unity fires a
/// separate trigger event for every collider that enters. Counting on the way in rather than per event is
/// what stops a single milkshake being collected twice on the same pass.
/// </summary>
public class BoostPickup : MonoBehaviour
{
    [Header("Pickup Settings")]
    [Tooltip("Destroy the milkshake once it is collected. Cleared, it stays where it is and can be collected " +
             "again the next time the truck drives over it - a boost pad rather than a one-off.")]
    public bool destroyAfterPickup = true;

    [Header("Visual Motion")]
    public float rotateSpeed = 90f;
    public float bounceHeight = 0.25f;
    public float bounceSpeed = 2f;

    private Vector3 startPos;

    // How many of the truck's colliders are inside the pickup at this moment. A boost is granted when that
    // count goes from none to some, and not again until the truck has left and come back - which is what
    // makes the second collider free of charge, and a kept pickup reusable.
    private int inside;

    private void Start()
    {
        startPos = transform.position;
    }

    private void Update()
    {
        transform.Rotate(Vector3.up, rotateSpeed * Time.deltaTime, Space.World);
        float yOffset = Mathf.Sin(Time.time * bounceSpeed) * bounceHeight;
        transform.position = startPos + Vector3.up * yOffset;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        inside++;

        // Something of the truck was already in the milkshake: this is its second collider arriving, not a
        // second milkshake collected.
        if (inside > 1)
            return;

        BoostManager boost = FindBoostManager(other);

        if (boost != null)
        {
            boost.Collect();
        }

        if (destroyAfterPickup)
        {
            Destroy(gameObject);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        if (inside > 0)
            inside--;
    }

    /// <summary>
    /// The truck's boost manager, found from whatever part of the truck set the trigger off.
    ///
    /// The truck carries more than one collider and the manager is not on all of them, so the search starts
    /// from the rigidbody a collider belongs to - which is the truck itself, whichever panel was hit - and
    /// falls back to the manager in the scene.
    /// </summary>
    private static BoostManager FindBoostManager(Collider other)
    {
        Rigidbody body = other.attachedRigidbody;

        BoostManager boost = body != null
            ? body.GetComponentInChildren<BoostManager>()
            : other.GetComponentInChildren<BoostManager>();

        return boost != null ? boost : BoostManager.Instance;
    }
}
