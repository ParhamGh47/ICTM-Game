using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Keeps the main menu song running across the menus.
///
/// The song belongs to the Menu scene, but the object carrying it is moved to <see cref="Object.DontDestroyOnLoad"/>
/// so it survives every scene change. Each time a scene loads this decides what to do with it:
///
///  - a menu scene (menu, level list, controls, tips, customize) keeps the song playing, picking up
///    where it left off so walking through the menus is one continuous piece of music,
///  - anything else - a level, a story scene - pauses it, and coming back to a menu starts the song
///    again from the top instead of dropping the player into the middle of it.
///
/// Scene names are compared by name, so a scene only has to be added to <see cref="MenuScenes"/> to
/// keep the soundtrack.
/// </summary>
public class MenuSoundtrackHandler : MonoBehaviour
{
    /// <summary>Scenes the menu song plays in.</summary>
    private static readonly string[] MenuScenes =
    {
        "Menu",
        "Levels",
        "Option",
        "Tips",
        "Customize",
    };

    private AudioSource audioSource;
    private string lastSceneName;

    void Awake()
    {
        if (FindObjectsOfType<MenuSoundtrackHandler>().Length > 1)
        {
            Destroy(gameObject);
            return;
        }

        DontDestroyOnLoad(gameObject);
        audioSource = GetComponent<AudioSource>();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (KeepsSoundtrack(scene.name))
        {
            // Arriving from a scene that did not have the song (a level): start it over rather than
            // dropping the player into the middle of it. Coming from another menu scene - including
            // the first load of the game - it simply carries on.
            bool afterSilence = !string.IsNullOrEmpty(lastSceneName) && !KeepsSoundtrack(lastSceneName);

            if (afterSilence)
            {
                audioSource.Stop();
                audioSource.Play();
            }
            else if (!audioSource.isPlaying)
            {
                audioSource.Play();
            }
        }
        else
        {
            if (audioSource.isPlaying)
                audioSource.Pause();
        }

        lastSceneName = scene.name;
    }

    private static bool KeepsSoundtrack(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
            return false;

        for (int i = 0; i < MenuScenes.Length; i++)
            if (MenuScenes[i] == sceneName)
                return true;

        return false;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
}
