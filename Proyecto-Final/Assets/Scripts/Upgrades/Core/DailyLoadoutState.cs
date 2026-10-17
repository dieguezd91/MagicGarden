using System;

namespace MagicGarden
{
    /// <summary>Run-local equipment, independent of inventory order and quantities.</summary>
    public sealed class DailyLoadoutState
    {
        public const int Capacity = 3;
        private readonly string[] draftSpells = new string[Capacity];
        private readonly string[] draftPlants = new string[Capacity];
        private readonly string[] spells = new string[Capacity];
        private readonly string[] plants = new string[Capacity];
        public int Day { get; private set; } = -1;
        public bool IsConfirmed { get; private set; }
        public event Action Confirmed;

        public string GetDraft(bool spell, int slot) => Get(spell ? draftSpells : draftPlants, slot);
        public string GetEquipped(bool spell, int slot) => Get(spell ? spells : plants, slot);
        private static string Get(string[] slots, int slot) => slot >= 0 && slot < Capacity ? slots[slot] : null;

        public bool BeginDay(int day)
        {
            if (day <= Day) return false;
            Day = day;
            IsConfirmed = false;
            Array.Copy(spells, draftSpells, Capacity);
            Array.Copy(plants, draftPlants, Capacity);
            return true;
        }

        public bool TryEdit(bool spell, int slot, string id)
        {
            if (IsConfirmed || Day < 0 || slot < 0 || slot >= Capacity) return false;
            if (string.IsNullOrEmpty(id)) id = null;
            var slots = spell ? draftSpells : draftPlants;
            if (id != null)
                for (int i = 0; i < Capacity; i++)
                    if (i != slot && slots[i] == id) return false;
            slots[slot] = id;
            return true;
        }

        public bool TryConfirm(Func<string, bool> spellUnlocked, Func<string, bool> plantUnlocked)
        {
            if (IsConfirmed || Day < 0 || !Valid(draftSpells, spellUnlocked) || !Valid(draftPlants, plantUnlocked)) return false;
            Array.Copy(draftSpells, spells, Capacity);
            Array.Copy(draftPlants, plants, Capacity);
            IsConfirmed = true; // Commit every slot and the lock before publishing.
            Confirmed?.Invoke();
            return true;
        }

        private static bool Valid(string[] slots, Func<string, bool> unlocked)
        {
            if (unlocked == null) return false;
            for (int i = 0; i < Capacity; i++)
            {
                if (slots[i] == null) continue;
                if (!unlocked(slots[i])) return false;
                for (int j = 0; j < i; j++) if (slots[j] == slots[i]) return false;
            }
            return true;
        }

        public void ResetRun()
        {
            Day = -1;
            IsConfirmed = false;
            Array.Clear(spells, 0, Capacity);
            Array.Clear(plants, 0, Capacity);
            Array.Clear(draftSpells, 0, Capacity);
            Array.Clear(draftPlants, 0, Capacity);
        }
    }
}
