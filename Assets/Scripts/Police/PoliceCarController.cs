using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class PoliceCarController : MonoBehaviour
{
    [Header("References")]
    public Transform player;
    public Rigidbody playerRb;

    [Header("Chase Settings")]
    public float baseSpeed = 35f; // m/s (~126 kph)
    public float acceleration = 30f;
    public float angularSpeed = 540f; // Increased for snappier rotation
    public float tailDistance = 10f;
    public float stoppingDistance = 3f;
    public float maxPredictionTime = 0.8f;

    [Header("Rotation Polish")]
    public float rotationSpeed = 8f; // Smooth alignment to movement direction
    public float velocityAlignmentWeight = 0.7f; // Blend velocity vs path direction

    private NavMeshAgent agent;

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
        agent.baseOffset = 0f; // Ensures wheels touch ground
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;

        if (player != null && playerRb == null)
            playerRb = player.GetComponent<Rigidbody>();
    }

    void Update()
    {
        if (player == null) return;

        Vector3 playerVelocity = playerRb ? playerRb.velocity : Vector3.zero;
        float playerSpeed = playerVelocity.magnitude;

        float predictionTime = Mathf.Lerp(0.2f, maxPredictionTime, playerSpeed / 40f);

        Vector3 predictedPos = player.position + playerVelocity * predictionTime;
        Vector3 rawTargetPos = predictedPos - player.forward * tailDistance;

        NavMeshHit hit;
        Vector3 targetPos;
        if (NavMesh.SamplePosition(rawTargetPos, out hit, 10f, NavMesh.AllAreas))
        {
            targetPos = hit.position;
        }
        else
        {
            targetPos = predictedPos;
        }

        float distToTarget = Vector3.Distance(transform.position, targetPos);
        agent.speed = Mathf.Lerp(baseSpeed * 0.8f, baseSpeed * 1.3f, distToTarget / 20f);

        if (Time.frameCount % 3 == 0)
        {
            agent.SetDestination(targetPos);
        }
    }

    void LateUpdate()
    {
        if (agent.velocity.sqrMagnitude > 0.1f)
        {
            Vector3 moveDir = Vector3.Slerp(agent.steeringTarget - transform.position, agent.velocity, velocityAlignmentWeight);
            moveDir.y = 0;
            moveDir.Normalize();

            if (moveDir.sqrMagnitude > 0.1f)
            {
                Quaternion targetRot = Quaternion.LookRotation(moveDir);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
            }
        }
    }
}