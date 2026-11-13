using UnityEngine;

public class InventoryUIController : UIControllerBase
{
    private void Awake()
    {
        if (pauseController == null) pauseController = FindObjectOfType<PauseController>();
    }
    [SerializeField] private PauseController pauseController;
    [Header("Inventory Settings")]
    [SerializeField] private GameObject inventoryPanel;
    [SerializeField] private KeyCode toggleInventoryKey = KeyCode.I;
    [SerializeField] private KeyCode alternateToggleKey = KeyCode.Tab;
    [SerializeField] private bool closeInventoryOnEscape = true;
    [SerializeField] private InventoryUI inventoryUI;

    [Header("Animation")]
    [SerializeField] private InventoryAnimationController animationController;
    [SerializeField] private bool waitForAnimationToComplete = true;

    private bool resumePauseWhenInventoryCloses;
    public bool IsInventoryOpen => UIManager.Instance?.Flow != null && UIManager.Instance.Flow.IsOpen(UIModal.Inventory);

    protected override void CacheReferences()
    {
        if (inventoryPanel == null)
            inventoryPanel = GameObject.FindGameObjectWithTag("InventoryPanel");

        if (inventoryUI == null && inventoryPanel != null)
            inventoryUI = inventoryPanel.GetComponent<InventoryUI>();

        if (animationController == null && inventoryPanel != null)
        {
            Transform animatorTransform = inventoryPanel.transform.Find("InventoryAnimator");
            if (animatorTransform != null)
            {
                animationController = animatorTransform.GetComponent<InventoryAnimationController>();
            }

            if (animationController == null)
            {
                animationController = inventoryPanel.GetComponentInChildren<InventoryAnimationController>();
            }
        }
    }

    protected override void ConfigureInitialState()
    {
        if (inventoryPanel != null)
        {
            inventoryPanel.SetActive(false);
            
        }
    }

    protected override void SetupEventListeners()
    {
        UIEvents.OnInventoryToggleRequested += ToggleInventory;

        if (animationController != null)
        {
            animationController.OnCloseAnimationComplete += OnInventoryCloseAnimationComplete;
            animationController.OnPageReadyToShow += OnPageReadyToShow;
        }
    }

    public override void HandleUpdate()
    {
        HandleInventoryInput();
    }

    private void HandleInventoryInput()
    {
        if (IsInventoryOpen)
        {
            if (animationController != null && animationController.IsAnimating)
                return;

            if (Input.GetKeyDown(toggleInventoryKey) ||
                Input.GetKeyDown(alternateToggleKey))
            {
                UIManager.Instance?.Tooltip?.ForceHide();
                CloseInventory();
                return;
            }

            if (closeInventoryOnEscape &&
                Input.GetKeyDown(KeyCode.Escape) &&
                !InputConsumptionManager.IsEscapeConsumed)
            {
                InputConsumptionManager.ConsumeEscape();
                UIManager.Instance?.Tooltip?.ForceHide();
                CloseInventory();
            }

            return;
        }

        if (pauseController != null && pauseController.IsPaused)
            return;

        if (Input.GetKeyDown(toggleInventoryKey) ||
            Input.GetKeyDown(alternateToggleKey))
        {
            UIManager.Instance?.Tooltip?.ForceHide();
            OpenInventory();
        }
    }

    public void ToggleInventory()
    {
        if (IsInventoryOpen)
            CloseInventory();
        else
            OpenInventory();
    }

    public void OpenInventory()
    {
        if (inventoryPanel == null)
            return;

        if (!CanOpenInventory())
            return;

        if (animationController != null && animationController.IsAnimating)
            return;

        bool wasPaused = pauseController != null && pauseController.IsPaused;

        if (UIManager.Instance != null && UIManager.Instance.Flow != null)
        {
            if (!UIManager.Instance.Flow.Open(UIModal.Inventory))
                return;
        }

        resumePauseWhenInventoryCloses = !wasPaused;

        inventoryPanel.SetActive(true);

        TutorialEvents.InvokeInventoryOpened();
        UIEvents.TriggerInventoryOpened();
    }

    private void OnPageReadyToShow()
    {
        if (inventoryUI != null)
        {
            inventoryUI.UpdateAllSlots();
            inventoryUI.ForceRefresh();
        }
    }

    public void CloseInventory()
    {
        if (inventoryPanel == null || !inventoryPanel.activeSelf) return;

        if (animationController != null && animationController.IsAnimating)
        {
            return;
        }

        if (waitForAnimationToComplete && animationController != null && animationController.gameObject.activeInHierarchy)
        {
            animationController.CloseBook();
        }
        else
        {
            PerformCloseInventory();
        }
    }

    private void PerformCloseInventory()
    {
        if (inventoryPanel == null)
            return;

        if (inventoryUI != null)
            inventoryUI.ClearDescriptionPanel();

        inventoryPanel.SetActive(false);

        UIManager.Instance?.Flow?.Close(UIModal.Inventory);

        if (UIManager.Instance != null && UIManager.Instance.HUD != null)
            UIManager.Instance.HUD.SetActive(true);

        UIEvents.TriggerInventoryClosed();

        if (resumePauseWhenInventoryCloses)
        {
            resumePauseWhenInventoryCloses = false;

            if (pauseController != null && pauseController.IsPaused)
                pauseController.Resume();
        }
    }

    private bool CanOpenInventory(bool isOptions = false)
    {
        if (GameFlowController.Instance == null)
            return false;

        var state = GameFlowController.Instance.CurrentPhase;
        
        if (state == GamePhase.GameOver || state == GamePhase.OnRitual)
            return false;

        if (isOptions)
            return true;

        return state != GamePhase.Night &&
               (pauseController == null || !pauseController.IsPaused);
    }


    private void OnInventoryCloseAnimationComplete()
    {
        PerformCloseInventory();
    }

    protected override void CleanupEventListeners()
    {
        UIEvents.OnInventoryToggleRequested -= ToggleInventory;

        if (animationController != null)
        {
            animationController.OnCloseAnimationComplete -= OnInventoryCloseAnimationComplete;
            animationController.OnPageReadyToShow -= OnPageReadyToShow;
        }
    }

    public void OpenInventoryWithPage(InventoryPage page)
    {
        if (inventoryPanel == null)
            return;

        if (!CanOpenInventory(page == InventoryPage.Options))
            return;

        if (animationController != null && animationController.IsAnimating)
            return;

        bool wasPaused = pauseController != null && pauseController.IsPaused;

        if (UIManager.Instance != null && UIManager.Instance.Flow != null)
        {
            if (!UIManager.Instance.Flow.Open(UIModal.Inventory))
                return;
        }

        resumePauseWhenInventoryCloses = !wasPaused;

        inventoryPanel.SetActive(true);

        if (animationController != null)
            animationController.OpenWithPage(page);

        UIEvents.TriggerInventoryOpened();
    }

    public void ChangePage(InventoryPage page)
    {
        if (!IsInventoryOpen || animationController == null)
            return;

        animationController.ChangePage(page);
    }

    public bool IsAnimating => animationController != null && animationController.IsAnimating;
}








