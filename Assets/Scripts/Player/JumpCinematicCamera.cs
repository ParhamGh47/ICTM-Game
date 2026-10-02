using Cinemachine;
using UnityEngine;

/// <summary>
/// The cinematic a jump plays: while the truck is in the air over a break in the road, the view cuts to a
/// tracking shot of it from the side and time slows down for a couple of seconds, and then the ordinary
/// camera is handed back - deliberately before the truck lands, so the driver sees and feels the touchdown
/// from the view they were driving from.
///
/// A jump is recognised by <see cref="JumpAssist"/>, which is on the same truck and already works out the
/// one thing this needs to know: that the truck is in the air with no road under it, which is a break in
/// the road and not a bump, a crest or a kerb. Everything else here is presentation.
///
/// Nothing about the truck is touched. The slow motion is the engine's own time scale, which scales every
/// physics step by the same amount, so the jump follows exactly the trajectory it would have followed at
/// full speed - it simply takes longer to watch. The steering, the grip, the assist over the gap and the
/// landing all behave identically; only the clock is slower.
///
/// The shot is a virtual camera built at runtime rather than a new one in the scene's camera rig, so the
/// rig keeps working exactly as it did: the shot camera only has to out-rank whatever the rig has active
/// while it is on, and it stops being a candidate the moment it is handed back. Both are the ordinary
/// Cinemachine way of doing this, so the blend in and the blend back come from the brain like any other
/// camera change.
///
/// The shot is always played; a truck prefab that should not play it at all leaves <see cref="enable"/> off.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class JumpCinematicCamera : MonoBehaviour
{
    [Header("When")]
    [Tooltip("Whether this truck plays the jump cinematic at all.")]
    public bool enable = true;

    [Tooltip("How long the truck has to have been over the gap before the shot begins. A moment of air is " +
             "a bump; this waits for the truck to be up and clear before cutting away from the drive.")]
    public float startDelay = 0.25f;

    [Tooltip("The longest the shot may run, in real seconds. It is cut short before this whenever the " +
             "truck is close to landing, which is the usual case.")]
    public float maxShowSeconds = 2.6f;

    [Tooltip("How close the ground has to be below the truck before the shot starts handing the view back. " +
             "Kept generous so the return has finished before the wheels touch.")]
    public float endHeightAboveGround = 3.5f;

    [Header("Slow motion")]
    [Tooltip("The time scale the shot runs at. Lower is slower; 1 would be no slow motion at all. Physics " +
             "is scaled with it, so the jump is the same jump, just watched more slowly.")]
    [Range(0.1f, 1f)]
    public float slowMotionScale = 0.4f;

    [Header("The shot")]
    [Tooltip("How far to the side of the truck the shot camera sits.")]
    public float sideDistance = 2.5f;

    [Tooltip("How high above the truck the shot camera sits, before the up-or-down choice below moves it.")]
    public float shotHeight = 1.15f;

    [Tooltip("How far above or below the truck the shot may sit. Whether the shot is taken from above the " +
             "truck or from a little below it is chosen afresh for every jump, the same way the side is, and " +
             "this is how far up or down that choice may go. Kept small so both are still shots of the truck.")]
    public float verticalSpread = 0.6f;

    [Tooltip("How far behind the truck the shot camera sits, so the shot is a little down the road rather " +
             "than straight across.")]
    public float backDistance = 0.5f;

    [Tooltip("How high on the truck the shot is aimed, above its origin.")]
    public float lookHeight = 0.6f;

    [Tooltip("The shot camera's field of view.")]
    public float shotFieldOfView = 45f;

    [Tooltip("How much the shot varies from jump to jump, as a fraction. It picks a side for every jump - " +
             "sometimes left, sometimes right - and whether the shot is taken from above the truck or from a " +
             "little below it, and nudges how far out, how far back and how tight the framing is, and how slow " +
             "the slow motion is, by up to this much. That is what keeps a level's jumps from all playing as " +
             "the same shot.")]
    [Range(0f, 0.5f)]
    public float variation = 0.18f;

    [Tooltip("How close the ground has to be below the truck for the jump to count as finished and a new " +
             "one to be filmable. Used only to decide when the next shot is allowed, so it is the wheels " +
             "being back down rather than merely a road passing under the truck on the way in to land.")]
    public float groundedProbe = 1.5f;

    [Header("Blends")]
    [Tooltip("How long the view takes to move onto the shot.")]
    public float blendInSeconds = 0.4f;

    [Tooltip("How long the view takes to move back to the drive.")]
    public float blendOutSeconds = 0.4f;

    [Tooltip("The shot camera's priority while it is on. Above the level rig's own cameras, whatever they " +
             "are.")]
    public int shotPriority = 2500;

    // ---------------------------------------------------------------- state

    private enum Stage
    {
        Idle,
        Showing,
        Returning,
    }

    private Rigidbody rb;
    private JumpAssist jump;
    private CinemachineBrain brain;
    private CinemachineVirtualCamera shotCamera;
    private CinemachineComposer shotComposer;

    // One shot per jump. The truck is in the air over the same gap for a good while, so without this the
    // shot would be free to start again the moment its own return finished, and play over and over through
    // the one jump. It is cleared when the truck is back on something, which is a new jump's cue.
    private bool playedThisJump;

    private Stage stage = Stage.Idle;

    private float shownFor;
    private float returnedFor;

    private CinemachineBlendDefinition driveBlend;
    private bool driveBlendSaved;

    // What this set the clock to, so the clock is only given back while it is still the one this put
    // there. A pause or the game over screen setting it themselves must not be undone here.
    private float appliedTimeScale = 1f;

    // Shared so the ground check costs no allocation every frame.
    private static readonly RaycastHit[] hits = new RaycastHit[16];

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        jump = GetComponent<JumpAssist>();
    }

    private void Start()
    {
        brain = FindObjectOfType<CinemachineBrain>();

        if (brain != null)
            BuildShotCamera();
    }

    /// <summary>
    /// Builds the shot camera: a virtual camera that follows the truck from a fixed world offset, aimed at
    /// it, sitting at priority 0 until the shot needs it. It is a Cinemachine camera like the level rig's
    /// own, so the brain blends onto it and off it exactly as it blends between those.
    /// </summary>
    private void BuildShotCamera()
    {
        GameObject go = new GameObject("Jump Cinematic Camera");
        go.transform.SetParent(null, false);

        shotCamera = go.AddComponent<CinemachineVirtualCamera>();

        shotCamera.Follow = transform;
        shotCamera.LookAt = transform;
        shotCamera.Priority = 0;
        shotCamera.m_Lens.FieldOfView = shotFieldOfView;

        CinemachineTransposer transposer = shotCamera.AddCinemachineComponent<CinemachineTransposer>();

        // World space: the offset is in world axes rather than the truck's, so the shot is taken from one
        // place beside the road while the truck flies through it - a tracking shot, rather than a camera
        // that spins with every twitch of the truck on take-off.
        transposer.m_BindingMode = CinemachineTransposer.BindingMode.WorldSpace;
        transposer.m_FollowOffset = Vector3.up * shotHeight;

        shotComposer = shotCamera.AddCinemachineComponent<CinemachineComposer>();
        shotComposer.m_TrackedObjectOffset = Vector3.up * lookHeight;
    }

    private void Update()
    {
        // The wheels are back down: this jump is over, and the next one may be filmed. The check is on the
        // ground under the truck rather than on the gap detector alone, because the gap detector also goes
        // quiet while the truck is still in the air with the landing road passing under it - clearing the
        // latch there would let a second shot start on the one jump.
        if (jump != null && !jump.IsOverGap && GroundWithin(groundedProbe))
            playedThisJump = false;

        if (!CanRun())
        {
            // Paused, or the truck is not in a state to be filmed: end any shot now rather than at the end
            // of it, so the view is handed back before the pause takes the clock.
            if (stage != Stage.Idle) Finish();
            return;
        }

        switch (stage)
        {
            case Stage.Idle:
                TryBegin();
                break;

            case Stage.Showing:
                UpdateShowing();
                break;

            case Stage.Returning:
                UpdateReturning();
                break;
        }
    }

    private bool CanRun()
    {
        if (!enable) return false;
        if (brain == null || shotCamera == null) return false;
        if (rb == null || jump == null) return false;

        // A shot over a paused game is a paused shot; the pause owns the clock while it is up.
        if (PauseTracker.Instance != null && PauseTracker.Instance.isPaused) return false;

        return true;
    }

    private void TryBegin()
    {
        if (playedThisJump) return;
        if (!jump.IsOverGap) return;
        if (jump.OverGapTime < startDelay) return;

        BeginShowing();
    }

    private void BeginShowing()
    {
        stage = Stage.Showing;
        shownFor = 0f;

        // The drive's own blend is put back afterwards, so the rig's camera changes keep feeling the way
        // they did either side of the shot.
        driveBlend = brain.m_DefaultBlend;
        driveBlendSaved = true;

        brain.m_DefaultBlend =
            new CinemachineBlendDefinition(CinemachineBlendDefinition.Style.EaseInOut, blendInSeconds);

        // Taken from the truck's heading as it leaves, so the shot is off its side rather than across its
        // nose.
        Vector3 forward = transform.forward;
        forward.y = 0f;
        forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;

        Vector3 right = Vector3.Cross(Vector3.up, forward);

        playedThisJump = true;

        // A corner and a nudge to the framing, chosen afresh every jump. The side and the height are the two
        // that matter most - the same shot from the same quarter every time reads as a canned animation - and
        // both are picked outright rather than nudged: sometimes left, sometimes right, and sometimes a shot
        // from above the truck, sometimes one from a little below it. The rest keeps the shot from being
        // identical even when the quarter happens to repeat.
        float spread = Mathf.Clamp01(variation);
        float side = Random.value < 0.5f ? -1f : 1f;
        float upDown = Random.value < 0.5f ? -1f : 1f;
        float distance = sideDistance * Random.Range(1f - spread, 1f + spread);
        float height = shotHeight + upDown * verticalSpread * Random.Range(0.55f, 1f);
        float back = backDistance * Random.Range(1f - spread, 1f + spread);
        float aim = lookHeight * Random.Range(1f - spread, 1f + spread);
        float lens = shotFieldOfView * Random.Range(1f - spread * 0.5f, 1f + spread * 0.5f);

        CinemachineTransposer transposer = shotCamera.GetCinemachineComponent<CinemachineTransposer>();

        if (transposer != null)
            transposer.m_FollowOffset =
                right * (side * distance) + Vector3.up * Mathf.Max(0.3f, height) - forward * back;

        if (shotComposer != null)
            shotComposer.m_TrackedObjectOffset = Vector3.up * aim;

        shotCamera.m_Lens.FieldOfView = lens;

        shotCamera.Priority = shotPriority;

        // The slow motion is varied a little too, so its rhythm is not metronomic either.
        appliedTimeScale =
            Mathf.Clamp(slowMotionScale * Random.Range(1f - spread * 0.25f, 1f + spread * 0.25f),
                        0.05f, 1f);

        Time.timeScale = appliedTimeScale;
    }

    private void UpdateShowing()
    {
        shownFor += Time.unscaledDeltaTime;

        bool landed = !jump.IsOverGap;

        // Falling and close to the ground: the landing is a moment away. The check is only made while the
        // truck is on its way down, so the ground under a ramp it has just left cannot end the shot early.
        bool nearlyDown = rb.velocity.y < 0f && GroundWithin(endHeightAboveGround);

        bool longEnough = shownFor >= maxShowSeconds;

        if (landed || nearlyDown || longEnough)
            BeginReturning();
    }

    private void BeginReturning()
    {
        stage = Stage.Returning;
        returnedFor = 0f;

        // Time is put back first, so the return happens at the speed the drive runs at and the truck lands
        // at the speed it would have landed at without any of this.
        RestoreTimeScale();

        brain.m_DefaultBlend =
            new CinemachineBlendDefinition(CinemachineBlendDefinition.Style.EaseInOut, blendOutSeconds);

        // Dropping the priority is what hands the view back: the rig's own camera is the highest-priority
        // one again, and the brain blends onto it.
        shotCamera.Priority = 0;
    }

    private void UpdateReturning()
    {
        returnedFor += Time.unscaledDeltaTime;

        if (returnedFor >= blendOutSeconds)
            Finish();
    }

    /// <summary>Ends any shot and puts everything this touched back the way it was.</summary>
    private void Finish()
    {
        if (stage == Stage.Idle) return;

        stage = Stage.Idle;

        if (shotCamera != null) shotCamera.Priority = 0;

        RestoreTimeScale();
        RestoreBlend();
    }

    private void RestoreBlend()
    {
        if (!driveBlendSaved || brain == null) return;

        brain.m_DefaultBlend = driveBlend;
        driveBlendSaved = false;
    }

    /// <summary>
    /// Back to normal speed, but only if the clock is still the slow one this set.
    ///
    /// The pause menu and the game over screen set the time scale themselves, and each own it while it is
    /// theirs: a shot that ended on top of either would otherwise set the clock running again and quietly
    /// unpause the game. So the clock is given back only while it is still the value this put there; if
    /// something else has taken it, it is left alone, and whatever took it puts it back in its own time.
    /// </summary>
    private void RestoreTimeScale()
    {
        if (PauseTracker.Instance != null && PauseTracker.Instance.isPaused) return;
        if (!Mathf.Approximately(Time.timeScale, appliedTimeScale)) return;

        Time.timeScale = 1f;
        appliedTimeScale = 1f;
    }

    private void OnDisable()
    {
        Finish();
    }

    private void OnDestroy()
    {
        if (shotCamera != null) Destroy(shotCamera.gameObject);
    }

    /// <summary>Whether anything solid is within the given distance below the truck.</summary>
    private bool GroundWithin(float distance)
    {
        int count =
            Physics.RaycastNonAlloc(transform.position, Vector3.down, hits, distance, ~0,
                                    QueryTriggerInteraction.Ignore);

        for (int i = 0; i < count; i++)
        {
            Collider collider = hits[i].collider;

            if (collider == null) continue;
            if (collider.transform.IsChildOf(transform)) continue;

            return true;
        }

        return false;
    }
}
