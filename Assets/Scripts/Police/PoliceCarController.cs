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

    [Header("Siren")]
    [Tooltip("The looping siren. Left empty, one is made - a source on this car and, failing a clip, the " +
             "generated wail (see PoliceSirenClip) - so a police car dropped into a level is heard without " +
             "being wired first. Assign a source carrying your own looping clip to use that instead; turn " +
             "Generate Siren If Missing off for a car that should be silent. Wrap the source in a 3D blend, " +
             "or it will be heard at one volume wherever the car is.")]
    public AudioSource siren;

    [Tooltip("Make a siren when none is assigned: a source on this car, and the generated wail if that " +
             "source has no clip of its own.")]
    public bool generateSirenIfMissing = true;

    [Tooltip("Siren volume before the player's own setting.")]
    [Range(0f, 1f)] public float sirenVolume = 0.8f;

    [Tooltip("How much of that a car standing still is worth. A police car stopped across the road is still " +
             "a police car.")]
    [Range(0f, 1f)] public float sirenAtStandstill = 0.35f;

    [Tooltip("Seconds the siren takes to reach its volume and to fade out again.")]
    public float sirenFadeSeconds = 1.2f;

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

        SetUpSiren();
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

        UpdateSiren();
    }

    // ------------------------------------------------------------------
    //  Siren
    // ------------------------------------------------------------------

    /// <summary>
    /// Starts the siren, if this car was given one.
    ///
    /// The volume is worked out here rather than left to a <see cref="SoundChannelSource"/>, because it also
    /// follows whether the car is moving - so the bus is told to leave this source alone (see
    /// <see cref="SoundBus.MarkHandled"/>). A car that was given nothing is handed a source and the generated
    /// wail, so the first level to place one has a siren in it rather than silence.
    /// </summary>
    private void SetUpSiren()
    {
        if (siren == null && generateSirenIfMissing)
        {
            GameObject go = new GameObject("Siren");
            go.transform.SetParent(transform, false);

            siren = go.AddComponent<AudioSource>();

            // Positional, because a siren comes from the car: the player should hear it come up behind them.
            siren.spatialBlend = 1f;
            siren.minDistance = 6f;
            siren.maxDistance = 110f;
            siren.dopplerLevel = 0.2f;
        }

        if (siren == null) return;

        siren.playOnAwake = false;
        siren.loop = true;
        siren.volume = 0f;

        if (siren.clip == null && generateSirenIfMissing)
            siren.clip = PoliceSirenClip.Shared;

        SoundBus.MarkHandled(siren);

        if (siren.clip != null && !siren.isPlaying) siren.Play();
    }

    /// <summary>
    /// Brings the siren up while the car is on the road and eases it back when the car is standing still, so a
    /// car that appears in the distance does not arrive at full volume out of nowhere. The player's ENVIRONMENT
    /// setting is folded in each frame, so moving that slider is heard immediately - including while the car is
    /// racing the player, which is when it matters most.
    /// </summary>
    private void UpdateSiren()
    {
        if (siren == null) return;

        float moving = agent != null ? Mathf.Clamp01(agent.velocity.magnitude / Mathf.Max(0.1f, baseSpeed)) : 0f;

        float target = siren.clip == null
            ? 0f
            : sirenVolume * SoundSettings.Volume(SoundChannel.Environment) *
              Mathf.Lerp(sirenAtStandstill, 1f, moving);

        siren.volume = Mathf.MoveTowards(siren.volume, target,
            Time.deltaTime / Mathf.Max(0.05f, sirenFadeSeconds));
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