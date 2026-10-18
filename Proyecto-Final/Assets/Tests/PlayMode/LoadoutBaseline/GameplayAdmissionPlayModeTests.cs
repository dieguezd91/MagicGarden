using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.Tilemaps;

/// <summary>
/// Real Unity coroutines and production entry points in an isolated empty scene.
/// Reflection supplies serialized fixtures across the Assembly-CSharp boundary;
/// no gameplay rule is replaced with a stub. Input events are dispatched explicitly,
/// not claimed as OS-key or animation-clip integration coverage.
/// </summary>
public sealed class GameplayAdmissionPlayModeTests
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
    private readonly Dictionary<FieldInfo, object> statics = new Dictionary<FieldInfo, object>();
    private readonly List<Tuple<EventInfo, Delegate>> tutorialListeners = new List<Tuple<EventInfo, Delegate>>();
    private Component seeds, spells, cycle, flow, ability, caster, mana, owner, input, timer, tiles;
    private GameObject player, plantPrefab;
    private Tilemap map;
    private object session;
    private ScriptableObject fireData, rangeData;
    private float oldScale, oldCapture;
    private int plantedEvents, castEvents;
    private static readonly Vector3Int Cell = Vector3Int.zero;

    private static Type Type(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Single(a => a.GetName().Name == "Assembly-CSharp").GetType(name, true);
    private static object Get(object value, string field) => value.GetType().GetField(field, Flags).GetValue(value);
    private static void Set(object value, string field, object data) => value.GetType().GetField(field, Flags).SetValue(value, data);
    private static object Property(object value, string name) => value.GetType().GetProperty(name).GetValue(value);
    private static object Call(object value, string method, params object[] args) => value.GetType().GetMethods(Flags)
        .Single(m => m.Name == method && m.GetParameters().Length == args.Length &&
            m.GetParameters().Select((p, i) => args[i] == null || p.ParameterType.IsInstanceOfType(args[i])).All(x => x))
        .Invoke(value, args);
    private static object Seed(int identity) => Enum.ToObject(Type("SeedsEnum"), identity);
    private static object Phase(int phase) => Enum.ToObject(Type("GamePhase"), phase);
    private void Emit(string name, params object[] args) => ((Delegate)Get(input, name))?.DynamicInvoke(args);
    private float Mana => (float)Call(mana, "GetCurrentMana");
    private int Stock => (int)Call(seeds, "GetSeedCountInSlot", 0);
    private int Actions => (int)Property(timer, "RemainingActions");
    private float Cooldown(int index) => (float)Get(Call(spells, "GetSpellSlot", index), "currentCooldown");

    private GameObject Object(string name, bool active = false)
    {
        var go = new GameObject("Stage3 " + name); go.SetActive(active); owned.Add(go); return go;
    }
    private Component Inactive(string name, bool awake = true)
    {
        var result = Object(name).AddComponent(Type(name));
        if (awake) Call(result, "Awake");
        return result;
    }
    private ScriptableObject Data(string name)
    {
        var result = ScriptableObject.CreateInstance(Type(name)); owned.Add(result); return result;
    }
    private static IList List(string name, params object[] values)
    {
        var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(Type(name)));
        foreach (var value in values) list.Add(value);
        return list;
    }
    private void Listen(string name, Action listener)
    {
        var ev = Type("TutorialEvents").GetEvent(name);
        ev.AddEventHandler(null, listener); tutorialListeners.Add(Tuple.Create(ev, (Delegate)listener));
    }
    private ScriptableObject SpellData(int index, string kind)
    {
        var data = Data("SpellDataSO");
        var prefab = Object(kind + " prefab");
        var spell = prefab.AddComponent(Type(kind));
        if (kind == "FireSpell")
        {
            Set(spell, "duration", 0.08f); Set(spell, "fireRate", 0.02f);
            Set(spell, "projectileLifeTime", 0.04f);
            prefab.SetActive(true); // FireRoutine needs active runtime clones.
        }
        Set(data, "slotIndex", index); Set(data, "spellName", kind);
        Set(data, "spellType", Enum.ToObject(Type("SpellType"), 1));
        Set(data, "spellPrefab", prefab); Set(data, "manaCost", 7); Set(data, "cooldown", 5f);
        return data;
    }

    [UnitySetUp]
    public IEnumerator Setup()
    {
        // Never run a world-mutating fixture over a user's loaded gameplay scene.
        if (UnityEngine.Object.FindObjectOfType(Type("WorldTransitionAnimator")) != null ||
            UnityEngine.Object.FindObjectOfType(Type("PlayerAbilitySystem")) != null || Camera.main != null)
            Assert.Ignore("Run Stage 3 PlayMode tests from an empty scene, not a loaded gameplay world.");
        oldScale = Time.timeScale; oldCapture = Time.captureDeltaTime;
        Time.timeScale = 1f; Time.captureDeltaTime = 0.02f;
        foreach (string name in new[] { "SeedInventory", "SpellInventory", "DayCycleController", "GameFlowController",
            "TilePlantingSystem", "PlantManager", "SoundManager", "UIManager", "LevelManager", "InventoryManager" })
        {
            var type = Type(name);
            var field = type.GetField("Instance", BindingFlags.Static | BindingFlags.Public)
                ?? type.GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic)
                ?? type.GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            statics.Add(field, field.GetValue(null)); field.SetValue(null, null);
        }
        flow = Inactive("GameFlowController"); cycle = Inactive("DayCycleController");
        seeds = Inactive("SeedInventory"); spells = Inactive("SpellInventory");
        Inactive("PlantManager"); Inactive("SoundManager");
        timer = Inactive("DayTimerController");
        var grid = Object("Grid"); grid.AddComponent<Grid>();
        var tileObject = Object("Tilemap"); tileObject.transform.SetParent(grid.transform);
        map = tileObject.AddComponent<Tilemap>();
        var soil = ScriptableObject.CreateInstance<Tile>(); owned.Add(soil); map.SetTile(Cell, soil);
        tiles = Inactive("TilePlantingSystem"); Set(tiles, "plantingTilemap", map); Set(tiles, "allowedPlantTile", soil);
        var cameraObject = Object("Camera"); cameraObject.tag = "MainCamera";
        var camera = cameraObject.AddComponent<Camera>(); camera.orthographic = true;
        camera.transform.position = new Vector3(0, 0, -10); cameraObject.SetActive(true);
        var inputObject = Object("Input"); input = inputObject.AddComponent(Type("InputReader"));
        ((Behaviour)input).enabled = false; inputObject.SetActive(true);
        Set(input, "<MouseScreenPosition>k__BackingField", (Vector2)camera.WorldToScreenPoint(map.GetCellCenterWorld(Cell)));
        plantPrefab = Object("plant prefab"); plantPrefab.AddComponent(Type("Plant"));
        var plant = Data("PlantDataSO"); Set(plant, "seedType", Seed(7)); Set(plant, "plantPrefab", plantPrefab);
        var otherPlant = Data("PlantDataSO"); Set(otherPlant, "seedType", Seed(14)); Set(otherPlant, "plantPrefab", plantPrefab);
        fireData = SpellData(4, "FireSpell"); rangeData = SpellData(1, "BasicRangeSpell");
        var progression = Data("ExperienceProgressionDataSO");
        // Unlocks populate the catalog/storage, never equipment. This admission
        // fixture explicitly commits the identities needed by its existing action tests.
        var level = Activator.CreateInstance(Type("ExperienceLevelData"), 100,
            List("PlantDataSO", plant, otherPlant), List("SpellDataSO", fireData, rangeData));
        Set(progression, "levelRequirements", List("ExperienceLevelData", level));
        player = Object("Player");
        var xp = player.AddComponent(Type("PlayerExperienceSystem")); Set(xp, "progressionData", progression);
        mana = player.AddComponent(Type("ManaSystem")); ((Behaviour)mana).enabled = false; // no regeneration obscures exact payments
        owner = player.AddComponent(Type("PlayerContentUnlockSystem"));
        Set(owner, "spellInventory", spells); Set(owner, "playerExperienceSystem", xp);
        ability = player.AddComponent(Type("PlayerAbilitySystem")); Set(ability, "input", input);
        Set(ability, "seedInventory", seeds); Set(ability, "plantManaCost", 10f);
        Set(ability, "interactionAnimationDuration", 0.2f); Set(ability, "interactionDistance", 100f);
        caster = player.AddComponent(Type("PlayerSpellController")); Set(caster, "firePoint", player.transform);
        player.SetActive(true); // Real Awake/OnEnable subscriptions, followed by Start.
        yield return null;
        session = Property(owner, "Loadout");
        Call(cycle, "StartDay");
        for (int i = 0; i < 3; i++)
        {
            Assert.That(Property(Call(session, "GetCommittedPlant", i), "HasValue"), Is.False);
            Assert.That(Property(Call(session, "GetCommittedSpell", i), "HasValue"), Is.False);
        }
        Assert.That(Call(session, "TrySetDraftPlant", 0, Seed(7)), Is.EqualTo(true));
        Assert.That(Call(session, "TrySetDraftSpell", 0, fireData), Is.EqualTo(true));
        Assert.That(Call(session, "TrySetDraftSpell", 1, rangeData), Is.EqualTo(true));
        Assert.That(Call(session, "TryConfirm"), Is.EqualTo(true));
        Call(seeds, "TryGrantUnlockedPlant", plant, 3);
        Call(seeds, "TryGrantUnlockedPlant", otherPlant, 2);
        Set(ability, "dayTimerController", timer);
        Call(seeds, "SelectSlot", 0);
        Call(ability, "SetAbility", Enum.ToObject(Type("PlayerAbility"), 1));
        Listen("OnProductionPlantPlanted", () => plantedEvents++);
        Listen("OnSpellCasted", () => castEvents++);
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        if (statics.Count == 0) yield break; // Setup ignored before acquiring isolated state.
        try
        {
            foreach (var listener in tutorialListeners) listener.Item1.RemoveEventHandler(null, listener.Item2);
            tutorialListeners.Clear();
            // Clones may be inactive; only destroy fixtures with the unique Stage3 prefix.
            foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
                if (go != null && go.scene.IsValid() && go.name.StartsWith("Stage3 ")) UnityEngine.Object.Destroy(go);
            foreach (var value in owned) if (value != null && !(value is GameObject)) UnityEngine.Object.Destroy(value);
            yield return null;
        }
        finally
        {
            owned.Clear();
            foreach (var entry in statics) entry.Key.SetValue(null, entry.Value);
            statics.Clear(); Time.timeScale = oldScale; Time.captureDeltaTime = oldCapture;
            plantedEvents = 0; castEvents = 0;
        }
    }

    private void StartPlant() => Emit("OnPrimaryPressed");
    private void AssertNoPlant(int stock, float manaValue)
    {
        Assert.That(Call(tiles, "GetPlantAt", Cell), Is.Null);
        Assert.That(Stock, Is.EqualTo(stock)); Assert.That(Mana, Is.EqualTo(manaValue));
        Assert.That(Actions, Is.EqualTo(10)); Assert.That(plantedEvents, Is.Zero);
    }

    [UnityTest]
    public IEnumerator EquippedPlantRetainsEarlyPaymentConsumptionAndTutorialEvent()
    {
        StartPlant(); Assert.That(Mana, Is.EqualTo(90f)); Assert.That(Stock, Is.EqualTo(3));
        yield return new WaitForSeconds(0.12f);
        var planted = Call(tiles, "GetPlantAt", Cell) as Component;
        Assert.That(planted, Is.Not.Null); Assert.That(Stock, Is.EqualTo(2));
        Assert.That(Mana, Is.EqualTo(90f)); Assert.That(Actions, Is.EqualTo(9)); Assert.That(plantedEvents, Is.EqualTo(1));
        Call(cycle, "StartNight"); Call(cycle, "StartDay");
        Call(session, "TrySetDraftPlant", 0, Seed(0)); Call(session, "TryConfirm");
        Assert.That(Call(tiles, "GetPlantAt", Cell), Is.SameAs(planted));
    }

    [UnityTest]
    public IEnumerator UnequippedAndDepletedPlantsNeverStartOrPayAndRestockRestoresUsability()
    {
        Call(seeds, "SelectSlot", 1); StartPlant(); AssertNoPlant(3, 100f);
        Call(seeds, "SelectSlot", 0);
        Call(seeds, "ConsumeSeedInSelectedSlot"); Call(seeds, "ConsumeSeedInSelectedSlot"); Call(seeds, "ConsumeSeedInSelectedSlot");
        StartPlant(); AssertNoPlant(0, 100f);
        var data = Data("PlantDataSO"); Set(data, "seedType", Seed(7)); Set(data, "plantPrefab", plantPrefab);
        Call(seeds, "TryGrantUnlockedPlant", data, 3); StartPlant();
        yield return new WaitForSeconds(0.12f);
        Assert.That(Call(tiles, "GetPlantAt", Cell), Is.Not.Null); Assert.That(Stock, Is.EqualTo(2));
        Assert.That(Mana, Is.EqualTo(90f)); Assert.That(plantedEvents, Is.EqualTo(1));
    }

    [UnityTest]
    public IEnumerator PhaseChangeDuringPlantAnimationRestoresOnlyPaymentAndHasNoSuccessEffects()
    {
        StartPlant(); Assert.That(Mana, Is.EqualTo(90f)); Call(cycle, "StartNight");
        yield return new WaitForSeconds(0.12f);
        AssertNoPlant(3, 100f); Assert.That(Call(ability, "IsBusy"), Is.EqualTo(false));
    }

    [UnityTest]
    public IEnumerator StorageSwapDuringPlantAnimationCannotConsumeAnotherSeedOrSpawnCapturedPlant()
    {
        StartPlant(); Call(seeds, "SwapSlots", 0, 1);
        yield return new WaitForSeconds(0.12f);
        AssertNoPlant(2, 100f);
        Assert.That(Call(seeds, "GetSeedCountInSlot", 1), Is.EqualTo(3));
        Assert.That(Property(Call(session, "GetCommittedPlant", 0), "Identity"), Is.EqualTo(Seed(7)));
    }

    [UnityTest]
    public IEnumerator OrdinaryAbilityCancellationAndInvalidSoilRetainOriginalManaCost()
    {
        StartPlant(); Call(ability, "SetAbility", Enum.ToObject(Type("PlayerAbility"), 3));
        yield return new WaitForSeconds(0.12f); AssertNoPlant(3, 90f);
        Call(ability, "SetAbility", Enum.ToObject(Type("PlayerAbility"), 1));
        map.SetTile(Cell, null); StartPlant();
        yield return new WaitForSeconds(0.12f); AssertNoPlant(3, 80f);
    }

    [UnityTest]
    public IEnumerator SelectingAnotherEquippedPlantRetainsCancellationCost()
    {
        Call(cycle, "StartNight"); Call(cycle, "StartDay");
        Assert.That(Call(session, "TrySetDraftPlant", 1, Seed(14)), Is.EqualTo(true));
        Assert.That(Call(session, "TryConfirm"), Is.EqualTo(true));
        StartPlant(); Assert.That(Mana, Is.EqualTo(90f));
        Call(seeds, "SelectSlot", 1);
        yield return new WaitForSeconds(0.12f);
        AssertNoPlant(3, 90f);
        Assert.That(Call(seeds, "GetSeedCountInSlot", 1), Is.EqualTo(2));
        Assert.That(Call(ability, "IsBusy"), Is.EqualTo(false));
    }

    [UnityTest]
    public IEnumerator DepletionDuringWindupRejectsAndRestoresPayment()
    {
        StartPlant(); Assert.That(Mana, Is.EqualTo(90f));
        for (int i = 0; i < 3; i++) Call(seeds, "ConsumeSeedInSelectedSlot");
        yield return new WaitForSeconds(0.12f);
        AssertNoPlant(0, 100f);
        Assert.That(Call(seeds, "GetSeedCountInSlot", 1), Is.EqualTo(2));
        Assert.That(Call(ability, "IsBusy"), Is.EqualTo(false));
    }

    [UnityTest]
    public IEnumerator AdmissionRefundPreservesOtherSpendingAndCannotRepeat()
    {
        StartPlant(); Assert.That(Call(mana, "UseMana", 7f), Is.EqualTo(true));
        Call(cycle, "StartNight");
        yield return new WaitForSeconds(0.12f);
        AssertNoPlant(3, 93f);
        Assert.That(Call(mana, "UseMana", 4f), Is.EqualTo(true));
        Call(ability, "SetAbility", Enum.ToObject(Type("PlayerAbility"), 3));
        yield return new WaitForSeconds(0.12f);
        AssertNoPlant(3, 89f);
    }

    [UnityTest]
    public IEnumerator SuccessfulSpellUsesOriginalInventorySlotManaAndCastEvent()
    {
        Call(cycle, "StartNight"); Call(spells, "SelectSlot", 1);
        Call(caster, "ShootFromHand");
        Assert.That(Mana, Is.EqualTo(93f)); Assert.That(Cooldown(1), Is.EqualTo(5f));
        Assert.That(Cooldown(4), Is.Zero); Assert.That(castEvents, Is.EqualTo(1));
        var clones = Resources.FindObjectsOfTypeAll<GameObject>().Where(go => go.name == "Stage3 BasicRangeSpell prefab(Clone)").ToArray();
        Assert.That(clones.Length, Is.EqualTo(1));
        Assert.That(Property(clones[0].GetComponent(Type("Spell")), "UpgradeTargetId"), Is.EqualTo("spell:1"));
        yield return null;
    }

    [UnityTest]
    public IEnumerator SpellExecutionRechecksPhaseEquipmentCooldownAndManaWithoutSuccessEffects()
    {
        Call(spells, "SelectSlot", 1); Call(caster, "ShootFromHand"); // Day
        Assert.That(Mana, Is.EqualTo(100f)); Assert.That(castEvents, Is.Zero);
        Call(cycle, "StartNight"); Call(spells, "StartCooldown", 1);
        Call(caster, "ShootFromHand"); Assert.That(Mana, Is.EqualTo(100f)); Assert.That(Cooldown(1), Is.EqualTo(5f));
        Set(Call(spells, "GetSpellSlot", 1), "currentCooldown", 0f);
        Call(mana, "SetMana", 1f); Call(caster, "ShootFromHand"); Assert.That(Mana, Is.EqualTo(1f));
        Call(mana, "SetMana", 100f);
        // Valid at animation start/readiness, then equipment changes before event.
        Assert.That(Call(caster, "CanCastSpell"), Is.EqualTo(true));
        Call(cycle, "StartDay"); Call(session, "TrySetDraftSpell", 1, null); Call(session, "TryConfirm"); Call(cycle, "StartNight");
        Emit("OnPrimaryPressed"); // Rejected before touching a hand Animator.
        Call(caster, "ShootFromHand");
        Assert.That(Mana, Is.EqualTo(100f)); Assert.That(Cooldown(1), Is.Zero); Assert.That(Cooldown(4), Is.Zero);
        Assert.That(castEvents, Is.Zero);
        Assert.That(Resources.FindObjectsOfTypeAll<GameObject>().Count(go => go.name == "Stage3 BasicRangeSpell prefab(Clone)"), Is.Zero);
        yield return null;
    }

    [UnityTest]
    public IEnumerator FireCompletionKeepsOriginalCooldownAfterSelectionAndEquipmentChange()
    {
        Call(cycle, "StartNight"); Call(spells, "SelectSlot", 4); Call(caster, "ShootFromHand");
        Assert.That(Mana, Is.EqualTo(93f)); Assert.That(Cooldown(4), Is.Zero); Assert.That(castEvents, Is.EqualTo(1));
        Call(spells, "SelectSlot", 1);
        Call(cycle, "StartDay"); Call(session, "TrySetDraftSpell", 0, null); Call(session, "TryConfirm"); Call(cycle, "StartNight");
        yield return new WaitForSeconds(0.24f);
        Assert.That(Cooldown(4), Is.EqualTo(5f)); Assert.That(Cooldown(1), Is.Zero);
        Assert.That(Call(spells, "GetSelectedSlotIndex"), Is.EqualTo(1)); Assert.That(castEvents, Is.EqualTo(1));
    }

    [UnityTest]
    public IEnumerator DayNumbersAddressOnlyThreeEquipmentPositionsRegardlessOfStorageCapacity()
    {
        // Explicitly equip the second plant in position 3, leaving position 2 empty.
        Call(cycle, "StartNight"); Call(cycle, "StartDay");
        Assert.That(Call(session, "TrySetDraftPlant", 2, Seed(14)), Is.EqualTo(true));
        Assert.That(Call(session, "TryConfirm"), Is.EqualTo(true));
        int events = 0, lastIndex = -1;
        var selected = seeds.GetType().GetField("onSlotSelected");
        selected.SetValue(seeds, Delegate.Combine((Delegate)selected.GetValue(seeds),
            (Action<int>)(i => { events++; lastIndex = i; })));
        var initial = (Array)Get(seeds, "plantSlots");
        foreach (int capacity in new[] { 3, 9 })
        {
            var entries = Array.CreateInstance(Type("PlantSlot"), capacity);
            for (int i = 0; i < capacity; i++)
                entries.SetValue(i < initial.Length ? initial.GetValue(i) : Activator.CreateInstance(Type("PlantSlot")), i);
            Set(seeds, "plantSlots", entries);
            int before = events;
            foreach (int position in new[] { 0, 2, 1 })
            {
                Emit("OnSlotSelected", position);
                int expected = position == 0 ? 0 : position == 2 ? 1 : -1;
                Assert.That(Call(seeds, "GetSelectedSlotIndex"), Is.EqualTo(expected));
                Assert.That(lastIndex, Is.EqualTo(expected));
            }
            Assert.That(events, Is.EqualTo(before + 3));
            Emit("OnSlotSelected", 0); // Invalid numbers must retain a usable previous selection.
            int afterValid = events;
            foreach (int invalid in new[] { -1, 3, 4, 5, 6, 7, 8, 9, 20 }) Emit("OnSlotSelected", invalid);
            Assert.That(events, Is.EqualTo(afterValid));
            Assert.That(Call(seeds, "GetSelectedSlotIndex"), Is.Zero);
            Assert.That(Property(Call(session, "GetCommittedPlant", 0), "Identity"), Is.EqualTo(Seed(7)));
            Assert.That(Property(Call(session, "GetCommittedPlant", 2), "Identity"), Is.EqualTo(Seed(14)));
            Assert.That(Stock, Is.EqualTo(3)); Assert.That(Call(seeds, "GetSeedCountInSlot", 1), Is.EqualTo(2));
            Assert.That(Mana, Is.EqualTo(100f)); Assert.That(Actions, Is.EqualTo(10));
            Assert.That(plantedEvents, Is.Zero); Assert.That(castEvents, Is.Zero);
            Assert.That(Cooldown(1), Is.Zero); Assert.That(Cooldown(4), Is.Zero);
        }
        yield return null;
    }

    [UnityTest]
    public IEnumerator DayNumbersResolveSwappedIdentityAndUnavailableSessionCannotMutateSelectionOrAuthorizePlanting()
    {
        var original = Call(seeds, "GetPlantSlot", 0);
        var other = Call(seeds, "GetPlantSlot", 1);
        Assert.That(Call(seeds, "SwapSlots", 0, 1), Is.EqualTo(true));
        Emit("OnSlotSelected", 0);
        Assert.That(Call(seeds, "GetSelectedSlotIndex"), Is.EqualTo(1));
        Assert.That(Call(seeds, "GetSelectedPlantSlot"), Is.SameAs(original));
        Assert.That(Property(Call(session, "GetCommittedPlant", 0), "Identity"), Is.EqualTo(Seed(7)));
        Call(session, "Dispose");
        int events = 0;
        var selected = seeds.GetType().GetField("onSlotSelected");
        selected.SetValue(seeds, Delegate.Combine((Delegate)selected.GetValue(seeds), (Action<int>)(_ => events++)));
        foreach (int position in new[] { 0, 1, 2, 3, 8 }) Emit("OnSlotSelected", position);
        StartPlant(); yield return new WaitForSeconds(0.12f);
        Assert.That(Call(seeds, "GetSelectedSlotIndex"), Is.EqualTo(1)); Assert.That(events, Is.Zero);
        Assert.That(Call(seeds, "GetPlantSlot", 0), Is.SameAs(other));
        Assert.That(Call(seeds, "GetPlantSlot", 1), Is.SameAs(original));
        AssertNoPlant(2, 100f);
        Assert.That(Call(seeds, "GetSeedCountInSlot", 1), Is.EqualTo(3));
        Assert.That(Cooldown(1), Is.Zero); Assert.That(Cooldown(4), Is.Zero); Assert.That(castEvents, Is.Zero);
    }

    [UnityTest]
    public IEnumerator EmptyAndUnavailableEquipmentNumbersClearPreviousRawSelectionAndCannotAuthorizePlanting()
    {
        foreach (int position in new[] { 1, 2 })
        {
            Emit("OnSlotSelected", 0);
            Assert.That(Call(seeds, "GetSelectedSlotIndex"), Is.Zero);
            Emit("OnSlotSelected", position);
            Assert.That(Call(seeds, "GetSelectedSlotIndex"), Is.EqualTo(-1));
            Assert.That(Call(seeds, "GetSelectedPlantSlot"), Is.Null);
            StartPlant(); yield return new WaitForSeconds(0.12f);
            AssertNoPlant(3, 100f);
            Assert.That(Call(seeds, "GetSeedCountInSlot", 1), Is.EqualTo(2));
            Assert.That(Call(seeds, "GetSeedCountInSlot", 2), Is.Zero);
            Assert.That(Call(ability, "IsBusy"), Is.EqualTo(false));
        }
        // A committed identity without stock is unavailable, not an alias for raw storage 0.
        Call(seeds, "RemoveSeedFromSlot", 0); Call(seeds, "SelectSlot", 1);
        Emit("OnSlotSelected", 0);
        Assert.That(Call(seeds, "GetSelectedSlotIndex"), Is.EqualTo(-1));
        Assert.That(Property(Call(session, "GetCommittedPlant", 0), "Identity"), Is.EqualTo(Seed(7)));
        StartPlant(); yield return new WaitForSeconds(0.12f);
        AssertNoPlant(0, 100f);
        Assert.That(Call(seeds, "GetSeedCountInSlot", 1), Is.EqualTo(2));
        Assert.That(Cooldown(1), Is.Zero); Assert.That(Cooldown(4), Is.Zero); Assert.That(castEvents, Is.Zero);
    }

    [UnityTest]
    public IEnumerator NumericDispatchIsSingleContextualAndDayCycleStillChangesAbilities()
    {
        int seedEvents = 0, spellEvents = 0;
        var seedEvent = seeds.GetType().GetField("onSlotSelected");
        var prior = (Delegate)seedEvent.GetValue(seeds);
        seedEvent.SetValue(seeds, Delegate.Combine(prior, (Action<int>)(_ => seedEvents++)));
        spells.GetType().GetEvent("onSpellSlotSelected").AddEventHandler(spells, (Action<int>)(_ => spellEvents++));
        Call(seeds, "SwapSlots", 0, 5);
        Emit("OnSlotSelected", 0);
        Assert.That(seedEvents, Is.EqualTo(1)); Assert.That(Call(seeds, "GetSelectedSlotIndex"), Is.EqualTo(5));
        Emit("OnSlotSelected", 3); Emit("OnSlotSelected", 7);
        Assert.That(seedEvents, Is.EqualTo(1));
        Assert.That(Call(seeds, "GetSelectedSlotIndex"), Is.EqualTo(5));
        Assert.That(((Delegate)Get(input, "OnSlotSelected")).GetInvocationList().Length, Is.EqualTo(1));
        Call(ability, "SetAbility", Enum.ToObject(Type("PlayerAbility"), 3)); // Digging
        Emit("OnCycleInput", 1); Assert.That(Property(ability, "CurrentAbility").ToString(), Is.EqualTo("Planting"));
        Emit("OnCycleInput", -1); Assert.That(Property(ability, "CurrentAbility").ToString(), Is.EqualTo("Digging"));
        Call(cycle, "StartNight"); Emit("OnSlotSelected", 0);
        Assert.That(Call(spells, "GetSelectedSlotIndex"), Is.EqualTo(4)); Assert.That(spellEvents, Is.EqualTo(1));
        Emit("OnSlotSelected", 1); Assert.That(Call(spells, "GetSelectedSlotIndex"), Is.EqualTo(1));
        Assert.That(spellEvents, Is.EqualTo(2)); Assert.That(seedEvents, Is.EqualTo(1));
        Assert.That(Call(seeds, "GetSelectedSlotIndex"), Is.EqualTo(5));
        Emit("OnCycleInput", 1);
        Assert.That(Call(spells, "GetSelectedSlotIndex"), Is.EqualTo(4));
        Emit("OnCycleInput", -1);
        Assert.That(Call(spells, "GetSelectedSlotIndex"), Is.EqualTo(1));
        Assert.That(spellEvents, Is.EqualTo(4));
        Assert.That(Property(ability, "CurrentAbility").ToString(), Is.EqualTo("Digging"));
        Assert.That(Mana, Is.EqualTo(100f)); Assert.That(Actions, Is.EqualTo(10));
        Assert.That(Call(seeds, "GetSeedCountInSlot", 5), Is.EqualTo(3));
        Assert.That(Call(seeds, "GetSeedCountInSlot", 1), Is.EqualTo(2));
        Assert.That(Cooldown(1), Is.Zero); Assert.That(Cooldown(4), Is.Zero);
        Assert.That(plantedEvents, Is.Zero); Assert.That(castEvents, Is.Zero);
        yield return null;
    }

    [UnityTest]
    public IEnumerator NumericSelectionRespectsPlayerTutorialBlockAndSubscriptionLifetime()
    {
        var life = player.AddComponent(Type("LifeController"));
        Set(life, "currentHealth", 100f);
        var controller = player.AddComponent(Type("PlayerController"));
        Call(seeds, "SelectSlot", 5);
        Call(controller, "SetTutorialActionBlocked", true);
        Emit("OnSlotSelected", 0);
        Assert.That(Call(seeds, "GetSelectedSlotIndex"), Is.EqualTo(5));
        Call(controller, "SetTutorialActionBlocked", false);
        Emit("OnSlotSelected", 0);
        Assert.That(Call(seeds, "GetSelectedSlotIndex"), Is.EqualTo(0));
        ((Behaviour)ability).enabled = false;
        Assert.That(Get(input, "OnSlotSelected"), Is.Null);
        ((Behaviour)ability).enabled = true;
        Assert.That(((Delegate)Get(input, "OnSlotSelected")).GetInvocationList().Length, Is.EqualTo(1));
        yield return null;
    }

    [UnityTest]
    public IEnumerator TeleportInputRemainsIndependentOfEquipment()
    {
        var teleport = player.AddComponent(Type("PlayerTeleportController"));
        int teleports = 0; Listen("OnTeleportCasted", () => teleports++);
        Call(cycle, "StartNight"); Call(session, "Dispose");
        Emit("OnTeleportPressed");
        Assert.That(teleports, Is.EqualTo(1)); Assert.That(Mana, Is.EqualTo(90f));
        Assert.That(Property(teleport, "CurrentTeleportCooldown"), Is.GreaterThan(0f));
        Assert.That(castEvents, Is.Zero); Assert.That(Cooldown(1), Is.Zero); Assert.That(Cooldown(4), Is.Zero);
        yield return null;
    }
}
