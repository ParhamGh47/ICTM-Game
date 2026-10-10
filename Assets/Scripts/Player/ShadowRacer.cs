using System.Collections.Generic;
using UnityEngine;
using RoadArchitect;

/// <summary>
/// A shadow of the player's truck that races it: an almost-black, collisionless copy that runs the whole
/// course beside the player. Built as a prototype for level 5's race.
///
/// It is dropped into a level as a single object and needs nothing wired: the course is read off the level's
/// RoadArchitect roads at the moment the level starts, and the shadow's own shape is a copy of the truck
/// prefab it is given (see <see cref="truckPrefab"/>), with every script, collider and rigidbody taken out
/// of it - so it drives, collides and makes no sound at all, and only the meshes are left.
///
/// <b>The course.</b> A level is a handful of separate roads with breaks between them, and the shadow has to
/// know which order they go in. It works that out for itself, once, by starting at the road end nearest the
/// player's spawn and then repeatedly hopping to the nearest end of a road it has not used yet - the same
/// way a person would trace the route on a map. Ends further apart than <see cref="maxLinkDistance"/> are
/// left unlinked, so a stray road across the map cannot be dragged into the course.
///
/// <b>Breaks in the road.</b> Where two roads meet with a real gap between them - a jump - the shadow is
/// thrown across it: it leaves the end of one road and lands on the start of the next along a ballistic
/// arc, with the nose pitched up on the way out and down on the way in. The arc's height is worked out from
/// its own flight time, so a short hop is a hop and a long leap is a proper leap. A seam with no gap (two
/// stretches of road that simply meet) is driven over as road.
///
/// <b>Weaving.</b> Because it cannot be touched by anything, it is allowed to move about far more than the
/// player can: two sine waves of different lengths slide it from one side of its lane to the other as it
/// goes, and the heading follows the movement rather than the road, so it leans and aims as it swings. The
/// weave is the same on every surface and always stays on the asphalt.
///
/// <b>Difficulty.</b> It travels at the speed the level's difficulty calls for (see <see cref="easySpeedKPH"/>
/// and friends), read once as the level starts, the same way a level reads its own time and target.
///
/// <b>Removing it.</b> This is a prototype. Deleting the "Shadow Racer" object from a level removes it
/// completely, and this script and "Shadow Truck.prefab" can then be deleted with it.
/// </summary>
[DisallowMultipleComponent]
public class ShadowRacer : MonoBehaviour
{
    [Header("The shadow of")]
    [Tooltip("The visual the shadow wears - 'Shadow Truck.prefab', a script-free copy of the player truck. " +
             "It is instantiated as the shadow's body.")]
    public GameObject truckPrefab;

    [Tooltip("The truck it races. Left empty, the player truck in the scene is found by its tag.")]
    public Transform player;

    [Header("Speed (km/h) - one per difficulty")]
    [Tooltip("The speed the shadow runs at on Easy - a pace a tidy run leaves behind.")]
    public float easySpeedKPH = 60f;

    [Tooltip("The speed on Medium - the game as authored.")]
    public float mediumSpeedKPH = 75f;

    [Tooltip("The speed on Hard: a race the player has to drive for, but still short of their own top speed.")]
    public float hardSpeedKPH = 90f;

    [Tooltip("How quickly it works up to that speed from the start line, in metres per second squared. It " +
             "pulls away with the player rather than being at speed on the first frame.")]
    public float acceleration = 5f;

    [Tooltip("How far ahead of the player, in metres along the road, it lines up at the start. A little in " +
             "front reads as a rival on the grid rather than as a truck sitting inside the player.")]
    public float startAhead = 6f;

    [Header("Rubber banding")]
    [Tooltip("Whether the shadow adjusts its pace to stay near the player. Switched off, it just runs at " +
             "its difficulty's speed and either runs away or gets left behind.")]
    public bool rubberBand = true;

    [Tooltip("How far behind the player, in metres, the shadow may fall before it starts pushing to catch " +
             "up. This is the one that stops the player running away with the race.")]
    public float catchUpRange = 80f;

    [Tooltip("How much faster it may run while catching up: 1.45 is nearly half again its speed. Kept " +
             "modest on purpose - a shadow that suddenly sprints is worse than one that is simply quick.")]
    public float catchUpBoost = 1.45f;

    [Tooltip("How far ahead of the player, in metres, the shadow may get before it eases off to stay in " +
             "reach.")]
    public float leadRange = 80f;

    [Tooltip("How slow it eases down to while running ahead: 0.8 is a fifth off its speed.")]
    public float leadEase = 0.8f;

    [Tooltip("How often, in seconds, the player's place on the course is worked out for the rubber band. " +
             "It is a search along the roads, so it is not worth doing every frame.")]
    public float paceRefresh = 0.15f;

    [Tooltip("How far apart the shadow and the player may get, in metres, before the leash stops pulling and " +
             "simply puts the shadow back beside the player. The pace nudge can only make up so much ground; a " +
             "gap wider than this is closed by moving. 0 leaves it to the pace alone.")]
    public float snapRange = 180f;

    [Tooltip("Where the shadow is put when that gap is closed: this far behind the player, so the race picks " +
             "up again with it in sight instead of sitting inside the player's own truck.")]
    public float snapSpacing = 12f;

    [Header("Course")]
    [Tooltip("The furthest apart two road ends may be and still be treated as the same course. A road whose " +
             "nearest end is further off than this is not part of the route.")]
    public float maxLinkDistance = 170f;

    [Tooltip("A break wider than this is a jump to be thrown across; anything narrower is just a seam and is " +
             "driven over.")]
    public float jumpGapMin = 4f;

    [Tooltip("The lowest a jump's arc rises above the straight line between take-off and landing, in metres, " +
             "so even a short hop has some air under it.")]
    public float minJumpArc = 1.4f;

    [Tooltip("The highest that arc may rise, however long the leap.")]
    public float maxJumpArc = 16f;

    [Tooltip("How far above the road surface the shadow sits. The truck rides its own springs, so its meshes " +
             "want lifting a little off the spline.")]
    public float rideHeight = 0.15f;

    [Header("Weaving")]
    [Tooltip("How far the shadow swings off the middle of the road, in metres, on its main weave. Enough to " +
             "read as a car working its lane, not enough to look like it is being thrown about.")]
    public float weaveAmplitude = 1.3f;

    [Tooltip("The length of one full swing of that weave, in metres. Long and lazy rather than quick.")]
    public float weaveWavelength = 55f;

    [Tooltip("A second, shorter weave laid over the first, so the movement never settles into a rhythm.")]
    public float weaveSecondAmplitude = 0.5f;

    public float weaveSecondWavelength = 22f;

    [Tooltip("Offsets the weave, so a shadow placed or restarted starts where it is rather than always " +
             "crossing the middle at the same points.")]
    public float weavePhase;

    [Header("Look")]
    [Tooltip("The colour every material on the shadow is taken down to: near-black with the faintest cool " +
             "cast, so it reads as a shadow rather than as black paint - and still a shade above black, so it " +
             "has a surface to catch what light there is.")]
    public Color shadowTint = new Color(0.07f, 0.07f, 0.085f, 1f);

    [Tooltip("The colour the shadow glows with from inside, which is all that keeps it from vanishing in a " +
             "level with almost no light in it. Deliberately a near-neutral cold grey rather than a colour of " +
             "its own - a tint strong enough to notice stops reading as a shadow and starts reading as a " +
             "glowing truck.")]
    public Color shadowGlow = new Color(0.42f, 0.46f, 0.6f, 1f);

    [Tooltip("How bright that glow is. Kept low on purpose: this is what makes the shadow visible in the " +
             "dark, not what turns it into a lamp.")]
    public float glowStrength = 0.3f;

    [Tooltip("How far a turn is allowed to bank the shadow over, in degrees. A car leans into a corner; this " +
             "is that lean.")]
    public float maxBank = 3f;

    [Tooltip("How strongly the turn rate is turned into bank.")]
    public float bankResponse = 1.6f;

    [Tooltip("How quickly the heading follows the course. Lower is looser, which suits a shadow.")]
    public float turnResponse = 9f;

    [Header("Shadow fire")]
    [Tooltip("The colour the fire burns brightest at. A cool charcoal rather than a true black, because fire " +
             "that is actually black cannot be seen against a dark level - but light enough that the flame " +
             "reads as burning rather than as smoke. Each tongue starts at the truck's own colour and blooms " +
             "into this as it climbs.")]
    public Color particleColour = new Color(0.36f, 0.35f, 0.44f, 0.9f);

    [Tooltip("Flames per second from each burning part at full quality. There is one fire per part - six on " +
             "the player truck - so this is a lot of small fires rather than one big one.")]
    public float particleRate = 90f;

    [Tooltip("The most flames each part's fire may have alive at once.")]
    public int particleMax = 150;

    [Tooltip("How large each tongue of flame is, in metres. Everything about the fire's size and speed is " +
             "scaled from this, so it is the one dial for how big the fire is.")]
    public float particleRadius = 2f;

    [Tooltip("How wide around each burning part the fire is born, in metres.")]
    public float emitterRadius = 0.4f;

    [Tooltip("How much of a part's fire goes out to its sides and behind the truck, as a share of the rate " +
             "above. These are the fewer, shorter flames rather than the climbing ones, so this is under 1.")]
    public float sideRateShare = 0.4f;

    [Tooltip("How large the side and back flames are, as a share of the climbing ones. They are the short " +
             "flames, so this is deliberately well under 1.")]
    public float sideSizeShare = 0.5f;

    [Tooltip("How hard the side flames are thrown backwards, as a multiple of the flame size. This is what " +
             "turns them into a wake streaming off the truck rather than more fire sitting on it.")]
    public float backBias = 1.8f;

    [Tooltip("Extra height above the truck's own centre for the fire to start at, in metres. 0 burns " +
             "straight off the body.")]
    public float particleHeight;

    // ---------------------------------------------------------------- the course

    // One stretch of the course: either a road to be driven or a gap to be jumped.
    private struct Leg
    {
        public Road road;          // null on a jump
        public bool forward;       // road: does travel run from node 0 to the last node
        public Vector3 from;       // jump: take-off
        public Vector3 to;         // jump: landing
        public float length;       // metres of course this leg covers
        public float start;        // metres from the course start to the beginning of this leg

        public bool IsJump { get { return road == null; } }
    }

    private readonly List<Leg> legs = new List<Leg>();
    private float courseLength;
    private float along;

    private Transform body;
    private Material puffMaterial;
    private Texture2D puffTexture;
    private Material fallbackShadowMaterial;

    private float targetSpeed;
    private float speedNow;

    private float bank;
    private float lastYaw;
    private bool hasYaw;

    // The player's own place on the course, for the rubber band, and when it was last worked out.
    private float playerAlong;
    private float nextPaceTime;

    // The parts of the truck the fire burns from, by the names the models give them: its four wheels, its
    // engine and its lamps. A truck that has none of these is lit as a whole instead - see BuildAura.
    private static readonly string[] BurnPartNames =
    {
        "WheelFL", "WheelFR", "WheelRL", "WheelRR", "Engine", "HeadLights",
    };

    // ---------------------------------------------------------------- life

    private void Awake()
    {
        if (player == null)
        {
            GameObject found = GameObject.FindGameObjectWithTag("Player");
            if (found != null) player = found.transform;
        }
    }

    private void Start()
    {
        targetSpeed = DifficultySpeed() / 3.6f;

        BuildCourse();

        if (legs.Count == 0)
        {
            Debug.LogWarning(
                "[ShadowRacer] No road course could be built, so the shadow has nowhere to drive. It needs " +
                "the level's RoadArchitect roads to be in the scene when it starts.", this);
            return;
        }

        BuildBody();
        BuildAura();

        // Line up where the player lines up, a little in front, so the race begins together.
        along = NearestAlong(ResolveOrigin()) + startAhead;
    }

    /// <summary>
    /// The speed the shadow is actually running at this frame: the difficulty's own speed, nudged up when
    /// it has fallen behind the player and down when it has pulled ahead.
    ///
    /// The nudge only ever scales the difficulty's speed, so Hard still feels faster than Easy - the
    /// rubber band is a leash on top of the difficulty, not a replacement for it. The player's own place on
    /// the course is a search along the roads, so it is only re-found a few times a second rather than every
    /// frame, and the shadow simply keeps its last reading in between.
    /// </summary>
    private float WantedSpeed()
    {
        if (!rubberBand || player == null)
            return targetSpeed;

        if (Time.time >= nextPaceTime)
        {
            nextPaceTime = Time.time + Mathf.Max(0.02f, paceRefresh);
            playerAlong = NearestAlong(player.position);
        }

        float gap = along - playerAlong;        // positive: the shadow is ahead of the player

        // The pace nudge can only make up ground a little at a time, so once the two have drifted too far
        // apart the gap is closed by moving rather than by driving. Anything narrower than this is left to the
        // nudge above, which is what keeps the ordinary, close racing smooth; only a gap the pace could not
        // realistically close is answered with a move.
        if (snapRange > 0f && Mathf.Abs(gap) > snapRange)
        {
            Snap(playerAlong - snapSpacing);
            return targetSpeed;
        }

        if (gap < 0f)
        {
            float behind = Mathf.Clamp01(-gap / Mathf.Max(1f, catchUpRange));
            return targetSpeed * Mathf.Lerp(1f, catchUpBoost, behind);
        }

        float ahead = Mathf.Clamp01(gap / Mathf.Max(1f, leadRange));
        return targetSpeed * Mathf.Lerp(1f, leadEase, ahead);
    }

    /// <summary>
    /// Puts the shadow at a place on the course it did not drive to - the level's own road, a little behind
    /// the player - and re-hangs it there cleanly. The heading is re-read off the road rather than swung round
    /// from wherever it happened to be, and the bank is dropped, so arriving somewhere new does not look like
    /// a pivot; and because this only ever fires with the two far enough apart that the shadow is out of sight,
    /// the move itself is not seen.
    /// </summary>
    private void Snap(float distance)
    {
        along = Mathf.Clamp(distance, 0f, courseLength);

        hasYaw = false;
        bank = 0f;

        Vector3 position, forward;
        Sample(along, out position, out forward);

        transform.position = position;

        if (forward.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
    }

    /// <summary>The speed this difficulty asks for, km/h. Read once, as a level reads its own numbers.</summary>
    private float DifficultySpeed()
    {
        switch (GameDifficulty.Current)
        {
            case DifficultyLevel.Easy: return easySpeedKPH;
            case DifficultyLevel.Hard: return hardSpeedKPH;
            default: return mediumSpeedKPH;
        }
    }

    /// <summary>Where the race starts: the player's own position, or this object's if there is no player.</summary>
    private Vector3 ResolveOrigin()
    {
        return player != null ? player.position : transform.position;
    }

    private void Update()
    {
        if (legs.Count == 0 || body == null)
            return;

        // Work up to speed rather than being at it, so it pulls away from the line - and towards the pace
        // the rubber band asks for, which nudges it back towards the player from either side.
        speedNow = Mathf.MoveTowards(speedNow, WantedSpeed(), acceleration * Time.deltaTime);

        float remaining = courseLength - along;
        if (remaining <= 0f)
            speedNow = 0f;

        along = Mathf.Min(along + speedNow * Time.deltaTime, courseLength);

        Vector3 position, forward;
        Sample(along, out position, out forward);

        // The heading is read off the course a short way ahead - including the weave, so the shadow aims
        // where it is actually going rather than where the road goes - which is what makes a swing read as
        // the car turning.
        float step = Mathf.Max(0.75f, speedNow * 0.05f);
        Vector3 aheadPosition, aheadForward;
        Sample(along + step, out aheadPosition, out aheadForward);

        // The rise and fall is kept in, so the shadow pitches with the road's own slope and lifts its nose
        // over a jump and drops it onto the landing, rather than staying level however the ground runs.
        Vector3 heading = aheadPosition - position;

        if (heading.sqrMagnitude < 0.0001f)
            heading = forward;

        if (heading.sqrMagnitude < 0.0001f)
            heading = transform.forward;

        heading.Normalize();

        // Bank into the turn, read off how fast the heading is swinging.
        float yaw = Mathf.Atan2(heading.x, heading.z) * Mathf.Rad2Deg;

        float yawRate = hasYaw ? Mathf.DeltaAngle(lastYaw, yaw) / Mathf.Max(Time.deltaTime, 0.0001f) : 0f;
        lastYaw = yaw;
        hasYaw = true;

        float wantedBank = Mathf.Clamp(-yawRate * bankResponse, -maxBank, maxBank);
        bank = Mathf.Lerp(bank, wantedBank, 1f - Mathf.Exp(-6f * Time.deltaTime));

        Quaternion look = Quaternion.LookRotation(heading, Vector3.up) * Quaternion.Euler(0f, 0f, bank);

        transform.position = position;
        transform.rotation = Quaternion.Slerp(transform.rotation, look, 1f - Mathf.Exp(-turnResponse * Time.deltaTime));
    }

    // ---------------------------------------------------------------- the course, built once

    /// <summary>
    /// Traces the level's roads into one ordered course: from the road end nearest the player, repeatedly
    /// hop to the nearest end of a road not used yet, and note the gap crossed on the way.
    /// </summary>
    private void BuildCourse()
    {
        legs.Clear();
        courseLength = 0f;

        Road[] roads = FindObjectsOfType<Road>();

        List<Road> remaining = new List<Road>();
        for (int i = 0; i < roads.Length; i++)
        {
            Road road = roads[i];
            if (road == null || road.spline == null) continue;
            if (road.spline.distance <= 0.05f || road.spline.GetNodeCount() < 2) continue;
            remaining.Add(road);
        }

        if (remaining.Count == 0) return;

        Vector3 origin = ResolveOrigin();

        // The road the player starts on, entered at whichever of its ends is nearest them.
        Road first = null;
        bool firstForward = true;
        float best = float.MaxValue;

        for (int i = 0; i < remaining.Count; i++)
        {
            Road road = remaining[i];

            float toStart = Vector3.SqrMagnitude(EndOf(road, true) - origin);
            float toEnd = Vector3.SqrMagnitude(EndOf(road, false) - origin);

            if (toStart < best) { best = toStart; first = road; firstForward = true; }
            if (toEnd < best) { best = toEnd; first = road; firstForward = false; }
        }

        remaining.Remove(first);
        AppendRoad(first, firstForward);

        Vector3 cursor = firstForward ? EndOf(first, false) : EndOf(first, true);

        while (remaining.Count > 0)
        {
            Road next = null;
            bool nextForward = true;
            float nearest = float.MaxValue;

            for (int i = 0; i < remaining.Count; i++)
            {
                Road road = remaining[i];

                float toStart = Vector3.Distance(EndOf(road, true), cursor);
                float toEnd = Vector3.Distance(EndOf(road, false), cursor);

                if (toStart < nearest) { nearest = toStart; next = road; nextForward = true; }
                if (toEnd < nearest) { nearest = toEnd; next = road; nextForward = false; }
            }

            // A gap too wide to be the next stretch of this course: the route ends here.
            if (next == null || nearest > maxLinkDistance) break;

            Vector3 entry = nextForward ? EndOf(next, true) : EndOf(next, false);

            // A real break in the road is a jump; a road that simply carries on is driven over.
            if (Vector3.Distance(cursor, entry) > jumpGapMin)
                AppendJump(cursor, entry);

            AppendRoad(next, nextForward);

            cursor = nextForward ? EndOf(next, false) : EndOf(next, true);
            remaining.Remove(next);
        }
    }

    private void AppendRoad(Road road, bool forward)
    {
        Leg leg = new Leg();
        leg.road = road;
        leg.forward = forward;
        leg.length = road.spline.distance;
        leg.start = courseLength;

        courseLength += leg.length;

        legs.Add(leg);
    }

    private void AppendJump(Vector3 from, Vector3 to)
    {
        Leg leg = new Leg();
        leg.road = null;
        leg.from = from;
        leg.to = to;

        // Time is spent over the horizontal distance; the rise and fall is the arc.
        Vector3 flat = to - from;
        flat.y = 0f;

        leg.length = Mathf.Max(0.1f, flat.magnitude);
        leg.start = courseLength;

        courseLength += leg.length;

        legs.Add(leg);
    }

    private static Vector3 EndOf(Road road, bool start)
    {
        Vector3 position, tangent;
        road.spline.GetSplineValueBoth(start ? 0f : 1f, out position, out tangent);
        return position;
    }

    /// <summary>The place along the course nearest a world point, in metres from the course start.</summary>
    private float NearestAlong(Vector3 world)
    {
        float best = 0f;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < legs.Count; i++)
        {
            Leg leg = legs[i];
            if (leg.IsJump) continue;

            float param = leg.road.spline.GetClosestParam(world, false, true);

            float t = leg.forward ? param : 1f - param;
            float candidate = leg.start + Mathf.Clamp01(t) * leg.length;

            Vector3 position, forward;
            Sample(candidate, out position, out forward);

            float distance = Vector3.SqrMagnitude(position - world);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = candidate;
            }
        }

        return best;
    }

    /// <summary>
    /// The shadow's place and heading on the course at a given distance along it, with the weave applied.
    /// </summary>
    private void Sample(float distance, out Vector3 position, out Vector3 forward)
    {
        position = Vector3.zero;
        forward = transform.forward;

        float clamped = Mathf.Clamp(distance, 0f, courseLength);

        Leg leg = legs[legs.Count - 1];

        for (int i = 0; i < legs.Count; i++)
        {
            if (clamped <= legs[i].start + legs[i].length + 0.0001f) { leg = legs[i]; break; }
        }

        float local = Mathf.Clamp(clamped - leg.start, 0f, leg.length);
        float t = leg.length > 0.0001f ? local / leg.length : 0f;

        Vector3 onRoad;
        Vector3 direction;

        if (!leg.IsJump)
        {
            Vector3 tangent;
            leg.road.spline.GetSplineValueBoth(leg.forward ? t : 1f - t, out onRoad, out tangent);

            // The tangent always points the way the spline's own parameters run, so a leg driven backwards
            // has to be reversed to get the direction of travel.
            direction = new Vector3(tangent.x, 0f, tangent.z);
            if (!leg.forward) direction = -direction;

            if (direction.sqrMagnitude < 0.0001f)
                direction = Vector3.forward;
        }
        else
        {
            direction = leg.to - leg.from;
            direction.y = 0f;

            if (direction.sqrMagnitude < 0.0001f)
                direction = Vector3.forward;

            direction.Normalize();

            onRoad = Vector3.Lerp(leg.from, leg.to, t);

            // The leap: a straight line plus a parabola whose height is its own flight time's worth of fall,
            // so the shadow visibly leaves the road and comes back down onto it.
            float flight = leg.length / Mathf.Max(1f, Mathf.Max(speedNow, 4f));
            float apex = Mathf.Clamp(9.81f * flight * flight * 0.125f, minJumpArc, maxJumpArc);

            onRoad.y += apex * 4f * t * (1f - t);
        }

        direction.Normalize();

        forward = direction;
        position = onRoad + Lateral(clamped, direction) + Vector3.up * rideHeight;
    }

    /// <summary>The weave: how far off the middle of the road the shadow is at a given distance along it.</summary>
    private Vector3 Lateral(float distance, Vector3 forward)
    {
        float swing =
            weaveAmplitude * Mathf.Sin(distance / weaveWavelength * Mathf.PI * 2f + weavePhase) +
            weaveSecondAmplitude * Mathf.Sin(distance / weaveSecondWavelength * Mathf.PI * 2f + weavePhase * 1.7f);

        Vector3 right = Vector3.Cross(Vector3.up, forward);
        right.y = 0f;

        if (right.sqrMagnitude < 0.0001f)
            return Vector3.zero;

        return right.normalized * swing;
    }

    // ---------------------------------------------------------------- the shadow itself

    private void BuildBody()
    {
        if (truckPrefab == null)
        {
            Debug.LogWarning(
                "[ShadowRacer] No truck prefab assigned, so the shadow has no body to wear. Assign " +
                "'Shadow Truck.prefab' in the Inspector.", this);
            return;
        }

        GameObject clone = Instantiate(truckPrefab);
        clone.name = "Shadow Body";
        clone.transform.SetParent(transform, false);
        clone.transform.localPosition = Vector3.zero;
        clone.transform.localRotation = Quaternion.identity;

        fallbackShadowMaterial = CreateShadowMaterial();
        MakeItAShadow(clone);

        body = clone.transform;
    }

    /// <summary>
    /// Turns the truck-shaped copy into a shadow: every surface taken down to one almost-black, the glow of
    /// any lamp put out, and anything that would shine or float left exactly as it is but unlit.
    /// </summary>
    private void MakeItAShadow(GameObject clone)
    {
        MeshRenderer[] meshes = clone.GetComponentsInChildren<MeshRenderer>(true);
        SkinnedMeshRenderer[] skins = clone.GetComponentsInChildren<SkinnedMeshRenderer>(true);

        for (int i = 0; i < meshes.Length; i++) Darken(meshes[i]);
        for (int i = 0; i < skins.Length; i++) Darken(skins[i]);
    }

    /// <summary>
    /// Takes one part of the truck down to shadow.
    ///
    /// A material that has a colour is tinted in place, which keeps whatever texture and detail it already
    /// has - the model's own materials are what give the shadow the truck's shape rather than a flat black
    /// blob - and its shine and any glow are turned down. A material with no colour at all cannot be tinted,
    /// so it is swapped for the shadow material outright rather than left bright.
    /// </summary>
    private void Darken(Renderer renderer)
    {
        if (renderer == null) return;

        Material[] materials = renderer.materials;

        for (int i = 0; i < materials.Length; i++)
        {
            Material material = materials[i];

            bool tinted = false;

            if (material != null)
            {
                if (material.HasProperty("_Color")) { material.SetColor("_Color", shadowTint); tinted = true; }
                if (material.HasProperty("_BaseColor")) { material.SetColor("_BaseColor", shadowTint); tinted = true; }

                if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0.2f);
                if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0.12f);
                if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.12f);

                // The glow is what keeps the shadow readable in a level with almost no light in it.
                if (material.HasProperty("_EmissionColor"))
                {
                    material.EnableKeyword("_EMISSION");
                    material.SetColor("_EmissionColor", shadowGlow * glowStrength);
                }
            }

            if (!tinted && fallbackShadowMaterial != null)
                materials[i] = fallbackShadowMaterial;
        }

        renderer.materials = materials;
        renderer.receiveShadows = true;
    }

    /// <summary>The material a part with no colour of its own is given: an unlit-looking near-black.</summary>
    private Material CreateShadowMaterial()
    {
        string[] candidates = { "Standard", "Universal Render Pipeline/Lit", "Legacy Shaders/Diffuse", "Sprites/Default" };

        Shader shader = null;
        for (int i = 0; i < candidates.Length; i++)
        {
            shader = Shader.Find(candidates[i]);
            if (shader != null) break;
        }

        Material material = new Material(shader);
        material.name = "Shadow (Runtime)";

        if (material.HasProperty("_Color")) material.SetColor("_Color", shadowTint);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", shadowTint);
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0.2f);
        if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0.12f);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.12f);

        if (material.HasProperty("_EmissionColor"))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", shadowGlow * glowStrength);
        }

        return material;
    }

    /// <summary>
    /// The black fire that comes off the shadow: small tongues of flame burning from the truck's own parts -
    /// its wheels, its engine, its lamps - rather than one cloud around the whole shape.
    ///
    /// That is what makes it read as a car that is alight rather than as a truck sitting in smoke: fire on a
    /// burning car catches in places, and a handful of small fires fixed to the parts flames belong to look
    /// like the truck itself is burning. Each fire is bolted to a point on the body, so it turns and travels
    /// with the truck and stays on the part it belongs to.
    ///
    /// Each part carries two fires, not one: the climbing tongues that make the burning visible, and a smaller,
    /// shorter wake that streams off to the sides and behind as the truck travels - see BuildFlameEmitter.
    /// Both are short-lived, which is what makes a flame rather than a puff; the rendered billboards are
    /// stretched a little along their own motion so a rising or trailing dot reads as a tongue.
    /// </summary>
    private void BuildAura()
    {
        puffTexture = CreateFlameTexture();
        puffMaterial = CreatePuffMaterial(puffTexture);

        // The places a car burns: its wheels, its engine and its lamps. A part a given truck does not have is
        // simply skipped, so this is safe on any model.
        List<Transform> parts = new List<Transform>();

        for (int i = 0; i < BurnPartNames.Length; i++)
        {
            Transform part = FindDeep(body, BurnPartNames[i]);
            if (part != null) parts.Add(part);
        }

        // None of the known parts (a different truck): light the whole body instead, so there is still fire.
        if (parts.Count == 0)
            parts.Add(body);

        for (int i = 0; i < parts.Count; i++)
        {
            BuildFlameEmitter(parts[i], false);
            BuildFlameEmitter(parts[i], true);
        }
    }

    /// <summary>
    /// One of the fires bolted to a point on the truck. <paramref name="side"/> picks which of the two it is:
    /// the tall flames that climb straight off the part, or the fewer, shorter ones that crawl out to the
    /// part's sides and stream off behind the truck as it moves.
    ///
    /// They are two fires rather than two directions of one because they are not the same thing. The climbing
    /// flame is the fire you see; the side and back flames are that fire being dragged off the burning truck
    /// as it travels. Keeping them apart is what lets the climbing fire stay the dominant shape while the wake
    /// is a quiet, short fringe of it.
    /// </summary>
    private void BuildFlameEmitter(Transform part, bool side)
    {
        GameObject emitter = new GameObject((side ? "Flame sides - " : "Flame - ") + part.name);
        emitter.transform.SetParent(transform, false);
        emitter.transform.localRotation = Quaternion.identity;
        emitter.transform.localScale = Vector3.one;

        // The part's place on the truck, in the truck's own space, so the fire sits on the part and turns
        // with it.
        emitter.transform.localPosition =
            transform.InverseTransformPoint(part.position) + Vector3.up * particleHeight;

        ParticleSystem system = emitter.AddComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        // The side and back flames are the short ones: a smaller size and a shorter, flatter life are what
        // make them read as a fringe the truck is trailing rather than as more of the fire on top of it.
        float flameSize = particleRadius * (side ? sideSizeShare : 1f);

        ParticleSystem.MainModule main = system.main;
        main.loop = true;
        main.startLifetime = side ? new ParticleSystem.MinMaxCurve(0.16f, 0.34f)
                                  : new ParticleSystem.MinMaxCurve(0.16f, 0.36f);
        main.startSpeed = side ? new ParticleSystem.MinMaxCurve(0.05f, 0.3f)
                               : new ParticleSystem.MinMaxCurve(0.1f, 0.55f);
        main.startSize = new ParticleSystem.MinMaxCurve(flameSize * 0.18f, flameSize * 0.4f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);

        // Left white on purpose: the colour the flames actually show is the gradient over their life, which
        // is what lets a tongue start at the truck's colour and bloom into the flame colour.
        main.startColor = new ParticleSystem.MinMaxGradient(Color.white);
        main.gravityModifier = side ? -0.02f : -0.09f;           // fire climbs, and keeps climbing
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.scalingMode = ParticleSystemScalingMode.Local;
        // The wake carries its own, smaller share of the count, so there are plainly fewer of them than of the
        // climbing flames.
        int cap = side ? Mathf.Max(4, Mathf.RoundToInt(particleMax * sideRateShare)) : particleMax;
        main.maxParticles = Mathf.Max(6, GraphicsQuality.ScaleCount(cap, GraphicsQuality.ParticleScale, 6));
        main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;

        float rate = particleRate * (side ? sideRateShare : 1f);

        ParticleSystem.EmissionModule emission = system.emission;
        emission.rateOverTime = GraphicsQuality.ScaleRate(rate, GraphicsQuality.ParticleScale, side ? 2f : 4f);

        // Born on a small shell around the part, so the flames start on the part itself.
        ParticleSystem.ShapeModule shape = system.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = side ? emitterRadius * 1.8f : emitterRadius;
        shape.radiusThickness = 1f;
        shape.randomDirectionAmount = side ? 0.45f : 0.7f;

        // The climbing fire is thrown in every direction, up included, so it comes off the part all the way
        // round rather than standing up in a column; the wake is thrown out sideways and backwards instead.
        // All three axes are set to the same kind of curve - two constants - because a particle system requires
        // its velocity curves to agree on their mode, and mixing them is a runtime error.
        ParticleSystem.VelocityOverLifetimeModule velocity = system.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;

        if (side)
        {
            // Barely any climb, and a shove backwards with some sideways spread on it. The truck's local -Z is
            // behind it - its heading is applied as a look rotation - so this is the fire being left behind in
            // the shadow's wake rather than the fire burning on it.
            float spread = flameSize * 1.6f;
            float back = particleRadius * backBias;

            velocity.x = new ParticleSystem.MinMaxCurve(-spread, spread);
            velocity.y = new ParticleSystem.MinMaxCurve(flameSize * 0.12f, flameSize * 0.5f);
            velocity.z = new ParticleSystem.MinMaxCurve(-(back + spread * 0.5f), -back * 0.25f + spread * 0.5f);
        }
        else
        {
            float rise = particleRadius * 1.2f;
            float drift = particleRadius * 0.8f;

            velocity.x = new ParticleSystem.MinMaxCurve(-drift, drift);
            velocity.y = new ParticleSystem.MinMaxCurve(rise * 0.7f, rise * 1.7f);
            velocity.z = new ParticleSystem.MinMaxCurve(-drift, drift);
        }

        ParticleSystem.NoiseModule noise = system.noise;
        noise.enabled = true;
        noise.strength = new ParticleSystem.MinMaxCurve(flameSize * 0.1f, flameSize * 0.3f);
        noise.frequency = 1.3f;
        noise.damping = true;
        noise.octaveCount = 2;
        noise.quality = ParticleSystemNoiseQuality.Medium;

        ParticleSystem.RotationOverLifetimeModule spin = system.rotationOverLifetime;
        spin.enabled = true;
        spin.z = new ParticleSystem.MinMaxCurve(-1.2f, 1.2f);

        ParticleSystem.SizeOverLifetimeModule sizeOverLife = system.sizeOverLifetime;
        sizeOverLife.enabled = true;
        sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, FlameCurve());

        ParticleSystem.ColorOverLifetimeModule colourOverLife = system.colorOverLifetime;
        colourOverLife.enabled = true;
        colourOverLife.color = new ParticleSystem.MinMaxGradient(FlameGradient(side));

        ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
        {
            renderer.material = puffMaterial;

            // Stretched along its own motion, but only a little: enough to read as a tongue of flame rather
            // than a dot, without the fast-rising ones drawing out into long streaks.
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = side ? 0.03f : 0.05f;
            renderer.lengthScale = side ? 1.05f : 1.2f;
            renderer.cameraVelocityScale = 0f;

            renderer.sortMode = ParticleSystemSortMode.Distance;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        system.Play();
    }

    /// <summary>Finds a descendant by name, to hang a fire on.</summary>
    private static Transform FindDeep(Transform root, string name)
    {
        Transform[] all = root.GetComponentsInChildren<Transform>(true);

        for (int i = 0; i < all.Length; i++)
        {
            if (all[i].name == name) return all[i];
        }

        return null;
    }

    private void OnDestroy()
    {
        if (puffMaterial != null) Destroy(puffMaterial);
        if (puffTexture != null) Destroy(puffTexture);
        if (fallbackShadowMaterial != null) Destroy(fallbackShadowMaterial);
    }

    /// <summary>
    /// How a flame's size runs over its life: born small, swelling into a tongue just above the base, then
    /// withering away to a point as it burns out.
    /// </summary>
    private static AnimationCurve FlameCurve()
    {
        AnimationCurve curve = new AnimationCurve();
        curve.AddKey(0f, 0.35f);
        curve.AddKey(0.3f, 1.05f);
        curve.AddKey(0.7f, 0.8f);
        curve.AddKey(1f, 0.15f);
        return curve;
    }

    /// <summary>
    /// The colour a tongue of fire runs through over its life, which is what gives it the feel of burning:
    /// it leaves the truck in the truck's own colour, blooms into the flame colour a little way up, and then
    /// cools and darkens as it dies. A flame that is one flat colour for its whole life reads as a puff of
    /// smoke, however it moves.
    /// </summary>
    private Gradient FlameGradient(bool side)
    {
        // The truck's own colour, lifted just enough off black to be seen as the fire's base.
        Color baseColour = Color.Lerp(shadowTint, particleColour, 0.3f);

        Color cooled = new Color(particleColour.r * 0.45f, particleColour.g * 0.45f,
                                 particleColour.b * 0.5f, particleColour.a);

        Gradient gradient = new Gradient();

        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(baseColour, 0f),
                new GradientColorKey(particleColour, 0.28f),
                new GradientColorKey(cooled, 1f),
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(side ? 0.6f : 0.95f, 0.12f),
                new GradientAlphaKey(side ? 0.3f : 0.55f, 0.6f),
                new GradientAlphaKey(0f, 1f),
            });

        return gradient;
    }

    /// <summary>
    /// A tongue of flame, drawn here rather than shipped as art: narrow at the base, swelling just above it
    /// and tapering to a point at the top, with a soft edge so a billboard of it does not read as a cut-out
    /// cone. The base is the bottom of the image, because a particle's up is its texture's up.
    /// </summary>
    private static Texture2D CreateFlameTexture()
    {
        const int size = 64;

        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "Shadow Flame";

        Color[] pixels = new Color[size * size];
        float centre = (size - 1) * 0.5f;

        for (int y = 0; y < size; y++)
        {
            float v = y / (size - 1f);                              // 0 at the base, 1 at the tip

            // Narrow at the base, widest a little above it, tapering to a point at the top. Kept slim: a
            // wide, soft tongue is what reads as a puff of smoke rather than as a flame.
            float halfWidth = (1f - v) * Mathf.Sqrt(Mathf.Max(v, 0.0001f)) * 1.85f;

            for (int x = 0; x < size; x++)
            {
                float nx = (x - centre) / centre;
                float alpha = 0f;

                if (halfWidth > 0.001f)
                {
                    float d = Mathf.Abs(nx) / halfWidth;

                    // A solid core with a soft edge, rather than a falloff across the whole width.
                    alpha = Mathf.Pow(Mathf.Clamp01(1f - d), 0.65f);

                    // Soft tip and base, so the tongue is not cut off flat at either end.
                    alpha *= Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((1f - v) / 0.28f));
                    alpha *= Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(v / 0.14f));

                    // A little grain, so no two flames are quite the same shape.
                    alpha *= Mathf.Lerp(0.72f, 1f, Mathf.PerlinNoise(x * 0.27f + 3.1f, y * 0.27f));
                }

                pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();

        return texture;
    }

    /// <summary>
    /// A material for the puffs, found the same way the tyre smoke finds its own: the sprite shader first,
    /// because it is always in a build and is already unlit and alpha blended, with the particle shaders as
    /// fallbacks.
    /// </summary>
    private static Material CreatePuffMaterial(Texture2D texture)
    {
        string[] candidates =
        {
            "Sprites/Default",
            "Mobile/Particles/Alpha Blended",
            "Legacy Shaders/Particles/Alpha Blended",
            "Particles/Standard Unlit",
            "Unlit/Transparent",
        };

        Shader shader = null;
        for (int i = 0; i < candidates.Length; i++)
        {
            shader = Shader.Find(candidates[i]);
            if (shader != null) break;
        }

        Material material = new Material(shader);
        material.name = "Shadow Flame (Runtime)";
        material.mainTexture = texture;

        if (material.HasProperty("_TintColor")) material.SetColor("_TintColor", Color.white);
        if (material.HasProperty("_Color")) material.SetColor("_Color", Color.white);

        if (material.HasProperty("_SrcBlend"))
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (material.HasProperty("_DstBlend"))
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (material.HasProperty("_ZWrite")) material.SetInt("_ZWrite", 0);
        if (material.HasProperty("_Cull"))
            material.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);

        material.DisableKeyword("_ALPHATEST_ON");
        material.EnableKeyword("_ALPHABLEND_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");

        material.SetOverrideTag("RenderType", "Transparent");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

        return material;
    }
}
