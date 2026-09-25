using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Keeps the mouse pointer out of the way while the player is driving.
///
/// The game is played with the keyboard or a pad, and a pointer sitting in the middle of the road is nothing
/// the player can use - it is only ever what the menus left behind. So in a level's gameplay scene the pointer
/// is hidden, and it comes straight back whenever the game is not being played: the pause menu, the game over
/// panel, and the moment the level is left, all have buttons that are meant to be clicked.
///
/// It ships on the level HUD's own manager (CanvasUI), which every Core-n scene carries, and it decides from
/// the scene's name which scenes it is in at all. Only Core-n is played with a truck under the player's hands;
/// the menus, the story scenes and the loading screen keep their pointer whatever they are holding, and so does
/// the playground, which shares the same HUD prefab and is meant to be poked at with a mouse.
///
/// Nothing here touches <see cref="Cursor.lockState"/>. Locking the pointer is a different promise - it stops
/// it moving - and a game with a menu to click has no business making it: hiding it is enough, and the player's
/// own pointer stays where they left it for the moment they need it again.
/// </summary>
public class GameplayCursor : MonoBehaviour
{
    [Tooltip("The scenes the pointer is hidden in. A level's gameplay scene is Core-n: the menus, the story " +
             "scenes and the loading screen all keep the pointer, and so does the playground.")]
    public string gameplayScenePrefix = "Core-";

    // Whether this scene is one the pointer is hidden in at all, worked out once as the scene starts.
    private bool gameplayScene;

    // What the cursor was last set to, so it is only written when it has to change.
    private bool hidden;

    void Start()
    {
        string scene = SceneManager.GetActiveScene().name;

        gameplayScene = !string.IsNullOrEmpty(gameplayScenePrefix)
            && scene.StartsWith(gameplayScenePrefix, System.StringComparison.OrdinalIgnoreCase);

        Apply();
    }

    void Update()
    {
        Apply();
    }

    void OnApplicationFocus(bool focused)
    {
        // Losing and regaining focus is not a change of state, it is the same state - but the platform may
        // hand the pointer back to the window on the way in, so the state is written again rather than
        // assumed.
        Apply();
    }

    void OnDisable()
    {
        // The scene is being left - or the game is quitting - and whatever comes next is a menu.
        SetHidden(false);
    }

    private void Apply()
    {
        bool shouldHide = gameplayScene && !IsSuspended();

        if (shouldHide == hidden) return;

        SetHidden(shouldHide);
    }

    /// <summary>
    /// Whether the game is not being played at this moment, whatever the reason. The pause menu and the game
    /// over panel both say so through <see cref="PauseTracker"/> - they are the two screens a level can put up
    /// over itself, and both of them are clicked - and the player's attention being somewhere else entirely
    /// comes to the same thing as far as the pointer is concerned.
    /// </summary>
    private static bool IsSuspended()
    {
        if (!Application.isFocused) return true;

        if (PauseTracker.Instance != null) return PauseTracker.Instance.isPaused;

        // No pause tracker in the scene: frozen time is then the only thing left that says the game is not
        // being played, and it is what both of those screens set.
        return Time.timeScale <= 0f;
    }

    private void SetHidden(bool value)
    {
        hidden = value;
        Cursor.visible = !value;
    }
}
