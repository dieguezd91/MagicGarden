using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

// Progressive tutorial action lock (S2, S6, S7): cumulative unlocks, inactive tutorial, blocked entry
// points, pause/skip, and the no-soft-lock guard over the installed sequence.
// Production types are reached by reflection like the other tutorial fixtures (Assembly-CSharp).
public class TutorialActionLockTests
{
    const string ManagerPath = "Assets/Prefabs/Managers/TutorialManager.prefab";
    const string SequencePath = "Assets/Scriptables/Tutorial/SummerlimbsTutorial.asset";

    static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "Assembly-CSharp").GetType(name, true);
    static FieldInfo F(object o, string name) => o.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
    static void Set(object o, string name, object value) => F(o, name).SetValue(o, value);
    static object Get(object o, string name) => F(o, name).GetValue(o);
    static object Call(object o, string name, params object[] args) =>
        o.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(o, args);
    static object E(string type, string value) => Enum.Parse(T(type), value);
    static object Act(params string[] names) => E("TutorialAction", names.Length == 0 ? "None" : string.Join(",", names));
    static object Lock(string method, params object[] args) =>
        T("TutorialActionLock").GetMethod(method, BindingFlags.Static | BindingFlags.Public).Invoke(null, args);
    static IList NewSteps() => (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(T("TutorialStep")));
    static void Fire(string method) => T("TutorialEvents").GetMethod(method).Invoke(null, null);
    static object Instance
    {
        get => T("TutorialManager").GetProperty("Instance").GetValue(null);
        set => T("TutorialManager").GetProperty("Instance").GetSetMethod(true).Invoke(null, new[] { value });
    }
    // Counts how many times a static TutorialEvents event fires while the action runs.
    static int Count(string tutorialEvent, Action act)
    {
        int count = 0;
        Action handler = () => count++;
        var evt = T("TutorialEvents").GetEvent(tutorialEvent);
        evt.AddEventHandler(null, handler);
        try { act(); } finally { evt.RemoveEventHandler(null, handler); }
        return count;
    }

    readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
    GameObject root;
    Component manager;
    ScriptableObject sequence;
    object previousInstance;

    ScriptableObject MakeStep(string objective, params string[] unlocks)
    {
        var step = ScriptableObject.CreateInstance(T("TutorialStep")); owned.Add(step);
        Set(step, "objectiveType", E("TutorialObjectiveType", objective));
        Set(step, "unlockedActions", Act(unlocks));
        return step;
    }
    ScriptableObject AddStep(string objective, params string[] unlocks)
    {
        var step = MakeStep(objective, unlocks); ((IList)Get(sequence, "steps")).Add(step); return step;
    }
    bool Allowed(string action) => (bool)Call(manager, "IsActionAllowed", E("TutorialAction", action));
    bool StaticAllows(string action) => (bool)T("TutorialManager").GetMethod("Allows").Invoke(null, new[] { E("TutorialAction", action) });
    bool Active => (bool)Call(manager, "IsTutorialActive");
    IEnumerable<string> AllActions => Enum.GetNames(T("TutorialAction")).Where(n => n != "None");
    void StartLocked()
    {
        Set(sequence, "lockActionsUntilTaught", true);
        Call(manager, "StartTutorial"); Call(manager, "ProcessBufferAndEnableInput");
    }

    [SetUp]
    public void Setup()
    {
        previousInstance = Instance; Instance = null;
        root = new GameObject("Tutorial action lock test"); root.SetActive(false);
        manager = root.AddComponent(T("TutorialManager"));
        sequence = ScriptableObject.CreateInstance(T("TutorialSequence")); owned.Add(sequence);
        Set(manager, "sequence", sequence); Set(manager, "enableTutorial", false);
        root.SetActive(true);
        // EditMode does not guarantee lifecycle callbacks: pair subscriptions and own the singleton explicitly.
        Instance = manager;
        Call(manager, "OnDisable"); Call(manager, "OnEnable");
    }

    [TearDown]
    public void Cleanup()
    {
        Call(manager, "OnDisable");
        UnityEngine.Object.DestroyImmediate(root);
        foreach (var o in owned) if (o != null) UnityEngine.Object.DestroyImmediate(o);
        owned.Clear();
        Instance = previousInstance;
    }

    // ---- Pure cumulative rule -------------------------------------------------------------------

    [Test]
    public void CumulativeUnlockIsUnionOfEarlierStepsIncludingContinueOnlySteps()
    {
        var steps = NewSteps();
        steps.Add(MakeStep("Wait"));
        steps.Add(MakeStep("Move", "Move"));
        steps.Add(MakeStep("Wait"));
        steps.Add(MakeStep("Dig", "Dig", "CycleTool"));
        steps.Add(MakeStep("Wait"));
        var expected = new[] { Act(), Act("Move"), Act("Move"), Act("Move", "Dig", "CycleTool"), Act("Move", "Dig", "CycleTool") };
        for (int i = 0; i < expected.Length; i++)
            Assert.That(Lock("Unlocked", steps, i), Is.EqualTo(expected[i]), "step index " + i);
    }

    [Test]
    public void UnlockedClampsToTheSequenceAndNothingIsUnlockedBeforeTheFirstStep()
    {
        var steps = NewSteps(); steps.Add(MakeStep("Move", "Move"));
        Assert.That(Lock("Unlocked", steps, -1), Is.EqualTo(Act()));
        Assert.That(Lock("Unlocked", steps, 99), Is.EqualTo(Act("Move")));
        Assert.That(Lock("Unlocked", null, 0), Is.EqualTo(Act()));
    }

    [TestCase("Dig", "Dig", true)]
    [TestCase("Dig", "Plant", false)]
    [TestCase("Plant", "Plant", false)]
    [TestCase("Plant", "Plant,SelectSeed", true)]
    [TestCase("Plant", "Plant,CycleTool", true)]
    [TestCase("Harvest", "Harvest", false)]
    [TestCase("Harvest", "Harvest,CycleTool", true)]
    [TestCase("EnterHouse", "HouseDoor", true)]
    [TestCase("ExitHouse", "None", false)]
    [TestCase("OpenDailyLoadout", "OpenDailyLoadout", true)]
    [TestCase("ConfirmDailyLoadout", "OpenDailyLoadout", true)]
    [TestCase("TeleportSpell", "Teleport", true)]
    [TestCase("OpenInventory", "Inventory", true)]
    [TestCase("OpenCrafting", "CauldronShop", true)]
    [TestCase("OpenCauldronShop", "CauldronShop", true)]
    [TestCase("OpenCauldronShop", "Harvest", false)]
    [TestCase("RemovePlant", "RemovePlant", false)]
    [TestCase("RemovePlant", "CycleTool", false)]
    [TestCase("RemovePlant", "RemovePlant,CycleTool", true)]
    [TestCase("UseRitualAltar", "RitualAltar", true)]
    [TestCase("CastSpell", "RitualAltar", false)]
    [TestCase("Wait", "None", true)]
    [TestCase("Welcome", "None", true)]
    public void ObjectiveNeedsItsActionsUnlocked(string objective, string unlocked, bool completable)
    {
        var step = MakeStep(objective);
        Assert.That((bool)Lock("IsCompletable", step, E("TutorialAction", unlocked)), Is.EqualTo(completable));
    }

    [TestCase("All", "RitualAltar", false)]
    [TestCase("All", "RitualAltar,CastSpell", true)]
    [TestCase("Any", "RitualAltar", true)]
    [TestCase("Any", "Teleport", false)]
    public void ConfiguredConditionsFollowTheirCombination(string combination, string unlocked, bool completable)
    {
        var step = MakeStep("UseRitualAltar");
        Set(step, "useConfiguredCompletion", true);
        Set(step, "conditionCombination", E("TutorialConditionCombination", combination));
        var conditions = (IList)Get(step, "conditions");
        foreach (var objective in new[] { "UseRitualAltar", "CastSpell", "SurviveNight" })
        {
            var condition = Activator.CreateInstance(T("TutorialCondition"));
            Set(condition, "objective", E("TutorialObjectiveType", objective)); conditions.Add(condition);
        }
        Assert.That((bool)Lock("IsCompletable", step, E("TutorialAction", unlocked)), Is.EqualTo(completable));
    }

    [Test]
    public void SoftLockedStepIsReportedAndFixedByUnlockingItsAction()
    {
        var steps = NewSteps(); steps.Add(MakeStep("Move", "Move")); steps.Add(MakeStep("Dig"));
        var problems = (List<string>)Lock("FindSoftLocks", steps);
        Assert.That(problems, Has.Count.EqualTo(1));
        Assert.That(problems[0], Does.Contain("Step 2"));
        steps.Clear(); steps.Add(MakeStep("Move", "Move")); steps.Add(MakeStep("Dig", "Dig"));
        Assert.That((List<string>)Lock("FindSoftLocks", steps), Is.Empty);
    }

    // ---- TutorialManager query ------------------------------------------------------------------

    [Test]
    public void InactiveTutorialAllowsEveryAction()
    {
        AddStep("Move", "Move"); Set(sequence, "lockActionsUntilTaught", true);
        foreach (var action in AllActions)
        {
            Assert.That(Allowed(action), Is.True, "not started: " + action);
            Assert.That(StaticAllows(action), Is.True, "not started (static): " + action);
        }
        Instance = null;
        foreach (var action in AllActions) Assert.That(StaticAllows(action), Is.True, "no manager: " + action);
    }

    [Test]
    public void SequenceWithoutTheLockFlagDoesNotRestrictAnything()
    {
        AddStep("Move"); // authored with no unlocks, but lockActionsUntilTaught is off (legacy/test sequences)
        Call(manager, "StartTutorial"); Assert.That(Active, Is.True);
        foreach (var action in AllActions) Assert.That(Allowed(action), Is.True, action);
    }

    [Test]
    public void LockFollowsTheCurrentStepAndKeepsWhatWasLearned()
    {
        AddStep("Move", "Move"); AddStep("Dig", "Dig"); AddStep("Wait");
        StartLocked();
        Assert.That(Allowed("Move"), Is.True); Assert.That(Allowed("Dig"), Is.False); Assert.That(Allowed("HouseDoor"), Is.False);

        Fire("InvokePlayerMoved"); Call(manager, "ProcessBufferAndEnableInput");
        Assert.That(Call(manager, "GetCurrentObjectiveType"), Is.EqualTo(E("TutorialObjectiveType", "Dig")));
        Assert.That(Allowed("Move"), Is.True, "learned movement stays enabled");
        Assert.That(Allowed("Dig"), Is.True); Assert.That(Allowed("Plant"), Is.False);

        Fire("InvokeGroundDug"); Call(manager, "ProcessBufferAndEnableInput");
        Assert.That(Call(manager, "GetCurrentObjectiveType"), Is.EqualTo(E("TutorialObjectiveType", "Wait")));
        Assert.That(Allowed("Move"), Is.True, "Continue-only step keeps earlier lessons");
        Assert.That(Allowed("Dig"), Is.True); Assert.That(Allowed("Teleport"), Is.False);

        Call(manager, "ConfirmWaitStep");
        Assert.That(Active, Is.False);
        foreach (var action in AllActions) Assert.That(Allowed(action), Is.True, "completed tutorial unlocks " + action);
    }

    [Test]
    public void SkipUnlocksEverythingEvenBeforeAnythingWasTaught()
    {
        AddStep("Wait"); StartLocked();
        foreach (var action in AllActions) Assert.That(Allowed(action), Is.False, "locked before skip: " + action);
        Call(manager, "SkipTutorial");
        Assert.That(Active, Is.False);
        foreach (var action in AllActions) Assert.That(Allowed(action), Is.True, "skipped: " + action);
    }

    [Test]
    public void PausingTheTutorialDoesNotChangeTheLock()
    {
        AddStep("Move", "Move"); AddStep("Dig", "Dig"); StartLocked();
        var before = AllActions.Select(Allowed).ToArray();
        Call(manager, "PauseTutorial");
        Assert.That(AllActions.Select(Allowed).ToArray(), Is.EqualTo(before));
        Call(manager, "ResumeTutorial");
        Assert.That(AllActions.Select(Allowed).ToArray(), Is.EqualTo(before));
        Assert.That(Enum.GetNames(T("TutorialAction")).Any(n => Regex.IsMatch(n, "pause|escape|skip", RegexOptions.IgnoreCase)), Is.False,
            "Pause and skip are never lockable actions (S7).");
    }

    [Test]
    public void LockDoesNotDependOnTheLegacyGatedStepFlag()
    {
        // The old per-step freeze keyed on isGatedStep; unlocks must come from step data only.
        Set(AddStep("Move", "Move"), "isGatedStep", true);
        Set(AddStep("Wait"), "isGatedStep", true);
        StartLocked();
        Assert.That(Allowed("Move"), Is.True);
        Fire("InvokePlayerMoved"); Call(manager, "ProcessBufferAndEnableInput");
        Assert.That(Call(manager, "GetCurrentObjectiveType"), Is.EqualTo(E("TutorialObjectiveType", "Wait")));
        Assert.That(Allowed("Move"), Is.True, "text-only gated step keeps movement enabled (S6)");
        Assert.That(Allowed("Dig"), Is.False);
    }

    // ---- Blocked entry points: no side effect, no completion, with positive controls ------------

    [Test]
    public void BlockedDigIsInertAndFiresNoCompletion()
    {
        AddStep("Move", "Move"); AddStep("Dig", "Dig"); StartLocked();
        var tilesType = T("TilePlantingSystem");
        var instanceProperty = tilesType.GetProperty("Instance");
        var previousTiles = instanceProperty.GetValue(null);
        try
        {
            // Minimal real world for TryDig: diggable collider, empty tilemap and a tilled-soil tile asset.
            var grid = new GameObject("Dig grid"); owned.Add(grid); grid.AddComponent<Grid>();
            instanceProperty.GetSetMethod(true).Invoke(null, new object[] { null });
            var tilemapObject = new GameObject("Dig tilemap"); tilemapObject.transform.SetParent(grid.transform, false);
            var tilemap = tilemapObject.AddComponent<UnityEngine.Tilemaps.Tilemap>();
            var tiles = grid.AddComponent(tilesType);
            Set(tiles, "plantingTilemap", tilemap);
            instanceProperty.GetSetMethod(true).Invoke(null, new object[] { tiles });
            var ground = new GameObject("Dig ground"); owned.Add(ground);
            ground.transform.position = new Vector3(1.2f, 0.2f, 0f); ground.AddComponent<BoxCollider2D>();
            Physics2D.SyncTransforms();
            var player = new GameObject("Dig player"); owned.Add(player);
            var abilities = player.AddComponent(T("PlayerAbilitySystem"));
            Set(abilities, "diggableLayer", new LayerMask { value = -1 });
            var soil = ScriptableObject.CreateInstance<UnityEngine.Tilemaps.Tile>(); owned.Add(soil);
            Set(abilities, "tilledSoilTile", soil);
            var target = new Vector2(1.2f, 0.2f);
            var objectiveBefore = Call(manager, "GetCurrentObjectiveType");

            bool dug = true;
            int completions = Count("OnGroundDug", () => dug = (bool)Call(abilities, "TryDig", target));
            Assert.That(dug, Is.False, "Dig is still locked");
            Assert.That(completions, Is.EqualTo(0));
            Assert.That(Call(abilities, "IsDigging"), Is.False);
            Assert.That(Call(abilities, "IsBusy"), Is.False);
            Assert.That(Call(manager, "GetCurrentObjectiveType"), Is.EqualTo(objectiveBefore));
            Assert.That(Active, Is.True);

            // Positive control: identical world and call succeed once the lock no longer applies.
            Call(manager, "SkipTutorial");
            Assert.That((bool)Call(abilities, "TryDig", target), Is.True, "the same dig works without the lock");
            Assert.That(Call(abilities, "IsDigging"), Is.True);
        }
        finally { instanceProperty.GetSetMethod(true).Invoke(null, new[] { previousTiles }); }
    }

    // A positive control for planting cannot be built in EditMode: HandlePlanting only reaches its first
    // observable effect (StartCoroutine) behind a ready DailyLoadoutSession admission, a selected SeedInventory
    // slot with prefab and seeds, and a TilePlantingSystem. Without those the call is a no-op with or without the
    // lock, so this test pins the locked-side contract only; the unlocked side is covered by
    // LockFollowsTheCurrentStepAndKeepsWhatWasLearned (Plant/SelectSeed queries) and the Play Mode run (T3).
    [Test]
    public void BlockedPlantingIsInertAndFiresNoPlantEvent()
    {
        AddStep("Move", "Move"); AddStep("Plant", "Plant", "SelectSeed"); StartLocked();
        var player = new GameObject("Locked plant player"); owned.Add(player);
        var abilities = player.AddComponent(T("PlayerAbilitySystem"));
        int production = 0, defensive = 0;
        production = Count("OnProductionPlantPlanted", () => defensive = Count("OnDefensivePlantPlanted", () => Call(abilities, "HandlePlanting")));
        Assert.That(production, Is.EqualTo(0));
        Assert.That(defensive, Is.EqualTo(0));
        Assert.That(Call(abilities, "IsBusy"), Is.False, "no planting animation or coroutine started");
        Assert.That(Call(manager, "GetCurrentObjectiveType"), Is.EqualTo(E("TutorialObjectiveType", "Move")));
        Assert.That(Active, Is.True);
    }

    (Component door, Component transition, BoxCollider2D trigger) BuildDoor()
    {
        var doorObject = new GameObject("Door"); owned.Add(doorObject);
        var door = doorObject.AddComponent(T("HouseDoor"));
        var transition = doorObject.AddComponent(T("WorldTransitionAnimator"));
        Set(door, "worldTransition", transition);
        var spawn = new GameObject("Door spawn"); owned.Add(spawn); spawn.transform.position = new Vector3(50f, 0f, 0f);
        Set(door, "insideSpawn", spawn.transform); Set(door, "outsideSpawn", spawn.transform);
        Set(door, "lastUseTime", -10f); // the cooldown must not mask the lock
        var player = new GameObject("Door player"); owned.Add(player);
        player.tag = "Player"; player.transform.position = Vector3.zero;
        return (door, transition, player.AddComponent<BoxCollider2D>());
    }
    void SetInterior(Component transition, bool interior) =>
        Set(transition, "currentState", E("WorldState", interior ? "Interior" : "Day"));
    // Exiting moves the player before the animator runs; the animator may need scene objects, so only the
    // observable position is relied on in positive controls.
    void ExitThroughDoor(Component door, Collider2D trigger)
    {
        try { Call(door, "OnTriggerEnter2D", trigger); } catch (TargetInvocationException) { }
    }

    [Test]
    public void BlockedHouseDoorLeavesPlayerAndTutorialUntouched()
    {
        AddStep("Move", "Move"); AddStep("EnterHouse", "HouseDoor"); StartLocked();
        var (door, transition, trigger) = BuildDoor();
        var player = trigger.transform;

        SetInterior(transition, false);
        int entered = Count("OnHouseEntered", () => Call(door, "OnTriggerEnter2D", trigger));
        Assert.That(player.position, Is.EqualTo(Vector3.zero), "player is not moved inside");
        Assert.That(Get(door, "tutorialCoroutine"), Is.Null, "no delayed house-entered completion scheduled");
        Assert.That(entered, Is.EqualTo(0));

        SetInterior(transition, true);
        int exited = Count("OnHouseExited", () => Call(door, "OnTriggerEnter2D", trigger));
        Assert.That(player.position, Is.EqualTo(Vector3.zero), "player is not moved outside either");
        Assert.That((float)Get(door, "lastUseTime"), Is.EqualTo(-10f));
        Assert.That(exited, Is.EqualTo(0));
        Assert.That(Call(manager, "GetCurrentObjectiveType"), Is.EqualTo(E("TutorialObjectiveType", "Move")));

        // Positive control: the same trigger moves the player once the lock no longer applies.
        Call(manager, "SkipTutorial");
        ExitThroughDoor(door, trigger);
        Assert.That(player.position, Is.EqualTo(new Vector3(50f, 0f, 0f)), "the same door works without the lock");
    }

    [Test]
    public void BlockedHudAbilityClickDoesNotSwitchTool()
    {
        AddStep("Wait"); StartLocked();
        var player = new GameObject("HUD ability player"); owned.Add(player);
        var abilities = player.AddComponent(T("PlayerAbilitySystem"));
        var hud = new GameObject("HUD ability manager"); owned.Add(hud);
        var ui = hud.AddComponent(T("AbilityUIManager"));
        Set(ui, "playerAbilitySystem", abilities);
        var current = abilities.GetType().GetProperty("CurrentAbility");
        var planting = E("PlayerAbility", "Planting");
        Assert.That(current.GetValue(abilities), Is.EqualTo(E("PlayerAbility", "Digging")));

        int lockedChanges = Count("OnAbilityChanged", () => Call(ui, "OnAbilitySlotSelected", planting));
        Assert.That(current.GetValue(abilities), Is.EqualTo(E("PlayerAbility", "Digging")), "locked HUD click keeps the tool");
        Assert.That(lockedChanges, Is.EqualTo(0));

        // Positive control: the same click switches once the lock no longer applies.
        Call(manager, "SkipTutorial");
        int freeChanges = Count("OnAbilityChanged", () => Call(ui, "OnAbilitySlotSelected", planting));
        Assert.That(current.GetValue(abilities), Is.EqualTo(planting));
        Assert.That(freeChanges, Is.EqualTo(1));
    }

    // ---- Without the lock flag the previous gated-step freeze is unchanged -----------------------

    (Component movement, Component actions) AttachPlayerControllers()
    {
        var player = new GameObject("Gated player"); owned.Add(player);
        var movement = player.AddComponent(T("PlayerMovementController"));
        var actions = player.AddComponent(T("PlayerController"));
        Set(manager, "playerMovementController", movement); Set(manager, "playerController", actions);
        return (movement, actions);
    }

    [Test]
    public void WithoutTheLockFlagAGatedStepStillFreezesMovementAndActions()
    {
        var (movement, actions) = AttachPlayerControllers();
        Set(AddStep("Wait"), "isGatedStep", true);
        Call(manager, "StartTutorial");
        Assert.That(Get(movement, "tutorialBlocksMovement"), Is.True);
        Assert.That(Get(actions, "tutorialBlocksActions"), Is.True);
        Assert.That(Call(manager, "IsPlayerGated"), Is.True);
    }

    [Test]
    public void WithoutTheLockFlagAMoveStepStillUnfreezesAndTheNextGatedStepPreFreezes()
    {
        var (movement, actions) = AttachPlayerControllers();
        AddStep("Move"); Set(AddStep("Wait"), "isGatedStep", true);
        Call(manager, "StartTutorial");
        Assert.That(Get(movement, "tutorialBlocksMovement"), Is.False);
        Assert.That(Get(actions, "tutorialBlocksActions"), Is.False);
        Call(manager, "ProcessBufferAndEnableInput"); Fire("InvokePlayerMoved");
        Assert.That(Get(movement, "tutorialBlocksMovement"), Is.True);
        Assert.That(Get(actions, "tutorialBlocksActions"), Is.True);
    }

    [Test]
    public void WithTheLockFlagAGatedStepDoesNotFreezeMovementOrActions()
    {
        var (movement, actions) = AttachPlayerControllers();
        Set(AddStep("Wait"), "isGatedStep", true);
        StartLocked();
        Assert.That(Get(movement, "tutorialBlocksMovement"), Is.False);
        Assert.That(Get(actions, "tutorialBlocksActions"), Is.False);
    }

    [Test]
    public void WithoutTheLockFlagAGatedStepStillBlocksLeavingTheHouse()
    {
        Set(AddStep("Wait"), "isGatedStep", true);
        Call(manager, "StartTutorial");
        var (door, transition, trigger) = BuildDoor();
        SetInterior(transition, true);
        int exited = Count("OnHouseExited", () => Call(door, "OnTriggerEnter2D", trigger));
        Assert.That(trigger.transform.position, Is.EqualTo(Vector3.zero));
        Assert.That(exited, Is.EqualTo(0));

        Call(manager, "SkipTutorial");
        ExitThroughDoor(door, trigger);
        Assert.That(trigger.transform.position, Is.EqualTo(new Vector3(50f, 0f, 0f)), "positive control: no gating after skip");
    }

    // ---- Guard rail over the installed sequence -------------------------------------------------

    static object InstalledSequence()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ManagerPath);
        Assert.That(prefab, Is.Not.Null, ManagerPath);
        var installed = Get(prefab.GetComponent(T("TutorialManager")), "sequence");
        Assert.That(installed, Is.Not.Null, "The Game scene's TutorialManager must use the Summerlimbs sequence.");
        Assert.That(AssetDatabase.GetAssetPath((UnityEngine.Object)installed), Is.EqualTo(SequencePath));
        return installed;
    }
    static IList InstalledSteps()
    {
        var steps = NewSteps();
        foreach (var step in (IList)Get(InstalledSequence(), "steps")) { Assert.That(step, Is.Not.Null); steps.Add(step); }
        return steps;
    }

    [Test]
    public void InstalledSequenceEnablesTheLock()
    {
        Assert.That((bool)Get(InstalledSequence(), "lockActionsUntilTaught"), Is.True);
    }

    [Test]
    public void InstalledSequenceHasNoSoftLockedStep()
    {
        var problems = (List<string>)Lock("FindSoftLocks", InstalledSteps());
        Assert.That(problems, Is.Empty, string.Join("\n", problems));
    }

    [Test]
    public void InstalledContinueOnlyStepsNeedNoUnlockedAction()
    {
        int continueOnly = 0;
        foreach (UnityEngine.Object step in InstalledSteps())
        {
            bool configured = (bool)Get(step, "useConfiguredCompletion");
            bool noConditions = ((IList)Get(step, "conditions")).Count == 0;
            bool waitOnly = Get(step, "objectiveType").ToString() == "Wait" && (!configured || noConditions);
            if (!waitOnly) continue;
            continueOnly++;
            Assert.That((bool)Lock("IsCompletable", step, Act()), Is.True, step.name + " is Continue-only and must need nothing.");
        }
        Assert.That(continueOnly, Is.GreaterThan(0));
    }

    // Each step unlocks exactly what it asks the player to do (redesigned 14-step list, S3 + S5).
    static readonly string[][] InstalledUnlocks =
    {
        new[] { "Awakening" }, new[] { "Movement", "Move" }, new[] { "EnterHouse", "HouseDoor" },
        new[] { "OpenDailyLoadout", "OpenDailyLoadout" }, new[] { "ConfirmDailyLoadout" }, new[] { "ExitHouse" },
        new[] { "Dig", "Dig", "CycleTool" }, new[] { "Plant", "Plant", "SelectSeed" },
        new[] { "RemovePlant", "RemovePlant" }, new[] { "TeleportAndInventory", "Teleport", "Inventory" },
        new[] { "NightDefense", "RitualAltar", "CastSpell" }, new[] { "CauldronUpgradeShop", "CauldronShop" },
        new[] { "HarvestResources", "Harvest" }, new[] { "TutorialComplete" },
    };

    [Test]
    public void InstalledStepsUnlockExactlyWhatTheyTeach()
    {
        var steps = InstalledSteps();
        Assert.That(steps.Count, Is.EqualTo(InstalledUnlocks.Length));
        for (int i = 0; i < InstalledUnlocks.Length; i++)
        {
            var step = (UnityEngine.Object)steps[i];
            Assert.That(step.name, Is.EqualTo(InstalledUnlocks[i][0]));
            Assert.That(Get(step, "unlockedActions"), Is.EqualTo(Act(InstalledUnlocks[i].Skip(1).ToArray())), step.name);
        }
    }

    [Test]
    public void EachTaughtActionStaysLockedUntilItsStepAndEnabledFromThenOn()
    {
        var steps = InstalledSteps();
        for (int i = 0; i < InstalledUnlocks.Length; i++)
            foreach (var action in InstalledUnlocks[i].Skip(1))
            {
                var flag = E("TutorialAction", action);
                if (i > 0) Assert.That(Lock("IsAllowed", steps, i - 1, flag), Is.EqualTo(false), action + " must stay locked before " + InstalledUnlocks[i][0]);
                for (int later = i; later < steps.Count; later++)
                    Assert.That(Lock("IsAllowed", steps, later, flag), Is.EqualTo(true), action + " stays enabled at step " + (later + 1));
            }
    }

    [Test]
    public void EveryActionIsTaughtExactlyOnceAndAllAreEnabledAtTheEnd()
    {
        var steps = InstalledSteps();
        foreach (var name in Enum.GetNames(T("TutorialAction")).Where(n => n != "None"))
        {
            int teachers = InstalledUnlocks.Count(row => row.Skip(1).Contains(name));
            Assert.That(teachers, Is.EqualTo(1), name + " is unlocked by exactly one step");
            Assert.That(Lock("IsAllowed", steps, steps.Count - 1, E("TutorialAction", name)), Is.EqualTo(true), name);
        }
    }
}
