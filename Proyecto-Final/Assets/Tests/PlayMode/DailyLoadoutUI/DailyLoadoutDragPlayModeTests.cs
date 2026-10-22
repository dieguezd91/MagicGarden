#if UNITY_EDITOR
using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using F = DailyLoadoutUIFixture;

public sealed class DailyLoadoutDragPlayModeTests
{
    [UnityTest]
    public IEnumerator CatalogScrollingFollowsLiveOverflowAndReturnsToTopWhenUnlockedOptionsFit()
    {
        using (var f = new F()) using (var drag = new LoadoutDragHarness(f))
        {
            f.Open(); yield return null; Canvas.ForceUpdateCanvases(); yield return null;
            var scroll = (ScrollRect)F.Property(f.View, "OptionsScroll");
            Assert.That(scroll.vertical, Is.False);
            var before = scroll.content.anchoredPosition;
            ExecuteEvents.Execute(scroll.gameObject, new PointerEventData(drag.Events)
                { scrollDelta = new Vector2(0, -1) }, ExecuteEvents.scrollHandler);
            Assert.That(scroll.content.anchoredPosition, Is.EqualTo(before));
            f.Select(false, 0); yield return null; Canvas.ForceUpdateCanvases(); yield return null;
            scroll = (ScrollRect)F.Property(f.View, "OptionsScroll");
            Assert.That(scroll.vertical, Is.False);
            f.AdvanceCatalog(); yield return null; Canvas.ForceUpdateCanvases(); yield return null;
            Assert.That(scroll.vertical, Is.True, "Third spell starts an overflowing second row in the authored two-column spell catalog");
            scroll.verticalNormalizedPosition = 0; scroll.velocity = new Vector2(0, 400);
            F.Call(f.Unlocks, "RebuildUnlocksUpToLevel", 1); f.Refresh();
            yield return null; Canvas.ForceUpdateCanvases(); yield return null;
            Assert.That(scroll.vertical, Is.False);
            Assert.That(scroll.content.anchoredPosition.y, Is.EqualTo(0).Within(.01f));
            Assert.That(scroll.velocity, Is.EqualTo(Vector2.zero));
            Assert.That(((TMPro.TMP_Text)F.Field(f.Option(0), "stateLabel")).gameObject.activeSelf, Is.False);
        }
    }

    [UnityTest]
    public IEnumerator CategoryTabRaycastsOnlyExposeTheActiveCatalog()
    {
        using (var f = new F()) using (var drag = new LoadoutDragHarness(f))
        {
            f.Open(); yield return null; Canvas.ForceUpdateCanvases();
            var oldOption = f.Option(0);
            var tab = (Button)F.Field(f.View, "spellTabButton");
            var pointer = new PointerEventData(EventSystem.current)
                { position = LoadoutDragHarness.Center((RectTransform)tab.transform), button = PointerEventData.InputButton.Left };
            var hit = drag.Raycast(pointer.position);
            Assert.That(hit, Is.Not.Null);
            var click = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hit);
            Assert.That(click, Is.SameAs(tab.gameObject));
            ExecuteEvents.Execute(click, pointer, ExecuteEvents.pointerClickHandler);
            yield return null; Canvas.ForceUpdateCanvases();
            Assert.That(oldOption.gameObject.activeInHierarchy, Is.False);
            Assert.That(F.Property(f.Presenter, "IsPlantCategory"), Is.False);
            var spells = (GameObject)F.Field(f.View, "spellOptionsPanel");
            hit = drag.Raycast(LoadoutDragHarness.Center((RectTransform)f.Option(0).transform));
            Assert.That(hit, Is.Not.Null); Assert.That(hit.transform.IsChildOf(spells.transform), Is.True);
            Assert.That(((GameObject)F.Field(f.View, "plantOptionsPanel")).activeSelf, Is.False);
        }
    }

    [UnityTest]
    public IEnumerator DestroyingThePanelReleasesAnUnfinishedDragSubscription()
    {
        using (var f = new F()) using (var drag = new LoadoutDragHarness(f))
        {
            f.Open(); yield return null;
            int before = ((Delegate)F.Field(f.Session, "CommittedChanged"))?.GetInvocationList().Length ?? 0;
            drag.Begin(0); UnityEngine.Object.Destroy(f.Panel); yield return null;
            Assert.That(((Delegate)F.Field(f.Session, "CommittedChanged"))?.GetInvocationList().Length ?? 0, Is.EqualTo(before));
        }
    }
    [UnityTest]
    public IEnumerator NativeRaycastsChooseIconDragButCardBodyAndWheelStillReachScrollRect()
    {
        using (var f = new F()) using (var drag = new LoadoutDragHarness(f))
        {
            f.Open(); f.ExpandPlantCatalogToSixOptions(); yield return null; Canvas.ForceUpdateCanvases();
            var area = drag.IconArea(0); var iconHit = drag.Raycast(LoadoutDragHarness.Center(area.transform));
            Assert.That(iconHit, Is.Not.Null);
            var iconDrag = ExecuteEvents.GetEventHandler<IDragHandler>(iconHit);
            Assert.That(iconDrag, Is.SameAs(area));
            var bodyHit = drag.Raycast(LoadoutDragHarness.Center(f.Option(0)));
            var scroll = (ScrollRect)F.Property(f.View, "OptionsScroll");
            Assert.That(ExecuteEvents.GetEventHandler<IDragHandler>(bodyHit), Is.SameAs(scroll.gameObject));
            Assert.That(ExecuteEvents.GetEventHandler<IScrollHandler>(iconHit), Is.SameAs(scroll.gameObject));
            Canvas.ForceUpdateCanvases();
            Assert.That(scroll.content.rect.height, Is.GreaterThan(scroll.viewport.rect.height), "Six real options must overflow the authored category viewport");
            Canvas.ForceUpdateCanvases(); scroll.verticalNormalizedPosition = 1;
            var wheel = new PointerEventData(drag.Events) { position = LoadoutDragHarness.Center(area.transform), scrollDelta = new Vector2(0, -1) };
            ExecuteEvents.ExecuteHierarchy(iconHit, wheel, ExecuteEvents.scrollHandler);
            Assert.That(scroll.verticalNormalizedPosition, Is.LessThan(1));
            var pointer = new PointerEventData(drag.Events) { button = PointerEventData.InputButton.Left, position = wheel.position };
            ExecuteEvents.Execute(scroll.gameObject, pointer, ExecuteEvents.beginDragHandler);
            float before = scroll.content.anchoredPosition.y;
            pointer.position += new Vector2(0, 80); ExecuteEvents.Execute(scroll.gameObject, pointer, ExecuteEvents.dragHandler);
            Assert.That(scroll.content.anchoredPosition.y, Is.Not.EqualTo(before));
            ExecuteEvents.Execute(scroll.gameObject, pointer, ExecuteEvents.endDragHandler);
            if (scroll.verticalScrollbar != null)
            {
                Assert.That(ExecuteEvents.GetEventHandler<IDragHandler>(scroll.verticalScrollbar.gameObject), Is.SameAs(scroll.verticalScrollbar.gameObject));
                scroll.verticalScrollbar.value = .25f;
                Assert.That(scroll.verticalNormalizedPosition, Is.EqualTo(.25f).Within(.02f));
            }
        }
    }

    [UnityTest]
    public IEnumerator PreviewTracksScaledPointerDoesNotRaycastAndRefreshRetainsTargetFeedback()
    {
        using (var f = new F()) using (var drag = new LoadoutDragHarness(f))
        {
            f.Open(); yield return null;
            float referenceScale = drag.Canvas.scaleFactor;
            drag.Canvas.GetComponent<CanvasScaler>().enabled = false;
            foreach (float scale in new[] { 1f, 1280f / 1920f })
            {
                drag.Canvas.scaleFactor = referenceScale * scale; Canvas.ForceUpdateCanvases();
                int hierarchy = f.Panel.GetComponentsInChildren<Transform>(true).Length;
                drag.Begin(0); drag.Hover(f.Slot(true, 2)); f.Refresh(); yield return null;
                var preview = (RectTransform)f.Panel.transform.Find("Drag Preview");
                Assert.That(preview.gameObject.activeSelf, Is.True);
                Assert.That(Vector2.Distance(LoadoutDragHarness.Center(preview), drag.Pointer.position), Is.LessThan(1));
                Assert.That(F.State(f.Slot(true, 2)), Is.EqualTo(
                    (int)F.Property(f.Presenter, "SelectedPosition") == 2 ? "Selected" : "Empty"));
                Assert.That(F.State(f.Slot(true, 1)), Is.EqualTo("Empty"));
                Assert.That(((Image)F.Field(f.Slot(true, 2), "background")).color,
                    Is.Not.EqualTo(((Image)F.Field(f.Slot(true, 1), "background")).color));
                var hit = drag.Raycast(drag.Pointer.position);
                Assert.That(hit, Is.Not.Null); Assert.That(hit.transform.IsChildOf(preview), Is.False);
                Assert.That(((Image)F.Field(f.Option(0), "icon")).enabled, Is.True, "Catalog original is not hidden or consumed");
                drag.Drop(f.Slot(true, 2)); yield return null;
                Assert.That(preview.gameObject.activeSelf, Is.False);
                Assert.That(f.HasValue(true, false, 2), Is.True);
                Assert.That(f.Panel.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(hierarchy));
                f.Action("Clear").onClick.Invoke();
            }
        }
    }

    [UnityTest]
    public IEnumerator ReopeningKeepsOneDragSubscriptionAndNormalIconClicksStillAssign()
    {
        using (var f = new F()) using (var drag = new LoadoutDragHarness(f))
        {
            for (int i = 0; i < 3; i++)
            {
                f.Open(); yield return null;
                int before = ((Delegate)F.Field(f.Session, "CommittedChanged"))?.GetInvocationList().Length ?? 0;
                drag.Begin(0);
                Assert.That(((Delegate)F.Field(f.Session, "CommittedChanged"))?.GetInvocationList().Length ?? 0, Is.EqualTo(before + 1));
                drag.End();
                Assert.That(((Delegate)F.Field(f.Session, "CommittedChanged"))?.GetInvocationList().Length ?? 0, Is.EqualTo(before));
                var pointer = new PointerEventData(drag.Events) { button = PointerEventData.InputButton.Left };
                ExecuteEvents.ExecuteHierarchy(drag.IconArea(0), pointer, ExecuteEvents.pointerClickHandler);
                Assert.That(f.HasValue(true, false, 0), Is.True);
                drag.Begin(1); yield return null;
                F.Call(f.Presenter, "TryCloseFromEscape");
                Assert.That(f.IsOpen, Is.False);
                Assert.That(f.Panel.transform.Find("Drag Preview").gameObject.activeSelf, Is.False);
                Assert.That(((Delegate)F.Field(f.Session, "CommittedChanged"))?.GetInvocationList().Length ?? 0, Is.EqualTo(before));
            }
        }
    }
}
#endif
