using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;


public class LevelOneSceneManager : MonoBehaviour
{
    public void Gameplay()
    {
        SceneManager.LoadScene("Playground"); // Should be Level-1-Core
    }
}
