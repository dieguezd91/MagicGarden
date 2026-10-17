using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class TilePlantingSystem : MonoBehaviour
{
    public static TilePlantingSystem Instance { get; private set; }

    [Header("Tilemap Reference")]
    [SerializeField] private Tilemap plantingTilemap;
    [SerializeField] private TileBase allowedPlantTile;
    public Tilemap PlantingTilemap => plantingTilemap;

    private Dictionary<Vector3Int, Plant> plantedTiles = new();
    private DailyLoadoutController loadout;
    private PlayerAbilitySystem abilities;
    private ManaSystem mana;
    private PlantQuantitySystem quantity;
    private bool committingPlant;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        loadout = FindObjectOfType<DailyLoadoutController>();
        abilities = FindObjectOfType<PlayerAbilitySystem>();
        mana = abilities != null ? abilities.GetComponent<ManaSystem>() : null;
        quantity = FindObjectOfType<PlantQuantitySystem>();
    }

    public bool CanPlantAt(Vector3Int cellPos)
    {
        TileBase currentTile = plantingTilemap.GetTile(cellPos);
        return currentTile == allowedPlantTile && !plantedTiles.ContainsKey(cellPos);
    }

    public bool TryPlant(Vector3Int cellPos, GameObject plantPrefab, out string failureReason)
    {
        failureReason = "Planting requires an equipped seed during the day.";
        if (committingPlant || loadout == null || !loadout.CanPlant(plantPrefab)) return false;
        if (plantingTilemap == null || plantPrefab == null || plantPrefab.GetComponent<Plant>() == null) return false;
        if (abilities != null && Vector2.Distance(abilities.transform.position,
            plantingTilemap.GetCellCenterWorld(cellPos)) > abilities.interactionDistance)
        {
            failureReason = "Too far to plant.";
            return false;
        }
        if (quantity != null && !quantity.CanPlant())
        {
            failureReason = "You reached the plant limit.";
            return false;
        }

        TileBase currentTile = plantingTilemap.GetTile(cellPos);

        if (currentTile != allowedPlantTile)
        {
            failureReason = "The soil is not tilled.";
            return false;
        }

        if (plantedTiles.ContainsKey(cellPos))
        {
            failureReason = "There's already a plant here.";
            return false;
        }

        float cost = abilities != null ? abilities.PlantManaCost : 0f;
        failureReason = "Not enough mana to plant!";
        if (mana != null && mana.GetCurrentMana() < cost) return false;
        string id = loadout.SelectedPlantId;
        committingPlant = true;
        try
        {
            if (mana != null && !mana.UseMana(cost)) return false;
            if (!loadout.Seeds.TryConsumeSeed(id))
            {
                if (mana != null) mana.SetMana(mana.GetCurrentMana() + cost);
                failureReason = "No seeds left.";
                return false;
            }
            Vector3 worldPos = plantingTilemap.GetCellCenterWorld(cellPos);
            GameObject plantGO = Instantiate(plantPrefab, worldPos, Quaternion.identity);
            Plant plant = plantGO.GetComponent<Plant>();
            plant.tilePosition = cellPos;
            plantedTiles[cellPos] = plant;
            PlantManager.Instance.RegisterPlant(plant);
            InvokePlantEventByType(plant);
            failureReason = "";
            return true;
        }
        finally { committingPlant = false; }
    }

    private void InvokePlantEventByType(Plant plant)
    {
        if (plant == null) return;

        PlantType plantType = DeterminePlantType(plant);

        switch (plantType)
        {
            case PlantType.Production:
                TutorialEvents.InvokeProductionPlantPlanted();
                break;

            case PlantType.Defensive:
                TutorialEvents.InvokeDefensivePlantPlanted();
                break;
        }
    }

    private PlantType DeterminePlantType(Plant plant)
    {
        if (plant is ResourcePlant)
        {
            return PlantType.Production;
        }

        if (plant is AttackPlant || plant is DefensePlant)
        {
            return PlantType.Defensive;
        }

        return PlantType.Production;
    }

    public void UnregisterPlantAt(Vector3Int cellPos)
    {
        if (plantedTiles.TryGetValue(cellPos, out Plant plant))
        {
            PlantManager.Instance.UnregisterPlant(plant);
            plantedTiles.Remove(cellPos);
        }
    }

    public Plant GetPlantAt(Vector3Int cellPos)
    {
        plantedTiles.TryGetValue(cellPos, out var plant);
        return plant;
    }

    public IEnumerable<Vector3Int> GetAllPlantedTiles() => plantedTiles.Keys;
}

public enum PlantType
{
    Production,
    Defensive,
}