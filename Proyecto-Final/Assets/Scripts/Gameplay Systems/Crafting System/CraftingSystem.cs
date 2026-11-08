using System.Collections.Generic;
using UnityEngine;

public enum SeedsEnum
{
    None = 0,
    // Production Plants
    MoonTear,
    CrimsonFruit,
    DarkRoot,
    EtherBloom,
    SpectralMushroom,
    CyclonicVine,

    // Defensive Plants
    ThornedTendrils,
    ElectricOrchid,
    ShadowIvy,
    ExplosiveBulb,
    IceThorn,
    MoonLily,
    StellarOrchid,

    // Hybrid Plants
    FrostThorn,
    StormRose,
    VolcanicMushroom,
    AstralWaterLily
}

[System.Serializable]
public class MaterialRequirement
{
    public MaterialType materialType;
    public int quantity;
}

public class CraftingSystem : MonoBehaviour
{
    [SerializeField] public CraftingRecipesSeedsListSO craftingRecipes;
    [SerializeField] private List<PlantDataSO> plantDataList;
    [SerializeField] private PlayerContentUnlockSystem contentUnlockSystem;

    private Dictionary<SeedsEnum, PlantDataSO> seedToPlantData = new Dictionary<SeedsEnum, PlantDataSO>();

    private void Awake()
    {
        if (plantDataList != null)
        {
            foreach (var plantData in plantDataList)
            {
                if (plantData != null && plantData.seedType != SeedsEnum.None)
                {
                    seedToPlantData[plantData.seedType] = plantData;
                }
            }
        }

        EnsureContentUnlockSystem();
    }

    private void EnsureContentUnlockSystem()
    {
        if (contentUnlockSystem == null)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                contentUnlockSystem = player.GetComponent<PlayerContentUnlockSystem>();
            }

            if (contentUnlockSystem == null)
            {
                contentUnlockSystem = FindObjectOfType<PlayerContentUnlockSystem>();
            }
        }
    }

    public bool IsRecipeUnlocked(SeedsEnum seedType)
    {
        EnsureContentUnlockSystem();
        if (contentUnlockSystem == null) return true;

        PlantDataSO plantData = GetPlantData(seedType);
        if (plantData == null) return false;

        return contentUnlockSystem.IsPlantUnlocked(plantData);
    }

    public void CraftSeed(SeedsEnum seedToCraft)
    {
        if (craftingRecipes == null)
        {
            return;
        }

        if (!IsRecipeUnlocked(seedToCraft))
        {
            Debug.LogWarning($"[CraftingSystem] Cannot craft seed {seedToCraft}: Plant is not unlocked yet.");
            return;
        }

        CraftingRecipeSeedData recipe = craftingRecipes.recipes.Find(r => r.SeedToCraft == seedToCraft);

        if (recipe == null)
        {
            return;
        }

        if (UpgradeRuntime.GameplayBlocked || !seedToPlantData.TryGetValue(seedToCraft, out var knownPlant) || SeedInventory.Instance == null) return;
        if (FindSlotBySeed(seedToCraft) == -1 && FindFreeSlotOrSpecific(knownPlant) == -1) return;
        if (InventoryManager.Instance != null && InventoryManager.Instance.TrySpendCosts(ToCosts(recipe.MaterialsRequired)))
        {

            if (seedToPlantData.TryGetValue(seedToCraft, out PlantDataSO plantData))
            {
                int slotIndex = FindFreeSlotOrSpecific(plantData);

                var existingSlot = FindSlotBySeed(seedToCraft);
                if (existingSlot == -1)
                {
                    if (slotIndex != -1)
                    {
                        SeedInventory.Instance.UnlockPlant(
                            plantData.seedType,
                            plantData.plantPrefab,
                            plantData.plantName,
                            plantData.plantIcon,
                            slotIndex,
                            plantData.daysToGrow,
                            1,
                            plantData.description,
                            plantData
                        );
                    }
                }
                else
                {
                    SeedInventory.Instance.AddSeedsToSlot(existingSlot, 1);
                }
            }

            if (UIManager.Instance?.inventoryUI != null)
            {
                UIManager.Instance.inventoryUI.UpdateAllSlots();
            }
        }
    }

    private int FindFreeSlotOrSpecific(PlantDataSO plantData)
    {
        int slotCount = SeedInventory.Instance != null ? SeedInventory.Instance.PlantSlotsCount : 0;
        for (int i = 0; i < slotCount; i++)
        {
            PlantSlot slot = SeedInventory.Instance.GetPlantSlot(i);
            if (slot != null && slot.plantPrefab == null && string.IsNullOrEmpty(slot.plantName))
            {
                return i;
            }
        }

        return -1;
    }

    public bool HasRequiredMaterials(List<MaterialRequirement> materialsRequired)
    {
        return InventoryManager.Instance != null && InventoryManager.Instance.HasCosts(ToCosts(materialsRequired));
    }

    private static List<MagicGarden.Cost> ToCosts(List<MaterialRequirement> requirements)
    {
        if (requirements == null) return null;
        var costs = new List<MagicGarden.Cost>();
        foreach (var requirement in requirements)
        {
            if (requirement == null) return null;
            costs.Add(new MagicGarden.Cost { material = (int)requirement.materialType, amount = requirement.quantity });
        }
        return costs;
    }

    public CraftingRecipeSeedData GetRecipe(SeedsEnum seedType)
    {
        if (craftingRecipes == null || craftingRecipes.recipes == null) return null;
        return craftingRecipes.recipes.Find(r => r.SeedToCraft == seedType);
    }

    public bool HasRecipeFor(SeedsEnum seedType)
    {
        return GetRecipe(seedType) != null;
    }

    public List<CraftingRecipeSeedData> GetAllAvailableRecipes()
    {
        var result = new List<CraftingRecipeSeedData>();
        if (craftingRecipes == null || craftingRecipes.recipes == null)
            return result;

        EnsureContentUnlockSystem();

        for (int i = 0; i < craftingRecipes.recipes.Count; i++)
        {
            var recipe = craftingRecipes.recipes[i];
            if (recipe == null) continue;

            if (contentUnlockSystem != null)
            {
                PlantDataSO plantData = GetPlantData(recipe.SeedToCraft);
                if (plantData != null && contentUnlockSystem.IsPlantUnlocked(plantData))
                {
                    result.Add(recipe);
                }
            }
            else
            {
                result.Add(recipe);
            }
        }

        return result;
    }

    public PlantDataSO GetPlantData(SeedsEnum seedType)
    {
        if (seedToPlantData.TryGetValue(seedType, out var data))
            return data;

        return null;
    }

    private int FindSlotBySeed(SeedsEnum seed)
    {
        int slotCount = SeedInventory.Instance != null ? SeedInventory.Instance.PlantSlotsCount : 0;
        for (int i = 0; i < slotCount; i++)
        {
            var slot = SeedInventory.Instance.GetPlantSlot(i);
            if (slot != null && slot.seedType == seed && slot.plantPrefab != null)
                return i;
        }
        return -1;
    }
}
