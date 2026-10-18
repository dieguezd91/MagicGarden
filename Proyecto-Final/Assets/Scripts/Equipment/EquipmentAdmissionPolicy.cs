using MagicGarden.Loadout;

public enum EquipmentAdmissionResult
{
    Allowed,
    SessionUnavailable,
    WrongPhase,
    EmptyPosition,
    Locked,
    Unequipped,
    Unavailable
}

/// <summary>
/// Read-only equipment eligibility, not action readiness. In particular a spell
/// on cooldown is still equipped; its original casting controller checks readiness.
/// </summary>
public sealed class EquipmentAdmissionPolicy
{
    private readonly DailyLoadoutSession session;
    private readonly GameFlowController flow;
    private const int Count = DailyLoadoutState<SeedsEnum, SpellDataSO>.SlotsPerCategory;

    public EquipmentAdmissionPolicy(DailyLoadoutSession session, GameFlowController flow)
    {
        this.session = session;
        this.flow = flow;
    }

    public EquipmentAdmissionResult CheckPlant(SeedsEnum identity, out PlantEquipmentResolution resolution)
    {
        resolution = default;
        var phase = CheckPhase(GamePhase.Day);
        if (phase != EquipmentAdmissionResult.Allowed) return phase;
        resolution = session.ResolvePlant(identity);
        if (!resolution.IsUnlocked) return EquipmentAdmissionResult.Locked;
        if (!resolution.IsEquipped) return EquipmentAdmissionResult.Unequipped;
        return resolution.HasUsableInventoryEntry ? EquipmentAdmissionResult.Allowed : EquipmentAdmissionResult.Unavailable;
    }

    public EquipmentAdmissionResult CheckSpell(SpellDataSO identity, out SpellEquipmentResolution resolution)
    {
        resolution = default;
        var phase = CheckPhase(GamePhase.Night);
        if (phase != EquipmentAdmissionResult.Allowed) return phase;
        resolution = session.ResolveSpell(identity);
        if (!resolution.IsUnlocked) return EquipmentAdmissionResult.Locked;
        if (!resolution.IsEquipped) return EquipmentAdmissionResult.Unequipped;
        return resolution.HasUsableInventoryEntry ? EquipmentAdmissionResult.Allowed : EquipmentAdmissionResult.Unavailable;
    }

    public EquipmentAdmissionResult CheckPlantPosition(int position, out PlantEquipmentResolution resolution)
    {
        resolution = default;
        var phase = CheckPhase(GamePhase.Day);
        if (phase != EquipmentAdmissionResult.Allowed) return phase;
        if (position < 0 || position >= Count) return EquipmentAdmissionResult.EmptyPosition;
        var assignment = session.GetCommittedPlant(position);
        return assignment.HasValue ? CheckPlant(assignment.Identity, out resolution) : EquipmentAdmissionResult.EmptyPosition;
    }

    public EquipmentAdmissionResult CheckSpellPosition(int position, out SpellEquipmentResolution resolution)
    {
        resolution = default;
        var phase = CheckPhase(GamePhase.Night);
        if (phase != EquipmentAdmissionResult.Allowed) return phase;
        if (position < 0 || position >= Count) return EquipmentAdmissionResult.EmptyPosition;
        var assignment = session.GetCommittedSpell(position);
        return assignment.HasValue ? CheckSpell(assignment.Identity, out resolution) : EquipmentAdmissionResult.EmptyPosition;
    }

    // Inventory selection can be changed by any existing consumer. Resolve the
    // selected storage index against equipment rather than granting it permission.
    public EquipmentAdmissionResult CheckSpellInventoryIndex(int index, out SpellEquipmentResolution resolution)
    {
        resolution = default;
        var phase = CheckPhase(GamePhase.Night);
        if (phase != EquipmentAdmissionResult.Allowed) return phase;
        for (int i = 0; i < Count; i++)
        {
            var assignment = session.GetCommittedSpell(i);
            if (assignment.HasValue && assignment.Identity != null && assignment.Identity.SlotIndex == index)
                return CheckSpell(assignment.Identity, out resolution);
        }
        return EquipmentAdmissionResult.Unequipped;
    }

    private EquipmentAdmissionResult CheckPhase(GamePhase categoryPhase)
    {
        if (session == null || !session.IsReady) return EquipmentAdmissionResult.SessionUnavailable;
        return flow != null && flow.CurrentPhase == categoryPhase
            ? EquipmentAdmissionResult.Allowed : EquipmentAdmissionResult.WrongPhase;
    }
}
