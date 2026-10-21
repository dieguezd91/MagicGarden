using UnityEngine;
using System.Collections;

public class UIManager : MonoBehaviour
{
    [Header("UI Module Components")]
    [SerializeField] private PauseMenuController pauseMenuController;
    [SerializeField] private HealthUIController healthUI;
    [SerializeField] private ManaUIController manaUI;
    [SerializeField] private InventoryUIController inventoryUIController;
    [SerializeField] private SeedSlotsUIController seedSlotsUI;
    [SerializeField] private GameStateUIController gameStateUI;
    [SerializeField] private FeedbackUIController feedbackUI;
    [SerializeField] private TooltipUIController tooltipUI;
    [SerializeField] private SpellSlotsUIController spellSlotsUI;
    [SerializeField] private GameObject hudAbilities;
    [SerializeField] private RitualUIController ritualUI;
    [SerializeField] private TeleportAbilityUI teleportAbilityUI;

    [Header("Sound")]
    [SerializeField] private InterfaceSoundBase interfaceSounds;

    [SerializeField] private UIFlowController uiFlowController;
    [SerializeField] private MagicGarden.LoadoutUI.DailyLoadoutPresenter dailyLoadout;

    [Header("UI References")]
    public GameObject gameOverPanel;

    [Header("Continue Panel Elements")]
    public GameObject continuePanel;
    public UnityEngine.UI.Image continuePanelBackground;
    public TMPro.TextMeshProUGUI continueText;
    public GameObject continueButtonsContainer;

    public GameObject HUD;
    public GameObject pausePanel;
    public InventoryUI inventoryUI;


    public InterfaceSoundBase InterfaceSounds => interfaceSounds;
    public static UIManager Instance { get; private set; }

    public PauseMenuController PauseMenu => pauseMenuController;
    public HealthUIController Health => healthUI;
    public ManaUIController Mana => manaUI;
    public InventoryUIController Inventory => inventoryUIController;
    public SeedSlotsUIController SeedSlots => seedSlotsUI;
    public SpellSlotsUIController SpellSlots => spellSlotsUI;
    public GameStateUIController GamePhase => gameStateUI;
    public FeedbackUIController Feedback => feedbackUI;
    public TooltipUIController Tooltip => tooltipUI;
    public RitualUIController Ritual => ritualUI;
    public TeleportAbilityUI TeleportAbility => teleportAbilityUI;
    public UIFlowController Flow => uiFlowController;
    public MagicGarden.LoadoutUI.DailyLoadoutPresenter DailyLoadout => dailyLoadout;

    public void CloseDailyLoadout() => dailyLoadout?.Close();
    private void OnDisable() => CloseDailyLoadout();
    private void OnDestroy()
    {
        CloseDailyLoadout();
        if (Instance == this) Instance = null;
    }

    private void Awake() { if (uiFlowController == null) uiFlowController = GetComponent<UIFlowController>(); if (uiFlowController == null) uiFlowController = gameObject.AddComponent<UIFlowController>();
        if (Instance == null)
        {
            Instance = this;
            InitializeModules();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        SetupModules();
    }

    private void Update()
    {
        pauseMenuController?.HandleUpdate();
        gameStateUI?.HandleUpdate();
        tooltipUI?.HandleUpdate();
        feedbackUI?.HandleUpdate();
        inventoryUIController?.HandleUpdate();
        seedSlotsUI?.HandleUpdate();
        spellSlotsUI?.HandleUpdate();
        teleportAbilityUI?.HandleUpdate();
    }


    private void InitializeModules()
    {
        if (pauseMenuController == null) pauseMenuController = GetComponentInChildren<PauseMenuController>();
        if (healthUI == null) healthUI = GetComponentInChildren<HealthUIController>();
        if (manaUI == null) manaUI = GetComponentInChildren<ManaUIController>();
        if (inventoryUIController == null) inventoryUIController = GetComponentInChildren<InventoryUIController>();
        if (inventoryUI == null) inventoryUI = GetComponentInChildren<InventoryUI>();
        if (seedSlotsUI == null) seedSlotsUI = GetComponentInChildren<SeedSlotsUIController>();
        if (gameStateUI == null) gameStateUI = GetComponentInChildren<GameStateUIController>();
        if (feedbackUI == null) feedbackUI = GetComponentInChildren<FeedbackUIController>();
        if (tooltipUI == null) tooltipUI = GetComponentInChildren<TooltipUIController>();
        if (spellSlotsUI == null) spellSlotsUI = GetComponentInChildren<SpellSlotsUIController>();
        if (ritualUI == null) ritualUI = GetComponentInChildren<RitualUIController>();
        if (teleportAbilityUI == null) teleportAbilityUI = GetComponentInChildren<TeleportAbilityUI>();

        pauseMenuController?.Initialize();
        healthUI?.Initialize();
        manaUI?.Initialize();
        inventoryUIController?.Initialize();
        seedSlotsUI?.Initialize();
        gameStateUI?.Initialize();
        feedbackUI?.Initialize();
        tooltipUI?.Initialize();
        spellSlotsUI?.Initialize();
        ritualUI?.Initialize();
        teleportAbilityUI?.Initialize();
    }

    public void OpenInventoryWithPage(InventoryPage page)
    {
        if (Inventory != null)
            Inventory.OpenInventoryWithPage(page);
    }

    private void SetupModules()
    {
        pauseMenuController?.Setup();
        healthUI?.Setup();
        manaUI?.Setup();
        inventoryUIController?.Setup();
        seedSlotsUI?.Setup();
        gameStateUI?.Setup();
        feedbackUI?.Setup();
        tooltipUI?.Setup();
        spellSlotsUI?.Setup();
        ritualUI?.Setup();
        teleportAbilityUI?.Setup();

    }

    public void OpenInventory()
    {
        if (Inventory != null)
            Inventory.OpenInventory();
    }

    public void CloseInventory()
    {
        if (Inventory != null)
            Inventory.CloseInventory();
    }

    public void ToggleInventory()
    {
        if (Inventory != null)
            Inventory.ToggleInventory();
    }

    public bool IsInventoryOpen()
    {
        return Inventory != null ? Inventory.IsInventoryOpen : false;
    }

    public void InitializeSpellSlotsUI()
    {
        if (SpellSlots != null)
            SpellSlots.RefreshAllSlots();
    }

    public void UpdateSpellSlotsUI()
    {
        if (SpellSlots != null)
            SpellSlots.RefreshAllSlots();
    }

    //public void ShowSpellTooltip(int slotIndex)
    //{

    //}

    public void ShowTooltipForSlot(int slotIndex)
    {
        if (Tooltip != null)
            Tooltip.ShowSlotTooltip(slotIndex);
    }

    public void HideTooltip()
    {
        if (Tooltip != null)
            Tooltip.HideTooltip();
    }

    public void AnimateRespawnRecovery(float duration)
        => StartCoroutine(AnimateRespawnCoroutine(duration));

    public void SetGrayscaleGhostEffect(bool enabled)
    {
        if (Feedback != null)
            Feedback.SetGrayscaleEffect(enabled);
    }

    private IEnumerator AnimateRespawnCoroutine(float duration)
    {
        if (Health != null)
            yield return Health.AnimateRespawnRecovery(duration);
        if (Mana != null)
            yield return Mana.AnimateRespawnRecovery(duration);
    }

    public void ShowInteriorHUD()
    {
        //if (HUD != null)

        if (Health != null)
            Health.gameObject.SetActive(true);

        if (Mana != null)
            Mana.gameObject.SetActive(true);

        if (SeedSlots != null)
            SeedSlots.gameObject.SetActive(false);

        if (SpellSlots != null)
            SpellSlots.gameObject.SetActive(false);

        if (hudAbilities != null)
            hudAbilities.SetActive(false);

    }

    public void ShowExteriorHUD()
    {
        if (HUD != null)
            HUD.SetActive(true);

        if (Health != null)
            Health.gameObject.SetActive(true);

        if (Mana != null)
            Mana.gameObject.SetActive(true);

        if (SeedSlots != null)
            SeedSlots.gameObject.SetActive(true);

        if (SpellSlots != null)
            SpellSlots.gameObject.SetActive(true);

        if (GamePhase != null)
        {
            GamePhase.OnPhaseChanged(GameFlowController.Instance.CurrentPhase);
        }
    }

    public IEnumerator AnimateContinuePanel()
    {
        continuePanel.SetActive(true);

        if (continueText != null) continueText.gameObject.SetActive(false);
        if (continueButtonsContainer != null) continueButtonsContainer.SetActive(false);

        if (continuePanelBackground != null)
        {
            Color c = continuePanelBackground.color;
            c.a = 0f;
            continuePanelBackground.color = c;

            float duration = 1.2f;
            float t = 0f;

            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float alpha = Mathf.Lerp(0f, 1f, t / duration);

                continuePanelBackground.color = new Color(c.r, c.g, c.b, alpha);

                yield return null;
            }

        }

        yield return new WaitForSecondsRealtime(1f);
        if (continueText != null)
            continueText.gameObject.SetActive(true);

        yield return new WaitForSecondsRealtime(0.4f);
        if (continueButtonsContainer != null)
            continueButtonsContainer.SetActive(true);
    }

    public void ShowRitualOverlay()
    {
        if (ritualUI != null)
            ritualUI.ShowRitualOverlay();
    }

    public void HideRitualOverlay()
    {
        if (ritualUI != null)
            ritualUI.HideRitualOverlay();
    }
}

