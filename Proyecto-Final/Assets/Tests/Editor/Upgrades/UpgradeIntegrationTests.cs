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
    [Test] public void DefaultBalanceSpecialsCostOnlyTwoNormalNightsOfGold()
    {
        // A normal night pays at most 30 + 50 + 20 = 100 gold (RewardsSystem defaults).
        var balance = JsonUtility.FromJson<Balance>(Resources.Load<TextAsset>("MagicGardenBalance").text);
        Assert.That(balance.specials.Length, Is.EqualTo(5));
        foreach (var special in balance.specials)
        {
            Assert.That(special.costs.Length, Is.EqualTo(1), special.id);
            Assert.That(special.costs[0].material, Is.EqualTo(GoldId), special.id);
            Assert.That(special.costs[0].amount, Is.EqualTo(200), special.id);
        }
    }
}
