using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoaderManager : MonoBehaviour
{
    public static SceneLoaderManager Instance;

    [Header("Transition Settings")]
    [SerializeField] private bool useFadeTransitions = true;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    #region Public API con Fade

    // Parameterless overloads stay: buttons call LoadMenuScene() through Inspector UnityEvents.
    public void LoadMenuScene() => LoadSceneByIndexWithTransition(0);
    public void LoadGameScene() => LoadSceneByIndexWithTransition(1);

    /// <param name="onCovered">Runs once the transition fully covers the screen, before the scene loads.</param>
    public void LoadMenuScene(System.Action onCovered) => LoadSceneByIndexWithTransition(0, null, onCovered);
    /// <param name="onCovered">Runs once the transition fully covers the screen, before the scene loads.</param>
    public void LoadGameScene(System.Action onCovered) => LoadSceneByIndexWithTransition(1, null, onCovered);

    public void LoadSceneByIndexWithTransition(int sceneIndex, System.Action onLoaded = null, System.Action onCovered = null)
    {
        if (useFadeTransitions && SceneTransitionController.Instance != null)
        {
            SceneTransitionController.Instance.LoadScene(sceneIndex, onLoaded, onCovered);
        }
        else
        {
            onCovered?.Invoke();
            LoadSceneByIndex(sceneIndex);
            onLoaded?.Invoke();
        }
    }

    public void LoadSceneByNameWithTransition(string sceneName, System.Action onLoaded = null)
    {
        if (useFadeTransitions && SceneTransitionController.Instance != null)
        {
            SceneTransitionController.Instance.LoadScene(sceneName, onLoaded);
        }
        else
        {
            LoadSceneByName(sceneName);
            onLoaded?.Invoke();
        }
    }

    #endregion

    public void LoadSceneByIndex(int loadSceneIndex)
    {
        SceneManager.LoadScene(loadSceneIndex);
    }

    public void LoadSceneByName(string loadSceneName)
    {
        SceneManager.LoadScene(loadSceneName);
    }
}