using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class menuBTN : MonoBehaviour
{
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    public void extBtn()
    {
        Application.Quit();
    }
    
    public void playBtn()
    {
        SceneManager.LoadScene("Playground");
    }
    public void optionBtn()
    {
        SceneManager.LoadScene("Option");
    }
    public void backBtn()
    {
        SceneManager.LoadScene("Menu");
    }
    
}
