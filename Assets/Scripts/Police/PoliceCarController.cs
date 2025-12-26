using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class PoliceCarController : MonoBehaviour
{
    [Header("References")]
    public Transform player; // Drag the player car transform here
    public Rigidbody playerRb; // Optional: Drag player Rigidbody for prediction (better chasing)

    [Header("Chase Settings")]
    public float baseSpeed = 50f; // Base speed in m/s (~180 kph)
    public float acceleration = 20f;
    public float angularSpeed = 300f; // Faster turning
    public float stoppingDistance = 5f; // Close enough for ram without orbiting

    [Header("Aggression")]
    public float predictionTime = 1.5f; // Seconds to lead the player
    public float closeDistanceBoost = 15f; // Distance to trigger speed boost
    public float speedBoostMultiplier = 1.3f; // Extra speed when close

    [Header("Ramming")]
    public float ramForce = 20000f; // Impulse force on player when hit

    [Header("Post-Ram Cooldown")]
    public float cooldownTime = 4f; // Seconds to back off after ram
    public float cooldownSpeedMultiplier = 0.4f; // Slower during cooldown

    private NavMeshAgent agent;
    private float lastRamTime;
    private bool inCooldown;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    void Start()
    {
        // Configure agent for car-like feel
        agent.speed = baseSpeed;
        agent.acceleration = acceleration;
        agent.angularSpeed = angularSpeed;
        agent.stoppingDistance = stoppingDistance;
        agent.autoBraking = true;
        agent.radius = 1.5f; // Adjust based on your car size
        agent.height = 1.5f;
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;

        if (playerRb == null && player != null)
            playerRb = player.GetComponent<Rigidbody>();
    }

    void Update()
    {
        if (player == null) return;

        inCooldown = Time.time - lastRamTime < cooldownTime;

        Vector3 targetPos;

        if (inCooldown)
        {
            // Back off: target a point away from player
            Vector3 awayDir = (transform.position - player.position).normalized;
            targetPos = transform.position + awayDir * 30f; // Aim 30m away
            agent.speed = baseSpeed * cooldownSpeedMultiplier;
        }
        else
        {
            // Normal chase: predict player position
            Vector3 playerVelocity = playerRb ? playerRb.velocity : Vector3.zero;
            Vector3 predictedPlayerPos = player.position + playerVelocity * predictionTime;

            targetPos = predictedPlayerPos;

            // Speed boost when close
            float distToPlayer = Vector3.Distance(transform.position, player.position);
            if (distToPlayer < closeDistanceBoost)
            {
                agent.speed = baseSpeed * speedBoostMultiplier;
            }
            else
            {
                agent.speed = baseSpeed;
            }
        }

        // Update destination less frequently for performance (every 0.1s)
        if (Time.frameCount % 6 == 0) // ~10 times per second
        {
            agent.SetDestination(targetPos);
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        if (!collision.collider.CompareTag("Player")) return;

        Rigidbody playerRigid = collision.collider.GetComponent<Rigidbody>();
        if (playerRigid != null)
        {
            Vector3 ramDirection = (collision.transform.position - transform.position).normalized;
            playerRigid.AddForce(ramDirection * ramForce, ForceMode.Impulse);
        }

        // Trigger cooldown after successful ram
        lastRamTime = Time.time;
    }
}