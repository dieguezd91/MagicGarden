#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Isolated real-session fixture shared by EditMode and Editor PlayMode tests.</summary>
public sealed class DailyLoadoutUIFixture : IDisposable
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
    private readonly Dictionary<FieldInfo, object> saved = new Dictionary<FieldInfo, object>();
    public Component Flow, Days, Modals, Experience, Unlocks, Presenter, View, Seeds, Spells;
    public object Session;
    public ScriptableObject FirstPlant, SecondPlant, LaterPlant, FirstSpell, SecondSpell, LaterSpell, Progression;
    public GameObject Panel;
    public static Type Production(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Single(a => a.GetName().Name == "Assembly-CSharp").GetType(name, true);
    public static object Property(object value, string name) => value.GetType().GetProperty(name).GetValue(value);
    public static object Field(object value, string name)
    {
        for (var type = value.GetType(); type != null; type = type.BaseType)
        {
            var field = type.GetField(name, Flags);
            if (field != null) return field.GetValue(value);
        }
        throw new MissingFieldException(value.GetType().Name, name);
    }
    public static void Set(object value, string name, object data) => value.GetType().GetField(name, Flags).SetValue(value, data);
    public static object Call(object value, string method, params object[] args) => value.GetType().GetMethods(Flags)
        .Single(m => m.Name == method && m.GetParameters().Length == args.Length &&
            m.GetParameters().Select((p, i) => args[i] == null || p.ParameterType.IsInstanceOfType(args[i])).All(x => x))
        .Invoke(value, args);
    public static object Seed(int identity) => Enum.ToObject(Production("SeedsEnum"), identity);
    public string Status => (string)Property(Presenter, "StatusMessage");
    public bool IsOpen => (bool)Property(Presenter, "IsOpen");
    public bool Confirmed => (bool)Property(Session, "HasConfirmedToday");
    public Component Slot(bool plants, int index) => (Component)((IList)Property(View, plants ? "PlantSlots" : "SpellSlots"))[index];
    public Component Option(int index) => (Component)((IList)Property(View, "OptionViews"))[index];
    public static Button SelectButton(Component view) => (Button)Property(view, "SelectButton");
    public static string Name(Component view) => ((TMP_Text)Field(view, "nameLabel")).text;
    public static string State(Component view) => ((TMP_Text)Field(view, "stateLabel")).text;
    public Button Action(string name) => (Button)Property(View, name + "Button");
    public object Assignment(bool plant, bool committed, int position) => Call(Session,
        "Get" + (committed ? "Committed" : "Draft") + (plant ? "Plant" : "Spell"), position);
    public object Identity(bool plant, bool committed, int position) => Property(Assignment(plant, committed, position), "Identity");
    public bool HasValue(bool plant, bool committed, int position) => (bool)Property(Assignment(plant, committed, position), "HasValue");

    public DailyLoadoutUIFixture()
    {
        try { Setup(); }
        catch { Dispose(); throw; }
    }

    private void Setup()
    {
        if (UnityEngine.Object.FindObjectOfType(Production("WorldTransitionAnimator")) != null ||
            UnityEngine.Object.FindObjectOfType(Production("PlayerAbilitySystem")) != null)
            Assert.Ignore("Run isolated loadout tests from an empty/Menu context, not a loaded gameplay world.");
        foreach (string name in new[] { "GameFlowController", "DayCycleController", "SeedInventory", "SpellInventory", "UIManager", "InventoryManager" })
        {
            var type = Production(name);
            var field = type.GetField("Instance", BindingFlags.Public | BindingFlags.Static)
                ?? type.GetField("<Instance>k__BackingField", BindingFlags.NonPublic | BindingFlags.Static)
                ?? type.GetField("_instance", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(field, Is.Not.Null, name);
            saved.Add(field, field.GetValue(null)); field.SetValue(null, null);
        }
        Flow = Component("GameFlowController"); Days = Component("DayCycleController");
        Modals = Component("UIFlowController", false);
        Seeds = Component("SeedInventory"); Spells = Component("SpellInventory");
        FirstPlant = Plant(7, "First authored plant"); SecondPlant = Plant(3, "Second authored plant");
        LaterPlant = Plant(14, "Later plant");
        FirstSpell = Spell(4, "First authored spell"); SecondSpell = Spell(0, "Second authored spell");
        LaterSpell = Spell(2, "Later spell");
        Progression = Data("ExperienceProgressionDataSO");
        var levels = List("ExperienceLevelData");
        levels.Add(Activator.CreateInstance(Production("ExperienceLevelData"), 100,
            List("PlantDataSO", FirstPlant, SecondPlant, FirstPlant), List("SpellDataSO", FirstSpell, SecondSpell, FirstSpell)));
        levels.Add(Activator.CreateInstance(Production("ExperienceLevelData"), 250,
            List("PlantDataSO", LaterPlant), List("SpellDataSO", LaterSpell)));
        Set(Progression, "levelRequirements", levels);
        Experience = Component("PlayerExperienceSystem", false);
        Set(Experience, "progressionData", Progression); Call(Experience, "Awake");
        Unlocks = Experience.gameObject.AddComponent(Production("PlayerContentUnlockSystem"));
        Set(Unlocks, "playerExperienceSystem", Experience); Set(Unlocks, "spellInventory", Spells);
        Call(Unlocks, "OnEnable"); Call(Unlocks, "Start"); Session = Property(Unlocks, "Loadout");
        Call(Days, "StartDay");
        var holder = Object("Inactive UI fixture parent");
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/DailyLoadout/DailyLoadoutPanel.prefab");
        Assert.That(prefab, Is.Not.Null);
        Panel = UnityEngine.Object.Instantiate(prefab, holder.transform); owned.Add(Panel); Panel.SetActive(false);
        View = Panel.GetComponent(Production("MagicGarden.LoadoutUI.DailyLoadoutPanelView"));
        var icon = (Sprite)Field(Property(View, "OptionPrefab"), "normalSprite");
        foreach (var plant in new[] { FirstPlant, SecondPlant, LaterPlant }) Set(plant, "plantIcon", icon);
        foreach (var spell in new[] { FirstSpell, SecondSpell, LaterSpell }) Set(spell, "spellIcon", icon);
        // Native Editor authoring, on an unsaved isolated instance only. Never alter the user's prefab in tests.
        var authoring = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "Assembly-CSharp-Editor")
            .GetType("DailyLoadoutUIIntegrationAuthoring", true);
        authoring.GetMethod("Configure").Invoke(null, new object[] { View });
        Presenter = Panel.GetComponent(Production("MagicGarden.LoadoutUI.DailyLoadoutPresenter"));
        Call(Presenter, "Initialize", Session, Unlocks, Experience, Flow, Modals);
        holder.SetActive(true);
    }

    public bool Open() => (bool)Call(Presenter, "Open");
    public void Refresh() => Call(Presenter, "Refresh");
    public void Select(bool plant, int position) => SelectButton(Slot(plant, position)).onClick.Invoke();
    public void Assign(int option) => SelectButton(Option(option)).onClick.Invoke();
    public void AdvanceCatalog() { Call(Experience, "AddExperience", 100); Refresh(); }
    public static IList List(string name, params object[] values)
    {
        var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(Production(name)));
        foreach (var value in values) list.Add(value); return list;
    }
    private GameObject Object(string name)
    {
        var go = new GameObject("Stage4B " + name); go.SetActive(false); owned.Add(go); return go;
    }
    private Component Component(string name, bool awake = true)
    {
        var value = Object(name).AddComponent(Production(name)); if (awake) Call(value, "Awake"); return value;
    }
    private ScriptableObject Data(string name)
    {
        var value = ScriptableObject.CreateInstance(Production(name)); owned.Add(value); return value;
    }
    private ScriptableObject Plant(int identity, string name)
    {
        var value = Data("PlantDataSO"); Set(value, "seedType", Seed(identity)); Set(value, "plantName", name);
        Set(value, "plantPrefab", Object(name)); return value;
    }
    private ScriptableObject Spell(int index, string name)
    {
        var value = Data("SpellDataSO"); Set(value, "slotIndex", index); Set(value, "spellName", name);
        Set(value, "spellPrefab", Object(name)); return value;
    }
    public void Dispose()
    {
        try
        {
            if (Presenter != null) Call(Presenter, "Close");
            if (Unlocks != null) { Call(Unlocks, "OnDisable"); Call(Unlocks, "OnDestroy"); }
            for (int i = owned.Count - 1; i >= 0; i--)
                if (owned[i] != null) UnityEngine.Object.DestroyImmediate(owned[i]);
        }
        finally { foreach (var pair in saved) pair.Key.SetValue(null, pair.Value); saved.Clear(); owned.Clear(); }
    }
}
#endif
