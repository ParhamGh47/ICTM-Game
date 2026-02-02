using UnityEngine;

public class FallingIceCream : MonoBehaviour
{
    [Header("Movement")]
    public Vector3 velocity;
    public float gravity = 10f;

    [Header("Lifetime")]
    public float lifetime = 2f;

    Vector3 rotationSpeed;

    void Start()
    {
        rotationSpeed = Random.insideUnitSphere * 180f;
        Destroy(gameObject, lifetime);
    }

    void Update()
    {
        velocity.y -= gravity * Time.deltaTime;
        transform.position += velocity * Time.deltaTime;
        transform.Rotate(rotationSpeed * Time.deltaTime);
    }

}
