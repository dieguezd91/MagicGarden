using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class DailyLoadoutIntegrationTests
{
    private readonly List<UnityEngine.Object> created = new List<UnityEngine.Object>();
    private readonly Dictionary<PropertyInfo, object> singletons = new Dictionary<PropertyInfo, object>();
    private object flow, days, loadout, spells, seeds, unlocks;
    private GameObject plantPrefab;
    private string plantId;
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies()[Array.FindIndex(
        AppDomain.CurrentDomain.GetAssemblies(), a => a.GetName().Name == "Assembly-CSharp")].GetType(name, true);
    private static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, Fields).Invoke(target, args);
    private static object Get(object target, string property) => target.GetType().GetProperty(property).GetValue(target);
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, Fields).SetValue(target, value);
    private Component Component(string name)
    {
        var go = new GameObject("Loadout isolated fixture"); created.Add(go);
        var c = go.AddComponent(T(name));
        var property = T(name).GetProperty("Instance", BindingFlags.Static | BindingFlags.Public);
        if (property != null && property.CanWrite)
        {
            singletons[property] = property.GetValue(null);
            property.SetValue(null, c);
        }
        return c;
    }
    private ScriptableObject Data(string type)
    {
        var data = ScriptableObject.CreateInstance(T(type)); created.Add(data); return data;
    }
    private void Phase(string phase) => Call(flow, "SetPhase", Enum.Parse(T("GamePhase"), phase));
    [SetUp] public void Setup()
    {
        flow = Component("GameFlowController"); days = Component("DayCycleController");
        spells = Component("SpellInventory"); seeds = Component("SeedInventory");
        var slots = (Array)seeds.GetType().GetField("plantSlots", Fields).GetValue(seeds);
        for (int i = 0; i < slots.Length; i++) slots.SetValue(Activator.CreateInstance(T("PlantSlot")), i);
        unlocks = Component("PlayerContentUnlockSystem");
        var experience = ((Component)unlocks).GetComponent(T("PlayerExperienceSystem"));
        var progression = Data("ExperienceProgressionDataSO");
        plantPrefab = new GameObject("Plant fixture prefab"); created.Add(plantPrefab);
        var plant = Data("PlantDataSO");
        var seed = Enum.ToObject(T("SeedsEnum"), 1);
        Set(plant, "seedType", seed); Set(plant, "plantPrefab", plantPrefab); Set(plant, "plantName", "Plant fixture");
        plantId = "plant:" + seed;
        var level = Activator.CreateInstance(T("ExperienceLevelData"));
        var plants = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(T("PlantDataSO"))); plants.Add(plant);
        Set(level, "plantUnlocks", plants);
        var spellData = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(T("SpellDataSO")));
        for (int i = 0; i < 4; i++)
        {
            var data = Data("SpellDataSO");
            Set(data, "slotIndex", i); Set(data, "spellType", Enum.ToObject(T("SpellType"), 1));
            Set(data, "spellPrefab", plantPrefab); Set(data, "cooldown", 8f); Set(data, "spellName", "Spell " + i);
            spellData.Add(data);
        }
        Set(level, "spellUnlocks", spellData);
        var levels = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(T("ExperienceLevelData"))); levels.Add(level);
        Set(progression, "levelRequirements", levels); Set(experience, "progressionData", progression);
        Set(unlocks, "playerExperienceSystem", experience); Set(unlocks, "spellInventory", spells);
        Call(unlocks, "InitializeUnlocks");
        Call(seeds, "TryGrantUnlockedPlant", plant, 2);
        loadout = Component("DailyLoadoutController");
        Set(loadout, "spells", spells); Set(loadout, "seeds", seeds); Set(loadout, "unlocks", unlocks);
        Set(loadout, "days", days); Set(loadout, "flow", flow); Set(loadout, "player", null); Set(loadout, "pause", null);
        Set(spells, "loadout", loadout); Set(seeds, "loadout", loadout);
        Call(loadout, "Start"); Call(days, "StartDay");
    }
    [TearDown] public void Cleanup()
    {
        if (loadout != null) Call(loadout, "OnDestroy");
        for (int i = created.Count - 1; i >= 0; i--) if (created[i] != null) UnityEngine.Object.DestroyImmediate(created[i]);
        foreach (var entry in singletons) entry.Key.SetValue(null, entry.Value);
        created.Clear(); singletons.Clear();
    }
    private void Equip()
    {
        Assert.That(Call(loadout, "TryEdit", true, 0, "spell:0"), Is.True);
        Assert.That(Call(loadout, "TryEdit", false, 0, plantId), Is.True);
        Assert.That(Call(loadout, "TryConfirm"), Is.True);
    }
    [Test] public void DayGenerationIgnoresDuplicatesAndWeekWrapButResetsForNewRun()
    {
        Equip(); int day = (int)Get(loadout, "Day");
        Call(days, "StartDay"); Assert.That(Get(loadout, "IsConfirmed"), Is.True);
        Call(days, "RequestWeekReset"); Call(days, "StartNight"); Call(days, "StartNight");
        Call(days, "StartDay"); Assert.That(Get(loadout, "Day"), Is.EqualTo(day + 1));
        Assert.That(Get(loadout, "IsConfirmed"), Is.False);
        Assert.That(Call(loadout, "TryConfirm"), Is.True);
        Call(days, "StartDay"); Assert.That(Get(loadout, "IsConfirmed"), Is.True);
        Call(days, "ResetDayCount");
        Assert.That(Get(loadout, "IsConfirmed"), Is.False);
        Assert.That(Call(loadout, "GetEquipped", true, 0), Is.Null);
    }
    [Test] public void SelectionAndPublicCastingPredicateRespectEquipmentAndAllPhases()
    {
        Call(spells, "SelectSlot", 0); Assert.That(Call(spells, "GetSelectedSlotIndex"), Is.EqualTo(-1));
        Equip(); Call(spells, "SelectSlot", 0); Assert.That(Call(spells, "CanCastSelectedSpell"), Is.False);
        Call(days, "StartNight"); Call(spells, "SelectSlot", 3);
        Assert.That(Call(spells, "GetSelectedSlotIndex"), Is.EqualTo(-1));
        Call(spells, "SelectSlot", 0); Assert.That(Call(spells, "CanCastSelectedSpell"), Is.True);
        Call(spells, "StartCooldown", 0); Assert.That(Call(spells, "CanCastSelectedSpell"), Is.False);
        foreach (string phase in new[] { "Day", "None", "GameOver", "MainMenu", "OnRitual", "LevelUp" })
        {
            Phase(phase);
            Assert.That(Call(loadout, "CanUse", true, "spell:0"), Is.False, phase);
            Assert.That(Call(spells, "CanCastSelectedSpell"), Is.False, phase);
        }
        Assert.That(Get(loadout, "IsConfirmed"), Is.True, "Active selection never spends a new daily confirmation");
    }
    [Test] public void SeedConsumptionAndStorageSwapsNeverRewriteEquipment()
    {
        Equip(); Call(seeds, "SelectSlot", 0);
        Assert.That(Call(seeds, "SwapSlots", 0, 2), Is.True);
        Assert.That(Call(seeds, "GetSelectedSlotIndex"), Is.EqualTo(2));
        Call(seeds, "ConsumeSeedInSelectedSlot"); Call(seeds, "ConsumeSeedInSelectedSlot");
        Assert.That(Call(loadout, "GetEquipped", false, 0), Is.EqualTo(plantId));
        Assert.That(Call(loadout, "CanUse", false, plantId), Is.False);
        Assert.That(Call(seeds, "GetSeedCountInSlot", 2), Is.EqualTo(0));
        Call(days, "StartNight");
        Assert.That(Call(loadout, "CanPlant", plantPrefab), Is.False);
        var planting = Component("TilePlantingSystem"); Set(planting, "loadout", loadout);
        var args = new object[] { Vector3Int.zero, plantPrefab, null };
        Assert.That(Call(planting, "TryPlant", args), Is.False, "Public entry rejects before touching terrain or instantiating");
        Assert.That(args[2], Is.EqualTo("Planting requires an equipped seed during the day."));
        Assert.That(Call(loadout, "GetEquipped", false, 0), Is.EqualTo(plantId));
    }
    [Test] public void LockedItemsAndReopeningCannotMutateConfirmedConfiguration()
    {
        Assert.That(Call(loadout, "TryEdit", true, 0, "spell:locked"), Is.False);
        Equip();
        Assert.That(Call(loadout, "TryEdit", true, 0, "spell:1"), Is.False);
        Assert.That(Call(loadout, "TryConfirm"), Is.False);
        Assert.That(Call(loadout, "GetDraft", true, 0), Is.EqualTo("spell:0"));
        Assert.That(Call(loadout, "GetEquipped", true, 0), Is.EqualTo("spell:0"));
    }
    [Test] public void UnifiedSlotPointerEventsRetainTooltipAndTutorialEntry()
    {
        var slot = Component("LoadoutSlotView");
        Assert.That(slot, Is.InstanceOf<UnityEngine.EventSystems.IPointerEnterHandler>());
        Assert.That(slot, Is.InstanceOf<UnityEngine.EventSystems.IPointerExitHandler>());
        int entered = 0, exited = 0;
        Action enter = () => entered++;
        Action exit = () => exited++;
        slot.GetType().GetEvent("Hovered").AddEventHandler(slot, enter);
        slot.GetType().GetEvent("Unhovered").AddEventHandler(slot, exit);
        ((UnityEngine.EventSystems.IPointerEnterHandler)slot).OnPointerEnter(null);
        Assert.That(entered, Is.EqualTo(1));
        ((UnityEngine.EventSystems.IPointerExitHandler)slot).OnPointerExit(null);
        Assert.That(exited, Is.EqualTo(1));
        ((UnityEngine.EventSystems.IPointerEnterHandler)slot).OnPointerEnter(null);
        Call(slot, "OnDisable");
        Assert.That(exited, Is.EqualTo(2), "Disabling a hovered slot must hide its tooltip");
        Call(slot, "OnDisable");
        Assert.That(exited, Is.EqualTo(2), "Repeated disable must not duplicate hover exits");
    }

    [Test] public void SerializedPrefabHasSixHudAndSixDraftSlotsAndGameSceneReferencesIt()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/DailyLoadout.prefab");
        Assert.That(prefab, Is.Not.Null);
        var ui = prefab.GetComponent(T("DailyLoadoutUI"));
        Assert.That(Get(ui, "IsConfigured"), Is.True);
        foreach (string field in new[] { "hudSlots", "draftSlots" })
            Assert.That(((Array)ui.GetType().GetField(field, Fields).GetValue(ui)).Length, Is.EqualTo(6));
        string guid = AssetDatabase.AssetPathToGUID("Assets/Prefabs/UI/DailyLoadout.prefab");
        Assert.That(System.IO.File.ReadAllText("Assets/Scenes/Game.unity"), Does.Contain(guid));
    }
}
