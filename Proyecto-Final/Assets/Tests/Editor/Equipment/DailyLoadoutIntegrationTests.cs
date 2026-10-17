using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

// Exercise actual Assembly-CSharp public APIs; private access only configures
// isolated fixtures and dispatches Unity lifecycle callbacks on inactive objects.
public sealed class DailyLoadoutIntegrationTests
{
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private readonly List<UnityEngine.Object> objects = new List<UnityEngine.Object>();
    private readonly List<Component> owners = new List<Component>();
    private readonly List<IDisposable> sessions = new List<IDisposable>();
    private readonly Dictionary<FieldInfo, object> saved = new Dictionary<FieldInfo, object>();
    private Component seeds, spells, xp, unlocks, flow, cycle;
    private ScriptableObject progression, firstSpell, secondSpell, laterSpell;

    private static Type Production(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Single(a => a.GetName().Name == "Assembly-CSharp").GetType(name, true);
    private static object Property(object value, string name) => value.GetType().GetProperty(name).GetValue(value);
    private static object Field(object value, string name) => value.GetType().GetField(name, Fields).GetValue(value);
    private static void Set(object value, string name, object data) => value.GetType().GetField(name, Fields).SetValue(value, data);
    private static object Seed(int id) => Enum.ToObject(Production("SeedsEnum"), id);
    private static void Lifecycle(object value, string name) => value.GetType().GetMethod(name, Fields).Invoke(value, null);
    private static object Call(object value, string name, params object[] args) => value.GetType().GetMethods()
        .Single(m => m.Name == name && m.GetParameters().Length == args.Length &&
            m.GetParameters().Select((p, i) => args[i] == null || p.ParameterType.IsInstanceOfType(args[i])).All(x => x))
        .Invoke(value, args);
    private static void Listen(object value, string name, Delegate callback)
    {
        var ev = value.GetType().GetEvent(name);
        if (ev != null) ev.AddEventHandler(value, callback);
        else
        {
            var field = value.GetType().GetField(name);
            field.SetValue(value, Delegate.Combine((Delegate)field.GetValue(value), callback));
        }
    }

    [SetUp]
    public void Setup()
    {
        // Actual SetPhase must not find/animate a user's world from an EditMode test.
        if (UnityEngine.Object.FindObjectOfType(Production("WorldTransitionAnimator")) != null)
            Assert.Ignore("Run equipment integration tests in an empty/Menu test context, not a live world scene.");
        foreach (string name in new[] { "SeedInventory", "SpellInventory", "GameFlowController", "DayCycleController", "UIManager", "InventoryManager" })
        {
            var type = Production(name);
            var field = type.GetField("Instance", BindingFlags.Public | BindingFlags.Static)
                ?? type.GetField("<Instance>k__BackingField", BindingFlags.NonPublic | BindingFlags.Static)
                ?? type.GetField("_instance", BindingFlags.NonPublic | BindingFlags.Static);
            saved.Add(field, field.GetValue(null));
            field.SetValue(null, null);
        }
        flow = Component("GameFlowController");
        cycle = Component("DayCycleController");
        seeds = Component("SeedInventory");
        spells = Component("SpellInventory");
        progression = Data("ExperienceProgressionDataSO");
        firstSpell = Spell(0);
        secondSpell = Spell(1);
        laterSpell = Spell(2);
        var levels = TypedList("ExperienceLevelData");
        levels.Add(Activator.CreateInstance(Production("ExperienceLevelData"), 100,
            TypedList("PlantDataSO", Plant(7), Plant(3)), TypedList("SpellDataSO", firstSpell, secondSpell)));
        levels.Add(Activator.CreateInstance(Production("ExperienceLevelData"), 250,
            TypedList("PlantDataSO", Plant(14)), TypedList("SpellDataSO", laterSpell)));
        Set(progression, "levelRequirements", levels);
        xp = Component("PlayerExperienceSystem", false);
        Set(xp, "progressionData", progression);
        Lifecycle(xp, "Awake");
        unlocks = xp.gameObject.AddComponent(Production("PlayerContentUnlockSystem"));
        Set(unlocks, "playerExperienceSystem", xp);
        Set(unlocks, "spellInventory", spells);
        owners.Add(unlocks);
        Lifecycle(unlocks, "OnEnable");
    }

    [TearDown]
    public void Cleanup()
    {
        try
        {
            foreach (var owner in owners)
                if (owner != null) { Lifecycle(owner, "OnDisable"); Lifecycle(owner, "OnDestroy"); }
            foreach (var session in sessions) session.Dispose();
            for (int i = objects.Count - 1; i >= 0; i--)
                if (objects[i] != null) UnityEngine.Object.DestroyImmediate(objects[i]);
        }
        finally
        {
            owners.Clear(); sessions.Clear(); objects.Clear();
            foreach (var entry in saved) entry.Key.SetValue(null, entry.Value);
            saved.Clear();
        }
    }

    private GameObject Object(string name)
    {
        var go = new GameObject(name); go.SetActive(false); objects.Add(go); return go;
    }
    private Component Component(string name, bool awake = true)
    {
        var component = Object("Equipment fixture " + name).AddComponent(Production(name));
        if (awake) Lifecycle(component, "Awake");
        return component;
    }
    private ScriptableObject Data(string name)
    {
        var data = ScriptableObject.CreateInstance(Production(name)); objects.Add(data); return data;
    }
    private ScriptableObject Plant(int id)
    {
        var data = Data("PlantDataSO"); Set(data, "seedType", Seed(id));
        Set(data, "plantPrefab", Object("Plant " + id)); return data;
    }
    private ScriptableObject Spell(int index)
    {
        var data = Data("SpellDataSO"); Set(data, "slotIndex", index);
        Set(data, "spellType", Enum.ToObject(Production("SpellType"), 1));
        Set(data, "spellPrefab", Object("Spell " + index));
        Set(data, "spellName", "Spell " + index); Set(data, "cooldown", 3600f); return data;
    }
    private static IList TypedList(string name, params object[] values)
    {
        var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(Production(name)));
        foreach (var value in values) list.Add(value);
        return list;
    }
    private object StartOwner()
    {
        Lifecycle(unlocks, "Start");
        return Property(unlocks, "Loadout");
    }
    private object NewSession()
    {
        var session = (IDisposable)Activator.CreateInstance(Production("DailyLoadoutSession"), unlocks, xp, seeds, spells, cycle, flow);
        sessions.Add(session); return session;
    }
    private void Day() => Call(cycle, "StartDay");
    private void Night() => Call(cycle, "StartNight");
    private static object Identity(object session, string getter, int index) => Property(Call(session, getter, index), "Identity");
    private static void AssertResolution(object result, bool unlocked, bool equipped, bool entry)
    {
        Assert.That(Property(result, "IsUnlocked"), Is.EqualTo(unlocked));
        Assert.That(Property(result, "IsEquipped"), Is.EqualTo(equipped));
        Assert.That(Property(result, "HasUsableInventoryEntry"), Is.EqualTo(entry));
    }

    [Test]
    public void BootstrapWaitsForCompletedUnlocksAndReconcilesDayThatStartedFirst()
    {
        Day();
        var session = NewSession();
        Assert.That(Property(session, "IsInitialized"), Is.False);
        Call(unlocks, "RebuildUnlocksUpToLevel", 1);
        Assert.That(Property(session, "IsInitialized"), Is.True);
        Assert.That(Identity(session, "GetCommittedPlant", 0), Is.EqualTo(Seed(7)));
        Assert.That(Identity(session, "GetCommittedPlant", 1), Is.EqualTo(Seed(3)));
        Assert.That(Property(Call(session, "GetCommittedPlant", 2), "HasValue"), Is.False);
        Assert.That(Identity(session, "GetCommittedSpell", 0), Is.SameAs(firstSpell));
        Assert.That(Property(session, "HasConfirmedToday"), Is.False);
        Assert.That(Call(session, "TryConfirm"), Is.EqualTo(true));
    }

    [Test]
    public void OwnerStartBootstrapsBeforeFirstDayAndLaterUnlocksDoNotReplaceEquipment()
    {
        var session = StartOwner();
        Assert.That(Property(session, "IsInitialized"), Is.True);
        Assert.That(Call(session, "TryConfirm"), Is.EqualTo(false));
        Day();
        Call(xp, "AddExperience", 100);
        Call(unlocks, "RebuildUnlocksUpToLevel", 2);
        Assert.That(Identity(session, "GetCommittedPlant", 0), Is.EqualTo(Seed(7)));
        Assert.That(Property(Call(session, "GetCommittedPlant", 2), "HasValue"), Is.False);
        Assert.That(Property(Call(session, "GetCommittedSpell", 2), "HasValue"), Is.False);
        Assert.That(Call(session, "TrySetDraftPlant", 2, Seed(14)), Is.EqualTo(true));
        Assert.That(Call(session, "TryConfirm"), Is.EqualTo(true));
    }

    [Test]
    public void BootstrapMatchesSerializedLevelOneCatalogOrder()
    {
        var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>("Assets/Scriptables/Progression/Experience Progression.asset");
        Assert.That(asset, Is.Not.Null);
        Set(xp, "progressionData", asset);
        var session = StartOwner();
        Assert.That(Identity(session, "GetCommittedPlant", 0), Is.EqualTo(Seed(3)));
        Assert.That(Identity(session, "GetCommittedPlant", 1), Is.EqualTo(Seed(7)));
        var level = Call(asset, "GetLevelData", 1);
        var orderedSpells = (IList)Property(level, "SpellUnlocks");
        Assert.That(Identity(session, "GetCommittedSpell", 0), Is.SameAs(orderedSpells[0]));
        Assert.That(Identity(session, "GetCommittedSpell", 1), Is.SameAs(orderedSpells[1]));
        Assert.That(Property(Call(session, "GetCommittedSpell", 2), "HasValue"), Is.False);
    }

    [Test]
    public void BootstrapAndQueriesNeverGrantSelectOrChangeExistingStockAndCooldowns()
    {
        Call(unlocks, "RebuildUnlocksUpToLevel", 1);
        Call(seeds, "TryGrantUnlockedPlant", Plant(7), 5);
        Call(spells, "StartCooldown", 0);
        Call(seeds, "SelectSlot", 4); Call(spells, "SelectSlot", 1);
        int notifications = 0;
        Listen(seeds, "onInventoryChanged", (Action)(() => notifications++));
        Listen(seeds, "onSlotSelected", (Action<int>)(_ => notifications++));
        Listen(spells, "OnSpellInventoryChanged", (Action)(() => notifications++));
        Listen(spells, "onSpellSlotSelected", (Action<int>)(_ => notifications++));
        Listen(spells, "onCooldownUpdated", (Action<int>)(_ => notifications++));
        var session = NewSession();
        for (int i = 0; i < 4; i++)
        {
            AssertResolution(Call(session, "ResolvePlant", Seed(7)), true, true, true);
            AssertResolution(Call(session, "ResolveSpell", firstSpell), true, true, true);
        }
        Assert.That(Call(seeds, "GetSeedCountInSlot", 0), Is.EqualTo(5));
        Assert.That(Field(Call(spells, "GetSpellSlot", 0), "currentCooldown"), Is.EqualTo(3600f));
        Assert.That(Call(seeds, "GetSelectedSlotIndex"), Is.EqualTo(4));
        Assert.That(Call(spells, "GetSelectedSlotIndex"), Is.EqualTo(1));
        Assert.That(notifications, Is.Zero);
    }

    [Test]
    public void PlantIdentitySurvivesDepletionClearingSwapsAndReplenishment()
    {
        Day(); var session = StartOwner();
        AssertResolution(Call(session, "ResolvePlant", Seed(7)), true, true, false);
        Call(seeds, "TryGrantUnlockedPlant", Plant(7), 1);
        Call(seeds, "SelectSlot", 0);
        Call(seeds, "ConsumeSeedInSelectedSlot");
        AssertResolution(Call(session, "ResolvePlant", Seed(7)), true, true, false);
        Assert.That(Identity(session, "GetCommittedPlant", 0), Is.EqualTo(Seed(7)));
        Call(seeds, "TryGrantUnlockedPlant", Plant(7), 4);
        Call(seeds, "SwapSlots", 0, 5);
        var resolved = Call(session, "ResolvePlant", Seed(7));
        AssertResolution(resolved, true, true, true);
        Assert.That(Property(resolved, "InventoryIndex"), Is.EqualTo(5));
        Assert.That(Property(resolved, "Slot"), Is.SameAs(Call(seeds, "GetPlantSlot", 5)));
        Assert.That(Call(seeds, "GetSelectedSlotIndex"), Is.Zero);
        Call(seeds, "RemoveSeedFromSlot", 5);
        AssertResolution(Call(session, "ResolvePlant", Seed(7)), true, true, false);
    }

    [Test]
    public void UnlockedEquippedAndInventoryEntryAreSeparateStates()
    {
        Day(); var session = StartOwner();
        Call(xp, "AddExperience", 100);
        AssertResolution(Call(session, "ResolvePlant", Seed(14)), true, false, false);
        Call(seeds, "TryGrantUnlockedPlant", Plant(14), 2);
        AssertResolution(Call(session, "ResolvePlant", Seed(14)), true, false, true);
        AssertResolution(Call(session, "ResolvePlant", Seed(7)), true, true, false);
        AssertResolution(Call(session, "ResolvePlant", Seed(0)), false, false, false);
    }

    [Test]
    public void SpellResolutionReturnsOriginalCooldownOwnerAndRejectsInvalidMappings()
    {
        Day(); var session = StartOwner();
        Call(spells, "StartCooldown", 0);
        var slot = Call(spells, "GetSpellSlot", 0);
        var resolved = Call(session, "ResolveSpell", firstSpell);
        AssertResolution(resolved, true, true, true);
        Assert.That(Property(resolved, "Slot"), Is.SameAs(slot));
        Assert.That(Field(slot, "currentCooldown"), Is.EqualTo(3600f));
        // Invalid data configuration, not a new identity tied to storage position.
        Set(firstSpell, "slotIndex", 6);
        AssertResolution(Call(session, "ResolveSpell", firstSpell), true, true, false);
        Assert.That(Field(slot, "currentCooldown"), Is.EqualTo(3600f));
        Set(firstSpell, "slotIndex", 0);
        Set(firstSpell, "manaCost", 999);
        AssertResolution(Call(session, "ResolveSpell", firstSpell), true, true, false);
        Assert.That(Call(spells, "GetSelectedSlotIndex"), Is.Zero);
    }

    [Test]
    public void ConflictingUnlockedSpellAssetsCannotClaimTheSameInventoryEntry()
    {
        var conflict = Spell(0);
        var levels = (IList)Field(progression, "levelRequirements");
        ((IList)Property(levels[0], "SpellUnlocks")).Add(conflict);
        var session = StartOwner();
        AssertResolution(Call(session, "ResolveSpell", firstSpell), true, true, false);
        AssertResolution(Call(session, "ResolveSpell", conflict), true, true, false);
    }

    [Test]
    public void RepeatedDayAndDisplayedDayResetDoNotRenewManualAllowance()
    {
        Day(); var session = StartOwner();
        Assert.That(Call(session, "TryConfirm"), Is.EqualTo(true));
        Day(); Day(); Call(cycle, "ResetDayCount"); Day();
        Assert.That(Call(session, "TryConfirm"), Is.EqualTo(false));
        Assert.That(Property(session, "HasConfirmedToday"), Is.True);
        Night(); Day();
        Assert.That(Property(session, "HasConfirmedToday"), Is.False);
        Assert.That(Call(session, "TryConfirm"), Is.EqualTo(true));
    }

    [Test]
    public void NightDiscardsDraftAndNextGenuineDayRestoresCommittedDraftAndAllowance()
    {
        Day(); var session = StartOwner();
        Call(session, "TrySetDraftPlant", 0, Seed(0));
        Call(session, "TrySetDraftSpell", 0, null);
        Night();
        Assert.That(Call(session, "TryConfirm"), Is.EqualTo(false));
        Assert.That(Call(session, "TrySetDraftPlant", 0, Seed(3)), Is.EqualTo(false));
        Assert.That(Identity(session, "GetDraftPlant", 0), Is.EqualTo(Seed(7)));
        Assert.That(Identity(session, "GetDraftSpell", 0), Is.SameAs(firstSpell));
        Day();
        Assert.That(Call(session, "TryConfirm"), Is.EqualTo(true));
        Assert.That(Identity(session, "GetCommittedPlant", 0), Is.EqualTo(Seed(7)));
    }

    [Test]
    public void WeeklyRolloverDoesNotReuseTheDisplayedDayAsTheConfirmationKey()
    {
        Day(); var session = StartOwner();
        Call(session, "TryConfirm"); Night(); Day();
        Assert.That(Call(session, "TryConfirm"), Is.EqualTo(true));
        int displayed = (int)Property(cycle, "CurrentDay");
        Call(cycle, "RequestWeekReset"); Night(); Day();
        Assert.That(Property(cycle, "CurrentDay"), Is.EqualTo(displayed));
        Assert.That(Property(cycle, "TotalNights"), Is.EqualTo(2));
        Assert.That(Call(session, "TryConfirm"), Is.EqualTo(true));
    }

    [Test]
    public void InvalidDraftOrConfirmationDoesNotChangeCommittedStateOrSpendAllowance()
    {
        Day(); var session = StartOwner();
        Assert.That(Call(session, "TrySetDraftPlant", 2, Seed(7)), Is.EqualTo(false));
        Assert.That(Call(session, "TrySetDraftPlant", 2, Seed(14)), Is.EqualTo(false));
        Assert.That(Call(session, "TrySetDraftSpell", 2, firstSpell), Is.EqualTo(false));
        Call(unlocks, "RebuildUnlocksUpToLevel", 0); // Lose eligibility before confirmation.
        Assert.That(Call(session, "TryConfirm"), Is.EqualTo(false));
        Assert.That(Property(session, "HasConfirmedToday"), Is.False);
        Assert.That(Identity(session, "GetCommittedPlant", 0), Is.EqualTo(Seed(7)));
        Assert.That(Identity(session, "GetCommittedSpell", 0), Is.SameAs(firstSpell));
        Call(unlocks, "RebuildUnlocksUpToLevel", 1);
        Assert.That(Call(session, "TrySetDraftPlant", 0, Seed(0)), Is.EqualTo(true));
        Assert.That(Call(session, "TrySetDraftSpell", 0, null), Is.EqualTo(true));
        Assert.That(Call(session, "TryConfirm"), Is.EqualTo(true));
        Assert.That(Property(Call(session, "GetCommittedPlant", 0), "HasValue"), Is.False);
        Assert.That(Property(Call(session, "GetCommittedSpell", 0), "HasValue"), Is.False);
    }

    [Test]
    public void FullRunResetRebootstrapsOnlyAfterExistingProgressionResetCompletes()
    {
        // ResetGame includes global cleanup: never invoke against pre-existing world objects.
        if (GameObject.FindGameObjectsWithTag("Enemy").Length != 0 ||
            UnityEngine.Object.FindObjectsOfType(Production("BasicRangeSpell")).Length != 0 ||
            UnityEngine.Object.FindObjectOfType(Production("ManaSystem")) != null)
            Assert.Ignore("Full reset test requires an empty isolated test context.");
        Day(); var session = StartOwner();
        Call(xp, "AddExperience", 100);
        Call(session, "TrySetDraftPlant", 0, Seed(14));
        Call(session, "TrySetDraftSpell", 0, laterSpell);
        Assert.That(Call(session, "TryConfirm"), Is.EqualTo(true));
        Night(); Day(); Call(session, "TryConfirm");
        int nights = (int)Property(cycle, "TotalNights");
        var reset = Component("GameResetController", false);
        Set(reset, "player", xp.gameObject);
        Set(reset, "playerExperienceSystem", xp);
        Call(reset, "ResetGame");
        Assert.That(Property(unlocks, "Loadout"), Is.SameAs(session));
        Assert.That(Property(xp, "CurrentLevel"), Is.EqualTo(1));
        Assert.That(Property(cycle, "TotalNights"), Is.EqualTo(nights));
        Assert.That(Property(session, "IsInitialized"), Is.True);
        Assert.That(Property(session, "HasConfirmedToday"), Is.False);
        Assert.That(Identity(session, "GetCommittedPlant", 0), Is.EqualTo(Seed(7)));
        Assert.That(Identity(session, "GetCommittedSpell", 0), Is.SameAs(firstSpell));
        Assert.That(Property(Call(session, "GetCommittedPlant", 2), "HasValue"), Is.False);
        AssertResolution(Call(session, "ResolvePlant", Seed(14)), false, false, false);
        Assert.That(Call(session, "TryConfirm"), Is.EqualTo(true));
    }
}
