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
    public void ConfirmationLocksBothDraftCategoriesUntilGenuineDayOrRunReset()
    {
        Day(); var session = StartOwner();
        Assert.That(Property(session, "CanConfigure"), Is.True);
        Assert.That(Call(session, "TryConfirm"), Is.EqualTo(true));
        Assert.That(Property(session, "CanConfigure"), Is.False);
        Call(session, "DiscardDraft"); Day();
        Assert.That(Call(session, "TrySetDraftPlant", 0, Seed(0)), Is.EqualTo(false));
        Assert.That(Call(session, "TrySetDraftSpell", 0, null), Is.EqualTo(false));
        Assert.That(Identity(session, "GetDraftPlant", 0), Is.EqualTo(Seed(7)));
        Assert.That(Identity(session, "GetDraftSpell", 0), Is.SameAs(firstSpell));
        Night(); Day();
        Assert.That(Property(session, "CanConfigure"), Is.True);
        Assert.That(Call(session, "TrySetDraftPlant", 0, Seed(0)), Is.EqualTo(true));
        Assert.That(Call(session, "TrySetDraftSpell", 0, null), Is.EqualTo(true));
        Assert.That(Call(session, "TryConfirm"), Is.EqualTo(true));
        Call(session, "ResetForNewRun");
        Assert.That(Property(session, "CanConfigure"), Is.True);
        Assert.That(Property(session, "HasConfirmedToday"), Is.False);
        Assert.That(Identity(session, "GetCommittedPlant", 0), Is.EqualTo(Seed(7)));
        Assert.That(Identity(session, "GetCommittedSpell", 0), Is.SameAs(firstSpell));
    }

    private object Admission(object session) => Property(session, "Admission");
    private static string PlantAdmission(object policy, object identity)
    {
        var args = new[] { identity, null };
        return policy.GetType().GetMethod("CheckPlant").Invoke(policy, args).ToString();
    }
    private static string SpellAdmission(object policy, object identity)
    {
        var args = new[] { identity, null };
        return policy.GetType().GetMethod("CheckSpell").Invoke(policy, args).ToString();
    }

    [Test]
    public void AdmissionIsReadOnlyPhaseSpecificAndSeparatesSpellReadiness()
    {
        Day(); var session = StartOwner(); var policy = Admission(session);
        Call(seeds, "TryGrantUnlockedPlant", Plant(7), 2);
        Call(xp, "AddExperience", 100);
        Call(seeds, "TryGrantUnlockedPlant", Plant(14), 2);
        Call(seeds, "SelectSlot", 5); Call(spells, "SelectSlot", 2);
        Call(spells, "StartCooldown", 0);
        int notifications = 0;
        Listen(seeds, "onInventoryChanged", (Action)(() => notifications++));
        Listen(seeds, "onSlotSelected", (Action<int>)(_ => notifications++));
        Listen(spells, "onSpellSlotSelected", (Action<int>)(_ => notifications++));
        Listen(spells, "onCooldownUpdated", (Action<int>)(_ => notifications++));
        Assert.That(PlantAdmission(policy, Seed(7)), Is.EqualTo("Allowed"));
        Assert.That(PlantAdmission(policy, Seed(14)), Is.EqualTo("Unequipped"));
        Assert.That(SpellAdmission(policy, firstSpell), Is.EqualTo("WrongPhase"));
        Night();
        Assert.That(PlantAdmission(policy, Seed(7)), Is.EqualTo("WrongPhase"));
        Assert.That(SpellAdmission(policy, firstSpell), Is.EqualTo("Allowed"));
        Assert.That(SpellAdmission(policy, laterSpell), Is.EqualTo("Unequipped"));
        Assert.That(Call(seeds, "GetSelectedSlotIndex"), Is.EqualTo(5));
        Assert.That(Call(spells, "GetSelectedSlotIndex"), Is.EqualTo(2));
        Assert.That(Call(seeds, "GetSeedCountInSlot", 0), Is.EqualTo(2));
        Assert.That(Field(Call(spells, "GetSpellSlot", 0), "currentCooldown"), Is.EqualTo(3600f));
        Assert.That(notifications, Is.Zero);
    }

    [Test]
    public void AdmissionFailsClosedBeforeInitializationAndAfterDisposalOrUnlockLoss()
    {
        Day(); var session = NewSession(); var policy = Admission(session);
        Assert.That(PlantAdmission(policy, Seed(7)), Is.EqualTo("SessionUnavailable"));
        Call(unlocks, "RebuildUnlocksUpToLevel", 1);
        Call(seeds, "TryGrantUnlockedPlant", Plant(7), 1);
        Assert.That(PlantAdmission(policy, Seed(7)), Is.EqualTo("Allowed"));
        Call(unlocks, "RebuildUnlocksUpToLevel", 0);
        Assert.That(PlantAdmission(policy, Seed(7)), Is.EqualTo("Locked"));
        Call(session, "Dispose");
        Assert.That(PlantAdmission(policy, Seed(7)), Is.EqualTo("SessionUnavailable"));
    }

    [Test]
    public void AdmissionTracksDepletionRestockAndSwapsWithoutChangingAssignments()
    {
        Day(); var session = StartOwner(); var policy = Admission(session);
        Assert.That(PlantAdmission(policy, Seed(7)), Is.EqualTo("Unavailable"));
        Call(seeds, "TryGrantUnlockedPlant", Plant(7), 1);
        Assert.That(PlantAdmission(policy, Seed(7)), Is.EqualTo("Allowed"));
        Call(seeds, "SelectSlot", 0); Call(seeds, "ConsumeSeedInSelectedSlot");
        Assert.That(PlantAdmission(policy, Seed(7)), Is.EqualTo("Unavailable"));
        Call(seeds, "TryGrantUnlockedPlant", Plant(7), 3); Call(seeds, "SwapSlots", 0, 5);
        Assert.That(PlantAdmission(policy, Seed(7)), Is.EqualTo("Allowed"));
        Assert.That(Identity(session, "GetCommittedPlant", 0), Is.EqualTo(Seed(7)));
        Assert.That(Property(Call(session, "ResolvePlant", Seed(7)), "InventoryIndex"), Is.EqualTo(5));
    }

    private object Selection(object session) => Activator.CreateInstance(
        Production("EquipmentGameplaySelection"), session, seeds, spells);

    [Test]
    public void ContextualNumbersResolveEquipmentPositionsNotStorageAndEmitOneOriginalEvent()
    {
        Day(); var session = StartOwner(); var selection = Selection(session);
        Call(seeds, "TryGrantUnlockedPlant", Plant(7), 2);
        Call(seeds, "TryGrantUnlockedPlant", Plant(3), 2);
        Call(seeds, "SwapSlots", 0, 5); Call(seeds, "SwapSlots", 1, 4);
        int seedEvents = 0, spellEvents = 0;
        Listen(seeds, "onSlotSelected", (Action<int>)(_ => seedEvents++));
        Listen(spells, "onSpellSlotSelected", (Action<int>)(_ => spellEvents++));
        Assert.That(Call(selection, "TrySelectPosition", 0), Is.EqualTo(true));
        Assert.That(Call(seeds, "GetSelectedSlotIndex"), Is.EqualTo(5));
        Assert.That(seedEvents, Is.EqualTo(1)); Assert.That(spellEvents, Is.Zero);
        Assert.That(Call(selection, "TrySelectPosition", 1), Is.EqualTo(true));
        Assert.That(Call(seeds, "GetSelectedSlotIndex"), Is.EqualTo(4));
        Assert.That(seedEvents, Is.EqualTo(2));
        foreach (int position in new[] { -1, 2, 3, 7 })
            Assert.That(Call(selection, "TrySelectPosition", position), Is.EqualTo(false));
        Assert.That(seedEvents, Is.EqualTo(2));
        // Change equipment order without changing spell storage.
        Call(session, "TrySetDraftSpell", 0, null);
        Call(session, "TrySetDraftSpell", 1, firstSpell);
        Call(session, "TrySetDraftSpell", 0, secondSpell);
        Assert.That(Call(session, "TryConfirm"), Is.EqualTo(true));
        Night();
        Assert.That(Call(selection, "TrySelectPosition", 0), Is.EqualTo(true));
        Assert.That(Call(spells, "GetSelectedSlotIndex"), Is.EqualTo(1));
        Assert.That(spellEvents, Is.EqualTo(1)); Assert.That(seedEvents, Is.EqualTo(2));
        Assert.That(Call(selection, "TrySelectPosition", 1), Is.EqualTo(true));
        Assert.That(Call(spells, "GetSelectedSlotIndex"), Is.EqualTo(0));
        Assert.That(spellEvents, Is.EqualTo(2));
        Assert.That(Call(selection, "TrySelectPosition", 2), Is.EqualTo(false));
        Assert.That(Call(selection, "TrySelectPosition", 3), Is.EqualTo(false));
        Assert.That(spellEvents, Is.EqualTo(2));
    }

    [Test]
    public void ContextualSelectionRejectsDepletedAndInvalidEntriesAndRestockRestoresIt()
    {
        Day(); var session = StartOwner(); var selection = Selection(session);
        Call(seeds, "SelectSlot", 5);
        Assert.That(Call(selection, "TrySelectPosition", 0), Is.EqualTo(false));
        Assert.That(Call(seeds, "GetSelectedSlotIndex"), Is.EqualTo(5));
        Call(seeds, "TryGrantUnlockedPlant", Plant(7), 1);
        Assert.That(Call(selection, "TrySelectPosition", 0), Is.EqualTo(true));
        Call(seeds, "ConsumeSeedInSelectedSlot");
        Assert.That(Call(selection, "TrySelectPosition", 0), Is.EqualTo(false));
        Call(seeds, "TryGrantUnlockedPlant", Plant(7), 3);
        Assert.That(Call(selection, "TrySelectPosition", 0), Is.EqualTo(true));
        Night(); Call(spells, "SelectSlot", 6);
        Set(firstSpell, "manaCost", 999);
        Assert.That(Call(selection, "TrySelectPosition", 0), Is.EqualTo(false));
        Assert.That(Call(spells, "GetSelectedSlotIndex"), Is.EqualTo(6));
    }

    [Test]
    public void NightCyclingWrapsEquipmentOrderSkipsEmptyAndInvalidWithoutChangingGeneralCycle()
    {
        Day(); var session = StartOwner(); var selection = Selection(session);
        Call(xp, "AddExperience", 100); // inventory 2 unlocked, deliberately unequipped
        Call(spells, "SelectSlot", 0);
        Assert.That(Call(selection, "TryCycleSpell", 1), Is.EqualTo(false));
        Assert.That(Call(spells, "GetSelectedSlotIndex"), Is.Zero);
        Night();
        Assert.That(Call(selection, "TryCycleSpell", 1), Is.EqualTo(true));
        Assert.That(Call(spells, "GetSelectedSlotIndex"), Is.EqualTo(1));
        Assert.That(Call(selection, "TryCycleSpell", 1), Is.EqualTo(true));
        Assert.That(Call(spells, "GetSelectedSlotIndex"), Is.Zero);
        Assert.That(Call(selection, "TryCycleSpell", -1), Is.EqualTo(true));
        Assert.That(Call(spells, "GetSelectedSlotIndex"), Is.EqualTo(1));
        Set(secondSpell, "manaCost", 999);
        Assert.That(Call(selection, "TryCycleSpell", -1), Is.EqualTo(true));
        Assert.That(Call(spells, "GetSelectedSlotIndex"), Is.Zero);
        Assert.That(Call(selection, "TryCycleSpell", 0), Is.EqualTo(false));
        // General inventory APIs still traverse unlocked, unequipped storage.
        Call(spells, "SelectSlot", 1); Call(spells, "CycleSpell", 1);
        Assert.That(Call(spells, "GetSelectedSlotIndex"), Is.EqualTo(2));
    }

    [Test]
    public void MouseSelectionAndDragSwapStillUseOriginalInventoryContracts()
    {
        Day(); var session = StartOwner(); var selection = Selection(session);
        Call(seeds, "TryGrantUnlockedPlant", Plant(7), 3);
        Call(seeds, "TryGrantUnlockedPlant", Plant(3), 4);
        var original = Call(seeds, "GetPlantSlot", 0);
        var destination = Call(seeds, "GetPlantSlot", 5);
        var hud = Component("SeedSlotsUIController", false);
        Set(hud, "seedInventory", seeds);
        int selectedEvents = 0, storageEvents = 0;
        Listen(seeds, "onSlotSelected", (Action<int>)(_ => selectedEvents++));
        Listen(seeds, "onInventoryChanged", (Action)(() => storageEvents++));
        Call(hud, "SelectSlot", 1); // existing public mouse/inventory selection contract
        Assert.That(Call(seeds, "GetSelectedSlotIndex"), Is.EqualTo(1));
        Set(hud, "dragSourceIndex", 0);
        hud.GetType().GetMethod("ExecuteDragSwap", Fields).Invoke(hud, new object[] { 5 });
        Assert.That(Call(seeds, "GetPlantSlot", 5), Is.SameAs(original));
        Assert.That(Call(seeds, "GetPlantSlot", 0), Is.SameAs(destination));
        Assert.That(Call(seeds, "GetSelectedSlotIndex"), Is.EqualTo(5));
        Assert.That(selectedEvents, Is.EqualTo(2)); Assert.That(storageEvents, Is.EqualTo(1));
        Assert.That(Call(seeds, "GetSeedCountInSlot", 5), Is.EqualTo(3));
        Assert.That(Identity(session, "GetCommittedPlant", 0), Is.EqualTo(Seed(7)));
        Call(seeds, "SelectSlot", 1);
        Assert.That(Call(selection, "TrySelectPosition", 0), Is.EqualTo(true));
        Assert.That(Call(seeds, "GetSelectedSlotIndex"), Is.EqualTo(5));
        // No keyboard swap state survives LD-07; HUD remains alive for drag/mouse.
        Assert.That(hud.GetType().GetMethod("OnSlotKeyPressed", Fields), Is.Null);
    }

    private void LoadSerializedSeedInventory()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/SeedInventory.prefab");
        Assert.That(prefab, Is.Not.Null);
        // Clone under an inactive parent: never mutate the asset or run its Start.
        var clone = UnityEngine.Object.Instantiate(prefab, Object("Serialized inventory parent").transform);
        seeds = clone.GetComponent(Production("SeedInventory"));
        Production("SeedInventory").GetField("Instance").SetValue(null, seeds);
        Lifecycle(seeds, "Awake");
    }

    [Test]
    public void SerializedStartingSeedsMatchCanonicalIdentitiesWithoutChangingStockOrOrder()
    {
        LoadSerializedSeedInventory();
        Assert.That(Property(seeds, "PlantSlotsCount"), Is.EqualTo(10));
        foreach (int index in Enumerable.Range(0, 10))
        {
            var slot = Call(seeds, "GetPlantSlot", index);
            if (index < 3)
            {
                Assert.That(Field(slot, "seedType"), Is.EqualTo(Seed(new[] { 14, 7, 3 }[index])));
                Assert.That(Field(slot, "seedCount"), Is.EqualTo(3));
                var data = Field(slot, "data");
                Assert.That(data, Is.Not.Null);
                Assert.That(Field(slot, "seedType"), Is.EqualTo(Field(data, "seedType")), "slot " + index);
                Assert.That(Field(slot, "plantPrefab"), Is.SameAs(Field(data, "plantPrefab")));
            }
            else
            {
                Assert.That(Field(slot, "seedType"), Is.EqualTo(Seed(0)));
                Assert.That(Field(slot, "seedCount"), Is.EqualTo(0));
            }
        }
        Assert.That(Call(seeds, "GetSelectedSlotIndex"), Is.EqualTo(0));
    }

    [Test]
    public void ExistingSerializedStockPreventsAdditionalUnlockEntriesBeforeAndAfterSwap()
    {
        LoadSerializedSeedInventory();
        var slots = Enumerable.Range(0, 10).Select(i => Call(seeds, "GetPlantSlot", i)).ToArray();
        var counts = slots.Select(s => (int)Field(s, "seedCount")).ToArray();
        int changes = 0, selections = 0;
        Listen(seeds, "onInventoryChanged", (Action)(() => changes++));
        Listen(seeds, "onSlotSelected", (Action<int>)(_ => selections++));
        foreach (var slot in slots.Take(3))
            Assert.That(Call(seeds, "TryGrantUnlockedPlant", Field(slot, "data"), 1), Is.EqualTo(true));
        Assert.That(changes, Is.Zero); Assert.That(selections, Is.Zero);
        foreach (int i in Enumerable.Range(0, 10))
        {
            Assert.That(Call(seeds, "GetPlantSlot", i), Is.SameAs(slots[i]));
            Assert.That(Call(seeds, "GetSeedCountInSlot", i), Is.EqualTo(counts[i]));
        }
        Assert.That(Call(seeds, "SwapSlots", 1, 5), Is.EqualTo(true));
        Assert.That(Call(seeds, "TryGrantUnlockedPlant", Field(slots[1], "data"), 1), Is.EqualTo(true));
        Assert.That(changes, Is.EqualTo(1)); Assert.That(selections, Is.Zero);
        Assert.That(Call(seeds, "GetPlantSlot", 5), Is.SameAs(slots[1]));
        Assert.That(Call(seeds, "GetPlantSlot", 1), Is.SameAs(slots[5]));
        Assert.That(Enumerable.Range(0, 10).Sum(i => (int)Call(seeds, "GetSeedCountInSlot", i)), Is.EqualTo(9));
        // A genuinely missing unlocked plant still receives the legitimate grant.
        Assert.That(Call(seeds, "TryGrantUnlockedPlant", Plant(11), 1), Is.EqualTo(true));
        Assert.That(changes, Is.EqualTo(2)); Assert.That(selections, Is.Zero);
        Assert.That(Enumerable.Range(0, 10).Sum(i => (int)Call(seeds, "GetSeedCountInSlot", i)), Is.EqualTo(10));
        Assert.That(Call(seeds, "GetSelectedSlotIndex"), Is.EqualTo(0));
    }

    [Test]
    public void SerializedEquipmentSelectsDarkRootThenThornedAndTracksStorageSwap()
    {
        LoadSerializedSeedInventory();
        Set(xp, "progressionData", AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(
            "Assets/Scriptables/Progression/Experience Progression.asset"));
        Day(); var session = StartOwner(); var selection = Selection(session);
        int events = 0, lastIndex = -1;
        Listen(seeds, "onSlotSelected", (Action<int>)(i => { events++; lastIndex = i; }));
        Assert.That(Identity(session, "GetCommittedPlant", 0), Is.EqualTo(Seed(3)));
        Assert.That(Identity(session, "GetCommittedPlant", 1), Is.EqualTo(Seed(7)));
        Assert.That(Call(selection, "TrySelectPosition", 0), Is.EqualTo(true));
        Assert.That(Call(seeds, "GetSelectedSlotIndex"), Is.EqualTo(2));
        Assert.That(lastIndex, Is.EqualTo(2)); Assert.That(events, Is.EqualTo(1));
        Assert.That(Call(selection, "TrySelectPosition", 1), Is.EqualTo(true));
        Assert.That(Call(seeds, "GetSelectedSlotIndex"), Is.EqualTo(1));
        Assert.That(lastIndex, Is.EqualTo(1)); Assert.That(events, Is.EqualTo(2));
        Assert.That(Call(selection, "TrySelectPosition", 2), Is.EqualTo(false));
        Assert.That(Call(seeds, "GetSelectedSlotIndex"), Is.EqualTo(1));
        Assert.That(events, Is.EqualTo(2));
        Assert.That(Call(seeds, "SwapSlots", 1, 5), Is.EqualTo(true));
        Assert.That(Call(selection, "TrySelectPosition", 1), Is.EqualTo(true));
        Assert.That(Call(seeds, "GetSelectedSlotIndex"), Is.EqualTo(5));
        Assert.That(lastIndex, Is.EqualTo(5)); Assert.That(events, Is.EqualTo(3));
        Assert.That(Call(seeds, "GetSeedCountInSlot", 5), Is.EqualTo(3));
        Assert.That(Identity(session, "GetCommittedPlant", 0), Is.EqualTo(Seed(3)));
        Assert.That(Identity(session, "GetCommittedPlant", 1), Is.EqualTo(Seed(7)));
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
