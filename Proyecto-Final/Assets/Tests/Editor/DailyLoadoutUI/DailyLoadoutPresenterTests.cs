using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using F = DailyLoadoutUIFixture;

public sealed class DailyLoadoutPresenterTests
{
    private F f;
    [SetUp] public void Setup() { f = new F(); }
    [TearDown] public void Cleanup() { f?.Dispose(); f = null; }

    private void AssertEmpty(bool plant, bool committed, int position)
        => Assert.That(f.HasValue(plant, committed, position), Is.False);
    private string[] Snapshot(bool committed)
    {
        return Enumerable.Range(0, 6).Select(i => f.HasValue(i < 3, committed, i % 3)
            ? f.Identity(i < 3, committed, i % 3).ToString() : "<empty>").ToArray();
    }

    [Test]
    public void OpenSelectsPlantOneAndSingleAssignmentLeavesOtherFivePositionsEmpty()
    {
        Assert.That(f.Open(), Is.True);
        Assert.That(f.Status, Does.StartWith("Plant 1 selected."));
        Assert.That(F.State(f.Slot(true, 0)), Does.Contain("Selected"));
        Assert.That(F.Name(f.Option(0)), Is.EqualTo("First authored plant"));
        Assert.That(F.Name(f.Option(1)), Is.EqualTo("Second authored plant"));
        Assert.That(Snapshot(true), Is.All.EqualTo("<empty>"));
        Assert.That(F.State(f.Option(0)), Is.EqualTo("Available"));
        f.Assign(0);
        Assert.That(F.State(f.Option(0)), Is.EqualTo("Selected"));
        f.Select(true, 1);
        Assert.That(F.State(f.Option(0)), Is.EqualTo("Selected"));
        f.Select(true, 0);
        Assert.That(F.State(f.Option(0)), Is.EqualTo("Selected"));
        Assert.That(f.Identity(true, false, 0), Is.EqualTo(F.Seed(7)));
        Assert.That(Snapshot(false).Count(x => x != "<empty>"), Is.EqualTo(1));
        f.Action("Confirm").onClick.Invoke();
        Assert.That(Snapshot(true).Count(x => x != "<empty>"), Is.EqualTo(1));
        Assert.That(F.State(f.Slot(true, 0)), Does.Contain("Selected"));
        f.Select(false, 2); Assert.That(F.Name(f.Option(0)), Is.EqualTo("First authored spell"));
        f.Select(true, 0); Assert.That(F.Name(f.Option(0)), Is.EqualTo("First authored plant"));
    }

    [Test]
    public void CatalogUsesAuthoredOrderDeduplicatesIdentitiesFiltersLockedAndDoesNotGrantStock()
    {
        int seedCount = (int)F.Call(f.Seeds, "GetSeedCountInSlot", 0);
        int selectedSeed = (int)F.Call(f.Seeds, "GetSelectedSlotIndex");
        int selectedSpell = (int)F.Call(f.Spells, "GetSelectedSlotIndex");
        int notifications = 0;
        f.Seeds.GetType().GetField("onInventoryChanged").SetValue(f.Seeds, (Action)(() => notifications++));
        var spellEvent = f.Spells.GetType().GetEvent("OnSpellInventoryChanged");
        Action onSpell = () => notifications++;
        spellEvent.AddEventHandler(f.Spells, onSpell);
        try
        {
            Assert.That(f.Open(), Is.True);
            f.Select(true, 2);
            Assert.That(F.Name(f.Option(0)), Is.EqualTo("First authored plant"));
            Assert.That(F.Name(f.Option(1)), Is.EqualTo("Second authored plant"));
            Assert.That(((Image)F.Field(f.Option(0), "icon")).sprite, Is.SameAs(F.Field(f.FirstPlant, "plantIcon")));
            Assert.That(f.Option(2).gameObject.activeSelf, Is.False);
            f.Select(false, 2);
            Assert.That(F.Name(f.Option(0)), Is.EqualTo("First authored spell"));
            Assert.That(F.Name(f.Option(1)), Is.EqualTo("Second authored spell"));
            Assert.That(((Image)F.Field(f.Option(0), "icon")).sprite, Is.SameAs(F.Property(f.FirstSpell, "SpellIcon")));
            Assert.That(f.Option(2).gameObject.activeSelf, Is.False);
            f.Refresh(); f.Refresh();
            Assert.That(F.Call(f.Seeds, "GetSeedCountInSlot", 0), Is.EqualTo(seedCount));
            Assert.That(F.Call(f.Seeds, "GetSelectedSlotIndex"), Is.EqualTo(selectedSeed));
            Assert.That(F.Call(f.Spells, "GetSelectedSlotIndex"), Is.EqualTo(selectedSpell));
            Assert.That(notifications, Is.Zero);
        }
        finally { spellEvent.RemoveEventHandler(f.Spells, onSpell); }
        f.AdvanceCatalog();
        f.Select(true, 2); Assert.That(F.Name(f.Option(2)), Is.EqualTo("Later plant"));
        f.Select(false, 2); Assert.That(F.Name(f.Option(2)), Is.EqualTo("Later spell"));
    }

    [TestCase(true)] [TestCase(false)]
    public void AssignmentUsesIdentityNotInventoryIndexAndLeavesCommittedUntouched(bool plants)
    {
        f.AdvanceCatalog(); Assert.That(f.Open(), Is.True);
        string[] before = Snapshot(true);
        f.Select(plants, 2); f.Assign(2);
        Assert.That(f.Identity(plants, false, 2), Is.EqualTo(plants ? F.Seed(14) : (object)f.LaterSpell));
        CollectionAssert.AreEqual(before, Snapshot(true));
        Assert.That(F.Name(f.Slot(plants, 2)), Is.EqualTo(plants ? "Later plant" : "Later spell"));
        Assert.That(f.Confirmed, Is.False);
    }

    [TestCase(true)] [TestCase(false)]
    public void DuplicateIsRejectedWithoutSwapOrAnyDraftOrCommittedMutation(bool plants)
    {
        f.EquipInitial(); Assert.That(f.Open(), Is.True);
        string[] draft = Snapshot(false), committed = Snapshot(true);
        f.Select(plants, 1); f.Assign(0);
        CollectionAssert.AreEqual(draft, Snapshot(false));
        CollectionAssert.AreEqual(committed, Snapshot(true));
        Assert.That(f.Status, Does.Contain("Assignment rejected"));
        Assert.That(F.State(f.Option(0)), Is.EqualTo("Selected"));
        var rejectedBackground = (Image)F.Field(f.Option(0), "background");
        Assert.That(rejectedBackground.color.r, Is.GreaterThan(rejectedBackground.color.g));
        Assert.That(f.Confirmed, Is.False);
    }

    [Test]
    public void ClearAcceptsAlreadyEmptyAndCancelDiscardsBothCategoriesWithoutSpendingAllowance()
    {
        Assert.That(f.Open(), Is.True);
        string[] committed = Snapshot(true);
        f.Select(true, 2); f.Action("Clear").onClick.Invoke(); AssertEmpty(true, false, 2);
        f.Select(true, 0); f.Action("Clear").onClick.Invoke(); AssertEmpty(true, false, 0);
        f.Select(false, 0); f.Action("Clear").onClick.Invoke(); AssertEmpty(false, false, 0);
        f.Select(true, 1); f.Assign(0); f.Select(false, 2); f.Assign(1);
        CollectionAssert.AreEqual(committed, Snapshot(true));
        f.Action("Cancel").onClick.Invoke();
        Assert.That(f.IsOpen, Is.False); Assert.That(f.Panel.activeSelf, Is.False);
        Assert.That(F.Property(f.Modals, "HasOpenModal"), Is.False);
        CollectionAssert.AreEqual(committed, Snapshot(false)); Assert.That(f.Confirmed, Is.False);
        Assert.That(f.Open(), Is.True);
        CollectionAssert.AreEqual(committed, Snapshot(false));
    }

    [Test]
    public void ConfirmCommitsSixPositionsAtomicallyConsumesOnceAndRendersReadOnly()
    {
        f.AdvanceCatalog(); Assert.That(f.Open(), Is.True);
        for (int i = 0; i < 3; i++)
        {
            f.Select(true, i); f.Action("Clear").onClick.Invoke();
            f.Select(false, i); f.Action("Clear").onClick.Invoke();
        }
        f.Select(true, 1); f.Assign(2); f.Select(false, 2); f.Assign(0);
        string[] expected = Snapshot(false);
        f.Action("Confirm").onClick.Invoke();
        CollectionAssert.AreEqual(expected, Snapshot(true));
        Assert.That(f.Confirmed, Is.True); Assert.That(f.Status, Does.Contain("consumed"));
        Assert.That(f.Action("Confirm").interactable, Is.False); Assert.That(f.Action("Clear").interactable, Is.False);
        foreach (bool category in new[] { true, false })
            for (int i = 0; i < 3; i++)
            {
                Assert.That(F.SelectButton(f.Slot(category, i)).interactable, Is.True);
                Assert.That(F.State(f.Slot(category, i)), Does.Contain("Read-only"));
            }
        F.Call(f.Presenter, "SelectPosition", false, 1);
        Assert.That(F.Name(f.Option(0)), Is.EqualTo("First authored spell"));
        Assert.That(f.Status, Does.StartWith("Spell 2 selected."));
        Assert.That(F.State(f.Slot(false, 1)), Does.Contain("Selected"));
        F.Call(f.Presenter, "Clear"); F.Call(f.Presenter, "Confirm");
        CollectionAssert.AreEqual(expected, Snapshot(true)); CollectionAssert.AreEqual(expected, Snapshot(false));
        f.Action("Cancel").onClick.Invoke(); Assert.That(f.Open(), Is.True);
        F.Call(f.Days, "StartDay"); // A repeated phase/day notification is not a new allowance.
        f.Refresh(); Assert.That(f.Confirmed, Is.True);
        Assert.That(f.Action("Confirm").interactable, Is.False);
    }

    [Test]
    public void EligibilityLossRejectsAtomicConfirmationPreservesEntireDraftAndAllowance()
    {
        f.AdvanceCatalog(); Assert.That(f.Open(), Is.True);
        f.Select(true, 2); f.Assign(2); f.Select(false, 2); f.Assign(2);
        string[] draft = Snapshot(false), committed = Snapshot(true);
        F.Call(f.Unlocks, "RebuildUnlocksUpToLevel", 1);
        f.Action("Confirm").onClick.Invoke();
        CollectionAssert.AreEqual(draft, Snapshot(false)); CollectionAssert.AreEqual(committed, Snapshot(true));
        Assert.That(f.Confirmed, Is.False); Assert.That(f.Status, Does.Contain("Confirmation rejected"));
        Assert.That(f.Action("Confirm").interactable, Is.True);
    }

    [Test]
    public void NewDayKeepsCommittedConfigurationAndRestoresEditingWithoutReconfirmation()
    {
        Assert.That(f.Open(), Is.True); f.Assign(0); f.Select(false, 1); f.Assign(1);
        f.Action("Confirm").onClick.Invoke(); string[] expected = Snapshot(true);
        F.Call(f.Days, "StartNight"); Assert.That(f.IsOpen, Is.False);
        F.Call(f.Days, "StartDay"); Assert.That(f.Confirmed, Is.False);
        Assert.That(f.Open(), Is.True); Assert.That(f.Action("Confirm").interactable, Is.True);
        CollectionAssert.AreEqual(expected, Snapshot(true)); CollectionAssert.AreEqual(expected, Snapshot(false));
        F.Call(f.Days, "StartDay"); Assert.That(f.Confirmed, Is.False);
    }

    [Test]
    public void ExistingModalCannotBeStolenAndExternalModalCloseDiscardsDraft()
    {
        f.EquipInitial();
        var inventory = Enum.Parse(F.Production("UIModal"), "Inventory");
        Assert.That(F.Call(f.Modals, "Open", inventory), Is.EqualTo(true));
        Assert.That(f.Open(), Is.False); Assert.That(F.Property(f.Modals, "CurrentModal"), Is.EqualTo(inventory));
        F.Call(f.Modals, "Close", inventory); Assert.That(f.Open(), Is.True);
        f.Select(true, 0); f.Action("Clear").onClick.Invoke();
        F.Call(f.Modals, "Close", Enum.Parse(F.Production("UIModal"), "DailyLoadout"));
        Assert.That(f.IsOpen, Is.False); Assert.That(f.HasValue(true, false, 0), Is.True);
        Assert.That(F.Call(f.Modals, "Open", inventory), Is.EqualTo(true));
        F.Call(f.Presenter, "Close"); Assert.That(F.Property(f.Modals, "CurrentModal"), Is.EqualTo(inventory));
    }

    [Test]
    public void ReopenIsIdempotentAndDisableUnbindsOwnedCallbacksOnly()
    {
        f.EquipInitial();
        int externalClicks = 0;
        f.Action("Clear").onClick.AddListener(() => externalClicks++);
        for (int i = 0; i < 3; i++)
        {
            Assert.That(f.Open(), Is.True); Assert.That(f.Open(), Is.True);
            f.Select(true, 0); f.Action("Clear").onClick.Invoke();
            Assert.That(f.HasValue(true, false, 0), Is.False);
            Assert.That(f.HasValue(true, true, 0), Is.True);
            f.Panel.SetActive(false);
            // EditMode explicitly drives ordinary MonoBehaviour lifecycle callbacks,
            // as the fixture does for owner initialization. PlayMode tests engine dispatch.
            F.Call(f.Presenter, "OnDisable");
            Assert.That(f.IsOpen, Is.False);
            Assert.That(f.Panel.activeSelf, Is.False);
            Assert.That(f.HasValue(true, false, 0), Is.True);
            Assert.That(F.Property(f.Modals, "HasOpenModal"), Is.False);
            f.Action("Clear").onClick.Invoke(); // Only the unrelated listener must remain.
            Assert.That(f.HasValue(true, false, 0), Is.True);
            Assert.That(externalClicks, Is.EqualTo((i + 1) * 2));
        }
        Assert.That(externalClicks, Is.EqualTo(6));
        Assert.That(f.Confirmed, Is.False);
    }

    [Test]
    public void RepeatedOpenBindsOnlyOneConfirmationCallback()
    {
        Assert.That(f.Open(), Is.True); Assert.That(f.Open(), Is.True);
        f.Action("Confirm").onClick.Invoke();
        Assert.That(f.Confirmed, Is.True); Assert.That(f.Status, Does.Contain("Loadout confirmed."));
        Assert.That(f.Status, Does.Not.Contain("rejected"));
    }

    [Test]
    public void UnconfirmedRunResetRendersFreshDraftRatherThanPreviousEdits()
    {
        Assert.That(f.Open(), Is.True); f.Assign(0);
        Assert.That(f.HasValue(true, false, 0), Is.True);
        F.Call(f.Experience, "ResetProgression"); F.Call(f.Session, "ResetForNewRun"); f.Refresh();
        Assert.That(f.Confirmed, Is.False); CollectionAssert.AreEqual(Snapshot(true), Snapshot(false));
        Assert.That(f.HasValue(true, false, 0), Is.False);
    }

    [Test]
    public void FullRunResetAndDisposedSessionNeverLeaveStaleEditableUI()
    {
        Assert.That(f.Open(), Is.True); f.Assign(0); f.Select(false, 1); f.Assign(0);
        f.Action("Confirm").onClick.Invoke(); Assert.That(f.Confirmed, Is.True);
        F.Call(f.Experience, "ResetProgression"); F.Call(f.Session, "ResetForNewRun"); f.Refresh();
        Assert.That(f.Confirmed, Is.False); Assert.That(f.Action("Confirm").interactable, Is.True);
        Assert.That(f.Status, Does.Not.Contain("consumed")); Assert.That(f.HasValue(true, false, 0), Is.False);
        ((IDisposable)f.Session).Dispose(); f.Refresh();
        Assert.That(f.IsOpen, Is.False); Assert.That(F.Property(f.Modals, "HasOpenModal"), Is.False);
        Assert.That(f.Open(), Is.False); Assert.That(f.Action("Confirm").interactable, Is.False);
    }

    [Test]
    public void MissingSessionFailsClosedWithoutCreatingOwnerOrTakingModal()
    {
        F.Call(f.Presenter, "Initialize", null, null, f.Experience, f.Flow, f.Modals);
        Assert.That(f.Open(), Is.False);
        Assert.That(f.IsOpen, Is.False); Assert.That(f.Action("Confirm").interactable, Is.False);
        Assert.That(f.Action("Clear").interactable, Is.False);
        Assert.That(F.Property(f.Modals, "HasOpenModal"), Is.False);
        Assert.That(f.Status, Does.Contain("unavailable"));
    }

    [Test]
    public void PoolOverflowFailsClosedWithoutInstantiatingOrMutatingEquipment()
    {
        Assert.That(f.Open(), Is.True);
        var pool = new SerializedObject(f.View); var options = pool.FindProperty("optionViews");
        options.arraySize = 1; pool.ApplyModifiedPropertiesWithoutUndo();
        int children = ((RectTransform)F.Property(f.View, "OptionContent")).childCount;
        string[] committed = Snapshot(true), draft = Snapshot(false);
        f.Refresh(); Assert.That(f.Status, Does.Contain("capacity exceeded"));
        Assert.That(f.Action("Confirm").interactable, Is.False); Assert.That(f.Action("Clear").interactable, Is.False);
        F.Call(f.Presenter, "SelectPosition", true, 0); F.Call(f.Presenter, "AssignOption", 0);
        CollectionAssert.AreEqual(committed, Snapshot(true)); CollectionAssert.AreEqual(draft, Snapshot(false));
        Assert.That(((RectTransform)F.Property(f.View, "OptionContent")).childCount, Is.EqualTo(children));
    }
}
