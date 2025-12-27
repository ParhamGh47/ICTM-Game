using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuSoundtrackHandler : MonoBehaviour
{
    private AudioSource audioSource;

    void Awake()
    {
        // Ensure only one instance persists
        if (FindObjectsOfType<MenuSoundtrackHandler>().Length > 1)
        {
            Destroy(gameObject);
            return;
        }

        DontDestroyOnLoad(gameObject);
        audioSource = GetComponent<AudioSource>();

        // Subscribe to scene change events
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Resume soundtrack only for specific scenes
        if (scene.name == "Menu" || scene.name == "Levels" || scene.name == "Option")
        {
            if (!audioSource.isPlaying)
                audioSource.Play();
        }
        else
        {
            if (audioSource.isPlaying)
                audioSource.Pause();
        }
    }

    void OnDestroy()
    {
        // Unsubscribe to avoid memory leaks
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
}
