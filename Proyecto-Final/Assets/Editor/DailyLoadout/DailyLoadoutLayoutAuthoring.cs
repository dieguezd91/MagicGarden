using System;
using MagicGarden.LoadoutUI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Explicit native prefab layout migration. Reuses existing art, slots, and plant catalog identities.</summary>
public static class DailyLoadoutLayoutAuthoring
{
    private const string Path = DailyLoadoutDragAuthoring.PanelPath;

    [MenuItem("Tools/Magic Garden/Daily Loadout/Apply reference category layout")]
    public static void BindPrefab()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage() != null)
            throw new InvalidOperationException("Close Play Mode and Prefab Mode before authoring the loadout layout.");
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Path);
        foreach (var component in asset.GetComponentsInChildren<Component>(true))
            if (component != null && (EditorUtility.IsDirty(component) || EditorUtility.IsDirty(component.gameObject)))
                throw new InvalidOperationException("Save/review unsaved loadout prefab edits first.");
        var root = PrefabUtility.LoadPrefabContents(Path);
        try
        {
            Configure(root.GetComponent<DailyLoadoutPanelView>());
            PrefabUtility.SaveAsPrefabAsset(root, Path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    public static void Configure(DailyLoadoutPanelView view)
    {
        var data = new SerializedObject(view);
        var window = (RectTransform)view.transform.Find("Stone window");
        var table = (RectTransform)window.Find("Main stone table");
        var title = window.Find("Title").GetComponent<TMP_Text>();
        var art = table.GetComponent<Image>();
        Box(window, 0, 0, 1440, 1000);
        Label(title, "PREPARE LOADOUT", -180, 435, 960, 60, 40, TextAlignmentOptions.Left);
        Label(Text(window, "Loadout instruction", title), "Choose the plants and spells for your next defense.",
            -180, 385, 960, 40, 26);

        var badge = Panel(window, "Daily allowance badge", art);
        Box(badge, 470, 416, 320, 100);
        var allowance = Reference<TMP_Text>(data, "allowanceLabel");
        allowance.transform.SetParent(badge, false);
        Label(allowance, "1 CHANGE PER DAY\nAVAILABLE", 0, 0, 290, 76, 26, TextAlignmentOptions.Center);
        var close = Reference<Button>(data, "closeButton") ?? Reference<Button>(data, "cancelButton");
        Box((RectTransform)close.transform, 670, 440, 56, 56);

        Box(table, -216, 145, 928, 344);
        Label(Text(table, "Equipment heading", title), "YOUR EQUIPMENT", 0, 145, 864, 32, 28);
        Label(table.Find("Plants category").GetComponent<TMP_Text>(), "PLANTS", -228, 108, 420, 28, 24);
        Label(table.Find("Spells category").GetComponent<TMP_Text>(), "SPELLS", 228, 108, 420, 28, 24);
        for (int i = 0; i < 3; i++)
        {
            Slot(view.PlantSlots[i], -372 + 144 * i, "Plant " + (i + 1));
            Slot(view.SpellSlots[i], 84 + 144 * i, "Spell " + (i + 1));
        }

        var available = Panel(window, "Available panel", art);
        Box(available, -216, -215, 928, 330);
        TMP_Text availableHeading = null;
        foreach (var text in window.GetComponentsInChildren<TMP_Text>(true))
            if (text.text.StartsWith("AVAILABLE OPTIONS", StringComparison.Ordinal)) { availableHeading = text; break; }
        if (availableHeading != null)
        {
            var provisional = available.Find("AvailableOptions");
            if (provisional != null && provisional != availableHeading.transform) UnityEngine.Object.DestroyImmediate(provisional.gameObject);
            availableHeading.name = "AvailableOptions";
        }
        else availableHeading = Text(available, "AvailableOptions", title);
        availableHeading.transform.SetParent(available, false);
        Label(availableHeading, "AVAILABLE", -275, 130, 300, 34, 28);
        Label(Text(available, "Catalog instruction", title), "Drag an icon to a position, or select both with a click.",
            0, 82, 864, 34, 24);
        var plantTab = Button(available, "Plants tab", view.ClearButton, "PLANTS", 195, 130, 140, 48);
        var spellTab = Button(available, "Spells tab", view.ClearButton, "SPELLS", 347, 130, 140, 48);

        var plants = Rect(available, "Plant options panel");
        Stretch(plants, new Vector2(24, 20), new Vector2(-24, -112));
        var scroll = Reference<ScrollRect>(data, "optionsScroll");
        scroll.transform.SetParent(plants, false);
        ConfigureScroll(scroll);
        var empty = Reference<TMP_Text>(data, "emptyOptionsLabel");
        empty.name = "Empty options"; empty.transform.SetParent(plants, false);
        Label(empty, "No unlocked plants available.", 0, 0, 800, 80, 26, TextAlignmentOptions.Center);

        var spells = available.Find("Spell options panel") as RectTransform;
        if (spells == null)
        {
            spells = (RectTransform)UnityEngine.Object.Instantiate(plants.gameObject, available).transform;
            spells.name = "Spell options panel";
        }
        Stretch(spells, new Vector2(24, 20), new Vector2(-24, -112));
        var spellScroll = spells.GetComponentInChildren<ScrollRect>(true);
        ConfigureScroll(spellScroll);
        var spellEmpty = spells.Find("Empty options").GetComponent<TMP_Text>();
        Label(spellEmpty, "No unlocked spells available.", 0, 0, 800, 80, 26, TextAlignmentOptions.Center);
        SetPool(data, "spellOptionViews", spellScroll.content.GetComponentsInChildren<LoadoutOptionView>(true));
        plants.gameObject.SetActive(true); spells.gameObject.SetActive(false);
        empty.gameObject.SetActive(false); spellEmpty.gameObject.SetActive(false);

        var detail = Panel(window, "Selected position panel", art);
        Box(detail, 480, -215, 416, 330);
        Label(Text(detail, "Detail heading", title), "SELECTED POSITION", 0, 130, 360, 34, 26);
        var position = Text(detail, "Selected position", title);
        Label(position, "Plant 1", 0, 75, 352, 46, 34);
        var equipment = Text(detail, "Selected equipment", title);
        Label(equipment, "Empty", 45, 12, 246, 60, 26);
        var selectedIcon = detail.Find("Selected icon")?.GetComponent<Image>();
        if (selectedIcon == null) selectedIcon = New(detail, "Selected icon").gameObject.AddComponent<Image>();
        Box((RectTransform)selectedIcon.transform, -143, 12, 64, 64);
        selectedIcon.preserveAspect = true; selectedIcon.raycastTarget = false; selectedIcon.enabled = false;
        var state = Text(detail, "Selected state", title);
        Label(state, "Selected", 45, -38, 246, 30, 24);
        var feedback = Text(detail, "Draft feedback", title);
        Label(feedback, "Changes remain a draft until you confirm your Loadout.", 0, -108, 352, 82, 24);

        Box((RectTransform)view.ClearButton.transform, -560, -440, 240, 72);
        Box((RectTransform)view.ConfirmButton.transform, 550, -440, 260, 72);
        var cancel = Button(window, "Cancel draft", view.ClearButton, "CANCEL", 270, -440, 240, 72);
        Label(Text(window, "Discard instruction", title), "Closing discards\ndraft changes.", 20, -440, 220, 60, 24);
        Bind(data, "plantOptionsPanel", plants.gameObject); Bind(data, "spellOptionsPanel", spells.gameObject);
        Bind(data, "plantTabButton", plantTab); Bind(data, "spellTabButton", spellTab);
        Bind(data, "closeButton", close); Bind(data, "cancelButton", cancel);
        Bind(data, "spellOptionContent", spellScroll.content); Bind(data, "spellOptionsScroll", spellScroll);
        Bind(data, "spellEmptyOptionsLabel", spellEmpty); Bind(data, "selectedPositionLabel", position);
        Bind(data, "selectedEquipmentLabel", equipment); Bind(data, "selectedEquipmentIcon", selectedIcon);
        Bind(data, "selectedStateLabel", state); Bind(data, "feedbackLabel", feedback);
        data.ApplyModifiedPropertiesWithoutUndo();
        DailyLoadoutDragAuthoring.ConfigurePanel(view);
        DailyLoadoutPrefabPreviewAuthoring.Populate(view);
    }

    private static void ConfigureScroll(ScrollRect scroll)
    {
        Stretch((RectTransform)scroll.transform, Vector2.zero, Vector2.zero);
        scroll.horizontal = false; scroll.vertical = true;
        var content = scroll.content;
        content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one;
        content.pivot = new Vector2(.5f, 1); content.anchoredPosition = Vector2.zero; content.sizeDelta = Vector2.zero;
        var grid = content.GetComponent<GridLayoutGroup>();
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = 2;
        grid.padding = new RectOffset(12, 12, 12, 12); grid.spacing = new Vector2(16, 16);
        foreach (var option in content.GetComponentsInChildren<LoadoutOptionView>(true))
        {
            var data = new SerializedObject(option);
            Reference<TMP_Text>(data, "stateLabel").fontSize = 24;
        }
        scroll.verticalNormalizedPosition = 1;
    }

    private static void Slot(LoadoutSlotView slot, float x, string position)
    {
        Box((RectTransform)slot.transform, x, -26, 128, 236);
        var data = new SerializedObject(slot);
        Label(Reference<TMP_Text>(data, "positionLabel"), position, 0, 98, 120, 28, 24, TextAlignmentOptions.Center);
        Label(Reference<TMP_Text>(data, "nameLabel"), "Empty", 0, -62, 120, 52, 24, TextAlignmentOptions.Center);
        Label(Reference<TMP_Text>(data, "stateLabel"), "Empty", 0, -102, 120, 28, 24, TextAlignmentOptions.Center);
        var icon = Reference<Image>(data, "icon");
        Box((RectTransform)icon.transform.parent, 0, 14, 96, 96);
        Box((RectTransform)icon.transform, 0, 0, 64, 64);
    }

    private static T Reference<T>(SerializedObject data, string field) where T : UnityEngine.Object
        => data.FindProperty(field).objectReferenceValue as T;
    private static void Bind(SerializedObject data, string field, UnityEngine.Object value)
        => data.FindProperty(field).objectReferenceValue = value;
    private static void SetPool(SerializedObject data, string field, LoadoutOptionView[] options)
    {
        var pool = data.FindProperty(field); pool.arraySize = options.Length;
        for (int i = 0; i < options.Length; i++) pool.GetArrayElementAtIndex(i).objectReferenceValue = options[i];
    }
    private static RectTransform New(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform)); go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false); return (RectTransform)go.transform;
    }
    private static RectTransform Rect(Transform parent, string name) => parent.Find(name) as RectTransform ?? New(parent, name);
    private static RectTransform Panel(Transform parent, string name, Image art)
    {
        var rect = Rect(parent, name); var image = rect.GetComponent<Image>();
        if (image == null) { image = rect.gameObject.AddComponent<Image>(); EditorUtility.CopySerialized(art, image); }
        image.raycastTarget = false; return rect;
    }
    private static TMP_Text Text(Transform parent, string name, TMP_Text template)
    {
        var existing = parent.Find(name)?.GetComponent<TMP_Text>();
        if (existing != null) return existing;
        var text = New(parent, name).gameObject.AddComponent<TextMeshProUGUI>();
        text.font = template.font; text.fontSharedMaterial = template.fontSharedMaterial; text.color = template.color;
        text.raycastTarget = false; return text;
    }
    private static void Label(TMP_Text label, string value, float x, float y, float width, float height, float size,
        TextAlignmentOptions alignment = TextAlignmentOptions.Left)
    {
        Box((RectTransform)label.transform, x, y, width, height);
        label.text = value; label.fontSize = size; label.enableAutoSizing = false;
        label.alignment = alignment; label.enableWordWrapping = true; label.overflowMode = TextOverflowModes.Ellipsis;
    }
    private static Button Button(Transform parent, string name, Button template, string caption, float x, float y, float width, float height)
    {
        var button = parent.Find(name)?.GetComponent<Button>();
        if (button == null) { button = UnityEngine.Object.Instantiate(template, parent); button.name = name; }
        Box((RectTransform)button.transform, x, y, width, height); button.interactable = true;
        button.GetComponentInChildren<TMP_Text>(true).text = caption; return button;
    }
    private static void Box(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = new Vector2(x, y); rect.sizeDelta = new Vector2(width, height);
    }
    private static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.pivot = new Vector2(.5f, .5f);
        rect.offsetMin = min; rect.offsetMax = max;
    }
}
