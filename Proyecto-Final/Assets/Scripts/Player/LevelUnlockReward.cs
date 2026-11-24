using System.Collections.Generic;

// Seeds and spells newly unlocked by reaching one level, plus that level's plant limit increase,
// shown to the player before that level's upgrade choice.
public sealed class LevelUnlockReward
{
    private readonly List<PlantDataSO> plants = new List<PlantDataSO>();
    private readonly List<SpellDataSO> spells = new List<SpellDataSO>();

    public LevelUnlockReward(int level) => Level = level;

    public int Level { get; }
    public IReadOnlyList<PlantDataSO> Plants => plants;
    public IReadOnlyList<SpellDataSO> Spells => spells;
    public int PlantLimitBonus { get; private set; }
    // The plant limit after this level, or 0 when no plant quantity system was available to report it.
    public int PlantLimit { get; private set; }
    public bool HasContent => plants.Count > 0 || spells.Count > 0 || PlantLimitBonus > 0;

    internal void AddPlantLimit(int bonus, int limit)
    {
        PlantLimitBonus += bonus;
        PlantLimit = limit;
    }
    internal void AddPlant(PlantDataSO plant) => plants.Add(plant);
    internal void AddSpell(SpellDataSO spell) => spells.Add(spell);
}
