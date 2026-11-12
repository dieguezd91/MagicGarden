using System.Collections.Generic;

// Seeds and spells newly unlocked by reaching one level, shown to the player before that level's upgrade choice.
public sealed class LevelUnlockReward
{
    private readonly List<PlantDataSO> plants = new List<PlantDataSO>();
    private readonly List<SpellDataSO> spells = new List<SpellDataSO>();

    public LevelUnlockReward(int level) => Level = level;

    public int Level { get; }
    public IReadOnlyList<PlantDataSO> Plants => plants;
    public IReadOnlyList<SpellDataSO> Spells => spells;
    public bool HasContent => plants.Count > 0 || spells.Count > 0;

    internal void AddPlant(PlantDataSO plant) => plants.Add(plant);
    internal void AddSpell(SpellDataSO spell) => spells.Add(spell);
}
