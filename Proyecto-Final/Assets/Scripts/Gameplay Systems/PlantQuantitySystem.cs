using UnityEngine;

public class PlantQuantitySystem : MonoBehaviour
{
    public static PlantQuantitySystem Instance { get; private set; }

    [Header("PLANT QUANTITY")]
    [SerializeField] private int startingQuantity = 5;

    // Every level reached after the first raises the limit by this much, with no cap.
    public const int QuantityPerLevel = 1;

    // Level-1 limit: the serialized starting value unless legacy SetQuantity/IncreaseQuantity changed it.
    // The serialized value and prefab are never mutated; the level bonus is added on top when read.
    private int baseQuantity = -1;
    private PlayerExperienceSystem experience;

    public int BaseQuantity => baseQuantity >= 0 ? baseQuantity : Mathf.Max(0, startingQuantity);
    public int CurrentQuantity => LimitAtLevel(CurrentLevel);

    // Base limit plus one plant per level above 1. Resetting the progression to level 1 returns it to the base.
    public int LimitAtLevel(int level) => BaseQuantity + Mathf.Max(0, level - 1) * QuantityPerLevel;

    private int CurrentLevel
    {
        get
        {
            if (experience == null) experience = FindObjectOfType<PlayerExperienceSystem>();
            return experience != null ? experience.CurrentLevel : 1;
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public int GetCurrentPlantCount()
    {
        Plant[] plants = FindObjectsOfType<Plant>();

        return plants.Length;
    }

    public bool CanPlant()
    {
        return GetCurrentPlantCount() < CurrentQuantity;
    }

    public int GetRemainingQuantity()
    {
        int currentPlantCount = GetCurrentPlantCount();

        return Mathf.Max(0, CurrentQuantity - currentPlantCount);
    }

    public void IncreaseQuantity(int amount)
    {
        if (amount <= 0)
            return;

        baseQuantity = BaseQuantity + amount;

        Debug.Log(
            $"[PlantQuantitySystem] Quantity aumentada a {CurrentQuantity}"
        );
    }

    public void SetQuantity(int quantity)
    {
        baseQuantity = Mathf.Max(0, quantity);
    }
}