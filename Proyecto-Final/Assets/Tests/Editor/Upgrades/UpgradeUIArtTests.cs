using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

// Read-only asset checks. No prefab authoring or import/play callbacks.
public class UpgradeUIArtTests
{
    internal const string Folder = "Assets/Prefabs/UI/Upgrades/";
    internal const string Sheet = "Assets/Sprites/SpriteSheets/UISprites.png";
    internal const string FontPath = "Assets/Plugins/TextMesh Pro/Fonts/m5x7.asset";
    internal static Type TypeOf(string name, string assembly = "Assembly-CSharp") => Type.GetType(name + ", " + assembly, true);
    internal static object Field(object o, string name) => o.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).GetValue(o);
    internal static object Property(object o, string name) => o.GetType().GetProperty(name).GetValue(o);
    internal static object Call(object o, string name, params object[] args) => o.GetType().GetMethod(name).Invoke(o, args);
    internal static Component[] Components(GameObject root, string name) => root.GetComponentsInChildren<Component>(true).Where(c => c != null && c.GetType().FullName == name).ToArray();
    internal static Sprite Sprite(string name) => AssetDatabase.LoadAllAssetsAtPath(Sheet).OfType<Sprite>().Single(s => s.name == name);
    private static void Identity(UnityEngine.Object asset, string guid, long id)
    {
        Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string actualGuid, out long actualId), Is.True);
        Assert.That(actualGuid, Is.EqualTo(guid)); Assert.That(actualId, Is.EqualTo(id));
    }
    internal static void AssertFrame(GameObject root, object view)
    {
        var background = (Component)Field(view, "background");
        Component frame = background;
        if (root.name.StartsWith("LevelUpPanel") || root.name.StartsWith("SpecialUpgradePanel"))
        {
            Assert.That(background.gameObject, Is.SameAs(root), "Preserve the View's root background binding");
            Assert.That(Property(background, "sprite"), Is.Null, "No TutorialBox at screen edges");
            Assert.That(Property(background, "type").ToString(), Is.EqualTo("Simple"));
            Assert.That((Color)Property(background, "color"), Is.EqualTo(new Color(0, 0, 0, .65f)));
            Assert.That(Property(background, "raycastTarget"), Is.True);
            Assert.That(root.transform.childCount, Is.EqualTo(1));
            var window = root.transform.Find("Window"); Assert.That(window, Is.Not.Null);
            frame = window.GetComponent(TypeOf("UnityEngine.UI.Image", "UnityEngine.UI"));
        }
        Assert.That(Property(frame, "sprite"), Is.SameAs(Sprite("TutorialBox")));
        Assert.That(Property(frame, "type").ToString(), Is.EqualTo("Sliced"));
        Assert.That((Color)Property(frame, "color"), Is.EqualTo(Color.white));
        // Integer pixel borders: modal frame at 8x, compact card/item frame at 4x; no sprite/importer changes.
        float density = root.name.StartsWith("LevelUpPanel") || root.name.StartsWith("SpecialUpgradePanel") ? .78125f : 1.5625f;
        Assert.That((float)Property(frame, "pixelsPerUnitMultiplier"), Is.EqualTo(density));
    }
    [Test] public void ImportedTutorialArtAndFontHaveExactIdentityAndBorders()
    {
        var box = Sprite("TutorialBox"); var button = Sprite("CraftButton");
        Identity(box, "6b876b38e4597704b84b5e76e54d27a1", 2111154574);
        Identity(button, "6b876b38e4597704b84b5e76e54d27a1", 2122693667);
        Assert.That(box.border, Is.EqualTo(new Vector4(3, 4, 3, 3)));
        Assert.That(button.border, Is.EqualTo(Vector4.zero)); Assert.That(button.rect.size, Is.EqualTo(new Vector2(47, 9)));
        var font = AssetDatabase.LoadMainAssetAtPath(FontPath);
        Identity(font, "52be7e01f8fbce54186eac8da2da70cc", 11400000);
        Identity((UnityEngine.Object)Field(font, "material"), "52be7e01f8fbce54186eac8da2da70cc", 7763458193861376953);
    }
    [TestCase("LevelUpPanel", "LevelUpPanelView")]
    [TestCase("LevelUpCard", "LevelUpCardView")]
    [TestCase("SpecialUpgradePanel", "SpecialUpgradePanelView")]
    [TestCase("SpecialUpgradeItem", "SpecialUpgradeItemView")]
    public void FramesActionsAndEveryLabelUseTutorialArt(string asset, string viewName)
    {
        var root = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + asset + ".prefab");
        var view = root.GetComponent(TypeOf(viewName)); Assert.That(Property(view, "IsConfigured"), Is.True);
        AssertFrame(root, view);
        foreach (var label in Components(root, "TMPro.TextMeshProUGUI"))
        {
            Assert.That(Property(label, "font"), Is.SameAs(AssetDatabase.LoadMainAssetAtPath(FontPath)), label.name);
            Identity((UnityEngine.Object)Property(label, "fontSharedMaterial"), "52be7e01f8fbce54186eac8da2da70cc", 7763458193861376953);
            Assert.That(Property(label, "enableVertexGradient"), Is.True, "Readability must survive runtime eligibility/rarity tints");
            Assert.That((bool)Property(label, "raycastTarget"), Is.False);
        }
        foreach (var button in Components(root, "UnityEngine.UI.Button"))
        {
            var graphic = (Component)Property(button, "targetGraphic");
            Assert.That(Property(graphic, "sprite"), Is.SameAs(Sprite("CraftButton")));
            Assert.That(Property(graphic, "type").ToString(), Is.EqualTo("Simple"));
            Assert.That(Property(graphic, "preserveAspect"), Is.True);
            var rect = (RectTransform)graphic.transform;
            Assert.That(rect.anchorMin, Is.EqualTo(rect.anchorMax), "Fixed pixel-art action size");
            Assert.That(rect.sizeDelta.x / rect.sizeDelta.y, Is.EqualTo(47f / 9).Within(.001));
            Assert.That(rect.sizeDelta, Is.EqualTo(new Vector2(164.5f, 31.5f)));
            var state = new SerializedObject(button);
            Assert.That(state.FindProperty("m_Transition").enumValueIndex, Is.EqualTo(1));
            Assert.That(state.FindProperty("m_OnClick.m_PersistentCalls.m_Calls").arraySize, Is.Zero);
            foreach (string name in new[] { "Normal", "Highlighted", "Pressed", "Selected", "Disabled" })
                Assert.That(state.FindProperty("m_AnimationTriggers.m_" + name + "Trigger").stringValue, Is.EqualTo(name));
            if (asset.EndsWith("Card") || asset.EndsWith("Item"))
            {
                Assert.That(graphic.gameObject, Is.Not.SameAs(button.gameObject));
                Assert.That((bool)Property(graphic, "raycastTarget"), Is.True);
                Assert.That(graphic.transform.GetChild(0).GetComponent(TypeOf("TMPro.TextMeshProUGUI", "Unity.TextMeshPro")), Is.Not.Null);
            }
        }
    }

    // ---- Level-up reward card: same imported art as LevelUpPanel/LevelUpCard, elevated above them (S5/S6) ----
    private const string RewardPath = Folder + "LevelUpReward.prefab";
    private static GameObject Reward() => AssetDatabase.LoadAssetAtPath<GameObject>(RewardPath);
    private static Transform Child(GameObject root, string name) => root.GetComponentsInChildren<Transform>(true).Single(t => t.name == name);
    private static SerializedObject ImageOf(Transform t) => new SerializedObject(t.GetComponent(TypeOf("UnityEngine.UI.Image", "UnityEngine.UI")));
    private static Sprite Sliced(Transform t, float density, string what)
    {
        var image = ImageOf(t);
        Assert.That(image.FindProperty("m_Sprite").objectReferenceValue, Is.SameAs(Sprite("TutorialBox")), what);
        Assert.That(image.FindProperty("m_Type").enumValueIndex, Is.EqualTo(1), what + " must be Sliced");
        Assert.That(image.FindProperty("m_Color").colorValue, Is.EqualTo(Color.white), what);
        Assert.That(image.FindProperty("m_PixelsPerUnitMultiplier").floatValue, Is.EqualTo(density), what);
        return (Sprite)image.FindProperty("m_Sprite").objectReferenceValue;
    }
    [Test] public void RewardCardIsCenteredTutorialBoxWindowSmallerThanLevelUpWindow()
    {
        var reward = Reward();
        var card = (RectTransform)Child(reward, "Reward card");
        Sliced(card, .78125f, "Reward card frame uses the same window density as LevelUpPanel/Window");
        Assert.That(card.anchorMin, Is.EqualTo(new Vector2(.5f, .5f))); Assert.That(card.anchorMax, Is.EqualTo(card.anchorMin));
        Assert.That(card.anchoredPosition, Is.EqualTo(Vector2.zero));
        var window = (RectTransform)Child(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "LevelUpPanel.prefab"), "Window");
        Assert.That(card.sizeDelta.x, Is.LessThan(window.sizeDelta.x)); Assert.That(card.sizeDelta.y, Is.LessThan(window.sizeDelta.y));
        Assert.That(card.GetComponent(TypeOf("UnityEngine.UI.Shadow", "UnityEngine.UI")), Is.Not.Null, "Elevation shadow separates the reward from the window behind");
    }
    [TestCase("seed slot")] [TestCase("spell slot")]
    public void RewardSlotsUseTheCardFrame(string slot) => Sliced(Child(Reward(), slot), 1.5625f, slot);
    [Test] public void RewardContinueUsesCraftButtonWithTheSameStatesAsTheCardAction()
    {
        var reward = Reward(); var cont = Child(reward, "Continue");
        var image = ImageOf(cont);
        Assert.That(image.FindProperty("m_Sprite").objectReferenceValue, Is.SameAs(Sprite("CraftButton")));
        Assert.That(image.FindProperty("m_Type").enumValueIndex, Is.EqualTo(0)); Assert.That(image.FindProperty("m_PreserveAspect").boolValue, Is.True);
        var rect = (RectTransform)cont;
        Assert.That(rect.anchorMin, Is.EqualTo(rect.anchorMax)); Assert.That(rect.sizeDelta.x / rect.sizeDelta.y, Is.EqualTo(47f / 9).Within(.001));
        var card = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "LevelUpCard.prefab");
        var expected = new SerializedObject(Components(card, "UnityEngine.UI.Button").Single());
        var actual = new SerializedObject(Components(reward, "UnityEngine.UI.Button").Single());
        Assert.That(actual.FindProperty("m_Transition").enumValueIndex, Is.EqualTo(1));
        foreach (string state in new[] { "Normal", "Highlighted", "Pressed", "Selected", "Disabled" })
            Assert.That(actual.FindProperty("m_Colors.m_" + state + "Color").colorValue, Is.EqualTo(expected.FindProperty("m_Colors.m_" + state + "Color").colorValue), state);
        Assert.That(image.FindProperty("m_Color").colorValue, Is.EqualTo(ImageOf(Child(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "LevelUpPanel.prefab"), "Continue")).FindProperty("m_Color").colorValue));
    }
    [Test] public void RewardLabelsUseM5x7AndTheLevelUpCardReadabilityGradient()
    {
        var reward = Reward(); var font = AssetDatabase.LoadMainAssetAtPath(FontPath);
        var labels = Components(reward, "TMPro.TextMeshProUGUI"); Assert.That(labels.Length, Is.EqualTo(4)); // heading, seed, spell, Continue
        foreach (var label in labels)
        {
            Assert.That(Property(label, "font"), Is.SameAs(font), label.name);
            Identity((UnityEngine.Object)Property(label, "fontSharedMaterial"), "52be7e01f8fbce54186eac8da2da70cc", 7763458193861376953);
            Assert.That(Property(label, "enableVertexGradient"), Is.True, label.name);
            Assert.That((bool)Property(label, "raycastTarget"), Is.False, label.name);
        }
        var panel = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "LevelUpPanel.prefab");
        var heading = Components(reward, "TMPro.TextMeshProUGUI").Single(l => l.name == "Heading");
        var panelHeading = Components(panel, "TMPro.TextMeshProUGUI").Single(l => l.name == "Heading");
        Assert.That((float)Property(heading, "fontSizeMax"), Is.GreaterThan((float)Property(panelHeading, "fontSizeMax")), "Reward heading is larger than the level-up heading");
    }
    [TestCase("Assets/Prefabs/UI/Upgrades/LevelUpReward.prefab", false)]
    [TestCase("Assets/Prefabs/UI/Canvas/Canvas Game UI.prefab", true)]
    [TestCase("Assets/Prefabs/UI/Game UI.prefab", true)]
    public void BackdropDimsEnoughToSeparateModalsFromTheWindowBehind(string path, bool inventory)
    {
        float dim = (float)TypeOf("UIBackdrop").GetField("DimAlpha").GetRawConstantValue();
        Assert.That(dim, Is.GreaterThanOrEqualTo(.75f));
        var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        Component backdrop = inventory
            ? (Component)new SerializedObject(root.GetComponentInChildren(TypeOf("GameStateUIController"), true)).FindProperty("inventoryBackdrop").objectReferenceValue
            : root.GetComponentInChildren(TypeOf("UIBackdrop"), true);
        Assert.That(backdrop, Is.Not.Null);
        Assert.That(ImageOf(backdrop.transform).FindProperty("m_Color").colorValue, Is.EqualTo(new Color(0, 0, 0, dim)));
    }
}
