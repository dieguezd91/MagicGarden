using System.Collections.Generic;

namespace MagicGarden.LoadoutUI
{
    /// <summary>Ordered asset projection. Unlock collections are queried only for membership.</summary>
    public sealed class DailyLoadoutCatalog
    {
        private readonly List<PlantDataSO> plants = new List<PlantDataSO>();
        private readonly List<SpellDataSO> spells = new List<SpellDataSO>();
        private readonly HashSet<SeedsEnum> seenPlants = new HashSet<SeedsEnum>();
        private readonly HashSet<SpellDataSO> seenSpells = new HashSet<SpellDataSO>();
        public IReadOnlyList<PlantDataSO> Plants => plants;
        public IReadOnlyList<SpellDataSO> Spells => spells;

        public void Refresh(ExperienceProgressionDataSO progression, PlayerContentUnlockSystem unlocks)
        {
            plants.Clear(); spells.Clear(); seenPlants.Clear(); seenSpells.Clear();
            if (progression == null || unlocks == null || !unlocks.HasInitializedUnlockState) return;
            // Traverse the authored catalog, not inventory order or HashSet iteration order.
            int levels = progression.LevelRequirements?.Count ?? 0;
            for (int level = 1; level <= levels; level++)
            {
                var content = progression.GetLevelData(level);
                if (content == null) continue;
                foreach (var plant in content.PlantUnlocks)
                    if (plant != null && plant.seedType != SeedsEnum.None &&
                        unlocks.IsPlantUnlocked(plant.seedType) && seenPlants.Add(plant.seedType)) plants.Add(plant);
                foreach (var spell in content.SpellUnlocks)
                    if (spell != null && unlocks.IsSpellUnlocked(spell) && seenSpells.Add(spell)) spells.Add(spell);
            }
        }

        public PlantDataSO FindPlant(SeedsEnum identity)
        {
            foreach (var plant in plants) if (plant.seedType == identity) return plant;
            return null;
        }
    }
}
