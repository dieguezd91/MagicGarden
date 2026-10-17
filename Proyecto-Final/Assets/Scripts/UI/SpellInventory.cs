using System;
using UnityEngine;

public enum SpellType
{
    None = 0,
    Range = 1,
    Melee = 2,
    Area = 3,
    Teleport = 4,
}

[Serializable]
public class SpellSlot
{
    public string upgradeTargetId;
    public string UpgradeId(int index) => string.IsNullOrEmpty(upgradeTargetId) ? "spell:" + index : upgradeTargetId;
    public SpellType spellType;
    public string spellName;
    public Sprite spellIcon;
    public GameObject spellPrefab;
    public int manaCost;
    public float cooldown;
    public float currentCooldown;
    public bool isUnlocked;

    public SpellSlot()
    {
        spellType = SpellType.None;
        spellName = "Empty";
        spellIcon = null;
        spellPrefab = null;
        manaCost = 0;
        cooldown = 0f;
        currentCooldown = 0f;
        isUnlocked = false;
    }
}

public class SpellInventory : MonoBehaviour
{
    public static SpellInventory Instance { get; private set; }

    [Header("Spell Slots Configuration")]
    [SerializeField] public SpellSlot[] spellSlots = new SpellSlot[7];

    [SerializeField] private DailyLoadoutController loadout;
    public DailyLoadoutController Loadout => loadout != null ? loadout : (loadout = FindObjectOfType<DailyLoadoutController>());
    private float[] previousEffectiveCooldowns;

    public event Action<int> onSpellSlotSelected;
    public event Action<int> onCooldownUpdated;
    public event Action OnSpellInventoryChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        InitializeSpellSlots();
    }

    private void Update()
    {
        UpdateCooldowns();
    }

    private void UpdateCooldowns()
    {
        if (previousEffectiveCooldowns == null || previousEffectiveCooldowns.Length != spellSlots.Length)
            previousEffectiveCooldowns = new float[spellSlots.Length];
        for (int i = 0; i < spellSlots.Length; i++)
        {
            float effective = EffectiveCooldown(i);
            if (spellSlots[i] != null && previousEffectiveCooldowns[i] > 0 && previousEffectiveCooldowns[i] != effective)
                spellSlots[i].currentCooldown *= effective / previousEffectiveCooldowns[i];
            previousEffectiveCooldowns[i] = effective;
            if (spellSlots[i] != null && spellSlots[i].currentCooldown > 0f)
            {
                spellSlots[i].currentCooldown -= Time.deltaTime;

                if (spellSlots[i].currentCooldown <= 0f)
                {
                    spellSlots[i].currentCooldown = 0f;
                }

                onCooldownUpdated?.Invoke(i);
            }
        }
    }

    private void InitializeSpellSlots()
    {
        for (int i = 0; i < spellSlots.Length; i++)
        {
            if (spellSlots[i] == null)
            {
                spellSlots[i] = new SpellSlot();
            }
        }
    }

    public void SelectSlot(int index)
    {
        if (UpgradeRuntime.GameplayBlocked) return;
        if (index < 0 || index >= spellSlots.Length) return;

        var slot = GetSpellSlot(index);
        if (slot == null || Loadout == null || !Loadout.TrySelect(true, slot.UpgradeId(index))) return;
        onSpellSlotSelected?.Invoke(index);
    }

    public int GetSelectedSlotIndex()
    {
        return Loadout != null ? Loadout.SelectedSpellIndex : -1;
    }

    public SpellSlot GetSelectedSpellSlot()
    {
        return GetSpellSlot(GetSelectedSlotIndex());
    }

    public SpellSlot GetSpellSlot(int index)
    {
        if (index < 0 || index >= spellSlots.Length) return null;
        return spellSlots[index];
    }

    public bool CanCastSelectedSpell()
    {
        if (UpgradeRuntime.GameplayBlocked || Loadout == null || !Loadout.CanUse(true, Loadout.SelectedSpellId)) return false;
        var slot = GetSelectedSpellSlot();
        if (slot == null || !slot.isUnlocked || slot.spellType == SpellType.None || slot.spellPrefab == null) return false;
        if (slot.currentCooldown > 0f) return false;
        var manaSystem = FindObjectOfType<ManaSystem>();
        if (manaSystem != null && manaSystem.GetCurrentMana() < slot.manaCost)
            return false;

        return true;
    }

    public void StartCooldown(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= spellSlots.Length) return;

        var slot = spellSlots[slotIndex];
        if (slot != null)
        {
            slot.currentCooldown = EffectiveCooldown(slotIndex);
            if (previousEffectiveCooldowns != null && slotIndex < previousEffectiveCooldowns.Length)
                previousEffectiveCooldowns[slotIndex] = slot.currentCooldown;
            onCooldownUpdated?.Invoke(slotIndex);
        }
    }

    public bool UnlockSpell(SpellDataSO spellData)
    {
        if (spellData == null) return false;

        int slotIndex = spellData.SlotIndex;
        if (slotIndex < 0 || slotIndex >= spellSlots.Length)
        {
            Debug.LogWarning($"[SpellInventory] Invalid slot index {slotIndex} for spell {spellData.SpellName}");
            return false;
        }

        if (spellSlots[slotIndex] == null)
        {
            spellSlots[slotIndex] = new SpellSlot();
        }

        spellSlots[slotIndex].upgradeTargetId = spellData.UpgradeTargetId;
        spellSlots[slotIndex].spellType = spellData.SpellType;
        spellSlots[slotIndex].spellName = spellData.SpellName;
        spellSlots[slotIndex].spellIcon = spellData.SpellIcon;
        spellSlots[slotIndex].spellPrefab = spellData.SpellPrefab;
        spellSlots[slotIndex].manaCost = spellData.ManaCost;
        spellSlots[slotIndex].cooldown = spellData.Cooldown;
        spellSlots[slotIndex].isUnlocked = true;
        spellSlots[slotIndex].currentCooldown = 0f;

        var currentSelected = GetSelectedSpellSlot();
        if (currentSelected == null || !currentSelected.isUnlocked || currentSelected.spellType == SpellType.None)
        {
            SelectSlot(slotIndex);
        }

        OnSpellInventoryChanged?.Invoke();
        return true;
    }

    public void UnlockSpell(
        int slotIndex,
        SpellType spellType,
        string spellName,
        Sprite spellIcon,
        GameObject spellPrefab,
        int manaCost,
        float cooldown)
    {
        if (slotIndex < 0 || slotIndex >= spellSlots.Length) return;

        if (spellSlots[slotIndex] == null)
        {
            spellSlots[slotIndex] = new SpellSlot();
        }

        spellSlots[slotIndex].spellType = spellType;
        spellSlots[slotIndex].spellName = spellName;
        spellSlots[slotIndex].spellIcon = spellIcon;
        spellSlots[slotIndex].spellPrefab = spellPrefab;
        spellSlots[slotIndex].manaCost = manaCost;
        spellSlots[slotIndex].cooldown = cooldown;
        spellSlots[slotIndex].isUnlocked = true;
        spellSlots[slotIndex].currentCooldown = 0f;

        var currentSelected = GetSelectedSpellSlot();
        if (currentSelected == null || !currentSelected.isUnlocked || currentSelected.spellType == SpellType.None)
        {
            SelectSlot(slotIndex);
        }

        OnSpellInventoryChanged?.Invoke();
        Debug.Log($"Spell unlocked: {spellName} in slot {slotIndex + 1}");
    }

    public void ResetUnlockedSpells()
    {
        if (spellSlots == null) return;

        for (int i = 0; i < spellSlots.Length; i++)
        {
            if (spellSlots[i] != null)
            {
                spellSlots[i].isUnlocked = false;
                spellSlots[i].currentCooldown = 0f;
            }
        }

        OnSpellInventoryChanged?.Invoke();
    }

    public float GetCooldownProgress(int slotIndex)
    {
        var slot = GetSpellSlot(slotIndex);
        if (slot == null || slot.cooldown <= 0f) return 0f;

        return slot.currentCooldown / EffectiveCooldown(slotIndex);
    }

    public float EffectiveCooldown(int index)
    {
        var slot = GetSpellSlot(index);
        if (slot == null) return 0.001f;
        var spell = slot.spellPrefab != null ? slot.spellPrefab.GetComponent<Spell>() : null;
        return UpgradeRuntime.Cooldown(slot.UpgradeId(index), slot.cooldown, spell != null ? spell.GetType().Name : "");
    }

    public void CycleSpell(int direction)
    {
        if (UpgradeRuntime.GameplayBlocked) return;
        if (spellSlots == null || spellSlots.Length == 0) return;
        if (direction == 0) return;

        int step = direction > 0 ? 1 : -1;
        int totalSlots = spellSlots.Length;

        for (int i = 1; i <= totalSlots; i++)
        {
            int selectedIndex = GetSelectedSlotIndex();
            if (selectedIndex < 0) selectedIndex = step > 0 ? -1 : 0;
            int candidateIndex = (selectedIndex + (i * step) % totalSlots + totalSlots) % totalSlots;
            var slot = spellSlots[candidateIndex];
            if (slot != null && slot.isUnlocked && slot.spellType != SpellType.None && slot.spellPrefab != null &&
                Loadout != null && Loadout.CanUse(true, slot.UpgradeId(candidateIndex)))
            {
                SelectSlot(candidateIndex);
                return;
            }
        }
    }
}