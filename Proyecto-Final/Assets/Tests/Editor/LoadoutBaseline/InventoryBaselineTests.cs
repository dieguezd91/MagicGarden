using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;

// Assembly-CSharp cannot be referenced by an asmdef. Reflection is confined to
// fixture construction/lifecycle dispatch; assertions observe existing public APIs.
public sealed class InventoryBaselineTests
{
    private readonly List<UnityEngine.Object> created = new List<UnityEngine.Object>();
    private readonly Dictionary<FieldInfo, object> singletons = new Dictionary<FieldInfo, object>();
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private static Type Production(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Single(a => a.GetName().Name == "Assembly-CSharp").GetType(name, true);

    [SetUp]
    public void IsolateSingletons()
    {
        foreach (string name in new[] { "SeedInventory", "SpellInventory", "GameFlowController", "DayCycleController" })
        {
            var type = Production(name);
            var field = type.GetField("Instance", BindingFlags.Public | BindingFlags.Static)
                ?? type.GetField("<Instance>k__BackingField", BindingFlags.NonPublic | BindingFlags.Static);
            singletons.Add(field, field.GetValue(null));
            field.SetValue(null, null);
        }
    }

    [TearDown]
    public void RestoreSingletons()
    {
        try
        {
            for (int i = created.Count - 1; i >= 0; i--)
                if (created[i] != null) UnityEngine.Object.DestroyImmediate(created[i]);
        }
        finally
        {
            created.Clear();
            foreach (var saved in singletons) saved.Key.SetValue(null, saved.Value);
            singletons.Clear();
        }
    }

    private GameObject Object(string name)
    {
        var go = new GameObject(name);
        go.SetActive(false); // No Start, scene discovery, coroutines or automatic input.
        created.Add(go);
        return go;
    }

    private Component Component(string name, bool awake = true)
    {
        var component = Object("Baseline " + name).AddComponent(Production(name));
        if (awake) Lifecycle(component, "Awake");
        return component;
    }

    private ScriptableObject Data(string name)
    {
        var data = ScriptableObject.CreateInstance(Production(name));
        created.Add(data);
        return data;
    }

    private static object Get(object value, string field) => value.GetType().GetField(field, Fields).GetValue(value);
    private static void Set(object value, string field, object data) => value.GetType().GetField(field, Fields).SetValue(value, data);
    private static object Property(object value, string name) => value.GetType().GetProperty(name).GetValue(value);
    private static object EnumValue(string name, int value) => Enum.ToObject(Production(name), value);
    private static void Lifecycle(object value, string method) => value.GetType().GetMethod(method, Fields).Invoke(value, null);

    private static object Call(object value, string method, params object[] args)
    {
        var candidate = value.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Single(m => m.Name == method && m.GetParameters().Length == args.Length &&
                m.GetParameters().Select((p, i) => args[i] == null || p.ParameterType.IsInstanceOfType(args[i])).All(x => x));
        return candidate.Invoke(value, args);
    }

    private static void Listen(object value, string name, Delegate listener)
    {
        var ev = value.GetType().GetEvent(name);
        if (ev != null) ev.AddEventHandler(value, listener);
        else
        {
            var field = value.GetType().GetField(name);
            field.SetValue(value, Delegate.Combine((Delegate)field.GetValue(value), listener));
        }
    }

    private ScriptableObject Plant(int identity)
    {
        var data = Data("PlantDataSO");
        Set(data, "seedType", EnumValue("SeedsEnum", identity));
        Set(data, "plantPrefab", Object("Plant prefab " + identity));
        Set(data, "plantName", "Plant " + identity);
        return data;
    }

    private ScriptableObject Spell(int index, int type = 1, float cooldown = 10)
    {
        var data = Data("SpellDataSO");
        Set(data, "slotIndex", index);
        Set(data, "spellType", EnumValue("SpellType", type));
        Set(data, "spellName", "Spell " + index);
        Set(data, "spellPrefab", Object("Spell prefab " + index));
        Set(data, "manaCost", 7);
        Set(data, "cooldown", cooldown);
        return data;
    }

    [Test]
    public void SeedSelectionRepeatsEventsAndInvalidIndicesDoNothing()
    {
        var seeds = Component("SeedInventory");
        var selected = new List<int>();
        int changed = 0;
        Listen(seeds, "onSlotSelected", (Action<int>)selected.Add);
        Listen(seeds, "onInventoryChanged", (Action)(() => changed++));
        Call(seeds, "SelectSlot", 2);
        Call(seeds, "SelectSlot", 2);
        Call(seeds, "SelectSlot", -1);
        Call(seeds, "SelectSlot", (int)Property(seeds, "PlantSlotsCount"));
        Assert.That(selected, Is.EqualTo(new[] { 2, 2 }));
        Assert.That(Call(seeds, "GetSelectedSlotIndex"), Is.EqualTo(2));
        Assert.That(Call(seeds, "GetSelectedPlantSlot"), Is.SameAs(Call(seeds, "GetPlantSlot", 2)));
        Assert.That(changed, Is.Zero);
    }

    [Test]
    public void SeedConsumptionDepletesOnceAndRestockingRestoresPublicUsability()
    {
        var seeds = Component("SeedInventory");
        var plant = Plant(1);
        Assert.That(Call(seeds, "TryGrantUnlockedPlant", plant, 2), Is.EqualTo(true));
        int changed = 0, selected = 0;
        Listen(seeds, "onInventoryChanged", (Action)(() => changed++));
        Listen(seeds, "onSlotSelected", (Action<int>)(_ => selected++));
        Call(seeds, "ConsumeSeedInSelectedSlot");
        Assert.That(Call(seeds, "GetSeedCountInSlot", 0), Is.EqualTo(1));
        Assert.That(Call(seeds, "HasSeedsInSelectedSlot"), Is.EqualTo(true));
        Assert.That(changed, Is.EqualTo(1));
        Call(seeds, "ConsumeSeedInSelectedSlot");
        Assert.That(Call(seeds, "GetSeedCountInSlot", 0), Is.Zero);
        Assert.That(Call(seeds, "HasSeedsInSelectedSlot"), Is.EqualTo(false));
        Assert.That(Call(seeds, "GetSelectedPlantPrefab"), Is.Null);
        Assert.That(Call(seeds, "GetSelectedPlantName"), Is.EqualTo(""));
        Assert.That(Get(Call(seeds, "GetPlantSlot", 0), "seedType"), Is.EqualTo(EnumValue("SeedsEnum", 0)));
        Assert.That(changed, Is.EqualTo(2));
        Call(seeds, "ConsumeSeedInSelectedSlot");
        Assert.That(changed, Is.EqualTo(2));
        Assert.That(Call(seeds, "TryGrantUnlockedPlant", plant, 3), Is.EqualTo(true));
        Assert.That(Call(seeds, "GetSeedCountInSlot", 0), Is.EqualTo(3));
        Assert.That(Call(seeds, "HasSeedsInSelectedSlot"), Is.EqualTo(true));
        Assert.That(Call(seeds, "GetSelectedPlantPrefab"), Is.SameAs(Get(plant, "plantPrefab")));
        Assert.That(changed, Is.EqualTo(3));
        Assert.That(selected, Is.Zero);
        Assert.That(Call(seeds, "GetSelectedSlotIndex"), Is.Zero);
    }

    [Test]
    public void SeedSwapsMoveStorageReferencesButNotSelectionAndRejectWithoutNotifications()
    {
        var seeds = Component("SeedInventory");
        Call(seeds, "TryGrantUnlockedPlant", Plant(1), 2);
        Call(seeds, "TryGrantUnlockedPlant", Plant(2), 5);
        var first = Call(seeds, "GetPlantSlot", 0);
        var second = Call(seeds, "GetPlantSlot", 1);
        int changed = 0, selected = 0;
        Listen(seeds, "onInventoryChanged", (Action)(() => changed++));
        Listen(seeds, "onSlotSelected", (Action<int>)(_ => selected++));
        Assert.That(Call(seeds, "SwapSlots", 0, 1), Is.EqualTo(true));
        Assert.That(Call(seeds, "GetPlantSlot", 0), Is.SameAs(second));
        Assert.That(Call(seeds, "GetPlantSlot", 1), Is.SameAs(first));
        Assert.That(Call(seeds, "GetSeedCountInSlot", 0), Is.EqualTo(5));
        Assert.That(Call(seeds, "GetSeedCountInSlot", 1), Is.EqualTo(2));
        Assert.That(Call(seeds, "GetSelectedSlotIndex"), Is.Zero);
        Assert.That(selected, Is.Zero);
        Assert.That(changed, Is.EqualTo(1));
        Assert.That(Call(seeds, "SwapSlots", 0, 0), Is.EqualTo(false));
        Assert.That(Call(seeds, "SwapSlots", -1, 1), Is.EqualTo(false));
        Assert.That(Call(seeds, "SwapSlots", 0, 99), Is.EqualTo(false));
        Assert.That(changed, Is.EqualTo(1));
        Assert.That(Call(seeds, "GetPlantSlot", 0), Is.SameAs(second));
        Assert.That(Call(seeds, "GetPlantSlot", 1), Is.SameAs(first));
    }

    [Test]
    public void SeedGrantsPreserveExistingStockAndDeferUntilStorageIsFreed()
    {
        var seeds = Component("SeedInventory");
        int capacity = (int)Property(seeds, "PlantSlotsCount");
        var first = Plant(1);
        Call(seeds, "TryGrantUnlockedPlant", first, 2);
        for (int i = 1; i < capacity; i++) Call(seeds, "TryGrantUnlockedPlant", Plant(i + 1), i + 2);
        int changed = 0;
        Listen(seeds, "onInventoryChanged", (Action)(() => changed++));
        var pending = Plant(capacity + 1);
        Assert.That(Call(seeds, "TryGrantUnlockedPlant", first, 50), Is.EqualTo(true));
        Assert.That(Call(seeds, "TryGrantUnlockedPlant", pending, 4), Is.EqualTo(false));
        Assert.That(Call(seeds, "AddSeedsToSlot", 0, 0), Is.EqualTo(false));
        Assert.That(Call(seeds, "AddSeedsToSlot", -1, 1), Is.EqualTo(false));
        Assert.That(Call(seeds, "GetSeedCountInSlot", 0), Is.EqualTo(2));
        Assert.That(changed, Is.Zero);
        Call(seeds, "RemoveSeedFromSlot", 1);
        Assert.That(Call(seeds, "TryGrantUnlockedPlant", pending, 4), Is.EqualTo(true));
        Assert.That(Call(seeds, "GetSeedCountInSlot", 1), Is.EqualTo(4));
        Assert.That(Call(seeds, "AddSeedsToSlot", 1, 3), Is.EqualTo(true));
        Assert.That(Call(seeds, "GetSeedCountInSlot", 1), Is.EqualTo(7));
        Assert.That(changed, Is.EqualTo(3));
    }

    [Test]
    public void SpellSelectionAcceptsLockedSlotsAndCyclingSkipsInvalidEntriesAndWraps()
    {
        var spells = Component("SpellInventory");
        Call(spells, "UnlockSpell", Spell(1));
        Call(spells, "UnlockSpell", Spell(4));
        var selected = new List<int>();
        Listen(spells, "onSpellSlotSelected", (Action<int>)selected.Add);
        Call(spells, "SelectSlot", 0); // Locked selection remains a valid inventory operation.
        Call(spells, "SelectSlot", 0);
        Call(spells, "SelectSlot", -1);
        Call(spells, "SelectSlot", 99);
        Call(spells, "CycleSpell", 0);
        Call(spells, "CycleSpell", 1);
        Call(spells, "CycleSpell", 1);
        Call(spells, "CycleSpell", 1);
        Call(spells, "CycleSpell", -1);
        Assert.That(selected, Is.EqualTo(new[] { 0, 0, 1, 4, 1, 4 }));
        Assert.That(Call(spells, "GetSelectedSpellSlot"), Is.SameAs(Call(spells, "GetSpellSlot", 4)));
        Call(spells, "ResetUnlockedSpells");
        Call(spells, "CycleSpell", 1);
        Assert.That(selected.Count, Is.EqualTo(6));
        Assert.That(Call(spells, "GetSelectedSlotIndex"), Is.EqualTo(4));
    }

    [Test]
    public void SpellUnlockNotifiesSelectionBeforeInventoryAndSelectedEmptySlotDoesNotReselect()
    {
        var spells = Component("SpellInventory");
        var events = new List<string>();
        Listen(spells, "onSpellSlotSelected", (Action<int>)(i => events.Add("select:" + i)));
        Listen(spells, "OnSpellInventoryChanged", (Action)(() => events.Add("inventory")));
        Assert.That(Call(spells, "UnlockSpell", Spell(0)), Is.EqualTo(true));
        Assert.That(events, Is.EqualTo(new[] { "inventory" }));
        Call(spells, "SelectSlot", 2);
        events.Clear();
        Assert.That(Call(spells, "UnlockSpell", Spell(4)), Is.EqualTo(true));
        Assert.That(events, Is.EqualTo(new[] { "select:4", "inventory" }));
    }

    [Test]
    public void CooldownsBelongToOriginalSlotAndNotifyThroughLifecycleUpdate()
    {
        var spells = Component("SpellInventory");
        var data = Spell(1, cooldown: 3600);
        Call(spells, "UnlockSpell", data);
        Call(spells, "UnlockSpell", Spell(4));
        var updates = new List<int>();
        int changed = 0;
        Listen(spells, "onCooldownUpdated", (Action<int>)updates.Add);
        Listen(spells, "OnSpellInventoryChanged", (Action)(() => changed++));
        Call(spells, "StartCooldown", 1);
        Call(spells, "SelectSlot", 4);
        Assert.That(Call(spells, "GetCooldownProgress", 1), Is.EqualTo(1f));
        float before = (float)Get(Call(spells, "GetSpellSlot", 1), "currentCooldown");
        // Inactive fixtures do not receive SendMessage. Dispatch the actual Unity
        // callback explicitly, as for Awake above; real frame advancement is tested
        // separately in SpellCooldownPlayModeTests. EditMode delta may be zero.
        float delta = Time.deltaTime;
        Lifecycle(spells, "Update");
        Assert.That((float)Get(Call(spells, "GetSpellSlot", 1), "currentCooldown"),
            Is.EqualTo(Mathf.Max(0, before - delta)).Within(0.001f));
        Assert.That(Get(Call(spells, "GetSpellSlot", 4), "currentCooldown"), Is.EqualTo(0f));
        Assert.That(updates, Is.EqualTo(new[] { 1, 1 }));
        Assert.That(changed, Is.Zero);
        Assert.That(Call(spells, "GetSelectedSlotIndex"), Is.EqualTo(4));
        Call(spells, "UnlockSpell", data); // Baseline re-unlock resets its cooldown.
        Assert.That(Get(Call(spells, "GetSpellSlot", 1), "currentCooldown"), Is.EqualTo(0f));
        Assert.That(changed, Is.EqualTo(1));
    }

    [Test]
    public void SpellResetRetainsDefinitionsAndSelectionButClearsUnlocksAndCooldowns()
    {
        var spells = Component("SpellInventory");
        var data = Spell(3);
        Call(spells, "UnlockSpell", data);
        Call(spells, "StartCooldown", 3);
        var slot = Call(spells, "GetSpellSlot", 3);
        int changed = 0, selected = 0;
        Listen(spells, "OnSpellInventoryChanged", (Action)(() => changed++));
        Listen(spells, "onSpellSlotSelected", (Action<int>)(_ => selected++));
        Call(spells, "ResetUnlockedSpells");
        Call(spells, "ResetUnlockedSpells");
        Assert.That(Call(spells, "GetSpellSlot", 3), Is.SameAs(slot));
        Assert.That(Get(slot, "isUnlocked"), Is.EqualTo(false));
        Assert.That(Get(slot, "currentCooldown"), Is.EqualTo(0f));
        Assert.That(Get(slot, "spellPrefab"), Is.SameAs(Get(data, "spellPrefab")));
        Assert.That(Get(slot, "manaCost"), Is.EqualTo(7));
        Assert.That(Call(spells, "GetSelectedSlotIndex"), Is.EqualTo(3));
        Assert.That(changed, Is.EqualTo(2));
        Assert.That(selected, Is.Zero);
    }

    private Component Experience(out ScriptableObject progression)
    {
        progression = Data("ExperienceProgressionDataSO");
        var xp = Component("PlayerExperienceSystem", awake: false);
        Set(xp, "progressionData", progression);
        Lifecycle(xp, "Awake");
        return xp;
    }

    [Test]
    public void ExperienceAwardsKeepCumulativeTotalsAndResetNotifiesBeforeProgressionReset()
    {
        var xp = Experience(out _);
        var events = new List<string>();
        Listen(xp, "OnLevelUp", (Action<int>)(i => events.Add("level:" + i)));
        Listen(xp, "OnExperienceChanged", (Action<int, int, int>)((l, x, r) => events.Add(l + ":" + x + ":" + r)));
        Listen(xp, "OnProgressionReset", (Action)(() => events.Add("reset")));
        Call(xp, "AddExperience", 0);
        Call(xp, "AddExperience", -1);
        Assert.That(events, Is.Empty);
        Call(xp, "AddExperience", 450);
        Assert.That(events, Is.EqualTo(new[] { "level:2", "level:3", "level:4", "4:450:750" }));
        Assert.That(Property(xp, "CurrentExperience"), Is.EqualTo(450));
        Assert.That(Property(xp, "PendingChoices"), Is.EqualTo(3));
        events.Clear();
        Call(xp, "ResetProgression");
        Assert.That(events, Is.EqualTo(new[] { "1:0:100", "reset" }));
        Assert.That(Property(xp, "CurrentLevel"), Is.EqualTo(1));
        Assert.That(Property(xp, "CurrentExperience"), Is.Zero);
        Assert.That(Property(xp, "PendingChoices"), Is.Zero);
    }

    [Test]
    public void UnlockRebuildAndLevelUpKeepIdentityOwnershipAndDistinctEventSemantics()
    {
        var spells = Component("SpellInventory");
        var xp = Experience(out var progression);
        var plant1 = Plant(1);
        var plant2 = Plant(2);
        var spell1 = Spell(0);
        var spell2 = Spell(1);
        var levels = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(Production("ExperienceLevelData")));
        IList Plants(params object[] values) => TypedList("PlantDataSO", values);
        IList Spells(params object[] values) => TypedList("SpellDataSO", values);
        levels.Add(Activator.CreateInstance(Production("ExperienceLevelData"), 100, Plants(plant1), Spells(spell1)));
        levels.Add(Activator.CreateInstance(Production("ExperienceLevelData"), 250, Plants(plant1, plant2), Spells(spell1, spell2)));
        Set(progression, "levelRequirements", levels);
        var unlocks = xp.gameObject.AddComponent(Production("PlayerContentUnlockSystem"));
        Set(unlocks, "playerExperienceSystem", xp);
        Set(unlocks, "spellInventory", spells);
        var events = new List<string>();
        Listen(unlocks, "OnUnlockStateRebuilt", (Action)(() => events.Add("rebuild")));
        // These event types contain Assembly-CSharp types: construct compatible delegates.
        var plantHandler = new Action<object>(_ => events.Add("plant"));
        var spellHandler = new Action<object>(_ => events.Add("spell"));
        foreach (var pair in new[] { ("OnPlantUnlocked", plantHandler), ("OnSpellUnlocked", spellHandler) })
        {
            var ev = unlocks.GetType().GetEvent(pair.Item1);
            ev.AddEventHandler(unlocks, Delegate.CreateDelegate(ev.EventHandlerType, pair.Item2.Target, pair.Item2.Method));
        }
        Lifecycle(unlocks, "OnEnable");
        try
        {
            Call(unlocks, "RebuildUnlocksUpToLevel", 1);
            Assert.That(events, Is.EqualTo(new[] { "rebuild" }));
            Assert.That(Call(unlocks, "IsPlantUnlocked", EnumValue("SeedsEnum", 1)), Is.EqualTo(true));
            Assert.That(Call(unlocks, "IsSpellUnlocked", spell1), Is.EqualTo(true));
            events.Clear();
            Call(xp, "AddExperience", 100);
            Assert.That(events, Is.EqualTo(new[] { "plant", "spell" }));
            Assert.That(Call(unlocks, "IsPlantUnlocked", EnumValue("SeedsEnum", 2)), Is.EqualTo(true));
            Assert.That(Call(unlocks, "IsSpellUnlocked", spell2), Is.EqualTo(true));
            Call(xp, "ResetProgression");
            Assert.That(events, Is.EqualTo(new[] { "plant", "spell", "rebuild" }));
            Assert.That(Call(unlocks, "IsPlantUnlocked", EnumValue("SeedsEnum", 2)), Is.EqualTo(false));
            Assert.That(Call(unlocks, "IsSpellUnlocked", spell2), Is.EqualTo(false));
            Assert.That(Get(Call(spells, "GetSpellSlot", 1), "isUnlocked"), Is.EqualTo(false));
        }
        finally { Lifecycle(unlocks, "OnDisable"); }
    }

    private static IList TypedList(string type, object[] values)
    {
        var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(Production(type)));
        foreach (var value in values) list.Add(value);
        return list;
    }

    [Test]
    public void RepeatedDayNotificationsWeekResetAndDayResetHaveDifferentCounterSemantics()
    {
        var flow = Component("GameFlowController");
        var cycle = Component("DayCycleController");
        var days = new List<int>();
        ((UnityEvent<int>)Get(cycle, "OnNewDay")).AddListener(days.Add);
        // Match the phase before each operation so no world/scene transition is invoked.
        Set(flow, "currentPhase", EnumValue("GamePhase", 2));
        Call(cycle, "StartDay");
        Call(cycle, "StartDay");
        Assert.That(days, Is.EqualTo(new[] { 0, 0 }));
        Assert.That(Property(cycle, "CurrentDay"), Is.Zero);
        Set(flow, "currentPhase", EnumValue("GamePhase", 3));
        Call(cycle, "StartNight");
        Call(cycle, "StartNight");
        Assert.That(Property(cycle, "CurrentDay"), Is.EqualTo(2));
        Assert.That(Property(cycle, "TotalNights"), Is.EqualTo(2));
        Call(cycle, "RequestWeekReset");
        Call(cycle, "StartNight");
        Assert.That(Property(cycle, "CurrentDay"), Is.EqualTo(1));
        Assert.That(Property(cycle, "TotalNights"), Is.EqualTo(3));
        Call(cycle, "ResetDayCount");
        Assert.That(Property(cycle, "CurrentDay"), Is.EqualTo(1));
        Assert.That(Property(cycle, "TotalNights"), Is.EqualTo(3));
        Assert.That(days, Is.EqualTo(new[] { 0, 0 }));
        Assert.That(Property(flow, "WorldPhase"), Is.EqualTo(EnumValue("GamePhase", 3)));
    }
}
