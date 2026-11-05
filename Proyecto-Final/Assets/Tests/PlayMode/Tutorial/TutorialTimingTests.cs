using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class TutorialTimingTests
{
    static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "Assembly-CSharp").GetType(name, true);
    static void Set(object o, string name, object value) => o.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(o, value);
    static object Call(object o, string name) => o.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(o, null);
    static object E(string type, string value) => Enum.Parse(T(type), value);
    static void Emit(string method) => T("TutorialEvents").GetMethod(method).Invoke(null, null);
    static IList List(string type) => (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(T(type)));

    GameObject root;
    Component manager;
    ScriptableObject step;
    object previousInstance;
    readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
    bool Active => (bool)Call(manager, "IsTutorialActive");

    [SetUp]
    public void Setup()
    {
        root = new GameObject("Isolated tutorial lifecycle owner"); root.SetActive(false);
        manager = root.AddComponent(T("TutorialManager"));
        previousInstance = manager.GetType().GetProperty("Instance").GetValue(null);
        manager.GetType().GetProperty("Instance").GetSetMethod(true).Invoke(null, new object[] { null });
        step = Step("Dig");
        var steps = List("TutorialStep"); steps.Add(step);
        Set(manager, "tutorialSteps", steps);
        Set(manager, "enableTutorial", false);
        Set(manager, "bufferProcessDelay", 0.01f);
    }

    [TearDown]
    public void Cleanup()
    {
        // These are real runtime callbacks; no manual subscription in PlayMode.
        UnityEngine.Object.DestroyImmediate(root);
        foreach (var item in owned) UnityEngine.Object.DestroyImmediate(item);
        owned.Clear();
        T("TutorialManager").GetProperty("Instance").GetSetMethod(true).Invoke(null, new[] { previousInstance });
    }

    ScriptableObject Step(string objective)
    {
        var value = ScriptableObject.CreateInstance(T("TutorialStep")); owned.Add(value);
        Set(value, "objectiveType", E("TutorialObjectiveType", objective));
        return value;
    }

    void Configure(ScriptableObject value, string wait, params string[] objectives)
    {
        Set(value, "useConfiguredCompletion", true);
        Set(value, "waitMode", E("TutorialWaitMode", wait));
        var conditions = List("TutorialCondition");
        foreach (var objective in objectives)
        {
            var condition = Activator.CreateInstance(T("TutorialCondition"));
            Set(condition, "objective", E("TutorialObjectiveType", objective)); conditions.Add(condition);
        }
        Set(value, "conditions", conditions);
    }

    [UnityTest]
    public IEnumerator DurationSuspendsWhilePausedAndSkipCancelsTimer()
    {
        Configure(step, "Duration"); Set(step, "waitDuration", 0.15f);
        root.SetActive(true); yield return null;
        Call(manager, "StartTutorial"); yield return new WaitForSeconds(0.03f);
        Call(manager, "PauseTutorial"); yield return new WaitForSeconds(0.25f);
        Assert.That(Active, Is.True);
        Call(manager, "ResumeTutorial"); yield return new WaitForSeconds(0.3f);
        Assert.That(Active, Is.False);
        Call(manager, "StartTutorial"); Call(manager, "SkipTutorial");
        yield return new WaitForSeconds(0.3f);
        Assert.That(Active, Is.False);
        Assert.That(Call(manager, "CanAcceptPlayerInput"), Is.True);
    }

    [UnityTest]
    public IEnumerator SetActiveDisableClearsBufferedProgressAndReenableSubscribesOnce()
    {
        Configure(step, "None", "Harvest");
        var conditions = (IList)step.GetType().GetField("conditions").GetValue(step);
        Set(conditions[0], "requiredCount", 2);
        root.SetActive(true); yield return null;
        Call(manager, "StartTutorial"); Emit("InvokePlantHarvested");
        Assert.That(Active, Is.True);
        root.SetActive(false); yield return null;
        Assert.That(Active, Is.False);
        Assert.That(Call(manager, "CanAcceptPlayerInput"), Is.True);
        Emit("InvokePlantHarvested"); Assert.That(Active, Is.False);
        root.SetActive(true); Call(manager, "StartTutorial");
        yield return new WaitForSeconds(0.05f);
        Assert.That(Active, Is.True);
        Emit("InvokePlantHarvested"); Assert.That(Active, Is.True, "No stale progress or duplicate subscription after re-enable.");
        Emit("InvokePlantHarvested"); Assert.That(Active, Is.False);
    }

    [UnityTest]
    public IEnumerator HintsAndLastPresentationActionUseExistingPanelWithoutHiddenRitualDeadlock()
    {
        var uiObject = new GameObject("Isolated tutorial panel owner"); uiObject.SetActive(false); owned.Add(uiObject);
        var ui = uiObject.AddComponent(T("TutorialUI"));
        var panel = new GameObject("Tutorial panel", typeof(RectTransform), typeof(CanvasGroup)); panel.transform.SetParent(uiObject.transform);
        var labelObject = new GameObject("Tutorial text", typeof(RectTransform)); labelObject.transform.SetParent(panel.transform);
        var labelType = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("TMPro.TextMeshProUGUI")).First(t => t != null);
        var label = labelObject.AddComponent(labelType);
        Set(ui, "tutorialPanel", panel); Set(ui, "instructionText", label);
        Set(ui, "panelTransform", panel.GetComponent<RectTransform>()); Set(ui, "canvasGroup", panel.GetComponent<CanvasGroup>());
        Set(ui, "typewriterSpeed", 0.001f); Set(ui, "fadeSpeed", 0.01f); Set(ui, "scaleAnimDuration", 0.01f);
        Set(manager, "tutorialUI", ui);
        Set(step, "objectiveType", E("TutorialObjectiveType", "Wait")); Set(step, "instructionText", "Dialogue");
        Configure(step, "Continue");
        var actions = List("TutorialStepAction"); actions.Add(E("TutorialStepAction", "HidePanel")); actions.Add(E("TutorialStepAction", "ShowPanel"));
        Set(step, "enterActions", actions);
        var hint = ScriptableObject.CreateInstance(T("ContextualHint")); owned.Add(hint);
        Set(hint, "title", "Hint"); Set(hint, "description", "Dig here"); Set(step, "hint", hint);
        var hidden = Step("UseRitualAltar"); Set(hidden, "instructionText", "Hidden ritual"); Set(hidden, "stepOrder", 1);
        Configure(hidden, "Duration", "Dig"); Set(hidden, "waitDuration", 0.1f);
        var hide = List("TutorialStepAction"); hide.Add(E("TutorialStepAction", "HidePanel")); Set(hidden, "enterActions", hide);
        var shown = Step("Wait"); Set(shown, "instructionText", "Shown again"); Set(shown, "stepOrder", 2); Configure(shown, "Continue");
        var steps = List("TutorialStep"); steps.Add(step); steps.Add(hidden); steps.Add(shown); Set(manager, "tutorialSteps", steps);
        uiObject.SetActive(true); root.SetActive(true); yield return null;
        Call(manager, "StartTutorial"); yield return new WaitForSeconds(0.2f);
        Assert.That(panel.activeSelf, Is.True);
        Assert.That(labelType.GetProperty("text").GetValue(label), Is.EqualTo("Dialogue\n\nHint\nDig here"));
        Assert.That(Call(ui, "get_IsTyping"), Is.False);
        Call(manager, "ConfirmWaitStep"); yield return new WaitForSeconds(0.7f);
        Assert.That(Call(manager, "GetCurrentObjectiveType"), Is.EqualTo(E("TutorialObjectiveType", "UseRitualAltar")));
        Assert.That(panel.activeSelf, Is.False);
        Assert.That(Call(manager, "CanAcceptPlayerInput"), Is.True, "A hidden panel cannot emit TypingFinished.");
        Emit("InvokeGroundDug"); yield return new WaitForSeconds(0.85f);
        Assert.That(Call(manager, "GetCurrentObjectiveType"), Is.EqualTo(E("TutorialObjectiveType", "Wait")));
        Assert.That(panel.activeSelf, Is.True);
        Assert.That(labelType.GetProperty("text").GetValue(label), Is.EqualTo("Shown again"));
        Call(manager, "SkipTutorial"); yield return new WaitForSeconds(0.05f);
        Assert.That(Active, Is.False); Assert.That(panel.activeSelf, Is.False);
    }
}
