using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelLoader : MonoBehaviour
{
    public string gameplaySceneName = "Core-1";
    public string endScene;

    public void Menu()
    {
        SceneManager.LoadScene("Menu");
    }

    public void Gameplay()
    {
        SceneManager.LoadScene(gameplaySceneName);
    }

    public void LoadEnd()
    {
        if (!string.IsNullOrEmpty(endScene))
        {
            SceneManager.LoadScene(endScene);
        }
    }
}
