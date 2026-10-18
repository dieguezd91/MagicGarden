using System;
using NUnit.Framework;
using UnityEngine;
using F = DailyLoadoutUIFixture;

public sealed class DailyLoadoutWorldInteractionTests
{
    private F f;
    private GameObject manager, tree, player, prompt;
    private Component altar, trigger;
    private float timeScale;

    [SetUp]
    public void Setup()
    {
        timeScale = Time.timeScale; Time.timeScale = 1;
        f = new F();
        manager = new GameObject("Stage4C UI owner"); manager.SetActive(false);
        var ui = manager.AddComponent(F.Production("UIManager"));
        F.Set(ui, "dailyLoadout", f.Presenter); F.Set(ui, "uiFlowController", f.Modals);
        F.Production("UIManager").GetProperty("Instance").GetSetMethod(true).Invoke(null, new object[] { ui });
        tree = new GameObject("Stage4C existing tree"); tree.SetActive(false);
        altar = tree.AddComponent(F.Production("RestorationAltar"));
        trigger = tree.AddComponent(F.Production("InteractionTrigger"));
        prompt = new GameObject("F prompt"); prompt.transform.SetParent(tree.transform);
        F.Set(trigger, "interactionPromptCanvas", prompt); F.Set(trigger, "hidePromptDuringInteraction", true);
        tree.SetActive(true);
        F.Call(trigger, "Awake"); F.Call(trigger, "OnEnable");
        player = new GameObject("Player overlap"); player.tag = "Player";
        var collider = player.AddComponent<BoxCollider2D>();
        F.Call(trigger, "OnTriggerEnter2D", collider);
    }

    [TearDown]
    public void Cleanup()
    {
        if (trigger != null) F.Call(trigger, "OnDisable");
        if (altar != null) F.Call(altar, "OnDisable");
        UnityEngine.Object.DestroyImmediate(tree); UnityEngine.Object.DestroyImmediate(player);
        UnityEngine.Object.DestroyImmediate(manager); f?.Dispose(); Time.timeScale = timeScale;
        F.Production("InputConsumptionManager").GetMethod("Reset").Invoke(null, null);
    }

    private bool Interact() => (bool)F.Call(trigger, "TryInteract");
    private void AssertIdle(bool near = true)
    {
        Assert.That(f.IsOpen, Is.False);
        Assert.That(F.Property(f.Modals, "HasOpenModal"), Is.False);
        Assert.That(F.Field(trigger, "isInteracting"), Is.False);
        Assert.That(prompt.activeSelf, Is.EqualTo(near));
    }

    [Test]
    public void DaytimeTreeOpensRealCatalogOnceAndCancelRestoresPrompt()
    {
        int children = f.Panel.transform.childCount;
        Assert.That(Interact(), Is.True); Assert.That(f.IsOpen, Is.True);
        Assert.That(F.Property(f.Modals, "CurrentModal").ToString(), Is.EqualTo("DailyLoadout"));
        Assert.That(f.HasValue(true, false, 0), Is.False);
        Assert.That(F.Name(f.Option(0)), Is.EqualTo("First authored plant"));
        Assert.That(F.Name(f.Option(1)), Is.EqualTo("Second authored plant"));
        Assert.That(F.Field(trigger, "isInteracting"), Is.True); Assert.That(prompt.activeSelf, Is.False);
        Assert.That(Interact(), Is.False); Assert.That(f.Panel.transform.childCount, Is.EqualTo(children));
        f.Action("Cancel").onClick.Invoke(); AssertIdle();
        Assert.That(f.Confirmed, Is.False);
    }

    [Test]
    public void ConfirmThenCloseAllowsReadOnlyReopeningWithoutSpendingAgain()
    {
        Assert.That(Interact(), Is.True);
        f.Select(true, 1); f.Assign(1); f.Action("Confirm").onClick.Invoke();
        Assert.That(f.Confirmed, Is.True); Assert.That(f.HasValue(true, true, 0), Is.False);
        Assert.That(f.HasValue(true, true, 1), Is.True);
        f.Action("Cancel").onClick.Invoke(); AssertIdle();
        Assert.That(Interact(), Is.True); Assert.That(f.Action("Confirm").interactable, Is.False);
        Assert.That(f.Action("Clear").interactable, Is.False); Assert.That(f.Status, Does.Contain("consumed"));
        f.Action("Cancel").onClick.Invoke(); AssertIdle();
    }

    [TestCase("Night")] [TestCase("GameOver")] [TestCase("OnRitual")]
    public void NonDayInteractionIsRejectedWithoutTakingModalOrSpendingAllowance(string phase)
    {
        F.Call(f.Flow, "SetPhase", Enum.Parse(F.Production("GamePhase"), phase));
        Assert.That(Interact(), Is.False); Assert.That(f.IsOpen, Is.False);
        Assert.That(F.Property(f.Modals, "HasOpenModal"), Is.False);
        Assert.That(F.Field(trigger, "isInteracting"), Is.False); Assert.That(f.Confirmed, Is.False);
    }

    [Test]
    public void PauseAndAnotherModalRejectWithoutStealingOwnership()
    {
        Time.timeScale = 0; Assert.That(Interact(), Is.False); Time.timeScale = 1;
        var modal = Enum.Parse(F.Production("UIModal"), "Crafting");
        Assert.That(F.Call(f.Modals, "Open", modal), Is.True);
        Assert.That(Interact(), Is.False); Assert.That(F.Property(f.Modals, "CurrentModal"), Is.EqualTo(modal));
        Assert.That(F.Field(trigger, "isInteracting"), Is.False); Assert.That(f.Confirmed, Is.False);
    }

    [Test]
    public void EscapeIsConsumedAndCompletesOnlyOnce()
    {
        Assert.That(Interact(), Is.True); int closed = 0;
        Action callback = () => closed++;
        var closedEvent = f.Presenter.GetType().GetEvent("Closed"); closedEvent.AddEventHandler(f.Presenter, callback);
        try
        {
            Assert.That(F.Call(f.Presenter, "TryCloseFromEscape"), Is.True);
            Assert.That(F.Call(f.Presenter, "TryCloseFromEscape"), Is.False);
            F.Call(f.Presenter, "Close"); Assert.That(closed, Is.EqualTo(1)); AssertIdle();
            Assert.That(F.Production("InputConsumptionManager").GetProperty("IsEscapeConsumed").GetValue(null), Is.True);
        }
        finally { closedEvent.RemoveEventHandler(f.Presenter, callback); }
    }

    [Test]
    public void NightAndRangeExitNeverLeaveAStalePromptOrCallback()
    {
        Assert.That(Interact(), Is.True);
        F.Call(trigger, "OnTriggerExit2D", player.GetComponent<BoxCollider2D>());
        F.Call(f.Days, "StartNight"); AssertIdle(false);
        F.Call(f.Days, "StartDay"); F.Call(f.Presenter, "Close"); AssertIdle(false);
        F.Call(trigger, "OnTriggerEnter2D", player.GetComponent<BoxCollider2D>());
        Assert.That(prompt.activeSelf, Is.True); Assert.That(Interact(), Is.True);
        F.Call(trigger, "OnDisable"); Assert.That(f.IsOpen, Is.False);
        Assert.That(F.Property(f.Modals, "HasOpenModal"), Is.False); Assert.That(prompt.activeSelf, Is.False);
    }

    [Test]
    public void RestorationNotificationsCannotCompleteTheLoadoutInteraction()
    {
        Assert.That(Interact(), Is.True);
        F.Production("UIEvents").GetMethod("TriggerRestorationAltarUIClosed").Invoke(null, null);
        F.Production("UIEvents").GetMethod("TriggerCraftingUIClosed").Invoke(null, null);
        Assert.That(F.Field(trigger, "isInteracting"), Is.True); Assert.That(f.IsOpen, Is.True);
        f.Action("Cancel").onClick.Invoke(); AssertIdle();
    }

    [Test]
    public void LegacyCraftingInteractionStillHidesPromptAndUsesItsExistingCloseNotification()
    {
        var legacy = new GameObject("Existing crafting trigger"); legacy.SetActive(false);
        legacy.AddComponent(F.Production("CraftingCauldron"));
        var legacyTrigger = legacy.AddComponent(F.Production("InteractionTrigger"));
        var legacyPrompt = new GameObject("Existing crafting F prompt"); legacyPrompt.transform.SetParent(legacy.transform);
        F.Set(legacyTrigger, "interactionPromptCanvas", legacyPrompt); F.Set(legacyTrigger, "hidePromptDuringInteraction", true);
        var toggle = F.Production("UIEvents").GetEvent("OnCraftingUIToggleRequested");
        int opened = 0; Action callback = () => opened++;
        toggle.AddEventHandler(null, callback);
        try
        {
            legacy.SetActive(true); F.Call(legacyTrigger, "Awake"); F.Call(legacyTrigger, "OnEnable");
            Assert.That(F.Field(legacyTrigger, "lifecycle"), Is.Null);
            F.Call(legacyTrigger, "OnTriggerEnter2D", player.GetComponent<BoxCollider2D>());
            Assert.That(legacyPrompt.activeSelf, Is.True);
            Assert.That(F.Call(legacyTrigger, "TryInteract"), Is.True);
            Assert.That(opened, Is.EqualTo(1)); Assert.That(legacyPrompt.activeSelf, Is.False);
            Assert.That(F.Call(legacyTrigger, "TryInteract"), Is.False); Assert.That(opened, Is.EqualTo(1));
            F.Production("UIEvents").GetMethod("TriggerCraftingUIClosed").Invoke(null, null);
            Assert.That(F.Field(legacyTrigger, "isInteracting"), Is.False); Assert.That(legacyPrompt.activeSelf, Is.True);
            F.Call(legacyTrigger, "OnTriggerExit2D", player.GetComponent<BoxCollider2D>());
            Assert.That(legacyPrompt.activeSelf, Is.False); Assert.That(F.Call(legacyTrigger, "TryInteract"), Is.False);
        }
        finally
        {
            toggle.RemoveEventHandler(null, callback); F.Call(legacyTrigger, "OnDisable"); UnityEngine.Object.DestroyImmediate(legacy);
        }
    }

    [Test]
    public void RestorationStillChargesItsOriginalCostsHealsAndLimitsOneUsePerDay()
    {
        var inventoryObject = new GameObject("Isolated restoration inventory"); inventoryObject.SetActive(false);
        var inventory = inventoryObject.AddComponent(F.Production("InventoryManager"));
        var singleton = inventory.GetType().GetField("_instance", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        singleton.SetValue(null, inventory);
        var houseObject = new GameObject("Isolated restoration house"); houseObject.SetActive(false); houseObject.tag = "Home";
        var house = houseObject.AddComponent(F.Production("HouseLifeController"));
        F.Set(house, "maxHealth", 100f); F.Set(house, "currentHealth", 50f); houseObject.SetActive(true);
        var restoration = tree.AddComponent(F.Production("HouseRestorationSystem"));
        var material = Enum.GetValues(F.Production("MaterialType")).GetValue(1);
        var optionType = F.Production("HouseRestorationSystem+RestorationOption");
        var option = Activator.CreateInstance(optionType);
        optionType.GetField("goldCost").SetValue(option, 10);
        optionType.GetField("materialRequired").SetValue(option, material);
        optionType.GetField("restorePercentage").SetValue(option, 30);
        var options = Array.CreateInstance(optionType, 1); options.SetValue(option, 0); F.Set(restoration, "options", options);
        try
        {
            F.Call(inventory, "SetGold", 20);
            // Missing resources and invalid indices do not debit anything or consume the daily limit.
            Assert.That(F.Call(restoration, "TryRestore", 0), Is.False);
            Assert.That(F.Call(restoration, "TryRestore", -1), Is.False);
            Assert.That(F.Call(restoration, "TryRestore", 1), Is.False);
            Assert.That(F.Call(inventory, "GetGold"), Is.EqualTo(20));
            Assert.That(F.Property(house, "CurrentHealth"), Is.EqualTo(50f));
            Assert.That(F.Call(restoration, "CanRestore"), Is.True);
            F.Call(inventory, "AddMaterial", material, 3);
            Assert.That(F.Call(restoration, "TryRestore", 0), Is.True);
            Assert.That(F.Call(inventory, "GetGold"), Is.EqualTo(10));
            Assert.That(F.Call(inventory, "GetMaterialAmount", material), Is.EqualTo(2));
            Assert.That(F.Property(house, "CurrentHealth"), Is.EqualTo(80f));
            Assert.That(F.Call(restoration, "TryRestore", 0), Is.False);
            Assert.That(F.Call(inventory, "GetGold"), Is.EqualTo(10));
            Assert.That(F.Call(inventory, "GetMaterialAmount", material), Is.EqualTo(2));
            Assert.That(F.Property(house, "CurrentHealth"), Is.EqualTo(80f));
            F.Call(restoration, "ResetForNewDay"); Assert.That(F.Call(restoration, "TryRestore", 0), Is.True);
            Assert.That(F.Property(house, "CurrentHealth"), Is.EqualTo(100f));
            Assert.That(F.Call(inventory, "GetGold"), Is.EqualTo(0));
            Assert.That(F.Call(inventory, "GetMaterialAmount", material), Is.EqualTo(1));
        }
        finally
        {
            singleton.SetValue(null, null); UnityEngine.Object.DestroyImmediate(inventoryObject);
            UnityEngine.Object.DestroyImmediate(houseObject);
        }
    }

    [Test]
    public void LoadoutInteractionEmitsNoRestorationEventsAndOneSuccessfulOpenEvent()
    {
        int restoration = 0, opened = 0; Action old = () => restoration++, current = () => opened++;
        var events = F.Production("TutorialEvents"); var uiEvents = F.Production("UIEvents");
        var oldOpen = events.GetEvent("OnRestorationOpened"); var oldClose = events.GetEvent("OnRestorationClosed");
        var legacyToggle = uiEvents.GetEvent("OnRestorationAltarUIToggleRequested");
        var loadout = events.GetEvent("OnDailyLoadoutOpened");
        oldOpen.AddEventHandler(null, old); oldClose.AddEventHandler(null, old);
        legacyToggle.AddEventHandler(null, old); loadout.AddEventHandler(null, current);
        try
        {
            Assert.That(Interact(), Is.True); Assert.That(Interact(), Is.False);
            f.Action("Cancel").onClick.Invoke(); AssertIdle();
            Assert.That(restoration, Is.Zero); Assert.That(opened, Is.EqualTo(1));
        }
        finally
        {
            oldOpen.RemoveEventHandler(null, old); oldClose.RemoveEventHandler(null, old);
            legacyToggle.RemoveEventHandler(null, old); loadout.RemoveEventHandler(null, current);
        }
    }
}
