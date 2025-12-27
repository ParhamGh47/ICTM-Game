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
        SceneManager.LoadScene("Levels");
    }
    public void optionBtn()
    {
        SceneManager.LoadScene("Playground"); // This should be 'Option' later
    }
    public void backBtn()
    {
        SceneManager.LoadScene("Menu");
    }
    
}
