using System;
using System.Collections.Generic;
using MagicGarden.Loadout;

/// <summary>
/// Run-owned equipment coordination at the Assembly-CSharp boundary. No gameplay
/// admission, inventory selection/mutation, UI, singleton or cooldown ownership.
/// </summary>
public sealed class DailyLoadoutSession : IDisposable
{
    private readonly PlayerContentUnlockSystem unlocks;
    private readonly PlayerExperienceSystem experience;
    private readonly DayCycleController days;
    private readonly GameFlowController flow;
    private readonly DailyLoadoutState<SeedsEnum, SpellDataSO> state = new DailyLoadoutState<SeedsEnum, SpellDataSO>();
    private readonly EquipmentInventoryResolver resolver;
    private bool disposed;

    public bool IsInitialized => state.IsInitialized;
    public bool IsDay => state.IsDay;
    public bool HasConfirmedToday => state.HasConfirmedToday;
    public bool CanConfigure => !disposed && unlocks != null && unlocks.HasInitializedUnlockState &&
        flow != null && flow.CurrentPhase == GamePhase.Day && state.IsInitialized && state.IsDay;

    public DailyLoadoutSession(PlayerContentUnlockSystem unlocks, PlayerExperienceSystem experience,
        SeedInventory seeds, SpellInventory spells, DayCycleController days, GameFlowController flow)
    {
        this.unlocks = unlocks != null ? unlocks : throw new ArgumentNullException(nameof(unlocks));
        this.experience = experience;
        this.days = days;
        this.flow = flow;
        resolver = new EquipmentInventoryResolver(unlocks, seeds, spells, state);
        unlocks.OnUnlockStateRebuilt += HandleUnlockStateRebuilt;
        if (days != null) days.OnNewDay.AddListener(HandleNewDay);
        if (flow != null) flow.OnPhaseChanged += HandlePhaseChanged;
        TryInitialize(); // Also supports composition after an already-completed rebuild.
    }

    public EquipmentAssignment<SeedsEnum> GetCommittedPlant(int index) => state.GetCommittedPlant(index);
    public EquipmentAssignment<SpellDataSO> GetCommittedSpell(int index) => state.GetCommittedSpell(index);
    public EquipmentAssignment<SeedsEnum> GetDraftPlant(int index) => state.GetDraftPlant(index);
    public EquipmentAssignment<SpellDataSO> GetDraftSpell(int index) => state.GetDraftSpell(index);
    public PlantEquipmentResolution ResolvePlant(SeedsEnum identity) => resolver.ResolvePlant(identity);
    public SpellEquipmentResolution ResolveSpell(SpellDataSO identity) => resolver.ResolveSpell(identity);

    public bool TrySetDraftPlant(int index, SeedsEnum identity)
    {
        if (!CanConfigure) return false;
        if (identity != SeedsEnum.None && !unlocks.IsPlantUnlocked(identity)) return false;
        return state.TrySetDraftPlant(index, identity == SeedsEnum.None
            ? default : EquipmentAssignment<SeedsEnum>.For(identity));
    }

    public bool TrySetDraftSpell(int index, SpellDataSO identity)
    {
        if (!CanConfigure) return false;
        if (!ReferenceEquals(identity, null) && (identity == null || !unlocks.IsSpellUnlocked(identity))) return false;
        return state.TrySetDraftSpell(index, ReferenceEquals(identity, null)
            ? default : EquipmentAssignment<SpellDataSO>.For(identity));
    }

    public bool TryConfirm()
    {
        if (!CanConfigure) return false;
        // Eligibility can change after editing (e.g. a progression rebuild). Reject
        // without touching committed state or the daily allowance. Stock is not eligibility.
        for (int i = 0; i < DailyLoadoutState<SeedsEnum, SpellDataSO>.SlotsPerCategory; i++)
        {
            var plant = state.GetDraftPlant(i);
            var spell = state.GetDraftSpell(i);
            if (plant.HasValue && !unlocks.IsPlantUnlocked(plant.Identity)) return false;
            if (spell.HasValue && (spell.Identity == null || !unlocks.IsSpellUnlocked(spell.Identity))) return false;
        }
        return state.TryConfirm();
    }

    public void DiscardDraft()
    {
        if (!disposed) state.DiscardDraft();
    }

    /// <summary>Called only at full-run reset completion, after progression rebuild.</summary>
    public void ResetForNewRun()
    {
        if (disposed) return;
        state.ResetRun();
        TryInitialize();
    }

    private void HandleUnlockStateRebuilt() => TryInitialize();

    private void TryInitialize()
    {
        if (disposed || state.IsInitialized || unlocks == null || !unlocks.HasInitializedUnlockState ||
            experience == null || experience.ProgressionData == null) return;

        var plants = new List<SeedsEnum>();
        var spells = new List<SpellDataSO>();
        var progression = experience.ProgressionData;
        // Ordered catalog traversal, membership-only authority. Never enumerate
        // unlock HashSets to choose equipment, or invoke unlock/grant APIs here.
        int lastLevel = Math.Min(experience.CurrentLevel, progression.LevelRequirements?.Count ?? 0);
        for (int level = 1; level <= lastLevel; level++)
        {
            var content = progression.GetLevelData(level);
            if (content == null) continue;
            foreach (var plant in content.PlantUnlocks)
                if (plant != null && plant.seedType != SeedsEnum.None && unlocks.IsPlantUnlocked(plant.seedType))
                    plants.Add(plant.seedType);
            foreach (var spell in content.SpellUnlocks)
                if (spell != null && unlocks.IsSpellUnlocked(spell)) spells.Add(spell);
        }
        if (state.TryBootstrap(plants, spells) && flow != null && flow.WorldPhase == GamePhase.Day)
            HandleNewDay(0); // Reconcile a real Day established before owner.Start; argument is not a key.
    }

    private void HandleNewDay(int displayedDay)
    {
        if (!disposed && state.IsInitialized && days != null && flow != null && flow.WorldPhase == GamePhase.Day)
            state.TryBeginDay(days.TotalNights);
    }

    private void HandlePhaseChanged(GamePhase phase)
    {
        if (!disposed && phase == GamePhase.Night) state.BeginNight();
        // A Day phase alone is not a new day: e.g. GameOverController sets Day
        // before ResetGame. OnNewDay (or initial bootstrap) owns day opening.
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        if (unlocks != null) unlocks.OnUnlockStateRebuilt -= HandleUnlockStateRebuilt;
        if (days != null) days.OnNewDay.RemoveListener(HandleNewDay);
        if (flow != null) flow.OnPhaseChanged -= HandlePhaseChanged;
    }
}
