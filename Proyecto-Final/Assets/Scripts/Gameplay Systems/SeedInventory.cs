using System;
using System.Collections;
using UnityEngine;

[Serializable]
public class PlantSlot
{
    public SeedsEnum seedType;
    public GameObject plantPrefab;
    public string plantName;
    public Sprite plantIcon;
    public int daysToGrow;
    public string description;

    public int seedCount = 0;

    public PlantDataSO data;

}

public class SeedInventory : MonoBehaviour
{
    public static SeedInventory Instance;

    [Header("SEED INVENTORY")]
    [SerializeField] private PlantSlot[] plantSlots = new PlantSlot[9];
    [SerializeField] private DailyLoadoutController loadout;
    public DailyLoadoutController Loadout => loadout != null ? loadout : (loadout = FindObjectOfType<DailyLoadoutController>());
    private int selectedSlotIndex => Loadout != null ? Loadout.SelectedSeedIndex : -1;

    [Header("UI EVENTS")]
    public Action<int> onSlotSelected;
    public Action onInventoryChanged;

    public int PlantSlotsCount => plantSlots != null ? plantSlots.Length : 0;

    private void NotifyInventoryChanged()
    {
        onInventoryChanged?.Invoke();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        for (int i = 0; i < plantSlots.Length; i++)
        {
            if (plantSlots[i] == null)
                plantSlots[i] = new PlantSlot();
        }
    }

    private void Start()
    {
        StartCoroutine(DelayedSelectFirstSlot());
    }

    private IEnumerator DelayedSelectFirstSlot()
    {
        yield return null;
        SelectSlot(0);
    }

    //private void Update()
    //{
    //    for (int i = 0; i < 9; i++)
    //    {
    //        if (Input.GetKeyDown(KeyCode.Alpha1 + i))
    //        {
    //            SelectSlot(i);
    //        }
    //    }
    //}

    public void RemoveSeedFromSlot(int slotIndex)
    {
        if (slotIndex >= 0 && slotIndex < plantSlots.Length)
        {
            plantSlots[slotIndex].seedType = SeedsEnum.None;
            plantSlots[slotIndex].plantPrefab = null;
            plantSlots[slotIndex].plantName = "";
            plantSlots[slotIndex].plantIcon = null;
            plantSlots[slotIndex].daysToGrow = 0;
            plantSlots[slotIndex].description = "";
            plantSlots[slotIndex].data = null;
            plantSlots[slotIndex].seedCount = 0;

            Debug.Log($"Slot {slotIndex + 1} vaciado");
            NotifyInventoryChanged();
        }
    }

    public void SelectSlot(int slotIndex)
    {
        if (UpgradeRuntime.GameplayBlocked) return;
        if (slotIndex >= 0 && slotIndex < plantSlots.Length)
        {
            if (Loadout == null || !Loadout.TrySelect(false, DailyLoadoutController.PlantId(plantSlots[slotIndex]))) return;
            onSlotSelected?.Invoke(slotIndex);
        }
    }

    public GameObject GetSelectedPlantPrefab()
    {
        if (selectedSlotIndex >= 0 && selectedSlotIndex < plantSlots.Length)
        {
            return plantSlots[selectedSlotIndex].plantPrefab;
        }
        return null;
    }

    public PlantSlot GetPlantSlot(int index)
    {
        if (index >= 0 && index < plantSlots.Length)
        {
            return plantSlots[index];
        }
        return null;
    }

    public PlantSlot GetSelectedPlantSlot()
    {
        return GetPlantSlot(selectedSlotIndex);
    }

    public string GetSelectedPlantName()
    {
        PlantSlot slot = GetSelectedPlantSlot();
        return slot != null ? slot.plantName : "None";
    }

    public int GetSelectedSlotIndex()
    {
        return selectedSlotIndex;
    }

    public bool HasSeedsInSelectedSlot()
    {
        var slot = GetSelectedPlantSlot();
        return slot != null && slot.seedCount > 0;
    }

    public void ConsumeSeedInSelectedSlot()
    {
        if (Loadout == null) return;
        TryConsumeSeed(Loadout.SelectedPlantId);
    }

    public bool TryConsumeSeed(string plantId)
    {
        if (UpgradeRuntime.GameplayBlocked || Loadout == null || !Loadout.CanUse(false, plantId)) return false;
        int index = Loadout.FindSeed(plantId);
        var slot = GetPlantSlot(index);
        if (slot == null || slot.seedCount <= 0) return false;
        slot.seedCount--;
        if (slot.seedCount == 0) RemoveSeedFromSlot(index);
        else NotifyInventoryChanged();
        return true;
    }

    // New plant unlocks remain usable after the cauldron stops producing seeds.
    // Existing seed counts/data are preserved; full inventory defers delivery.
    public bool TryGrantUnlockedPlant(PlantDataSO data, int amount)
    {
        if (data == null || data.seedType == SeedsEnum.None || data.plantPrefab == null || amount <= 0) return false;
        for (int i = 0; i < plantSlots.Length; i++)
            if (plantSlots[i] != null && plantSlots[i].seedType == data.seedType && plantSlots[i].seedCount > 0) return true;
        for (int i = 0; i < plantSlots.Length; i++)
            if (plantSlots[i] == null || plantSlots[i].seedCount <= 0)
            {
                if (plantSlots[i] == null) plantSlots[i] = new PlantSlot();
                UnlockPlant(data.seedType, data.plantPrefab, data.plantName, data.plantIcon, i, data.daysToGrow, amount, data.description, data);
                return true;
            }
        return false;
    }

    public bool AddSeedsToSlot(int slotIndex, int amount)
    {
        if (slotIndex < 0 || slotIndex >= plantSlots.Length)
            return false;

        if (amount <= 0)
            return false;

        PlantSlot slot = plantSlots[slotIndex];
        if (slot == null)
            return false;

        slot.seedCount += amount;
        NotifyInventoryChanged();
        return true;
    }

    public int GetSeedCountInSlot(int index)
    {
        var slot = GetPlantSlot(index);
        return slot != null ? slot.seedCount : 0;
    }

    public void UnlockPlant(
        SeedsEnum seedType,
        GameObject plantPrefab,
        string plantName,
        Sprite plantIcon,
        int slotIndex,
        int daysToGrow,
        int initialSeedCount,
        string description = "",
        PlantDataSO data = null)
    {
        if (slotIndex >= 0 && slotIndex < plantSlots.Length)
        {
            plantSlots[slotIndex].seedType = seedType;
            plantSlots[slotIndex].plantPrefab = plantPrefab;
            plantSlots[slotIndex].plantName = plantName;
            plantSlots[slotIndex].plantIcon = plantIcon;
            plantSlots[slotIndex].daysToGrow = daysToGrow;
            plantSlots[slotIndex].description = description;
            plantSlots[slotIndex].data = data;
            plantSlots[slotIndex].seedCount = initialSeedCount;

            Debug.Log($"Unlocked new plant: {plantName} (Seed: {seedType}) in slot {slotIndex + 1}");
            NotifyInventoryChanged();
        }
    }

    public bool SwapSlots(int a, int b)
    {
        if (UpgradeRuntime.GameplayBlocked) return false;
        if (a < 0 || a >= plantSlots.Length || b < 0 || b >= plantSlots.Length)
            return false;

        if (a == b)
            return false;

        if (plantSlots[a] == null || plantSlots[b] == null)
            return false;

        (plantSlots[a], plantSlots[b]) = (plantSlots[b], plantSlots[a]);
        NotifyInventoryChanged();
        return true;
    }
}
