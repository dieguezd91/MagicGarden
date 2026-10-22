using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using F = DailyLoadoutUIFixture;

public sealed class DailyLoadoutDragTests
{
    [Test]
    public void HoverFeedbackSurvivesRefreshAndKeepsOccupiedContentUntilDrop()
    {
        using (var f = new F()) using (var drag = new LoadoutDragHarness(f))
        {
            f.Open(); f.Assign(0); string occupied = F.Name(f.Slot(true, 0));
            drag.Begin(1); drag.Hover(f.Slot(true, 0)); f.Refresh();
            Assert.That(F.State(f.Slot(true, 0)), Is.EqualTo("Selected"));
            Assert.That(F.Property(f.Slot(true, 0), "HasContent"), Is.True);
            Assert.That(F.Name(f.Slot(true, 0)), Is.EqualTo(occupied));
            drag.Hover(f.Slot(false, 1)); f.Refresh();
            Assert.That(F.State(f.Slot(false, 1)), Is.EqualTo("Empty"));
            Assert.That(((Image)F.Field(f.Slot(false, 1), "background")).color, Is.EqualTo((Color)F.Field(f.Slot(false, 1), "invalidColor")));
            drag.End();
            Assert.That(F.State(f.Slot(false, 1)), Does.Not.Contain("Invalid target"));
            Assert.That(F.Name(f.Slot(true, 0)), Is.EqualTo(occupied));
        }
    }

    [Test]
    public void OwnPositionDropIsHarmlessAndNativeAuthoringIsIdempotent()
    {
        using (var f = new F()) using (var drag = new LoadoutDragHarness(f))
        {
            f.Open(); f.Assign(0); var before = Assignments(f);
            drag.Begin(0); drag.Drop(f.Slot(true, 0)); CollectionAssert.AreEqual(before, Assignments(f));
        }
        System.Type authoring = null;
        foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies()) authoring = authoring ?? assembly.GetType("DailyLoadoutDragAuthoring");
        foreach (bool panel in new[] { true, false })
        {
            var root = UnityEditor.PrefabUtility.LoadPrefabContents("Assets/Prefabs/UI/DailyLoadout/" + (panel ? "DailyLoadoutPanel" : "LoadoutOption") + ".prefab");
            try
            {
                var rects = root.GetComponentsInChildren<RectTransform>(true); var positions = new Vector3[rects.Length]; var sizes = new Vector2[rects.Length];
                for (int i = 0; i < rects.Length; i++) { positions[i] = rects[i].localPosition; sizes[i] = rects[i].sizeDelta; }
                var component = root.GetComponent(F.Production("MagicGarden.LoadoutUI." + (panel ? "DailyLoadoutPanelView" : "LoadoutOptionView")));
                var method = authoring.GetMethod(panel ? "ConfigurePanel" : "ConfigureOption");
                method.Invoke(null, new object[] { component }); method.Invoke(null, new object[] { component });
                Assert.That(root.GetComponentsInChildren<RectTransform>(true).Length, Is.EqualTo(rects.Length));
                for (int i = 0; i < rects.Length; i++) { Assert.That(rects[i].localPosition, Is.EqualTo(positions[i])); Assert.That(rects[i].sizeDelta, Is.EqualTo(sizes[i])); }
            }
            finally { UnityEditor.PrefabUtility.UnloadPrefabContents(root); }
        }
    }

    private static object[] Assignments(F f)
    {
        var result = new List<object>();
        foreach (bool plants in new[] { true, false })
            foreach (bool committed in new[] { true, false })
                for (int i = 0; i < 3; i++) { result.Add(f.HasValue(plants, committed, i)); result.Add(f.Identity(plants, committed, i)); }
        return result.ToArray();
    }

    private static GameObject Preview(F f) => f.Panel.transform.Find("Drag Preview").gameObject;

    [TestCase(true)] [TestCase(false)]
    public void ReplacingOccupiedPositionUsesTheSameResultAsClickAndKeepsCommittedEquipment(bool plants)
    {
        using (var f = new F()) using (var drag = new LoadoutDragHarness(f))
        {
            f.Open(); f.Select(plants, 2); f.Assign(0);
            drag.Begin(1); drag.Drop(f.Slot(plants, 2));
            var dropped = Assignments(f);
            Assert.That(f.Identity(plants, false, 2), Is.EqualTo(plants ? F.Seed(3) : (object)f.SecondSpell));
            Assert.That(f.HasValue(plants, true, 2), Is.False);
            f.Action("Cancel").onClick.Invoke(); f.Open(); f.Select(plants, 2); f.Assign(1);
            CollectionAssert.AreEqual(dropped, Assignments(f));
            Assert.That(Preview(f).activeSelf, Is.False);
        }
    }

    [TestCase(true)] [TestCase(false)]
    public void IncompatibleAndDuplicateDropsNeverMutateAnyAssignment(bool plants)
    {
        using (var f = new F()) using (var drag = new LoadoutDragHarness(f))
        {
            f.Open(); f.Select(plants, 0); f.Assign(0);
            var before = Assignments(f);
            drag.Begin(1); drag.Drop(f.Slot(!plants, 2));
            CollectionAssert.AreEqual(before, Assignments(f));
            Assert.That(Preview(f).activeSelf, Is.False);
            drag.Begin(0); drag.Drop(f.Slot(plants, 1));
            CollectionAssert.AreEqual(before, Assignments(f));
            Assert.That(f.Status, Does.Contain("rejected"));
            Assert.That(Preview(f).activeSelf, Is.False);
        }
    }

    [Test]
    public void DragCapturesIdentityInsteadOfReusingTheRefreshedOptionIndex()
    {
        using (var f = new F()) using (var drag = new LoadoutDragHarness(f))
        {
            f.Open(); drag.Begin(0);
            var levels = (IList)F.Field(f.Progression, "levelRequirements");
            var plants = (IList)F.Field(levels[0], "plantUnlocks");
            var first = plants[0]; plants[0] = plants[1]; plants[1] = first;
            f.Refresh(); drag.Drop(f.Slot(true, 2));
            Assert.That(f.Identity(true, false, 2), Is.EqualTo(F.Seed(7)));
            Assert.That(f.HasValue(false, false, 2), Is.False);
        }
    }

    [TestCase(true)] [TestCase(false)]
    public void SwitchingCategoryPanelsCancelsTheHiddenSourceWithoutAssigningAnotherCategory(bool useTab)
    {
        using (var f = new F()) using (var drag = new LoadoutDragHarness(f))
        {
            f.Open(); var before = Assignments(f); drag.Begin(0);
            if (useTab) ((Button)F.Field(f.View, "spellTabButton")).onClick.Invoke();
            else f.Select(false, 0);
            Assert.That(Preview(f).activeSelf, Is.False);
            drag.Drop(f.Slot(true, 2));
            CollectionAssert.AreEqual(before, Assignments(f));
        }
    }

    [TestCase(true)] [TestCase(false)]
    public void RevokedUnlockCancelsTheCapturedSourceWithoutMutation(bool plants)
    {
        using (var f = new F()) using (var drag = new LoadoutDragHarness(f))
        {
            f.Open(); f.AdvanceCatalog(); f.Select(plants, 0); drag.Begin(2);
            Assert.That(Preview(f).activeSelf, Is.True);
            var before = Assignments(f);
            F.Call(f.Unlocks, "RebuildUnlocksUpToLevel", 1); f.Refresh();
            drag.Drop(f.Slot(plants, 2));
            CollectionAssert.AreEqual(before, Assignments(f)); Assert.That(Preview(f).activeSelf, Is.False);
        }
    }

    [Test]
    public void OutsideDropAndCancelReopenPreserveCommittedStateAndDiscardOnlyOnClose()
    {
        using (var f = new F()) using (var drag = new LoadoutDragHarness(f))
        {
            f.Open(); f.Assign(0); var before = Assignments(f);
            drag.Begin(1); Assert.That(Preview(f).activeSelf, Is.True); drag.End();
            CollectionAssert.AreEqual(before, Assignments(f)); Assert.That(Preview(f).activeSelf, Is.False);
            drag.Begin(1); f.Action("Cancel").onClick.Invoke();
            Assert.That(Preview(f).activeSelf, Is.False); f.Open();
            Assert.That(f.HasValue(true, false, 0), Is.False); Assert.That(f.HasValue(true, true, 0), Is.False);
        }
    }

    [TestCase("Reset")] [TestCase("Night")] [TestCase("Disable")]
    [TestCase("Dispose")] [TestCase("Confirm")] [TestCase("ExternalConfirm")] [TestCase("Modal")]
    public void LifecycleChangesReleasePreviewAndHighlights(string change)
    {
        using (var f = new F()) using (var drag = new LoadoutDragHarness(f))
        {
            f.Open(); drag.Begin(0); drag.Hover(f.Slot(true, 2)); f.Refresh();
            Assert.That(Preview(f).activeSelf, Is.True);
            if (change == "Reset") F.Call(f.Session, "ResetForNewRun");
            if (change == "Night") F.Call(f.Days, "StartNight");
            if (change == "Disable") { f.Panel.SetActive(false); if (!Application.isPlaying) F.Call(f.Presenter, "OnDisable"); }
            if (change == "Dispose") ((System.IDisposable)f.Session).Dispose();
            if (change == "Confirm") f.Action("Confirm").onClick.Invoke();
            if (change == "ExternalConfirm") F.Call(f.Session, "TryConfirm");
            if (change == "Modal") F.Call(f.Modals, "Close", F.Property(f.Modals, "CurrentModal"));
            f.Refresh(); Assert.That(Preview(f).activeSelf, Is.False);
            foreach (bool plants in new[] { true, false }) for (int i = 0; i < 3; i++)
                Assert.That(F.State(f.Slot(plants, i)), Does.Not.Contain("Drop here").And.Not.Contain("Compatible"));
        }
    }

    [Test]
    public void ReplacedProductionSessionClosesTheOldDragEvenWhenTheOldSessionIsStillReady()
    {
        using (var f = new F()) using (var drag = new LoadoutDragHarness(f))
        {
            F.Set(f.Presenter, "suppliedSession", null); f.Open(); drag.Begin(0);
            var replacement = System.Activator.CreateInstance(F.Production("DailyLoadoutSession"), f.Unlocks, f.Experience, f.Seeds, f.Spells, f.Days, f.Flow);
            try
            {
                F.Set(f.Unlocks, "<Loadout>k__BackingField", replacement); f.Refresh();
                Assert.That(f.IsOpen, Is.False); Assert.That(Preview(f).activeSelf, Is.False);
                Assert.That(f.HasValue(true, false, 2), Is.False);
            }
            finally { F.Set(f.Unlocks, "<Loadout>k__BackingField", f.Session); ((System.IDisposable)replacement).Dispose(); }
        }
    }

    [Test]
    public void ConfirmedDragAssignmentsAppearInTheExistingGameplayHud()
    {
        using (var f = new F()) using (var drag = new LoadoutDragHarness(f))
        {
            f.Open(); drag.Begin(0); drag.Drop(f.Slot(true, 2));
            f.Select(false, 0); drag.Begin(0); drag.Drop(f.Slot(false, 1));
            f.Action("Confirm").onClick.Invoke(); Assert.That(f.Confirmed, Is.True);
            Assert.That(f.Identity(true, true, 2), Is.EqualTo(F.Seed(7)));
            Assert.That(f.Identity(false, true, 1), Is.SameAs(f.FirstSpell));
            var plants = f.Hud(true); var spells = f.Hud(false);
            Assert.That(((Image)F.Field(F.HudSlot(plants, true, 2), "slotIcon")).sprite, Is.SameAs(F.Field(f.FirstPlant, "plantIcon")));
            Assert.That(((Image)F.Field(F.HudSlot(spells, false, 1), "slotIcon")).sprite, Is.SameAs(F.Property(f.FirstSpell, "SpellIcon")));
        }
    }

    [Test]
    public void ReadOnlyAndUnavailablePoolEntriesCannotStartDragging()
    {
        using (var f = new F()) using (var drag = new LoadoutDragHarness(f))
        {
            f.Open(); drag.Begin(5); Assert.That(Preview(f).activeSelf, Is.False);
            f.Action("Confirm").onClick.Invoke(); var before = Assignments(f);
            Assert.That(F.SelectButton(f.Slot(false, 2)).interactable, Is.True, "Read-only category browsing must remain available");
            drag.Begin(0); drag.Drop(f.Slot(true, 2));
            CollectionAssert.AreEqual(before, Assignments(f)); Assert.That(Preview(f).activeSelf, Is.False);
        }
    }

    [TestCase(true, 2)]
    [TestCase(false, 1)]
    public void NativeDragAssignsTheActualUnselectedTargetWithoutCommitting(bool plants, int position)
    {
        using (var f = new F())
        using (var drag = new LoadoutDragHarness(f))
        {
            Assert.That(f.Open(), Is.True); f.Select(plants, 0);
            drag.Begin(0);
            Assert.That(f.HasValue(plants, false, position), Is.False, "Beginning a drag must not edit the draft");
            f.Refresh(); drag.Drop(f.Slot(plants, position));
            Assert.That(f.HasValue(plants, false, position), Is.True);
            Assert.That(f.Identity(plants, false, position), Is.EqualTo(plants ? F.Seed(7) : (object)f.FirstSpell));
            Assert.That(f.HasValue(plants, true, position), Is.False);
            Assert.That(F.Property(f.Presenter, "SelectedPosition"), Is.EqualTo(position));
            Assert.That(F.Property(f.Presenter, "IsPlantCategory"), Is.EqualTo(plants));
            Assert.That(f.Status, Does.Contain("Draft updated. Confirm to commit."));
        }
    }

    [Test]
    public void PanelHasOneInactiveAuthoredNonRaycastPreview()
    {
        using (var f = new F())
        {
            var preview = f.Panel.transform.Find("Drag Preview");
            Assert.That(preview, Is.Not.Null);
            Assert.That(preview.gameObject.activeSelf, Is.False);
            Assert.That(preview.GetComponent<Image>().raycastTarget, Is.False);
            Assert.That(preview.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
            Assert.That(preview.parent, Is.SameAs(f.Panel.transform));
            Assert.That(preview.GetSiblingIndex(), Is.EqualTo(f.Panel.transform.childCount - 1));
        }
    }
}
