using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using MagicGarden;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

// Reflection avoids adding Assembly-CSharp / TMP / UGUI asmdef dependencies to the core test assembly.
public class UpgradeUIIntegrationTests
{
    private const string Folder = "Assets/Prefabs/UI/Upgrades/";
    private const string CanvasPath = "Assets/Prefabs/UI/Canvas/Canvas Game UI.prefab";
    private readonly List<UnityEngine.Object> created = new List<UnityEngine.Object>();
    private static Type Production(string name) => Type.GetType(name + ", Assembly-CSharp", true);
    private static object Field(object target, string name) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).GetValue(target);
    private static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name).Invoke(target, args);
    private static object Property(object target, string name) => target.GetType().GetProperty(name).GetValue(target);
    private static Component View(GameObject root, string name) => root.GetComponent(Production(name));
    private Component Clone(string prefab, string view)
    {
        var root = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + prefab + ".prefab"));
        created.Add(root); return View(root, view);
    }
    private static IList DataList(string name, int count)
    {
        var type = Production(name);
        var result = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(type));
        for (int i = 0; i < count; i++)
        {
            var data = Activator.CreateInstance(type);
            foreach (var field in type.GetFields()) if (field.FieldType == typeof(string)) field.SetValue(data, field.Name + " " + i);
            result.Add(data);
        }
        return result;
    }
    private static void Click(object button) => Call(Property(button, "onClick"), "Invoke");
    private static void Listen(object target, string name, Delegate callback) => target.GetType().GetEvent(name).AddEventHandler(target, callback);
    [TearDown] public void Cleanup()
    {
        for (int i = created.Count - 1; i >= 0; i--) if (created[i] != null) UnityEngine.Object.DestroyImmediate(created[i]);
        created.Clear();
    }

    [TestCase("LevelUpPanel", "LevelUpPanelView")]
    [TestCase("LevelUpCard", "LevelUpCardView")]
    [TestCase("SpecialUpgradePanel", "SpecialUpgradePanelView")]
    [TestCase("SpecialUpgradeItem", "SpecialUpgradeItemView")]
    public void FourPersistentPrefabsHaveConfiguredSerializedReferences(string asset, string type)
    {
        string path = Folder + asset + ".prefab";
        Assert.That(File.Exists(path + ".meta"), Is.True);
        var root = AssetDatabase.LoadAssetAtPath<GameObject>(path); Assert.That(root, Is.Not.Null);
        Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(root, out string guid, out long id), Is.True);
        Assert.That(guid.Length, Is.EqualTo(32)); Assert.That(id, Is.Not.Zero);
        var view = View(root, type); Assert.That(view, Is.Not.Null);
        Assert.That(Property(view, "IsConfigured"), Is.True);
        var serialized = new SerializedObject(view); var property = serialized.GetIterator();
        while (property.NextVisible(true))
        {
            if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
            Assert.That(property.objectReferenceValue, Is.Not.Null, asset + "." + property.propertyPath);
            if (property.name.EndsWith("Label"))
            {
                Assert.That(property.objectReferenceValue.GetType().FullName, Is.EqualTo("TMPro.TextMeshProUGUI"));
                Assert.That(Property(property.objectReferenceValue, "font"), Is.Not.Null);
            }
        }
        foreach (var component in root.GetComponentsInChildren<Component>(true))
        {
            Assert.That(component, Is.Not.Null, "Missing script");
            Assert.That(component.GetType().FullName, Is.Not.EqualTo("UnityEngine.UI.Text"));
            Assert.That(component.GetType().FullName, Is.Not.EqualTo("UnityEngine.EventSystems.EventSystem"));
            Assert.That(component, Is.Not.TypeOf<Canvas>());
        }
        UpgradeUIArtTests.AssertFrame(root, view);
    }
    [Test] public void LevelPanelContainsExactlyThreeHorizontalNestedCardSources()
    {
        var root = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "LevelUpPanel.prefab");
        var cards = (Array)Field(View(root, "LevelUpPanelView"), "cards");
        Assert.That(cards.Length, Is.EqualTo(3));
        Assert.That(root.GetComponentsInChildren(Production("LevelUpCardView"), true).Length, Is.EqualTo(3));
        float previous = -1;
        foreach (Component card in cards)
        {
            Assert.That(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(card.gameObject), Is.EqualTo(Folder + "LevelUpCard.prefab"));
            Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(card), Is.Not.Null);
            var rect = (RectTransform)card.transform;
            Assert.That(rect.anchorMin.x, Is.GreaterThan(previous)); previous = rect.anchorMin.x;
            Assert.That(rect.anchorMin.y, Is.EqualTo(.5f)); Assert.That(rect.anchorMax.y, Is.EqualTo(.5f));
            Assert.That(rect.anchorMax, Is.EqualTo(rect.anchorMin));
            Assert.That(rect.sizeDelta, Is.EqualTo(new Vector2(300, 420)));
        }
    }
    [Test] public void ExistingSceneCanvasInheritsPresenterAndInactiveNestedPanelsWithoutNewCanvasOrEventSystem()
    {
        var canvas = AssetDatabase.LoadAssetAtPath<GameObject>(CanvasPath);
        var presenter = View(canvas, "UpgradePanel"); Assert.That(presenter, Is.Not.Null);
        Assert.That(canvas.activeSelf, Is.True); Assert.That(presenter.gameObject, Is.EqualTo(canvas));
        foreach (string field in new[] { "levelUpView", "shopView" })
        {
            var view = (Component)Field(presenter, field);
            Assert.That(view, Is.Not.Null); Assert.That(view.gameObject.activeSelf, Is.False);
            Assert.That(view.transform.parent, Is.EqualTo(canvas.transform));
            Assert.That(view.GetComponentsInChildren<Canvas>(true), Is.Empty);
            Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(view), Is.Not.Null);
        }
        Assert.That(canvas.transform.Find("Panel Crafting UI").gameObject.activeSelf, Is.False);
        string scene = File.ReadAllText("Assets/Scenes/Game.unity");
        string guid = AssetDatabase.AssetPathToGUID(CanvasPath);
        Assert.That(Regex.Matches(scene, "m_SourcePrefab: \\{fileID: 100100000, guid: " + guid).Count, Is.EqualTo(1));
        Assert.That(Regex.Matches(scene, "m_Name: EventSystem\\r?\\n").Count, Is.EqualTo(1));
    }
    [Test] public void AuthoredButtonsSerializeAllStandardStatesAndSimpleCraftTargets()
    {
        var canvas = AssetDatabase.LoadAssetAtPath<GameObject>(CanvasPath);
        var roots = new[] { ((Component)Field(View(canvas, "UpgradePanel"), "levelUpView")).gameObject,
            ((Component)Field(View(canvas, "UpgradePanel"), "shopView")).gameObject,
            AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "SpecialUpgradeItem.prefab") };
        int found = 0;
        foreach (var root in roots)
            foreach (var component in root.GetComponentsInChildren<Component>(true))
            {
                if (component.GetType().FullName != "UnityEngine.UI.Button") continue;
                found++;
                Assert.That(Property(component, "transition").ToString(), Is.EqualTo("ColorTint"));
                Assert.That(Property(Property(component, "targetGraphic"), "type").ToString(), Is.EqualTo("Simple"));
                Assert.That(Property(Property(component, "targetGraphic"), "preserveAspect"), Is.True);
                Assert.That(((UnityEngine.Object)Property(Property(component, "targetGraphic"), "sprite")).name, Is.EqualTo("CraftButton"));
                var frameView = component.GetComponentInParent(Production(root.name.Contains("SpecialUpgradeItem")
                    ? "SpecialUpgradeItemView" : component.name == "LevelUpCard" ? "LevelUpCardView"
                    : component.name == "Close" ? "SpecialUpgradePanelView" : "LevelUpPanelView"), true);
                UpgradeUIArtTests.AssertFrame(frameView.gameObject, frameView);
                var serialized = new SerializedObject(component);
                foreach (string state in new[] { "Normal", "Highlighted", "Pressed", "Selected", "Disabled" })
                {
                    Assert.That(serialized.FindProperty("m_Colors.m_" + state + "Color"), Is.Not.Null);
                    Assert.That(serialized.FindProperty("m_AnimationTriggers.m_" + state + "Trigger").stringValue, Is.EqualTo(state));
                }
                Assert.That(serialized.FindProperty("m_SpriteState"), Is.Not.Null);
            }
        Assert.That(found, Is.EqualTo(6));
    }
    [Test] public void ChoicesReuseFixedCardsHideUnusedAndContinueEmptyWithoutDuplicateCallbacks()
    {
        var view = Clone("LevelUpPanel", "LevelUpPanelView");
        int clicks = 0, selected = -99;
        Listen(view, "Selected", (Action<int>)(i => { clicks++; selected = i; }));
        var cards = (Array)Field(view, "cards");
        var data = DataList("LevelUpCardData", 2);
        Assert.That(Call(view, "Show", data, 4), Is.True); Call(view, "Show", data, 4);
        Assert.That(((Component)cards.GetValue(2)).gameObject.activeSelf, Is.False);
        Click(Field(cards.GetValue(1), "selectButton")); Assert.That(clicks, Is.EqualTo(1)); Assert.That(selected, Is.EqualTo(1));
        Assert.That(Property(Field(cards.GetValue(1), "previewLabel"), "text"), Is.EqualTo("Preview 1"));
        var empty = DataList("LevelUpCardData", 0); Call(view, "Show", empty, 1); Call(view, "Show", empty, 1);
        foreach (Component card in cards) Assert.That(card.gameObject.activeSelf, Is.False);
        Assert.That(((Component)Field(view, "continueButton")).gameObject.activeSelf, Is.True);
        Click(Field(view, "continueButton")); Assert.That(clicks, Is.EqualTo(2)); Assert.That(selected, Is.EqualTo(-1));
        Assert.That(Call(view, "Show", DataList("LevelUpCardData", 4), 1), Is.False);
    }
    [Test] public void ShopInstantiatesOnlyItemPrefabRefreshesEligibilityReusesRowsAndClosesOnce()
    {
        var view = Clone("SpecialUpgradePanel", "SpecialUpgradePanelView");
        var data = DataList("SpecialUpgradeItemData", 2);
        int purchases = 0, closed = 0, selected = -1;
        Listen(view, "Purchased", (Action<int>)(i => { purchases++; selected = i; }));
        Listen(view, "Closed", (Action)(() => closed++));
        Call(view, "Show", data); Call(view, "Show", data);
        var rows = (IList)Field(view, "rows"); Assert.That(rows.Count, Is.EqualTo(2));
        var first = (Component)rows[0];
        Assert.That(first.transform.parent, Is.EqualTo(Field(view, "content")));
        Assert.That(first.gameObject.GetComponentsInChildren<Component>(true).Length,
            Is.EqualTo(((Component)Field(view, "itemPrefab")).gameObject.GetComponentsInChildren<Component>(true).Length));
        Assert.That(Property(Field(first, "purchaseButton"), "interactable"), Is.False);
        Click(Field(first, "purchaseButton")); Assert.That(purchases, Is.Zero);
        data[0].GetType().GetField("CanPurchase").SetValue(data[0], true);
        data[0].GetType().GetField("Costs").SetValue(data[0], "Wood 10/5");
        Call(view, "Show", data); Call(view, "Show", data);
        Assert.That(rows[0], Is.SameAs(first)); Assert.That(rows.Count, Is.EqualTo(2));
        Assert.That(Property(Field(first, "costsLabel"), "text"), Is.EqualTo("Wood 10/5"));
        Assert.That(Property(Field(first, "purchaseButton"), "interactable"), Is.True);
        Click(Field(first, "purchaseButton")); Assert.That(purchases, Is.EqualTo(1)); Assert.That(selected, Is.Zero);
        Click(Field(view, "closeButton")); Assert.That(closed, Is.EqualTo(1));
        Call(view, "Show", DataList("SpecialUpgradeItemData", 0));
        foreach (Component row in rows) Assert.That(row.gameObject.activeSelf, Is.False);
        Assert.That(((Component)Field(view, "emptyLabel")).gameObject.activeSelf, Is.True);
    }
    [Test] public void PresenterInitializationIsIdempotentAndMissingReferencesRejectSafely()
    {
        var root = new GameObject("Test presenter"); created.Add(root);
        var presenter = root.AddComponent(Production("UpgradePanel"));
        var ownerRoot = new GameObject("Test runtime"); created.Add(ownerRoot);
        var owner = ownerRoot.AddComponent(Production("UpgradeRuntime"));
        LogAssert.Expect(LogType.Error, "Magic Garden upgrade UI is missing or has invalid serialized prefab references on Canvas Game UI.");
        Assert.That(Call(presenter, "Initialize", owner), Is.False);
        var level = Clone("LevelUpPanel", "LevelUpPanelView"); var shop = Clone("SpecialUpgradePanel", "SpecialUpgradePanelView");
        var serialized = new SerializedObject(presenter);
        serialized.FindProperty("levelUpView").objectReferenceValue = level; serialized.FindProperty("shopView").objectReferenceValue = shop;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        Assert.That(Call(presenter, "Initialize", owner), Is.True); Assert.That(Call(presenter, "Initialize", owner), Is.True);
        Assert.That(((Delegate)Field(level, "Selected")).GetInvocationList().Length, Is.EqualTo(1));
        Assert.That(((Delegate)Field(shop, "Closed")).GetInvocationList().Length, Is.EqualTo(1));
        Call(presenter, "Unbind", owner);
        Assert.That(Field(level, "Selected"), Is.Null); Assert.That(Field(shop, "Closed"), Is.Null);
    }
    private object Reward(int level, string plantName, string spellName)
    {
        var reward = Activator.CreateInstance(Production("LevelUnlockReward"), level);
        const BindingFlags any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        if (plantName != null)
        {
            var plant = ScriptableObject.CreateInstance(Production("PlantDataSO")); created.Add(plant);
            plant.GetType().GetField("plantName").SetValue(plant, plantName);
            reward.GetType().GetMethod("AddPlant", any).Invoke(reward, new object[] { plant });
        }
        if (spellName != null)
        {
            var spell = ScriptableObject.CreateInstance(Production("SpellDataSO")); created.Add(spell);
            spell.GetType().GetField("spellName", any).SetValue(spell, spellName);
            reward.GetType().GetMethod("AddSpell", any).Invoke(reward, new object[] { spell });
        }
        return reward;
    }
    [Test] public void RewardCardShowsLevelSeedAndSpellOverBackdropAndContinueHidesItOnce()
    {
        var view = Clone("LevelUpReward", "LevelUpRewardView");
        Assert.That(Property(view, "IsConfigured"), Is.True);
        int continued = 0;
        Listen(view, "Continued", (Action)(() => continued++));
        Assert.That(Call(view, "Show", new object[] { null }), Is.False);
        Assert.That(Call(view, "Show", Reward(3, "Sunflower", "Fireball")), Is.True);
        Assert.That(Property(view, "IsVisible"), Is.True);
        var backdrop = (Component)Field(view, "backdrop");
        Assert.That(backdrop.gameObject.activeInHierarchy, Is.True);
        Assert.That((string)Property(Field(view, "headingLabel"), "text"), Does.Contain("3"));
        Assert.That((string)Property(Field(view, "seedLabel"), "text"), Does.Contain("Sunflower"));
        Assert.That((string)Property(Field(view, "spellLabel"), "text"), Does.Contain("Fireball"));
        Click(Field(view, "continueButton")); Click(Field(view, "continueButton"));
        Assert.That(continued, Is.EqualTo(1));
        Assert.That(Property(view, "IsVisible"), Is.False);
        Assert.That(Call(view, "Show", Reward(4, null, "Ice")), Is.True);
        Assert.That(((GameObject)Field(view, "seedSlot")).activeSelf, Is.False);
        Assert.That(((GameObject)Field(view, "spellSlot")).activeSelf, Is.True);
    }
    [Test] public void RewardPrefabIsNestedAboveLevelUpPanelInCanvas()
    {
        var canvas = AssetDatabase.LoadAssetAtPath<GameObject>(CanvasPath);
        var presenter = View(canvas, "UpgradePanel");
        var reward = (Component)Field(presenter, "rewardView");
        var level = (Component)Field(presenter, "levelUpView");
        Assert.That(reward, Is.Not.Null);
        Assert.That(reward.transform.parent, Is.SameAs(level.transform.parent));
        Assert.That(reward.transform.GetSiblingIndex(), Is.GreaterThan(level.transform.GetSiblingIndex()));
        Assert.That(reward.gameObject.activeSelf, Is.False);
    }
    [TestCase(5, 1, 5)]
    [TestCase(5, 3, 3)]
    public void PendingChoiceMapsToTheLevelThatGrantedIt(int current, int pending, int expected)
    {
        var method = Production("UpgradeRuntime").GetMethod("LevelForPendingChoice", BindingFlags.Public | BindingFlags.Static);
        Assert.That(method.Invoke(null, new object[] { current, pending }), Is.EqualTo(expected));
    }
    [Test] public void UnwiredViewsRejectDisplayWithoutConstructingFallbackObjects()
    {
        var root = new GameObject("Unwired view"); created.Add(root);
        var level = root.AddComponent(Production("LevelUpPanelView"));
        var shop = root.AddComponent(Production("SpecialUpgradePanelView"));
        Assert.That(Call(level, "Show", DataList("LevelUpCardData", 0), 1), Is.False);
        Assert.That(Call(shop, "Show", DataList("SpecialUpgradeItemData", 1)), Is.False);
        Call(level, "Hide"); Call(shop, "Hide");
        Assert.That(root.transform.childCount, Is.Zero);
    }
}
