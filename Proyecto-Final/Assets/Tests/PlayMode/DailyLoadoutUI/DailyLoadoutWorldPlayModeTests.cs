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

    [SetUp]
    public void Setup() { timeScale = Time.timeScale; Time.timeScale = 1; f = new F(); }

    [TearDown]
    public void Cleanup()
    {
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
