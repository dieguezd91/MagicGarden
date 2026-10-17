using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(PlayerExperienceSystem))]
public class PlayerContentUnlockSystem : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerExperienceSystem playerExperienceSystem;
    [SerializeField] private SpellInventory spellInventory;

    private readonly HashSet<SeedsEnum> unlockedPlants = new HashSet<SeedsEnum>();
    private readonly HashSet<SpellDataSO> unlockedSpells = new HashSet<SpellDataSO>();
    private readonly Dictionary<SeedsEnum, PlantDataSO> unlockedPlantData = new Dictionary<SeedsEnum, PlantDataSO>();
    public bool TryGetUnlockedPlant(SeedsEnum seed, out PlantDataSO data) => unlockedPlantData.TryGetValue(seed, out data);
    private readonly HashSet<SeedsEnum> deliveredSeeds = new HashSet<SeedsEnum>();
    private readonly List<PlantDataSO> pendingSeeds = new List<PlantDataSO>();
    private SeedInventory seedInventory;
    private bool deliveringSeeds;

    public event Action<PlantDataSO> OnPlantUnlocked;
    public event Action<SpellDataSO> OnSpellUnlocked;
    public event Action OnUnlockStateRebuilt;

    public IReadOnlyCollection<SeedsEnum> UnlockedPlants => unlockedPlants;
    public IReadOnlyCollection<SpellDataSO> UnlockedSpells => unlockedSpells;

    private void Awake()
    {
        EnsurePlayerExperienceSystem();
        EnsureSpellInventory();
    }

    private void OnEnable()
    {
        EnsurePlayerExperienceSystem();
        if (playerExperienceSystem != null)
        {
            playerExperienceSystem.OnLevelUp += HandleLevelUp;
            playerExperienceSystem.OnProgressionReset += HandleProgressionReset;
        }
    }

    private void OnDisable()
    {
        if (playerExperienceSystem != null)
        {
            playerExperienceSystem.OnLevelUp -= HandleLevelUp;
            playerExperienceSystem.OnProgressionReset -= HandleProgressionReset;
        }
    }

    private void Start()
    {
        seedInventory = SeedInventory.Instance;
        if (seedInventory != null) seedInventory.onInventoryChanged += DeliverPendingSeeds;
        InitializeUnlocks();
    }
    private void OnDestroy()
    {
        if (seedInventory != null) seedInventory.onInventoryChanged -= DeliverPendingSeeds;
    }
    private void QueueSeeds(PlantDataSO plant)
    {
        if (deliveredSeeds.Add(plant.seedType)) pendingSeeds.Add(plant);
    }
    private void DeliverPendingSeeds()
    {
        if (deliveringSeeds || seedInventory == null) return;
        int amount = UpgradeRuntime.Current?.Balance != null ? UpgradeRuntime.Current.Balance.seedsOnPlantUnlock : 0;
        if (amount <= 0) return;
        deliveringSeeds = true;
        try
        {
            for (int i = pendingSeeds.Count - 1; i >= 0; i--)
                if (seedInventory.TryGrantUnlockedPlant(pendingSeeds[i], amount)) pendingSeeds.RemoveAt(i);
        }
        finally { deliveringSeeds = false; }
    }

    private void EnsurePlayerExperienceSystem()
    {
        if (playerExperienceSystem == null)
        {
            playerExperienceSystem = GetComponent<PlayerExperienceSystem>();
            if (playerExperienceSystem == null)
            {
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                {
                    playerExperienceSystem = player.GetComponent<PlayerExperienceSystem>();
                }
            }

            if (playerExperienceSystem == null)
            {
                playerExperienceSystem = FindObjectOfType<PlayerExperienceSystem>();
            }
        }
    }

    private void EnsureSpellInventory()
    {
        if (spellInventory == null)
        {
            spellInventory = SpellInventory.Instance;
            if (spellInventory == null)
            {
                spellInventory = FindObjectOfType<SpellInventory>();
            }
        }
    }

    public void InitializeUnlocks()
    {
        EnsurePlayerExperienceSystem();
        EnsureSpellInventory();

        int targetLevel = playerExperienceSystem != null ? playerExperienceSystem.CurrentLevel : 1;
        RebuildUnlocksUpToLevel(targetLevel);
    }

    public void RebuildUnlocksUpToLevel(int targetLevel)
    {
        EnsurePlayerExperienceSystem();
        EnsureSpellInventory();

        unlockedPlants.Clear();
        unlockedPlantData.Clear();
        unlockedSpells.Clear();

        if (spellInventory != null)
        {
            spellInventory.ResetUnlockedSpells();
        }

        if (playerExperienceSystem == null || playerExperienceSystem.ProgressionData == null)
        {
            OnUnlockStateRebuilt?.Invoke();
            return;
        }

        var progressionData = playerExperienceSystem.ProgressionData;
        for (int lvl = 1; lvl <= targetLevel; lvl++)
        {
            var levelData = progressionData.GetLevelData(lvl);
            if (levelData == null) continue;

            var plantList = levelData.PlantUnlocks;
            if (plantList != null)
            {
                for (int i = 0; i < plantList.Count; i++)
                {
                    var plant = plantList[i];
                    if (plant != null && plant.seedType != SeedsEnum.None)
                    {
                        unlockedPlants.Add(plant.seedType);
                        unlockedPlantData[plant.seedType] = plant;
                        QueueSeeds(plant);
                    }
                }
            }

            var spellList = levelData.SpellUnlocks;
            if (spellList != null)
            {
                for (int i = 0; i < spellList.Count; i++)
                {
                    var spell = spellList[i];
                    if (spell != null)
                    {
                        unlockedSpells.Add(spell);
                        if (spellInventory != null)
                        {
                            spellInventory.UnlockSpell(spell);
                        }
                    }
                }
            }
        }

        DeliverPendingSeeds();
        OnUnlockStateRebuilt?.Invoke();
    }

    private void HandleLevelUp(int level)
    {
        EnsurePlayerExperienceSystem();
        EnsureSpellInventory();

        if (playerExperienceSystem == null || playerExperienceSystem.ProgressionData == null)
        {
            return;
        }

        var levelData = playerExperienceSystem.ProgressionData.GetLevelData(level);
        if (levelData == null)
        {
            return;
        }

        var plantList = levelData.PlantUnlocks;
        if (plantList != null)
        {
            for (int i = 0; i < plantList.Count; i++)
            {
                var plant = plantList[i];
                if (plant != null && plant.seedType != SeedsEnum.None)
                {
                    if (unlockedPlants.Add(plant.seedType))
                    {
                        unlockedPlantData[plant.seedType] = plant;
                        QueueSeeds(plant);
                        DeliverPendingSeeds();
                        OnPlantUnlocked?.Invoke(plant);
                    }
                }
            }
        }

        var spellList = levelData.SpellUnlocks;
        if (spellList != null)
        {
            for (int i = 0; i < spellList.Count; i++)
            {
                var spell = spellList[i];
                if (spell != null)
                {
                    if (unlockedSpells.Add(spell))
                    {
                        if (spellInventory != null)
                        {
                            spellInventory.UnlockSpell(spell);
                        }
                        OnSpellUnlocked?.Invoke(spell);
                    }
                }
            }
        }
    }

    private void HandleProgressionReset()
    {
        deliveredSeeds.Clear();
        pendingSeeds.Clear();
        int resetLevel = playerExperienceSystem != null ? playerExperienceSystem.CurrentLevel : 1;
        RebuildUnlocksUpToLevel(resetLevel);
    }

    public bool IsPlantUnlocked(PlantDataSO plantData)
    {
        if (plantData == null) return false;
        return IsPlantUnlocked(plantData.seedType);
    }

    public bool IsPlantUnlocked(SeedsEnum seedType)
    {
        if (seedType == SeedsEnum.None) return false;
        return unlockedPlants.Contains(seedType);
    }

    public bool IsSpellUnlocked(SpellDataSO spellData)
    {
        if (spellData == null) return false;
        return unlockedSpells.Contains(spellData);
    }
}
