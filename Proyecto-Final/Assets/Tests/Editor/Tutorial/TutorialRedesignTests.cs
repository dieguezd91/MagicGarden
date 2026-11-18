using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

// Redesigned Summerlimbs tutorial (S1, S3, S4, S5): concise 14-step order, merged steps, new practice objectives
// (remove plant, teleport + inventory, cauldron shop, harvest) and their completion hooks from the real gameplay paths.
// Production types are reached by reflection like the other tutorial fixtures (Assembly-CSharp).
public class TutorialRedesignTests
{
    const string ManagerPath = "Assets/Prefabs/Managers/TutorialManager.prefab";

    static readonly string[] Order =
    {
        "Awakening", "Movement", "EnterHouse", "OpenDailyLoadout", "ConfirmDailyLoadout", "ExitHouse", "Dig", "Plant",
        "RemovePlant", "TeleportAndInventory", "NightDefense", "CauldronUpgradeShop", "HarvestResources", "TutorialComplete",
    };

    static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "Assembly-CSharp").GetType(name, true);
    static FieldInfo Fi(object o, string name) => o.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
    static object Get(object o, string name) => Fi(o, name).GetValue(o);
    static void Set(object o, string name, object value) => Fi(o, name).SetValue(o, value);
    static object Call(object o, string name, params object[] args) =>
        o.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(o, args);
    static object E(string type, string value) => Enum.Parse(T(type), value);
    static void Fire(string method) => T("TutorialEvents").GetMethod(method).Invoke(null, null);
    static object Singleton(string type) => T(type).GetProperty("Instance").GetValue(null);
    static void SetSingleton(string type, object value) => T(type).GetProperty("Instance").GetSetMethod(true).Invoke(null, new[] { value });

    // Counts how many times a static event of the given production type fires while the action runs.
    static int Count(string type, string evt, Action act)
    {
        int count = 0;
        Action handler = () => count++;
        var info = T(type).GetEvent(evt);
        info.AddEventHandler(null, handler);
        try { act(); } finally { info.RemoveEventHandler(null, handler); }
        return count;
    }

    // ---- Installed data -------------------------------------------------------------------------

    static object InstalledSequence()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ManagerPath);
        Assert.That(prefab, Is.Not.Null, ManagerPath);
        var sequence = Get(prefab.GetComponent(T("TutorialManager")), "sequence");
        Assert.That(sequence, Is.Not.Null, "The manager prefab must reference the Summerlimbs sequence.");
        return sequence;
    }
    static UnityEngine.Object[] Installed()
    {
        var steps = ((IList)Get(InstalledSequence(), "steps")).Cast<UnityEngine.Object>().ToArray();
        Assert.That(steps.All(s => s != null), Is.True);
        return steps;
    }
    static int IndexOf(UnityEngine.Object[] steps, string name)
    {
        int index = Array.FindIndex(steps, s => s.name == name);
        Assert.That(index, Is.GreaterThanOrEqualTo(0), name + " must be part of the installed sequence.");
        return index;
    }
    // Effective completion rows: the configured conditions, or the single legacy objective.
    static string[] Objectives(object step)
    {
        if ((bool)Get(step, "useConfiguredCompletion"))
            return ((IList)Get(step, "conditions")).Cast<object>().Select(c => Get(c, "objective").ToString()).ToArray();
        return new[] { Get(step, "objectiveType").ToString() };
    }
    static string Raw(UnityEngine.Object step) => (string)step.GetType().GetProperty("DisplayText").GetValue(step);
    static string Visible(UnityEngine.Object step) => Regex.Replace(Raw(step), "<[^>]+>", "");
    static bool Flag(object step, string name) => (bool)Get(step, name);

    [Test]
    public void InstalledPlaybackIsTheRedesignedFourteenStepOrder()
    {
        var sequence = InstalledSequence();
        CollectionAssert.AreEqual(Order, Installed().Select(s => s.name).ToArray());
        Assert.That(Get(sequence, "lockActionsUntilTaught"), Is.EqualTo(true));
        Assert.That(Get(sequence, "requireUsableLoadout"), Is.EqualTo(true));
        Assert.That(Get(sequence, "useExplicitNightPermission"), Is.EqualTo(true));
    }

    [Test]
    public void MergedAwayStepsAreNoLongerPlayed()
    {
        var names = Installed().Select(s => s.name).ToArray();
        foreach (var merged in new[] { "ForestMission", "PrepareDailyLoadout", "PrepareForNight", "ResourcesAndProgression" })
            Assert.That(names, Does.Not.Contain(merged), merged + " was merged into another step.");
    }

    [Test]
    public void LoreIsOneShortBoxAtTheStartOnly()
    {
        var steps = Installed();
        var lore = steps[0];
        Assert.That(Get(lore, "objectiveType").ToString(), Is.EqualTo("Wait"), "The lore box is a Continue step.");
        Assert.That(Visible(lore).Length, Is.LessThanOrEqualTo(260));
        Assert.That(Visible(lore), Does.Match("(?i)summerlimbs").And.Match("(?i)forest").And.Match("(?i)ancestral tree"));
        foreach (var step in steps.Skip(1))
            Assert.That(Visible(step), Does.Not.Match("(?i)infernal|I am the forest|made you|created you"), step.name + ": no lore beyond the first box.");
    }

    [Test]
    public void OpeningTheLoadoutTeachesTheRulesInTheSameStep()
    {
        var steps = Installed();
        int open = IndexOf(steps, "OpenDailyLoadout");
        Assert.That(open, Is.EqualTo(IndexOf(steps, "EnterHouse") + 1), "No separate explanation box between entering and opening.");
        Assert.That(open, Is.EqualTo(IndexOf(steps, "ConfirmDailyLoadout") - 1));
        CollectionAssert.AreEqual(new[] { "OpenDailyLoadout" }, Objectives(steps[open]));
        var text = Raw(steps[open]);
        Assert.That(text, Does.Contain("slots 1-3").And.Contain("slots 4-6").And.Contain("Confirm").And.Contain("<sprite name=\"FKey\">"));
        Assert.That(text, Does.Match("(?i)at least one of each"), "Keep the tutorial rule: one plant and one spell.");
    }

    [Test]
    public void NightStepMergesAltarSpellAndSurvival()
    {
        var steps = Installed();
        var night = steps[IndexOf(steps, "NightDefense")];
        Assert.That(Flag(night, "useConfiguredCompletion"), Is.True);
        Assert.That(Get(night, "conditionCombination").ToString(), Is.EqualTo("All"));
        CollectionAssert.AreEquivalent(new[] { "UseRitualAltar", "CastSpell", "SurviveNight" }, Objectives(night));
        Assert.That(Raw(night), Does.Contain("4-6").And.Contain("<sprite name=\"FKey\">").And.Contain("<sprite name=\"MouseClick\">"));
    }

    [Test]
    public void TeleportAndInventoryAreOneStepThatNeedsBoth()
    {
        var steps = Installed();
        var step = steps[IndexOf(steps, "TeleportAndInventory")];
        Assert.That(Flag(step, "useConfiguredCompletion"), Is.True);
        Assert.That(Get(step, "conditionCombination").ToString(), Is.EqualTo("All"));
        CollectionAssert.AreEquivalent(new[] { "TeleportSpell", "OpenInventory" }, Objectives(step));
        Assert.That(Raw(step), Does.Contain("<sprite name=\"SpaceKey\">").And.Contain("<sprite name=\"TABKey\">").And.Contain("<sprite name=\"IKey\">"));
    }

    [Test]
    public void NewPracticeStepsFollowTheLessonsTheyNeed()
    {
        var steps = Installed();
        int plant = IndexOf(steps, "Plant"), remove = IndexOf(steps, "RemovePlant"), teleport = IndexOf(steps, "TeleportAndInventory");
        int night = IndexOf(steps, "NightDefense"), cauldron = IndexOf(steps, "CauldronUpgradeShop"), harvest = IndexOf(steps, "HarvestResources");
        Assert.That(remove, Is.GreaterThan(plant), "There must be a plant to remove first.");
        Assert.That(teleport, Is.GreaterThan(remove));
        Assert.That(night, Is.GreaterThan(teleport));
        Assert.That(cauldron, Is.GreaterThan(night), "The shop opens by day, after the first night.");
        Assert.That(harvest, Is.EqualTo(steps.Length - 2), "Harvest is the last practical step before the recap.");
        CollectionAssert.AreEqual(new[] { "RemovePlant" }, Objectives(steps[remove]));
        CollectionAssert.AreEqual(new[] { "OpenCauldronShop" }, Objectives(steps[cauldron]));
        CollectionAssert.AreEqual(new[] { "Harvest" }, Objectives(steps[harvest]));
        var recap = steps[steps.Length - 1];
        Assert.That(Get(recap, "objectiveType").ToString(), Is.EqualTo("Wait"));
        Assert.That(Raw(recap), Does.Match("(?i)unlocked"), "The recap tells the player everything is unlocked now.");
    }

    [Test]
    public void SeedCraftingAndTheRestorationAltarAreNotTaught()
    {
        var excluded = new[] { "OpenCrafting", "OpenRestoration", "CraftingProximity", "RestorationProximity", "FifthNightWarning", "FirstPlantReady" };
        foreach (var step in Installed())
        {
            CollectionAssert.IsEmpty(Objectives(step).Intersect(excluded), step.name);
            Assert.That(Visible(step), Does.Not.Match("(?i)craft|restor"), step.name + ": out of gameplay for now (S4).");
        }
    }

    [Test]
    public void CopyIsShortAndEachNewKeyIsTaughtOnce()
    {
        var steps = Installed();
        foreach (var step in steps)
        {
            Assert.That(Visible(step).Length, Is.LessThanOrEqualTo(260), step.name + ": one short idea per box.");
            Assert.That(Raw(step), Does.Match(@"^<b>FOREST:</b> "), step.name + ": speaker stays bold uppercase.");
        }
        foreach (var icon in new[] { "WASDKeys", "ArrowKeys", "QKey", "EKey", "ScrollMouse", "SpaceKey", "TABKey", "IKey" })
            Assert.That(steps.Count(s => Raw(s).Contains("<sprite name=\"" + icon + "\">")), Is.EqualTo(1), icon + " is explained in exactly one step.");
    }

    [Test]
    public void DayAndNightKeepProgressingFromTheNightLessonUntilTheEnd()
    {
        var steps = Installed();
        int night = IndexOf(steps, "NightDefense");
        for (int i = 0; i < steps.Length; i++)
            Assert.That(Flag(steps[i], "allowNightTransition"), Is.EqualTo(i >= night), steps[i].name +
                ": the altar stays usable after its lesson so days can pass, and never before the loadout/plant lessons.");
    }

    [Test]
    public void InstalledSequenceHasNoSoftLockAndTeachesEveryActionOnce()
    {
        var steps = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(T("TutorialStep")));
        foreach (var step in Installed()) steps.Add(step);
        var problems = (List<string>)T("TutorialActionLock").GetMethod("FindSoftLocks").Invoke(null, new object[] { steps });
        Assert.That(problems, Is.Empty, string.Join("\n", problems));
        int union = 0, sum = 0;
        foreach (var step in Installed()) { int bits = Convert.ToInt32(Get(step, "unlockedActions")); union |= bits; sum += bits; }
        int all = Enum.GetValues(T("TutorialAction")).Cast<object>().Select(Convert.ToInt32).Aggregate(0, (a, b) => a | b);
        Assert.That(union, Is.EqualTo(all), "Every gated action is unlocked by some step.");
        Assert.That(sum, Is.EqualTo(union), "No action is unlocked by two steps.");
    }

    [Test]
    public void AuthoringSoftLockCheckReportsOnlyForLockedSequences()
    {
        var editor = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "Assembly-CSharp-Editor").GetType("TutorialSequenceEditor", true);
        var check = editor.GetMethod("SoftLockProblems", BindingFlags.Static | BindingFlags.Public);
        var sequence = ScriptableObject.CreateInstance(T("TutorialSequence"));
        var step = ScriptableObject.CreateInstance(T("TutorialStep"));
        try
        {
            Set(step, "objectiveType", E("TutorialObjectiveType", "RemovePlant"));
            ((IList)Get(sequence, "steps")).Add(step);
            Assert.That((IList)check.Invoke(null, new object[] { sequence }), Is.Empty, "The lock is off: nothing can be soft-locked.");
            Set(sequence, "lockActionsUntilTaught", true);
            var problems = (List<string>)check.Invoke(null, new object[] { sequence });
            Assert.That(problems, Has.Count.EqualTo(1));
            Assert.That(problems[0], Does.StartWith("Soft-lock: Step 1"));
            Set(step, "unlockedActions", E("TutorialAction", "RemovePlant,CycleTool"));
            Assert.That((IList)check.Invoke(null, new object[] { sequence }), Is.Empty);
        }
        finally { UnityEngine.Object.DestroyImmediate(sequence); UnityEngine.Object.DestroyImmediate(step); }
    }

    // ---- Completion hooks, through the manager and the real gameplay entry points -----------------

    sealed class Fixture : IDisposable
    {
        public readonly GameObject Root;
        public readonly Component Manager;
        public readonly ScriptableObject Sequence;
        public readonly List<UnityEngine.Object> Owned = new List<UnityEngine.Object>();
        readonly object previousManager, previousFlow;

        public Fixture(bool lockActions = true)
        {
            previousManager = Singleton("TutorialManager"); SetSingleton("TutorialManager", null);
            previousFlow = Singleton("GameFlowController");
            Root = new GameObject("Tutorial redesign test"); Root.SetActive(false);
            Manager = Root.AddComponent(T("TutorialManager"));
            Sequence = ScriptableObject.CreateInstance(T("TutorialSequence"));
            Set(Sequence, "lockActionsUntilTaught", lockActions);
            Set(Manager, "sequence", Sequence); Set(Manager, "enableTutorial", false);
            Root.SetActive(true);
            SetSingleton("TutorialManager", Manager);
            // EditMode does not guarantee lifecycle callbacks: pair subscriptions explicitly.
            Call(Manager, "OnDisable"); Call(Manager, "OnEnable");
        }
        public ScriptableObject Add(string objective, params string[] unlocks)
        {
            var step = ScriptableObject.CreateInstance(T("TutorialStep")); Owned.Add(step);
            Set(step, "objectiveType", E("TutorialObjectiveType", objective));
            Set(step, "unlockedActions", E("TutorialAction", unlocks.Length == 0 ? "None" : string.Join(",", unlocks)));
            ((IList)Get(Sequence, "steps")).Add(step);
            return step;
        }
        public ScriptableObject AddConfigured(string combination, string[] objectives, params string[] unlocks)
        {
            var step = Add(objectives[0], unlocks);
            Set(step, "useConfiguredCompletion", true);
            Set(step, "conditionCombination", E("TutorialConditionCombination", combination));
            var list = (IList)Get(step, "conditions");
            foreach (var objective in objectives)
            {
                var condition = Activator.CreateInstance(T("TutorialCondition"));
                Set(condition, "objective", E("TutorialObjectiveType", objective)); list.Add(condition);
            }
            return step;
        }
        public void Start() { Call(Manager, "StartTutorial"); Settle(); }
        // Stand-in for the delayed input enabling that PlayMode gets from Invoke.
        public void Settle() => Call(Manager, "ProcessBufferAndEnableInput");
        public bool Active => (bool)Call(Manager, "IsTutorialActive");
        public string Objective => Call(Manager, "GetCurrentObjectiveType").ToString();
        public bool Allowed(string action) => (bool)Call(Manager, "IsActionAllowed", E("TutorialAction", action));
        // Day phase for the entry points that read GameFlowController.
        public void UseDay()
        {
            var flow = new GameObject("Day flow"); Owned.Add(flow);
            var controller = flow.AddComponent(T("GameFlowController"));
            Set(controller, "currentPhase", E("GamePhase", "Day"));
            SetSingleton("GameFlowController", controller);
        }
        public void Dispose()
        {
            Call(Manager, "OnDisable");
            UnityEngine.Object.DestroyImmediate(Root);
            UnityEngine.Object.DestroyImmediate(Sequence);
            foreach (var o in Owned) if (o != null) UnityEngine.Object.DestroyImmediate(o);
            SetSingleton("GameFlowController", previousFlow);
            SetSingleton("TutorialManager", previousManager);
        }
    }

    [Test]
    public void RemovedPlantEventCompletesOnlyTheRemovePlantObjective()
    {
        using (var f = new Fixture())
        {
            f.Add("RemovePlant", "RemovePlant", "CycleTool"); f.Start();
            Fire("InvokeGroundDug"); Fire("InvokePlantHarvested"); Fire("InvokeAbilityChanged");
            Assert.That(f.Active, Is.True, "Other actions do not count as removing a plant.");
            Fire("InvokePlantRemoved");
            Assert.That(f.Active, Is.False);
        }
    }

    [Test]
    public void OpeningTheShopCompletesOpenCauldronShopWhileLegacyCraftingStillWaitsForClose()
    {
        using (var f = new Fixture())
        {
            f.Add("OpenCauldronShop", "CauldronShop"); f.Start();
            Fire("InvokeCraftingClosed"); Assert.That(f.Active, Is.True, "Closing is not opening.");
            Fire("InvokeCraftingOpened"); Assert.That(f.Active, Is.False, "Opening is enough; buying is not required.");
        }
        using (var f = new Fixture())
        {
            f.Add("OpenCrafting", "CauldronShop"); f.Start();
            Fire("InvokeCraftingOpened"); Assert.That(f.Active, Is.True, "Legacy OpenCrafting keeps completing on close.");
            Fire("InvokeCraftingClosed"); Assert.That(f.Active, Is.False);
        }
    }

    [Test]
    public void HarvestObjectiveCompletesOnTheHarvestEvent()
    {
        using (var f = new Fixture())
        {
            f.Add("Harvest", "Harvest", "CycleTool"); f.Start();
            Fire("InvokePlantRemoved"); Fire("InvokeGroundDug");
            Assert.That(f.Active, Is.True);
            Fire("InvokePlantHarvested"); Assert.That(f.Active, Is.False);
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void TeleportAndInventoryNeedBothInAnyOrder(bool teleportFirst)
    {
        using (var f = new Fixture())
        {
            f.AddConfigured("All", new[] { "TeleportSpell", "OpenInventory" }, "Teleport", "Inventory"); f.Start();
            Fire(teleportFirst ? "InvokeTeleportCasted" : "InvokeInventoryOpened");
            Assert.That(f.Active, Is.True, "One of the two is not enough.");
            Fire(teleportFirst ? "InvokeInventoryOpened" : "InvokeTeleportCasted");
            Assert.That(f.Active, Is.False);
        }
    }

    // Opening the inventory is a modal: it pauses the tutorial just before the event is raised.
    // A configured step must keep that observation instead of dropping it while paused.
    [TestCase(false)]
    [TestCase(true)]
    public void InventoryOpenedWhilePausedByItsOwnModalStillCounts(bool teleportFirst)
    {
        using (var f = new Fixture())
        {
            f.AddConfigured("All", new[] { "TeleportSpell", "OpenInventory" }, "Teleport", "Inventory"); f.Start();
            if (teleportFirst) Fire("InvokeTeleportCasted");
            Call(f.Manager, "PauseTutorial");
            Fire("InvokeInventoryOpened");
            Assert.That(f.Active, Is.True, "Nothing advances behind the open modal.");
            Call(f.Manager, "ResumeTutorial"); f.Settle();
            if (!teleportFirst)
            {
                Assert.That(f.Active, Is.True, "The inventory counted, the teleport is still missing.");
                Fire("InvokeTeleportCasted");
            }
            Assert.That(f.Active, Is.False);
        }
    }

    [Test]
    public void CauldronShopOpenedWhilePausedByItsOwnModalStillCountsForAConfiguredStep()
    {
        using (var f = new Fixture())
        {
            f.AddConfigured("All", new[] { "OpenCauldronShop", "Harvest" }, "CauldronShop", "Harvest", "CycleTool"); f.Start();
            Call(f.Manager, "PauseTutorial"); Fire("InvokeCraftingOpened");
            Call(f.Manager, "ResumeTutorial"); f.Settle();
            Assert.That(f.Active, Is.True);
            Fire("InvokePlantHarvested"); Assert.That(f.Active, Is.False);
        }
    }

    [Test]
    public void TeleportFiresAfterSuccessOnlyAndStaysBlockedUntilTaught()
    {
        using (var f = new Fixture())
        {
            f.Add("Move", "Move"); f.Add("TeleportSpell", "Teleport"); f.Start();
            var player = new GameObject("Teleport player"); f.Owned.Add(player);
            var teleport = player.AddComponent(T("PlayerTeleportController"));
            var tryUse = teleport.GetType().GetMethod("TryUseTeleport", BindingFlags.Instance | BindingFlags.NonPublic);
            bool used = true;
            int blocked = Count("TutorialEvents", "OnTeleportCasted", () => used = (bool)tryUse.Invoke(teleport, new object[] { Vector2.right }));
            Assert.That(used, Is.False, "Teleport is locked while only movement is taught.");
            Assert.That(blocked, Is.EqualTo(0), "A blocked teleport must not complete the objective.");
            Assert.That(f.Objective, Is.EqualTo("Move"));

            Fire("InvokePlayerMoved"); f.Settle();
            Assert.That(f.Objective, Is.EqualTo("TeleportSpell"));
            int fired = Count("TutorialEvents", "OnTeleportCasted", () => used = (bool)tryUse.Invoke(teleport, new object[] { Vector2.right }));
            Assert.That(used, Is.True, "Positive control: the same call works once the step teaches it.");
            Assert.That(fired, Is.EqualTo(1));
            Assert.That(f.Active, Is.False);
        }
    }

    [Test]
    public void InventoryOpensAndRaisesItsEventOnlyOnceTaught()
    {
        using (var f = new Fixture())
        {
            f.UseDay();
            f.Add("Move", "Move"); f.Add("OpenInventory", "Inventory"); f.Start();
            var panel = new GameObject("Inventory panel"); f.Owned.Add(panel); panel.SetActive(false);
            var owner = new GameObject("Inventory owner"); f.Owned.Add(owner);
            var inventory = owner.AddComponent(T("InventoryUIController"));
            Set(inventory, "inventoryPanel", panel);
            int blocked = Count("TutorialEvents", "OnInventoryOpened", () => Call(inventory, "OpenInventory"));
            Assert.That(panel.activeSelf, Is.False, "Inventory is locked while only movement is taught.");
            Assert.That(blocked, Is.EqualTo(0));
            Assert.That(f.Objective, Is.EqualTo("Move"));

            Fire("InvokePlayerMoved"); f.Settle();
            int fired = Count("TutorialEvents", "OnInventoryOpened", () => Call(inventory, "OpenInventory"));
            Assert.That(panel.activeSelf, Is.True, "Positive control: the same call opens it once taught.");
            Assert.That(fired, Is.EqualTo(1));
            Assert.That(f.Active, Is.False);
        }
    }

    [Test]
    public void CauldronRequestsItsShopOnlyOnceTaught()
    {
        using (var f = new Fixture())
        {
            f.UseDay();
            f.Add("Move", "Move"); f.Add("OpenCauldronShop", "CauldronShop"); f.Start();
            var owner = new GameObject("Cauldron"); f.Owned.Add(owner);
            var cauldron = owner.AddComponent(T("CraftingCauldron"));
            int blocked = Count("UIEvents", "OnCraftingUIToggleRequested", () => Call(cauldron, "Interact"));
            Assert.That((bool)Call(cauldron, "CanInteract"), Is.False);
            Assert.That(blocked, Is.EqualTo(0), "A locked cauldron must not open the shop.");

            Fire("InvokePlayerMoved"); f.Settle();
            Assert.That((bool)Call(cauldron, "CanInteract"), Is.True, "Positive control by day once taught.");
            int requested = Count("UIEvents", "OnCraftingUIToggleRequested", () => Call(cauldron, "Interact"));
            Assert.That(requested, Is.EqualTo(1));
        }
    }

    // The remove coroutine needs a camera, tilemap, plant manager and sound manager, so Play Mode covers its
    // success path; here the blocked side and the unlock that gates it are pinned.
    [Test]
    public void BlockedRemoveToolIsInertAndFiresNoEventUntilTaught()
    {
        using (var f = new Fixture())
        {
            f.Add("Move", "Move"); f.Add("RemovePlant", "RemovePlant", "CycleTool"); f.Start();
            var player = new GameObject("Remove player"); f.Owned.Add(player);
            var abilities = player.AddComponent(T("PlayerAbilitySystem"));
            int removed = Count("TutorialEvents", "OnPlantRemoved", () => Call(abilities, "HandleRemoving"));
            Assert.That(removed, Is.EqualTo(0));
            Assert.That((bool)Call(abilities, "IsBusy"), Is.False, "No removal animation or coroutine started.");
            Assert.That(f.Allowed("RemovePlant"), Is.False);
            Fire("InvokePlayerMoved"); f.Settle();
            Assert.That(f.Allowed("RemovePlant"), Is.True, "The remove step unlocks the tool.");
            Assert.That(f.Allowed("Harvest"), Is.False, "Harvest is still not taught.");
        }
    }

    // ---- S9-S12: step images and the combat panel (Play Mode feedback) -------------------------------

    static IList ListOf(object o, string name) => (IList)Get(o, name) ?? new object[0];
    static Sprite StepSprite(UnityEngine.Object step, string field = "stepImage") => (Sprite)Get(step, field);
    static UnityEngine.Object Step(string name) => Installed().Single(s => s.name == name);
    static bool Hidden(Fixture f) => (bool)f.Manager.GetType().GetProperty("IsDialogueHiddenByRule").GetValue(f.Manager);
    static Sprite FirstClipSprite(string path)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        Assert.That(clip, Is.Not.Null, path);
        var binding = AnimationUtility.GetObjectReferenceCurveBindings(clip).First(b => b.propertyName == "m_Sprite");
        return (Sprite)AnimationUtility.GetObjectReferenceCurve(clip, binding)[0].value;
    }

    // The tool icons the HUD ability bar shows for an ability: every sprite under that ability's slot.
    static Sprite[] HudAbilitySprites(string ability)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/Canvas/Game/HUD/HUD Abilities.prefab");
        Assert.That(prefab, Is.Not.Null);
        var image = Type.GetType("UnityEngine.UI.Image, UnityEngine.UI", true);
        var slot = prefab.GetComponentsInChildren(T("AbilitySlot"), true).Single(s => Get(s, "abilityType").ToString() == ability);
        return slot.GetComponentsInChildren(image, true)
            .Select(c => (Sprite)image.GetProperty("sprite").GetValue(c)).Where(s => s != null).ToArray();
    }

    [TestCase("Dig", "Digging", "ShovelInventory")]
    [TestCase("Plant", "Planting", "InventoryBag")]
    [TestCase("RemovePlant", "Removing", "RakeInventory")]
    public void ToolStepsShowTheHudAbilityIcon(string stepName, string ability, string spriteName)
    {
        var sprite = StepSprite(Step(stepName));
        Assert.That(sprite, Is.Not.Null, stepName + " shows its tool like the Dig step.");
        Assert.That(sprite.name, Is.EqualTo(spriteName));
        Assert.That(AssetDatabase.GetAssetPath(sprite), Is.EqualTo("Assets/Sprites/SpriteSheets/UtilitySprites.png"));
        Assert.That(HudAbilitySprites(ability), Does.Contain(sprite), "Same sprite the HUD ability bar uses for " + ability + ".");
        Assert.That(ListOf(Step(stepName), "stepImages"), Is.Empty, "Single-image steps stay on stepImage.");
    }

    [Test]
    public void NightStepShowsTheRitualAltarCarpet()
    {
        var sprite = StepSprite(Step("NightDefense"));
        Assert.That(sprite, Is.Not.Null);
        Assert.That(sprite.name, Is.EqualTo("RitualRug"));
        var altar = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Interactables/Ritual.prefab");
        Assert.That(sprite, Is.SameAs(altar.GetComponentInChildren<SpriteRenderer>(true).sprite), "The sprite the RitualAltar object renders.");
        Assert.That(sprite, Is.SameAs(Get(altar.GetComponent(T("RitualAltar")), "defaultSprite")));
    }

    [Test]
    public void HarvestStepShowsTheBushRockAndTreeResourceNodes()
    {
        var harvest = Step("HarvestResources");
        var images = ((IList)Get(harvest, "stepImages")).Cast<Sprite>().ToArray();
        Assert.That(images, Has.Length.EqualTo(3));
        Assert.That(images.All(s => s != null), Is.True);
        const string idle = "Assets/Animations/Plants/Harvestables/";
        // What each node shows by default: its Idle animation frame, which the prefab Animator applies at runtime.
        Assert.That(images[0], Is.SameAs(FirstClipSprite(idle + "BushHarvestabel/Bush_Idle.anim")), "Bush");
        Assert.That(images[1], Is.SameAs(FirstClipSprite(idle + "RockHarvestable/Rock_Idle.anim")), "Rock");
        Assert.That(images[2], Is.SameAs(FirstClipSprite(idle + "TreeHarvestable/Tree_Harvestable_Idle.anim")), "Tree");
        foreach (var node in new[] { "Bush", "Rock" })
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Interactables/Harvestables/Harvestable " + node + ".prefab");
            Assert.That(prefab.GetComponentInChildren<SpriteRenderer>(true).sprite, Is.SameAs(images[node == "Bush" ? 0 : 1]), node + " prefab sprite.");
        }
        Assert.That(Visible(harvest), Does.Match("(?i)bush").And.Match("(?i)rock").And.Match("(?i)tree").And.Match("(?i)decorative"));
        Assert.That(Visible(harvest).Length, Is.LessThanOrEqualTo(260));
    }

    [Test]
    public void OnlyTheNightStepHidesItsDialogueAfterTheAltarAndTheFirstCast()
    {
        foreach (var step in Installed())
        {
            var rule = ListOf(step, "hideDialogueAfter").Cast<object>().Select(o => o.ToString()).ToArray();
            if (step.name == "NightDefense")
            {
                CollectionAssert.AreEquivalent(new[] { "UseRitualAltar", "CastSpell" }, rule);
                Assert.That(rule, Does.Not.Contain("SurviveNight"), "The step must still finish on SurviveNight.");
            }
            else CollectionAssert.IsEmpty(rule, step.name);
        }
    }

    [Test]
    public void DisplayImagesFallBackToTheSingleStepImage()
    {
        var texture = new Texture2D(4, 4);
        var a = Sprite.Create(texture, new Rect(0, 0, 4, 4), Vector2.zero);
        var b = Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.zero);
        var step = ScriptableObject.CreateInstance(T("TutorialStep"));
        try
        {
            Assert.That((IList)Call(step, "GetDisplayImages"), Is.Empty, "No image at all.");
            Set(step, "stepImage", a);
            CollectionAssert.AreEqual(new[] { a }, (IList)Call(step, "GetDisplayImages"), "Existing single-image steps are unchanged.");
            var list = (IList)Get(step, "stepImages");
            list.Add(null);
            CollectionAssert.AreEqual(new[] { a }, (IList)Call(step, "GetDisplayImages"), "An empty list entry does not replace stepImage.");
            list.Clear(); list.Add(b);
            CollectionAssert.AreEqual(new[] { b }, (IList)Call(step, "GetDisplayImages"), "One list entry is shown like a single image.");
            list.Add(null); list.Add(a);
            CollectionAssert.AreEqual(new[] { b, a }, (IList)Call(step, "GetDisplayImages"), "List order is left to right; nulls are skipped; the list wins over stepImage.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(step); UnityEngine.Object.DestroyImmediate(a);
            UnityEngine.Object.DestroyImmediate(b); UnityEngine.Object.DestroyImmediate(texture);
        }
    }

    [Test]
    public void HideRuleNeedsEveryListedObjectiveAndOnlyAppliesToConfiguredSteps()
    {
        var step = ScriptableObject.CreateInstance(T("TutorialStep"));
        try
        {
            var observed = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(T("TutorialObjectiveType")));
            Func<bool> hides = () => (bool)Call(step, "ShouldHideDialogue", observed);
            Set(step, "useConfiguredCompletion", true);
            Assert.That(hides(), Is.False, "No rule: never hides.");
            var rule = (IList)Get(step, "hideDialogueAfter");
            rule.Add(E("TutorialObjectiveType", "UseRitualAltar")); rule.Add(E("TutorialObjectiveType", "CastSpell"));
            Assert.That(hides(), Is.False);
            observed.Add(E("TutorialObjectiveType", "CastSpell")); Assert.That(hides(), Is.False, "Casting alone is not enough.");
            observed.Add(E("TutorialObjectiveType", "SurviveNight")); Assert.That(hides(), Is.False, "Unlisted objectives do not count.");
            observed.Add(E("TutorialObjectiveType", "UseRitualAltar")); Assert.That(hides(), Is.True);
            Set(step, "useConfiguredCompletion", false);
            Assert.That(hides(), Is.False, "Legacy single-objective steps ignore the rule.");
        }
        finally { UnityEngine.Object.DestroyImmediate(step); }
    }

    static ScriptableObject AddNightStep(Fixture f, bool withRule = true)
    {
        var night = f.AddConfigured("All", new[] { "UseRitualAltar", "CastSpell", "SurviveNight" }, "RitualAltar", "CastSpell");
        if (withRule)
        {
            var rule = (IList)Get(night, "hideDialogueAfter");
            rule.Add(E("TutorialObjectiveType", "UseRitualAltar")); rule.Add(E("TutorialObjectiveType", "CastSpell"));
        }
        return night;
    }

    [TestCase(false)]
    [TestCase(true)]
    public void DialogueHidesOnlyAfterTheAltarAndTheFirstCastAndTheStepStillEndsOnSurvival(bool castFirst)
    {
        using (var f = new Fixture())
        {
            AddNightStep(f); f.Add("Wait"); f.Start();
            Assert.That(Hidden(f), Is.False, "Visible while the player has done nothing.");
            Fire(castFirst ? "InvokeSpellCasted" : "InvokeRitualAltarUsed");
            Assert.That(Hidden(f), Is.False, "One of the two is not enough.");
            Fire("InvokeNightStarted"); Fire("InvokeEnemyDefeated");
            Assert.That(Hidden(f), Is.False, "Unrelated events do not hide it.");
            Fire(castFirst ? "InvokeRitualAltarUsed" : "InvokeSpellCasted");
            Assert.That(Hidden(f), Is.True, "Altar used and first spell cast: the panel hides.");
            Assert.That(f.Objective, Is.EqualTo("UseRitualAltar"), "Still on the night step, still waiting for survival.");
            Fire("InvokeSpellCasted"); Fire("InvokeRitualAltarUsed");
            Assert.That(f.Objective, Is.EqualTo("UseRitualAltar"), "More casts do not finish the step.");
            Fire("InvokeNightSurvived");
            Assert.That(f.Objective, Is.EqualTo("Wait"), "SurviveNight completes the step and shows the next one.");
            Assert.That(Hidden(f), Is.False, "The next step shows its panel normally.");
        }
    }

    [Test]
    public void HiddenDialogueDoesNotChangeWhatIsUnlockedAndSurvivesModalPauseAndResume()
    {
        using (var f = new Fixture())
        {
            AddNightStep(f); f.Add("Wait"); f.Start();
            bool altar = f.Allowed("RitualAltar"), cast = f.Allowed("CastSpell"), harvest = f.Allowed("Harvest");
            Fire("InvokeRitualAltarUsed"); Fire("InvokeSpellCasted");
            Assert.That(Hidden(f), Is.True);
            Assert.That(f.Allowed("RitualAltar"), Is.EqualTo(altar));
            Assert.That(f.Allowed("CastSpell"), Is.EqualTo(cast));
            Assert.That(f.Allowed("Harvest"), Is.EqualTo(harvest), "Hiding the panel unlocks nothing.");
            Assert.That(altar && cast && !harvest, Is.True, "Control: the step unlocks exactly the altar and casting.");
            // Inventory/pause hide and re-present the current step; the rule keeps the night panel away.
            Call(f.Manager, "PauseTutorial");
            Call(f.Manager, "ResumeTutorial"); f.Settle();
            Assert.That(Hidden(f), Is.True, "Resuming after a modal must not bring the panel back.");
            Assert.That(f.Objective, Is.EqualTo("UseRitualAltar"));
            Fire("InvokeNightSurvived");
            Assert.That(f.Objective, Is.EqualTo("Wait"));
        }
    }

    [Test]
    public void StepsWithoutAHideRuleKeepTheirDialogue()
    {
        using (var f = new Fixture())
        {
            AddNightStep(f, withRule: false); f.Start();
            Fire("InvokeRitualAltarUsed"); Fire("InvokeSpellCasted");
            Assert.That(Hidden(f), Is.False, "Default behavior: the panel stays until the step ends.");
            Fire("InvokeNightSurvived");
            Assert.That(f.Active, Is.False);
        }
    }

    [Test]
    public void EventsBufferedBeforeInputIsEnabledStillApplyTheHideRule()
    {
        using (var f = new Fixture())
        {
            AddNightStep(f); f.Add("Wait");
            Call(f.Manager, "StartTutorial");   // input not enabled yet: events are buffered
            Fire("InvokeRitualAltarUsed"); Fire("InvokeSpellCasted");
            Assert.That(Hidden(f), Is.False, "Nothing is applied while the step is still being presented.");
            f.Settle();
            Assert.That(Hidden(f), Is.True);
            Assert.That(f.Objective, Is.EqualTo("UseRitualAltar"));
        }
    }
}
