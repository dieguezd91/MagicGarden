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

        // Phase first: the sting is game-over screen feedback, so it must start after the game is "over"
        // to be routed to the UI mixer group instead of being muffled with the game.
        if (GameFlowController.Instance != null)
            GameFlowController.Instance.SetPhase(GamePhase.GameOver);

        SoundManager.Instance?.Stop("Player_Low_Health_Heartbeat_Loop");
        SoundManager.Instance?.Play("Game_Over");

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

        // After the phase change, like the game-over sting, so it routes to the UI group instead of being muffled.
        SoundManager.Instance?.Play("To_Be_Continued");

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
        if (gameResetController == null)
        {
            Debug.LogError("GameResetController missing on GameOverController.");
            return;
        }

        // Reset only once the transition covers the screen: resetting hides the game-over panel and
        // switching to Day re-shows the HUD, so doing it on click would flash the game view before the fade.
        if (SceneLoaderManager.Instance != null)
            SceneLoaderManager.Instance.LoadGameScene(ResetAfterGameOver);
        else
            ResetAfterGameOver();
    }

    public void GameOverMainMenu()
    {
        if (gameResetController == null)
        {
            Debug.LogError("GameResetController missing on GameOverController.");
            return;
        }

        // See GameOverRestart: reset behind the transition, not on click.
        if (SceneLoaderManager.Instance != null)
            SceneLoaderManager.Instance.LoadMenuScene(ResetAfterGameOver);
        else
            ResetAfterGameOver();
    }

    private void ResetAfterGameOver()
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
            gameResetController.ResetGame();
    }
}
