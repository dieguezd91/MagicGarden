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

    public bool TrySelectPosition(int position)
    {
        if (UpgradeRuntime.GameplayBlocked || session == null) return false;
        if (seeds != null && session.Admission.CheckPlantPosition(position, out var plant) == EquipmentAdmissionResult.Allowed)
        {
            seeds.SelectSlot(plant.InventoryIndex);
            return true;
        }
        if (spells != null && session.Admission.CheckSpellPosition(position, out var spell) == EquipmentAdmissionResult.Allowed)
        {
            spells.SelectSlot(spell.InventoryIndex);
            return true;
        }
        return false;
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
