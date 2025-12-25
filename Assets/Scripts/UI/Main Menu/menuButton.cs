using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class menuButton : MonoBehaviour
{
    // Start is called before the first frame update
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
