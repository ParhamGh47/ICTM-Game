using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Leaves a scene with the same input that opens the pause menu: Escape, or the gamepad's "B"
/// button. The pause menu already owns Escape inside gameplay scenes, so this component is
/// deliberately only placed in the scenes that have no pause menu - the level list, the options
/// screen and the story (typewriter) scenes.
///
/// It uses the project's "Cancel" input axis, which is already bound to both Escape and
/// joystick button 1, so keyboard and gamepad share one code path.
///
/// A text box that is being edited is the one exception. Escape and B both belong to it there - leaving the
/// box and putting the text back the way it was - so a scene with a box on it (the customize screen's colour
/// boxes) would otherwise be left by the very press that was meant to cancel an edit.
///
/// The scene that ends a level is the other. It is a one-way door - the level is over, and what the player
/// does next is continue to the level list - so back would be a way to fall into the level they have just
/// finished, or into it half-finished, without the game ever saying so. Those scenes switch <see cref="allowBack"/>
/// off and leave the continue button as the only way on.
/// </summary>
[DisallowMultipleComponent]
public class EscBack : MonoBehaviour
{
    [Tooltip("Scene to go back to. Must be in Build Settings.")]
    public string targetScene = "Menu";

    [Tooltip("Whether Escape / B leaves this scene at all. Switched off in the story scenes that end a level.")]
    public bool allowBack = true;

    private void Update()
    {
        // A transition already under way owns the screen; let it finish rather than stacking another.
        if (SceneLoader.IsLoading) return;

        // An end-of-level scene is a one-way door: Escape and B do nothing in it, and the player leaves it
        // with the button the scene itself puts up.
        if (!allowBack) return;

        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetButtonDown("Cancel"))
        {
            // Cancel backs out of one thing at a time: out of the box first, out of the scene on the next
            // press, which is what the field itself is about to do with this one.
            if (EditingATextBox()) return;

            SceneLoader.Load(targetScene);
        }
    }

    /// <summary>Whether the highlight is sitting in a text box the player is typing into.</summary>
    private static bool EditingATextBox()
    {
        EventSystem events = EventSystem.current;
        if (events == null || events.currentSelectedGameObject == null) return false;

        TMP_InputField field = events.currentSelectedGameObject.GetComponent<TMP_InputField>();

        return field != null && field.isFocused;
    }
}
