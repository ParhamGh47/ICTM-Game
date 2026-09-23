using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LevelLoader : MonoBehaviour
{
    public string gameplaySceneName = "Core-1";
    public string endScene;

    /// <summary>Back to the main menu.</summary>
    public void Menu()
    {
        SceneLoader.Load("Menu");
    }

    /// <summary>
    /// Back to the level list.
    ///
    /// This is where a finished level leaves the player: the story scenes' Skip button calls it, so the next
    /// level is one click away instead of being buried a scene further back in the main menu. The menu song
    /// starts again on the way in, because the level list is one of the scenes it plays in.
    /// </summary>
    public void Levels()
    {
        SceneLoader.Load("Levels");
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
