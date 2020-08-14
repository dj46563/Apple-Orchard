using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneController
{
    public void LoadMenuUI()
    {
        SceneManager.LoadSceneAsync("MenuUI", LoadSceneMode.Additive);
    }
    public void UnloadMenuUI()
    {
        SceneManager.UnloadSceneAsync("MenuUI");
    }

    public void LoadGameUI()
    {
        SceneManager.LoadScene("GameUI", LoadSceneMode.Additive);
    }
    public void UnloadGameUI()
    {
        SceneManager.UnloadSceneAsync("GameUI");
    }

    public void LoadLoginUI()
    {
        SceneManager.LoadScene("LoginUI", LoadSceneMode.Additive);
    }
    public void UnloadLoginUI()
    {
        SceneManager.UnloadSceneAsync("LoginUI");
    }
}
