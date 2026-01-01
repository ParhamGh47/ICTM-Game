using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class AITarget : MonoBehaviour
{
    [Header("References")]
    public Transform player;
    public Rigidbody playerRb;

    [Header("Chase Settings")]
    public float baseSpeed = 50f; // ~180 kph
    public float acceleration = 20f;
    public float angularSpeed = 300f; 
    public float stoppingDistance = 5f;

    [Header("Aggression")]
    public float predictionTime = 1.5f;
    public float closeDistanceBoost = 15f;
    public float speedBoostMultiplier = 1.3f;

    [Header("Ramming")]
    public float ramForce = 20000f;

    [Header("Post-Ram Cooldown")]
    public float cooldownTime = 4f;
    public float cooldownSpeedMultiplier = 0.4f;

    private NavMeshAgent agent;
    private float lastRamTime;
    private bool inCooldown;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    void Start()
    {
        agent.speed = baseSpeed;
        agent.acceleration = acceleration;
        agent.angularSpeed = angularSpeed;
        agent.stoppingDistance = stoppingDistance;
        agent.autoBraking = true;
        agent.radius = 1.5f;
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
            Vector3 awayDir = (transform.position - player.position).normalized;
            targetPos = transform.position + awayDir * 5f;
            agent.speed = baseSpeed * cooldownSpeedMultiplier;
        }
        else
        {
            Vector3 playerVelocity = playerRb ? playerRb.velocity : Vector3.zero;
            Vector3 predictedPlayerPos = player.position + playerVelocity * predictionTime;

            targetPos = predictedPlayerPos;

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

        if (Time.frameCount % 6 == 0)
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

        lastRamTime = Time.time;
    }
}