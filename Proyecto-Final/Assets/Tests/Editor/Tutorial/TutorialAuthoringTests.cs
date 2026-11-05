using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class TutorialAuthoringTests
{
    static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "Assembly-CSharp").GetType(name, true);
    static void Set(object o, string name, object value) => o.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(o, value);
    static object Call(object o, string name, params object[] args) => o.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(o, args);
    static object E(string type, string value) => Enum.Parse(T(type), value);
    GameObject root;
    Component manager;
    ScriptableObject step;
    object previousInstance;
    [SetUp] public void Setup()
    {
        root = new GameObject("Tutorial authoring test"); root.SetActive(false);
        manager = root.AddComponent(T("TutorialManager"));
        step = ScriptableObject.CreateInstance(T("TutorialStep"));
        Set(step, "objectiveType", E("TutorialObjectiveType", "Dig"));
        var steps = (IList)Activator.CreateInstance(typeof(System.Collections.Generic.List<>).MakeGenericType(T("TutorialStep")));
        steps.Add(step); Set(manager, "tutorialSteps", steps);
        previousInstance = manager.GetType().GetProperty("Instance").GetValue(null);
        manager.GetType().GetProperty("Instance").GetSetMethod(true).Invoke(null, new object[] { null });
        Set(manager, "enableTutorial", false); root.SetActive(true);
        // EditMode does not guarantee runtime MonoBehaviour lifecycle callbacks.
        // Pair subscriptions explicitly, as the existing DailyLoadout fixtures do.
        Call(manager, "OnDisable");
        Call(manager, "OnEnable");
    }
    [TearDown] public void Cleanup()
    {
        Call(manager, "OnDisable");
        UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(step);
        T("TutorialManager").GetProperty("Instance").GetSetMethod(true).Invoke(null, new[] { previousInstance });
    }
    void Configure(string combination, params string[] objectives)
    {
        Set(step, "useConfiguredCompletion", true);
        Set(step, "conditionCombination", E("TutorialConditionCombination", combination));
        var list = (IList)Activator.CreateInstance(typeof(System.Collections.Generic.List<>).MakeGenericType(T("TutorialCondition")));
        foreach (var objective in objectives)
        {
            var condition = Activator.CreateInstance(T("TutorialCondition"));
            Set(condition, "objective", E("TutorialObjectiveType", objective)); list.Add(condition);
        }
        Set(step, "conditions", list);
    }
    void Event(string objective)
    {
        string method;
        switch (objective)
        {
            case "Dig": method = "InvokeGroundDug"; break;
            case "Harvest": method = "InvokePlantHarvested"; break;
            default: throw new ArgumentException("Unsupported test event: " + objective);
        }
        T("TutorialEvents").GetMethod(method).Invoke(null, null);
    }
    bool Active => (bool)Call(manager, "IsTutorialActive");
    [TestCase("All", true)] [TestCase("Any", false)]
    public void CombinationCompletesOnlyWhenSatisfied(string combination, bool activeAfterFirst)
    {
        Configure(combination, "Dig", "Harvest"); Call(manager, "StartTutorial");
        Event("Dig"); Call(manager, "ProcessBufferAndEnableInput"); Assert.That(Active, Is.EqualTo(activeAfterFirst));
        if (activeAfterFirst) { Event("Harvest"); Assert.That(Active, Is.False); }
    }
    [Test] public void CountsAndBufferedEventsAreStepLocal()
    {
        Configure("All", "Harvest"); var list = (IList)step.GetType().GetField("conditions").GetValue(step);
        Set(list[0], "requiredCount", 2); Call(manager, "StartTutorial");
        Event("Harvest"); Call(manager, "ProcessBufferAndEnableInput"); Assert.That(Active, Is.True);
        Event("Harvest"); Assert.That(Active, Is.False);
        Call(manager, "StartTutorial"); Call(manager, "ProcessBufferAndEnableInput"); Assert.That(Active, Is.True);
    }
    [Test] public void PlantAliasUpdatesEachConditionOnceAndDoesNotLeak()
    {
        Configure("All", "Plant", "PlantProduction");
        var list = (IList)step.GetType().GetField("conditions").GetValue(step);
        Set(list[0], "requiredCount", 2);
        Call(manager, "StartTutorial"); Call(manager, "ProcessBufferAndEnableInput");
        T("TutorialEvents").GetMethod("InvokeProductionPlantPlanted").Invoke(null, null);
        Assert.That(Active, Is.True);
        T("TutorialEvents").GetMethod("InvokeProductionPlantPlanted").Invoke(null, null);
        Assert.That(Active, Is.False);
        Call(manager, "StartTutorial"); Call(manager, "ProcessBufferAndEnableInput"); Assert.That(Active, Is.True);
    }
    [Test] public void ConfiguredContinueRequiresConditionsAndDoesNotAcceptWhilePaused()
    {
        Configure("All", "Dig"); Set(step, "objectiveType", E("TutorialObjectiveType", "Wait"));
        Set(step, "waitMode", E("TutorialWaitMode", "Continue"));
        Call(manager, "StartTutorial"); Call(manager, "ProcessBufferAndEnableInput");
        Call(manager, "ConfirmWaitStep"); Assert.That(Active, Is.True);
        Event("Dig"); Call(manager, "PauseTutorial"); Call(manager, "ConfirmWaitStep"); Assert.That(Active, Is.True);
        Call(manager, "ResumeTutorial"); Call(manager, "ConfirmWaitStep"); Assert.That(Active, Is.False);
    }
    [Test] public void DisableCleansInputAndRestartDoesNotRetainProgress()
    {
        Configure("All", "Dig"); Call(manager, "StartTutorial"); Event("Dig");
        // Exercise the handler deterministically here; PlayMode covers SetActive itself.
        Call(manager, "OnDisable"); root.SetActive(false); Assert.That(Active, Is.False);
        Assert.That(Call(manager, "CanAcceptPlayerInput"), Is.True);
        Event("Dig"); Assert.That(Active, Is.False);
        root.SetActive(true); Call(manager, "OnDisable"); Call(manager, "OnEnable");
        Call(manager, "StartTutorial"); Call(manager, "ProcessBufferAndEnableInput");
        Assert.That(Active, Is.True);
    }
    [Test] public void LegacyCraftingStillUsesCloseInsteadOfOpen()
    {
        Set(step, "objectiveType", E("TutorialObjectiveType", "OpenCrafting"));
        Call(manager, "StartTutorial"); Call(manager, "ProcessBufferAndEnableInput");
        T("TutorialEvents").GetMethod("InvokeCraftingOpened").Invoke(null, null); Assert.That(Active, Is.True);
        T("TutorialEvents").GetMethod("InvokeCraftingClosed").Invoke(null, null); Assert.That(Active, Is.False);
    }
    [Test] public void HintAppendsToExistingDialogueWithoutMutatingCopy()
    {
        var hint = ScriptableObject.CreateInstance(T("ContextualHint"));
        try
        {
            Set(step, "instructionText", "Dialogue"); Set(hint, "title", "Hint"); Set(hint, "description", "Try digging"); Set(step, "hint", hint);
            Assert.That(step.GetType().GetProperty("DisplayText").GetValue(step), Is.EqualTo("Dialogue\n\nHint\nTry digging"));
            Assert.That(step.GetType().GetField("instructionText").GetValue(step), Is.EqualTo("Dialogue"));
        }
        finally { UnityEngine.Object.DestroyImmediate(hint); }
    }
    [Test] public void SequenceUsesListOrderButReportsExistingOrderAndGating()
    {
        var sequence = ScriptableObject.CreateInstance(T("TutorialSequence"));
        var second = ScriptableObject.CreateInstance(T("TutorialStep"));
        try
        {
            Set(step, "stepOrder", 30); Set(step, "isGatedStep", true);
            Set(second, "stepOrder", 10); Set(second, "objectiveType", E("TutorialObjectiveType", "Harvest"));
            var list = (IList)sequence.GetType().GetField("steps").GetValue(sequence); list.Add(step); list.Add(second);
            Set(manager, "sequence", sequence); Call(manager, "StartTutorial");
            Assert.That(Call(manager, "GetCurrentStepOrder"), Is.EqualTo(30)); Assert.That(Call(manager, "IsPlayerGated"), Is.True);
            Call(manager, "ProcessBufferAndEnableInput"); Event("Dig");
            Assert.That(Call(manager, "GetCurrentStepOrder"), Is.EqualTo(10));
            Assert.That(Call(manager, "GetCurrentObjectiveType"), Is.EqualTo(E("TutorialObjectiveType", "Harvest")));
        }
        finally { UnityEngine.Object.DestroyImmediate(sequence); UnityEngine.Object.DestroyImmediate(second); }
    }
    [Test] public void LegacyWaitStillRequiresConfirmation()
    {
        Set(step, "objectiveType", E("TutorialObjectiveType", "Wait")); Call(manager, "StartTutorial");
        Event("Dig"); Assert.That(Active, Is.True); Call(manager, "ConfirmWaitStep"); Assert.That(Active, Is.False);
    }
    [Test] public void LegacyObjectiveCountAndSkipRemainSupported()
    {
        Set(step, "requiredCount", 2); Call(manager, "StartTutorial"); Event("Dig");
        Call(manager, "ProcessBufferAndEnableInput"); Assert.That(Active, Is.True);
        Event("Dig"); Assert.That(Active, Is.False);
        Call(manager, "StartTutorial"); Call(manager, "SkipTutorial"); Assert.That(Active, Is.False);
        Call(manager, "ProcessBufferAndEnableInput"); Assert.That(Active, Is.False);
    }
}
