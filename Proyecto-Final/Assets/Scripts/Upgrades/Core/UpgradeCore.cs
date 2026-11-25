using System;
using System.Collections.Generic;

namespace MagicGarden
{
    public enum Stat { MaxHealth, Damage, AttackSpeed, MoveSpeed, Range, Area, Knockback, Quantity, PlantLimit }
    public enum Rarity { Base, Common, Rare, Legendary }
    public enum EffectKind { LateralProjectiles, ExtraPierce, TraversalDamage, AreaMultiplier, AdditionalTargets }

    [Serializable] public class StatRule
    {
        public Stat stat;
        public string displayName;
        public float weight;
        public int maxStacks;
        // Optional fixed rarity. Both fields default to "not fixed" when absent from JSON (JsonUtility leaves missing fields at default(T)).
        public bool forceRarity;
        public Rarity fixedRarity;
    }
    [Serializable] public class RarityRule
    {
        public Rarity rarity;
        public float weight;
        public float bonus;
    }
    [Serializable] public class ScalingProfile
    {
        public string capability;
        public string targetId;
        public float[] ratios;
        public float Ratio(Stat stat) => ratios != null && (int)stat >= 0 && (int)stat < ratios.Length ? ratios[(int)stat] : 0;
    }
    [Serializable] public class Cost { public int material; public int amount; }
    [Serializable] public class SpecialCostOverride
    {
        public string targetId;
        public int priorStacks;
        public Cost[] costs;
    }
    [Serializable] public class Effect { public EffectKind kind; public float value; }
    [Serializable] public class SpecialDefinition
    {
        public string id;
        public string capability;
        public string displayName;
        public string description;
        public int maxStacks;
        public string[] prerequisites;
        public Cost[] costs;
        public SpecialCostOverride[] costOverrides;
        public Effect[] effects;
    }
    [Serializable] public class Balance
    {
        public StatRule[] stats;
        public RarityRule[] rarities;
        public ScalingProfile[] profiles;
        public SpecialDefinition[] specials;
        public float lateralAngle;
        public float pierceGraceSeconds;
        public int seedsOnPlantUnlock;
    }
    public sealed class Target
    {
        public string Id, Name, Capability;
        public readonly Dictionary<Stat, float> Bases = new Dictionary<Stat, float>();
        public ScalingProfile Profile;
        public float Ratio(Stat stat) => Profile == null ? 0 : Profile.Ratio(stat);
    }
    public sealed class Offer
    {
        public Target Target;
        public StatRule Rule;
        public RarityRule Rarity;
        public float Before, After;
        public string Key => Target.Id + ":" + Rule.stat;
    }

    // State belongs to a run, not to balance assets or prefab instances.
    public sealed class RunState
    {
        private readonly Dictionary<string, float[]> bonuses = new Dictionary<string, float[]>();
        private readonly Dictionary<string, int[]> stacks = new Dictionary<string, int[]>();
        private readonly Dictionary<string, int> specials = new Dictionary<string, int>();
        private readonly Dictionary<string, float[]> effects = new Dictionary<string, float[]>();
        // Stat ids are append-only; arrays and guards derive their size from the last enum member.
        // PlantLimit is no longer offered by the default balance (the plant limit grows with the player level) but stays reserved.
        public const int StatCount = (int)Stat.PlantLimit + 1;
        public static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        // Additive stats add exactly one per stack to their base, independent of rarity bonus and profile ratio.
        public static bool Additive(Stat stat) => stat == Stat.Quantity || stat == Stat.PlantLimit;
        public int Stacks(string id, Stat stat) => !string.IsNullOrEmpty(id) && (int)stat >= 0 && (int)stat < StatCount && stacks.TryGetValue(id, out var a) ? a[(int)stat] : 0;
        public int SpecialStacks(string id, string target) => specials.TryGetValue(target + ":" + id, out var n) ? n : 0;
        public float Effect(string target, EffectKind kind) => !string.IsNullOrEmpty(target) && (int)kind >= 0 && (int)kind < 5 && effects.TryGetValue(target, out var a) ? a[(int)kind] : 0;
        public float Factor(string id, Stat stat, float ratio)
        {
            if (string.IsNullOrEmpty(id) || (int)stat < 0 || (int)stat >= StatCount) return 1;
            float bonus = bonuses.TryGetValue(id, out var a) ? a[(int)stat] : 0;
            double result = 1d + (double)bonus * ratio;
            return Finite(ratio) && ratio >= 0 ? (float)Math.Min(1000000d, Math.Max(1d, result)) : 1;
        }
        public float Value(string id, Stat stat, float basis, float ratio)
        {
            if (!Finite(basis) || basis < 0) return stat == Stat.AttackSpeed ? 0.001f : 0;
            if (Additive(stat)) return basis + Stacks(id, stat);
            double result = (double)basis * Factor(id, stat, ratio);
            return (float)Math.Min(stat == Stat.AttackSpeed ? 1000d : 100000000d, result);
        }
        public float Cooldown(string id, float basis, float ratio)
        {
            if (!Finite(basis) || basis <= 0) basis = 0.001f;
            return Math.Max(0.001f, basis / Factor(id, Stat.AttackSpeed, ratio));
        }
        public bool CanApply(Target target, StatRule rule)
        {
            return target != null && !string.IsNullOrEmpty(target.Id) && rule != null && (int)rule.stat >= 0 && (int)rule.stat < StatCount &&
                Finite(rule.weight) && rule.weight > 0 && rule.maxStacks > Stacks(target.Id, rule.stat) &&
                target.Bases.TryGetValue(rule.stat, out var basis) && Finite(basis) && basis > 0 &&
                (rule.stat != Stat.AttackSpeed || Value(target.Id, rule.stat, basis, target.Ratio(rule.stat)) < 1000) &&
                (Additive(rule.stat) || (Finite(target.Ratio(rule.stat)) && target.Ratio(rule.stat) > 0));
        }
        public float Preview(Target target, StatRule rule, RarityRule rarity)
        {
            float basis = target.Bases[rule.stat];
            float current = Value(target.Id, rule.stat, basis, target.Ratio(rule.stat));
            if (Additive(rule.stat)) return current + 1;
            float previous = bonuses.TryGetValue(target.Id, out var a) ? a[(int)rule.stat] : 0;
            double accumulated = Math.Min(1000000d, (double)previous + rarity.bonus);
            double factor = Math.Min(1000000d, 1d + accumulated * target.Ratio(rule.stat));
            return (float)Math.Min(rule.stat == Stat.AttackSpeed ? 1000d : 100000000d, basis * factor);
        }
        public bool Apply(Offer offer)
        {
            if (offer == null || !CanApply(offer.Target, offer.Rule) || offer.Rarity == null ||
                !Finite(offer.Rarity.bonus) || offer.Rarity.bonus < 0 ||
                (!Additive(offer.Rule.stat) && offer.Rarity.bonus == 0)) return false;
            string id = offer.Target.Id;
            if (!bonuses.TryGetValue(id, out var a)) bonuses.Add(id, a = new float[StatCount]);
            if (!stacks.TryGetValue(id, out var s)) stacks.Add(id, s = new int[StatCount]);
            a[(int)offer.Rule.stat] = Math.Min(1000000, a[(int)offer.Rule.stat] + offer.Rarity.bonus);
            s[(int)offer.Rule.stat]++;
            return true;
        }
        public Cost[] CostsFor(SpecialDefinition definition, Target target)
        {
            if (definition == null) return null;
            if (target != null && !string.IsNullOrEmpty(target.Id) && definition.costOverrides != null)
            {
                int priorStacks = SpecialStacks(definition.id, target.Id);
                foreach (var entry in definition.costOverrides)
                    if (entry != null && entry.targetId == target.Id && entry.priorStacks == priorStacks)
                        return entry.costs;
            }
            return definition.costs;
        }
        public bool CanBuy(SpecialDefinition definition, Target target)
        {
            if (definition == null || target == null || string.IsNullOrEmpty(target.Id) || string.IsNullOrEmpty(definition.id) ||
                definition.capability != target.Capability || definition.maxStacks <= SpecialStacks(definition.id, target.Id) ||
                definition.effects == null || definition.effects.Length == 0) return false;
            if (definition.prerequisites != null)
                foreach (string prerequisite in definition.prerequisites)
                    if (SpecialStacks(prerequisite, target.Id) == 0) return false;
            foreach (var effect in definition.effects)
                if (effect == null || !Finite(effect.value) || effect.value <= 0 || (int)effect.kind < 0 || (int)effect.kind >= 5)
                    return false;
            return true;
        }
        public bool Buy(SpecialDefinition definition, Target target)
        {
            if (!CanBuy(definition, target)) return false;
            string key = target.Id + ":" + definition.id;
            specials[key] = SpecialStacks(definition.id, target.Id) + 1;
            if (!effects.TryGetValue(target.Id, out var a)) effects.Add(target.Id, a = new float[5]);
            foreach (var effect in definition.effects)
                a[(int)effect.kind] = Math.Min(1000000, a[(int)effect.kind] + effect.value);
            return true;
        }
        public void Clear() { bonuses.Clear(); stacks.Clear(); specials.Clear(); effects.Clear(); }
    }

    public static class OfferSelector
    {
        public static List<Offer> Select(RunState state, IList<Target> owned, Balance balance, Random random, int count = 3)
        {
            var candidates = new List<Offer>();
            var seen = new HashSet<string>();
            var rarities = new List<RarityRule>();
            if (balance == null || balance.stats == null || balance.rarities == null) return candidates;
            foreach (var rarity in balance.rarities)
                if (rarity != null && RunState.Finite(rarity.weight) && rarity.weight > 0 &&
                    RunState.Finite(rarity.bonus) && rarity.bonus >= 0) rarities.Add(rarity);
            if (rarities.Count == 0) return candidates;
            bool hasPercentBonus = false;
            foreach (var rarity in rarities) if (rarity.bonus > 0) hasPercentBonus = true;
            foreach (var target in owned)
                foreach (var rule in balance.stats)
                    if (state.CanApply(target, rule) && (RunState.Additive(rule.stat) || hasPercentBonus) &&
                        (!rule.forceRarity || FixedRarity(rule, rarities) != null))
                    {
                        var offer = new Offer { Target = target, Rule = rule };
                        if (seen.Add(offer.Key)) candidates.Add(offer);
                    }
            var result = new List<Offer>();
            while (result.Count < count && candidates.Count > 0)
            {
                double total = 0;
                foreach (var c in candidates) total += c.Rule.weight;
                double roll = random.NextDouble() * total;
                int index = candidates.Count - 1;
                for (int i = 0; i < candidates.Count; i++)
                {
                    roll -= candidates[i].Rule.weight;
                    if (roll < 0) { index = i; break; }
                }
                var offer = candidates[index];
                candidates.RemoveAt(index);
                if (offer.Rule.forceRarity) offer.Rarity = FixedRarity(offer.Rule, rarities); // no roll: the rarity is data-driven
                else
                {
                    double rarityTotal = 0;
                    foreach (var rarity in rarities)
                        if (RunState.Additive(offer.Rule.stat) || rarity.bonus > 0) rarityTotal += rarity.weight;
                    roll = random.NextDouble() * rarityTotal;
                    offer.Rarity = rarities[rarities.Count - 1];
                    foreach (var rarity in rarities)
                    {
                        if (!RunState.Additive(offer.Rule.stat) && rarity.bonus == 0) continue;
                        offer.Rarity = rarity;
                        roll -= rarity.weight;
                        if (roll < 0) break;
                    }
                }
                offer.Before = state.Value(offer.Target.Id, offer.Rule.stat, offer.Target.Bases[offer.Rule.stat], offer.Target.Ratio(offer.Rule.stat));
                offer.After = state.Preview(offer.Target, offer.Rule, offer.Rarity);
                result.Add(offer);
            }
            return result;
        }
        // The valid rarity a rule is pinned to, or null when the rule is not fixed or its rarity cannot produce a usable offer.
        private static RarityRule FixedRarity(StatRule rule, List<RarityRule> rarities)
        {
            if (!rule.forceRarity) return null;
            foreach (var rarity in rarities)
                if (rarity.rarity == rule.fixedRarity && (RunState.Additive(rule.stat) || rarity.bonus > 0)) return rarity;
            return null;
        }
    }

    public static class CostPlanner
    {
        public static bool Aggregate(IList<Cost> costs, out Dictionary<int, int> totals)
        {
            totals = new Dictionary<int, int>();
            if (costs == null || costs.Count == 0) return false;
            foreach (var cost in costs)
            {
                if (cost == null || cost.material <= 0 || cost.amount <= 0) return false;
                totals.TryGetValue(cost.material, out int previous);
                long total = (long)previous + cost.amount;
                if (total > int.MaxValue) return false;
                totals[cost.material] = (int)total;
            }
            return true;
        }
        public static bool Affordable(Dictionary<int, int> totals, Func<int, int> available)
        {
            foreach (var pair in totals) if (available(pair.Key) < pair.Value) return false;
            return true;
        }
    }

    public static class ExperienceCurve
    {
        // Each index is the cumulative XP to enter level index + 1. No extrapolation.
        public static int Next(IList<int> thresholds, int level)
        {
            if (thresholds == null || level < 1 || level >= thresholds.Count) return 0;
            for (int i = 1; i <= level; i++)
                if (thresholds[i] <= thresholds[i - 1] || thresholds[0] != 0) return 0;
            return thresholds[level];
        }
        public static int Add(int total, int amount) => (int)Math.Min(int.MaxValue, (long)Math.Max(0, total) + Math.Max(0, amount));
        public static int Advance(IList<int> thresholds, int level, int total, Action<int> entered)
        {
            int next;
            while ((next = Next(thresholds, level)) > 0 && total >= next)
            {
                level++;
                entered?.Invoke(level);
            }
            return level;
        }
    }
}
