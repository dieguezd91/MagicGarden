using System;
using System.IO;
using System.Linq;
using MagicGarden.LoadoutUI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Explicit native authoring only. Does not run on import or touch existing scenes/prefabs.
public static class DailyLoadoutUIPrefabAuthoring
{
    public const string Folder = "Assets/Prefabs/UI/DailyLoadout";
    private const string SheetGuid = "6b876b38e4597704b84b5e76e54d27a1";
    private static TMP_FontAsset font;
    private static Sprite table, normal, hover, pressed, slotFrame, close, closeHover, closePressed;
    private static readonly string[] Names = { "LoadoutSlot", "LoadoutOption", "DailyLoadoutPanel" };

    [MenuItem("Tools/Magic Garden/Daily Loadout/Create UI prefabs (once)")]
    public static void Generate()
    {
        foreach (string name in Names)
            if (File.Exists(Folder + "/" + name + ".prefab"))
                throw new InvalidOperationException("Daily Loadout prefab already exists. Edit it in Prefab Mode; generation will not overwrite it.");
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath("52be7e01f8fbce54186eac8da2da70cc"));
        if (font == null) throw new InvalidOperationException("Existing m5x7 font is missing.");
        table = SpriteById(1488800299); normal = SpriteById(924556296);
        hover = SpriteById(-236189874); pressed = SpriteById(-1569556968);
        slotFrame = SpriteById(-1001860351);
        close = SpriteById(774320644); closeHover = SpriteById(726165551); closePressed = SpriteById(-1595310454);
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Prefabs/UI", "DailyLoadout");
        // Roll back only this invocation's new assets if any native save fails.
        try
        {
            var slot = CreateEquipment(true);
            var option = CreateEquipment(false);
            CreatePanel(slot, option);
            AssetDatabase.SaveAssets();
            Debug.Log("Daily Loadout: three independent prefabs created. No scene or existing prefab was modified. Run DailyLoadoutUI tests and visual checklist before use.");
        }
        catch
        {
            foreach (string name in Names) AssetDatabase.DeleteAsset(Folder + "/" + name + ".prefab");
            throw;
        }
    }

    private static Sprite SpriteById(long id)
    {
        var sprite = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(SheetGuid))
            .OfType<Sprite>().FirstOrDefault(s => AssetDatabase.TryGetGUIDAndLocalFileIdentifier(s, out string guid, out long local) && local == id);
        if (sprite == null) throw new InvalidOperationException("Required existing UI sprite missing: " + id);
        if (sprite.texture.filterMode != FilterMode.Point)
            throw new InvalidOperationException("Existing UI sheet must use Point filtering. No importer settings were changed.");
        return sprite;
    }

    private static GameObject CreateEquipment(bool isSlot)
    {
        var root = Rect(isSlot ? "LoadoutSlot" : "LoadoutOption", null, Vector2.zero, Vector2.one);
        root.sizeDelta = new Vector2(isSlot ? 190 : 380, isSlot ? 210 : 120);
        try
        {
            LoadoutEquipmentView view = isSlot ? (LoadoutEquipmentView)root.gameObject.AddComponent<LoadoutSlotView>() : root.gameObject.AddComponent<LoadoutOptionView>();
            var button = Button(root, normal, hover, pressed);
            button.transition = Selectable.Transition.None; // View composes pointer and externally supplied state.
            var iconFrame = Rect("Icon frame", root, new Vector2(isSlot ? .28f : .05f, isSlot ? .46f : .30f), new Vector2(isSlot ? .72f : .26f, .78f));
            Image(iconFrame, slotFrame).raycastTarget = false;
            var icon = Image(Rect("Icon", iconFrame, new Vector2(.12f, .12f), new Vector2(.88f, .88f)), null);
            icon.preserveAspect = true; icon.enabled = false; icon.raycastTarget = false;
            Ref(view, "selectButton", button); Ref(view, "background", button.image); Ref(view, "icon", icon);
            Ref(view, "nameLabel", Label("Equipment name", root, "Empty", isSlot ? .03f : .29f, .27f, .97f, isSlot ? .44f : .84f, 28));
            Ref(view, "stateLabel", Label("Visual state", root, "Empty", .03f, .04f, .97f, .25f, 22));
            Ref(view, "normalSprite", normal); Ref(view, "hoverSprite", hover); Ref(view, "pressedSprite", pressed);
            if (isSlot) Ref(view, "positionLabel", Label("Position", root, "Plant 1", .04f, .80f, .96f, .98f, 30));
            view.SetContent(null, null);
            return Save(root, isSlot ? "LoadoutSlot" : "LoadoutOption");
        }
        finally { Object.DestroyImmediate(root.gameObject); }
    }

    private static void CreatePanel(GameObject slot, GameObject option)
    {
        var root = Rect("DailyLoadoutPanel", null, Vector2.zero, Vector2.one);
        try
        {
            var view = root.gameObject.AddComponent<DailyLoadoutPanelView>();
            var shade = Image(root, null); shade.color = new Color(0, 0, 0, .7f);
            var window = Rect("Stone window", root, new Vector2(.06f, .04f), new Vector2(.94f, .96f));
            Image(window, table);
            Label("Title", window, "DAILY LOADOUT", .10f, .87f, .90f, .97f, 46);
            var exit = Rect("Close", window, new Vector2(.92f, .89f), new Vector2(.97f, .96f));
            Ref(view, "cancelButton", Button(exit, close, closeHover, closePressed));
            Ref(view, "allowanceLabel", Label("Daily allowance", window, "Daily configuration allowance: awaiting status", .08f, .81f, .92f, .87f, 28));
            var main = Rect("Main stone table", window, new Vector2(.04f, .47f), new Vector2(.96f, .81f));
            Image(main, table);
            Label("Plants category", main, "PLANTS", .03f, .81f, .49f, .98f, 32);
            Label("Spells category", main, "SPELLS", .51f, .81f, .97f, .98f, 32);
            var plants = new Object[3]; var spells = new Object[3];
            for (int i = 0; i < 6; i++)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(slot, main);
                instance.name = (i < 3 ? "Plant " : "Spell ") + (i % 3 + 1);
                var rect = (RectTransform)instance.transform;
                float start = i < 3 ? .03f : .52f;
                rect.anchorMin = new Vector2(start + (i % 3) * .15f, .08f);
                rect.anchorMax = new Vector2(start + (i % 3) * .15f + .14f, .79f);
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                var slotView = instance.GetComponent<LoadoutSlotView>();
                slotView.SetPositionLabel(instance.name);
                if (i < 3) plants[i] = slotView; else spells[i - 3] = slotView;
                PrefabUtility.RecordPrefabInstancePropertyModifications(rect);
                PrefabUtility.RecordPrefabInstancePropertyModifications(instance);
                foreach (var text in instance.GetComponentsInChildren<TMP_Text>()) PrefabUtility.RecordPrefabInstancePropertyModifications(text);
            }
            Refs(view, "plantSlots", plants); Refs(view, "spellSlots", spells);
            Label("Available heading", window, "AVAILABLE OPTIONS — scroll for more", .08f, .41f, .92f, .47f, 30);
            var scrollRoot = Rect("Options scroll", window, new Vector2(.07f, .15f), new Vector2(.93f, .41f));
            var scroll = scrollRoot.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false; scroll.vertical = true; scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40;
            var viewport = Rect("Viewport", scrollRoot, Vector2.zero, Vector2.one);
            Image(viewport, null).color = new Color(1, 1, 1, .01f);
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = Rect("Content", viewport, new Vector2(0, 1), Vector2.one);
            content.pivot = new Vector2(.5f, 1);
            var grid = content.gameObject.AddComponent<GridLayoutGroup>();
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = 3;
            grid.spacing = new Vector2(12, 12); grid.padding = new RectOffset(4, 4, 4, 4);
            grid.cellSize = new Vector2(380, 120);
            var adaptive = content.gameObject.AddComponent<LoadoutOptionGridLayout>(); Ref(adaptive, "grid", grid);
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = content; scroll.viewport = viewport;
            Ref(view, "optionPrefab", option.GetComponent<LoadoutOptionView>());
            Ref(view, "optionContent", content); Ref(view, "optionsScroll", scroll);
            Ref(view, "emptyOptionsLabel", Label("Empty options", viewport, "No available options", .05f, .35f, .95f, .65f, 32));
            var clear = Action("Clear position", window, "Clear position", .08f, .34f);
            var confirm = Action("Confirm", window, "Confirm", .66f, .92f);
            Ref(view, "clearButton", clear); Ref(view, "confirmButton", confirm);
            clear.interactable = false; confirm.interactable = false;
            // All Button events remain empty: a future presenter owns intent and outcomes.
            Save(root, "DailyLoadoutPanel");
        }
        finally { Object.DestroyImmediate(root.gameObject); }
    }

    private static Button Action(string name, Transform parent, string text, float x0, float x1)
    {
        var rect = Rect(name, parent, new Vector2(x0, .04f), new Vector2(x1, .12f));
        var button = Button(rect, normal, hover, pressed);
        Label("Label", rect, text, .06f, .10f, .94f, .90f, 30);
        return button;
    }
    private static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max)
    {
        var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        rect.gameObject.layer = 5; rect.SetParent(parent, false);
        rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }
    private static Image Image(RectTransform rect, Sprite sprite)
    {
        var image = rect.gameObject.AddComponent<Image>(); image.sprite = sprite;
        image.type = sprite != null && sprite.border != Vector4.zero ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
        return image;
    }
    private static Button Button(RectTransform rect, Sprite normalSprite, Sprite hoverSprite, Sprite pressedSprite)
    {
        var image = Image(rect, normalSprite); var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image; button.transition = Selectable.Transition.SpriteSwap;
        button.spriteState = new SpriteState { highlightedSprite = hoverSprite, pressedSprite = pressedSprite, selectedSprite = hoverSprite, disabledSprite = pressedSprite };
        return button;
    }
    private static TMP_Text Label(string name, Transform parent, string text, float x0, float y0, float x1, float y1, float size)
    {
        var label = Rect(name, parent, new Vector2(x0, y0), new Vector2(x1, y1)).gameObject.AddComponent<TextMeshProUGUI>();
        label.font = font; label.text = text; label.fontSize = size; label.color = Color.white;
        label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false;
        label.enableAutoSizing = true; label.fontSizeMin = 16; label.fontSizeMax = size;
        label.overflowMode = TextOverflowModes.Ellipsis;
        return label;
    }
    private static void Ref(Object owner, string field, Object value)
    {
        var serialized = new SerializedObject(owner); var property = serialized.FindProperty(field);
        if (property == null || value == null) throw new InvalidOperationException("Missing UI reference: " + field);
        property.objectReferenceValue = value; serialized.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void Refs(Object owner, string field, Object[] values)
    {
        var serialized = new SerializedObject(owner); var property = serialized.FindProperty(field);
        property.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
    private static GameObject Save(RectTransform root, string name)
    {
        var saved = PrefabUtility.SaveAsPrefabAsset(root.gameObject, Folder + "/" + name + ".prefab");
        if (saved == null) throw new InvalidOperationException("Could not save " + name);
        return saved;
    }
}
