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
    public void extbtn()
    {
        Application.Quit();
    }
    
    public void playbtn()
    {
        SceneManager.LoadScene("Playground");
    }
    public void optionbtn()
    {
        SceneManager.LoadScene("Option");
    }
}
