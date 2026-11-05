#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using F = DailyLoadoutUIFixture;

public sealed class TutorialLoadoutProgressionTests
{
    F f;
    GameObject root;
    Component tutorial;
    ScriptableObject sequence;
    readonly List<ScriptableObject> owned = new List<ScriptableObject>();
    object previous;
    bool Active => (bool)F.Call(tutorial, "IsTutorialActive");
    object Objective(string name)
    {
        var type = F.Production("TutorialObjectiveType");
        Assert.That(Enum.GetNames(type), Does.Contain(name), "Append the objective without renumbering serialized values.");
        return Enum.Parse(type, name);
    }
    ScriptableObject Step(string objective, int order)
    {
        var step = ScriptableObject.CreateInstance(F.Production("TutorialStep")); owned.Add(step);
        F.Set(step, "objectiveType", Objective(objective)); F.Set(step, "stepOrder", order);
        ((IList)F.Field(sequence, "steps")).Add(step); return step;
    }
    Component InactiveComponent(string type)
    {
        var holder = new GameObject("Isolated " + type);
        holder.SetActive(false); holder.transform.SetParent(root.transform, false);
        return holder.AddComponent(F.Production(type));
    }
    void RealConfirm()
    {
        Assert.That(f.Open(), Is.True);
        Assert.That(F.Call(f.Session, "TrySetDraftPlant", 0, F.Seed(7)), Is.True);
        Assert.That(F.Call(f.Session, "TrySetDraftSpell", 2, f.FirstSpell), Is.True);
        F.Call(f.Presenter, "Confirm"); Assert.That(f.Confirmed, Is.True);
    }
    void Event(string name) => F.Production("TutorialEvents").GetMethod(name).Invoke(null, null);
    IEnumerator Settle() { yield return new WaitForSeconds(0.12f); }

    [SetUp]
    public void Setup()
    {
        f = new F();
        var instance = F.Production("TutorialManager").GetProperty("Instance"); previous = instance.GetValue(null);
        instance.GetSetMethod(true).Invoke(null, new object[] { null });
        root = new GameObject("Real tutorial loadout callbacks"); root.SetActive(false);
        tutorial = root.AddComponent(F.Production("TutorialManager"));
        F.Set(tutorial, "enableTutorial", false); F.Set(tutorial, "bufferProcessDelay", 0.01f);
        sequence = ScriptableObject.CreateInstance(F.Production("TutorialSequence")); owned.Add(sequence);
        F.Set(tutorial, "sequence", sequence);
    }
    [TearDown]
    public void Cleanup()
    {
        UnityEngine.Object.DestroyImmediate(root);
        foreach (var value in owned) UnityEngine.Object.DestroyImmediate(value);
        owned.Clear();
        F.Production("TutorialManager").GetProperty("Instance").GetSetMethod(true).Invoke(null, new[] { previous });
        f?.Dispose();
    }
    void RequireUsable()
    {
        var field = sequence.GetType().GetField("requireUsableLoadout");
        Assert.That(field, Is.Not.Null, "Opt-in tutorial sequence policy is required."); field.SetValue(sequence, true);
    }
    IEnumerator Begin()
    {
        root.SetActive(true); yield return null; F.Call(tutorial, "StartTutorial"); yield return Settle();
    }

    [UnityTest]
    public IEnumerator InitializationOpenCloseAndRejectedConfirmationAreNotSuccessfulConfirmation()
    {
        Step("ConfirmDailyLoadout", 90); RequireUsable(); yield return Begin();
        Assert.That(Active, Is.True, "Session initialization must not satisfy confirmation.");
        Assert.That(f.Open(), Is.True); yield return Settle(); Assert.That(Active, Is.True);
        F.Call(f.Presenter, "Confirm"); Assert.That(f.Confirmed, Is.False);
        yield return Settle(); Assert.That(Active, Is.True);
        F.Call(f.Presenter, "Close"); yield return Settle(); Assert.That(Active, Is.True);
        RealConfirm(); yield return Settle(); Assert.That(Active, Is.False);
    }

    [UnityTest]
    public IEnumerator ConfirmationImmediatelyBeforeStepEntryReconcilesActualCommittedState()
    {
        Step("Dig", 100); Step("ConfirmDailyLoadout", -20); RequireUsable(); yield return Begin();
        RealConfirm(); Assert.That(Active, Is.True);
        Event("InvokeGroundDug"); yield return Settle(); Assert.That(Active, Is.False);
    }

    [UnityTest]
    public IEnumerator EligibilityFailureWithBothCategoriesDoesNotCompleteConfirmation()
    {
        Step("ConfirmDailyLoadout", 1); RequireUsable(); yield return Begin();
        f.AdvanceCatalog(); Assert.That(f.Open(), Is.True);
        Assert.That(F.Call(f.Session, "TrySetDraftPlant", 0, F.Seed(14)), Is.True);
        Assert.That(F.Call(f.Session, "TrySetDraftSpell", 0, f.LaterSpell), Is.True);
        F.Call(f.Unlocks, "RebuildUnlocksUpToLevel", 1);
        F.Call(f.Presenter, "Confirm");
        Assert.That(f.Confirmed, Is.False);
        Assert.That(f.HasValue(true, true, 0), Is.False);
        Assert.That(f.HasValue(false, true, 0), Is.False);
        Assert.That(f.HasValue(true, false, 0), Is.True);
        Assert.That(f.HasValue(false, false, 0), Is.True);
        yield return Settle(); Assert.That(Active, Is.True);
    }

    [UnityTest]
    public IEnumerator EmptyConfirmationOutsideOptInIsNotUsableConfirmedState()
    {
        Step("ConfirmDailyLoadout", 1); yield return Begin();
        Assert.That(f.Open(), Is.True); F.Call(f.Presenter, "Confirm"); Assert.That(f.Confirmed, Is.True);
        yield return Settle(); Assert.That(Active, Is.True, "Empty global confirmation remains legal but cannot teach usable equipment.");
    }

    [UnityTest]
    public IEnumerator PauseDoesNotAdvanceAndRestartAfterResetDoesNotReplayConfirmation()
    {
        Step("ConfirmDailyLoadout", 1); RequireUsable(); yield return Begin();
        F.Call(tutorial, "PauseTutorial"); RealConfirm(); yield return Settle(); Assert.That(Active, Is.True);
        F.Call(tutorial, "ResumeTutorial"); yield return Settle(); Assert.That(Active, Is.False);
        F.Call(f.Presenter, "Close");
        F.Call(f.Session, "ResetForNewRun");
        F.Call(tutorial, "StartTutorial"); yield return Settle(); Assert.That(Active, Is.True);
        F.Call(tutorial, "SkipTutorial");
        RealConfirm(); yield return Settle(); Assert.That(Active, Is.False);
        F.Call(f.Presenter, "Close"); F.Call(f.Session, "ResetForNewRun");
        F.Call(tutorial, "StartTutorial"); yield return Settle(); Assert.That(Active, Is.True);
    }

    [UnityTest]
    public IEnumerator ExplicitExitObjectiveUsesHouseExitNotEntryOrCastingAndRetainsExternalQueries()
    {
        Step("ExitHouse", -50); Step("CastSpell", 3); yield return Begin();
        Assert.That(F.Call(tutorial, "GetCurrentStepOrder"), Is.EqualTo(-50));
        Assert.That(F.Call(tutorial, "IsPlayerGated"), Is.False);
        Event("InvokeHouseEntered"); Event("InvokeSpellCasted"); yield return Settle();
        Assert.That(F.Call(tutorial, "GetCurrentObjectiveType").ToString(), Is.EqualTo("ExitHouse"));
        Event("InvokeHouseExited"); yield return Settle();
        Assert.That(F.Call(tutorial, "GetCurrentObjectiveType").ToString(), Is.EqualTo("CastSpell"));
        Event("InvokeSpellCasted"); yield return Settle(); Assert.That(Active, Is.False);
    }

    [UnityTest]
    public IEnumerator ExitBeforeDeferredStepEntryReconcilesRealWorldState()
    {
        Step("Dig", 1); Step("ExitHouse", 2);
        var world = root.AddComponent(F.Production("WorldTransitionAnimator"));
        ((Behaviour)world).enabled = false; // Avoid unrelated Start discovery/fade canvas creation.
        F.Set(world, "useFade", false);
        yield return Begin();
        F.Call(world, "EnterHouse");
        Assert.That(F.Property(world, "IsInInterior"), Is.True);
        F.Call(tutorial, "DeferNextStep"); Event("InvokeGroundDug");
        F.Call(world, "ExitHouse"); Event("InvokeHouseExited");
        F.Call(tutorial, "ReleaseDeferredNextStep", true);
        yield return Settle();
        Assert.That(Active, Is.False, "Actual exterior state survives the delay before ExitHouse step entry.");
    }

    [UnityTest]
    public IEnumerator ConfiguredConfirmationRespectsAllContinueAndIsObservedOnlyOnce()
    {
        var step = Step("Wait", 1); RequireUsable();
        F.Set(step, "useConfiguredCompletion", true);
        F.Set(step, "waitMode", Enum.Parse(F.Production("TutorialWaitMode"), "Continue"));
        var conditions = (IList)F.Field(step, "conditions");
        foreach (var name in new[] { "ConfirmDailyLoadout", "Dig" })
        {
            var condition = Activator.CreateInstance(F.Production("TutorialCondition"));
            F.Set(condition, "objective", Objective(name)); conditions.Add(condition);
        }
        yield return Begin(); RealConfirm(); yield return Settle();
        F.Call(tutorial, "ConfirmWaitStep"); Assert.That(Active, Is.True, "All still requires Dig.");
        Event("InvokeGroundDug"); Assert.That(Active, Is.True, "Conditions cannot bypass Continue.");
        F.Call(tutorial, "ConfirmWaitStep"); Assert.That(Active, Is.False);

        F.Set(conditions[0], "requiredCount", 2);
        F.Call(tutorial, "StartTutorial"); yield return Settle();
        Event("InvokeGroundDug"); yield return Settle();
        F.Call(tutorial, "ConfirmWaitStep");
        Assert.That(Active, Is.True, "Confirmed state is one observation, not one count per frame.");
    }

    [UnityTest]
    public IEnumerator ConfiguredAnyConfirmationStillWaitsForDuration()
    {
        var step = Step("Wait", 1); RequireUsable();
        F.Set(step, "useConfiguredCompletion", true);
        F.Set(step, "conditionCombination", Enum.Parse(F.Production("TutorialConditionCombination"), "Any"));
        F.Set(step, "waitMode", Enum.Parse(F.Production("TutorialWaitMode"), "Duration"));
        F.Set(step, "waitDuration", 0.3f);
        foreach (var name in new[] { "ConfirmDailyLoadout", "Dig" })
        {
            var condition = Activator.CreateInstance(F.Production("TutorialCondition"));
            F.Set(condition, "objective", Objective(name)); ((IList)F.Field(step, "conditions")).Add(condition);
        }
        yield return Begin(); RealConfirm(); yield return Settle(); Assert.That(Active, Is.True);
        yield return new WaitForSeconds(0.4f); Assert.That(Active, Is.False);
    }

    [UnityTest]
    public IEnumerator InstalledBattleRequiresRitualCastAndSurvivalAcrossDeferral()
    {
        var installed = UnityEditor.AssetDatabase.LoadAssetAtPath(
            "Assets/Scriptables/Tutorial/SummerlimbsTutorial.asset", F.Production("TutorialSequence"));
        Assert.That(installed, Is.Not.Null);
        var authored = ((IList)F.Field(installed, "steps")).Cast<ScriptableObject>().Single(step =>
            (bool)F.Field(step, "useConfiguredCompletion") &&
            ((IList)F.Field(step, "conditions")).Cast<object>().Any(condition =>
                F.Field(condition, "objective").ToString() == "UseRitualAltar"));
        var lesson = UnityEngine.Object.Instantiate(authored); owned.Add(lesson);
        ((IList)F.Field(sequence, "steps")).Add(lesson);
        Step("Wait", -1);
        F.Set(sequence, "useExplicitNightPermission", F.Field(installed, "useExplicitNightPermission"));
        string activeObjective = F.Field(lesson, "objectiveType").ToString();
        yield return Begin();
        Assert.That(F.Call(tutorial, "CanStartTutorialNight", 9), Is.EqualTo(true));
        F.Call(tutorial, "ConfirmWaitStep");
        Assert.That(F.Call(tutorial, "GetCurrentObjectiveType").ToString(), Is.EqualTo(activeObjective));

        // Exercise the public deferral contract used by RitualAltar, not its private coroutine.
        F.Call(tutorial, "DeferNextStep"); Event("InvokeRitualAltarUsed");
        Event("InvokeNightStarted"); Event("InvokeEnemyDefeated");
        yield return Settle();
        Assert.That(F.Call(tutorial, "GetCurrentObjectiveType").ToString(), Is.EqualTo(activeObjective),
            "Ritual use and unrelated night/enemy events cannot replace casting and survival.");
        F.Call(tutorial, "ReleaseDeferredNextStep", true); yield return Settle();
        Assert.That(F.Call(tutorial, "GetCurrentObjectiveType").ToString(), Is.EqualTo(activeObjective),
            "Ending ritual deferral cannot skip the remaining conditions.");
        Event("InvokeSpellCasted"); yield return Settle();
        Assert.That(F.Call(tutorial, "GetCurrentObjectiveType").ToString(), Is.EqualTo(activeObjective),
            "Casting still requires actual survival completion.");
        F.Call(tutorial, "ConfirmWaitStep");
        Assert.That(F.Call(tutorial, "GetCurrentObjectiveType").ToString(), Is.EqualTo(activeObjective));
        Event("InvokeNightSurvived"); yield return Settle();
        Assert.That(F.Call(tutorial, "GetCurrentObjectiveType").ToString(), Is.EqualTo("Wait"));
        F.Call(tutorial, "ConfirmWaitStep"); yield return Settle();
        Assert.That(Active, Is.False);
    }

    [UnityTest]
    public IEnumerator OptInNightPermissionUsesMetadataNotStepOrderAndLegacyQueriesStayUnchanged()
    {
        var first = Step("Dig", 100); var second = Step("Move", -100);
        var optIn = sequence.GetType().GetField("useExplicitNightPermission");
        var permission = first.GetType().GetField("allowNightTransition");
        Assert.That(optIn, Is.Not.Null); Assert.That(permission, Is.Not.Null);
        optIn.SetValue(sequence, true); permission.SetValue(first, false); permission.SetValue(second, true);
        var query = tutorial.GetType().GetMethod("CanStartTutorialNight", new[] { typeof(int) });
        Assert.That(query, Is.Not.Null, "RitualAltar should reuse this query with its legacy numeric threshold.");
        var altar = InactiveComponent("RitualAltar");
        F.Set(altar, "levelManager", InactiveComponent("LevelManager"));
        F.Set(altar, "playerLife", InactiveComponent("LifeController"));
        yield return Begin();
        Assert.That(F.Call(altar, "CanInteract"), Is.False, "Real altar must respect metadata despite high stepOrder.");
        Assert.That(query.Invoke(tutorial, new object[] { 9 }), Is.False);
        Assert.That(F.Call(tutorial, "GetCurrentStepOrder"), Is.EqualTo(100));
        Event("InvokeGroundDug"); yield return Settle();
        Assert.That(query.Invoke(tutorial, new object[] { 9 }), Is.True);
        Assert.That(F.Call(altar, "CanInteract"), Is.True, "Real altar permits authored night despite low stepOrder.");
        Assert.That(F.Call(tutorial, "GetCurrentStepOrder"), Is.EqualTo(-100));
        F.Call(tutorial, "SkipTutorial");
        optIn.SetValue(sequence, false); F.Call(tutorial, "StartTutorial"); yield return Settle();
        Assert.That(query.Invoke(tutorial, new object[] { 9 }), Is.True, "Legacy threshold remains the caller's authored value.");
        Event("InvokeGroundDug"); yield return Settle();
        Assert.That(query.Invoke(tutorial, new object[] { 9 }), Is.False);
        Assert.That(F.Call(altar, "CanInteract"), Is.False, "Legacy numeric altar restriction remains active by default.");
        F.Call(tutorial, "SkipTutorial");
        Assert.That(query.Invoke(tutorial, new object[] { 9 }), Is.True, "Outside tutorial remains unrestricted.");
    }
}
#endif
