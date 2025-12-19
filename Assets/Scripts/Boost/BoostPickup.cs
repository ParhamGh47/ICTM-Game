using UnityEngine;

public class BoostPickup : MonoBehaviour
{
    [Header("Pickup Settings")]
    public bool destroyAfterPickup = true;

    [Header("Visual Motion")]
    public float rotateSpeed = 90f;
    public float bounceHeight = 0.25f;
    public float bounceSpeed = 2f;         

    private Vector3 startPos;

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

        BoostManager boostVFX = other.GetComponentInChildren<BoostManager>();

        if (boostVFX != null)
        {
            boostVFX.PlayBoost();
        }

        if (destroyAfterPickup)
        {
            Destroy(gameObject);
        }
    }
}
