using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LevelLoader : MonoBehaviour
{
    public string gameplaySceneName = "Core-1";
    public string endScene;

    public void Menu()
    {
        SceneLoader.Load("Menu");
    }

    public void Gameplay()
    {
        SceneLoader.Load(gameplaySceneName);
    }

    public void LoadEnd()
    {
        if (!string.IsNullOrEmpty(endScene))
        {
            SceneLoader.Load(endScene);
        }
    }
}
