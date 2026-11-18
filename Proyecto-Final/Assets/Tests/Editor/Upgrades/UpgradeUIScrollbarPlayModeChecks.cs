using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MagicGarden;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using static UpgradeUIArtTests;
using static UpgradeUIScrollbarTests;

public class UpgradeUIScrollbarPlayModeChecks
{
    private static Component Find(string name) => (Component)UnityEngine.Object.FindObjectOfType(TypeOf(name));
    [UnityTearDown] public IEnumerator LeavePlayModeAfterFailure()
    {
        if (EditorApplication.isPlaying) yield return new ExitPlayMode();
    }
    private static IEnumerator Frames(int count = 3) { for (int i = 0; i < count; i++) yield return null; }
    private static void ValidHandle(object scroll)
    {
        var bar = (Component)Property(scroll, "verticalScrollbar"); var content = (RectTransform)Property(scroll, "content");
        var viewport = (RectTransform)Property(scroll, "viewport"); var handle = (RectTransform)Property(bar, "handleRect");
        Assert.That(bar.gameObject.activeInHierarchy, Is.True);
        Assert.That((float)Property(bar, "size"), Is.EqualTo(viewport.rect.height / content.rect.height).Within(.002));
        Assert.That(handle.rect.width, Is.EqualTo(16).Within(.01)); Assert.That(handle.rect.height, Is.GreaterThan(40));
        Assert.That(handle.rect.height, Is.LessThan(((RectTransform)handle.parent).rect.height - 40));
        Assert.That(viewport.GetComponent(TypeOf("UnityEngine.UI.RectMask2D", "UnityEngine.UI")), Is.Not.Null);
        Assert.That(content.parent, Is.SameAs(viewport));
        // Fully outside rows must be culled; a fully visible row must not be incorrectly clipped.
        int culled = 0, visible = 0;
        foreach (RectTransform row in content)
        {
            if (!row.gameObject.activeSelf) continue;
            var corners = new Vector3[4]; row.GetWorldCorners(corners);
            float bottom = viewport.InverseTransformPoint(corners[0]).y, top = viewport.InverseTransformPoint(corners[1]).y;
            var renderer = row.GetComponent<CanvasRenderer>();
            if (top < viewport.rect.yMin || bottom > viewport.rect.yMax) { Assert.That(renderer.cull, Is.True); culled++; }
            if (bottom >= viewport.rect.yMin && top <= viewport.rect.yMax) { Assert.That(renderer.cull, Is.False); visible++; }
        }
        Assert.That(culled, Is.GreaterThan(0)); Assert.That(visible, Is.GreaterThan(0));
    }
    [UnityTest] public IEnumerator RealGameOverflowNativeInputPurchaseMaterialRefreshAndReopen()
    {
        yield return new EnterPlayMode();
        yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Game.unity", new LoadSceneParameters(LoadSceneMode.Single));
        for (int i = 0; i < 15; i++) yield return null;
        // Allocate captured fixture data only after EnterPlayMode's domain reload.
        yield return GameScenario();
        yield return new ExitPlayMode();
    }
    private static IEnumerator GameScenario()
    {
        var tutorial = Find("TutorialManager"); if (tutorial != null) Call(tutorial, "SkipTutorial");
        var flow = Find("GameFlowController"); Call(flow, "SetPhase", Enum.Parse(TypeOf("GamePhase"), "Day"));
        var modal = Property(Find("UIManager"), "Flow"); var current = Property(modal, "CurrentModal");
        if (current.ToString() != "None") Call(modal, "Close", current);
        var runtime = Find("UpgradeRuntime"); var presenter = Find("UpgradePanel"); var inventory = Find("InventoryManager");
        Assert.That(presenter, Is.Not.Null, "Game scene must contain the authored presenter");
        Assert.That(runtime, Is.Not.Null, "Game scene must install its existing UpgradeRuntime");
        Assert.That(Property(presenter, "IsReady"), Is.True);
        Assert.That(((Behaviour)runtime).enabled, Is.True);
        Call(runtime, "RefreshTargets");
        var balance = (Balance)Property(runtime, "Balance");
        Assert.That(balance, Is.Not.Null);
        var original = balance.specials;
        var targets = (IList<Target>)Property(runtime, "Targets");
        var template = original.First(d => targets.Any(t => t.Capability == d.capability));
        // Test-only cloned definitions produce overflow against real owned targets and real runtime purchases.
        // No shipped balance, unlock, transaction or production eligibility path is changed.
        var fixture = Enumerable.Range(0, 8).Select(i => {
            var d = JsonUtility.FromJson<SpecialDefinition>(JsonUtility.ToJson(template));
            d.id = "scroll_fixture_" + i; d.displayName = "Ember blessing " + (i + 1); d.maxStacks = 3;
            d.prerequisites = i == 7 ? new[] { "fixture_unowned_prerequisite" } : new string[0];
            return d;
        }).ToArray();
        balance.specials = fixture;
        Call(runtime, "ToggleShop"); yield return Frames();
        Assert.That(Property(presenter, "IsShopOpen"), Is.True);
        var shop = (Component)Field(presenter, "shopView"); var scroll = (Component)Field(shop, "scroll");
        var bar = (Component)Property(scroll, "verticalScrollbar"); var close = (Component)Field(shop, "closeButton");
        var rows = (IList)Field(shop, "rows"); Assert.That(rows.Count, Is.GreaterThanOrEqualTo(8)); ValidHandle(scroll);
        var state = (RunState)Property(runtime, "State"); var definitions = (IList)Field(presenter, "shopDefinitions");
        var targetIds = (IList)Field(presenter, "shopTargets"); string target = (string)targetIds[3]; var purchase = (SpecialDefinition)definitions[3];
        Assert.That(CostPlanner.Aggregate(purchase.costs, out var costs), Is.True);
        foreach (var cost in costs) Drain(inventory, cost.Key);
        yield return Frames();
        Click((Component)Field(rows[3], "purchaseButton")); Assert.That(state.SpecialStacks(purchase.id, target), Is.Zero);
        foreach (var cost in costs) Grant(inventory, cost.Key, cost.Value * 10);
        yield return Frames(); Assert.That(Property(Field(rows[3], "purchaseButton"), "interactable"), Is.True);
        Assert.That(Property(Field(rows[7], "purchaseButton"), "interactable"), Is.False);
        var wheel = Pointer(RectTransformUtility.WorldToScreenPoint(null, scroll.transform.position)); Set(wheel, "scrollDelta", new Vector2(0, -4));
        float beforeWheel = Offset(scroll); Assert.That(Dispatch(scroll, "scroll", wheel), Is.True); yield return Frames();
        Assert.That(Offset(scroll), Is.GreaterThan(beforeWheel + 100));
        var handle = (RectTransform)Property(bar, "handleRect");
        var pointer = Pointer(RectTransformUtility.WorldToScreenPoint(null, handle.TransformPoint(handle.rect.center)));
        Set(pointer, "pointerDrag", bar.gameObject);
        Assert.That(Dispatch(bar, "initializePotentialDrag", pointer), Is.True);
        Assert.That(Dispatch(bar, "beginDrag", pointer), Is.True);
        float beforeDrag = Offset(scroll); float beforeValue = (float)Property(bar, "value");
        Set(pointer, "position", (Vector2)Property(pointer, "position") + new Vector2(0, -90));
        Assert.That(Dispatch(bar, "drag", pointer), Is.True);
        // UGUI Scrollbar has no IEndDragHandler; release is its native pointer-up contract.
        Assert.That(Dispatch(bar, "endDrag", pointer), Is.False); Assert.That(Dispatch(bar, "pointerUp", pointer), Is.True);
        yield return Frames(); Assert.That(Offset(scroll), Is.GreaterThan(beforeDrag)); Assert.That((float)Property(bar, "value"), Is.LessThan(beforeValue));
        Select(close); Move(close, "Right"); Assert.That(Selected, Is.SameAs(bar.gameObject));
        float arrowValue = (float)Property(bar, "value"); float arrowOffset = Offset(scroll);
        Move(bar, "Down"); Assert.That((float)Property(bar, "value"), Is.LessThan(arrowValue)); Assert.That(Offset(scroll), Is.GreaterThan(arrowOffset));
        Move(bar, "Up"); Assert.That((float)Property(bar, "value"), Is.EqualTo(arrowValue).Within(.001));
        Move(bar, "Left"); Assert.That(Selected, Is.SameAs(close.gameObject));
        Move(close, "Up"); Assert.That(Selected, Is.SameAs(((Component)rows[6]).gameObject), "Navigation skips disabled bottom row");
        yield return Frames(); ValidHandle(scroll);
        Offset(scroll, 600.25f); Select((Component)Field(rows[3], "purchaseButton")); yield return Frames();
        float purchaseOffset = Offset(scroll); var resources = costs.ToDictionary(c => c.Key, c => Amount(inventory, c.Key));
        Set(scroll, "velocity", new Vector2(0, 800)); Click((Component)Field(rows[3], "purchaseButton"));
        Assert.That(state.SpecialStacks(purchase.id, target), Is.EqualTo(1)); Assert.That(Offset(scroll), Is.EqualTo(purchaseOffset).Within(.01));
        foreach (var cost in costs) Assert.That(Amount(inventory, cost.Key), Is.EqualTo(resources[cost.Key] - cost.Value));
        yield return Frames(6); Assert.That(Offset(scroll), Is.EqualTo(purchaseOffset).Within(.05)); Assert.That(Property(scroll, "velocity"), Is.EqualTo(Vector2.zero));
        Assert.That(Property(Field(rows[3], "stacksLabel"), "text"), Is.EqualTo("1/3"));
        var resource = costs.First().Key;
        // Multiple real notifications queue one presenter refresh; labels update after its normal Update.
        string oldCosts = (string)Property(Field(rows[3], "costsLabel"), "text");
        Grant(inventory, resource, 1); Grant(inventory, resource, 1);
        Assert.That(Field(presenter, "shopRefresh"), Is.True);
        yield return Frames(6); Assert.That(Field(presenter, "shopRefresh"), Is.False);
        Assert.That(Property(Field(rows[3], "costsLabel"), "text"), Is.Not.EqualTo(oldCosts));
        Assert.That(Offset(scroll), Is.EqualTo(purchaseOffset).Within(.05)); ValidHandle(scroll);
        var canvas = presenter.GetComponent<Canvas>();
        yield return UpgradeUIArtPlayModeChecks.ButtonStates(bar);
        foreach (var size in new[] { new Vector2Int(1920, 1080), new Vector2Int(1280, 720) })
            yield return UpgradeUIArtPlayModeChecks.Capture(canvas, shop.gameObject, "scrollbar-overflow-scrolled", size.x, size.y);
        // Content resize uses refresh, not open: preserve valid pixels, clamp only when necessary.
        balance.specials = fixture.Take(2).ToArray(); Grant(inventory, resource, 1); yield return Frames(6);
        Assert.That(Offset(scroll), Is.Zero.Within(.05)); Assert.That(bar.gameObject.activeSelf, Is.False);
        Assert.That(Selected, Is.SameAs(close.gameObject), "Auto-hidden scrollbar focus returns to Close");
        foreach (var size in new[] { new Vector2Int(1920, 1080), new Vector2Int(1280, 720) })
            yield return UpgradeUIArtPlayModeChecks.Capture(canvas, shop.gameObject, "scrollbar-short-autohidden", size.x, size.y);
        balance.specials = new SpecialDefinition[0]; Grant(inventory, resource, 1); yield return Frames();
        Assert.That(bar.gameObject.activeSelf, Is.False); Assert.That(((Component)Field(shop, "emptyLabel")).gameObject.activeSelf, Is.True);
        balance.specials = fixture; Grant(inventory, resource, 1); yield return Frames(); ValidHandle(scroll);
        Offset(scroll, 333); Set(scroll, "velocity", new Vector2(0, 100)); Click(close); yield return Frames();
        Assert.That(Property(presenter, "IsShopOpen"), Is.False); Call(runtime, "ToggleShop"); yield return Frames(6);
        Assert.That(Offset(scroll), Is.Zero.Within(.05)); Assert.That(Property(scroll, "velocity"), Is.EqualTo(Vector2.zero));
        Offset(scroll, 222); Call(flow, "SetPhase", Enum.Parse(TypeOf("GamePhase"), "Night")); yield return Frames();
        Assert.That(Property(presenter, "IsShopOpen"), Is.False);
        Call(flow, "SetPhase", Enum.Parse(TypeOf("GamePhase"), "Day")); Call(runtime, "ToggleShop"); yield return Frames();
        Assert.That(Offset(scroll), Is.Zero.Within(.05));
        balance.specials = original; Call(presenter, "CloseShop");
        Directory.CreateDirectory(UpgradeUICompactLayoutTests.Output);
        File.WriteAllText(UpgradeUICompactLayoutTests.Output + "scrollbar-actions.txt",
            "Real Game EnterPlayMode: wheel, screen-space native thumb begin/drag/release, native arrows, Close navigation, disabled row skipping, mask culling, valid purchase/resource spending, coalesced material updates, pixel preservation/no velocity, short/empty AutoHide, reopen and phase-cancel reset passed.\n" +
            "Scrollbar does not implement IEndDragHandler; endDrag dispatched without a receiver and native pointerUp releases the handle.\n" +
            "Wheel offset " + beforeWheel + " -> >100; drag " + beforeDrag + " -> larger; purchase/material preserved " + purchaseOffset + " pixels.\n" +
            "Screenshots are native graphics camera captures, not physical mouse/keyboard automation.\n");
        Debug.Log("UI_SCROLLBAR_RUNTIME: overflow/wheel/drag/arrows/focus/mask/purchase/material/pixel-offset/autohide/reopen/phase assertions passed.");
    }
}
