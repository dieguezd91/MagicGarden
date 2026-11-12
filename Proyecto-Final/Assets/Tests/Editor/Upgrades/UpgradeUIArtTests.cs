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
            // The level-up flow's shared blur backdrop dims the screen; the level-up root keeps only an invisible click blocker.
            // The shop has no backdrop of its own and keeps its dark dimmer.
            var expected = root.name.StartsWith("LevelUpPanel") ? new Color(0, 0, 0, 0) : new Color(0, 0, 0, .65f);
            Assert.That((Color)Property(background, "color"), Is.EqualTo(expected));
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

    // ---- Level-up reward card: identity and wiring only. Layout (anchors, sizes, positions) belongs to the prefab and is hand-editable. ----
    private const string RewardPath = Folder + "LevelUpReward.prefab";
    private const string CanvasPath = "Assets/Prefabs/UI/Canvas/Canvas Game UI.prefab";
    private static GameObject Reward() => AssetDatabase.LoadAssetAtPath<GameObject>(RewardPath);
    private static UnityEngine.Object Serialized(object owner, string field) => new SerializedObject((UnityEngine.Object)owner).FindProperty(field).objectReferenceValue;
    private static Sprite SpriteOf(object graphic) => (Sprite)new SerializedObject((UnityEngine.Object)graphic).FindProperty("m_Sprite").objectReferenceValue;
    [Test] public void RewardViewSerializedReferencesAreWiredAndFramedWithTutorialArt()
    {
        var reward = Reward(); var view = reward.GetComponent(TypeOf("LevelUpRewardView"));
        Assert.That(Property(view, "IsConfigured"), Is.True);
        foreach (string field in new[] { "root", "headingLabel", "seedSlot", "spellSlot", "seedIcon", "spellIcon", "seedLabel", "spellLabel", "continueButton", "content", "group" })
            Assert.That(Serialized(view, field), Is.Not.Null, field);
        Assert.That(reward.GetComponentInChildren(TypeOf("UIBackdrop"), true), Is.Null, "the level-up flow owns one backdrop for the reward and the upgrades");
        var box = Sprite("TutorialBox");
        Assert.That(SpriteOf(((Component)Serialized(view, "content")).GetComponent(TypeOf("UnityEngine.UI.Image", "UnityEngine.UI"))), Is.SameAs(box), "card frame");
        foreach (string slot in new[] { "seedSlot", "spellSlot" })
            Assert.That(SpriteOf(((GameObject)Serialized(view, slot)).GetComponent(TypeOf("UnityEngine.UI.Image", "UnityEngine.UI"))), Is.SameAs(box), slot);
        var button = Serialized(view, "continueButton");
        Assert.That(SpriteOf(new SerializedObject(button).FindProperty("m_TargetGraphic").objectReferenceValue), Is.SameAs(Sprite("CraftButton")));
    }
    [Test] public void RewardLabelsUseM5x7()
    {
        var font = AssetDatabase.LoadMainAssetAtPath(FontPath);
        var labels = Components(Reward(), "TMPro.TextMeshProUGUI"); Assert.That(labels, Is.Not.Empty);
        foreach (var label in labels) Assert.That(Property(label, "font"), Is.SameAs(font), label.name);
    }
    [Test] public void LevelUpPanelViewTweenTargetsAreWired()
    {
        var panel = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "LevelUpPanel.prefab"); var view = panel.GetComponent(TypeOf("LevelUpPanelView"));
        Assert.That(Serialized(view, "content"), Is.Not.Null); Assert.That(Serialized(view, "group"), Is.Not.Null);
    }
    // The shared backdrop = a real blur layer under a lighter dark tint. The inventory and the level-up session use the same component.
    private static Component[] Backdrops(string path)
    {
        var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (path == CanvasPath) return new[] { (Component)Serialized(root.GetComponentInChildren(TypeOf("GameStateUIController"), true), "inventoryBackdrop"), SessionBackdrop(root) };
        return new[] { (Component)Serialized(root.GetComponentInChildren(TypeOf("GameStateUIController"), true), "inventoryBackdrop") };
    }
    private static Component SessionBackdrop(GameObject canvas) => (Component)Serialized(canvas.GetComponentInChildren(TypeOf("UpgradePanel"), true), "sessionBackdrop");
    [TestCase(CanvasPath)]
    [TestCase("Assets/Prefabs/UI/Game UI.prefab")]
    public void BackdropHasABlurLayerWithTheBlurShaderUnderALighterTint(string path)
    {
        float dim = (float)TypeOf("UIBackdrop").GetField("DimAlpha").GetRawConstantValue();
        Assert.That(dim, Is.InRange(.3f, .6f), "the blur must read, the tint only supports it");
        foreach (var backdrop in Backdrops(path))
        {
            Assert.That(backdrop, Is.Not.Null);
            var blur = (Component)Serialized(backdrop, "blurImage");
            Assert.That(blur, Is.Not.Null, "RawImage blur layer");
            Assert.That(blur.GetType().FullName, Is.EqualTo("UnityEngine.UI.RawImage"));
            var material = (Material)new SerializedObject(blur).FindProperty("m_Material").objectReferenceValue;
            Assert.That(material, Is.Not.Null); Assert.That(material.shader.name, Is.EqualTo("MagicGarden/UI/BackdropBlur"));
            Assert.That(blur.transform.IsChildOf(backdrop.transform), Is.True, "blur and tint sit under the same CanvasGroup, so they fade together");
            var tint = backdrop.GetComponentInChildren(TypeOf("UnityEngine.UI.Image", "UnityEngine.UI"), true);
            Assert.That(new SerializedObject(tint).FindProperty("m_Color").colorValue, Is.EqualTo(new Color(0, 0, 0, dim)));
            Assert.That(tint.transform.GetSiblingIndex(), Is.GreaterThan(blur.transform.GetSiblingIndex()), "tint is drawn over the blur");
            Assert.That(new SerializedObject(backdrop.GetComponent<CanvasGroup>()).FindProperty("m_Alpha").floatValue, Is.Zero, "starts transparent so the first Show fades in");
        }
    }
    // One backdrop for the whole level-up session: owned by the presenter, behind the reward card and the upgrade panel, in neither of them.
    [Test] public void LevelUpSessionBackdropIsOneSharedInstanceBehindTheRewardAndTheUpgradePanel()
    {
        var canvas = AssetDatabase.LoadAssetAtPath<GameObject>(CanvasPath);
        var presenter = canvas.GetComponentInChildren(TypeOf("UpgradePanel"), true);
        var backdrop = SessionBackdrop(canvas); Assert.That(backdrop, Is.Not.Null);
        var level = (Component)Serialized(presenter, "levelUpView"); var reward = (Component)Serialized(presenter, "rewardView");
        Assert.That(backdrop.transform.parent, Is.SameAs(level.transform.parent)); Assert.That(backdrop.transform.parent, Is.SameAs(reward.transform.parent));
        Assert.That(backdrop.transform.GetSiblingIndex(), Is.LessThan(level.transform.GetSiblingIndex()), "behind the upgrade modal");
        Assert.That(backdrop.transform.GetSiblingIndex(), Is.LessThan(reward.transform.GetSiblingIndex()), "behind the reward card");
        Assert.That(backdrop.gameObject.activeSelf, Is.False);
        Assert.That(backdrop, Is.Not.SameAs(Serialized(canvas.GetComponentInChildren(TypeOf("GameStateUIController"), true), "inventoryBackdrop")), "the inventory keeps its own");
        Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "LevelUpPanel.prefab").GetComponentInChildren(TypeOf("UIBackdrop"), true), Is.Null);
    }
    // Soft fades: slow enough to read as a fade (not a cut), with eased ends, unscaled time (see UIBackdrop). Prefab values override the code defaults, so check both.
    [TestCase(CanvasPath)]
    [TestCase("Assets/Prefabs/UI/Game UI.prefab")]
    public void BackdropFadesInAndOutSlowlyWithEasedEnds(string path)
    {
        foreach (var backdrop in Backdrops(path))
        {
            var so = new SerializedObject(backdrop);
            Assert.That(so.FindProperty("showDuration").floatValue, Is.GreaterThanOrEqualTo(.25f), "show");
            Assert.That(so.FindProperty("hideDuration").floatValue, Is.GreaterThanOrEqualTo(.25f), "hide");
        }
    }
    [Test] public void BackdropCodeDefaultsAreSlowAndEased()
    {
        var go = new GameObject("UIBackdrop defaults");
        try
        {
            var backdrop = go.AddComponent(TypeOf("UIBackdrop"));
            Assert.That((float)Field(backdrop, "showDuration"), Is.GreaterThanOrEqualTo(.25f));
            Assert.That((float)Field(backdrop, "hideDuration"), Is.GreaterThanOrEqualTo(.25f));
            Assert.That(Field(backdrop, "showEase").ToString(), Is.EqualTo("OutQuad"));
            Assert.That(Field(backdrop, "hideEase").ToString(), Is.EqualTo("InQuad"));
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }
}
