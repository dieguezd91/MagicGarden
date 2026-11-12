using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    public SettingsManager SettingsManager { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            SettingsManager = FindObjectOfType<SettingsManager>();
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }
    public void LoadGameScene()
    {
        SceneLoaderManager.Instance.LoadSceneByName(name);
    }

    public void QuitGame()
    {
        if (SceneTransitionController.Instance != null)
            SceneTransitionController.Instance.QuitWithTransition(QuitNow);
        else
            QuitNow();
    }

    private void QuitNow()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}