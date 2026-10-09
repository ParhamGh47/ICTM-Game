using UnityEngine;

/// <summary>
/// Marks a piece of the level's own solid furniture that straddles the road - the Block wall a level starts
/// behind, and the Flag that finishes one - as something a reset must never put the truck inside.
///
/// A reset normally has one job: put the truck back on the road, a little way back up it. That is the right
/// answer everywhere the road is empty, and the wrong answer at the two places a level deliberately puts
/// something across it. The steps a reset takes backwards are whole road nodes and a distance in metres, so at
/// the very start of a level - where there is nowhere behind the truck but the gate it started behind - the
/// search happily lands the truck on the far side of the wall, or inside it: facing the barrier, on the wrong
/// side of the line the level began at, and stuck in something the player cannot drive through. The same is
/// true of the finish gate if a reset is pressed just after it.
///
/// <see cref="CarController"/> looks for this component when a reset is done and keeps the truck on the side of
/// it the level races on.
///
/// Which side that is cannot be worked out from the gate itself, because the two gates need opposite answers:
/// the course runs *past* the Block at the start, so a truck left behind it is off the course, while the course
/// runs *up to* the Flag at the finish, so a truck left beyond it has left the course. So the gate says, in
/// <see cref="side"/>, which side the racing happens on, and a reset that would land the truck on the other one
/// is moved to the racing side of the gate.
///
/// The test is a gate the truck is *near*, not one it is *inside*: a reset steps back up the road a measured
/// distance, so it usually lands a metre or two clear of the wall rather than straddling it, and an overlap
/// test would say everything was fine while leaving the truck facing a barrier on the wrong side of the line
/// the level began at. Distance along the road, up to <see cref="CarController.resetBlockerReach"/>, is what
/// decides. A gate further away than that is not what the reset was aiming at, which is what keeps a walk
/// back to the ramp before a jump from being dragged forward through the start gate.
///
/// It is a component rather than a tag or a name for the reasons <see cref="NoImpactParticles"/> gives. A
/// prefab that has not been given it - every obstacle, sign and prop in the game - is not one, so this
/// changes nothing except the pieces of furniture that opt in.
/// </summary>
[DisallowMultipleComponent]
public class ResetBlocker : MonoBehaviour
{
    /// <summary>Which side of this gate a reset has to leave the truck on.</summary>
    public enum Side
    {
        /// <summary>The course runs past the gate, so the truck must not be left behind it. The Block wall a
        /// level starts behind is one of these: behind it there is no level.</summary>
        PastIt,

        /// <summary>The course runs up to the gate, so the truck must not be left beyond it. The finish Flag is
        /// one of these: beyond it is the end of the level rather than the way to the end.</summary>
        BeforeIt,
    }

    [Tooltip("Which side of this gate the level is raced on. A reset is kept on that side, whichever way up " +
             "the road runs and whichever way the truck was facing.")]
    public Side side = Side.PastIt;
}
