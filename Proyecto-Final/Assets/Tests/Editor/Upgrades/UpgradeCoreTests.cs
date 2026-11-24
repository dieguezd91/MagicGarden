using System;
using System.Collections.Generic;
using MagicGarden;
using NUnit.Framework;

public class UpgradeCoreTests
{
    private Target Target(string id = "weapon")
    {
        var target = new Target { Id = id, Capability = "test", Profile = new ScalingProfile { ratios = new float[] { 1, 0.5f, 1, 1, 1, 1, 1, 0 } } };
        target.Bases[Stat.Damage] = 20;
        target.Bases[Stat.Quantity] = 1;
        target.Bases[Stat.AttackSpeed] = 2;
        return target;
    }
    private StatRule Rule(Stat stat = Stat.Damage, int maximum = 10) => new StatRule { stat = stat, maxStacks = maximum, weight = 1 };
    private Offer Offer(Target target, Stat stat = Stat.Damage, float bonus = 0.2f) => new Offer
        { Target = target, Rule = Rule(stat), Rarity = new RarityRule { rarity = Rarity.Rare, bonus = bonus, weight = 1 } };
    private Balance Balance() => new Balance
    {
        stats = new[] { Rule(), Rule(Stat.Quantity), Rule(Stat.AttackSpeed) },
        rarities = new[] { new RarityRule { rarity = Rarity.Common, bonus = 0.1f, weight = 1 } }
    };
    [Test] public void StackingUsesBaseAndConfiguredRatioWithoutChangingTargetData()
    {
        var state = new RunState(); var target = Target();
        Assert.That(state.Apply(Offer(target)), Is.True);
        Assert.That(state.Apply(Offer(target)), Is.True);
        Assert.That(state.Value(target.Id, Stat.Damage, 20, 0.5f), Is.EqualTo(24).Within(0.0001));
        Assert.That(target.Bases[Stat.Damage], Is.EqualTo(20));
        Assert.That(state.Value("other", Stat.Damage, 20, 0.5f), Is.EqualTo(20));
    }
    [TestCase(0f)] [TestCase(0.05f)] [TestCase(0.1f)] [TestCase(0.2f)] [TestCase(0.35f)]
    public void QuantityIsExactlyOneEvenWithZeroRatioAndEveryRarity(float bonus)
    {
        var state = new RunState(); var target = Target(); var offer = Offer(target, Stat.Quantity, bonus);
        Assert.That(state.Preview(target, offer.Rule, offer.Rarity), Is.EqualTo(2));
        Assert.That(state.Apply(offer), Is.True);
        Assert.That(state.Value(target.Id, Stat.Quantity, 1, 0), Is.EqualTo(2));
    }
    [Test] public void RarityAndRatioAffectPreviewAndApplicationIdentically()
    {
        var target = Target(); var state = new RunState(); var offer = Offer(target, Stat.Damage, 0.35f);
        float result = state.Preview(target, offer.Rule, offer.Rarity);
        state.Apply(offer);
        Assert.That(state.Value(target.Id, Stat.Damage, 20, target.Ratio(Stat.Damage)), Is.EqualTo(result).Within(0.0001));
    }
    [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)] [TestCase(0)] [TestCase(-1)]
    public void InvalidCooldownAlwaysBecomesFiniteAndPositive(float basis)
    {
        float result = new RunState().Cooldown("weapon", basis, 1);
        Assert.That(RunState.Finite(result) && result > 0, Is.True);
    }
    [Test] public void AttackSpeedScalesInverseCooldown()
    {
        var state = new RunState(); var target = Target(); state.Apply(Offer(target, Stat.AttackSpeed));
        Assert.That(state.Cooldown(target.Id, 0.5f, 1), Is.EqualTo(0.5f / 1.2f).Within(0.0001));
        var extreme = Offer(target, Stat.AttackSpeed, float.MaxValue);
        Assert.That(state.Preview(target, extreme.Rule, extreme.Rarity), Is.EqualTo(1000));
        Assert.That(state.Apply(extreme), Is.True);
        Assert.That(state.Value(target.Id, Stat.AttackSpeed, 2, 1), Is.EqualTo(1 / state.Cooldown(target.Id, 0.5f, 1)).Within(0.001));
        Assert.That(state.CanApply(target, Rule(Stat.AttackSpeed)), Is.False);
    }
    [Test] public void SelectionIsSeededUniqueAndOnlyUsesCurrentOwnedTargets()
    {
        var owned = new List<Target> { Target(), Target() }; var state = new RunState(); var balance = Balance();
        var first = OfferSelector.Select(state, owned, balance, new Random(42));
        var second = OfferSelector.Select(state, owned, balance, new Random(42));
        var keys = new HashSet<string>();
        Assert.That(first.Count, Is.EqualTo(3));
        for (int i = 0; i < first.Count; i++)
        {
            Assert.That(keys.Add(first[i].Key), Is.True);
            Assert.That(first[i].Key, Is.EqualTo(second[i].Key));
            Assert.That(first[i].Target.Id, Is.EqualTo("weapon"));
        }
        Assert.That(OfferSelector.Select(state, new List<Target>(), balance, new Random(42)), Is.Empty);
    }
    [Test] public void FiltersUnsupportedInvalidWeightsAndMaximumStacksBeforeRandom()
    {
        var target = Target(); var state = new RunState(); var balance = Balance();
        balance.stats = new[] { Rule(Stat.Range), Rule(Stat.Damage, 1), new StatRule { stat = Stat.Quantity, weight = 0, maxStacks = 2 }, new StatRule { stat = Stat.AttackSpeed, weight = float.NaN, maxStacks = 2 } };
        state.Apply(Offer(target));
        Assert.That(OfferSelector.Select(state, new[] { target }, balance, new Random(1)), Is.Empty);
        var maxed = Offer(target); maxed.Rule.maxStacks = 1;
        Assert.That(state.Apply(maxed), Is.False);
    }
    [Test] public void FewEligibleChoicesNeverProduceDuplicatesOrRequireThree()
    {
        var balance = Balance(); balance.stats = new[] { Rule() };
        Assert.That(OfferSelector.Select(new RunState(), new[] { Target() }, balance, new Random(1)).Count, Is.EqualTo(1));
    }
    [Test] public void WeightDistributionMakesQuantityUncommon()
    {
        var balance = Balance(); balance.stats = new[] { Rule(), new StatRule { stat = Stat.Quantity, weight = 0.025f, maxStacks = 10 } };
        var state = new RunState(); var random = new Random(123); int quantity = 0;
        for (int i = 0; i < 2000; i++)
            if (OfferSelector.Select(state, new[] { Target() }, balance, random, 1)[0].Rule.stat == Stat.Quantity) quantity++;
        Assert.That(quantity, Is.InRange(20, 90));
    }
    [Test] public void CumulativeBoundariesQueueEveryCrossedLevelAndStopAtTen()
    {
        int[] curve = { 0, 100, 250, 450, 750, 1200, 1800, 2600, 3500, 4500 };
        var pending = new List<int>();
        Assert.That(ExperienceCurve.Advance(curve, 1, 99, pending.Add), Is.EqualTo(1));
        Assert.That(ExperienceCurve.Advance(curve, 1, 450, pending.Add), Is.EqualTo(4));
        CollectionAssert.AreEqual(new[] { 2, 3, 4 }, pending);
        Assert.That(ExperienceCurve.Advance(curve, 4, 100000, null), Is.EqualTo(10));
        Assert.That(ExperienceCurve.Next(curve, 10), Is.Zero);
        Assert.That(ExperienceCurve.Add(int.MaxValue - 5, 10), Is.EqualTo(int.MaxValue));
    }
    [Test] public void ExplicitEleventhLevelWorksButMalformedCurvesDoNotExtrapolate()
    {
        int[] curve = { 0, 100, 250, 450, 750, 1200, 1800, 2600, 3500, 4500, 6000 };
        Assert.That(ExperienceCurve.Advance(curve, 10, 6000, null), Is.EqualTo(11));
        Assert.That(ExperienceCurve.Next(new[] { 0, 100, 90 }, 2), Is.Zero);
    }
    [Test] public void DuplicateCostsAreAggregatedBeforeAffordabilityAndOverflowRejected()
    {
        var costs = new[] { new Cost { material = 1, amount = 3 }, new Cost { material = 1, amount = 4 }, new Cost { material = 2, amount = 1 } };
        Assert.That(CostPlanner.Aggregate(costs, out var totals), Is.True);
        Assert.That(totals[1], Is.EqualTo(7));
        Assert.That(CostPlanner.Affordable(totals, id => id == 1 ? 6 : 100), Is.False);
        Assert.That(CostPlanner.Aggregate(new[] { new Cost { material = 1, amount = int.MaxValue }, new Cost { material = 1, amount = 1 } }, out _), Is.False);
        Assert.That(CostPlanner.Aggregate(new[] { new Cost { material = 1, amount = -1 } }, out _), Is.False);
    }
    [Test] public void UnidentifiedLegacyInstancesRetainBaselineWithoutNullKeyExceptions()
    {
        var state = new RunState();
        Assert.That(state.Value(null, Stat.Damage, 20, 1), Is.EqualTo(20));
        Assert.That(state.Effect(null, EffectKind.AreaMultiplier), Is.Zero);
        Assert.That(state.Stacks(null, Stat.Quantity), Is.Zero);
        Assert.That(state.Cooldown(null, 2, 1), Is.EqualTo(2));
    }
    [Test] public void AllPercentageStatsUseTheirConfiguredBaseAndRatio()
    {
        var state = new RunState(); var target = Target();
        for (int i = 0; i < (int)Stat.Quantity; i++)
        {
            var stat = (Stat)i; target.Bases[stat] = 10;
            Assert.That(state.Apply(Offer(target, stat, 0.1f)), Is.True);
            Assert.That(state.Value(target.Id, stat, 10, target.Ratio(stat)), Is.EqualTo(10 + target.Ratio(stat)).Within(0.0001));
        }
    }
    [Test] public void EveryXpThresholdIsInclusiveWithoutSubtractingTheTotal()
    {
        int[] curve = { 0, 100, 250, 450, 750, 1200, 1800, 2600, 3500, 4500 };
        for (int i = 1; i < curve.Length; i++)
        {
            Assert.That(ExperienceCurve.Advance(curve, 1, curve[i] - 1, null), Is.EqualTo(i));
            Assert.That(ExperienceCurve.Advance(curve, 1, curve[i], null), Is.EqualTo(i + 1));
        }
    }
    [Test] public void HugeFiniteBonusesHaveMatchingFinitePreviewAndRuntimeValue()
    {
        var state = new RunState(); var target = Target(); var offer = Offer(target, Stat.Damage, float.MaxValue);
        float preview = state.Preview(target, offer.Rule, offer.Rarity);
        Assert.That(state.Apply(offer), Is.True);
        Assert.That(RunState.Finite(preview), Is.True);
        Assert.That(state.Value(target.Id, Stat.Damage, 20, 0.5f), Is.EqualTo(preview));
        var invalid = Offer(target); invalid.Rarity.bonus = float.NaN;
        Assert.That(state.Apply(invalid), Is.False);
    }
    [Test] public void SpecialEffectsComposeAndCheckPrerequisitesCapabilityAndStacks()
    {
        var state = new RunState(); var target = Target();
        var baseUpgrade = new SpecialDefinition { id = "lateral", capability = "test", maxStacks = 1,
            effects = new[] { new Effect { kind = EffectKind.LateralProjectiles, value = 2 } } };
        var dependent = new SpecialDefinition { id = "pierce", capability = "test", maxStacks = 1, prerequisites = new[] { "lateral" },
            effects = new[] { new Effect { kind = EffectKind.ExtraPierce, value = 1 }, new Effect { kind = EffectKind.TraversalDamage, value = 0.25f } } };
        Assert.That(state.Buy(dependent, target), Is.False);
        Assert.That(state.Buy(baseUpgrade, target), Is.True);
        Assert.That(state.Buy(baseUpgrade, target), Is.False);
        Assert.That(state.Buy(dependent, target), Is.True);
        Assert.That(state.Effect(target.Id, EffectKind.LateralProjectiles), Is.EqualTo(2));
        Assert.That(state.Effect(target.Id, EffectKind.ExtraPierce), Is.EqualTo(1));
        Assert.That(state.Effect(target.Id, EffectKind.TraversalDamage), Is.EqualTo(0.25f));
        var other = Target("other"); other.Capability = "different";
        Assert.That(state.Buy(baseUpgrade, other), Is.False);
        state.Clear(); Assert.That(state.SpecialStacks("lateral", target.Id), Is.Zero);
    }

    // Generic engine coverage for an additive stat at one fixed rarity, using the reserved PlantLimit id.
    // The default balance no longer offers it: the real plant limit grows with the player level (PlantQuantitySystem).
    private const float PlantLimitStart = 5;
    private Target PlayerTarget()
    {
        var target = new Target { Id = "player", Capability = "Player", Profile = new ScalingProfile { ratios = new float[] { 1, 0, 0, 1, 0, 0, 0, 0 } } };
        target.Bases[Stat.MaxHealth] = 100;
        target.Bases[Stat.MoveSpeed] = 5;
        target.Bases[Stat.PlantLimit] = PlantLimitStart;
        return target;
    }
    private StatRule PlantLimitRule(float weight = 0.15f, int maximum = 3) => new StatRule
        { stat = Stat.PlantLimit, displayName = "Garden bloom", weight = weight, maxStacks = maximum, forceRarity = true, fixedRarity = Rarity.Rare };
    private Balance FourRarityBalance(params StatRule[] stats) => new Balance
    {
        stats = stats,
        rarities = new[]
        {
            new RarityRule { rarity = Rarity.Base, weight = 50, bonus = 0.05f }, new RarityRule { rarity = Rarity.Common, weight = 35, bonus = 0.10f },
            new RarityRule { rarity = Rarity.Rare, weight = 12, bonus = 0.20f }, new RarityRule { rarity = Rarity.Legendary, weight = 3, bonus = 0.35f }
        }
    };
    [TestCase(0f)] [TestCase(0.05f)] [TestCase(0.1f)] [TestCase(0.2f)] [TestCase(0.35f)]
    public void PlantLimitIsExactlyOnePerStackForEveryRarityAndStopsAtThree(float bonus)
    {
        var state = new RunState(); var target = PlayerTarget(); var rule = PlantLimitRule();
        var rarity = new RarityRule { rarity = Rarity.Rare, bonus = bonus, weight = 1 };
        for (int stack = 0; stack < 3; stack++)
        {
            Assert.That(state.Value(target.Id, Stat.PlantLimit, PlantLimitStart, target.Ratio(Stat.PlantLimit)), Is.EqualTo(PlantLimitStart + stack));
            Assert.That(state.Preview(target, rule, rarity), Is.EqualTo(PlantLimitStart + stack + 1));
            Assert.That(state.Apply(new Offer { Target = target, Rule = rule, Rarity = rarity }), Is.True);
            Assert.That(state.Stacks(target.Id, Stat.PlantLimit), Is.EqualTo(stack + 1));
        }
        Assert.That(state.Value(target.Id, Stat.PlantLimit, PlantLimitStart, 0), Is.EqualTo(8));
        Assert.That(state.CanApply(target, rule), Is.False);
        Assert.That(state.Apply(new Offer { Target = target, Rule = rule, Rarity = rarity }), Is.False);
        Assert.That(state.Value(target.Id, Stat.PlantLimit, PlantLimitStart, 0), Is.EqualTo(8));
        Assert.That(target.Bases[Stat.PlantLimit], Is.EqualTo(PlantLimitStart));
        Assert.That(state.Value(target.Id, Stat.MaxHealth, 100, 1), Is.EqualTo(100));
        state.Clear();
        Assert.That(state.Value(target.Id, Stat.PlantLimit, PlantLimitStart, 0), Is.EqualTo(PlantLimitStart));
    }
    [Test] public void PlantLimitIsOnlyOfferedToTargetsWithAPlantLimitBase()
    {
        var state = new RunState(); var rule = PlantLimitRule();
        Assert.That(state.CanApply(PlayerTarget(), rule), Is.True);
        Assert.That(state.CanApply(Target(), rule), Is.False);
        var empty = PlayerTarget(); empty.Bases[Stat.PlantLimit] = 0;
        Assert.That(state.CanApply(empty, rule), Is.False);
    }
    [Test] public void MaximumStacksFilterPlantLimitFromTheOfferPool()
    {
        var state = new RunState(); var target = PlayerTarget(); var balance = FourRarityBalance(PlantLimitRule());
        for (int stack = 0; stack < 3; stack++)
        {
            var offers = OfferSelector.Select(state, new[] { target }, balance, new Random(stack));
            Assert.That(offers.Count, Is.EqualTo(1));
            Assert.That(offers[0].Before, Is.EqualTo(PlantLimitStart + stack));
            Assert.That(offers[0].After, Is.EqualTo(PlantLimitStart + stack + 1));
            Assert.That(state.Apply(offers[0]), Is.True);
        }
        Assert.That(OfferSelector.Select(state, new[] { target }, balance, new Random(9)), Is.Empty);
    }
    [Test] public void FixedRarityAlwaysYieldsRareWhileOtherStatsStillRoll()
    {
        var balance = FourRarityBalance(PlantLimitRule(1), new StatRule { stat = Stat.MaxHealth, weight = 1, maxStacks = 10 }, new StatRule { stat = Stat.MoveSpeed, weight = 1, maxStacks = 10 });
        var state = new RunState(); var target = PlayerTarget(); var random = new Random(77);
        var seen = new HashSet<Rarity>(); int plantLimit = 0;
        for (int i = 0; i < 600; i++)
            foreach (var offer in OfferSelector.Select(state, new[] { target }, balance, random, 3))
            {
                if (offer.Rule.stat == Stat.PlantLimit) { plantLimit++; Assert.That(offer.Rarity.rarity, Is.EqualTo(Rarity.Rare)); }
                else seen.Add(offer.Rarity.rarity);
            }
        Assert.That(plantLimit, Is.EqualTo(600));
        CollectionAssert.AreEquivalent(new[] { Rarity.Base, Rarity.Common, Rarity.Rare, Rarity.Legendary }, seen);
    }
    [Test] public void FixedRarityIsNotAppliedWhenTheFlagIsOffAndRuleDefaultsToNotFixed()
    {
        Assert.That(new StatRule().forceRarity, Is.False);
        var balance = FourRarityBalance(new StatRule { stat = Stat.MaxHealth, weight = 1, maxStacks = 10, fixedRarity = Rarity.Rare });
        var state = new RunState(); var target = PlayerTarget(); var random = new Random(5); var seen = new HashSet<Rarity>();
        for (int i = 0; i < 400; i++) seen.Add(OfferSelector.Select(state, new[] { target }, balance, random, 1)[0].Rarity.rarity);
        Assert.That(seen.Count, Is.EqualTo(4));
    }
    [Test] public void FixedRarityMissingFromTheRarityTableRemovesTheOfferInsteadOfSubstituting()
    {
        var balance = FourRarityBalance(PlantLimitRule());
        balance.rarities = new[] { new RarityRule { rarity = Rarity.Common, weight = 1, bonus = 0.1f }, new RarityRule { rarity = Rarity.Rare, weight = 0, bonus = 0.2f } };
        Assert.That(OfferSelector.Select(new RunState(), new[] { PlayerTarget() }, balance, new Random(1)), Is.Empty);
    }
    [Test] public void PlantLimitWeightKeepsItRareRelativeToWeightOneStats()
    {
        var balance = FourRarityBalance(PlantLimitRule(), new StatRule { stat = Stat.MaxHealth, weight = 1, maxStacks = 10 }, new StatRule { stat = Stat.MoveSpeed, weight = 1, maxStacks = 10 });
        var state = new RunState(); var target = PlayerTarget(); var random = new Random(2024); int plantLimit = 0, health = 0;
        for (int i = 0; i < 4000; i++)
        {
            var stat = OfferSelector.Select(state, new[] { target }, balance, random, 1)[0].Rule.stat;
            if (stat == Stat.PlantLimit) plantLimit++; else if (stat == Stat.MaxHealth) health++;
        }
        // Expected share 0.15 / 2.15 = 7.0% against 46.5% for a weight-1 stat.
        Assert.That(plantLimit, Is.InRange(200, 360));
        Assert.That(plantLimit * 4, Is.LessThan(health));
    }
    [Test] public void QuantityStillBehavesAsBeforeAndIsNotFixedToARarity()
    {
        var balance = FourRarityBalance(new StatRule { stat = Stat.Quantity, weight = 1, maxStacks = 3 });
        var state = new RunState(); var target = Target(); var random = new Random(11); var seen = new HashSet<Rarity>();
        for (int i = 0; i < 400; i++) seen.Add(OfferSelector.Select(state, new[] { target }, balance, random, 1)[0].Rarity.rarity);
        Assert.That(seen.Count, Is.EqualTo(4));
        Assert.That(state.Apply(new Offer { Target = target, Rule = balance.stats[0], Rarity = balance.rarities[0] }), Is.True);
        Assert.That(state.Value(target.Id, Stat.Quantity, 1, 0), Is.EqualTo(2));
        Assert.That(state.Value(target.Id, Stat.PlantLimit, PlantLimitStart, 0), Is.EqualTo(PlantLimitStart));
    }
}
