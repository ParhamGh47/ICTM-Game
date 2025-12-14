using UnityEngine;

public class AdamakController : MonoBehaviour
{
    [Header("Flee Settings")]
    [SerializeField] private float moveSpeed = 2.5f;

    [Header("Death Settings")]
    [SerializeField] private GameObject particlePrefab;
    [SerializeField] private float particleYOffset = 0.5f;

    [Header("Physics Swap")]
    [SerializeField] private Rigidbody parentRigidbody;
    [SerializeField] private Rigidbody[] ragdollRigidbodies;

    private Transform player;
    private bool isFleeing = false;
    private bool isDead = false;

    private void Awake()
    {
        EnableRagdoll(false);
    }

    private void Update()
    {
        if (!isFleeing || isDead || player == null)
            return;

        Vector3 fleeDirection = (transform.position - player.position).normalized;
        transform.position += fleeDirection * moveSpeed * Time.deltaTime;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isDead)
            return;

        if (!other.CompareTag("Player"))
            return;

        player = other.transform;
        isFleeing = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (isDead)
            return;

        if (!other.CompareTag("Player"))
            return;

        isFleeing = false;
        player = null;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (isDead)
            return;

        if (!collision.gameObject.CompareTag("Player"))
            return;

        Die();
    }

    private void Die()
    {
        isDead = true;
        isFleeing = false;

        if (particlePrefab != null)
        {
            Vector3 spawnPos = transform.position + Vector3.up * particleYOffset;
            Instantiate(particlePrefab, spawnPos, Quaternion.identity);
        }

        EnableRagdoll(true);
    }

    private void EnableRagdoll(bool enable)
    {
        if (parentRigidbody != null)
        {
            parentRigidbody.isKinematic = enable;
            parentRigidbody.detectCollisions = !enable;
        }

        foreach (Rigidbody rb in ragdollRigidbodies)
        {
            rb.isKinematic = !enable;
            rb.detectCollisions = enable;
            rb.useGravity = enable;
        }
    }
}
