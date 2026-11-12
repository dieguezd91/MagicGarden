using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// Explicit one-shot authoring only. Never runs on import, scene load or play, and refuses to overwrite art.
public static class UpgradeUIPrefabAuthoring
{
    public const string Folder = "Assets/Prefabs/UI/Upgrades";
    public const string CanvasPath = "Assets/Prefabs/UI/Canvas/Canvas Game UI.prefab";
    private static TMP_FontAsset font;

    [MenuItem("Tools/Magic Garden/Create upgrade UI prefabs (once)")]
    public static void Generate()
    {
        foreach (string name in new[] { "LevelUpCard", "LevelUpPanel", "SpecialUpgradeItem", "SpecialUpgradePanel" })
            if (File.Exists(Folder + "/" + name + ".prefab"))
                throw new InvalidOperationException("Upgrade UI already exists. Edit its prefabs in the Inspector; this helper will not overwrite them.");
        if (AssetDatabase.LoadAssetAtPath<GameObject>(CanvasPath).GetComponent<UpgradePanel>() != null)
            throw new InvalidOperationException("Canvas already has an upgrade presenter; no regeneration performed.");
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Plugins/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        if (font == null) throw new InvalidOperationException("Existing TMP font is missing; no replacement font generated.");
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Prefabs/UI", "Upgrades");
        var card = CreateCard();
        var item = CreateItem();
        var level = CreateLevelPanel(card);
        var shop = CreateShopPanel(item);
        var canvas = PrefabUtility.LoadPrefabContents(CanvasPath);
        try
        {
            var levelInstance = (GameObject)PrefabUtility.InstantiatePrefab(level, canvas.transform);
            var shopInstance = (GameObject)PrefabUtility.InstantiatePrefab(shop, canvas.transform);
            var presenter = canvas.AddComponent<UpgradePanel>();
            Ref(presenter, "levelUpView", levelInstance.GetComponent<LevelUpPanelView>());
            Ref(presenter, "shopView", shopInstance.GetComponent<SpecialUpgradePanelView>());
            var legacy = canvas.transform.Find("Panel Crafting UI");
            if (legacy == null) throw new InvalidOperationException("Expected legacy crafting panel was not found.");
            legacy.gameObject.SetActive(false);
            PrefabUtility.RecordPrefabInstancePropertyModifications(legacy.gameObject);
            PrefabUtility.SaveAsPrefabAsset(canvas, CanvasPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(canvas); }
        AssetDatabase.SaveAssets();
        foreach (string name in new[] { "LevelUpPanel", "LevelUpCard", "SpecialUpgradePanel", "SpecialUpgradeItem" })
        {
            string path = Folder + "/" + name + ".prefab";
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string guid, out long id) || !File.Exists(path + ".meta"))
                throw new InvalidOperationException("Missing persistent prefab or metadata: " + path);
            Debug.Log($"Upgrade UI authored: {path}, GUID {guid}, root fileID {id}");
        }
        var saved = AssetDatabase.LoadAssetAtPath<GameObject>(CanvasPath).GetComponent<UpgradePanel>();
        if (saved == null) throw new InvalidOperationException("Canvas presenter was not persisted.");
        Debug.Log("Upgrade UI authoring complete: four persistent prefabs, three nested card sources, two nested panels, existing Canvas only.");
    }

    [MenuItem("Tools/Magic Garden/Create level-up reward prefab (once)")]
    public static void GenerateReward()
    {
        string path = Folder + "/LevelUpReward.prefab";
        if (File.Exists(path)) throw new InvalidOperationException("LevelUpReward already exists. Edit it in the Inspector; this helper will not overwrite it.");
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Plugins/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        if (font == null) throw new InvalidOperationException("Existing TMP font is missing; no replacement font generated.");
        var root = Rect("LevelUpReward", null, Vector2.zero, Vector2.one);
        GameObject prefab;
        try
        {
            var view = root.gameObject.AddComponent<LevelUpRewardView>();
            Ref(view, "root", root.gameObject);
            Ref(view, "backdrop", CreateBackdrop(root));
            var card = Rect("Reward card", root, new Vector2(.35f, .22f), new Vector2(.65f, .78f));
            Background(card, true);
            Ref(view, "headingLabel", Label("Heading", card, "LEVEL N — New unlocks", .05f, .82f, .95f, .96f, 36));
            Slot(view, card, "seed", "Seed: name", .54f, .78f);
            Slot(view, card, "spell", "Spell: name", .28f, .52f);
            var continueRect = Rect("Continue", card, new Vector2(.25f, .05f), new Vector2(.75f, .19f));
            var button = Button(continueRect);
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            Ref(view, "continueButton", button);
            Label("Continue label", continueRect, "Continue", 0, 0, 1, 1, 32);
            root.gameObject.SetActive(false);
            prefab = Save(root, "LevelUpReward");
        }
        finally { UnityEngine.Object.DestroyImmediate(root.gameObject); }
        var canvas = PrefabUtility.LoadPrefabContents(CanvasPath);
        try
        {
            var presenter = canvas.GetComponent<UpgradePanel>();
            if (presenter == null) throw new InvalidOperationException("Canvas has no UpgradePanel presenter.");
            var serialized = new SerializedObject(presenter);
            var level = (Component)serialized.FindProperty("levelUpView").objectReferenceValue;
            if (level == null) throw new InvalidOperationException("UpgradePanel.levelUpView is not wired.");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, level.transform.parent);
            instance.transform.SetSiblingIndex(level.transform.GetSiblingIndex() + 1);
            Ref(presenter, "rewardView", instance.GetComponent<LevelUpRewardView>());
            PrefabUtility.SaveAsPrefabAsset(canvas, CanvasPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(canvas); }
        AssetDatabase.SaveAssets();
        Debug.Log("Level-up reward prefab authored and nested above LevelUpPanel: " + path);
    }
    // Shared dimmed backdrop (UIBackdrop): full-screen dark image that also blocks clicks to UI behind it.
    public static UIBackdrop CreateBackdrop(RectTransform parent)
    {
        var rect = Rect("Backdrop", parent, Vector2.zero, Vector2.one);
        var image = rect.gameObject.AddComponent<Image>();
        image.color = new Color(0f, 0f, 0f, .6f); image.raycastTarget = true;
        rect.gameObject.AddComponent<CanvasGroup>();
        return rect.gameObject.AddComponent<UIBackdrop>();
    }
    private static void Slot(LevelUpRewardView view, RectTransform card, string kind, string text, float y0, float y1)
    {
        var slot = Rect(kind + " slot", card, new Vector2(.05f, y0), new Vector2(.95f, y1));
        Ref(view, kind + "Slot", slot.gameObject);
        Ref(view, kind + "Icon", Icon(slot, new Vector2(0, .05f), new Vector2(.28f, .95f)));
        var label = Label(kind + " label", slot, text, .32f, 0, 1, 1, 30);
        label.alignment = TextAlignmentOptions.Left;
        Ref(view, kind + "Label", label);
    }
    private static GameObject CreateCard()
    {
        var root = Rect("LevelUpCard", null, Vector2.zero, Vector2.one);
        try
        {
            var view = root.gameObject.AddComponent<LevelUpCardView>();
            var button = Button(root);
            Ref(view, "selectButton", button); Ref(view, "background", button.image);
            Ref(view, "icon", Icon(root, new Vector2(.34f, .63f), new Vector2(.66f, .81f)));
            Ref(view, "rarityLabel", Label("Rarity", root, "RARITY", .04f, .85f, .96f, .97f, 28));
            Ref(view, "targetLabel", Label("Target", root, "Owned target", .04f, .53f, .96f, .64f, 28));
            Ref(view, "nameLabel", Label("Name", root, "Upgrade name", .04f, .41f, .96f, .53f, 28));
            Ref(view, "statLabel", Label("Stat", root, "Stat", .04f, .31f, .96f, .41f, 28));
            Ref(view, "bonusLabel", Label("Bonus", root, "+bonus", .04f, .20f, .96f, .31f, 32));
            Ref(view, "previewLabel", Label("Current to result", root, "Current → result", .03f, .05f, .97f, .20f, 32));
            return Save(root, "LevelUpCard");
        }
        finally { UnityEngine.Object.DestroyImmediate(root.gameObject); }
    }
    private static GameObject CreateItem()
    {
        var root = Rect("SpecialUpgradeItem", null, Vector2.zero, Vector2.one);
        try
        {
            root.sizeDelta = new Vector2(1400, 220);
            var layout = root.gameObject.AddComponent<LayoutElement>(); layout.preferredHeight = 220; layout.minHeight = 220;
            var view = root.gameObject.AddComponent<SpecialUpgradeItemView>();
            var button = Button(root);
            Ref(view, "purchaseButton", button); Ref(view, "background", button.image);
            Ref(view, "icon", Icon(root, new Vector2(.015f, .20f), new Vector2(.12f, .80f)));
            Ref(view, "targetLabel", Label("Target", root, "Owned target", .14f, .76f, .70f, .97f, 26));
            Ref(view, "nameLabel", Label("Name", root, "Special upgrade", .14f, .55f, .82f, .76f, 28));
            Ref(view, "stacksLabel", Label("Stacks", root, "0/1", .84f, .76f, .97f, .97f, 26));
            Ref(view, "descriptionLabel", Label("Description", root, "Effect description", .14f, .35f, .97f, .55f, 24));
            Ref(view, "costsLabel", Label("Costs", root, "Materials owned/cost", .14f, .16f, .97f, .35f, 24));
            Ref(view, "prerequisitesLabel", Label("Prerequisites", root, "Requirements", .14f, .01f, .97f, .16f, 22));
            return Save(root, "SpecialUpgradeItem");
        }
        finally { UnityEngine.Object.DestroyImmediate(root.gameObject); }
    }
    private static GameObject CreateLevelPanel(GameObject card)
    {
        var root = Rect("LevelUpPanel", null, Vector2.zero, Vector2.one);
        try
        {
            var view = root.gameObject.AddComponent<LevelUpPanelView>();
            Ref(view, "root", root.gameObject); Ref(view, "background", Background(root, true));
            Ref(view, "headingLabel", Label("Heading", root, "LEVEL UP — Choose a blessing", .04f, .86f, .96f, .97f, 38));
            Ref(view, "helpLabel", Label("Help", root, "Damage: base hit • Area: radius • Plant range: detection/attachment\nApplies to this owned line, including future instances.", .07f, .02f, .93f, .13f, 24));
            Ref(view, "emptyLabel", Label("Empty pool", root, "All owned upgrades are exhausted.", .20f, .48f, .80f, .66f, 32));
            var continueRect = Rect("Continue", root, new Vector2(.35f, .30f), new Vector2(.65f, .43f));
            Ref(view, "continueButton", Button(continueRect));
            Label("Continue label", continueRect, "Continue", 0, 0, 1, 1, 32);
            var cards = new LevelUpCardView[3];
            var container = Rect("Three fixed horizontal cards", root, new Vector2(.05f, .16f), new Vector2(.95f, .82f));
            for (int i = 0; i < cards.Length; i++)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(card, container);
                var rect = (RectTransform)instance.transform;
                rect.anchorMin = new Vector2(i / 3f + .01f, 0); rect.anchorMax = new Vector2((i + 1) / 3f - .01f, 1);
                rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
                PrefabUtility.RecordPrefabInstancePropertyModifications(rect);
                cards[i] = instance.GetComponent<LevelUpCardView>();
            }
            var serialized = new SerializedObject(view);
            var slots = serialized.FindProperty("cards"); slots.arraySize = cards.Length;
            for (int i = 0; i < cards.Length; i++) slots.GetArrayElementAtIndex(i).objectReferenceValue = cards[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
            root.gameObject.SetActive(false);
            return Save(root, "LevelUpPanel");
        }
        finally { UnityEngine.Object.DestroyImmediate(root.gameObject); }
    }
    private static GameObject CreateShopPanel(GameObject item)
    {
        var root = Rect("SpecialUpgradePanel", null, Vector2.zero, Vector2.one);
        try
        {
            var view = root.gameObject.AddComponent<SpecialUpgradePanelView>();
            Ref(view, "root", root.gameObject); Ref(view, "background", Background(root, true));
            Ref(view, "headingLabel", Label("Heading", root, "CAULDRON — Special upgrades (materials, not level-up choices)", .04f, .88f, .96f, .98f, 34));
            Ref(view, "emptyLabel", Label("Empty shop", root, "No owned compatible targets yet.", .20f, .40f, .80f, .60f, 32));
            var close = Rect("Close", root, new Vector2(.80f, .02f), new Vector2(.94f, .10f));
            Ref(view, "closeButton", Button(close)); Label("Close label", close, "Close", 0, 0, 1, 1, 30);
            var scrollRoot = Rect("Specials scroll", root, new Vector2(.08f, .13f), new Vector2(.92f, .85f));
            var scroll = scrollRoot.gameObject.AddComponent<ScrollRect>(); scroll.horizontal = false;
            var viewport = Rect("Viewport", scrollRoot, Vector2.zero, Vector2.one);
            Background(viewport, false).color = Color.clear;
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = Rect("Content", viewport, new Vector2(0, 1), Vector2.one); content.pivot = new Vector2(.5f, 1);
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 16; layout.childControlHeight = true; layout.childControlWidth = true;
            layout.childForceExpandHeight = false; layout.childForceExpandWidth = true;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport; scroll.content = content;
            Ref(view, "scroll", scroll); Ref(view, "content", content); Ref(view, "itemPrefab", item.GetComponent<SpecialUpgradeItemView>());
            root.gameObject.SetActive(false);
            return Save(root, "SpecialUpgradePanel");
        }
        finally { UnityEngine.Object.DestroyImmediate(root.gameObject); }
    }
    private static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max)
    {
        var go = new GameObject(name, typeof(RectTransform)); go.layer = 5;
        var rect = (RectTransform)go.transform; rect.SetParent(parent, false);
        rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
        return rect;
    }
    private static Image Background(RectTransform root, bool dark)
    {
        var image = root.gameObject.AddComponent<Image>(); image.type = Image.Type.Sliced;
        image.color = dark ? new Color(.025f, .025f, .055f, .96f) : new Color(.10f, .13f, .19f);
        return image; // Deliberately no generated/default sprite; assign final 9-slice art in Inspector.
    }
    private static Image Icon(Transform root, Vector2 min, Vector2 max)
    {
        var image = Rect("Target icon", root, min, max).gameObject.AddComponent<Image>();
        image.preserveAspect = true; image.raycastTarget = false; image.enabled = false; return image;
    }
    private static Button Button(RectTransform rect)
    {
        var image = Background(rect, false); var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
        button.transition = Selectable.Transition.ColorTint;
        button.colors = new ColorBlock
        {
            normalColor = Color.white, highlightedColor = new Color(1.35f, 1.35f, 1.35f), pressedColor = new Color(.7f, .7f, .7f),
            selectedColor = new Color(1.2f, 1.2f, 1.45f), disabledColor = new Color(.45f, .45f, .45f, .7f), colorMultiplier = 1, fadeDuration = .1f
        };
        // All standard Selectable transitions remain Inspector configurable, including SpriteSwap/Animation.
        button.animationTriggers = new AnimationTriggers
        { normalTrigger = "Normal", highlightedTrigger = "Highlighted", pressedTrigger = "Pressed", selectedTrigger = "Selected", disabledTrigger = "Disabled" };
        return button;
    }
    private static TMP_Text Label(string name, Transform parent, string text, float x0, float y0, float x1, float y1, float size)
    {
        var label = Rect(name, parent, new Vector2(x0, y0), new Vector2(x1, y1)).gameObject.AddComponent<TextMeshProUGUI>();
        label.font = font; label.text = text; label.fontSize = size; label.color = Color.white;
        label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false;
        label.enableAutoSizing = true; label.fontSizeMin = 16; label.fontSizeMax = size;
        return label;
    }
    private static void Ref(UnityEngine.Object owner, string property, UnityEngine.Object value)
    {
        var serialized = new SerializedObject(owner); serialized.FindProperty(property).objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
    private static GameObject Save(RectTransform root, string name)
    {
        var saved = PrefabUtility.SaveAsPrefabAsset(root.gameObject, Folder + "/" + name + ".prefab");
        if (saved == null) throw new InvalidOperationException("Could not persist " + name);
        return saved;
    }
}
