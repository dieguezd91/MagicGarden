using System;
using MagicGarden.Loadout;
using NUnit.Framework;

public sealed class DailyLoadoutStateTests
{
    private static EquipmentAssignment<string> Item(string id) => EquipmentAssignment<string>.For(id);
    private static DailyLoadoutState<string, string> Initialized()
    {
        var state = new DailyLoadoutState<string, string>();
        Assert.That(state.TryInitialize(), Is.True);
        state.TryBeginDay(0);
        state.TrySetDraftPlant(0, Item("plant-a")); state.TrySetDraftPlant(1, Item("plant-b"));
        state.TrySetDraftSpell(0, Item("spell-a")); state.TrySetDraftSpell(1, Item("spell-b"));
        Assert.That(state.TryConfirm(), Is.True);
        state.BeginNight();
        return state;
    }

    private static void AssertEquipment(DailyLoadoutState<string, string> state,
        string[] plants, string[] spells, bool draft = false)
    {
        for (int i = 0; i < 3; i++)
        {
            var plant = draft ? state.GetDraftPlant(i) : state.GetCommittedPlant(i);
            var spell = draft ? state.GetDraftSpell(i) : state.GetCommittedSpell(i);
            Assert.That(plant.HasValue, Is.EqualTo(plants[i] != null), "plant " + i);
            Assert.That(plant.Identity, Is.EqualTo(plants[i]), "plant " + i);
            Assert.That(spell.HasValue, Is.EqualTo(spells[i] != null), "spell " + i);
            Assert.That(spell.Identity, Is.EqualTo(spells[i]), "spell " + i);
        }
    }

    [Test]
    public void NewRunHasSixEmptyPositionsAndNoDayOrConfirmation()
    {
        var state = new DailyLoadoutState<string, string>();
        Assert.That(DailyLoadoutState<string, string>.SlotsPerCategory, Is.EqualTo(3));
        AssertEquipment(state, new string[3], new string[3]);
        AssertEquipment(state, new string[3], new string[3], draft: true);
        Assert.That(state.IsInitialized, Is.False);
        Assert.That(state.IsDay, Is.False);
        Assert.That(state.CurrentDayToken, Is.Null);
        Assert.That(state.HasConfirmedToday, Is.False);
        Assert.That(state.TryConfirm(), Is.False);
    }

    [Test]
    public void InitializationHasSixEmptyPositionsAndDoesNotChooseCatalogDefaults()
    {
        var state = new DailyLoadoutState<string, string>();
        Assert.That(state.TryInitialize(), Is.True);
        AssertEquipment(state, new string[3], new string[3]);
        AssertEquipment(state, new string[3], new string[3], draft: true);
        Assert.That(state.IsInitialized, Is.True);
        Assert.That(state.HasConfirmedToday, Is.False);
        Assert.That(state.IsDay, Is.False);
    }

    [Test]
    public void InitializationCannotOverwriteAnEstablishedRun()
    {
        var state = Initialized();
        Assert.That(state.TryInitialize(), Is.False);
        AssertEquipment(state, new[] { "plant-a", "plant-b", null }, new[] { "spell-a", "spell-b", null });
    }

    [Test]
    public void OnePlantAndOneSpellCanBeConfirmedWithoutFillingOtherPositions()
    {
        var state = new DailyLoadoutState<string, string>(); state.TryInitialize(); state.TryBeginDay(1);
        state.TrySetDraftPlant(1, Item("a")); state.TrySetDraftSpell(2, Item("x"));
        AssertEquipment(state, new string[3], new string[3]);
        Assert.That(state.TryConfirm(), Is.True);
        AssertEquipment(state, new[] { null, "a", null }, new[] { null, null, "x" });
        Assert.That(state.HasConfirmedToday, Is.True);
    }

    [Test]
    public void AutomaticBootstrapDoesNotConsumeTheFirstManualConfirmation()
    {
        var state = new DailyLoadoutState<string, string>();
        Assert.That(state.TryBeginDay(10), Is.True);
        Assert.That(state.TryInitialize(), Is.True);
        Assert.That(state.HasConfirmedToday, Is.False);
        Assert.That(state.TryConfirm(), Is.True);
        Assert.That(state.HasConfirmedToday, Is.True);
        Assert.That(state.TryConfirm(), Is.False);
    }

    [Test]
    public void DraftEditsAffectNeitherCommittedCategoryUntilAtomicConfirmation()
    {
        var state = Initialized();
        state.TryBeginDay(1);
        Assert.That(state.TrySetDraftPlant(0, Item("plant-c")), Is.True);
        Assert.That(state.TrySetDraftSpell(1, Item("spell-c")), Is.True);
        AssertEquipment(state, new[] { "plant-a", "plant-b", null }, new[] { "spell-a", "spell-b", null });
        AssertEquipment(state, new[] { "plant-c", "plant-b", null }, new[] { "spell-a", "spell-c", null }, draft: true);
        Assert.That(state.HasConfirmedToday, Is.False);
        Assert.That(state.TryConfirm(), Is.True);
        AssertEquipment(state, new[] { "plant-c", "plant-b", null }, new[] { "spell-a", "spell-c", null });
    }

    [Test]
    public void DuplicatesAreRejectedWithinEachCategoryWithoutAnyOtherStateChange()
    {
        var state = Initialized();
        state.TryBeginDay(1);
        Assert.That(state.TrySetDraftPlant(2, Item("plant-a")), Is.False);
        Assert.That(state.TrySetDraftSpell(2, Item("spell-b")), Is.False);
        AssertEquipment(state, new[] { "plant-a", "plant-b", null }, new[] { "spell-a", "spell-b", null }, draft: true);
        AssertEquipment(state, new[] { "plant-a", "plant-b", null }, new[] { "spell-a", "spell-b", null });
        Assert.That(state.HasConfirmedToday, Is.False);
        Assert.That(state.TrySetDraftPlant(0, Item("plant-a")), Is.True);
        Assert.That(state.TrySetDraftSpell(0, Item("spell-a")), Is.True);
    }

    [Test]
    public void TheSameIdentityValueInDifferentCategoriesIsNotADuplicate()
    {
        var state = Initialized();
        state.TryBeginDay(1);
        Assert.That(state.TrySetDraftPlant(2, Item("shared")), Is.True);
        Assert.That(state.TrySetDraftSpell(2, Item("shared")), Is.True);
        Assert.That(state.TryConfirm(), Is.True);
        Assert.That(state.GetCommittedPlant(2).Identity, Is.EqualTo("shared"));
        Assert.That(state.GetCommittedSpell(2).Identity, Is.EqualTo("shared"));
    }

    [Test]
    public void EmptyPositionsCanBeCommittedInBothCategories()
    {
        var state = Initialized();
        state.TryBeginDay(1);
        for (int i = 0; i < 3; i++)
        {
            Assert.That(state.TrySetDraftPlant(i, default), Is.True);
            Assert.That(state.TrySetDraftSpell(i, default), Is.True);
        }
        Assert.That(state.TryConfirm(), Is.True);
        AssertEquipment(state, new string[3], new string[3]);
        AssertEquipment(state, new string[3], new string[3], draft: true);
    }

    [Test]
    public void InvalidEquipmentPositionsDoNotMutateDraftCommittedOrAllowance()
    {
        var state = Initialized();
        state.TryBeginDay(1);
        foreach (int index in new[] { -1, 3, int.MaxValue })
        {
            Assert.That(state.TrySetDraftPlant(index, Item("c")), Is.False);
            Assert.That(state.TrySetDraftSpell(index, Item("z")), Is.False);
        }
        AssertEquipment(state, new[] { "plant-a", "plant-b", null }, new[] { "spell-a", "spell-b", null }, draft: true);
        AssertEquipment(state, new[] { "plant-a", "plant-b", null }, new[] { "spell-a", "spell-b", null });
        Assert.That(state.HasConfirmedToday, Is.False);
    }

    [Test]
    public void EditingAndConfirmationRequireAnInitializedRunAndADay()
    {
        var state = new DailyLoadoutState<string, string>();
        state.TryBeginDay(1);
        Assert.That(state.TrySetDraftPlant(0, Item("a")), Is.False);
        Assert.That(state.TrySetDraftSpell(0, Item("x")), Is.False);
        Assert.That(state.TryConfirm(), Is.False);
        Assert.That(state.HasConfirmedToday, Is.False);
        state.TryInitialize();
        state.TrySetDraftPlant(0, Item("a")); state.TrySetDraftSpell(0, Item("x"));
        Assert.That(state.TryConfirm(), Is.True);
        state.BeginNight();
        Assert.That(state.TrySetDraftPlant(0, Item("b")), Is.False);
        Assert.That(state.TrySetDraftSpell(0, Item("y")), Is.False);
        Assert.That(state.TryConfirm(), Is.False);
        AssertEquipment(state, new[] { "a", null, null }, new[] { "x", null, null });
        AssertEquipment(state, new[] { "a", null, null }, new[] { "x", null, null }, draft: true);
        Assert.That(state.HasConfirmedToday, Is.True);
    }

    [Test]
    public void SecondConfirmationIsRejectedAtomicallyAndNextDayRenewsAllowance()
    {
        var state = Initialized();
        state.TryBeginDay(1);
        state.TrySetDraftPlant(0, Item("c"));
        Assert.That(state.TryConfirm(), Is.True);
        Assert.That(state.TrySetDraftPlant(0, Item("d")), Is.False);
        Assert.That(state.TrySetDraftSpell(0, Item("z")), Is.False);
        Assert.That(state.TryConfirm(), Is.False);
        AssertEquipment(state, new[] { "c", "plant-b", null }, new[] { "spell-a", "spell-b", null });
        AssertEquipment(state, new[] { "c", "plant-b", null }, new[] { "spell-a", "spell-b", null }, draft: true);
        state.DiscardDraft();
        Assert.That(state.TrySetDraftPlant(0, default), Is.False);
        Assert.That(state.TrySetDraftSpell(0, default), Is.False);
        Assert.That(state.HasConfirmedToday, Is.True);
        state.BeginNight();
        Assert.That(state.TryBeginDay(2), Is.True);
        Assert.That(state.HasConfirmedToday, Is.False);
        AssertEquipment(state, new[] { "c", "plant-b", null }, new[] { "spell-a", "spell-b", null }, draft: true);
        Assert.That(state.TrySetDraftPlant(0, Item("d")), Is.True);
        Assert.That(state.TrySetDraftSpell(0, Item("z")), Is.True);
        Assert.That(state.TryConfirm(), Is.True);
    }

    [Test]
    public void RepeatedAndStaleDayTokensCannotResetDraftOrReopenConfirmation()
    {
        var state = Initialized();
        state.TryBeginDay(100);
        state.TrySetDraftPlant(0, Item("c"));
        Assert.That(state.TryBeginDay(100), Is.True);
        Assert.That(state.GetDraftPlant(0).Identity, Is.EqualTo("c"));
        Assert.That(state.TryConfirm(), Is.True);
        Assert.That(state.TryBeginDay(99), Is.False);
        Assert.That(state.TryBeginDay(100), Is.True);
        Assert.That(state.TryConfirm(), Is.False);
        Assert.That(state.CurrentDayToken, Is.EqualTo(100));
        Assert.That(state.HasConfirmedToday, Is.True);
        state.BeginNight();
        Assert.That(state.TryBeginDay(100), Is.False);
        Assert.That(state.IsDay, Is.False);
        Assert.That(state.HasConfirmedToday, Is.True);
        Assert.That(state.TryBeginDay(101), Is.True);
        Assert.That(state.HasConfirmedToday, Is.False);
    }

    [Test]
    public void NightDiscardsBothDraftCategoriesAndNextDayCopiesCommittedEquipment()
    {
        var state = Initialized();
        state.TryBeginDay(1);
        state.TrySetDraftPlant(0, Item("c"));
        state.TrySetDraftSpell(0, Item("z"));
        state.BeginNight();
        state.BeginNight();
        Assert.That(state.IsDay, Is.False);
        AssertEquipment(state, new[] { "plant-a", "plant-b", null }, new[] { "spell-a", "spell-b", null }, draft: true);
        Assert.That(state.TryBeginDay(2), Is.True);
        AssertEquipment(state, new[] { "plant-a", "plant-b", null }, new[] { "spell-a", "spell-b", null });
        AssertEquipment(state, new[] { "plant-a", "plant-b", null }, new[] { "spell-a", "spell-b", null }, draft: true);
        Assert.That(state.HasConfirmedToday, Is.False);
    }

    [Test]
    public void ExplicitDiscardDoesNotSpendAllowanceOrChangeCommittedEquipment()
    {
        var state = Initialized();
        state.TryBeginDay(1);
        state.TrySetDraftPlant(0, Item("c"));
        state.TrySetDraftSpell(0, Item("z"));
        state.DiscardDraft();
        AssertEquipment(state, new[] { "plant-a", "plant-b", null }, new[] { "spell-a", "spell-b", null }, draft: true);
        AssertEquipment(state, new[] { "plant-a", "plant-b", null }, new[] { "spell-a", "spell-b", null });
        Assert.That(state.HasConfirmedToday, Is.False);
        Assert.That(state.TryConfirm(), Is.True);
    }

    [Test]
    public void RunResetClearsAllStateAndAllowsBootstrapAndPreviouslyUsedDayTokenAgain()
    {
        var state = Initialized();
        state.TryBeginDay(7);
        state.TryConfirm();
        state.TrySetDraftPlant(0, Item("c"));
        state.ResetRun();
        Assert.That(state.IsInitialized, Is.False);
        Assert.That(state.IsDay, Is.False);
        Assert.That(state.HasConfirmedToday, Is.False);
        Assert.That(state.CurrentDayToken, Is.Null);
        AssertEquipment(state, new string[3], new string[3]);
        AssertEquipment(state, new string[3], new string[3], draft: true);
        Assert.That(state.TryInitialize(), Is.True);
        Assert.That(state.TryBeginDay(7), Is.True);
        Assert.That(state.TryConfirm(), Is.True);
        AssertEquipment(state, new string[3], new string[3]);
    }

    [Test]
    public void InventoryStockAndStorageOrderDoNotParticipateInAssignmentIdentity()
    {
        var plants = new[] { "plant-a", "plant-b" };
        var spells = new[] { "spell-a" };
        var quantities = new[] { 1, 4 };
        var cooldowns = new[] { 8f };
        var state = new DailyLoadoutState<string, string>();
        state.TryInitialize(); state.TryBeginDay(1);
        state.TrySetDraftPlant(0, Item(plants[0])); state.TrySetDraftPlant(1, Item(plants[1]));
        state.TrySetDraftSpell(0, Item(spells[0])); state.TryConfirm();
        Assert.That(quantities, Is.EqualTo(new[] { 1, 4 }));
        Assert.That(cooldowns, Is.EqualTo(new[] { 8f }));
        quantities[0] = 0;
        Array.Reverse(plants);
        plants[0] = null;
        AssertEquipment(state, new[] { "plant-a", "plant-b", null }, new[] { "spell-a", null, null });
        quantities[0] = 5;
        AssertEquipment(state, new[] { "plant-a", "plant-b", null }, new[] { "spell-a", null, null });
    }

    private enum PlantIdentity { None, MoonTear, DarkRoot }
    private sealed class SpellIdentity { }

    [Test]
    public void EnumPlantsAndReferenceSpellsDoNotNeedAStorageIndexOrNewIdScheme()
    {
        var spell = new SpellIdentity();
        var state = new DailyLoadoutState<PlantIdentity, SpellIdentity>();
        Assert.That(state.TryInitialize(), Is.True); state.TryBeginDay(0);
        state.TrySetDraftPlant(0, EquipmentAssignment<PlantIdentity>.For(PlantIdentity.MoonTear));
        state.TrySetDraftSpell(0, EquipmentAssignment<SpellIdentity>.For(spell)); state.TryConfirm(); state.BeginNight();
        Assert.That(state.GetCommittedPlant(0).Identity, Is.EqualTo(PlantIdentity.MoonTear));
        Assert.That(state.GetCommittedSpell(0).Identity, Is.SameAs(spell));
        Assert.That(state.GetCommittedPlant(1).HasValue, Is.False);
        Assert.That(state.GetCommittedPlant(1).Identity, Is.EqualTo(PlantIdentity.None));
        state.TryBeginDay(1);
        Assert.That(state.TrySetDraftSpell(2, EquipmentAssignment<SpellIdentity>.For(spell)), Is.False);
    }

    [Test]
    public void NullReferenceIsNotAnAssignedIdentityAndEmptyIsExplicit()
    {
        Assert.Throws<ArgumentNullException>(() => EquipmentAssignment<string>.For(null));
        Assert.That(default(EquipmentAssignment<string>).HasValue, Is.False);
        Assert.That(EquipmentAssignment<int>.For(0).HasValue, Is.True);
    }
}
