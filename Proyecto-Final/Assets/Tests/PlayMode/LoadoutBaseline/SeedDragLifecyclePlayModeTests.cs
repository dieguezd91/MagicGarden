using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;

/// <summary>
/// Public pointer callbacks on equipment HUD and standalone legacy SeedSlots.
/// Real Canvas/EventSystem fixtures exercise callback/rendering contracts, not OS mouse input.
/// Equipment dragging must not mutate storage; standalone storage presentation remains supported.
/// </summary>
public sealed class SeedDragLifecyclePlayModeTests
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
    private readonly Dictionary<FieldInfo, object> statics = new Dictionary<FieldInfo, object>();
    private Component inventory, hud, eventSystem, owner;
    private Component[] slots, icons, labels, storageSlots, storageIcons, storageLabels;
    private object session;
    private ScriptableObject firstPlant, secondPlant;
    private int storageEvents, selectionEvents, selectedEventIndex;
    private int beginEvents, dragEvents, endEvents;
    private static Type Type(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(a => a.GetType(name)).First(t => t != null);
    private static object Field(object value, string name) => value.GetType().GetField(name, Flags).GetValue(value);
    private static void Set(object value, string name, object data) => value.GetType().GetField(name, Flags).SetValue(value, data);
    private static object Property(object value, string name) => value.GetType().GetProperty(name).GetValue(value);
    private static void SetProperty(object value, string name, object data) => value.GetType().GetProperty(name).SetValue(value, data);
    private static object Call(object value, string name, params object[] args) => value.GetType().GetMethods(Flags)
        .Single(m => m.Name == name && m.GetParameters().Length == args.Length &&
            m.GetParameters().Select((p, i) => args[i] == null || p.ParameterType.IsInstanceOfType(args[i])).All(x => x))
        .Invoke(value, args);
    private static object Seed(int id) => Enum.ToObject(Type("SeedsEnum"), id);

    private GameObject Object(string name, Transform parent = null)
    {
        var go = new GameObject("SeedDragRegression " + name, typeof(RectTransform));
        go.SetActive(false); owned.Add(go);
        if (parent != null) go.transform.SetParent(parent, false);
        return go;
    }
    private ScriptableObject Data(string name)
    {
        var data = ScriptableObject.CreateInstance(Type(name)); owned.Add(data); return data;
    }
    private Component Component(string name)
    {
        var value = Object(name).AddComponent(Type(name)); Call(value, "Awake"); return value;
    }
    private static IList List(string name, params object[] values)
    {
        var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(Type(name)));
        foreach (var value in values) list.Add(value);
        return list;
    }
    private object Plant(int id, Color color)
    {
        var texture = new Texture2D(1, 1); owned.Add(texture);
        texture.SetPixel(0, 0, color); texture.Apply();
        var sprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), Vector2.one * .5f); owned.Add(sprite);
        var data = Data("PlantDataSO");
        var prefab = Object("Plant " + id);
        Set(data, "seedType", Seed(id)); Set(data, "plantPrefab", prefab); Set(data, "plantIcon", sprite);
        var slot = Activator.CreateInstance(Type("PlantSlot"));
        Set(slot, "seedType", Seed(id)); Set(slot, "seedCount", 3); Set(slot, "data", data);
        Set(slot, "plantPrefab", prefab); Set(slot, "plantIcon", sprite);
        return slot;
    }
    private Component CreateSlot(int index, Transform parent, out Component icon, out Component label)
    {
        var go = Object("Slot " + index, parent);
        var rect = (RectTransform)go.transform; rect.sizeDelta = new Vector2(80, 80);
        rect.anchoredPosition = new Vector2((index - 1.5f) * 100, 0);
        var background = go.AddComponent(Type("UnityEngine.UI.Image"));
        var slot = go.AddComponent(Type("SeedSlot"));
        var iconObject = Object("Icon " + index, go.transform);
        ((RectTransform)iconObject.transform).sizeDelta = new Vector2(50, 50);
        icon = iconObject.AddComponent(Type("UnityEngine.UI.Image"));
        var labelObject = Object("Count " + index, go.transform);
        ((RectTransform)labelObject.transform).sizeDelta = new Vector2(50, 30);
        label = labelObject.AddComponent(Type("TMPro.TextMeshProUGUI"));
        var font = Resources.Load("Fonts & Materials/LiberationSans SDF");
        Assert.That(font, Is.Not.Null, "Existing TMP font required for actual count-label rendering.");
        SetProperty(label, "font", font); SetProperty(label, "raycastTarget", false);
        Set(slot, "slotIcon", icon); Set(slot, "slotBackground", background); Set(slot, "seedCountText", label);
        iconObject.SetActive(true); labelObject.SetActive(true); go.SetActive(true);
        return slot;
    }

    [UnitySetUp]
    public IEnumerator Setup()
    {
        storageEvents = selectionEvents = beginEvents = dragEvents = endEvents = 0; selectedEventIndex = -1;
        if (UnityEngine.Object.FindObjectOfType(Type("SeedInventory")) != null ||
            UnityEngine.Object.FindObjectOfType(Type("PlayerAbilitySystem")) != null ||
            UnityEngine.Object.FindObjectOfType(Type("WorldTransitionAnimator")) != null || EventSystem.current != null)
            Assert.Ignore("Run seed pointer fixtures from an empty scene, not a live gameplay/UI scene.");
        if ((bool)Type("UpgradeRuntime").GetProperty("GameplayBlocked").GetValue(null))
            Assert.Ignore("Close the upgrade-selection modal before running isolated seed pointer fixtures.");
        foreach (string name in new[] { "SeedInventory", "SpellInventory", "GameFlowController", "DayCycleController", "UIManager", "InventoryManager" })
        {
            var type = Type(name);
            var field = type.GetField("Instance", BindingFlags.Public | BindingFlags.Static)
                ?? type.GetField("<Instance>k__BackingField", BindingFlags.NonPublic | BindingFlags.Static)
                ?? type.GetField("_instance", BindingFlags.NonPublic | BindingFlags.Static);
            statics.Add(field, field.GetValue(null)); field.SetValue(null, null);
        }
        Component("GameFlowController"); var days = Component("DayCycleController"); Component("SpellInventory");
        inventory = Component("SeedInventory");
        var entries = Array.CreateInstance(Type("PlantSlot"), 3);
        entries.SetValue(Plant(7, Color.green), 0); entries.SetValue(Plant(3, Color.blue), 1);
        entries.SetValue(Activator.CreateInstance(Type("PlantSlot")), 2); Set(inventory, "plantSlots", entries);
        Set(inventory, "onInventoryChanged", (Action)(() => storageEvents++));
        Set(inventory, "onSlotSelected", (Action<int>)(i => { selectionEvents++; selectedEventIndex = i; }));
        firstPlant = (ScriptableObject)Field(entries.GetValue(0), "data");
        secondPlant = (ScriptableObject)Field(entries.GetValue(1), "data");
        var progression = Data("ExperienceProgressionDataSO");
        var level = Activator.CreateInstance(Type("ExperienceLevelData"), 100,
            List("PlantDataSO", firstPlant, secondPlant), List("SpellDataSO"));
        Set(progression, "levelRequirements", List("ExperienceLevelData", level));
        var experience = Object("Progression owner").AddComponent(Type("PlayerExperienceSystem"));
        Set(experience, "progressionData", progression); Call(experience, "Awake");
        owner = experience.gameObject.AddComponent(Type("PlayerContentUnlockSystem"));
        Set(owner, "playerExperienceSystem", experience); Set(owner, "spellInventory", Type("SpellInventory").GetProperty("Instance").GetValue(null));
        Call(owner, "OnEnable"); Call(owner, "Start"); session = Property(owner, "Loadout");
        Call(days, "StartDay");

        eventSystem = Object("EventSystem").AddComponent<EventSystem>(); eventSystem.gameObject.SetActive(true);
        var canvasObject = Object("Equipment Canvas");
        var canvas = canvasObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObject.AddComponent(Type("UnityEngine.UI.GraphicRaycaster"));
        hud = canvasObject.AddComponent(Type("SeedSlotsUIController"));
        slots = new Component[4]; icons = new Component[4]; labels = new Component[4];
        var slotArray = Array.CreateInstance(Type("SeedSlot"), slots.Length);
        for (int i = 0; i < slots.Length; i++)
        {
            slots[i] = CreateSlot(i, canvasObject.transform, out icons[i], out labels[i]); slotArray.SetValue(slots[i], i);
        }
        Set(hud, "seedSlots", slotArray);
        canvasObject.SetActive(true); Call(hud, "Initialize"); Call(hud, "Setup");
        // Separate standalone storage views deliberately do not belong to the equipment HUD.
        var storageCanvas = Object("Standalone Storage Canvas");
        storageCanvas.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        storageSlots = new Component[4]; storageIcons = new Component[4]; storageLabels = new Component[4];
        for (int i = 0; i < storageSlots.Length; i++)
        {
            storageSlots[i] = CreateSlot(i, storageCanvas.transform, out storageIcons[i], out storageLabels[i]);
            Call(storageSlots[i], "Initialize", i);
        }
        storageCanvas.SetActive(true); RefreshStorageDisplays();
        yield return null;
        AssertEquipmentDisplays(); AssertStorageDisplays(); Assert.That(storageEvents, Is.Zero); Assert.That(selectionEvents, Is.Zero);
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        try
        {
            if (hud != null) UnityEngine.Object.DestroyImmediate(hud.gameObject);
            if (owner != null) { Call(owner, "OnDisable"); Call(owner, "OnDestroy"); }
            for (int i = owned.Count - 1; i >= 0; i--) if (owned[i] != null) UnityEngine.Object.DestroyImmediate(owned[i]);
            owned.Clear();
        }
        finally { foreach (var entry in statics) entry.Key.SetValue(null, entry.Value); statics.Clear(); }
        yield return null;
    }
    private PointerEventData Pointer(Component[] views, int target)
    {
        Canvas.ForceUpdateCanvases();
        Assert.That(eventSystem != null && eventSystem.gameObject.activeInHierarchy, Is.True);
        Assert.That(EventSystem.current, Is.SameAs(eventSystem));
        return new PointerEventData((EventSystem)eventSystem)
        { position = target < 0 ? new Vector2(-10000, -10000) : (Vector2)views[target].transform.position };
    }
    private void Begin(int source) => Call(slots[source], "OnBeginDrag", Pointer(slots, source));
    private void End(int source, int target) => Call(slots[source], "OnEndDrag", Pointer(slots, target));
    private object[] Snapshot() => Enumerable.Range(0, 3).Select(i => Call(inventory, "GetPlantSlot", i)).ToArray();
    private void Equip()
    {
        Assert.That(Call(session, "TrySetDraftPlant", 0, Seed(7)), Is.EqualTo(true));
        Assert.That(Call(session, "TrySetDraftPlant", 1, Seed(3)), Is.EqualTo(true));
        Assert.That(Call(session, "TryConfirm"), Is.EqualTo(true));
        AssertEquipmentDisplays();
    }
    private static void AssertDisplay(Component icon, Component label, object sprite, int count, bool visible)
    {
        Assert.That(icon.gameObject.activeSelf, Is.EqualTo(visible));
        Assert.That(Property(icon, "sprite"), Is.EqualTo(sprite));
        Assert.That(((Behaviour)label).enabled, Is.EqualTo(visible));
        Assert.That(Property(label, "text"), Is.EqualTo(visible ? count.ToString() : ""));
    }
    private void AssertEquipmentDisplays()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            Assert.That(slots[i].gameObject.activeSelf, Is.EqualTo(i < 3));
            if (i >= 3) continue;
            var assignment = Call(session, "GetCommittedPlant", i);
            bool assigned = (bool)Property(assignment, "HasValue");
            object data = null; int count = 0;
            if (assigned)
            {
                // Catalog metadata outlives depleted storage. These authored identities are fixed in this fixture.
                int identity = Convert.ToInt32(Property(assignment, "Identity"));
                data = identity == 7 ? firstPlant : secondPlant;
                var resolved = Call(session, "ResolvePlant", Property(assignment, "Identity"));
                var entry = Property(resolved, "Slot"); count = entry != null ? (int)Field(entry, "seedCount") : 0;
            }
            AssertDisplay(icons[i], labels[i], data != null ? Field(data, "plantIcon") : null, count, assigned);
            Assert.That(Call(slots[i], "CanStartDrag"), Is.EqualTo(false));
        }
    }
    private void RefreshStorageDisplays()
    {
        for (int i = 0; i < storageSlots.Length; i++) Call(storageSlots[i], "UpdateSlotDisplay", Call(inventory, "GetPlantSlot", i));
    }
    private void AssertStorageDisplays(int dragging = -1)
    {
        for (int i = 0; i < storageSlots.Length; i++)
        {
            var entry = Call(inventory, "GetPlantSlot", i);
            bool valid = entry != null && (int)Field(entry, "seedCount") > 0 && (UnityEngine.Object)Field(entry, "plantPrefab") != null;
            AssertDisplay(storageIcons[i], storageLabels[i], valid ? Field(entry, "plantIcon") : null,
                valid ? (int)Field(entry, "seedCount") : 0, valid && i != dragging);
        }
    }
    private void AssertUnchanged(object[] original)
    {
        for (int i = 0; i < 3; i++) Assert.That(Call(inventory, "GetPlantSlot", i), Is.SameAs(original[i]));
        Assert.That(storageEvents, Is.Zero); Assert.That(selectionEvents, Is.Zero);
        Assert.That(Call(inventory, "GetSelectedSlotIndex"), Is.EqualTo(0));
        Assert.That(Enumerable.Range(0, 3).Sum(i => (int)Call(inventory, "GetSeedCountInSlot", i)), Is.EqualTo(6));
        AssertEquipmentDisplays();
    }

    [UnityTest]
    public IEnumerator NewRunHudIsEmptyDespiteStockAndDraftCannotChangeItBeforeConfirmation()
    {
        Assert.That(Call(session, "TrySetDraftPlant", 0, Seed(7)), Is.EqualTo(true));
        Begin(0); End(0, 1); AssertEquipmentDisplays();
        Assert.That(Call(session, "TryConfirm"), Is.EqualTo(true));
        AssertEquipmentDisplays();
        Assert.That(Property(Call(session, "GetCommittedPlant", 1), "HasValue"), Is.False);
        Assert.That(storageEvents, Is.Zero); Assert.That(selectionEvents, Is.Zero);
        yield return null;
    }

    [UnityTest]
    public IEnumerator OccupiedEmptyOutsideSameAndExtraSlotDropsNeverDragOrSwapEquipmentHud()
    {
        Equip(); var original = Snapshot(); int children = hud.GetComponentsInChildren<Transform>(true).Length;
        Action<int> onBegin = _ => beginEvents++;
        foreach (var slot in slots.Take(3)) slot.GetType().GetEvent("OnDragStarted").AddEventHandler(slot, onBegin);
        foreach (int target in new[] { 1, 2, -1, 0, 3 })
        {
            Begin(0); Call(slots[0], "OnDrag", Pointer(slots, target)); End(0, target);
            yield return null; AssertUnchanged(original);
        }
        Begin(1); End(1, 0); yield return null; AssertUnchanged(original);
        Assert.That(beginEvents, Is.Zero);
        Assert.That(hud.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(children));
    }

    [UnityTest]
    public IEnumerator EndWithoutBeginAndEmptyBeginAreHarmlessAndDisableReenableDoesNotEnableDragging()
    {
        Equip(); var original = Snapshot();
        End(0, 1); Begin(2); End(2, 1); AssertUnchanged(original);
        Begin(0); hud.gameObject.SetActive(false); yield return null; AssertUnchanged(original);
        hud.gameObject.SetActive(true); Begin(0); End(0, 1); yield return null; AssertUnchanged(original);
    }

    [UnityTest]
    public IEnumerator DirectStorageSwapsPreserveEntriesCountsSelectionAndCommittedHudIdentities()
    {
        Equip(); var original = Snapshot();
        Assert.That(Call(inventory, "SwapSlots", 0, 1), Is.EqualTo(true));
        Assert.That(Call(inventory, "GetPlantSlot", 1), Is.SameAs(original[0]));
        Assert.That(Call(inventory, "GetPlantSlot", 0), Is.SameAs(original[1]));
        AssertEquipmentDisplays();
        Assert.That(Call(inventory, "SwapSlots", 1, 2), Is.EqualTo(true));
        Assert.That(Call(inventory, "GetPlantSlot", 2), Is.SameAs(original[0]));
        Assert.That(Call(inventory, "GetPlantSlot", 1), Is.SameAs(original[2]));
        Assert.That(storageEvents, Is.EqualTo(2)); Assert.That(selectionEvents, Is.Zero);
        Call(hud, "SelectSlot", 0);
        Assert.That(Call(inventory, "GetSelectedSlotIndex"), Is.EqualTo(2));
        Assert.That(selectionEvents, Is.EqualTo(1)); Assert.That(selectedEventIndex, Is.EqualTo(2));
        Assert.That((Vector3)slots[0].transform.localScale, Is.EqualTo(new Vector3(1.2f, 1.2f, 1f)));
        Assert.That(Property(Call(session, "GetCommittedPlant", 0), "Identity"), Is.EqualTo(Seed(7)));
        Assert.That(Property(Call(session, "GetCommittedPlant", 1), "Identity"), Is.EqualTo(Seed(3)));
        AssertEquipmentDisplays();
        foreach (var pair in new[] { new[] { 1, 2 }, new[] { 0, 1 } }) Assert.That(Call(inventory, "SwapSlots", pair[0], pair[1]), Is.EqualTo(true));
        for (int i = 0; i < 3; i++) Assert.That(Call(inventory, "GetPlantSlot", i), Is.SameAs(original[i]));
        Assert.That(storageEvents, Is.EqualTo(4)); Assert.That(selectionEvents, Is.EqualTo(1));
        Assert.That(Enumerable.Range(0, 3).Sum(i => (int)Call(inventory, "GetSeedCountInSlot", i)), Is.EqualTo(6));
        yield return null;
    }

    [UnityTest]
    public IEnumerator InvalidDirectStorageSwapLeavesEntriesCountsAndNotificationsUnchanged()
    {
        Equip(); var original = Snapshot();
        foreach (var pair in new[] { new[] { 0, 3 }, new[] { -1, 1 }, new[] { 0, 0 } })
            Assert.That(Call(inventory, "SwapSlots", pair[0], pair[1]), Is.EqualTo(false));
        AssertUnchanged(original); yield return null;
    }

    [UnityTest]
    public IEnumerator StandaloneStorageViewsKeepDisplayCanStartDragAndPublicCallbacksWithoutOwningSwaps()
    {
        var original = Snapshot();
        Assert.That(Call(storageSlots[0], "CanStartDrag"), Is.EqualTo(true));
        Assert.That(Call(storageSlots[1], "CanStartDrag"), Is.EqualTo(true));
        Assert.That(Call(storageSlots[2], "CanStartDrag"), Is.EqualTo(false));
        Assert.That(Call(storageSlots[3], "CanStartDrag"), Is.EqualTo(false));
        int source = -1;
        Action<int> begin = index => { source = index; beginEvents++; };
        Action<int, PointerEventData> drag = (index, data) => { Assert.That(index, Is.Zero); Assert.That(data, Is.Not.Null); dragEvents++; };
        Action<int, PointerEventData> end = (index, data) => { Assert.That(index, Is.Zero); Assert.That(data, Is.Not.Null); endEvents++; };
        storageSlots[0].GetType().GetEvent("OnDragStarted").AddEventHandler(storageSlots[0], begin);
        storageSlots[0].GetType().GetEvent("OnSlotDragged").AddEventHandler(storageSlots[0], drag);
        storageSlots[0].GetType().GetEvent("OnDragEnded").AddEventHandler(storageSlots[0], end);
        Call(storageSlots[0], "OnBeginDrag", Pointer(storageSlots, 0));
        Call(storageSlots[0], "OnDrag", Pointer(storageSlots, 1));
        Call(storageSlots[0], "OnEndDrag", Pointer(storageSlots, 1));
        Assert.That(source, Is.Zero); Assert.That(beginEvents, Is.EqualTo(1));
        Assert.That(dragEvents, Is.EqualTo(1)); Assert.That(endEvents, Is.EqualTo(1));
        foreach (int index in new[] { 2, 3 })
        {
            storageSlots[index].GetType().GetEvent("OnDragStarted").AddEventHandler(storageSlots[index], begin);
            Call(storageSlots[index], "OnBeginDrag", Pointer(storageSlots, index));
        }
        Assert.That(beginEvents, Is.EqualTo(1)); // Empty/out-of-capacity storage views reject begin callbacks.
        Call(storageSlots[0], "SetDragSource", true);
        Assert.That(storageIcons[0].gameObject.activeSelf, Is.False); Assert.That(((Behaviour)storageLabels[0]).enabled, Is.False);
        Call(storageSlots[0], "SetDragSource", false); AssertStorageDisplays();
        AssertUnchanged(original); yield return null;
    }

    [UnityTest]
    public IEnumerator StandaloneStorageRefreshClearsStaleSpriteAndCountAfterSwapToEmptyAndBack()
    {
        var original = Snapshot();
        Assert.That(Call(inventory, "SwapSlots", 0, 2), Is.EqualTo(true)); RefreshStorageDisplays(); AssertStorageDisplays();
        Assert.That(Property(storageIcons[0], "sprite"), Is.Null); Assert.That(Property(storageLabels[0], "text"), Is.EqualTo(""));
        Assert.That(Call(inventory, "GetPlantSlot", 2), Is.SameAs(original[0]));
        Assert.That(Call(inventory, "SwapSlots", 2, 0), Is.EqualTo(true)); RefreshStorageDisplays(); AssertStorageDisplays();
        for (int i = 0; i < 3; i++) Assert.That(Call(inventory, "GetPlantSlot", i), Is.SameAs(original[i]));
        Assert.That(storageEvents, Is.EqualTo(2)); Assert.That(selectionEvents, Is.Zero);
        Assert.That(Call(inventory, "GetSelectedSlotIndex"), Is.Zero);
        yield return null;
    }
}
