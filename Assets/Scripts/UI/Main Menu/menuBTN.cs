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
    #endregion

    #region Levels

    public void levelOne()
    {
        SceneLoader.Load("TW-Start-1");
    }

    public void levelTwo()
    {
        SceneLoader.Load("TW-Start-2");
    }

    public void levelThree()
    {
        SceneLoader.Load("TW-Start-3");
    }

    public void levelFour()
    {
        SceneLoader.Load("TW-Start-4");
    }

    #endregion
    
}
