using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class menuBTN : MonoBehaviour
{

    public void backBtn()
    {
        SceneLoader.Load("Menu");
    }

    public void optionBtn()
    {
        SceneLoader.Load("Option");
    }

    #region MainMenu

    public void extBtn()
    {
        Application.Quit();
    }
    
    public void playBtn()
    {
        SceneLoader.Load("Levels");
    }

    public void creditsBtn()
    {
        SceneLoader.Load("Credits");
    }

    public void tipsBtn()
    {
        SceneLoader.Load("Tips");
    }

    public void customizeBtn()
    {
        SceneLoader.Load("Customize");
    }

    #endregion

    #region Levels

    public void levelOne()
    {
        OpenLevel(1, "TW-Start-1");
    }

    public void levelTwo()
    {
        OpenLevel(2, "TW-Start-2");
    }

    public void levelThree()
    {
        OpenLevel(3, "TW-Start-3");
    }

    public void levelFour()
    {
        OpenLevel(4, "TW-Start-4");
    }

    /// <summary>
    /// Opens a level's first scene - unless the player has not reached that level yet.
    ///
    /// The level list already switches the locked buttons off and marks them (<see cref="LevelSelectLocks"/>),
    /// so this is the guard behind that rather than the thing that shows it: however the button is reached -
    /// a click, a gamepad, or anything that ever calls these methods - a level that is still locked does not
    /// open.
    /// </summary>
    private void OpenLevel(int level, string sceneName)
    {
        if (!LevelProgress.IsUnlocked(level))
        {
            Debug.Log("[LevelProgress] Level " + level + " has not been reached yet, so it stays locked.");
            return;
        }

        SceneLoader.Load(sceneName);
    }

    #endregion
    
}
