using System;
using System.Collections.Generic;

namespace MagicGarden.Loadout
{
    /// <summary>An explicit empty position or an item identity, never a storage index.</summary>
    public readonly struct EquipmentAssignment<T>
    {
        public bool HasValue { get; }
        public T Identity { get; }

        private EquipmentAssignment(T identity)
        {
            HasValue = true;
            Identity = identity;
        }

        public static EquipmentAssignment<T> For(T identity)
        {
            if (identity is null) throw new ArgumentNullException(nameof(identity));
            return new EquipmentAssignment<T>(identity);
        }
    }

    /// <summary>
    /// Equipment state only. Callers supply stable identities, validated unlocks,
    /// and a monotonic day token scoped to this run. Initialization never equips content.
    /// No inventory, selection, cooldown, gameplay or Unity lifecycle is owned here.
    /// </summary>
    public sealed class DailyLoadoutState<TPlant, TSpell>
    {
        public const int SlotsPerCategory = 3;
        private readonly EquipmentAssignment<TPlant>[] committedPlants = new EquipmentAssignment<TPlant>[SlotsPerCategory];
        private readonly EquipmentAssignment<TSpell>[] committedSpells = new EquipmentAssignment<TSpell>[SlotsPerCategory];
        private readonly EquipmentAssignment<TPlant>[] draftPlants = new EquipmentAssignment<TPlant>[SlotsPerCategory];
        private readonly EquipmentAssignment<TSpell>[] draftSpells = new EquipmentAssignment<TSpell>[SlotsPerCategory];

        public bool IsInitialized { get; private set; }
        public bool IsDay { get; private set; }
        public bool HasConfirmedToday { get; private set; }
        public long? CurrentDayToken { get; private set; }

        // Assignments are value snapshots; internal arrays are never exposed.
        public EquipmentAssignment<TPlant> GetCommittedPlant(int index) => committedPlants[index];
        public EquipmentAssignment<TSpell> GetCommittedSpell(int index) => committedSpells[index];
        public EquipmentAssignment<TPlant> GetDraftPlant(int index) => draftPlants[index];
        public EquipmentAssignment<TSpell> GetDraftSpell(int index) => draftSpells[index];

        /// <summary>
        /// Initialize six empty positions once per run. Unlocks are catalog membership,
        /// never default assignments. Does not open a day or spend its confirmation.
        /// </summary>
        public bool TryInitialize()
        {
            if (IsInitialized) return false;
            IsInitialized = true;
            DiscardDraft();
            return true;
        }

        /// <summary>
        /// Tokens must increase for each new day within a run, even if the game's
        /// displayed day repeats. Repeating the active day is idempotent; stale
        /// notifications cannot reopen a closed day or renew its confirmation.
        /// ResetRun is the only operation that permits token reuse in a new run.
        /// </summary>
        public bool TryBeginDay(long dayToken)
        {
            if (CurrentDayToken.HasValue)
            {
                if (dayToken == CurrentDayToken.Value) return IsDay;
                if (dayToken < CurrentDayToken.Value) return false;
            }

            CurrentDayToken = dayToken;
            IsDay = true;
            HasConfirmedToday = false;
            DiscardDraft();
            return true;
        }

        public void BeginNight()
        {
            IsDay = false;
            DiscardDraft();
        }

        public bool TrySetDraftPlant(int index, EquipmentAssignment<TPlant> assignment)
            => IsInitialized && IsDay && !HasConfirmedToday && TryAssign(draftPlants, index, assignment);

        public bool TrySetDraftSpell(int index, EquipmentAssignment<TSpell> assignment)
            => IsInitialized && IsDay && !HasConfirmedToday && TryAssign(draftSpells, index, assignment);

        /// <summary>
        /// Commits all six positions, or changes nothing on rejection. Draft setters
        /// already enforce capacity and uniqueness. Even an unchanged manual
        /// confirmation uses the day's single allowance.
        /// </summary>
        public bool TryConfirm()
        {
            if (!IsInitialized || !IsDay || HasConfirmedToday) return false;
            Array.Copy(draftPlants, committedPlants, SlotsPerCategory);
            Array.Copy(draftSpells, committedSpells, SlotsPerCategory);
            HasConfirmedToday = true;
            return true;
        }

        public void DiscardDraft()
        {
            Array.Copy(committedPlants, draftPlants, SlotsPerCategory);
            Array.Copy(committedSpells, draftSpells, SlotsPerCategory);
        }

        public void ResetRun()
        {
            Array.Clear(committedPlants, 0, SlotsPerCategory);
            Array.Clear(committedSpells, 0, SlotsPerCategory);
            DiscardDraft();
            IsInitialized = false;
            IsDay = false;
            HasConfirmedToday = false;
            CurrentDayToken = null;
        }

        private static bool TryAssign<T>(EquipmentAssignment<T>[] assignments, int index, EquipmentAssignment<T> item)
        {
            if (index < 0 || index >= SlotsPerCategory) return false;
            if (item.HasValue)
            {
                for (int i = 0; i < SlotsPerCategory; i++)
                    if (i != index && assignments[i].HasValue &&
                        EqualityComparer<T>.Default.Equals(assignments[i].Identity, item.Identity)) return false;
            }
            assignments[index] = item;
            return true;
        }

    }
}
