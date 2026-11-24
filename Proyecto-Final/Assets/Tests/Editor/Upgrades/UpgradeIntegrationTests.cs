using System;
using System.Collections.Generic;
using System.Reflection;
using MagicGarden;
using NUnit.Framework;
using UnityEngine;

// Predefined Assembly-CSharp cannot be referenced by an asmdef. Reflection keeps
// the production serialization/plugin boundary intact while testing its actual APIs.
public class UpgradeIntegrationTests
{
    private readonly List<UnityEngine.Object> created = new List<UnityEngine.Object>();
    private static Type Production(string name)
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            if (assembly.GetName().Name == "Assembly-CSharp") return assembly.GetType(name, true);
        throw new InvalidOperationException("Assembly-CSharp is not loaded");
    }
    private Component Component(string type)
    {
        var go = new GameObject("Upgrade integration test"); created.Add(go);
        return go.AddComponent(Production(type));
    }
    private static object Call(object instance, string name, params object[] args)
        => instance.GetType().GetMethod(name, BindingFlags.Public | BindingFlags.Instance).Invoke(instance, args);
    private static object Material(int id) => Enum.ToObject(Production("MaterialType"), id);
    [TearDown] public void Cleanup()
    {
        foreach (var item in created) if (item != null) UnityEngine.Object.DestroyImmediate(item);
        created.Clear();
    }
    [TestCase("pickupDisplayTime")]
    [TestCase("fadeDuration")]
    [TestCase("warningColor")]
    public void PickupSharedFeelControlsHaveTooltips(string name)
    {
        var field = Production("FloatingTextController").GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);
        Assert.That(field.IsDefined(typeof(SerializeField), false), Is.True);
        Assert.That(field.IsDefined(typeof(TooltipAttribute), false), Is.True);
    }
    [Test] public void IndicatorSoundHasADesignerEditableSelectorWithTheExistingDefault()
    {
        var field = Production("LevelUpIndicator").GetField("levelUpSound", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, "The indicator sound needs a serialized selector");
        Assert.That(field.IsDefined(typeof(SerializeField), false), Is.True);
        Assert.That(field.IsDefined(typeof(TooltipAttribute), false), Is.True);
        Assert.That(field.GetValue(Component("LevelUpIndicator")), Is.EqualTo("Level_Up"));
    }
    // Night gold feedback: designers edit messages, colors and the gold line format in the Inspector.
    private static object Get(object instance, string field)
        => instance.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(instance);
    private static void Set(object instance, string field, object value)
        => instance.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(instance, value);
    [Test] public void NightRewardMessagesAndColorsAreEditableAndDefaultToTheCurrentFeedback()
    {
        var rewards = Component("RewardsSystem");
        Assert.That(Get(rewards, "rewardNoPlantsDestroyed"), Is.EqualTo(30));
        Assert.That(Get(rewards, "rewardHomeUntouched"), Is.EqualTo(50));
        Assert.That(Get(rewards, "rewardPlayerUnharmed"), Is.EqualTo(20));
        Assert.That(Get(rewards, "defensesIntactMessage"), Is.EqualTo("Defenses Intact"));
        Assert.That(Get(rewards, "homeProtectedMessage"), Is.EqualTo("Home Protected"));
        Assert.That(Get(rewards, "witchUnharmedMessage"), Is.EqualTo("Witch Unharmed"));
        Assert.That(Get(rewards, "defensesIntactColor"), Is.EqualTo(new Color(0.3f, 0.9f, 0.4f)));
        Assert.That(Get(rewards, "homeProtectedColor"), Is.EqualTo(new Color(1f, 0.8f, 0.2f)));
        Assert.That(Get(rewards, "witchUnharmedColor"), Is.EqualTo(new Color(0.7f, 0.4f, 1f)));
        var floating = Component("FloatingTextController");
        Assert.That(Get(floating, "noRewardMessage"), Is.EqualTo("The shadows watch in silence..."));
        Assert.That(Get(floating, "noRewardColor"), Is.EqualTo(new Color(0.6f, 0.2f, 0.2f)));
    }
    [Test] public void GoldFeedbackLineUsesItsEditableFormatAndFallsBackWhenInvalid()
    {
        var floating = Component("FloatingTextController");
        Assert.That(Call(floating, "FormatGoldEntry", "Defenses Intact", 30), Is.EqualTo("<sprite name=\"GoldIcon\"> Defenses Intact\n+30 gold"));
        Assert.That(Call(floating, "FormatGoldEntry", "The shadows watch in silence...", 0), Is.EqualTo("The shadows watch in silence..."), "no gold: message only");
        Set(floating, "goldEntryFormat", "{0}: {1} oro");
        Assert.That(Call(floating, "FormatGoldEntry", "Casa protegida", 50), Is.EqualTo("Casa protegida: 50 oro"));
        Set(floating, "goldEntryFormat", "{0} {3}");
        UnityEngine.TestTools.LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Invalid text format"));
        Assert.That(Call(floating, "FormatGoldEntry", "Home Protected", 50), Is.EqualTo("<sprite name=\"GoldIcon\"> Home Protected\n+50 gold"));
    }
    // Night gold feedback game feel: every timing, scale and sound is an Inspector field.
    [Test] public void NightGoldFeedbackAnimationIsEditableAndDefaultsToTheDesignedFeel()
    {
        var floating = Component("FloatingTextController");
        Assert.That(Get(floating, "goldLineStagger"), Is.EqualTo(0.3f));
        Assert.That(Get(floating, "goldFeedbackDisplayTime"), Is.EqualTo(2f));
        Assert.That(Get(floating, "goldFadeDuration"), Is.EqualTo(0.35f));
        Assert.That(Get(floating, "goldLineEnterDuration"), Is.EqualTo(0.3f));
        Assert.That(Get(floating, "goldLineStartScale"), Is.EqualTo(0.4f));
        Assert.That(Get(floating, "goldCountDuration"), Is.EqualTo(0.6f));
        Assert.That(Get(floating, "goldTickSound"), Is.EqualTo("PickUp"));
        Assert.That(Get(floating, "goldTickInterval"), Is.EqualTo(0.06f));
        Assert.That(Get(floating, "goldLineEnterEase").ToString(), Is.EqualTo("OutBack"));
    }
    [Test] public void NightGoldLinesAreStaggeredAndTheCounterGoesFromZeroToTheAmount()
    {
        var floating = Component("FloatingTextController");
        Assert.That(Call(floating, "GoldLineDelay", 0), Is.EqualTo(0f));
        Assert.That((float)Call(floating, "GoldLineDelay", 2), Is.EqualTo(2.4f).Within(1e-5f));
        Set(floating, "goldLineStagger", 0.5f);
        Assert.That((float)Call(floating, "GoldLineDelay", 3), Is.EqualTo(4.2f).Within(1e-5f));
        Assert.That(Call(floating, "CountedGold", 30, 0f), Is.EqualTo(0));
        Assert.That(Call(floating, "CountedGold", 30, 0.5f), Is.EqualTo(15));
        Assert.That(Call(floating, "CountedGold", 30, 1f), Is.EqualTo(30));
        Assert.That(Call(floating, "CountedGold", 30, 2f), Is.EqualTo(30), "progress is clamped");
        Assert.That(Call(floating, "FormatGoldCount", "Home Protected", 0), Is.EqualTo("<sprite name=\"GoldIcon\"> Home Protected\n+0 gold"));
        Assert.That(Call(floating, "FormatGoldCount", "Home Protected", 50), Is.EqualTo(Call(floating, "FormatGoldEntry", "Home Protected", 50)));
    }
    // Material pickup game feel: every timing, scale, color and sound is an Inspector field with a tooltip.
    private static readonly string[] PickupFeelFields =
    {
        "pickupPopStartScale", "pickupPopDuration", "pickupPopEase", "pickupPunchScale", "pickupPunchDuration",
        "pickupPunchVibrato", "pickupPunchElasticity", "pickupCountDuration", "pickupHighlightColor",
        "pickupFlashDuration", "pickupSound", "pickupSoundInterval",
    };
    [Test] public void MaterialPickupFeelIsEditableAndDefaultsToTheDesignedFeel()
    {
        var floating = Component("FloatingTextController");
        Assert.That(Get(floating, "pickupPopStartScale"), Is.EqualTo(0.5f));
        Assert.That(Get(floating, "pickupPopDuration"), Is.EqualTo(0.3f));
        Assert.That(Get(floating, "pickupDisplayTime"), Is.EqualTo(1.5f));
        Assert.That(Get(floating, "fadeDuration"), Is.EqualTo(0.35f));
        Assert.That(Get(floating, "pickupPopEase").ToString(), Is.EqualTo("OutBack"));
        Assert.That(Get(floating, "pickupPunchScale"), Is.EqualTo(0.25f));
        Assert.That(Get(floating, "pickupPunchDuration"), Is.EqualTo(0.25f));
        Assert.That(Get(floating, "pickupPunchVibrato"), Is.EqualTo(2));
        Assert.That(Get(floating, "pickupPunchElasticity"), Is.EqualTo(0.5f));
        Assert.That(Get(floating, "pickupCountDuration"), Is.EqualTo(0.5f));
        Assert.That(Get(floating, "pickupHighlightColor"), Is.EqualTo(new Color(1f, 0.92f, 0.45f)));
        Assert.That(Get(floating, "pickupFlashDuration"), Is.EqualTo(0.35f));
        Assert.That(Get(floating, "pickupSound"), Is.EqualTo("PickUp"));
        Assert.That(Get(floating, "pickupSoundInterval"), Is.EqualTo(0.08f));
    }
    [TestCase("goldFeedbackDisplayTime")]
    [TestCase("goldFadeDuration")]
    [TestCase("goldLineStagger")]
    [TestCase("goldLineEnterDuration")]
    [TestCase("goldCountDuration")]
    public void GoldPacingControlsAreSerializedWithTooltips(string name)
    {
        var field = Production("FloatingTextController").GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(field.IsDefined(typeof(SerializeField), false), Is.True);
        Assert.That(field.IsDefined(typeof(TooltipAttribute), false), Is.True);
    }
    [Test] public void MaterialPickupFeelFieldsAreSerializedWithTooltips()
    {
        var type = Production("FloatingTextController");
        foreach (var name in PickupFeelFields)
        {
            var field = type.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null, name);
            Assert.That(field.IsDefined(typeof(SerializeField), false), Is.True, name + " is serialized");
            Assert.That(field.IsDefined(typeof(TooltipAttribute), false), Is.True, name + " has a tooltip");
        }
    }
    [Test] public void MaterialPickupCounterGoesFromThePreviousTotalToTheNewOne()
    {
        var floating = Component("FloatingTextController");
        Assert.That(Call(floating, "CountedValue", 3, 8, 0f), Is.EqualTo(3));
        Assert.That(Call(floating, "CountedValue", 3, 8, 0.4f), Is.EqualTo(5));
        Assert.That(Call(floating, "CountedValue", 3, 8, 1f), Is.EqualTo(8));
        Assert.That(Call(floating, "CountedValue", 3, 8, -1f), Is.EqualTo(3), "progress is clamped");
        Assert.That(Call(floating, "CountedValue", 3, 8, 2f), Is.EqualTo(8), "progress is clamped");
        Assert.That(Call(floating, "CountedValue", 4, 4, 0.5f), Is.EqualTo(4));
    }
    [Test] public void MaterialPickupLineKeepsTheCurrentTextFormat()
    {
        var floating = Component("FloatingTextController");
        Assert.That(Call(floating, "FormatPickupLine", "<sprite name=\"Bone\"> ", 3, "Bone"), Is.EqualTo("<sprite name=\"Bone\"> +3 Bone"));
        Assert.That(Call(floating, "FormatPickupLine", "", 1, "Mana"), Is.EqualTo("+1 Mana"));
    }
    // "El mana no muestra el icono": the mana sprite (InventorySpriteSheet_6) is not in spriteMappings and
    // TMP_SpriteAsset.spriteInfoList is legacy/empty, so the icon is found through the sprite asset itself.
    private object SpriteAssetWith(params string[] names)
    {
        var assetType = Type.GetType("TMPro.TMP_SpriteAsset, Unity.TextMeshPro", true);
        var glyphType = Type.GetType("TMPro.TMP_SpriteGlyph, Unity.TextMeshPro", true);
        var characterType = Type.GetType("TMPro.TMP_SpriteCharacter, Unity.TextMeshPro", true);
        var asset = ScriptableObject.CreateInstance(assetType); created.Add(asset);
        var glyph = Activator.CreateInstance(glyphType);
        ((System.Collections.IList)Get(asset, "m_SpriteGlyphTable")).Add(glyph);
        var characters = (System.Collections.IList)Get(asset, "m_SpriteCharacterTable");
        foreach (var name in names)
        {
            var character = Activator.CreateInstance(characterType, new object[] { 0xFFFEu, glyph });
            characterType.GetProperty("name").SetValue(character, name);
            characters.Add(character);
        }
        return asset;
    }
    [Test] public void PickupIconResolvesSpritesThatAreInTheSpriteAssetEvenWithoutAMapping()
    {
        var floating = Component("FloatingTextController");
        Assert.That(Call(floating, "ResolveSpriteName", "InventorySpriteSheet_6"), Is.Null, "no sprite asset");
        Set(floating, "itemSpriteAsset", SpriteAssetWith("GoldIcon", "InventorySpriteSheet_6"));
        Assert.That(Call(floating, "ResolveSpriteName", "InventorySpriteSheet_6"), Is.EqualTo("InventorySpriteSheet_6"));
        Assert.That(Call(floating, "ResolveSpriteName", "GoldIcon"), Is.EqualTo("GoldIcon"));
        Assert.That(Call(floating, "ResolveSpriteName", "NotInTheAsset"), Is.Null);
        Assert.That(Call(floating, "ResolveSpriteName", ""), Is.Null);
    }
    [Test] public void PickupIconMappingStillWinsOverTheSpriteAssetName()
    {
        var floating = Component("FloatingTextController");
        Set(floating, "itemSpriteAsset", SpriteAssetWith("EternalEmber"));
        var mappingType = Production("FloatingTextController").GetNestedType("SpriteMapping");
        var mapping = Activator.CreateInstance(mappingType);
        mappingType.GetField("originalSpriteName").SetValue(mapping, "EternalEmberEternalEmber");
        mappingType.GetField("tmpSpriteName").SetValue(mapping, "EternalEmber");
        ((System.Collections.IList)Get(floating, "spriteMappings")).Add(mapping);
        Assert.That(Call(floating, "ResolveSpriteName", "EternalEmberEternalEmber"), Is.EqualTo("EternalEmber"));
    }
    [Test] public void InventoryInsufficientDuplicateCostsNeverPartiallySpend()
    {
        var inventory = Component("InventoryManager");
        Call(inventory, "AddMaterial", Material(1), 5);
        Call(inventory, "AddMaterial", Material(2), 10);
        var costs = new[] { new Cost { material = 1, amount = 3 }, new Cost { material = 1, amount = 3 }, new Cost { material = 2, amount = 1 } };
        Assert.That(Call(inventory, "TrySpendCosts", costs, null), Is.EqualTo(false));
        Assert.That(Call(inventory, "GetMaterialAmount", Material(1)), Is.EqualTo(5));
        Assert.That(Call(inventory, "GetMaterialAmount", Material(2)), Is.EqualTo(10));
    }
    [Test] public void InventoryCommitRejectionRollsBackBeforeNotifications()
    {
        var inventory = Component("InventoryManager"); Call(inventory, "AddMaterial", Material(1), 5);
        var costs = new[] { new Cost { material = 1, amount = 3 } };
        Assert.That(Call(inventory, "TrySpendCosts", costs, (Func<bool>)(() => false)), Is.EqualTo(false));
        Assert.That(Call(inventory, "GetMaterialAmount", Material(1)), Is.EqualTo(5));
    }
    [Test] public void InventorySuccessCommitsAllBalancesBeforeEventsAndRejectsReentrantConsumption()
    {
        var inventory = Component("InventoryManager");
        Call(inventory, "AddMaterial", Material(1), 7); Call(inventory, "AddMaterial", Material(2), 3);
        var costs = new[] { new Cost { material = 1, amount = 3 }, new Cost { material = 1, amount = 4 }, new Cost { material = 2, amount = 1 } };
        observed = inventory; notifications = 0;
        var field = inventory.GetType().GetField("onMaterialChanged");
        var handler = typeof(UpgradeIntegrationTests).GetMethod(nameof(ResourceChanged), BindingFlags.NonPublic | BindingFlags.Static)
            .MakeGenericMethod(Production("MaterialType"));
        field.SetValue(inventory, Delegate.CreateDelegate(field.FieldType, handler));
        Assert.That(Call(inventory, "TrySpendCosts", costs, (Func<bool>)(() => true)), Is.EqualTo(true));
        Assert.That(notifications, Is.EqualTo(2));
    }
    private static object observed;
    private static int notifications;
    private static void ResourceChanged<T>(T material, int amount)
    {
        notifications++;
        Assert.That(Call(observed, "GetMaterialAmount", Material(1)), Is.EqualTo(0));
        Assert.That(Call(observed, "GetMaterialAmount", Material(2)), Is.EqualTo(2));
        Assert.That(Call(observed, "UseMaterial", Material(2), 1), Is.EqualTo(false));
        Assert.That(Call(observed, "TrySpendCosts", new[] { new Cost { material = 2, amount = 1 } }, null), Is.EqualTo(false));
    }
    [Test] public void InventoryRejectsInvalidMaterialAndNonpositiveCosts()
    {
        var inventory = Component("InventoryManager");
        Assert.That(Call(inventory, "HasCosts", (object)new[] { new Cost { material = 999, amount = 1 } }), Is.EqualTo(false));
        Assert.That(Call(inventory, "HasCosts", (object)new[] { new Cost { material = 1, amount = 0 } }), Is.EqualTo(false));
    }
    [Test] public void GoldCostIsPaidFromGoldBalanceAndNotifiesOnceAfterCommit()
    {
        var inventory = Component("InventoryManager"); Call(inventory, "SetGold", 250);
        var gold = new[] { new Cost { material = GoldId, amount = 200 } };
        var goldEvents = new List<int>();
        inventory.GetType().GetField("onGoldChanged").SetValue(inventory, (Action<int>)(amount =>
        {
            goldEvents.Add(amount);
            Assert.That(Call(inventory, "TrySpendCosts", gold, null), Is.EqualTo(false), "Reentrant spend is rejected");
        }));
        var field = inventory.GetType().GetField("onMaterialChanged");
        field.SetValue(inventory, Delegate.CreateDelegate(field.FieldType, this, GetType()
            .GetMethod(nameof(CountMaterialEvent), BindingFlags.NonPublic | BindingFlags.Instance).MakeGenericMethod(Production("MaterialType"))));
        materialEventCount = 0;
        Assert.That(Call(inventory, "HasCosts", (object)gold), Is.EqualTo(true));
        Assert.That(Call(inventory, "TrySpendCosts", gold, (Func<bool>)(() => true)), Is.EqualTo(true));
        Assert.That(Call(inventory, "GetGold"), Is.EqualTo(50));
        Assert.That(Call(inventory, "GetResourceAmount", Material(GoldId)), Is.EqualTo(50));
        Assert.That(Call(inventory, "GetMaterialAmount", Material(GoldId)), Is.EqualTo(0), "Gold never enters the materials list");
        Assert.That(goldEvents, Is.EqualTo(new[] { 50 }));
        Assert.That(materialEventCount, Is.EqualTo(0));
    }
    private int materialEventCount;
    private void CountMaterialEvent<T>(T material, int amount) => materialEventCount++;
    [Test] public void GoldCostInsufficientOrRejectedNeverSpendsAnyResource()
    {
        var inventory = Component("InventoryManager"); Call(inventory, "SetGold", 199); Call(inventory, "AddMaterial", Material(1), 5);
        var gold = new[] { new Cost { material = GoldId, amount = 200 } };
        Assert.That(Call(inventory, "HasCosts", (object)gold), Is.EqualTo(false));
        Assert.That(Call(inventory, "TrySpendCosts", gold, null), Is.EqualTo(false));
        Assert.That(Call(inventory, "GetGold"), Is.EqualTo(199));
        Call(inventory, "SetGold", 200);
        Assert.That(Call(inventory, "TrySpendCosts", gold, (Func<bool>)(() => false)), Is.EqualTo(false));
        Assert.That(Call(inventory, "GetGold"), Is.EqualTo(200));
        var mixed = new[] { new Cost { material = GoldId, amount = 200 }, new Cost { material = 1, amount = 6 } };
        Assert.That(Call(inventory, "TrySpendCosts", mixed, null), Is.EqualTo(false));
        Assert.That(Call(inventory, "GetGold"), Is.EqualTo(200));
        Assert.That(Call(inventory, "GetMaterialAmount", Material(1)), Is.EqualTo(5));
    }
    private const int GoldId = 15;
    [Test] public void PlayerExperienceKeepsCumulativeTotalAndQueuesMultipleChoices()
    {
        var experience = Component("PlayerExperienceSystem");
        var progression = ScriptableObject.CreateInstance(Production("ExperienceProgressionDataSO")); created.Add(progression);
        experience.GetType().GetField("progressionData", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(experience, progression);
        Call(experience, "AddExperience", 450);
        Assert.That(experience.GetType().GetProperty("CurrentLevel").GetValue(experience), Is.EqualTo(4));
        Assert.That(experience.GetType().GetProperty("CurrentExperience").GetValue(experience), Is.EqualTo(450));
        Assert.That(experience.GetType().GetProperty("PendingChoices").GetValue(experience), Is.EqualTo(3));
        Call(experience, "ConsumeChoice");
        Assert.That(experience.GetType().GetProperty("PendingChoices").GetValue(experience), Is.EqualTo(2));
        Call(experience, "ResetProgression");
        Assert.That(experience.GetType().GetProperty("PendingChoices").GetValue(experience), Is.EqualTo(0));
    }
    [Test] public void EnemyDropRequiresDeathAndSpawnsExperienceOnlyOnce()
    {
        var life = Component("LifeController");
        var pickupType = Production("ExperiencePickup");
        var before = new HashSet<UnityEngine.Object>(UnityEngine.Object.FindObjectsOfType(pickupType));
        Call(life, "ConfigureExperienceDrop", 15, null);
        Call(life, "Drop");
        Assert.That(UnityEngine.Object.FindObjectsOfType(pickupType).Length, Is.EqualTo(before.Count));
        life.GetType().GetField("isDead", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(life, true);
        Call(life, "Drop"); Call(life, "Drop");
        int spawned = 0;
        foreach (Component pickup in UnityEngine.Object.FindObjectsOfType(pickupType))
            if (!before.Contains(pickup)) { created.Add(pickup.gameObject); spawned++; }
        Assert.That(spawned, Is.EqualTo(1));
    }
    [Test] public void UnlockSeedDeliveryPreservesExistingCountsAndDefersWhenFull()
    {
        var seeds = Component("SeedInventory");
        var prefab = new GameObject("Seed test prefab"); created.Add(prefab);
        var data = ScriptableObject.CreateInstance(Production("PlantDataSO")); created.Add(data);
        data.GetType().GetField("plantPrefab").SetValue(data, prefab);
        for (int i = 1; i <= 9; i++)
        {
            data.GetType().GetField("seedType").SetValue(data, Enum.ToObject(Production("SeedsEnum"), i));
            Assert.That(Call(seeds, "TryGrantUnlockedPlant", data, 1), Is.EqualTo(true));
        }
        data.GetType().GetField("seedType").SetValue(data, Enum.ToObject(Production("SeedsEnum"), 10));
        Assert.That(Call(seeds, "TryGrantUnlockedPlant", data, 1), Is.EqualTo(false));
        data.GetType().GetField("seedType").SetValue(data, Enum.ToObject(Production("SeedsEnum"), 1));
        Assert.That(Call(seeds, "TryGrantUnlockedPlant", data, 5), Is.EqualTo(true));
        Assert.That(Call(seeds, "GetSeedCountInSlot", 0), Is.EqualTo(1));
    }
    [Test] public void DefaultBalanceAssetContainsActualEffectsAndAllRarities()
    {
        var asset = Resources.Load<TextAsset>("MagicGardenBalance");
        Assert.That(asset, Is.Not.Null);
        var balance = JsonUtility.FromJson<Balance>(asset.text);
        Assert.That(balance.rarities.Length, Is.EqualTo(4));
        var found = new HashSet<EffectKind>();
        foreach (var special in balance.specials)
        {
            Assert.That(CostPlanner.Aggregate(special.costs, out _), Is.True);
            foreach (var effect in special.effects) found.Add(effect.kind);
        }
        Assert.That(found.Count, Is.EqualTo(5));
        Assert.That(balance.stats[(int)Stat.Quantity].weight, Is.LessThan(0.05f));
    }
    [Test] public void DefaultBalanceSpecialsCostTwoNormalNightsOfGoldPlusTheirMaterials()
    {
        // A normal night pays at most 30 + 50 + 20 = 100 gold (RewardsSystem defaults); materials keep their original amounts.
        var expected = new Dictionary<string, string>
        {
            ["basic-lateral"] = "15x200 1x3 2x2",
            ["pierce-extension"] = "15x200 1x4 3x2",
            ["wide-explosion"] = "15x200 4x3 1x2",
            ["branching-volley"] = "15x200 2x3 3x2",
            ["branching-volley-master"] = "15x200 3x5",
        };
        var balance = JsonUtility.FromJson<Balance>(Resources.Load<TextAsset>("MagicGardenBalance").text);
        Assert.That(balance.specials.Length, Is.EqualTo(expected.Count));
        foreach (var special in balance.specials)
        {
            var costs = new List<string>();
            foreach (var cost in special.costs) costs.Add(cost.material + "x" + cost.amount);
            Assert.That(string.Join(" ", costs), Is.EqualTo(expected[special.id]), special.id);
        }
    }
    [Test] public void CauldronCostLabelUsesIconsAndKeepsNamesForMaterialsWithoutIcon()
    {
        var label = Production("UpgradePanel").GetMethod("CostLabel", BindingFlags.NonPublic | BindingFlags.Static);
        string Label(int material, string name, int owned, int amount) => (string)label.Invoke(null, new[] { Material(material), name, owned, amount });
        Assert.That(Label(GoldId, "Gold", 120, 200), Is.EqualTo("<sprite name=\"GoldIcon\"> 120/200"));
        Assert.That(Label(1, "Spectral Crystal", 2, 3), Is.EqualTo("<sprite name=\"SpectralCrystal\"> 2/3"));
        Assert.That(Label(2, "Windwalker Essence", 0, 2), Is.EqualTo("<sprite name=\"WindwalkerEssence\"> 0/2"));
        Assert.That(Label(3, "Voltaic Core", 5, 5), Is.EqualTo("<sprite name=\"VoltaicCore\"> 5/5"));
        Assert.That(Label(4, "Eternal Ember", 1, 3), Is.EqualTo("<sprite name=\"EternalEmber\"> 1/3"));
        Assert.That(Label(5, "Stellar Fragment", 1, 2), Is.EqualTo("Stellar Fragment 1/2"), "No icon in UtilitySprites: keep the name");
        Assert.That(Production("UpgradePanel").GetField("CostSeparator", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null), Is.EqualTo(" + "));
    }
    [Test] public void CostIconsAreCenteredOnTheDigitsWithoutHorizontalOffset()
    {
        // TMP 3.0 scales a sprite (sprite asset without face info) to the font ascent. Its vertical center must match the
        // m5x7 digits' center (half the cap line) so it does not look raised; bearingX == 0 keeps it from overlapping the
        // previous character or leaving a gap after it.
        var font = UnityEditor.AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/Plugins/TextMesh Pro/Fonts/m5x7.asset");
        var face = font.GetType().GetProperty("faceInfo").GetValue(font);
        float ascent = (float)face.GetType().GetProperty("ascentLine").GetValue(face);
        float cap = (float)face.GetType().GetProperty("capLine").GetValue(face);
        var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/Sprites/SpriteSheets/UtilitySprites.asset");
        Assert.That(asset, Is.Not.Null);
        var glyphs = new Dictionary<uint, object>();
        foreach (var glyph in (System.Collections.IEnumerable)asset.GetType().GetProperty("spriteGlyphTable").GetValue(asset))
            glyphs[(uint)glyph.GetType().GetProperty("index").GetValue(glyph)] = glyph;
        var names = new HashSet<string> { "GoldIcon", "SpectralCrystal", "WindwalkerEssence", "VoltaicCore", "EternalEmber" };
        int found = 0;
        foreach (var character in (System.Collections.IEnumerable)asset.GetType().GetProperty("spriteCharacterTable").GetValue(asset))
        {
            string name = (string)character.GetType().GetProperty("name").GetValue(character);
            if (!names.Contains(name)) continue;
            var glyph = glyphs[(uint)character.GetType().GetProperty("glyphIndex").GetValue(character)];
            var metrics = glyph.GetType().GetProperty("metrics").GetValue(glyph);
            float Metric(string field) => (float)metrics.GetType().GetProperty(field).GetValue(metrics);
            Assert.That(Metric("horizontalBearingX"), Is.EqualTo(0f), name);
            float center = (Metric("horizontalBearingY") - Metric("height") / 2f) * ascent / Metric("height");
            Assert.That(center, Is.EqualTo(cap / 2f).Within(0.1f), name);
            Assert.That(Metric("horizontalAdvance"), Is.EqualTo(Metric("width")), name);
            found++;
        }
        Assert.That(found, Is.EqualTo(names.Count));
    }
    private static StatRule RuleFor(Balance balance, Stat stat)
    {
        foreach (var rule in balance.stats) if (rule.stat == stat) return rule;
        return null;
    }
    // The plant limit grows with the player level, so the default balance no longer offers the "Garden bloom" upgrade.
    [Test] public void DefaultBalanceHasNoPlantLimitUpgradeAndNoFixedRarity()
    {
        var balance = JsonUtility.FromJson<Balance>(Resources.Load<TextAsset>("MagicGardenBalance").text);
        Assert.That(RuleFor(balance, Stat.PlantLimit), Is.Null);
        foreach (var rule in balance.stats) Assert.That(rule.displayName, Is.Not.EqualTo("Garden bloom"));
        // JsonUtility leaves absent fields at default(T): every rule must still roll its rarity.
        foreach (var rule in balance.stats) Assert.That(rule.forceRarity, Is.False, rule.stat.ToString());
        var quantity = RuleFor(balance, Stat.Quantity);
        Assert.That(quantity.displayName, Is.EqualTo("Abundant bloom"));
        Assert.That(quantity.weight, Is.EqualTo(0.025f).Within(0.0001f));
        Assert.That(quantity.maxStacks, Is.EqualTo(3));
        for (int i = 0; i < balance.stats.Length; i++) Assert.That((int)balance.stats[i].stat, Is.EqualTo(i));
    }
    [Test] public void DefaultBalanceNeverOffersPlantLimitEvenToAPlayerWithAPlantLimitBase()
    {
        var balance = JsonUtility.FromJson<Balance>(Resources.Load<TextAsset>("MagicGardenBalance").text);
        var player = new Target { Id = "player", Name = "Player", Capability = "Player" };
        player.Bases[Stat.MaxHealth] = 100; player.Bases[Stat.MoveSpeed] = 5; player.Bases[Stat.PlantLimit] = 5;
        foreach (var profile in balance.profiles) if (profile.capability == "Player") player.Profile = profile;
        var spell = new Target { Id = "spell:0", Name = "Spell", Capability = "BasicRangeSpell" };
        spell.Bases[Stat.Damage] = 10; spell.Bases[Stat.Quantity] = 1;
        foreach (var profile in balance.profiles) if (profile.capability == spell.Capability) spell.Profile = profile;
        var state = new RunState(); var random = new System.Random(31); int plantLimitOffers = 0;
        var rarities = new HashSet<Rarity>();
        for (int i = 0; i < 400; i++)
            foreach (var offer in OfferSelector.Select(state, new[] { player, spell }, balance, random, 3))
            {
                if (offer.Rule.stat == Stat.PlantLimit) plantLimitOffers++;
                else rarities.Add(offer.Rarity.rarity);
            }
        Assert.That(plantLimitOffers, Is.Zero);
        Assert.That(rarities.Count, Is.EqualTo(4));
    }
    [Test] public void PlantQuantityLimitIsStartingValuePlusOnePerLevelWithoutCapAndWithoutMutatingTheSerializedValue()
    {
        var system = Component("PlantQuantitySystem");
        var starting = system.GetType().GetField("startingQuantity", BindingFlags.NonPublic | BindingFlags.Instance);
        int start = (int)starting.GetValue(system);
        Assert.That(start, Is.EqualTo(5));
        Assert.That(system.GetType().GetField("QuantityPerLevel").GetValue(null), Is.EqualTo(1));
        Assert.That(Call(system, "get_BaseQuantity"), Is.EqualTo(start));
        Assert.That(Call(system, "get_CurrentQuantity"), Is.EqualTo(start)); // no player yet: level 1
        Assert.That(Call(system, "LimitAtLevel", 1), Is.EqualTo(5));
        Assert.That(Call(system, "LimitAtLevel", 2), Is.EqualTo(6));
        Assert.That(Call(system, "LimitAtLevel", 3), Is.EqualTo(7));
        Assert.That(Call(system, "LimitAtLevel", 100), Is.EqualTo(104), "no cap");
        Assert.That(Call(system, "LimitAtLevel", 0), Is.EqualTo(5), "levels below 1 count as level 1");
        var experience = Component("PlayerExperienceSystem");
        var level = experience.GetType().GetField("currentLevel", BindingFlags.NonPublic | BindingFlags.Instance);
        level.SetValue(experience, 4);
        Assert.That(Call(system, "get_CurrentQuantity"), Is.EqualTo(8));
        level.SetValue(experience, 1); // what PlayerExperienceSystem.ResetProgression does
        Assert.That(Call(system, "get_CurrentQuantity"), Is.EqualTo(start));
        Assert.That(starting.GetValue(system), Is.EqualTo(start));
        Assert.That(Call(system, "get_BaseQuantity"), Is.EqualTo(start));
    }
}
