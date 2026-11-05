using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class TutorialNarrativeTests
{
    const string ManagerPath = "Assets/Prefabs/Managers/TutorialManager.prefab";
    const string SequencePath = "Assets/Scriptables/Tutorial/SummerlimbsTutorial.asset";

    [TestCase("b5b4175de3cb5684e8d3e96c0cd4112a", "Awakening", "Welcome")]
    [TestCase("a31fc7518f137254b897040382fa3f24", "ForestMission", "Welcome")]
    [TestCase("d4710db85cc460f4599be7dbc410419f", "Movement", "Move")]
    [TestCase("cb7dacc489cc747499b90491e8aeff6e", "EnterHouse", "EnterHouse")]
    [TestCase("8e1e6aeb5954a88fda4ebc8c7222e39e", "PrepareDailyLoadout", "PrepareDailyLoadout")]
    [TestCase("70f3c369687ff3c49a0e32972ed1102f", "OpenDailyLoadout", "OpenRestoration")]
    [TestCase("ef553f94880ac5045877bec9009af927", "ConfirmDailyLoadout", "CastSpell")]
    [TestCase("84611ffc204d3e0489d10fbf745a1469", "ExitHouse", "LeaveHouse")]
    [TestCase("16ffda535a901394d8ffed95633194d3", "Dig", "Dig")]
    [TestCase("35354b9ca811854458056aa6d7ad6a5d", "Plant", "DefensivePlant")]
    [TestCase("32923ab09714fac4797e4fee3fcfd529", "PrepareForNight", "Teleport")]
    [TestCase("a546d1b43bc895247b7588f27571b808", "NightDefense", "UseRitualAltar")]
    [TestCase("2fb5c407efd677244b021bb8ebf88593", "ResourcesAndProgression", "Harvest")]
    [TestCase("d3178642d5f6f524caa68f9a44276f34", "TutorialComplete", "TutorialComplete")]
    public void LessonUsesSemanticNameWithoutChangingStableIdentity(string guid, string name, string legacyId)
    {
        var path = AssetDatabase.GUIDToAssetPath(guid);
        Assert.That(path, Is.Not.Empty, "Preserve the original asset GUID.");
        Assert.That(System.IO.Path.GetFileNameWithoutExtension(path), Is.EqualTo(name));
        var step = AssetDatabase.LoadMainAssetAtPath(path);
        Assert.That(step, Is.Not.Null);
        Assert.That(step.name, Is.EqualTo(name));
        Assert.That(Field(step, "stepID"), Is.EqualTo(legacyId));
    }

    static Type Production(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Single(a => a.GetName().Name == "Assembly-CSharp").GetType(name, true);
    static object Field(object target, string name) => target.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(target);
    static UnityEngine.Object[] Steps(object target, string field) => ((IList)Field(target, field))
        .Cast<UnityEngine.Object>().ToArray();
    static Component Manager()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ManagerPath);
        Assert.That(prefab, Is.Not.Null, ManagerPath);
        var manager = prefab.GetComponent(Production("TutorialManager"));
        Assert.That(manager, Is.Not.Null, "Preserve the installed TutorialManager component.");
        return manager;
    }
    // Fall back only for independent diagnostics before the sequence is installed.
    // The installation test below requires the explicit sequence, not this fallback.
    static UnityEngine.Object[] Playback()
    {
        var manager = Manager();
        var sequence = (UnityEngine.Object)Field(manager, "sequence");
        var steps = sequence != null ? Steps(sequence, "steps") : Steps(manager, "tutorialSteps")
            .OrderBy(s => (int)Field(s, "stepOrder")).ToArray();
        Assert.That(steps, Is.Not.Empty);
        Assert.That(steps.All(s => s != null), Is.True, "Playback cannot contain missing step references.");
        return steps;
    }
    static string Copy(UnityEngine.Object step) => (string)step.GetType().GetProperty("DisplayText").GetValue(step);

    [Test]
    public void InstalledSequenceIsAssignedAndHasLinearNonNullPlayback()
    {
        var manager = Manager();
        var sequence = AssetDatabase.LoadAssetAtPath(SequencePath, Production("TutorialSequence"));
        Assert.That(sequence, Is.Not.Null, "Install the designer-editable Summerlimbs sequence.");
        Assert.That(Field(manager, "sequence"), Is.EqualTo(sequence), "Assign it on the preserved manager prefab.");
        var installed = Steps(sequence, "steps");
        Assert.That(installed, Is.Not.Empty);
        Assert.That(installed.All(s => s != null), Is.True, "The authored list is the linear playback; reuse is allowed.");
        CollectionAssert.AreEqual(installed, Playback());
    }

    // Read effective completion, not the legacy objective metadata ignored by configured steps.
    static KeyValuePair<string, int>[] Rows(UnityEngine.Object step)
    {
        if (!(bool)Field(step, "useConfiguredCompletion"))
            return new[] { new KeyValuePair<string, int>(Field(step, "objectiveType").ToString(), (int)Field(step, "requiredCount")) };
        return ((IList)Field(step, "conditions")).Cast<object>().Select(c =>
            new KeyValuePair<string, int>(Field(c, "objective").ToString(), (int)Field(c, "requiredCount"))).ToArray();
    }
    static bool EveryRoute(UnityEngine.Object step, Func<KeyValuePair<string, int>, bool> predicate)
    {
        var rows = Rows(step);
        if (rows.Length == 0) return false;
        return (bool)Field(step, "useConfiguredCompletion") && Field(step, "conditionCombination").ToString() == "Any"
            ? rows.All(predicate) : rows.Any(predicate);
    }
    static int RequiredIndex(UnityEngine.Object[] steps, string objective)
    {
        int index = Array.FindIndex(steps, s => EveryRoute(s, r => r.Key == objective && r.Value > 0));
        Assert.That(index, Is.GreaterThanOrEqualTo(0), objective + " must be a real unavoidable installed completion, not copy or a bypassable Any row.");
        return index;
    }

    [Test]
    public void InstalledFlowRequiresConfirmedHouseLoadoutBeforeExitAndPractice()
    {
        var sequence = Field(Manager(), "sequence");
        Assert.That(sequence, Is.Not.Null);
        var steps = Playback();
        Assert.That(Field(sequence, "requireUsableLoadout"), Is.EqualTo(true), "Reject unusable tutorial drafts before consuming daily confirmation.");
        Assert.That(Field(sequence, "useExplicitNightPermission"), Is.EqualTo(true), "Use semantic night admission, not legacy stepOrder.");
        int enter = RequiredIndex(steps, "EnterHouse");
        int open = RequiredIndex(steps, "OpenDailyLoadout");
        int confirm = RequiredIndex(steps, "ConfirmDailyLoadout");
        int exit = RequiredIndex(steps, "ExitHouse");
        Assert.That(enter, Is.LessThan(open), "Enter before opening the house loadout.");
        Assert.That(open, Is.LessThan(confirm), "Opening is not confirmation.");
        Assert.That(confirm, Is.LessThan(exit), "Confirm successfully before leaving.");
        for (int i = 0; i < steps.Length; i++)
        {
            var rows = Rows(steps[i]);
            if (rows.Any(r => new[] { "Dig", "Plant", "PlantProduction", "PlantDefensive", "CastSpell", "ExitHouse" }.Contains(r.Key)))
                Assert.That(i, Is.GreaterThan(confirm), steps[i].name + ": practice/exit cannot precede usable confirmation, including configured alternatives.");
            if (rows.Any(r => r.Key == "OpenDailyLoadout"))
                Assert.That(i, Is.GreaterThan(enter), steps[i].name + ": enter before any loadout opening route.");
            if (rows.Any(r => r.Key == "ConfirmDailyLoadout"))
                Assert.That(i, Is.GreaterThan(open), steps[i].name + ": open before any confirmation route.");
            if (rows.Any(r => r.Key == "ExitHouse"))
                Assert.That(Field(steps[i], "isGatedStep"), Is.EqualTo(false), steps[i].name + ": a gated house door blocks exit.");
        }
    }

    [Test]
    public void EveryInstalledDialogueStartsWithBoldUppercaseSpeaker()
    {
        foreach (var step in Playback().Where(s => !string.IsNullOrWhiteSpace(Copy(s))))
            Assert.That(Copy(step), Does.Match(@"^<b>(FOREST|SUMMERLIMBS):</b>"), step.name + ": label must lead every nonempty DisplayText.");
    }

    static object Member(object value, string name) => value.GetType().GetProperty(name)?.GetValue(value)
        ?? value.GetType().GetField(name).GetValue(value);

    [Test]
    public void MovementAndAllNamedSpritesUseInstalledTmpRegistry()
    {
        var panel = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/Canvas/Game/Panel Tutorial.prefab");
        var textType = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("TMPro.TextMeshProUGUI")).First(t => t != null);
        var label = panel.GetComponentsInChildren(textType, true).Single(c => c.name == "InstructionText");
        var spriteAsset = Member(label, "spriteAsset");
        Assert.That(spriteAsset, Is.Not.Null, "Installed InstructionText must bind its actual TMP sprite registry.");
        var names = ((IEnumerable)Member(spriteAsset, "spriteCharacterTable")).Cast<object>()
            .Select(c => (string)Member(c, "name")).ToArray();
        var steps = Playback();
        var movement = steps.Where(s => Rows(s).Any(r => r.Key == "Move")).ToArray();
        Assert.That(movement, Is.Not.Empty, "Teach actual movement.");
        foreach (var step in movement)
        {
            Assert.That(Copy(step), Does.Contain("<sprite name=\"ArrowKeys\">"), step.name + ": teach arrows through their registered icon.");
            Assert.That(Copy(step), Does.Contain("<sprite name=\"WASDKeys\">"), step.name + ": retain the verified alternative movement icon.");
        }
        foreach (var step in steps)
        {
            Assert.That(Copy(step), Does.Not.Match(@"(?i)arrow\s+keys"), step.name + ": use the registered arrow sprite, not literal keys.");
            foreach (Match match in Regex.Matches(Copy(step), "<sprite\\s+name=\"([^\"]+)\"[^>]*>"))
                Assert.That(names, Does.Contain(match.Groups[1].Value), step.name + ": unknown installed TMP sprite " + match.Value);
        }
    }

    [Test]
    public void PracticeAcceptsOnePlantOfEitherAvailableCategoryAndOneSpell()
    {
        var steps = Playback();
        RequiredIndex(steps, "Dig");
        RequiredIndex(steps, "Plant");
        RequiredIndex(steps, "CastSpell");
        foreach (string choice in new[] { "PlantProduction", "PlantDefensive" })
        {
            int plants = 0, digs = 0, spells = 0;
            foreach (var step in steps)
            {
                var rows = Rows(step);
                bool any = (bool)Field(step, "useConfiguredCompletion") && Field(step, "conditionCombination").ToString() == "Any";
                // A non-harvestable legal choice cannot emit readiness/harvest. Other lessons remain possible.
                Func<KeyValuePair<string, int>, bool> possible = r => r.Key != "Harvest" && r.Key != "FirstPlantReady" &&
                    (!(r.Key == "PlantProduction" || r.Key == "PlantDefensive") || r.Key == choice);
                Assert.That(rows.Length == 0 || (any ? rows.Any(possible) : rows.All(possible)), Is.True,
                    step.name + ": mandatory completion excludes a single non-harvestable " + choice + " choice.");
                Func<string[], int> cost = objectives =>
                {
                    var costs = rows.Select(r => objectives.Contains(r.Key) ? r.Value : 0).ToArray();
                    return costs.Length == 0 ? 0 : any ? costs.Min() : costs.Max();
                };
                plants += cost(new[] { "Plant", "PlantProduction", "PlantDefensive" });
                digs += cost(new[] { "Dig" });
                spells += cost(new[] { "CastSpell" });
            }
            Assert.That(plants, Is.EqualTo(1), choice + ": teach one chosen plant, not multiple mandatory plantings.");
            Assert.That(digs, Is.EqualTo(1), choice + ": teach one generic dig.");
            Assert.That(spells, Is.EqualTo(1), choice + ": one real spell practice is sufficient.");
        }
    }

    [Test]
    public void NightAdmissionWaitsForLoadoutAndPlantPracticeThenUsesRealAltar()
    {
        var steps = Playback();
        int confirm = RequiredIndex(steps, "ConfirmDailyLoadout");
        int plant = RequiredIndex(steps, "Plant");
        int altar = Array.FindIndex(steps, s => (bool)Field(s, "allowNightTransition") &&
            EveryRoute(s, r => r.Key == "UseRitualAltar" && r.Value > 0));
        Assert.That(altar, Is.GreaterThan(Math.Max(confirm, plant)), "Enable permission on real ritual use after confirmation and planting.");
        for (int i = 0; i < altar; i++)
            Assert.That(Field(steps[i], "allowNightTransition"), Is.EqualTo(false), steps[i].name + ": no night permission during house/loadout or initial preparation.");
    }

    [Test]
    public void OpeningIntroducesSummerlimbsAndTheForestInsteadOfADruid()
    {
        var opening = string.Join("\n", Playback().TakeWhile(s => Rows(s).All(r =>
            new[] { "Wait", "None", "Welcome" }.Contains(r.Key))).Select(Copy));
        Assert.That(opening, Does.Match("(?i)summerlimbs"), "Introduce the named protagonist at the beginning.");
        Assert.That(opening, Does.Match("(?i)forest"), "The forest is the mentor.");
        Assert.That(opening, Does.Not.Match("(?i)\\bdruid\\b"), "Summerlimbs is a newly created ENT, not the former druid.");
    }

    [Test]
    public void EndingAddressesSummerlimbsAndContinuesTheDayNightResponsibility()
    {
        var ending = Copy(Playback().Last());
        Assert.That(ending, Does.Match("(?i)summerlimbs"));
        Assert.That(ending, Does.Match("(?i)day|garden|grow|cultivat|prepar"));
        Assert.That(ending, Does.Match("(?i)night|defen|protect"));
        Assert.That(ending, Does.Not.Match("(?i)\\bdruid\\b"));
    }

    [Test]
    public void InstalledCompletionUsesGameplaySupportedConditions()
    {
        // Event objectives have subscribed gameplay emitters; FirstPlantReady instead
        // reconciles actual PlantManager availability locally on the initialized step.
        var supported = new[] { "Move", "Dig", "Plant", "Harvest", "CastSpell", "EnterHouse",
            "UseRitualAltar", "StartNight", "DefeatEnemy", "SurviveNight", "OpenInventory",
            "OpenCrafting", "OpenRestoration", "PlantProduction", "PlantDefensive",
            "CraftingProximity", "RestorationProximity", "RitualAltarProximity",
            "SeedTooltipDisplayed", "TeleportSpell", "AbilityChanged", "OpenDailyLoadout", "DailyLoadoutProximity",
            "FirstPlantReady", "ConfirmDailyLoadout", "ExitHouse" };
        foreach (var step in Playback())
        {
            var configured = (bool)Field(step, "useConfiguredCompletion");
            if (!configured)
            {
                var objective = Field(step, "objectiveType").ToString();
                Assert.That(objective == "Wait" || supported.Contains(objective), Is.True,
                    step.name + ": " + objective + " needs a real gameplay completion source, not an assumed timer.");
                Assert.That((int)Field(step, "requiredCount"), Is.GreaterThan(0), step.name);
                if (objective == "FirstPlantReady")
                    Assert.That((int)Field(step, "requiredCount"), Is.EqualTo(1), step.name);
                continue;
            }
            foreach (var condition in (IList)Field(step, "conditions"))
            {
                Assert.That(condition, Is.Not.Null, step.name);
                Assert.That(supported, Does.Contain(Field(condition, "objective").ToString()), step.name);
                Assert.That((int)Field(condition, "requiredCount"), Is.GreaterThan(0), step.name);
                if (Field(condition, "objective").ToString() == "FirstPlantReady")
                    Assert.That((int)Field(condition, "requiredCount"), Is.EqualTo(1), step.name);
            }
            if (Field(step, "waitMode").ToString() == "Continue")
                Assert.That(Field(step, "objectiveType").ToString(), Is.EqualTo("Wait"), step.name);
        }
    }
}
