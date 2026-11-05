using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class TutorialReadinessTests
{
    static Type Production(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Single(a => a.GetName().Name == "Assembly-CSharp").GetType(name, true);
    static FieldInfo Field(object target, string name) => target.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
    static void Set(object target, string name, object value) => Field(target, name).SetValue(target, value);
    static object Call(object target, string name) => target.GetType().GetMethod(name).Invoke(target, null);
    static object Objective(string name) => Enum.Parse(Production("TutorialObjectiveType"), name);
    static IList List(string name) => (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(Production(name)));
    static PropertyInfo TutorialInstance => Production("TutorialManager").GetProperty("Instance");
    static FieldInfo PlantInstance => Production("PlantManager").GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic);

    GameObject root;
    Component manager;
    Component plants;
    Component harvestable;
    ScriptableObject step;
    ScriptableObject resource;
    object previousTutorial;
    object previousPlants;
    bool Active => (bool)Call(manager, "IsTutorialActive");
    IList ReadyPlants => (IList)Call(plants, "GetHarvestablePlants");

    [SetUp]
    public void Setup()
    {
        previousTutorial = TutorialInstance.GetValue(null);
        previousPlants = PlantInstance.GetValue(null);
        TutorialInstance.GetSetMethod(true).Invoke(null, new object[] { null });
        PlantInstance.SetValue(null, null);

        root = new GameObject("Isolated real harvestable readiness fixture");
        root.SetActive(false);
        manager = root.AddComponent(Production("TutorialManager"));
        plants = root.AddComponent(Production("PlantManager"));
        // GetHarvestablePlants does not need PlantManager.Start's unrelated GameFlow singleton.
        // Awake still establishes the real manager instance when the root activates.
        ((Behaviour)plants).enabled = false;
        Set(manager, "enableTutorial", false);
        Set(manager, "bufferProcessDelay", 0.01f);

        resource = ScriptableObject.CreateInstance(Production("HarvestableResourceSO"));
        Set(resource, "maxUsesPerDay", 1);
        var plantObject = new GameObject("Real harvestable resource");
        plantObject.transform.SetParent(root.transform, false);
        harvestable = plantObject.AddComponent(Production("HarvestablePlant"));
        Set(harvestable, "data", resource);

        step = ScriptableObject.CreateInstance(Production("TutorialStep"));
        Set(step, "objectiveType", Objective("FirstPlantReady"));
        var steps = List("TutorialStep");
        steps.Add(step);
        Set(manager, "tutorialSteps", steps);
    }

    [TearDown]
    public void Cleanup()
    {
        UnityEngine.Object.DestroyImmediate(root);
        UnityEngine.Object.DestroyImmediate(step);
        UnityEngine.Object.DestroyImmediate(resource);
        TutorialInstance.GetSetMethod(true).Invoke(null, new[] { previousTutorial });
        PlantInstance.SetValue(null, previousPlants);
    }

    void ConfigureReadiness()
    {
        Set(step, "useConfiguredCompletion", true);
        Set(step, "objectiveType", Objective("Wait"));
        var condition = Activator.CreateInstance(Production("TutorialCondition"));
        Set(condition, "objective", Objective("FirstPlantReady"));
        var conditions = List("TutorialCondition");
        conditions.Add(condition);
        Set(step, "conditions", conditions);
    }

    IEnumerator Begin(bool configured, bool ready)
    {
        if (configured) ConfigureReadiness();
        root.SetActive(true);
        yield return null;
        // This resource models availability through remaining uses, not a growth timer.
        // Set only the unavailable fixture state; readiness is asserted publicly below.
        if (!ready) Set(harvestable, "usesRemaining", 0);
        Assert.That(Call(harvestable, "IsReadyToHarvest"), Is.EqualTo(ready));
        Assert.That(ReadyPlants.Count, Is.EqualTo(ready ? 1 : 0), "Fixture must have no unrelated ready plants.");
        Call(manager, "StartTutorial");
    }

    IEnumerator ReadyAtEntry(bool configured)
    {
        yield return Begin(configured, true);
        yield return new WaitForSeconds(0.15f);
        Assert.That(Active, Is.False, "Already-ready plants must be reconciled at step entry without a manual tutorial event.");
    }

    IEnumerator ReadyLater(bool configured)
    {
        yield return Begin(configured, false);
        yield return new WaitForSeconds(0.15f);
        Assert.That(Active, Is.True, "Unavailable plants must not complete readiness.");
        Call(harvestable, "Refresh");
        Assert.That(Call(harvestable, "IsReadyToHarvest"), Is.True);
        Assert.That(ReadyPlants, Does.Contain(harvestable));
        yield return new WaitForSeconds(0.15f);
        Assert.That(Active, Is.False, "Observe real availability changes while the step is waiting.");
    }

    [UnityTest] public IEnumerator LegacyReadinessAlreadyPresentAtEntryCompletes() { yield return ReadyAtEntry(false); }
    [UnityTest] public IEnumerator ConfiguredReadinessAlreadyPresentAtEntryCompletes() { yield return ReadyAtEntry(true); }
    [UnityTest] public IEnumerator LegacyReadinessBecomingAvailableLaterCompletes() { yield return ReadyLater(false); }
    [UnityTest] public IEnumerator ConfiguredReadinessBecomingAvailableLaterCompletes() { yield return ReadyLater(true); }

    [UnityTest]
    public IEnumerator AvailabilityIsOneObservationPerStepNotOnePerFrame()
    {
        Set(step, "requiredCount", 2);
        yield return Begin(false, true);
        yield return new WaitForSeconds(0.2f);
        Assert.That(Active, Is.True, "An unsupported count of two cannot be fulfilled by polling one ready resource.");
        ConfigureReadiness();
        var conditions = (IList)Field(step, "conditions").GetValue(step);
        Set(conditions[0], "requiredCount", 2);
        Call(manager, "StartTutorial");
        yield return new WaitForSeconds(0.2f);
        Assert.That(Active, Is.True, "Configured readiness is also latched once per step.");
    }

    [UnityTest]
    public IEnumerator ReadinessRespectsAllAndContinueWithADifferentPrimaryObjective()
    {
        ConfigureReadiness();
        Set(step, "waitMode", Enum.Parse(Production("TutorialWaitMode"), "Continue"));
        var conditions = (IList)Field(step, "conditions").GetValue(step);
        var dig = Activator.CreateInstance(Production("TutorialCondition"));
        Set(dig, "objective", Objective("Dig"));
        conditions.Add(dig);
        yield return Begin(false, true);
        yield return new WaitForSeconds(0.1f);
        Call(manager, "ConfirmWaitStep");
        Assert.That(Active, Is.True, "All still requires Dig as well as readiness.");
        Production("TutorialEvents").GetMethod("InvokeGroundDug").Invoke(null, null);
        Assert.That(Active, Is.True, "Satisfied conditions do not bypass Continue.");
        Call(manager, "ConfirmWaitStep");
        Assert.That(Active, Is.False);
    }

    [UnityTest]
    public IEnumerator ReadinessRespectsAnyAndDuration()
    {
        ConfigureReadiness();
        Set(step, "conditionCombination", Enum.Parse(Production("TutorialConditionCombination"), "Any"));
        Set(step, "waitMode", Enum.Parse(Production("TutorialWaitMode"), "Duration"));
        Set(step, "waitDuration", 0.25f);
        var conditions = (IList)Field(step, "conditions").GetValue(step);
        var dig = Activator.CreateInstance(Production("TutorialCondition"));
        Set(dig, "objective", Objective("Dig"));
        conditions.Add(dig);
        yield return Begin(false, true);
        yield return new WaitForSeconds(0.1f);
        Assert.That(Active, Is.True, "Any readiness must not bypass Duration.");
        yield return new WaitForSeconds(0.25f);
        Assert.That(Active, Is.False, "Any can complete from readiness without Dig once Duration finishes.");
    }

    [UnityTest]
    public IEnumerator NoReadyPlantOrPlantBeingHarvestedDoesNotAdvance()
    {
        yield return Begin(false, false);
        yield return new WaitForSeconds(0.15f);
        Assert.That(Active, Is.True);
        Call(harvestable, "Refresh");
        Call(harvestable, "StartHarvest");
        Assert.That(Call(harvestable, "IsReadyToHarvest"), Is.True);
        Assert.That(Call(harvestable, "IsBeingHarvested"), Is.True);
        Assert.That(ReadyPlants, Is.Empty);
        yield return new WaitForSeconds(0.15f);
        Assert.That(Active, Is.True, "A resource already being harvested is not available.");
        Call(harvestable, "CancelHarvest");
        yield return new WaitForSeconds(0.15f);
        Assert.That(Active, Is.False, "Availability after cancelling harvest must be observed.");
    }

    [UnityTest]
    public IEnumerator PauseReconcilesOnResumeAndSkipDoesNotLeakIntoRestart()
    {
        yield return Begin(false, false);
        yield return new WaitForSeconds(0.05f);
        Call(manager, "PauseTutorial");
        Call(harvestable, "Refresh");
        yield return new WaitForSeconds(0.15f);
        Assert.That(Active, Is.True, "Do not advance while paused.");
        Call(manager, "ResumeTutorial");
        yield return new WaitForSeconds(0.15f);
        Assert.That(Active, Is.False, "Resume must reconcile availability that appeared while paused.");

        Set(harvestable, "usesRemaining", 0);
        Call(manager, "StartTutorial");
        Call(manager, "SkipTutorial");
        Call(harvestable, "Refresh");
        yield return new WaitForSeconds(0.15f);
        Assert.That(Active, Is.False);
        Assert.That(Call(manager, "GetCurrentObjectiveType").ToString(), Is.EqualTo("None"));
        Set(harvestable, "usesRemaining", 0);
        Call(manager, "StartTutorial");
        yield return new WaitForSeconds(0.15f);
        Assert.That(Active, Is.True, "Skipped observation must not leak a ready event into restart.");
        Call(harvestable, "Refresh");
        yield return new WaitForSeconds(0.15f);
        Assert.That(Active, Is.False);
    }
}
