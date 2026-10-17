using System;
using System.Collections.Generic;
using MagicGarden.Loadout;

/// <summary>A query result, not a cached storage binding or gameplay selection.</summary>
public readonly struct PlantEquipmentResolution
{
    public SeedsEnum Identity { get; }
    public bool IsUnlocked { get; }
    public bool IsEquipped { get; }
    public int InventoryIndex { get; }
    public PlantSlot Slot { get; }
    public bool HasUsableInventoryEntry => Slot != null;

    internal PlantEquipmentResolution(SeedsEnum identity, bool unlocked, bool equipped, int index, PlantSlot slot)
    {
        Identity = identity; IsUnlocked = unlocked; IsEquipped = equipped;
        InventoryIndex = index; Slot = slot;
    }
}

public readonly struct SpellEquipmentResolution
{
    public SpellDataSO Identity { get; }
    public bool IsUnlocked { get; }
    public bool IsEquipped { get; }
    public int InventoryIndex { get; }
    public SpellSlot Slot { get; }
    // Valid inventory configuration, not cast readiness: mana, phase and cooldown
    // admission remain entirely with the existing gameplay controllers.
    public bool HasUsableInventoryEntry => Slot != null;

    internal SpellEquipmentResolution(SpellDataSO identity, bool unlocked, bool equipped, int index, SpellSlot slot)
    {
        Identity = identity; IsUnlocked = unlocked; IsEquipped = equipped;
        InventoryIndex = index; Slot = slot;
    }
}

/// <summary>
/// Read-only live lookup. Equipment holds identities; indices in results are
/// ephemeral and must be resolved again after any inventory change.
/// </summary>
public sealed class EquipmentInventoryResolver
{
    private readonly PlayerContentUnlockSystem unlocks;
    private readonly SeedInventory seeds;
    private readonly SpellInventory spells;
    private readonly DailyLoadoutState<SeedsEnum, SpellDataSO> equipment;

    public EquipmentInventoryResolver(PlayerContentUnlockSystem unlocks, SeedInventory seeds,
        SpellInventory spells, DailyLoadoutState<SeedsEnum, SpellDataSO> equipment)
    {
        this.unlocks = unlocks != null ? unlocks : throw new ArgumentNullException(nameof(unlocks));
        this.seeds = seeds;
        this.spells = spells;
        this.equipment = equipment ?? throw new ArgumentNullException(nameof(equipment));
    }

    public PlantEquipmentResolution ResolvePlant(SeedsEnum identity)
    {
        bool unlocked = unlocks != null && unlocks.IsPlantUnlocked(identity);
        bool equipped = false;
        for (int i = 0; i < DailyLoadoutState<SeedsEnum, SpellDataSO>.SlotsPerCategory; i++)
        {
            var item = equipment.GetCommittedPlant(i);
            if (identity != SeedsEnum.None && item.HasValue && item.Identity == identity) equipped = true;
        }
        if (identity != SeedsEnum.None && seeds != null)
        {
            for (int i = 0; i < seeds.PlantSlotsCount; i++)
            {
                var slot = seeds.GetPlantSlot(i);
                if (slot != null && slot.seedType == identity && slot.seedCount > 0 && slot.plantPrefab != null)
                    return new PlantEquipmentResolution(identity, unlocked, equipped, i, slot);
            }
        }
        return new PlantEquipmentResolution(identity, unlocked, equipped, -1, null);
    }

    public SpellEquipmentResolution ResolveSpell(SpellDataSO identity)
    {
        bool unlocked = identity != null && unlocks != null && unlocks.IsSpellUnlocked(identity);
        bool equipped = false;
        for (int i = 0; i < DailyLoadoutState<SeedsEnum, SpellDataSO>.SlotsPerCategory; i++)
        {
            var item = equipment.GetCommittedSpell(i);
            if (identity != null && item.HasValue && EqualityComparer<SpellDataSO>.Default.Equals(item.Identity, identity))
                equipped = true;
        }
        if (identity != null && spells != null && !HasConflictingMapping(identity))
        {
            int index = identity.SlotIndex;
            var slot = spells.GetSpellSlot(index);
            if (Matches(slot, identity, index))
                return new SpellEquipmentResolution(identity, unlocked, equipped, index, slot);
        }
        return new SpellEquipmentResolution(identity, unlocked, equipped, -1, null);
    }

    private bool HasConflictingMapping(SpellDataSO identity)
    {
        if (unlocks == null) return false;
        foreach (var other in unlocks.UnlockedSpells)
            if (other != null && other != identity && other.SlotIndex == identity.SlotIndex) return true;
        return false;
    }

    private static bool Matches(SpellSlot slot, SpellDataSO data, int index)
        => slot != null && slot.isUnlocked && slot.spellType != SpellType.None && slot.spellPrefab != null &&
           slot.spellType == data.SpellType && slot.spellPrefab == data.SpellPrefab &&
           slot.spellName == data.SpellName && slot.spellIcon == data.SpellIcon &&
           slot.manaCost == data.ManaCost && slot.cooldown == data.Cooldown &&
           slot.UpgradeId(index) == data.UpgradeTargetId;
}
