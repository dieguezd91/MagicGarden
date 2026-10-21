#if UNITY_EDITOR
using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using F = DailyLoadoutUIFixture;

public sealed class DailyLoadoutEquippedHudPlayModeTests
{
    private F f;
    [SetUp] public void Setup() { f = new F(); }
    [TearDown] public void Cleanup() { f?.Dispose(); }

    [UnityTest]
    public IEnumerator InteriorDisablesPointerTargetsAndRepeatedExitsRefreshSelectionWithoutDuplicateClicks()
    {
        f.EquipInitial();
        var plants = f.Hud(true); var spells = f.Hud(false);
        var location = f.HudLocationOwner(plants, spells);
        int events = 0;
        Action<int> callback = _ => events++;
        var selected = f.Spells.GetType().GetEvent("onSpellSlotSelected");
        selected.AddEventHandler(f.Spells, callback);
        try
        {
            for (int cycle = 0; cycle < 3; cycle++)
            {
                F.Call(location, "ShowInteriorHUD");
                Assert.That(plants.gameObject.activeInHierarchy, Is.False);
                Assert.That(spells.gameObject.activeInHierarchy, Is.False);
                foreach (var hud in new[] { plants, spells })
                {
                    foreach (var graphic in hud.GetComponentsInChildren<Graphic>(true))
                        Assert.That(graphic.isActiveAndEnabled, Is.False);
                    foreach (var button in hud.GetComponentsInChildren<Button>(true))
                        Assert.That(button.IsActive(), Is.False);
                }
                // An inactive root cannot expose graphics to the pointer raycaster.
                F.Call(f.Spells, "ClearSelection"); // Hidden views must refresh this on exit.
                F.Call(f.Days, "StartNight");
                yield return null;
                Assert.That(plants.gameObject.activeSelf, Is.False);
                Assert.That(spells.gameObject.activeSelf, Is.False);
                F.Call(location, "ShowExteriorHUD");
                Assert.That(plants.gameObject.activeInHierarchy, Is.True);
                Assert.That(spells.gameObject.activeInHierarchy, Is.True);
                var pg = (CanvasGroup)F.Field(plants, "seedSlotsCanvasGroup");
                var sg = (CanvasGroup)F.Field(spells, "spellSlotsCanvasGroup");
                Assert.That(sg.alpha, Is.GreaterThan(pg.alpha));
                Assert.That(sg.interactable && sg.blocksRaycasts, Is.True);
                Assert.That(pg.interactable || pg.blocksRaycasts, Is.False);
                int position = cycle % 2;
                var view = F.HudSlot(spells, false, position);
                Assert.That(((Image)F.Field(view, "slotBackground")).color,
                    Is.EqualTo((Color)F.Field(view, "normalColor")));
                int beforeClick = events;
                view.GetComponent<Button>().onClick.Invoke();
                Assert.That(events, Is.EqualTo(beforeClick + 1));
                Assert.That(F.Call(f.Spells, "GetSelectedSlotIndex"), Is.EqualTo(position == 0 ? 4 : 0));
                Assert.That(((Image)F.Field(view, "slotBackground")).color,
                    Is.EqualTo((Color)F.Field(view, "selectedColor")));
                var other = F.HudSlot(spells, false, 1 - position);
                Assert.That(((Image)F.Field(other, "slotBackground")).color,
                    Is.EqualTo((Color)F.Field(other, "normalColor")));
                F.Call(location, "ShowInteriorHUD");
                F.Call(f.Days, "StartDay");
                yield return null;
                Assert.That(plants.gameObject.activeSelf || spells.gameObject.activeSelf, Is.False);
                F.Call(location, "ShowExteriorHUD");
                Assert.That(pg.alpha, Is.GreaterThan(sg.alpha));
                Assert.That(pg.interactable && pg.blocksRaycasts, Is.True);
                Assert.That(sg.interactable || sg.blocksRaycasts, Is.False);
            }
        }
        finally { selected.RemoveEventHandler(f.Spells, callback); }
    }

    [UnityTest]
    public IEnumerator CommittedHudRebindsAfterDisableAndResetsWithoutAddingClickListeners()
    {
        f.EquipInitial(); var plants = f.Hud(true); var spells = f.Hud(false);
        int children = plants.GetComponentsInChildren<Transform>(true).Length;
        for (int i = 0; i < 3; i++)
        {
            plants.gameObject.SetActive(false); spells.gameObject.SetActive(false);
            yield return null;
            plants.gameObject.SetActive(true); spells.gameObject.SetActive(true);
            F.Call(plants, "RefreshAllSlots"); F.Call(spells, "RefreshAllSlots");
        }
        F.Call(f.Days, "StartNight");
        Assert.That(((CanvasGroup)F.Field(plants, "seedSlotsCanvasGroup")).interactable, Is.False);
        Assert.That(((CanvasGroup)F.Field(spells, "spellSlotsCanvasGroup")).interactable, Is.True);
        yield return new WaitForSeconds(.4f);
        Assert.That(((CanvasGroup)F.Field(plants, "seedSlotsCanvasGroup")).alpha, Is.GreaterThan(0).And.LessThan(1));
        Assert.That(((CanvasGroup)F.Field(spells, "spellSlotsCanvasGroup")).alpha, Is.EqualTo(1));
        int events = 0; Action<int> callback = _ => events++;
        var selected = f.Spells.GetType().GetEvent("onSpellSlotSelected"); selected.AddEventHandler(f.Spells, callback);
        try
        {
            ((UnityEngine.Events.UnityEvent)F.Field(F.HudSlot(spells, false, 0), "OnClick")).Invoke();
            Assert.That(F.Call(f.Spells, "GetSelectedSlotIndex"), Is.EqualTo(4));
            Assert.That(events, Is.EqualTo(1));
        }
        finally { selected.RemoveEventHandler(f.Spells, callback); }
        F.Call(f.Days, "StartDay"); F.Call(f.Session, "ResetForNewRun");
        Assert.That(((Image)F.Field(F.HudSlot(plants, true, 0), "slotIcon")).gameObject.activeSelf, Is.False);
        Assert.That(((Image)F.Field(F.HudSlot(spells, false, 0), "slotIcon")).gameObject.activeSelf, Is.False);
        Assert.That(plants.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(children));
    }

    [UnityTest]
    public IEnumerator PhaseTransitionsKeepBothCategoriesVisibleAndDisableOutgoingInputImmediately()
    {
        var plants = f.Hud(true); var spells = f.Hud(false);
        var pg = (CanvasGroup)F.Field(plants, "seedSlotsCanvasGroup");
        var sg = (CanvasGroup)F.Field(spells, "spellSlotsCanvasGroup");
        Assert.That(pg.alpha, Is.EqualTo(1)); Assert.That(sg.alpha, Is.GreaterThan(0).And.LessThan(pg.alpha));
        F.Call(f.Days, "StartNight");
        Assert.That(pg.alpha, Is.GreaterThan(0)); Assert.That(pg.interactable, Is.False);
        Assert.That(pg.blocksRaycasts, Is.False);
        for (int i = 0; i < 3; i++)
        {
            yield return null;
            Assert.That(pg.alpha, Is.GreaterThan(0));
            Assert.That(sg.alpha, Is.GreaterThan(0));
        }
        yield return new WaitForSeconds(.4f);
        Assert.That(sg.alpha, Is.GreaterThan(pg.alpha));
        F.Call(f.Days, "StartDay");
        yield return null;
        F.Call(f.Days, "StartNight"); // Interrupt both fades before returning to Day.
        F.Call(f.Days, "StartDay");
        Assert.That(sg.alpha, Is.GreaterThan(0)); Assert.That(sg.interactable, Is.False);
        Assert.That(sg.blocksRaycasts, Is.False);
        yield return new WaitForSeconds(.4f);
        Assert.That(pg.alpha, Is.EqualTo(1)); Assert.That(pg.interactable, Is.True);
        Assert.That(pg.blocksRaycasts, Is.True);
        Assert.That(sg.alpha, Is.GreaterThan(0).And.LessThan(pg.alpha));
    }

    [UnityTest]
    public IEnumerator ProductionSpellButtonDispatchesExactlyOnceAfterRefreshAndReenable()
    {
        f.EquipInitial();
        var hud = f.Hud(false);
        F.Call(f.Days, "StartNight");
        for (int i = 0; i < 3; i++)
        {
            hud.gameObject.SetActive(false);
            yield return null;
            hud.gameObject.SetActive(true);
            F.Call(hud, "RefreshAllSlots");
        }
        yield return null; // Use the production prefab's native Awake/OnEnable lifecycle.
        var view = F.HudSlot(hud, false, 0);
        var button = view.GetComponent<Button>();
        Assert.That(button, Is.Not.Null);
        int events = 0;
        Action<int> callback = _ => events++;
        var selected = f.Spells.GetType().GetEvent("onSpellSlotSelected");
        selected.AddEventHandler(f.Spells, callback);
        try
        {
            button.onClick.Invoke(); // Do not bypass the Button -> ImprovedUIButton bridge.
            Assert.That(F.Call(f.Spells, "GetSelectedSlotIndex"), Is.EqualTo(4));
            Assert.That(events, Is.EqualTo(1));
        }
        finally { selected.RemoveEventHandler(f.Spells, callback); }
    }

    [UnityTest]
    public IEnumerator LateSessionOwnerIsResolvedThroughExistingHandleUpdateAndReplacesDisposedOwner()
    {
        var plants = f.Hud(true); var spells = f.Hud(false);
        ((IDisposable)f.Session).Dispose();
        var replacement = new GameObject("Stage4D replacement progression owner"); replacement.SetActive(false);
        try
        {
            var experience = replacement.AddComponent(F.Production("PlayerExperienceSystem"));
            F.Set(experience, "progressionData", f.Progression);
            var unlocks = replacement.AddComponent(F.Production("PlayerContentUnlockSystem"));
            replacement.SetActive(true);
            Assert.That(F.Property(unlocks, "Loadout"), Is.Null);
            F.Call(plants, "HandleUpdate"); F.Call(spells, "HandleUpdate");
            yield return null; // Real owner.Start creates the session.
            F.Call(plants, "HandleUpdate"); F.Call(spells, "HandleUpdate");
            var session = F.Property(unlocks, "Loadout");
            Assert.That(F.Property(session, "IsReady"), Is.True);
            Assert.That(F.Call(session, "TrySetDraftPlant", 1, F.Seed(7)), Is.True);
            Assert.That(F.Call(session, "TryConfirm"), Is.True);
            Assert.That(((Image)F.Field(F.HudSlot(plants, true, 1), "slotIcon")).gameObject.activeSelf, Is.True);
            Assert.That(((Image)F.Field(F.HudSlot(plants, true, 0), "slotIcon")).gameObject.activeSelf, Is.False);
        }
        finally { UnityEngine.Object.DestroyImmediate(replacement); }
    }
}
#endif
