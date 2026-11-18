using System;
using System.Collections.Generic;

/// <summary>
/// Player actions the progressive tutorial lock can gate. Serialized as an int mask on
/// <see cref="TutorialStep.unlockedActions"/>: append only, never renumber existing values.
/// </summary>
[Flags]
public enum TutorialAction
{
    None = 0,
    Move = 1 << 0,
    HouseDoor = 1 << 1,
    OpenDailyLoadout = 1 << 2,
    Dig = 1 << 3,
    Plant = 1 << 4,
    RemovePlant = 1 << 5,
    Harvest = 1 << 6,
    CycleTool = 1 << 7,
    SelectSeed = 1 << 8,
    Teleport = 1 << 9,
    Inventory = 1 << 10,
    RitualAltar = 1 << 11,
    CastSpell = 1 << 12,
    CauldronShop = 1 << 13
}

/// <summary>
/// Pure rules of the progressive action lock (no scene or manager state), so they can be tested
/// and reused by authoring validation. Allowed actions at step N are the union of the unlocks of
/// steps 0..N, so what was learned stays enabled in every later instruction.
/// </summary>
public static class TutorialActionLock
{
    public static TutorialAction Unlocked(IList<TutorialStep> steps, int stepIndex)
    {
        var unlocked = TutorialAction.None;
        if (steps == null) return unlocked;
        int last = Math.Min(stepIndex, steps.Count - 1);
        for (int i = 0; i <= last; i++)
            if (steps[i] != null) unlocked |= steps[i].unlockedActions;
        return unlocked;
    }

    /// <summary>True when every flag of <paramref name="action"/> is unlocked at the step index.</summary>
    public static bool IsAllowed(IList<TutorialStep> steps, int stepIndex, TutorialAction action)
    {
        return (Unlocked(steps, stepIndex) & action) == action;
    }

    /// <summary>
    /// Actions a player needs to produce the objective's event. Every returned mask must have at
    /// least one unlocked flag (alternatives inside a mask, all masks required). Empty means the
    /// objective is passive or Continue-only and needs no unlocked action.
    /// </summary>
    public static TutorialAction[] Requirement(TutorialObjectiveType objective)
    {
        switch (objective)
        {
            case TutorialObjectiveType.Move:
            case TutorialObjectiveType.ExploreHouse:
            case TutorialObjectiveType.CraftingProximity:
            case TutorialObjectiveType.RestorationProximity:
            case TutorialObjectiveType.RitualAltarProximity:
            case TutorialObjectiveType.DailyLoadoutProximity:
                return new[] { TutorialAction.Move };
            case TutorialObjectiveType.Dig:
                return new[] { TutorialAction.Dig };
            case TutorialObjectiveType.Plant:
            case TutorialObjectiveType.PlantProduction:
            case TutorialObjectiveType.PlantDefensive:
            case TutorialObjectiveType.PlantHybrid:
                // Planting also needs the planting ability, reached by a seed slot or by cycling tools.
                return new[] { TutorialAction.Plant, TutorialAction.SelectSeed | TutorialAction.CycleTool };
            case TutorialObjectiveType.Harvest:
                return new[] { TutorialAction.Harvest, TutorialAction.CycleTool };
            case TutorialObjectiveType.RemovePlant:
                // The remove tool has no hotkey: it is reached by cycling tools.
                return new[] { TutorialAction.RemovePlant, TutorialAction.CycleTool };
            case TutorialObjectiveType.AbilityChanged:
                return new[] { TutorialAction.SelectSeed | TutorialAction.CycleTool };
            case TutorialObjectiveType.CastSpell:
            case TutorialObjectiveType.DefeatEnemy:
                return new[] { TutorialAction.CastSpell };
            case TutorialObjectiveType.TeleportSpell:
                return new[] { TutorialAction.Teleport };
            case TutorialObjectiveType.EnterHouse:
            case TutorialObjectiveType.ExitHouse:
                return new[] { TutorialAction.HouseDoor };
            case TutorialObjectiveType.OpenInventory:
                return new[] { TutorialAction.Inventory };
            case TutorialObjectiveType.OpenCrafting:
            case TutorialObjectiveType.OpenCauldronShop:
                return new[] { TutorialAction.CauldronShop };
            case TutorialObjectiveType.OpenRestoration:
            case TutorialObjectiveType.OpenDailyLoadout:
            case TutorialObjectiveType.ConfirmDailyLoadout:
                return new[] { TutorialAction.OpenDailyLoadout };
            case TutorialObjectiveType.UseRitualAltar:
            case TutorialObjectiveType.StartNight:
                return new[] { TutorialAction.RitualAltar };
            default:
                return Array.Empty<TutorialAction>();
        }
    }

    /// <summary>
    /// Whether the actions unlocked at this step are enough to finish its objective or conditions.
    /// Continue/Wait-only steps always are.
    /// </summary>
    public static bool IsCompletable(TutorialStep step, TutorialAction unlocked)
    {
        if (step == null) return true;
        if (!step.useConfiguredCompletion) return Satisfies(Requirement(step.objectiveType), unlocked);

        bool any = false, all = true;
        int counted = 0;
        if (step.conditions != null)
        {
            foreach (var condition in step.conditions)
            {
                if (condition == null) continue;
                bool satisfied = Satisfies(Requirement(condition.objective), unlocked);
                counted++;
                any |= satisfied;
                all &= satisfied;
            }
        }
        if (counted == 0) return true;
        return step.conditionCombination == TutorialConditionCombination.Any ? any : all;
    }

    /// <summary>One message per step whose objective could not be completed with the actions unlocked so far.</summary>
    public static List<string> FindSoftLocks(IList<TutorialStep> steps)
    {
        var problems = new List<string>();
        if (steps == null) return problems;
        for (int i = 0; i < steps.Count; i++)
        {
            var step = steps[i];
            if (step == null) continue;
            var unlocked = Unlocked(steps, i);
            if (IsCompletable(step, unlocked)) continue;
            problems.Add("Step " + (i + 1) + " '" + step.name + "' (" + step.objectiveType +
                ") needs an action that is still locked here; unlocked so far: " + unlocked + ".");
        }
        return problems;
    }

    private static bool Satisfies(TutorialAction[] requirement, TutorialAction unlocked)
    {
        foreach (var alternatives in requirement)
            if (alternatives != TutorialAction.None && (unlocked & alternatives) == TutorialAction.None) return false;
        return true;
    }
}
