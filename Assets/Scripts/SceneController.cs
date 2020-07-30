using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneController
{
    public SceneController()
    {
        LoadMenuUI();
    }
    
    public void LoadMenuUI()
    {
        SceneManager.LoadSceneAsync("MenuUI", LoadSceneMode.Additive);
    }

    public void UnloadMenuUI()
    {
        SceneManager.UnloadSceneAsync("MenuUI");
    }
}
