using System;
using MagicGarden;
using UnityEngine;

/// <summary>Single equipment authority. Inventories still own stock and cooldowns.</summary>
public sealed class DailyLoadoutController : MonoBehaviour
{
    [SerializeField] private SpellInventory spells;
    [SerializeField] private SeedInventory seeds;
    [SerializeField] private PlayerContentUnlockSystem unlocks;
    [SerializeField] private DayCycleController days;
    [SerializeField] private GameFlowController flow;
    [SerializeField] private PlayerController player;
    [SerializeField] private PauseController pause;
    private readonly DailyLoadoutState state = new DailyLoadoutState();
    private string selectedSpell, selectedPlant;

    public event Action Changed;
    public bool IsConfirmed => state.IsConfirmed;
    public int Day => state.Day;
    public SpellInventory Spells => spells;
    public SeedInventory Seeds => seeds;
    public PlayerContentUnlockSystem Unlocks => unlocks;
    public string SelectedSpellId => selectedSpell;
    public string SelectedPlantId => selectedPlant;

    private void Awake()
    {
        spells = spells != null ? spells : FindObjectOfType<SpellInventory>();
        seeds = seeds != null ? seeds : FindObjectOfType<SeedInventory>();
        unlocks = unlocks != null ? unlocks : FindObjectOfType<PlayerContentUnlockSystem>();
        days = days != null ? days : GetComponent<DayCycleController>();
        flow = flow != null ? flow : GetComponent<GameFlowController>();
        player = player != null ? player : FindObjectOfType<PlayerController>();
        pause = pause != null ? pause : GetComponent<PauseController>();
    }

    private void Start()
    {
        if (days != null)
        {
            days.GameDayStarted += BeginDay;
            days.RunReset += ResetRun;
            BeginDay(days.GameDaySerial);
        }
        if (flow != null) flow.OnPhaseChanged += PhaseChanged;
        if (spells != null) spells.OnSpellInventoryChanged += Notify;
        if (seeds != null) seeds.onInventoryChanged += Notify;
        if (unlocks != null) unlocks.OnUnlockStateRebuilt += Notify;
    }

    private void OnDestroy()
    {
        if (days != null) { days.GameDayStarted -= BeginDay; days.RunReset -= ResetRun; }
        if (flow != null) flow.OnPhaseChanged -= PhaseChanged;
        if (spells != null) spells.OnSpellInventoryChanged -= Notify;
        if (seeds != null) seeds.onInventoryChanged -= Notify;
        if (unlocks != null) unlocks.OnUnlockStateRebuilt -= Notify;
    }

    private void PhaseChanged(GamePhase phase) => Notify();
    private void Notify() => Changed?.Invoke();
    private void BeginDay(int serial)
    {
        if (!state.BeginDay(serial)) return;
        selectedSpell = selectedPlant = null;
        Notify();
    }
    private void ResetRun()
    {
        state.ResetRun();
        selectedSpell = selectedPlant = null;
        state.BeginDay(days.GameDaySerial);
        Notify();
    }

    public string GetDraft(bool spell, int slot) => state.GetDraft(spell, slot);
    public string GetEquipped(bool spell, int slot) => state.GetEquipped(spell, slot);

    // Editing is allowed in either world phase, never in menus/ritual/upgrade/game over.
    public bool CanConfigure => flow != null && !UpgradeRuntime.GameplayBlocked &&
        (flow.CurrentPhase == GamePhase.Day || flow.CurrentPhase == GamePhase.Night) &&
        (pause == null || !pause.IsPaused);

    public bool TryEdit(bool spell, int slot, string id)
    {
        if (!CanConfigure || (!string.IsNullOrEmpty(id) && !(spell ? IsSpellUnlocked(id) : IsPlantUnlocked(id)))) return false;
        if (!state.TryEdit(spell, slot, id)) return false;
        Notify();
        return true;
    }

    public bool TryConfirm()
    {
        if (!CanConfigure || days == null || state.Day != days.GameDaySerial ||
            !state.TryConfirm(IsSpellUnlocked, IsPlantUnlocked)) return false;
        selectedSpell = selectedPlant = null;
        Notify();
        return true;
    }

    public static string PlantId(SeedsEnum seed) => seed == SeedsEnum.None ? null : "plant:" + seed;
    public static string PlantId(PlantSlot slot) => slot != null ? PlantId(slot.seedType) : null;
    public bool IsSpellUnlocked(string id)
    {
        if (id == null || spells == null || unlocks == null) return false;
        foreach (var data in unlocks.UnlockedSpells)
            if (data != null && data.UpgradeTargetId == id)
            {
                int index = FindSpell(id);
                var slot = spells.GetSpellSlot(index);
                return slot != null && slot.isUnlocked && slot.spellPrefab != null && slot.spellType != SpellType.None;
            }
        return false;
    }
    public bool IsPlantUnlocked(string id)
    {
        if (id == null || unlocks == null) return false;
        foreach (var seed in unlocks.UnlockedPlants)
            if (PlantId(seed) == id && unlocks.TryGetUnlockedPlant(seed, out var data)) return data.plantPrefab != null;
        return false;
    }
    public PlantDataSO GetPlantData(string id)
    {
        if (id != null && unlocks != null)
            foreach (var seed in unlocks.UnlockedPlants)
                if (PlantId(seed) == id && unlocks.TryGetUnlockedPlant(seed, out var data)) return data;
        return null;
    }
    public int FindSpell(string id)
    {
        if (id != null && spells != null)
            for (int i = 0; i < spells.spellSlots.Length; i++)
                if (spells.spellSlots[i] != null && spells.spellSlots[i].UpgradeId(i) == id) return i;
        return -1;
    }
    public int FindSeed(string id)
    {
        if (id != null && seeds != null)
            for (int i = 0; i < seeds.PlantSlotsCount; i++)
                if (PlantId(seeds.GetPlantSlot(i)) == id && seeds.GetSeedCountInSlot(i) > 0) return i;
        return -1;
    }
    public int SelectedSpellIndex => FindSpell(selectedSpell);
    public int SelectedSeedIndex => FindSeed(selectedPlant);

    public bool IsEquipped(bool spell, string id)
    {
        if (id == null || !state.IsConfirmed) return false;
        for (int i = 0; i < DailyLoadoutState.Capacity; i++) if (state.GetEquipped(spell, i) == id) return true;
        return false;
    }
    public bool CanUse(bool spell, string id)
    {
        if (!isActiveAndEnabled || !IsEquipped(spell, id) || flow == null || UpgradeRuntime.GameplayBlocked ||
            flow.CurrentPhase != (spell ? GamePhase.Night : GamePhase.Day) ||
            (pause != null && pause.IsPaused) || (player != null && !player.CanAct()) ||
            (UIManager.Instance != null && UIManager.Instance.Flow != null && UIManager.Instance.Flow.HasOpenModal)) return false;
        return spell ? IsSpellUnlocked(id) : IsPlantUnlocked(id) && FindSeed(id) >= 0;
    }
    public bool TrySelect(bool spell, string id)
    {
        if (!CanUse(spell, id)) return false;
        if (spell) selectedSpell = id; else selectedPlant = id;
        Notify();
        return true;
    }
    public bool CanPlant(GameObject prefab)
    {
        int index = SelectedSeedIndex;
        var slot = seeds != null ? seeds.GetPlantSlot(index) : null;
        return slot != null && slot.plantPrefab == prefab && CanUse(false, selectedPlant);
    }
}
