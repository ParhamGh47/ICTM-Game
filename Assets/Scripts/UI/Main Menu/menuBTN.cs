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

    public void playGround()
    {
        SceneManager.LoadScene("Playground");
    }

        public void playType()
    {
        SceneManager.LoadScene("Typewriter");
    }

    #endregion
    
}
