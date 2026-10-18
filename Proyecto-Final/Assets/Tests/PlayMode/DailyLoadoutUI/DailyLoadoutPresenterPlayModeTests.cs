#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using F = DailyLoadoutUIFixture;

public sealed class DailyLoadoutPresenterPlayModeTests
{
    private F f;
    [SetUp] public void Setup() { f = new F(); }
    [TearDown] public void Cleanup() { f?.Dispose(); f = null; }

    [UnityTest]
    public IEnumerator NightWhileOpenClosesImmediatelyDiscardsDraftAndReleasesModal()
    {
        f.EquipInitial();
        Assert.That(f.Open(), Is.True);
        f.Select(true, 0); f.Action("Clear").onClick.Invoke();
        f.Select(false, 0); f.Action("Clear").onClick.Invoke();
        Assert.That(f.HasValue(true, false, 0), Is.False);
        F.Call(f.Days, "StartNight");
        Assert.That(f.IsOpen, Is.False); Assert.That(f.Panel.activeSelf, Is.False);
        Assert.That(F.Property(f.Modals, "HasOpenModal"), Is.False);
        Assert.That(f.HasValue(true, false, 0), Is.True); Assert.That(f.HasValue(false, false, 0), Is.True);
        Assert.That(f.Confirmed, Is.False); Assert.That(f.Open(), Is.False);
        yield return null;
        F.Call(f.Days, "StartDay"); Assert.That(f.Open(), Is.True);
        Assert.That(f.Action("Confirm").interactable, Is.True);
    }

    [UnityTest]
    public IEnumerator PublicButtonsEditConfirmAndSurviveNewDayWithoutRuntimeHierarchyCreation()
    {
        f.AdvanceCatalog(); Assert.That(f.Open(), Is.True);
        int children = ((RectTransform)F.Property(f.View, "OptionContent")).childCount;
        f.Select(true, 2); f.Assign(2); f.Select(false, 2); f.Assign(2);
        yield return null;
        Assert.That(f.Identity(true, false, 2), Is.EqualTo(F.Seed(14)));
        Assert.That(f.Identity(false, false, 2), Is.SameAs(f.LaterSpell));
        Assert.That(f.HasValue(true, true, 2), Is.False); Assert.That(f.HasValue(false, true, 2), Is.False);
        f.Action("Confirm").onClick.Invoke();
        Assert.That(f.Identity(true, true, 2), Is.EqualTo(F.Seed(14)));
        Assert.That(f.Identity(false, true, 2), Is.SameAs(f.LaterSpell));
        Assert.That(f.Confirmed, Is.True); Assert.That(f.Action("Confirm").interactable, Is.False);
        F.Call(f.Days, "StartNight"); F.Call(f.Days, "StartDay"); Assert.That(f.Open(), Is.True);
        yield return null;
        Assert.That(f.Confirmed, Is.False); Assert.That(f.Action("Confirm").interactable, Is.True);
        Assert.That(f.Identity(false, true, 2), Is.SameAs(f.LaterSpell));
        Assert.That(((RectTransform)F.Property(f.View, "OptionContent")).childCount, Is.EqualTo(children));
    }

    [UnityTest]
    public IEnumerator EffectiveUpgradeOverlayBlocksEditingEvenWithoutPhaseEvent()
    {
        f.EquipInitial();
        Assert.That(f.Open(), Is.True); f.Select(true, 0);
        F.Set(f.Flow, "<IsChoosingUpgrade>k__BackingField", true);
        yield return null;
        Assert.That(f.Action("Confirm").interactable, Is.False); Assert.That(f.Action("Clear").interactable, Is.False);
        F.Call(f.Presenter, "Clear"); Assert.That(f.HasValue(true, false, 0), Is.True);
        Assert.That(f.Confirmed, Is.False);
        F.Set(f.Flow, "<IsChoosingUpgrade>k__BackingField", false);
        yield return null;
        Assert.That(f.Action("Confirm").interactable, Is.True);
    }

    [UnityTest]
    public IEnumerator DisableAndDestroyReleaseSubscriptionsWithoutTouchingAnotherModal()
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
            f.Panel.SetActive(false); // Actual Unity dispatch; no manual lifecycle invocation.
            yield return null;
            Assert.That(f.IsOpen, Is.False); Assert.That(f.HasValue(true, false, 0), Is.True);
            Assert.That(F.Property(f.Modals, "HasOpenModal"), Is.False);
            f.Action("Clear").onClick.Invoke();
            Assert.That(f.HasValue(true, false, 0), Is.True);
            Assert.That(externalClicks, Is.EqualTo((i + 1) * 2));
            Assert.That(f.Confirmed, Is.False);
        }
        Assert.That(f.Open(), Is.True);
        f.Select(true, 0); f.Action("Clear").onClick.Invoke();
        Assert.That(f.HasValue(true, false, 0), Is.False);
        Object.Destroy(f.Panel);
        yield return null;
        Assert.That(f.HasValue(true, false, 0), Is.True);
        Assert.That(f.Confirmed, Is.False);
        Assert.That(F.Property(f.Modals, "HasOpenModal"), Is.False);
        var inventory = System.Enum.Parse(F.Production("UIModal"), "Inventory");
        Assert.That(F.Call(f.Modals, "Open", inventory), Is.EqualTo(true));
        F.Call(f.Days, "StartNight");
        Assert.That(F.Property(f.Modals, "CurrentModal"), Is.EqualTo(inventory));
    }

    [UnityTest]
    public IEnumerator ResetAndSessionDisposalAreObservedByActivePresenterUpdate()
    {
        Assert.That(f.Open(), Is.True); f.Action("Confirm").onClick.Invoke();
        F.Call(f.Experience, "ResetProgression"); F.Call(f.Session, "ResetForNewRun");
        yield return null;
        Assert.That(f.IsOpen, Is.True); Assert.That(f.Confirmed, Is.False);
        Assert.That(f.Action("Confirm").interactable, Is.True); Assert.That(f.Status, Does.Not.Contain("consumed"));
        ((System.IDisposable)f.Session).Dispose();
        yield return null;
        Assert.That(f.IsOpen, Is.False); Assert.That(F.Property(f.Modals, "HasOpenModal"), Is.False);
    }
}
#endif
