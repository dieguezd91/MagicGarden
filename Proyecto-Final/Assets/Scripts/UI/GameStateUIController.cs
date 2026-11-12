using UnityEngine;

public class GameStateUIController : UIControllerBase
{


    [Header("UI Elements")]
    [SerializeField] private GameObject HUD;
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private GameObject dayControlPanel;
    [SerializeField] private GameObject abilityPanel;

    [Header("Backdrop")]
    [Tooltip("Shared dimmed backdrop shown behind the inventory (same UIBackdrop as the level-up reward).")]
    [SerializeField] private UIBackdrop inventoryBackdrop;

    private bool wasInventoryOpen = false;

    private WorldTransitionAnimator worldTransition;


    protected override void CacheReferences()
    {
        if (worldTransition == null)
        {
            worldTransition = FindObjectOfType<WorldTransitionAnimator>();
        }
    }

    protected override void SetupEventListeners()
    {
        UIEvents.OnInventoryClosed += HandleInventoryClosedEvent;
        UIEvents.OnInventoryOpened += HandleInventoryOpenedEvent;

        if (worldTransition != null)
        {
            worldTransition.OnStateChanged += OnWorldStateChanged;
        }

        if (GameFlowController.Instance != null)
            GameFlowController.Instance.OnPhaseChanged += OnPhaseChanged;
    }

    protected override void CleanupEventListeners()
    {
        UIEvents.OnInventoryClosed -= HandleInventoryClosedEvent;
        UIEvents.OnInventoryOpened -= HandleInventoryOpenedEvent;

        if (worldTransition != null)
        {
            worldTransition.OnStateChanged -= OnWorldStateChanged;
        }

        if (GameFlowController.Instance != null)
            GameFlowController.Instance.OnPhaseChanged -= OnPhaseChanged;
    }

    private void OnWorldStateChanged(WorldState newWorldState)
    {
        UpdateAbilityUIVisibility();
    }

    protected override void ConfigureInitialState()
    {
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (pausePanel != null) pausePanel.SetActive(true);

        if (inventoryBackdrop != null) inventoryBackdrop.Hide();

        if (GameFlowController.Instance != null)
        {
            OnPhaseChanged(GameFlowController.Instance.CurrentPhase);
        }
        else
        {
            UpdateUIElementsVisibility();
            UpdateAbilityUIVisibility();
        }
    }

    public override void HandleUpdate()
    {
        HandleGameOverState();
        CheckInventoryOpenState();
    }

    private void CheckInventoryOpenState()
    {
        if (UIManager.Instance == null) return;

        bool isOpen = UIManager.Instance.IsInventoryOpen();

        if (isOpen && !wasInventoryOpen)
        {
            if (inventoryBackdrop != null) inventoryBackdrop.Show();
        }
        else if (!isOpen && wasInventoryOpen)
        {
            if (inventoryBackdrop != null) inventoryBackdrop.Hide();
        }

        wasInventoryOpen = isOpen;
    }

    public void OnPhaseChanged(GamePhase newPhase)
    {
        UpdateUIElementsVisibility();
        UpdateAbilityUIVisibility();
    }

    private void UpdateAbilityUIVisibility()
    {
        if (abilityPanel == null || GameFlowController.Instance == null)
        {
            return;
        }

        GamePhase phase = GameFlowController.Instance.CurrentPhase;

        bool isInInterior = worldTransition != null && worldTransition.IsInInterior;

        bool showAbilities = phase != GamePhase.Night &&
                             phase != GamePhase.GameOver &&
                             !isInInterior;

        abilityPanel.SetActive(showAbilities);
    }


    private void HandleGameOverState()
    {
        if (gameOverPanel != null && gameOverPanel.activeInHierarchy && HUD != null)
            HUD.SetActive(false);
    }

    private void UpdateUIElementsVisibility()
    {
        if (GameFlowController.Instance == null) return;

        GamePhase currentPhase = GameFlowController.Instance.CurrentPhase;

        bool showHUD = IsGameplayPhase(currentPhase);
        if (HUD != null)
            HUD.SetActive(showHUD);

        bool showDayControls = IsActiveGameplayPhase(currentPhase);
        if (dayControlPanel != null)
            dayControlPanel.SetActive(showDayControls);
    }

    private bool IsGameplayPhase(GamePhase phase)
    {
        return phase == GamePhase.Day ||
               phase == GamePhase.Night;
    }

    private bool IsActiveGameplayPhase(GamePhase phase)
    {
        return phase == GamePhase.Day;
    }

    public void SetUIElementsVisibility(bool visible)
    {
        if (HUD != null) HUD.SetActive(visible);

        if (dayControlPanel != null) dayControlPanel.SetActive(visible);

        if (!visible && UIManager.Instance.IsInventoryOpen())
        {
            UIManager.Instance.CloseInventory();
        }
    }



    private void HandleInventoryClosedEvent()
    {
        if (inventoryBackdrop != null) inventoryBackdrop.Hide();

        // When inventory closes, resume tutorial if it was paused by the menu
        if (TutorialManager.Instance != null)
        {
            TutorialManager.Instance.ResumeTutorial();
        }
    }

    private void HandleInventoryOpenedEvent()
    {
        // Called when inventory opens (including alternate keys like Tab).
        if (inventoryBackdrop != null) inventoryBackdrop.Show();

        // Pause the tutorial when the inventory opens so it hides and can be resumed on close
        if (TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive())
        {
            TutorialManager.Instance.PauseTutorial();
        }
    }
}




