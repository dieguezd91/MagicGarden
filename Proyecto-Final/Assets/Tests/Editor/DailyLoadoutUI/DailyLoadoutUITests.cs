using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class DailyLoadoutUITests
{
    private const string Folder = "Assets/Prefabs/UI/DailyLoadout/";
    private GameObject instance;

    private GameObject Instantiate(string name)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + name + ".prefab");
        Assert.That(prefab, Is.Not.Null, "Run Tools/Magic Garden/Daily Loadout/Create UI prefabs (once) first.");
        instance = Object.Instantiate(prefab);
        return instance;
    }
    private static Component View(GameObject root, string name)
    {
        return root.GetComponent(Type.GetType("MagicGarden.LoadoutUI." + name + ", Assembly-CSharp", true));
    }
    private static Object Ref(Component owner, string field)
    {
        return new SerializedObject(owner).FindProperty(field).objectReferenceValue;
    }
    private static void Call(Component view, string method, params object[] args)
    {
        view.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.Public).Invoke(view, args);
    }
    [TearDown] public void Cleanup() { if (instance != null) Object.DestroyImmediate(instance); }

    [Test]
    public void PanelHasSixBoundSlotsAndNoGameplayCallbacks()
    {
        var root = Instantiate("DailyLoadoutPanel");
        var view = View(root, "DailyLoadoutPanelView");
        Assert.That(view, Is.Not.Null);
        var serialized = new SerializedObject(view);
        foreach (string category in new[] { "plantSlots", "spellSlots" })
        {
            var array = serialized.FindProperty(category);
            Assert.That(array.arraySize, Is.EqualTo(3));
            for (int i = 0; i < 3; i++)
            {
                var slot = (Component)array.GetArrayElementAtIndex(i).objectReferenceValue;
                Assert.That(slot, Is.Not.Null);
                Assert.That(slot.gameObject.activeSelf, Is.True);
                Assert.That(slot.transform.IsChildOf(root.transform), Is.True);
                Assert.That(((TMP_Text)Ref(slot, "positionLabel")).text,
                    Is.EqualTo((category == "plantSlots" ? "Plant " : "Spell ") + (i + 1)));
            }
        }
        foreach (string field in new[] { "optionPrefab", "optionContent", "optionsScroll", "emptyOptionsLabel", "allowanceLabel", "confirmButton", "cancelButton", "clearButton" })
            Assert.That(Ref(view, field), Is.Not.Null, field);
        foreach (var button in root.GetComponentsInChildren<Button>(true))
            Assert.That(button.onClick.GetPersistentEventCount(), Is.Zero, button.name);
        Assert.That(((Button)Ref(view, "confirmButton")).interactable, Is.False);
        Assert.That(((Button)Ref(view, "clearButton")).interactable, Is.False);
        Assert.That(((Button)Ref(view, "cancelButton")).interactable, Is.True);
        Assert.That(root.GetComponentsInChildren<MonoBehaviour>(true).All(c => c != null), Is.True, "Missing script");
        foreach (var script in root.GetComponentsInChildren<MonoBehaviour>(true))
        {
            var type = script.GetType();
            Assert.That(type.Assembly.GetName().Name != "Assembly-CSharp" || type.Namespace == "MagicGarden.LoadoutUI", Is.True, type.FullName);
        }
    }

    [TestCase("LoadoutSlot", "LoadoutSlotView")]
    [TestCase("LoadoutOption", "LoadoutOptionView")]
    public void EquipmentStatesAreDistinctAndDoNotChangeContent(string prefab, string component)
    {
        var view = View(Instantiate(prefab), component);
        var button = (Button)Ref(view, "selectButton");
        var background = (Image)Ref(view, "background");
        var icon = (Image)Ref(view, "icon");
        var name = (TMP_Text)Ref(view, "nameLabel");
        var state = (TMP_Text)Ref(view, "stateLabel");
        foreach (string field in new[] { "selectButton", "background", "icon", "nameLabel", "stateLabel", "normalSprite", "hoverSprite", "pressedSprite" })
            Assert.That(Ref(view, field), Is.Not.Null, field);
        Call(view, "SetContent", null, null);
        Assert.That(name.text, Is.EqualTo("Empty"));
        Assert.That(icon.enabled, Is.False);
        Assert.That(state.text, Is.EqualTo("Empty"));
        Assert.That(button.interactable, Is.EqualTo(prefab == "LoadoutSlot"));
        var sprite = (Sprite)Ref(view, "normalSprite");
        Call(view, "SetContent", sprite, "Test plant or spell");
        Assert.That(icon.enabled, Is.True); Assert.That(icon.sprite, Is.SameAs(sprite));
        Assert.That(button.interactable, Is.True); Assert.That(state.text, Is.EqualTo("Available"));
        Call(view, "SetAssigned", true); Assert.That(state.text, Is.EqualTo("Assigned"));
        Call(view, "SetSelected", true);
        Assert.That(state.text, Is.EqualTo("Assigned | Selected"));
        Assert.That(background.color, Is.Not.EqualTo(Color.white));
        Call(view, "SetHighlighted", true);
        Assert.That(background.sprite, Is.SameAs(Ref(view, "hoverSprite")));
        Assert.That(state.text, Is.EqualTo("Assigned | Selected | Highlighted"));
        Call(view, "SetInvalidDropTarget", true);
        Assert.That(state.text, Is.EqualTo("Assigned | Selected | Invalid target"));
        Assert.That(background.color.r, Is.GreaterThan(background.color.g));
        Call(view, "SetReadOnly", true);
        Assert.That(button.interactable, Is.False); Assert.That(state.text, Does.EndWith("Read-only"));
        Call(view, "SetReadOnly", false);
        Call(view, "SetSelected", false); Call(view, "SetAssigned", false);
        Call(view, "SetHighlighted", false); Call(view, "SetInvalidDropTarget", false);
        Call(view, "OnPointerEnter", new PointerEventData(null));
        Assert.That(background.sprite, Is.SameAs(Ref(view, "hoverSprite")));
        Call(view, "OnPointerDown", new PointerEventData(null) { button = PointerEventData.InputButton.Left });
        Assert.That(background.sprite, Is.SameAs(Ref(view, "pressedSprite")));
        Call(view, "OnPointerUp", new PointerEventData(null));
        Call(view, "OnPointerExit", new PointerEventData(null));
        Assert.That(background.sprite, Is.SameAs(Ref(view, "normalSprite")));
        Assert.That(name.text, Is.EqualTo("Test plant or spell")); Assert.That(icon.sprite, Is.SameAs(sprite));
        if (prefab == "LoadoutOption")
        {
            Call(view, "SetLocked", true);
            Assert.That(button.interactable, Is.False); Assert.That(state.text, Is.EqualTo("Available | Locked"));
            Call(view, "SetLocked", false); Assert.That(button.interactable, Is.True);
        }
    }

    [Test]
    public void ArtUsesExistingPointFilteredSheetAndPixelFont()
    {
        var root = Instantiate("DailyLoadoutPanel");
        foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
            Assert.That(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(text.font)), Is.EqualTo("52be7e01f8fbce54186eac8da2da70cc"));
        foreach (var image in root.GetComponentsInChildren<Image>(true).Where(i => i.sprite != null))
        {
            Assert.That(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(image.sprite)), Is.EqualTo("6b876b38e4597704b84b5e76e54d27a1"));
            Assert.That(image.sprite.texture.filterMode, Is.EqualTo(FilterMode.Point));
        }
    }

    [TestCase(1920, 1080)]
    [TestCase(1280, 720)]
    [TestCase(2560, 1440)]
    [TestCase(1024, 768)]
    public void LayoutFitsScaledCanvasAndAdditionalOptionsScroll(int width, int height)
    {
        var root = Instantiate("DailyLoadoutPanel");
        var canvas = new GameObject("Isolated test canvas", typeof(RectTransform), typeof(Canvas));
        try
        {
            // Equivalent virtual dimensions for the existing CanvasScaler's match = .5.
            float scale = Mathf.Sqrt(width / 1920f * (height / 1080f));
            ((RectTransform)canvas.transform).sizeDelta = new Vector2(width / scale, height / scale);
            root.transform.SetParent(canvas.transform, false);
            var view = View(root, "DailyLoadoutPanelView");
            var content = (RectTransform)Ref(view, "optionContent");
            var prefab = (Component)Ref(view, "optionPrefab");
            for (int i = 0; i < 24; i++)
            {
                var option = Object.Instantiate(prefab.gameObject, content);
                Call(View(option, "LoadoutOptionView"), "SetContent", null, "Option " + i);
            }
            Call(view, "SetOptionsEmpty", false);
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)root.transform);
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            var scroll = (ScrollRect)Ref(view, "optionsScroll");
            Assert.That(content.rect.height, Is.GreaterThan(scroll.viewport.rect.height));
            Assert.That(scroll.viewport.GetComponent<RectMask2D>(), Is.Not.Null);
            Assert.That(scroll.vertical, Is.True); Assert.That(scroll.horizontal, Is.False);
            foreach (RectTransform child in content)
            {
                var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(content, child);
                Assert.That(bounds.min.x, Is.GreaterThanOrEqualTo(content.rect.xMin - 1));
                Assert.That(bounds.max.x, Is.LessThanOrEqualTo(content.rect.xMax + 1));
            }
            scroll.verticalNormalizedPosition = 0;
            Canvas.ForceUpdateCanvases();
            var last = (RectTransform)content.GetChild(content.childCount - 1);
            var lastBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport, last);
            Assert.That(lastBounds.min.y, Is.GreaterThanOrEqualTo(scroll.viewport.rect.yMin - 1));
            Assert.That(lastBounds.max.y, Is.LessThanOrEqualTo(scroll.viewport.rect.yMax + 1));
            foreach (var slot in root.GetComponentsInChildren<Component>().Where(c => c.GetType().Name == "LoadoutSlotView"))
            {
                var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(root.transform, slot.transform);
                var rect = ((RectTransform)root.transform).rect;
                Assert.That(bounds.min.x, Is.GreaterThanOrEqualTo(rect.xMin));
                Assert.That(bounds.max.x, Is.LessThanOrEqualTo(rect.xMax));
                Assert.That(bounds.min.y, Is.GreaterThanOrEqualTo(rect.yMin));
                Assert.That(bounds.max.y, Is.LessThanOrEqualTo(rect.yMax));
            }
        }
        finally { Object.DestroyImmediate(canvas); }
    }
}
