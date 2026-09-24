using UnityEngine;

/// <summary>
/// Marks something the truck's impact dust should never be kicked up against.
///
/// <see cref="CollisionSound"/> spawns a puff of dust wherever the truck hits, which is what makes a
/// kerb, a barrier or a landing read as a hit. Some things do not want it: a target has its own effect
/// when it dies, and a roadside sign is scenery rather than something the player is meant to feel
/// crashing into. Those objects carry this component instead, and the dust is left out for them.
///
/// It is a component rather than a tag on purpose. A tag would have to sit on the prefab's root, and a
/// target clears its own tag the moment it dies - which is exactly the moment the truck touches it - so
/// a tag can be gone by the time the collision is handled. It also cannot be given to the signs, which
/// are untagged. A component is found by walking up from whichever collider was actually touched, so it
/// survives both of those, and it reads in the Inspector.
///
/// It has no fields and does nothing by itself: what it means is decided entirely by whoever looks for
/// it.
/// </summary>
[DisallowMultipleComponent]
public class NoImpactParticles : MonoBehaviour
{
}
