using UnityEngine;
using MagicGarden.LoadoutUI;

/// <summary>Existing seed HUD, projected from committed equipment, never storage order.</summary>
public class SeedSlotsUIController : UIControllerBase
{
    [Header("Seed Slots")]
    [SerializeField] private SeedSlot[] seedSlots = new SeedSlot[3];
    [SerializeField] private CanvasGroup seedSlotsCanvasGroup;
    private SeedInventory seedInventory;
    private PlayerAbilitySystem abilitySystem;
    private GameFlowController gameFlowController;
    private PlayerContentUnlockSystem unlocks;
    private DailyLoadoutSession session;
    private readonly DailyLoadoutCatalog catalog = new DailyLoadoutCatalog();
    private bool listening;

    protected override void CacheReferences() { }
    protected override void SetupEventListeners()
    {
        if (listening) return;
        listening = true;
        for (int i = 0; i < seedSlots.Length; i++)
        {
            if (seedSlots[i] == null) continue;
            seedSlots[i].gameObject.SetActive(i < 3);
            if (i >= 3) continue;
            seedSlots[i].InitializeEquipment(i);
            seedSlots[i].OnSlotClicked += OnSlotClicked;
        }
        BindOwners();
    }
    protected override void ConfigureInitialState() { RefreshAllSlots(); ApplyPhase(); }
    private void OnEnable() { if (isSetup) { SetupEventListeners(); RefreshAllSlots(); } }
    private void OnDisable() => CleanupEventListeners();
    public override void HandleUpdate() { if (isSetup && listening) BindOwners(); }

    private void BindOwners()
    {
        var owner = EquipmentGameplaySelection.ResolveOwner(unlocks);
        var nextSession = owner != null ? owner.Loadout : null;
        var inventory = SeedInventory.Instance;
        var flow = GameFlowController.Instance;
        if (ReferenceEquals(session, nextSession) && seedInventory == inventory && gameFlowController == flow && unlocks == owner)
            return;
        UnbindOwners();
        unlocks = owner; session = nextSession; seedInventory = inventory; gameFlowController = flow;
        abilitySystem = owner != null ? owner.GetComponent<PlayerAbilitySystem>() : null;
        if (session != null) session.CommittedChanged += RefreshAllSlots;
        if (seedInventory != null)
        {
            seedInventory.onInventoryChanged += UpdateSeedCounts;
            seedInventory.onSlotSelected += UpdateSelectedSlotUI;
        }
        if (gameFlowController != null) gameFlowController.OnPhaseChanged += OnPhaseChanged;
        RefreshAllSlots();
        ApplyPhase();
    }
    private void UnbindOwners()
    {
        if (session != null) session.CommittedChanged -= RefreshAllSlots;
        if (seedInventory != null)
        {
            seedInventory.onInventoryChanged -= UpdateSeedCounts;
            seedInventory.onSlotSelected -= UpdateSelectedSlotUI;
        }
        if (gameFlowController != null) gameFlowController.OnPhaseChanged -= OnPhaseChanged;
    }
    public void InitializeSlots() => RefreshAllSlots();
    public void RefreshAllSlots()
    {
        var experience = unlocks != null ? unlocks.GetComponent<PlayerExperienceSystem>() : null;
        catalog.Refresh(experience != null ? experience.ProgressionData : null, unlocks);
        UpdateSeedCounts();
    }
    public void UpdateSeedCounts()
    {
        for (int i = 0; i < seedSlots.Length && i < 3; i++) RefreshSlotDisplay(i);
        UpdateSelectedSlotUI(-1);
    }
    public void RefreshSlotDisplay(int position)
    {
        if (position < 0 || position >= 3 || position >= seedSlots.Length || seedSlots[position] == null) return;
        var assignment = session != null && session.IsReady ? session.GetCommittedPlant(position) : default;
        var data = assignment.HasValue ? catalog.FindPlant(assignment.Identity) : null;
        var resolution = assignment.HasValue ? session.ResolvePlant(assignment.Identity) : default;
        seedSlots[position].UpdateEquipmentDisplay(data, resolution.Slot != null ? resolution.Slot.seedCount : 0,
            resolution.InventoryIndex);
    }
    private void UpdateSelectedSlotUI(int ignored)
    {
        int selected = GetSelectedSlotIndex();
        for (int i = 0; i < seedSlots.Length && i < 3; i++)
            if (seedSlots[i] != null) seedSlots[i].SetSelected(i == selected);
    }
    public int GetSelectedSlotIndex() => new EquipmentGameplaySelection(session, seedInventory, null).GetSelectedPosition(true);
    public PlantSlot GetSlotData(int position)
    {
        if (session == null || !session.IsReady || position < 0 || position >= 3) return null;
        var assignment = session.GetCommittedPlant(position);
        return assignment.HasValue ? session.ResolvePlant(assignment.Identity).Slot : null;
    }
    public bool IsSlotEmpty(int position) => session == null || !session.IsReady || position < 0 || position >= 3 ||
        !session.GetCommittedPlant(position).HasValue;
    public void SelectSlot(int position) => OnSlotClicked(position);
    private void OnSlotClicked(int position)
    {
        if (gameFlowController == null || gameFlowController.CurrentPhase != GamePhase.Day) return;
        if (abilitySystem != null) abilitySystem.TrySelectEquipmentPosition(position);
        else new EquipmentGameplaySelection(session, seedInventory, null).TrySelectPosition(position);
    }
    private void OnPhaseChanged(GamePhase phase) { ApplyPhase(); UpdateSelectedSlotUI(-1); }
    private void ApplyPhase()
    {
        bool active = gameFlowController != null && gameFlowController.CurrentPhase == GamePhase.Day;
        if (seedSlotsCanvasGroup != null)
        {
            // Location owns root visibility; opaque per-slot tints own phase presentation.
            seedSlotsCanvasGroup.alpha = 1f;
            seedSlotsCanvasGroup.interactable = active;
            seedSlotsCanvasGroup.blocksRaycasts = active;
        }
        foreach (var slot in seedSlots)
            if (slot != null) slot.SetEquipmentPhaseActive(active);
    }
    protected override void CleanupEventListeners()
    {
        if (!listening) return;
        listening = false;
        UnbindOwners();
        session = null; seedInventory = null; gameFlowController = null; unlocks = null; abilitySystem = null;
        foreach (var slot in seedSlots) if (slot != null) slot.OnSlotClicked -= OnSlotClicked;
    }
}
