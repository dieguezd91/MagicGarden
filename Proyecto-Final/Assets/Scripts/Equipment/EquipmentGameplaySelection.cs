using MagicGarden.Loadout;

/// <summary>
/// Translates gameplay equipment positions into existing inventory selections.
/// No selected-item state is stored here. Player-state blocking belongs to input callers.
/// </summary>
public sealed class EquipmentGameplaySelection
{
    private readonly DailyLoadoutSession session;
    private readonly SeedInventory seeds;
    private readonly SpellInventory spells;
    private const int Count = DailyLoadoutState<SeedsEnum, SpellDataSO>.SlotsPerCategory;

    public EquipmentGameplaySelection(DailyLoadoutSession session, SeedInventory seeds, SpellInventory spells)
    {
        this.session = session;
        this.seeds = seeds;
        this.spells = spells;
    }

    // Startup can precede owner.Start; a disposed/replaced owner must not pin HUD bindings.
    public static PlayerContentUnlockSystem ResolveOwner(PlayerContentUnlockSystem current)
    {
        if (current != null && current.Loadout != null && current.Loadout.IsReady) return current;
        var active = UnityEngine.Object.FindObjectsOfType<PlayerContentUnlockSystem>();
        foreach (var owner in active)
            if (owner.Loadout != null && owner.Loadout.IsReady) return owner;
        foreach (var owner in active)
            if (owner.Loadout == null) return owner; // Wait for its Start, do not manufacture a session.
        if (current != null) return current;
        return UnityEngine.Object.FindObjectOfType<PlayerContentUnlockSystem>(true);
    }

    public bool TrySelectPosition(int position)
    {
        if (UpgradeRuntime.GameplayBlocked || session == null || !session.IsReady || position < 0 || position >= Count)
            return false;
        var plantResult = session.Admission.CheckPlantPosition(position, out var plant);
        if (plantResult != EquipmentAdmissionResult.WrongPhase && plantResult != EquipmentAdmissionResult.SessionUnavailable)
        {
            if (seeds == null) return false;
            if (plantResult == EquipmentAdmissionResult.Allowed) seeds.SelectSlot(plant.InventoryIndex);
            else if (plantResult != EquipmentAdmissionResult.EmptyPosition) seeds.ClearSelection();
            return plantResult == EquipmentAdmissionResult.Allowed;
        }
        var spellResult = session.Admission.CheckSpellPosition(position, out var spell);
        if (spellResult == EquipmentAdmissionResult.WrongPhase || spellResult == EquipmentAdmissionResult.SessionUnavailable || spells == null)
            return false;
        if (spellResult == EquipmentAdmissionResult.Allowed) spells.SelectSlot(spell.InventoryIndex);
        else if (spellResult != EquipmentAdmissionResult.EmptyPosition) spells.ClearSelection();
        return spellResult == EquipmentAdmissionResult.Allowed;
    }

    public int GetSelectedPosition(bool plants)
    {
        if (session == null || !session.IsReady) return -1;
        for (int i = 0; i < Count; i++)
        {
            if (plants)
            {
                var assignment = session.GetCommittedPlant(i);
                var selected = seeds != null ? seeds.GetSelectedPlantSlot() : null;
                if (assignment.HasValue && selected != null && selected.seedType == assignment.Identity) return i;
            }
            else
            {
                var assignment = session.GetCommittedSpell(i);
                if (assignment.HasValue && spells != null &&
                    session.ResolveSpell(assignment.Identity).InventoryIndex == spells.GetSelectedSlotIndex() &&
                    spells.GetSelectedSlotIndex() >= 0) return i;
            }
        }
        return -1;
    }

    public bool TryCycleSpell(int direction)
    {
        if (UpgradeRuntime.GameplayBlocked || session == null || spells == null || direction == 0) return false;
        int step = direction > 0 ? 1 : -1;
        int current = step > 0 ? -1 : 0;
        for (int i = 0; i < Count; i++)
        {
            var assignment = session.GetCommittedSpell(i);
            if (assignment.HasValue && assignment.Identity != null && assignment.Identity.SlotIndex == spells.GetSelectedSlotIndex())
            {
                current = i;
                break;
            }
        }
        for (int offset = 1; offset <= Count; offset++)
        {
            int position = (current + offset * step + Count) % Count;
            if (session.Admission.CheckSpellPosition(position, out var spell) != EquipmentAdmissionResult.Allowed) continue;
            spells.SelectSlot(spell.InventoryIndex);
            return true;
        }
        return false;
    }
}
