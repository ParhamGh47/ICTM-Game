using UnityEngine;
using RoadArchitect;

/// <summary>
/// Gives the truck a push while it is in the air over a break in the road, so a leap from one stretch of
/// road to the next is easier to clear. Level 4 is built out of such breaks.
///
/// The push is deliberately narrow, and it is narrow so that nothing else has to change. It acts only
/// while the truck is airborne AND there is no road beneath it - together, "in the air over a gap in the
/// road". Every other time it does nothing at all: driving, braking, turning, going over a crest, a kerb
/// or a bump, and off the road on the grass, are all left exactly as they were, because in each of them
/// the truck is either touching the ground or is still over road. Nothing here reads or writes the
/// steering, the throttle, the wheels or the truck's mass - it is a forward force while the wheels are
/// off the ground and nothing else, and its only lasting effect is the speed carried into the landing.
///
/// The speed it pushes towards is a target rather than a lump added on take-off: a slow jump is helped a
/// lot, a jump already at or above the target is not helped at all. That is what makes a jump forgiving
/// without making a fast one faster than the player asked for.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class JumpAssist : MonoBehaviour
{
    [Tooltip("Whether the truck gets any help over a gap at all.")]
    public bool enableAssist = true;

    [Tooltip("How far below the truck still counts as being on the ground. A truck riding its springs sits " +
             "about twenty centimetres up, so anything within a metre and a half means the wheels are in " +
             "contact or about to be - a bump or a crest, not a jump - and no help is given.")]
    public float groundProbe = 1.5f;

    [Tooltip("How far below the truck to look for road. The help only comes when there is no road under the " +
             "truck within this distance, which is what makes a break in the road different from a bump on it.")]
    public float roadProbe = 6f;

    [Tooltip("The slowest horizontal speed (km/h) that counts as jumping rather than falling. Below this the " +
             "truck gets no help, so one that trickles off the end of a road is not flung forwards.")]
    public float activationSpeedKPH = 25f;

    [Tooltip("The speed (km/h) the help pushes the truck towards while it is over a gap. A jump slower than " +
             "this is sped up; one already at or above it is left alone.")]
    public float assistSpeedKPH = 80f;

    [Tooltip("How hard the help accelerates the truck over a gap, in metres per second squared. Kept gentle: " +
             "it is a nudge forwards, not a rocket, and it only has the length of the jump to work in.")]
    public float assistAcceleration = 8f;

    [Tooltip("Ignore the first moments in the air, which is what a kerb, a crest or a scrappy landing looks " +
             "like. A real jump spends longer off the ground than this.")]
    public float airborneDelay = 0.15f;

    [Tooltip("Give up after this long in the air. A driver who has not been helped by then is falling, not " +
             "jumping, and pushing them through a long fall is not what this is for.")]
    public float maxAirTime = 3f;

    private Rigidbody rb;
    private float airborneTime;

    // Shared so the checks cost no allocation every frame.
    private static readonly RaycastHit[] hits = new RaycastHit[16];

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void FixedUpdate()
    {
        if (!enableAssist || rb == null)
            return;

        // On the ground, or still over road: not a jump. Whichever it is, the truck is back on something,
        // so the air clock starts again for the next time it leaves the ground.
        if (HasGroundBelow(groundProbe) || HasRoadBelow(roadProbe))
        {
            airborneTime = 0f;
            return;
        }

        airborneTime += Time.fixedDeltaTime;

        if (airborneTime < airborneDelay || airborneTime > maxAirTime)
            return;

        Vector3 velocity = rb.velocity;
        velocity.y = 0f;

        float speedKPH = velocity.magnitude * 3.6f;

        if (speedKPH >= assistSpeedKPH || speedKPH < activationSpeedKPH)
            return;

        // Along the way it is already travelling, so the help adds speed without steering the truck: a jump
        // that has drifted off line is not quietly pulled back onto it.
        Vector3 direction =
            velocity.sqrMagnitude > 0.01f ? velocity.normalized : transform.forward;

        rb.AddForce(direction * assistAcceleration, ForceMode.Acceleration);
    }

    /// <summary>Whether anything solid is within the given distance below the truck.</summary>
    private bool HasGroundBelow(float distance)
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

    /// <summary>
    /// Whether a road is within the given distance below the truck. A road is recognised the way the rest
    /// of the game recognises one, by the RoadArchitect Road it belongs to rather than by its tag, which is
    /// what keeps a level's terrain from being mistaken for one.
    /// </summary>
    private bool HasRoadBelow(float distance)
    {
        int count =
            Physics.RaycastNonAlloc(transform.position, Vector3.down, hits, distance, ~0,
                                    QueryTriggerInteraction.Ignore);

        for (int i = 0; i < count; i++)
        {
            Collider collider = hits[i].collider;

            if (collider == null) continue;
            if (collider.transform.IsChildOf(transform)) continue;
            if (collider.GetComponentInParent<Road>() == null) continue;

            return true;
        }

        return false;
    }
}
