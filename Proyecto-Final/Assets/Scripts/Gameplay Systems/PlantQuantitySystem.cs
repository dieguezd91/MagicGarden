using MagicGarden;
using UnityEngine;

public class PlantQuantitySystem : MonoBehaviour
{
    public static PlantQuantitySystem Instance { get; private set; }

    [Header("PLANT QUANTITY")]
    [SerializeField] private int startingQuantity = 5;

    // Run-independent base limit: the serialized starting value unless legacy SetQuantity/IncreaseQuantity changed it.
    // The serialized value and prefab are never mutated; the run's PlantLimit upgrades are added on top when read.
    private int baseQuantity = -1;

    public int BaseQuantity => baseQuantity >= 0 ? baseQuantity : Mathf.Max(0, startingQuantity);
    public int CurrentQuantity => LimitWith(UpgradeRuntime.Current != null ? UpgradeRuntime.Current.State : null);

    // Base limit plus the PlantLimit stacks held by the given run state (one plant per stack). Resetting the run clears the stacks.
    public int LimitWith(RunState state)
    {
        float value = state != null ? state.Value(UpgradeRuntime.PlayerId, Stat.PlantLimit, BaseQuantity, 0) : BaseQuantity;
        return Mathf.Max(0, Mathf.RoundToInt(value));
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