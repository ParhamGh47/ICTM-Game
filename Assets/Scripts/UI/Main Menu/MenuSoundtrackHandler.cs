using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuSoundtrackHandler : MonoBehaviour
{
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
        bool isMenuScene = scene.name == "Menu" || scene.name == "Levels" || scene.name == "Option";

        if (isMenuScene)
        {
            if (!string.IsNullOrEmpty(lastSceneName) && 
                lastSceneName != "Menu" && lastSceneName != "Levels" && lastSceneName != "Option")
            {
                audioSource.Stop();
                audioSource.Play();
            }
            else
            {
                if (!audioSource.isPlaying)
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

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
}
