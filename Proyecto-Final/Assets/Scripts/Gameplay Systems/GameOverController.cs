using UnityEngine;
using System.Collections;

[RequireComponent(typeof(GameResetController))]
public class GameOverController : MonoBehaviour
{
    [SerializeField] private float gameOverDelay = 2f;
    [SerializeField] private GameObject home;
    [SerializeField] private PauseController pauseController;

    private HouseLifeController homeLife;
    private GameResetController gameResetController;

    private GameOverRestartButton restartButton;
    private GameOverMainMenuButton gameOverMainMenuButton;
    private GameOverMainMenuButton continueMainMenuButton;

    private void Awake()
    {
        gameResetController = GetComponent<GameResetController>();
        if (gameResetController == null)
        {
            Debug.LogError("GameResetController missing on GameOverController!");
        }

        if (pauseController == null)
        {
            pauseController = FindObjectOfType<PauseController>();
        }

        if (home == null)
        {
            home = GameObject.FindGameObjectWithTag("Home");
        }

        if (home != null)
        {
            homeLife = home.GetComponent<HouseLifeController>();
        }
    }

    private void Start()
    {
        if (homeLife != null)
        {
            homeLife.onHouseDestroyed.AddListener(HandleHomeDeath);
        }
        
        CacheButtons();
        BindButtons();
    }

    private void CacheButtons()
    {
        if (UIManager.Instance == null) return;

        if (UIManager.Instance.gameOverPanel != null)
        {
            restartButton = UIManager.Instance.gameOverPanel.GetComponentInChildren<GameOverRestartButton>(true);
            gameOverMainMenuButton = UIManager.Instance.gameOverPanel.GetComponentInChildren<GameOverMainMenuButton>(true);
        }

        if (UIManager.Instance.continuePanel != null)
        {
            continueMainMenuButton = UIManager.Instance.continuePanel.GetComponentInChildren<GameOverMainMenuButton>(true);
        }
    }

    private void BindButtons()
    {
        if (restartButton != null)
            restartButton.OnClick.AddListener(GameOverRestart);

        if (gameOverMainMenuButton != null)
            gameOverMainMenuButton.OnClick.AddListener(GameOverMainMenu);

        if (continueMainMenuButton != null)
            continueMainMenuButton.OnClick.AddListener(GameOverMainMenu);
    }

    private void UnbindButtons()
    {
        if (restartButton != null)
            restartButton.OnClick.RemoveListener(GameOverRestart);

        if (gameOverMainMenuButton != null)
            gameOverMainMenuButton.OnClick.RemoveListener(GameOverMainMenu);

        if (continueMainMenuButton != null)
            continueMainMenuButton.OnClick.RemoveListener(GameOverMainMenu);
    }

    private void OnDestroy()
    {
        if (homeLife != null)
        {
            homeLife.onHouseDestroyed.RemoveListener(HandleHomeDeath);
        }
        
        UnbindButtons();
    }

    private void HandleHomeDeath()
    {
        StartCoroutine(ShowGameOverAfterDelay());
    }

    private IEnumerator ShowGameOverAfterDelay()
    {
        yield return new WaitForSeconds(gameOverDelay);

        SoundManager.Instance?.Stop("Player_Low_Health_Heartbeat_Loop");
        SoundManager.Instance?.Play("Game_Over");

        if (GameFlowController.Instance != null)
            GameFlowController.Instance.SetPhase(GamePhase.GameOver);

        if (UIManager.Instance != null && UIManager.Instance.gameOverPanel != null)
        {
            UIManager.Instance.gameOverPanel.SetActive(true);

            if (pauseController != null)
            {
                pauseController.Pause();
            }
            else
            {
                Debug.LogError("PauseController missing on GameOverController.");
            }
        }
    }

    public void ShowContinuePanel()
    {
        if (GameFlowController.Instance != null)
            GameFlowController.Instance.SetPhase(GamePhase.GameOver);

        if (UIManager.Instance != null && UIManager.Instance.continuePanel != null)
        {
            UIManager.Instance.StartCoroutine(UIManager.Instance.AnimateContinuePanel());
            
            if (pauseController != null)
            {
                pauseController.Pause();
            }
            else
            {
                Debug.LogError("PauseController missing on GameOverController.");
            }
        }
        else
        {
            Debug.LogWarning("No se encontró el panel de 'Continuar' en el UIManager.");
        }
    }

    public void GameOverRestart()
    {
        if (pauseController != null)
        {
            pauseController.Resume();
        }
        else
        {
            Debug.LogError("PauseController missing on GameOverController.");
        }

        if (GameFlowController.Instance != null)
            GameFlowController.Instance.SetPhase(GamePhase.Day);
            
        if (gameResetController != null)
        {
            gameResetController.ResetGame();
        }
        else
        {
            Debug.LogError("GameResetController missing on GameOverController.");
            return;
        }
        
        if (SceneLoaderManager.Instance != null)
        {
            SceneLoaderManager.Instance.LoadGameScene();
        }
    }

    public void GameOverMainMenu()
    {
        if (pauseController != null)
        {
            pauseController.Resume();
        }
        else
        {
            Debug.LogError("PauseController missing on GameOverController.");
        }

        if (GameFlowController.Instance != null)
            GameFlowController.Instance.SetPhase(GamePhase.Day);
            
        if (gameResetController != null)
        {
            gameResetController.ResetGame();
        }
        else
        {
            Debug.LogError("GameResetController missing on GameOverController.");
            return;
        }
        
        if (SceneLoaderManager.Instance != null)
        {
            SceneLoaderManager.Instance.LoadMenuScene();
        }
    }
}
