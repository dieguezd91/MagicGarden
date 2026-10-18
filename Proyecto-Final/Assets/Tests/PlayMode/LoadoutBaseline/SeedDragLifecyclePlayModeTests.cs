using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// Public SeedSlot drag callbacks with a real Canvas, GraphicRaycaster and
/// EventSystem. Reflection only supplies serialized fixtures across asmdef boundaries.
/// This exercises pointer-event routing and rendering state, not physical mouse input.
/// </summary>
public sealed class SeedDragLifecyclePlayModeTests
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
    private Component inventory, hud, eventSystem;
    private Component[] slots, icons, labels;
    private FieldInfo singleton;
    private object previousInventory;
    private int storageEvents, selectionEvents, selectedEventIndex;

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
    private object Plant(int id, Color color)
    {
        var texture = new Texture2D(1, 1); owned.Add(texture);
        texture.SetPixel(0, 0, color); texture.Apply();
        var sprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), Vector2.one * 0.5f); owned.Add(sprite);
        var data = ScriptableObject.CreateInstance(Type("PlantDataSO")); owned.Add(data);
        var prefab = Object("Plant " + id);
        Set(data, "seedType", Seed(id)); Set(data, "plantPrefab", prefab); Set(data, "plantIcon", sprite);
        var slot = Activator.CreateInstance(Type("PlantSlot"));
        Set(slot, "seedType", Seed(id)); Set(slot, "seedCount", 3); Set(slot, "data", data);
        Set(slot, "plantPrefab", prefab); Set(slot, "plantIcon", sprite);
        return slot;
    }

    [UnitySetUp]
    public IEnumerator Setup()
    {
        singleton = null; storageEvents = selectionEvents = 0; selectedEventIndex = -1;
        if (UnityEngine.Object.FindObjectOfType(Type("SeedInventory")) != null ||
            UnityEngine.Object.FindObjectOfType(Type("PlayerAbilitySystem")) != null ||
            Type("UnityEngine.EventSystems.EventSystem").GetProperty("current").GetValue(null) != null)
            Assert.Ignore("Run seed drag fixtures from an empty scene, not a live gameplay/UI scene.");
        if ((bool)Type("UpgradeRuntime").GetProperty("GameplayBlocked").GetValue(null))
            Assert.Ignore("Close the upgrade-selection modal before running isolated seed drag fixtures.");
        singleton = Type("SeedInventory").GetField("Instance"); previousInventory = singleton.GetValue(null);
        inventory = Object("Inventory").AddComponent(Type("SeedInventory"));
        var entries = Array.CreateInstance(Type("PlantSlot"), 3);
        entries.SetValue(Plant(7, Color.green), 0); entries.SetValue(Plant(3, Color.blue), 1);
        entries.SetValue(Activator.CreateInstance(Type("PlantSlot")), 2);
        Set(inventory, "plantSlots", entries); singleton.SetValue(null, inventory); Call(inventory, "Awake");
        Set(inventory, "onInventoryChanged", (Action)(() => storageEvents++));
        Set(inventory, "onSlotSelected", (Action<int>)(i => { selectionEvents++; selectedEventIndex = i; }));

        eventSystem = Object("EventSystem").AddComponent(Type("UnityEngine.EventSystems.EventSystem"));
        eventSystem.gameObject.SetActive(true);
        var canvasObject = Object("Canvas");
        var canvas = canvasObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObject.AddComponent(Type("UnityEngine.UI.GraphicRaycaster"));
        hud = canvasObject.AddComponent(Type("SeedSlotsUIController")); Set(hud, "seedInventory", inventory);
        slots = new Component[4]; icons = new Component[4]; labels = new Component[4];
        var slotArray = Array.CreateInstance(Type("SeedSlot"), slots.Length);
        var font = Resources.Load("Fonts & Materials/LiberationSans SDF");
        Assert.That(font, Is.Not.Null, "Existing TMP font required for actual count-label rendering.");
        for (int i = 0; i < slots.Length; i++)
        {
            var go = Object("Slot " + i, canvasObject.transform);
            var rect = (RectTransform)go.transform; rect.sizeDelta = new Vector2(80, 80);
            rect.anchoredPosition = new Vector2((i - 1.5f) * 100, 0);
            var background = go.AddComponent(Type("UnityEngine.UI.Image"));
            slots[i] = go.AddComponent(Type("SeedSlot")); slotArray.SetValue(slots[i], i);
            var iconObject = Object("Icon " + i, go.transform);
            ((RectTransform)iconObject.transform).sizeDelta = new Vector2(50, 50);
            icons[i] = iconObject.AddComponent(Type("UnityEngine.UI.Image"));
            var labelObject = Object("Count " + i, go.transform);
            ((RectTransform)labelObject.transform).sizeDelta = new Vector2(50, 30);
            labels[i] = labelObject.AddComponent(Type("TMPro.TextMeshProUGUI"));
            SetProperty(labels[i], "font", font); SetProperty(labels[i], "raycastTarget", false);
            Set(slots[i], "slotIcon", icons[i]); Set(slots[i], "slotBackground", background);
            Set(slots[i], "seedCountText", labels[i]);
            iconObject.SetActive(true); labelObject.SetActive(true); go.SetActive(true);
        }
        Set(hud, "seedSlots", slotArray);
        Call(hud, "SetupEventListeners"); Call(hud, "InitializeSlots");
        canvasObject.SetActive(true); Canvas.ForceUpdateCanvases();
        yield return null;
        AssertDisplays();
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        // Stop UI subscriptions before destroying inventory/visual fixtures.
        if (hud != null) UnityEngine.Object.DestroyImmediate(hud.gameObject);
        for (int i = owned.Count - 1; i >= 0; i--)
            if (owned[i] != null) UnityEngine.Object.DestroyImmediate(owned[i]);
        owned.Clear();
        if (singleton != null) singleton.SetValue(null, previousInventory);
        yield return null;
    }

    private object Pointer(int target)
    {
        Canvas.ForceUpdateCanvases();
        Assert.That(eventSystem != null && eventSystem.gameObject.activeInHierarchy, Is.True,
            "The fixture-owned EventSystem must be initialized before constructing pointer events.");
        Assert.That(Type("UnityEngine.EventSystems.EventSystem").GetProperty("current").GetValue(null),
            Is.SameAs(eventSystem), "Pointer raycasts must use the fixture-owned EventSystem.");
        // Component converts to bool: an explicit argument array avoids the
        // Activator.CreateInstance(Type, bool) parameterless-constructor overload.
        var data = Activator.CreateInstance(Type("UnityEngine.EventSystems.PointerEventData"), new object[] { eventSystem });
        SetProperty(data, "position", target < 0 ? new Vector2(-10000, -10000) : (Vector2)slots[target].transform.position);
        return data;
    }
    private void Begin(int source) => Call(slots[source], "OnBeginDrag", Pointer(source));
    private void End(int source, int target) => Call(slots[source], "OnEndDrag", Pointer(target));
    private object[] Snapshot() => Enumerable.Range(0, 3).Select(i => Call(inventory, "GetPlantSlot", i)).ToArray();
    private void AssertDisplays(int dragging = -1)
    {
        for (int i = 0; i < slots.Length; i++)
        {
            var entry = Call(inventory, "GetPlantSlot", i);
            bool valid = entry != null && (int)Field(entry, "seedCount") > 0 &&
                (UnityEngine.Object)Field(entry, "plantPrefab") != null;
            Assert.That(icons[i].gameObject.activeSelf, Is.EqualTo(valid && i != dragging), "icon " + i);
            Assert.That(Property(icons[i], "sprite"), Is.EqualTo(valid ? Field(entry, "plantIcon") : null), "sprite " + i);
            Assert.That(((Behaviour)labels[i]).enabled, Is.EqualTo(valid && i != dragging), "label " + i);
            Assert.That(Property(labels[i], "text"), Is.EqualTo(valid ? Field(entry, "seedCount").ToString() : ""), "count " + i);
            Assert.That(Field(slots[i], "isDragSource"), Is.EqualTo(i == dragging), "drag source " + i);
        }
    }
    private void AssertIdle()
    {
        Assert.That(Field(hud, "dragSourceIndex"), Is.EqualTo(-1));
        Assert.That((UnityEngine.Object)Field(hud, "dragIcon"), Is.Null);
        AssertDisplays();
        Assert.That(Enumerable.Range(0, 3).Sum(i => (int)Call(inventory, "GetSeedCountInSlot", i)), Is.EqualTo(6));
    }

    [UnityTest]
    public IEnumerator FirstAndSecondOccupiedSwapsPreserveEntriesCountsSelectionAndEvents()
    {
        var original = Snapshot();
        Begin(0); AssertDisplays(0); End(0, 1); yield return null;
        AssertIdle();
        Assert.That(Call(inventory, "GetPlantSlot", 1), Is.SameAs(original[0]));
        Assert.That(Call(inventory, "GetPlantSlot", 0), Is.SameAs(original[1]));
        Assert.That(Call(inventory, "GetSelectedSlotIndex"), Is.EqualTo(1));
        Assert.That(selectionEvents, Is.EqualTo(1)); Assert.That(selectedEventIndex, Is.EqualTo(1));
        Assert.That(storageEvents, Is.EqualTo(1));
        Assert.That((Vector3)slots[1].transform.localScale, Is.EqualTo(new Vector3(1.2f, 1.2f, 1f)));
        Begin(1); AssertDisplays(1); End(1, 0); yield return null;
        AssertIdle();
        foreach (int i in Enumerable.Range(0, 3)) Assert.That(Call(inventory, "GetPlantSlot", i), Is.SameAs(original[i]));
        Assert.That(Call(inventory, "GetSelectedSlotIndex"), Is.EqualTo(0));
        Assert.That(selectionEvents, Is.EqualTo(2)); Assert.That(storageEvents, Is.EqualTo(2));
    }

    [UnityTest]
    public IEnumerator SwapIntoEmptySlotClearsStaleSpriteAndCountAndCanSwapBack()
    {
        var original = Snapshot();
        Begin(0); End(0, 2); yield return null; AssertIdle();
        Assert.That(Call(inventory, "GetPlantSlot", 2), Is.SameAs(original[0]));
        Assert.That(Call(inventory, "GetPlantSlot", 0), Is.SameAs(original[2]));
        Call(slots[0], "SetDragSource", false); AssertDisplays();
        Begin(2); End(2, 0); yield return null; AssertIdle();
        Assert.That(Call(inventory, "GetPlantSlot", 0), Is.SameAs(original[0]));
        Assert.That(storageEvents, Is.EqualTo(2)); Assert.That(selectionEvents, Is.EqualTo(2));
    }

    [UnityTest]
    public IEnumerator FailedSwapRestoresCurrentVisualsWithoutNotificationsOrSelection()
    {
        var original = Snapshot();
        // UI position 3 exists, but storage capacity is 3: original SwapSlots rejects it.
        Begin(0); End(0, 3); yield return null; AssertIdle();
        foreach (int i in Enumerable.Range(0, 3)) Assert.That(Call(inventory, "GetPlantSlot", i), Is.SameAs(original[i]));
        Assert.That(storageEvents, Is.Zero); Assert.That(selectionEvents, Is.Zero);
        Assert.That(Call(inventory, "GetSelectedSlotIndex"), Is.EqualTo(0));
    }

    [UnityTest]
    public IEnumerator OutsideAndSameSlotCancellationRestoreVisualsWithoutMutatingInventory()
    {
        var original = Snapshot();
        foreach (int target in new[] { -1, 0 })
        {
            Begin(0); AssertDisplays(0); End(0, target); yield return null; AssertIdle();
            foreach (int i in Enumerable.Range(0, 3)) Assert.That(Call(inventory, "GetPlantSlot", i), Is.SameAs(original[i]));
            Assert.That(storageEvents, Is.Zero); Assert.That(selectionEvents, Is.Zero);
            Assert.That(Call(inventory, "GetSelectedSlotIndex"), Is.EqualTo(0));
        }
    }

    [UnityTest]
    public IEnumerator EndWithoutBeginOrAfterRejectedEmptyBeginIsHarmless()
    {
        var original = Snapshot();
        End(0, 1); Begin(2); End(2, 1); yield return null; AssertIdle();
        foreach (int i in Enumerable.Range(0, 3)) Assert.That(Call(inventory, "GetPlantSlot", i), Is.SameAs(original[i]));
        Assert.That(storageEvents, Is.Zero); Assert.That(selectionEvents, Is.Zero);
        Assert.That(Call(inventory, "GetSelectedSlotIndex"), Is.EqualTo(0));
    }

    [UnityTest]
    public IEnumerator DisablingHudCancelsDragAndRestoresCurrentInventoryDisplay()
    {
        Begin(0); AssertDisplays(0);
        hud.gameObject.SetActive(false); yield return null; AssertIdle();
        Assert.That(storageEvents, Is.Zero); Assert.That(selectionEvents, Is.Zero);
    }
}
