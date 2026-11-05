#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using F = DailyLoadoutUIFixture;

public sealed class DailyLoadoutWorldPlayModeTests
{
    private F f;
    private GameObject manager, tree, owner, prompt, player;
    private float timeScale;
    private GameObject tutorialRoot, tutorialUiRoot;
    private Component modalTutorial, modalTutorialUi;
    private ScriptableObject modalStep;
    private object previousTutorial, previousLevel;

    [SetUp]
    public void Setup() { timeScale = Time.timeScale; Time.timeScale = 1; f = new F(); }

    [TearDown]
    public void Cleanup()
    {
        if (tutorialRoot != null)
        {
            Object.DestroyImmediate(tutorialRoot); Object.DestroyImmediate(tutorialUiRoot); Object.DestroyImmediate(modalStep);
            F.Production("TutorialManager").GetProperty("Instance").GetSetMethod(true).Invoke(null, new[] { previousTutorial });
            F.Production("LevelManager").GetProperty("Instance").GetSetMethod(true).Invoke(null, new[] { previousLevel });
        }
        Object.DestroyImmediate(tree); Object.DestroyImmediate(manager); Object.DestroyImmediate(owner);
        Object.DestroyImmediate(player); f?.Dispose(); Time.timeScale = timeScale;
        F.Production("InputConsumptionManager").GetMethod("Reset").Invoke(null, null);
    }

    [UnityTest]
    public IEnumerator NaturalStartDiscoversSessionAfterRejectedEarlyFAndUsesTheSamePanelAcrossReopens()
    {
        // Unlike the Stage4B fixture, this owner receives real Awake/OnEnable/Start dispatch.
        // No session/owner/progression/flow is injected into this presenter.
        F.Call(f.Presenter, "Initialize", null, null, null, null, null);
        f.Modals.gameObject.SetActive(true);
        manager = new GameObject("Stage4C gameplay UI composition"); manager.SetActive(false);
        var ui = manager.AddComponent(F.Production("UIManager"));
        F.Set(ui, "uiFlowController", f.Modals); F.Set(ui, "dailyLoadout", f.Presenter);
        manager.SetActive(true);

        tree = new GameObject("Stage4C Sacred Tree"); tree.SetActive(false);
        tree.AddComponent(F.Production("RestorationAltar"));
        var trigger = tree.AddComponent(F.Production("InteractionTrigger"));
        prompt = new GameObject("Existing F prompt"); prompt.transform.SetParent(tree.transform);
        F.Set(trigger, "interactionPromptCanvas", prompt); F.Set(trigger, "hidePromptDuringInteraction", true);
        tree.SetActive(true);
        player = new GameObject("Player overlap"); player.tag = "Player";
        var collision = player.AddComponent<BoxCollider2D>();
        tree.SendMessage("OnTriggerEnter2D", collision);

        owner = new GameObject("Naturally initialized progression owner"); owner.SetActive(false);
        var experience = owner.AddComponent(F.Production("PlayerExperienceSystem"));
        F.Set(experience, "progressionData", f.Progression);
        var unlocks = owner.AddComponent(F.Production("PlayerContentUnlockSystem"));
        owner.SetActive(true); // Loadout does not exist until Start.
        Assert.That(F.Property(unlocks, "Loadout"), Is.Null);
        Assert.That(F.Call(trigger, "TryInteract"), Is.False);
        Assert.That(F.Field(trigger, "isInteracting"), Is.False);
        Assert.That(F.Property(f.Modals, "HasOpenModal"), Is.False);
        yield return null;
        tree.SendMessage("OnTriggerEnter2D", collision); // Start hides the authored prompt initially.
        Assert.That(F.Property(F.Property(unlocks, "Loadout"), "IsReady"), Is.True);
        Assert.That(F.Call(trigger, "TryInteract"), Is.True);
        Assert.That(f.IsOpen, Is.True); Assert.That(F.Field(f.Presenter, "activeSession"), Is.SameAs(F.Property(unlocks, "Loadout")));
        Assert.That(F.Property(F.Call(F.Property(unlocks, "Loadout"), "GetCommittedPlant", 0), "HasValue"), Is.False);
        Assert.That(F.Name(f.Option(0)), Is.EqualTo("First authored plant"));
        Assert.That(F.Name(f.Option(1)), Is.EqualTo("Second authored plant"));
        Assert.That(prompt.activeSelf, Is.False);
        Assert.That(F.Call(trigger, "TryInteract"), Is.False);
        f.Select(true, 0); f.Action("Clear").onClick.Invoke(); f.Action("Confirm").onClick.Invoke();
        Assert.That(F.Property(F.Property(unlocks, "Loadout"), "HasConfirmedToday"), Is.True);
        f.Action("Cancel").onClick.Invoke(); Assert.That(prompt.activeSelf, Is.True);
        Assert.That(F.Call(trigger, "TryInteract"), Is.True); Assert.That(f.Action("Confirm").interactable, Is.False);
        Assert.That(F.Call(f.Presenter, "TryCloseFromEscape"), Is.True);
        Assert.That(F.Call(f.Presenter, "TryCloseFromEscape"), Is.False);
        Assert.That(prompt.activeSelf, Is.True);
        Assert.That(F.Call(trigger, "TryInteract"), Is.True);
        F.Call(f.Days, "StartNight"); Assert.That(f.IsOpen, Is.False);
        Assert.That(F.Property(f.Modals, "HasOpenModal"), Is.False);
        Assert.That(F.Field(trigger, "isInteracting"), Is.False); Assert.That(prompt.activeSelf, Is.False);
        yield return null;
        F.Call(f.Days, "StartDay"); yield return null;
        Assert.That(prompt.activeSelf, Is.True);
        Assert.That(F.Call(trigger, "TryInteract"), Is.True);
        tree.SetActive(false); // Native disable closes the initiator's panel and releases subscriptions.
        Assert.That(f.IsOpen, Is.False); Assert.That(F.Property(f.Modals, "HasOpenModal"), Is.False);
    }

    [UnityTest]
    public IEnumerator ProductionTutorialUiDefersNextStepUntilLoadoutClosesAndResumesWithoutLegacyEvents()
    {
        manager = new GameObject("Stage4C tutorial UI owner"); manager.SetActive(false);
        var ui = manager.AddComponent(F.Production("UIManager"));
        F.Set(ui, "uiFlowController", f.Modals); F.Set(ui, "dailyLoadout", f.Presenter); manager.SetActive(true);
        tree = new GameObject("Stage4C tutorial tree"); tree.SetActive(false);
        var altar = tree.AddComponent(F.Production("RestorationAltar")); tree.SetActive(true);
        var tutorialObject = new GameObject("Production tutorial manager"); tutorialObject.SetActive(false);
        var tutorial = tutorialObject.AddComponent(F.Production("TutorialManager"));
        var levelObject = new GameObject("Isolated LevelManager identity"); levelObject.SetActive(false);
        var level = levelObject.AddComponent(F.Production("LevelManager"));
        var levelProperty = level.GetType().GetProperty("Instance"); var previousLevel = levelProperty.GetValue(null);
        levelProperty.GetSetMethod(true).Invoke(null, new object[] { level });
        var tutorialProperty = tutorial.GetType().GetProperty("Instance"); var previousTutorial = tutorialProperty.GetValue(null);
        tutorialProperty.GetSetMethod(true).Invoke(null, new object[] { null });
        var uiObject = new GameObject("Production tutorial UI"); uiObject.SetActive(false);
        var tutorialUi = uiObject.AddComponent(F.Production("TutorialUI"));
        var panel = new GameObject("Tutorial panel", typeof(RectTransform), typeof(CanvasGroup)); panel.transform.SetParent(uiObject.transform);
        var labelObject = new GameObject("Tutorial text", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI)); labelObject.transform.SetParent(panel.transform);
        var label = labelObject.GetComponent<TMPro.TextMeshProUGUI>();
        var open = ScriptableObject.CreateInstance(F.Production("TutorialStep"));
        var next = ScriptableObject.CreateInstance(F.Production("TutorialStep"));
        try
        {
            F.Set(tutorialUi, "tutorialPanel", panel); F.Set(tutorialUi, "instructionText", label);
            F.Set(tutorialUi, "panelTransform", panel.GetComponent<RectTransform>());
            F.Set(tutorialUi, "canvasGroup", panel.GetComponent<CanvasGroup>());
            F.Set(tutorialUi, "typewriterSpeed", .001f); F.Set(tutorialUi, "fadeSpeed", .01f);
            F.Set(tutorialUi, "scaleAnimDuration", .01f);
            uiObject.SetActive(true);
            var type = F.Production("TutorialObjectiveType");
            F.Set(open, "objectiveType", System.Enum.Parse(type, "OpenDailyLoadout"));
            F.Set(open, "instructionText", "Open loadout."); F.Set(open, "stepOrder", 0);
            F.Set(next, "objectiveType", System.Enum.Parse(type, "Wait"));
            F.Set(next, "instructionText", "Next tutorial step."); F.Set(next, "stepOrder", 1);
            F.Set(tutorial, "tutorialSteps", F.List("TutorialStep", open, next));
            F.Set(tutorial, "tutorialUI", tutorialUi); F.Set(tutorial, "enableTutorial", false);
            F.Set(tutorial, "bufferProcessDelay", .01f); tutorialObject.SetActive(true);
            F.Call(tutorial, "StartTutorial");
            yield return new WaitForSeconds(.2f);
            Assert.That(F.Call(tutorial, "CanAcceptPlayerInput"), Is.True);
            Assert.That(F.Call(altar, "TryInteract"), Is.True);
            yield return new WaitForSeconds(.8f); // Real scheduled next-step path, with Loadout still owning the modal.
            Assert.That(f.IsOpen, Is.True);
            Assert.That(F.Call(tutorial, "GetCurrentObjectiveType").ToString(), Is.EqualTo("Wait"));
            Assert.That(panel.activeSelf, Is.False); Assert.That(F.Call(tutorial, "CanAcceptPlayerInput"), Is.False);
            f.Action("Cancel").onClick.Invoke();
            yield return new WaitForSeconds(.3f); // Real Update fallback; no restoration/inventory close event or private buffer call.
            Assert.That(panel.activeSelf, Is.True); Assert.That(label.text, Is.EqualTo("Next tutorial step."));
            Assert.That(F.Call(tutorial, "CanAcceptPlayerInput"), Is.True);
            F.Call(tutorial, "ConfirmWaitStep"); yield return new WaitForSeconds(.7f);
            Assert.That(F.Call(tutorial, "IsTutorialActive"), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(tutorialObject); Object.DestroyImmediate(uiObject); Object.DestroyImmediate(levelObject);
            Object.DestroyImmediate(open); Object.DestroyImmediate(next);
            levelProperty.GetSetMethod(true).Invoke(null, new[] { previousLevel });
            tutorialProperty.GetSetMethod(true).Invoke(null, new[] { previousTutorial });
        }
    }

    private GameObject CreateVisibleModalTutorial(bool trackConfirmation = false, bool configuredOpening = false)
    {
        manager = new GameObject("Modal coordination UI owner"); manager.SetActive(false);
        var ui = manager.AddComponent(F.Production("UIManager"));
        F.Set(ui, "uiFlowController", f.Modals); F.Set(ui, "dailyLoadout", f.Presenter); manager.SetActive(true);
        var singleton = F.Production("TutorialManager").GetProperty("Instance");
        previousTutorial = singleton.GetValue(null); singleton.GetSetMethod(true).Invoke(null, new object[] { null });
        var level = F.Production("LevelManager").GetProperty("Instance");
        previousLevel = level.GetValue(null); level.GetSetMethod(true).Invoke(null, new object[] { null });
        tutorialRoot = new GameObject("Modal coordination tutorial"); tutorialRoot.SetActive(false);
        modalTutorial = tutorialRoot.AddComponent(F.Production("TutorialManager"));
        tutorialUiRoot = new GameObject("Modal coordination tutorial UI"); tutorialUiRoot.SetActive(false);
        modalTutorialUi = tutorialUiRoot.AddComponent(F.Production("TutorialUI"));
        var panel = new GameObject("Tutorial panel", typeof(RectTransform), typeof(CanvasGroup)); panel.transform.SetParent(tutorialUiRoot.transform);
        var text = new GameObject("Dialogue", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI)); text.transform.SetParent(panel.transform);
        F.Set(modalTutorialUi, "tutorialPanel", panel); F.Set(modalTutorialUi, "instructionText", text.GetComponent<TMPro.TextMeshProUGUI>());
        F.Set(modalTutorialUi, "panelTransform", panel.GetComponent<RectTransform>()); F.Set(modalTutorialUi, "canvasGroup", panel.GetComponent<CanvasGroup>());
        F.Set(modalTutorialUi, "typewriterSpeed", .001f); F.Set(modalTutorialUi, "fadeSpeed", .01f); F.Set(modalTutorialUi, "scaleAnimDuration", .01f);
        modalStep = ScriptableObject.CreateInstance(F.Production("TutorialStep"));
        F.Set(modalStep, "objectiveType", System.Enum.Parse(F.Production("TutorialObjectiveType"), trackConfirmation ? "ConfirmDailyLoadout" : configuredOpening ? "OpenDailyLoadout" : "Wait"));
        F.Set(modalStep, "instructionText", "Prepare your loadout.");
        if (trackConfirmation)
        {
            F.Set(modalStep, "useConfiguredCompletion", true);
            var dig = System.Activator.CreateInstance(F.Production("TutorialCondition"));
            F.Set(dig, "objective", System.Enum.Parse(F.Production("TutorialObjectiveType"), "Dig")); F.Set(dig, "requiredCount", 2);
            var confirm = System.Activator.CreateInstance(F.Production("TutorialCondition"));
            F.Set(confirm, "objective", System.Enum.Parse(F.Production("TutorialObjectiveType"), "ConfirmDailyLoadout"));
            F.Set(modalStep, "conditions", F.List("TutorialCondition", dig, confirm));
        }
        if (configuredOpening)
        {
            F.Set(modalStep, "useConfiguredCompletion", true);
            var opening = System.Activator.CreateInstance(F.Production("TutorialCondition"));
            F.Set(opening, "objective", System.Enum.Parse(F.Production("TutorialObjectiveType"), "OpenDailyLoadout"));
            F.Set(modalStep, "conditions", F.List("TutorialCondition", opening));
        }
        F.Set(modalTutorial, "tutorialSteps", F.List("TutorialStep", modalStep)); F.Set(modalTutorial, "tutorialUI", modalTutorialUi);
        F.Set(modalTutorial, "enableTutorial", false); F.Set(modalTutorial, "bufferProcessDelay", .01f);
        tutorialUiRoot.SetActive(true); tutorialRoot.SetActive(true); F.Call(modalTutorial, "StartTutorial");
        return panel;
    }

    [UnityTest]
    public IEnumerator VisibleConfirmationHidesOnLoadoutReopenAndRetainsProgress()
    {
        var panel = CreateVisibleModalTutorial(true); yield return new WaitForSeconds(.2f);
        Assert.That(panel.activeSelf, Is.True);
        F.Production("TutorialEvents").GetMethod("InvokeGroundDug").Invoke(null, null);
        tree = new GameObject("Loadout reopen tree"); var altar = tree.AddComponent(F.Production("RestorationAltar"));
        for (int opening = 0; opening < 2; opening++)
        {
            Assert.That(F.Call(altar, "TryInteract"), Is.True);
            Assert.That(f.IsOpen, Is.True); Assert.That(panel.activeSelf, Is.False, "Opening a modal must hide the current dialogue immediately.");
            Assert.That(F.Call(modalTutorial, "CanAcceptPlayerInput"), Is.False);
            Assert.That(F.Call(modalTutorial, "GetCurrentObjectiveType").ToString(), Is.EqualTo("ConfirmDailyLoadout"));
            if (opening == 1)
            {
                f.Select(true, 0); F.SelectButton(f.Option(0)).onClick.Invoke();
                f.Select(false, 0); F.SelectButton(f.Option(0)).onClick.Invoke(); f.Action("Confirm").onClick.Invoke();
                Assert.That(f.Confirmed, Is.True);
            }
            else Assert.That(f.Confirmed, Is.False);
            yield return new WaitForSeconds(.1f); Assert.That(panel.activeSelf, Is.False);
            f.Action("Cancel").onClick.Invoke(); yield return new WaitForSeconds(.2f);
            Assert.That(f.IsOpen, Is.False); Assert.That(panel.activeSelf, Is.True);
            Assert.That(F.Call(modalTutorial, "IsTutorialActive"), Is.True, "One of two Dig observations must survive every hide/resume.");
        }
        F.Production("TutorialEvents").GetMethod("InvokeGroundDug").Invoke(null, null);
        yield return new WaitForSeconds(.7f);
        Assert.That(F.Call(modalTutorial, "IsTutorialActive"), Is.False, "The second Dig plus the real confirmation must finish without resetting progress.");
        Assert.That(panel.activeSelf, Is.False);
    }

    [UnityTest]
    public IEnumerator ForcedAndLegacyResumeCannotCoverAnOwnedModalWithoutLevelManager()
    {
        var panel = CreateVisibleModalTutorial(); yield return new WaitForSeconds(.2f);
        foreach (var name in new[] { "Inventory", "Crafting", "Restoration", "LevelUp" })
        {
            var modal = System.Enum.Parse(F.Production("UIModal"), name);
            Assert.That(F.Call(f.Modals, "Open", modal), Is.True);
            Assert.That(panel.activeSelf, Is.False, name);
            F.Call(modalTutorialUi, "ForceShowStep", modalStep); F.Call(modalTutorial, "ResumeTutorial");
            Assert.That(panel.activeSelf, Is.False, "Forced presentation must still respect modal ownership.");
            F.Production("UIEvents").GetMethod("TriggerInventoryClosed").Invoke(null, null);
            F.Production("UIEvents").GetMethod("TriggerPauseMenuClosed").Invoke(null, null);
            Assert.That(panel.activeSelf, Is.False, "Legacy close notifications cannot override another modal.");
            Assert.That(F.Call(modalTutorial, "CanAcceptPlayerInput"), Is.False);
            F.Call(modalTutorial, "ConfirmWaitStep");
            Assert.That(F.Call(modalTutorial, "IsTutorialActive"), Is.True);
            yield return new WaitForSeconds(.1f); Assert.That(panel.activeSelf, Is.False);
            F.Call(f.Modals, "Close", modal); yield return new WaitForSeconds(.2f);
            Assert.That(panel.activeSelf, Is.True); Assert.That(F.Call(modalTutorial, "IsTutorialActive"), Is.True);
            F.Call(modalTutorialUi, "ForceShowStep", modalStep);
            Assert.That(panel.activeSelf, Is.True, "Explicit redisplay must still work after the modal closes.");
            yield return new WaitForSeconds(.1f);
        }
    }

    [UnityTest]
    public IEnumerator ConfiguredOpeningObservationSurvivesModalPause()
    {
        var panel = CreateVisibleModalTutorial(configuredOpening: true); yield return new WaitForSeconds(.2f);
        tree = new GameObject("Configured opening tree"); var altar = tree.AddComponent(F.Production("RestorationAltar"));
        Assert.That(F.Call(altar, "TryInteract"), Is.True); Assert.That(panel.activeSelf, Is.False);
        yield return new WaitForSeconds(.8f);
        Assert.That(F.Call(modalTutorial, "IsTutorialActive"), Is.True, "Configured lessons do not advance behind the modal.");
        f.Action("Cancel").onClick.Invoke(); yield return new WaitForSeconds(.9f);
        Assert.That(F.Call(modalTutorial, "IsTutorialActive"), Is.False, "The opening observation must not be lost to the pause.");
        Assert.That(panel.activeSelf, Is.False); Assert.That(f.Confirmed, Is.False);
    }

    [UnityTest]
    public IEnumerator ModalHandoffBlocksDelayedResumeAndDisableCancelsPendingPresentation()
    {
        var panel = CreateVisibleModalTutorial(); yield return new WaitForSeconds(.2f);
        F.Call(modalTutorialUi, "HideStep"); // Modal acquisition must also finish an in-flight hide animation.
        Assert.That(f.Open(), Is.True); Assert.That(panel.activeSelf, Is.False);
        f.Action("Cancel").onClick.Invoke();
        yield return null; // Let the 0.05-second return queue, then replace its modal before it executes.
        var levelUp = System.Enum.Parse(F.Production("UIModal"), "LevelUp");
        Assert.That(F.Call(f.Modals, "Open", levelUp), Is.True);
        yield return new WaitForSeconds(.12f);
        Assert.That(panel.activeSelf, Is.False, "A queued return must not display above the replacement modal.");
        F.Call(f.Modals, "Close", levelUp); yield return null;
        tutorialRoot.SetActive(false); yield return new WaitForSeconds(.15f);
        Assert.That(panel.activeSelf, Is.False); Assert.That(F.Call(modalTutorial, "IsTutorialActive"), Is.False);
        tutorialRoot.SetActive(true); F.Call(modalTutorial, "StartTutorial"); yield return new WaitForSeconds(.2f);
        Assert.That(panel.activeSelf, Is.True);
        Assert.That(f.Open(), Is.True); Assert.That(panel.activeSelf, Is.False);
        f.Action("Cancel").onClick.Invoke(); yield return new WaitForSeconds(.2f);
        Assert.That(panel.activeSelf, Is.True);
    }

    [UnityTest]
    public IEnumerator GameplayResetAndDestructionCloseOnlyTheOwnedPanel()
    {
        manager = new GameObject("Stage4C reset owner"); manager.SetActive(false);
        var ui = manager.AddComponent(F.Production("UIManager"));
        F.Set(ui, "uiFlowController", f.Modals); F.Set(ui, "dailyLoadout", f.Presenter);
        manager.SetActive(true);
        Assert.That(f.Open(), Is.True);
        var resetObject = new GameObject("Reset composition"); resetObject.SetActive(false);
        var reset = resetObject.AddComponent(F.Production("GameResetController"));
        try
        {
            F.Call(reset, "ResetGame");
            Assert.That(f.IsOpen, Is.False); Assert.That(F.Property(f.Modals, "HasOpenModal"), Is.False);
            Assert.That(f.Open(), Is.True);
            Object.Destroy(manager); yield return null;
            Assert.That(f.IsOpen, Is.False); Assert.That(F.Property(f.Modals, "HasOpenModal"), Is.False);
        }
        finally { Object.DestroyImmediate(resetObject); }
    }
}
#endif
