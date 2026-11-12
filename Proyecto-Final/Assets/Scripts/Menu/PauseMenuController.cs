using UnityEngine;
using UnityEngine.SceneManagement;


public class PauseMenuController : UIControllerBase
{
    [SerializeField] private PauseController pauseController; 
    [SerializeField] private UIManager uiManager;

    [Header("UI Panels")]
    [SerializeField] private PauseMenuPanel _pauseMenuPanel;
    [SerializeField] private OptionsMenuPanel _optionsMenuPanel;

    protected override void CacheReferences()
    {
        if (uiManager == null)
        {
            uiManager = FindObjectOfType<UIManager>();
        }
        if (pauseController == null)
        {
            pauseController = FindObjectOfType<PauseController>();
        }
    }

    protected override void ConfigureInitialState()
    {

    }

    private void OnEnable()
    {
        ShowPauseMenu();
    }

    public override void Show()
    {
        _currentState = PanelState.Shown;
        gameObject.SetActive(true);
    }

    public override void Hide()
    {
        // Visibility is managed entirely by the parent OptionsPage.
        // We only update state, but do NOT disable the gameObject.
        _currentState = PanelState.Hidden;
    }

    

    protected override void SetupEventListeners()
    {
        // Subscribe to PauseMenuPanel events
        if (_pauseMenuPanel != null)
        {
            _pauseMenuPanel.OnContinueClicked.AddListener(Continue);
            _pauseMenuPanel.OnOptionsClicked.AddListener(ShowOptions);
            _pauseMenuPanel.OnInstructionsClicked.AddListener(ShowInstructions);
            _pauseMenuPanel.OnMainMenuClicked.AddListener(GoToMainMenu);
            _pauseMenuPanel.OnExitClicked.AddListener(HandleExitClicked);
            _pauseMenuPanel.OnSkipButtonClicked.AddListener(HandleSkipTutorialClicked);
        }

        // Subscribe to OptionsMenuPanel events
        if (_optionsMenuPanel != null)
        {
            _optionsMenuPanel.OnGoBackClicked.AddListener(HideOptions);
        }
    }

    protected override void CleanupEventListeners()
    {
        // Unsubscribe from PauseMenuPanel events
        if (_pauseMenuPanel != null)
        {
            _pauseMenuPanel.OnContinueClicked.RemoveListener(Continue);
            _pauseMenuPanel.OnOptionsClicked.RemoveListener(ShowOptions);
            _pauseMenuPanel.OnInstructionsClicked.RemoveListener(ShowInstructions);
            _pauseMenuPanel.OnMainMenuClicked.RemoveListener(GoToMainMenu);
            _pauseMenuPanel.OnExitClicked.RemoveListener(HandleExitClicked);
            _pauseMenuPanel.OnSkipButtonClicked.RemoveListener(HandleSkipTutorialClicked);
        }

        // Unsubscribe from OptionsMenuPanel events
        if (_optionsMenuPanel != null)
        {
            _optionsMenuPanel.OnGoBackClicked.RemoveListener(HideOptions);
        }
    }

    private void HandleExitClicked()
    {
        GameManager.Instance?.QuitGame();
    }

    public override void HandleUpdate()
    {
        HandlePauseInput();
    }

    private void HandlePauseInput()
    {
        if (!Input.GetKeyDown(KeyCode.Escape) || InputConsumptionManager.IsEscapeConsumed) return;

        UIManager.Instance?.Tooltip?.ForceHide();

        if (UIManager.Instance != null && UIManager.Instance.Inventory != null)
        {
            if (UIManager.Instance.Inventory.IsAnimating)
            {
                return;
            }
        }

        if (IsAnyGameplayUIOpen())
        {
            return;
        }

        if ((pauseController != null && pauseController.IsPaused))
        {
            if (UIManager.Instance != null && UIManager.Instance.IsInventoryOpen())
            {
                ResumeGame();
            }
            return;
        }

        if (UIManager.Instance != null && UIManager.Instance.IsInventoryOpen()) 
        { 
            UIManager.Instance.Inventory.ToggleInventory(); 
            if (uiManager != null && uiManager.HUD != null) uiManager.HUD.SetActive(true); 
            return; 
        }

        if (UIManager.Instance?.Flow != null && UIManager.Instance.Flow.HasOpenModal) return; 
        
        OpenInventoryOptions(); 
    }

    private bool IsAnyGameplayUIOpen()
    {
        if (GameFlowController.Instance == null) return false;

        GamePhase currentPhase = GameFlowController.Instance.CurrentPhase;
        return currentPhase == GamePhase.OnRitual;
    }

    private void OpenInventoryOptions()
    {
        bool canOpen = GameFlowController.Instance != null &&
                       GameFlowController.Instance.CurrentPhase != GamePhase.GameOver &&
                       GameFlowController.Instance.CurrentPhase != GamePhase.OnRitual;

        if (!canOpen) return;

        UIManager.Instance?.OpenInventoryWithPage(InventoryPage.Options); 
        
        if (UIManager.Instance != null && UIManager.Instance.IsInventoryOpen())
        {
            if (pauseController != null) pauseController.Pause();

            if (TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive())
            {
                TutorialManager.Instance.PauseTutorial();
            }

            if (uiManager != null && uiManager.HUD != null) 
                uiManager.HUD.SetActive(false);
        }
    }

    private void ResumeGame()
    {
        if (UIManager.Instance != null && UIManager.Instance.IsInventoryOpen())
        {
            UIManager.Instance.CloseInventory();
        }

        if (pauseController != null) pauseController.Resume();

        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.ResumeAll();
        }

        if (uiManager != null && uiManager.HUD != null)
            uiManager.HUD.SetActive(true);

        if (TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive())
        {
            TutorialManager.Instance.ResumeTutorial();
        }
    }

    // UI-only methods that handle button interactions
    public void Continue()
    {
        ResumeGame();
    }

    // Methods to control just the main pause menu visibility
    public void ShowPauseMenu()
    {
        if (_pauseMenuPanel != null)
        {
            if (TutorialManager.Instance != null)
            {
                _pauseMenuPanel.SetSkipButtonActive(TutorialManager.Instance.IsTutorialActive());
            }
            else
            {
                _pauseMenuPanel.SetSkipButtonActive(false);
            }

            _pauseMenuPanel.Show();
        }
        if (_optionsMenuPanel != null)
            _optionsMenuPanel.Hide();
    }

    public void HidePauseMenu()
    {
        if (_pauseMenuPanel != null)
            _pauseMenuPanel.Hide();
    }

    // Options menu methods
    public void ShowOptions()
    {
        if (_pauseMenuPanel != null)
        {
            _pauseMenuPanel.Hide();
        }

        if (_optionsMenuPanel != null)
        {
            _optionsMenuPanel.Show();
        }

        SoundManager.Instance.PlayOneShot("ButtonClick");
    }

    public void HideOptions()
    {
        if (_optionsMenuPanel != null)
            _optionsMenuPanel.Hide();
        ShowPauseMenu();
        SoundManager.Instance.PlayOneShot("ButtonClick");
    }

    public void ShowInstructions()
    {
        if (UIManager.Instance?.Inventory != null)
        {
            UIManager.Instance.Inventory.ChangePage(InventoryPage.Controls);
        }
        else
        {
            SoundManager.Instance?.PlayOneShot("ButtonClick");
        }
    }

    public void GoToMainMenu()
    {
        if (pauseController != null) pauseController.Resume();

        if (_pauseMenuPanel != null)
            _pauseMenuPanel.Hide();

        SoundManager.Instance.PlayOneShot("ButtonClick");
        SceneLoaderManager.Instance.LoadSceneByNameWithTransition("Menu");
    }

    private void HandleSkipTutorialClicked()
    {
        if (TutorialManager.Instance != null)
        {
            TutorialManager.Instance.SkipTutorial();
        }

        Continue();
    }
}



