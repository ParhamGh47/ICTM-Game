using UnityEngine;

/// <summary>
/// What a thing the truck can hit is made of, as far as its impact sound is concerned.
///
/// Each value picks one of the game's impact voices rather than describing a material in the abstract, so the
/// list is short and each one is a sound the player has heard. The things that use each are in its own note,
/// and a prefab that has not been given this component at all is <see cref="ImpactKind.Solid"/>.
/// </summary>
public enum ImpactKind
{
    /// <summary>
    /// The world: trees, buildings, billboards, barriers, kerbs, the ground. Heavy, and the sound everything
    /// else is measured against - the project's own impact recording, not one of the generated ones.
    /// </summary>
    Solid = 0,

    /// <summary>
    /// A thin, light sheet: the blinder, a log, a waste bin. Sharp, empty, and it rattles after the hit.
    /// </summary>
    Sheet = 1,

    /// <summary>
    /// A big flat panel: the share-the-road sign and the stop sign. Thicker and larger than a blinder, so it
    /// answers with a sharper, longer clang rather than with a rattle.
    /// </summary>
    Panel = 2,

    /// <summary>
    /// A hollow container: a barrel, a crate, a cone. Struck rather than swept aside, so it answers with a
    /// note of its own - a firm knock with a drum-like ring and a short rattle behind it.
    /// </summary>
    Barrel = 3,
}

/// <summary>
/// Marks something the truck can hit with what it is made of, so <see cref="CollisionSound"/> can answer the
/// impact with the right sound.
///
/// The strength of a hit and how loud it is played are not decided here - those are the same whatever was hit,
/// from the same speed and the same thresholds. All this changes is which sound that hit is: a tree is the
/// world and gets the world's voice, a blinder is a thin sheet and rattles, a big sign clangs, and a barrel
/// or a cone knocks hollow. A car is not on the list at all - another car takes the world's own recording,
/// the same as a tree or a building does.
///
/// It is a component rather than a tag, a layer or a name, for the reasons <see cref="NoImpactParticles"/>
/// gives: a tag has to sit on a prefab's root and targets clear their own the moment they die, a layer is a
/// single global choice shared with collision physics, and a name is not reliable on a prefab instance whose
/// own name has been overridden. A component is found by walking up from whichever collider was actually
/// touched, so it does not matter which part of the thing the truck caught, and it reads in the Inspector.
///
/// Leaving it off is the same as <see cref="ImpactKind.Solid"/>, which is what the world already sounded like
/// - so a prefab that has not thought about this keeps the sound it had, and a new one has to opt in.
/// </summary>
[DisallowMultipleComponent]
public class ImpactMaterial : MonoBehaviour
{
    [Tooltip("What this is made of, as far as its impact sound is concerned. Solid is the world - a tree, a " +
             "building, a billboard, a barrier - and is what an object without this component sounds like.")]
    public ImpactKind kind = ImpactKind.Solid;
}
