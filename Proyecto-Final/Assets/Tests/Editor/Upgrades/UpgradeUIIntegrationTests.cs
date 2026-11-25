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
    [TestCase("storm-rose")]
    [TestCase("expanded-blast")]
    [TestCase("other-attack-plant")]
    [TestCase("master-volley")]
    public void CauldronUsesTheTargetAndPurchaseLevelForLabelsEligibilityAndSpending(string recipe)
    {
        var inventory = NewComponent("InventoryManager");
        var flow = NewComponent("GameFlowController");
        var spells = NewComponent("SpellInventory");
        var runtime = flow.gameObject.AddComponent(Production("UpgradeRuntime"));
        var presenter = NewComponent("UpgradePanel");
        var shop = Clone("SpecialUpgradePanel", "SpecialUpgradePanelView");
        var serialized = new SerializedObject(presenter);
        serialized.FindProperty("levelUpView").objectReferenceValue = Clone("LevelUpPanel", "LevelUpPanelView");
        serialized.FindProperty("shopView").objectReferenceValue = shop;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        Assert.That(Call(presenter, "Initialize", runtime), Is.True);
        SetField(runtime, "panel", presenter);
        // Round-trip the same draft model that the balance editor saves.
        var draft = JsonUtility.FromJson<Balance>(Resources.Load<TextAsset>("MagicGardenBalance").text);
        var balance = JsonUtility.FromJson<Balance>(JsonUtility.ToJson(draft));
        runtime.GetType().GetProperty("Balance").SetValue(runtime, balance);
        var state = (RunState)Property(runtime, "State");

        const int spectral = 1, windwalker = 2, voltaic = 3, ember = 4, stellar = 5, heart = 14, gold = 15;
        int[] tracked = { spectral, windwalker, voltaic, ember, stellar, heart, gold };
        object Material(int id) => Enum.ToObject(Production("MaterialType"), id);
        void Add(int material, int amount) => Call(inventory, "AddMaterial", Material(material), amount);
        int[] Wallet() => Array.ConvertAll(tracked, id => (int)Call(inventory, "GetResourceAmount", Material(id)));

        // EditMode does not run the singleton Awake methods. Swap and restore the scene dependencies for this test only.
        var instances = new[] { inventory, flow, spells };
        var singletons = Array.ConvertAll(instances, component => component.GetType().GetField(
            component == inventory ? "_instance" : "<Instance>k__BackingField", BindingFlags.NonPublic | BindingFlags.Static));
        var previous = Array.ConvertAll(singletons, field => field.GetValue(null));
        try
        {
            for (int i = 0; i < singletons.Length; i++) singletons[i].SetValue(null, instances[i]);
            SetField(flow, "currentPhase", Enum.Parse(Production("GamePhase"), "Day"));
            bool area = recipe == "expanded-blast", other = recipe == "other-attack-plant", master = recipe == "master-volley";
            string definitionId = area ? "wide-explosion" : master ? "branching-volley-master" : "branching-volley";
            var definition = Array.Find(balance.specials, special => special.id == definitionId);
            string targetId;
            if (area)
            {
                var spell = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/Scriptables/Spells/Spell_Area.asset");
                var unlock = spells.GetType().GetMethod("UnlockSpell", new[] { Production("SpellDataSO") });
                Assert.That(unlock.Invoke(spells, new object[] { spell }), Is.True);
                targetId = (string)Property(spell, "UpgradeTargetId");
                Assert.That(targetId, Is.EqualTo("spell:2"));
            }
            else
            {
                string plantName = other ? "Astral Water Lily" : "Storm Rose";
                var plantPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Plants/Hybrid/{plantName}.prefab");
                var plant = UnityEngine.Object.Instantiate(plantPrefab); created.Add(plant);
                targetId = (string)Property(View(plant, "AttackPlant"), "UpgradeId");
                Assert.That(targetId, Is.EqualTo(other ? "plant:AstralWaterLily" : "plant:StormRose"));
            }
            Call(runtime, "RefreshTargets");
            var target = (Target)Call(runtime, "FindTarget", targetId);
            Assert.That(target, Is.Not.Null);
            if (master) Assert.That(state.Buy(Array.Find(balance.specials, special => special.id == "branching-volley"), target), Is.True);
            var effect = definition.effects[0];
            float startingEffect = state.Effect(targetId, effect.kind);

            void Row(string materials, bool affordable, int stacks)
            {
                Call(presenter, "ShowShop");
                var rows = (IList)Field(shop, "rows");
                Component match = null;
                foreach (Component row in rows)
                    if (row.gameObject.activeSelf && Equals(Property(Field(row, "targetLabel"), "text"), target.Name) &&
                        Equals(Property(Field(row, "nameLabel"), "text"), definition.displayName)) match = row;
                Assert.That(match, Is.Not.Null, "target recipe row");
                Assert.That(Property(Field(match, "costsLabel"), "text"), Is.EqualTo("<sprite name=\"GoldIcon\"> 200/200 + " + materials));
                Assert.That(Property(Field(match, "stacksLabel"), "text"), Is.EqualTo($"{stacks}/{definition.maxStacks}"));
                Assert.That(Property(Field(match, "purchaseButton"), "interactable"), Is.EqualTo(affordable));
            }
            void Attempt(bool succeeds, int stacks, params int[] spent)
            {
                int[] before = Wallet();
                Assert.That(Call(runtime, "CanPurchase", definition, target), Is.EqualTo(succeeds));
                Assert.That(Call(runtime, "Purchase", definition, targetId), Is.EqualTo(succeeds));
                for (int i = 0; i < spent.Length; i += 2) before[Array.IndexOf(tracked, spent[i])] -= spent[i + 1];
                Assert.That(Wallet(), Is.EqualTo(before));
                Assert.That(state.SpecialStacks(definition.id, targetId), Is.EqualTo(stacks));
                Assert.That(state.Effect(targetId, effect.kind), Is.EqualTo(startingEffect + stacks * effect.value).Within(.0001f));
            }

            Call(inventory, "SetGold", 200);
            if (area)
            {
                Add(ember, 2); Add(spectral, 2);
                Row("<sprite name=\"EternalEmber\"> 2/3 + <sprite name=\"SpectralCrystal\"> 2/2", false, 0);
                Attempt(false, 0);
                Add(ember, 1);
                Row("<sprite name=\"EternalEmber\"> 3/3 + <sprite name=\"SpectralCrystal\"> 2/2", true, 0);
                Attempt(true, 1, gold, 200, ember, 3, spectral, 2);
                Call(inventory, "SetGold", 200); Add(ember, 5); Add(spectral, 4);
                Row("<sprite name=\"EternalEmber\"> 5/5 + <sprite name=\"SpectralCrystal\"> 4/4 + BossHeart 0/1", false, 1);
                Attempt(false, 1);
                Add(heart, 1);
                Row("<sprite name=\"EternalEmber\"> 5/5 + <sprite name=\"SpectralCrystal\"> 4/4 + BossHeart 1/1", true, 1);
                Attempt(true, 2, gold, 200, ember, 5, spectral, 4, heart, 1);
            }
            else if (other)
            {
                for (int stack = 0; stack < 2; stack++)
                {
                    Call(inventory, "SetGold", 200); Add(windwalker, 3); Add(voltaic, 2);
                    Row("<sprite name=\"WindwalkerEssence\"> 3/3 + <sprite name=\"VoltaicCore\"> 2/2", true, stack);
                    Attempt(true, stack + 1, gold, 200, windwalker, 3, voltaic, 2);
                }
            }
            else if (master)
            {
                Add(voltaic, 5);
                Row("<sprite name=\"VoltaicCore\"> 5/5", true, 0);
                Attempt(true, 1, gold, 200, voltaic, 5);
            }
            else
            {
                Add(stellar, 4);
                Row("StellarFragment 4/5", false, 0);
                Attempt(false, 0);
                Add(stellar, 1);
                Row("StellarFragment 5/5", true, 0);
                Attempt(true, 1, gold, 200, stellar, 5);
                Call(inventory, "SetGold", 200); Add(stellar, 7);
                Row("StellarFragment 7/7 + BossHeart 0/1", false, 1);
                Attempt(false, 1);
                Add(heart, 1);
                Row("StellarFragment 7/7 + BossHeart 1/1", true, 1);
                Attempt(true, 2, gold, 200, stellar, 7, heart, 1);
            }
        }
        finally
        {
            for (int i = 0; i < singletons.Length; i++) singletons[i].SetValue(null, previous[i]);
        }
    }
    private Component NewComponent(string name)
    {
        var root = new GameObject("Cauldron test " + name); created.Add(root);
        return root.AddComponent(Production(name));
    }
    private static void SetField(object target, string name, object value) =>
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

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
    private object Reward(int level, string plantName, string spellName, int plantLimitBonus = 0, int plantLimit = 0)
    {
        var reward = Activator.CreateInstance(Production("LevelUnlockReward"), level);
        const BindingFlags any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        if (plantLimitBonus > 0) reward.GetType().GetMethod("AddPlantLimit", any).Invoke(reward, new object[] { plantLimitBonus, plantLimit });
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
    [Test] public void RewardCardShowsLevelSeedAndSpellAndContinueHidesItOnce()
    {
        var view = Clone("LevelUpReward", "LevelUpRewardView");
        Assert.That(Property(view, "IsConfigured"), Is.True);
        int continued = 0;
        Listen(view, "Continued", (Action)(() => continued++));
        Assert.That(Call(view, "Show", new object[] { null }), Is.False);
        Assert.That(Call(view, "Show", Reward(3, "Sunflower", "Fireball")), Is.True);
        Assert.That(Property(view, "IsVisible"), Is.True);
        Assert.That((string)Property(Field(view, "headingLabel"), "text"), Does.Contain("3"));
        Assert.That((string)Property(Field(view, "seedLabel"), "text"), Does.Contain("Sunflower"));
        Assert.That((string)Property(Field(view, "spellLabel"), "text"), Does.Contain("Fireball"));
        Click(Field(view, "continueButton")); Click(Field(view, "continueButton"));
        Assert.That(continued, Is.EqualTo(1));
        Assert.That(Property(view, "IsVisible"), Is.False);
        Assert.That(Call(view, "Show", Reward(4, null, "Ice")), Is.True);
        Assert.That(((GameObject)Field(view, "seedSlot")).activeSelf, Is.False);
        Assert.That(((GameObject)Field(view, "spellSlot")).activeSelf, Is.True);
        Assert.That(((GameObject)Field(view, "plantLimitSlot")).activeSelf, Is.False);
    }
    [Test] public void RewardCardShowsThePlantLimitIncreaseAndItsNewTotal()
    {
        var view = Clone("LevelUpReward", "LevelUpRewardView");
        Assert.That(Property(view, "IsConfigured"), Is.True);
        Assert.That(Call(view, "Show", Reward(2, null, null, 1, 6)), Is.True);
        Assert.That(Property(view, "IsVisible"), Is.True);
        Assert.That((string)Property(Field(view, "headingLabel"), "text"), Does.Contain("2"));
        Assert.That(((GameObject)Field(view, "seedSlot")).activeSelf, Is.False);
        Assert.That(((GameObject)Field(view, "spellSlot")).activeSelf, Is.False);
        Assert.That(((GameObject)Field(view, "plantLimitSlot")).activeSelf, Is.True);
        Assert.That((string)Property(Field(view, "plantLimitLabel"), "text"), Is.EqualTo("Plant limit: +1 (6)"));
        Call(view, "HideImmediate");
        Assert.That(Call(view, "Show", Reward(3, "Sunflower", null, 1, 0)), Is.True);
        Assert.That((string)Property(Field(view, "plantLimitLabel"), "text"), Is.EqualTo("Plant limit: +1"), "no total without a plant quantity system");
        Assert.That(((GameObject)Field(view, "seedSlot")).activeSelf, Is.True);
    }
    [Test] public void RewardHideWhileInactiveIsImmediateAndSafeAndHideAfterShowIsImmediateOutsidePlayMode()
    {
        var view = Clone("LevelUpReward", "LevelUpRewardView");
        view.gameObject.SetActive(false);
        Assert.DoesNotThrow(() => Call(view, "Hide"));
        Assert.That(Property(view, "IsVisible"), Is.False); Assert.That(Property(view, "IsAnimating"), Is.False);
        Assert.That(Call(view, "Show", Reward(3, "Sunflower", null)), Is.True);
        Assert.That(Property(view, "IsAnimating"), Is.False, "EditMode snaps instead of tweening");
        Assert.That(((CanvasGroup)Field(view, "group")).alpha, Is.EqualTo(1f));
        Assert.That(((RectTransform)Field(view, "content")).localScale, Is.EqualTo(Vector3.one));
        Call(view, "Hide");
        Assert.That(Property(view, "IsVisible"), Is.False);
    }
    [Test] public void RewardCardRowAnimationsAreDesignerEditableWithReadablePauses()
    {
        var view = Clone("LevelUpReward", "LevelUpRewardView");
        var serialized = new SerializedObject(view);
        var defaults = new (string name, float value)[]
        {
            ("rowReadingGap", .35f), ("finalHoldDuration", .5f), ("rowDuration", .25f), ("rowStartScale", .6f), ("iconBounceScale", 1.25f), ("iconBounceDuration", .3f),
            ("plantLimitCountDuration", .5f), ("plantLimitPunchScale", 1.3f), ("plantLimitPunchDuration", .25f),
        };
        foreach (var (name, value) in defaults)
        {
            var property = serialized.FindProperty(name);
            Assert.That(property, Is.Not.Null, name);
            Assert.That(property.floatValue, Is.EqualTo(value).Within(1e-4f), name);
            var field = view.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field.GetCustomAttributes(typeof(SerializeField), false), Is.Not.Empty, name + " is serialized");
            Assert.That(field.GetCustomAttributes(typeof(TooltipAttribute), false), Is.Not.Empty, name + " has a tooltip");
        }
        Assert.That(serialized.FindProperty("tween.openDuration").floatValue, Is.EqualTo(.3f));
        Assert.That(serialized.FindProperty("tween.closeDuration").floatValue, Is.EqualTo(.25f));
        Assert.That(serialized.FindProperty("rowEase"), Is.Not.Null);
        Assert.That(Field(view, "rowEase").ToString(), Is.EqualTo("OutBack"));
        Assert.That(view.GetType().GetField("rowEase", BindingFlags.Instance | BindingFlags.NonPublic).GetCustomAttributes(typeof(TooltipAttribute), false), Is.Not.Empty);
    }
    [Test] public void RewardCardRowsSnapToTheirFinalStateOutsidePlayMode()
    {
        var view = Clone("LevelUpReward", "LevelUpRewardView");
        var slots = new[] { (GameObject)Field(view, "seedSlot"), (GameObject)Field(view, "spellSlot"), (GameObject)Field(view, "plantLimitSlot") };
        var icons = new[] { ((Component)Field(view, "seedIcon")).transform, ((Component)Field(view, "spellIcon")).transform };
        var plantLimit = (Component)Field(view, "plantLimitLabel");
        var positions = Array.ConvertAll(slots, slot => ((RectTransform)slot.transform).anchoredPosition);
        foreach (var slot in slots) slot.transform.localScale = Vector3.one * .3f; // left over from a previous animation
        foreach (var icon in icons) icon.localScale = Vector3.one * 2f;
        plantLimit.transform.localScale = Vector3.one * 1.5f;
        Assert.That(Call(view, "Show", Reward(5, "Sunflower", "Ice", 1, 9)), Is.True);
        Assert.That(Property(view, "IsAnimating"), Is.False, "EditMode snaps instead of animating the rows");
        foreach (var slot in slots)
        {
            Assert.That(slot.activeSelf, Is.True, slot.name);
            Assert.That(slot.transform.localScale, Is.EqualTo(Vector3.one), slot.name);
            if (slot.TryGetComponent(out CanvasGroup rowGroup)) Assert.That(rowGroup.alpha, Is.EqualTo(1f), slot.name);
        }
        for (int i = 0; i < slots.Length; i++)
            Assert.That(((RectTransform)slots[i].transform).anchoredPosition, Is.EqualTo(positions[i]), "row layout stays authored");
        Assert.That(Property(Field(view, "continueButton"), "interactable"), Is.True);
        foreach (var icon in icons) Assert.That(icon.localScale, Is.EqualTo(Vector3.one));
        Assert.That(plantLimit.transform.localScale, Is.EqualTo(Vector3.one));
        Assert.That((string)Property(plantLimit, "text"), Is.EqualTo("Plant limit: +1 (9)"), "the final value, not the previous limit");
        Assert.That((string)Property(Field(view, "seedLabel"), "text"), Is.EqualTo("Seed: Sunflower"));
        Assert.That((string)Property(Field(view, "spellLabel"), "text"), Is.EqualTo("Spell: Ice"));
        Assert.That(Call(view, "Show", Reward(6, null, null, 1, 0)), Is.True, "re-Show with an unknown plant limit");
        Assert.That(Property(view, "IsAnimating"), Is.False);
        Assert.That((string)Property(plantLimit, "text"), Is.EqualTo("Plant limit: +1"));
        Assert.That(plantLimit.transform.localScale, Is.EqualTo(Vector3.one));
    }
    [Test] public void LevelUpPanelHideWhileInactiveIsImmediateAndNeverRaisesHidden()
    {
        var view = Clone("LevelUpPanel", "LevelUpPanelView");
        int hidden = 0; Listen(view, "Hidden", (Action)(() => hidden++));
        view.gameObject.SetActive(false); hidden = 0;
        Assert.DoesNotThrow(() => Call(view, "Hide"));
        Assert.That(Property(view, "IsVisible"), Is.False); Assert.That(hidden, Is.Zero);
        Assert.That(Call(view, "Show", DataList("LevelUpCardData", 3), 2), Is.True);
        Assert.That(Property(view, "IsAnimating"), Is.False); Assert.That(((CanvasGroup)Field(view, "group")).alpha, Is.EqualTo(1f));
        Call(view, "Hide");
        Assert.That(Property(view, "IsVisible"), Is.False); Assert.That(hidden, Is.Zero, "closing through Hide is not an external disable");
    }
    [Test] public void RewardCardTextsComeFromDesignerEditableFormats()
    {
        var view = Clone("LevelUpReward", "LevelUpRewardView");
        var serialized = new SerializedObject(view);
        Assert.That(serialized.FindProperty("headingFormat").stringValue, Is.EqualTo("LEVEL {0} — Rewards"));
        Assert.That(serialized.FindProperty("seedFormat").stringValue, Is.EqualTo("Seed: {0}"));
        Assert.That(serialized.FindProperty("spellFormat").stringValue, Is.EqualTo("Spell: {0}"));
        Assert.That(serialized.FindProperty("plantLimitFormat").stringValue, Is.EqualTo("Plant limit: +{0} ({1})"));
        Assert.That(serialized.FindProperty("plantLimitBonusOnlyFormat").stringValue, Is.EqualTo("Plant limit: +{0}"));
        serialized.FindProperty("headingFormat").stringValue = "Nivel {0}";
        serialized.FindProperty("seedFormat").stringValue = "Semilla: {0}";
        serialized.FindProperty("spellFormat").stringValue = "Hechizo: {0}";
        serialized.FindProperty("plantLimitFormat").stringValue = "Plantas +{0} = {1}";
        serialized.ApplyModifiedPropertiesWithoutUndo();
        Assert.That(Call(view, "Show", Reward(4, "Sunflower", "Ice", 1, 8)), Is.True);
        Assert.That((string)Property(Field(view, "headingLabel"), "text"), Is.EqualTo("Nivel 4"));
        Assert.That((string)Property(Field(view, "seedLabel"), "text"), Is.EqualTo("Semilla: Sunflower"));
        Assert.That((string)Property(Field(view, "spellLabel"), "text"), Is.EqualTo("Hechizo: Ice"));
        Assert.That((string)Property(Field(view, "plantLimitLabel"), "text"), Is.EqualTo("Plantas +1 = 8"));
    }
    [Test] public void RewardCardFallsBackToTheDefaultTextWhenAFormatIsInvalid()
    {
        var view = Clone("LevelUpReward", "LevelUpRewardView");
        var serialized = new SerializedObject(view);
        serialized.FindProperty("plantLimitFormat").stringValue = "Plants {2}";
        serialized.FindProperty("seedFormat").stringValue = "";
        serialized.ApplyModifiedPropertiesWithoutUndo();
        LogAssert.Expect(LogType.Warning, new Regex("Invalid text format \"Plants \\{2\\}\""));
        Assert.That(Call(view, "Show", Reward(2, "Sunflower", null, 1, 6)), Is.True);
        Assert.That((string)Property(Field(view, "plantLimitLabel"), "text"), Is.EqualTo("Plant limit: +1 (6)"));
        Assert.That((string)Property(Field(view, "seedLabel"), "text"), Is.EqualTo("Seed: Sunflower"), "an empty format uses the default");
    }
    [Test] public void RewardHasContentWhenItUnlocksASeedASpellOrRaisesThePlantLimit()
    {
        Assert.That(Property(Reward(3, null, null), "HasContent"), Is.False);
        Assert.That(Property(Reward(3, "Sunflower", null), "HasContent"), Is.True);
        Assert.That(Property(Reward(4, null, "Ice"), "HasContent"), Is.True);
        var plantLimit = Reward(5, null, null, 1, 9);
        Assert.That(Property(plantLimit, "HasContent"), Is.True);
        Assert.That(Property(plantLimit, "PlantLimitBonus"), Is.EqualTo(1));
        Assert.That(Property(plantLimit, "PlantLimit"), Is.EqualTo(9));
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
    [TestCase("Assets/Prefabs/UI/Canvas/Canvas Game UI.prefab")]
    [TestCase("Assets/Prefabs/UI/Game UI.prefab")]
    public void InventoryUsesSharedBackdropInsteadOfDepthOfFieldBlur(string path)
    {
        var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        var controller = root.GetComponentInChildren(Production("GameStateUIController"), true);
        Assert.That(controller, Is.Not.Null);
        Assert.That(controller.GetType().GetField("blurVolume", BindingFlags.Instance | BindingFlags.NonPublic), Is.Null);
        var backdrop = (Component)Field(controller, "inventoryBackdrop");
        Assert.That(backdrop, Is.Not.Null);
        Assert.That(backdrop.GetType().Name, Is.EqualTo("UIBackdrop"));
        Assert.That(backdrop.transform.IsChildOf(root.transform), Is.True);
    }
    private const string HudPath = "Assets/Prefabs/UI/Canvas/Game/Panel HUD.prefab";
    private static readonly string[] ExperienceBarFields =
        { "fillGraphic", "punchTarget", "fillDuration", "fillEase", "punchStrength", "punchDuration", "punchMinInterval", "fullFlashColor", "fullFlashDuration", "fullHoldDuration" };
    private Component Hud()
    {
        var root = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(HudPath));
        created.Add(root);
        return root.GetComponentInChildren(Production("ExperienceUIController"), true);
    }
    [Test] public void ExperienceBarFeelIsDesignerEditableWithSafeDefaultsAndTheFillIsWired()
    {
        var bar = Hud(); Assert.That(bar, Is.Not.Null);
        foreach (var name in ExperienceBarFields)
        {
            var field = bar.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            Assert.That(field.GetCustomAttribute<SerializeField>(), Is.Not.Null, name + " is serialized");
            Assert.That(field.GetCustomAttribute<TooltipAttribute>(), Is.Not.Null, name + " has a tooltip");
        }
        Assert.That((float)Field(bar, "fillDuration"), Is.EqualTo(.5f));
        Assert.That((float)Field(bar, "fullHoldDuration"), Is.EqualTo(.3f), "the full bar has a reading hold before resetting");
        Assert.That((float)Field(bar, "punchStrength"), Is.GreaterThan(0f));
        Assert.That((float)Field(bar, "punchDuration"), Is.GreaterThan(0f));
        Assert.That((float)Field(bar, "punchMinInterval"), Is.GreaterThan(0f), "the punch is throttled by default");
        Assert.That((float)Field(bar, "fullFlashDuration"), Is.GreaterThan(0f));
        var slider = (Component)Field(bar, "xpSlider"); var fill = (Component)Field(bar, "fillGraphic");
        Assert.That(fill, Is.Not.Null, "Panel HUD wires the fill graphic");
        Assert.That(fill.gameObject, Is.SameAs(((Component)Property(slider, "fillRect")).gameObject), "it is the slider's own Fill");
    }
    // HandleExperienceChanged is the private OnExperienceChanged handler; Call only finds public methods.
    private static void HandleExperience(Component bar, int level, int currentXp, int requiredXp) =>
        bar.GetType().GetMethod("HandleExperienceChanged", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(bar, new object[] { level, currentXp, requiredXp });
    [Test] public void ExperienceBarSnapsToExactValuesOutsidePlayMode()
    {
        var bar = Hud(); var slider = (Component)Field(bar, "xpSlider");
        var levelText = (Component)Field(bar, "levelText"); var xpText = (Component)Field(bar, "xpText");
        var scale = slider.transform.localScale;
        HandleExperience(bar, 2, 150, 250);
        Assert.That(Property(levelText, "text"), Is.EqualTo("LV. 2")); Assert.That(Property(xpText, "text"), Is.EqualTo("150 / 250 XP"));
        Assert.That(Property(slider, "minValue"), Is.EqualTo(0f)); Assert.That(Property(slider, "maxValue"), Is.EqualTo(250f));
        Assert.That(Property(slider, "value"), Is.EqualTo(150f));
        HandleExperience(bar, 3, 400, 0);
        Assert.That(Property(levelText, "text"), Is.EqualTo("LV. 3")); Assert.That(Property(xpText, "text"), Is.EqualTo("400 XP (MAX)"));
        Assert.That(Property(slider, "maxValue"), Is.EqualTo(1f)); Assert.That(Property(slider, "value"), Is.EqualTo(1f));
        HandleExperience(bar, 1, 0, 100);
        Assert.That(Property(slider, "maxValue"), Is.EqualTo(100f)); Assert.That(Property(slider, "value"), Is.EqualTo(0f), "a reset snaps back");
        Assert.That(slider.transform.localScale, Is.EqualTo(scale), "no punch outside Play Mode");
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
