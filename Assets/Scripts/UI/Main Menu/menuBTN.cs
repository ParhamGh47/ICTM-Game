using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class menuBTN : MonoBehaviour
{

    public void backBtn()
    {
        SceneManager.LoadScene("Menu");
    }

    public void optionBtn()
    {
        SceneManager.LoadScene("Option");
    }

    #region MainMenu

    public void extBtn()
    {
        Application.Quit();
    }
    
    public void playBtn()
    {
        SceneManager.LoadScene("Levels");
    }
    #endregion

    #region Levels

    public void levelOne()
    {
        SceneManager.LoadScene("TW-Start-1");
    }

    public void levelTwo()
    {
        SceneManager.LoadScene("TW-Start-2");
    }

    #endregion
    
}
