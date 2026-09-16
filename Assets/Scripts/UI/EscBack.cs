using UnityEngine;

/// <summary>
/// Leaves a scene with the same input that opens the pause menu: Escape, or the gamepad's "B"
/// button. The pause menu already owns Escape inside gameplay scenes, so this component is
/// deliberately only placed in the scenes that have no pause menu - the level list, the controls
/// page and the story (typewriter) scenes.
///
/// It uses the project's "Cancel" input axis, which is already bound to both Escape and
/// joystick button 1, so keyboard and gamepad share one code path.
/// </summary>
[DisallowMultipleComponent]
public class EscBack : MonoBehaviour
{
    [Tooltip("Scene to go back to. Must be in Build Settings.")]
    public string targetScene = "Menu";

    private void Update()
    {
        // A transition already under way owns the screen; let it finish rather than stacking another.
        if (SceneLoader.IsLoading) return;

        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetButtonDown("Cancel"))
            SceneLoader.Load(targetScene);
    }
}
