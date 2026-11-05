using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(TutorialSequence))]
public class TutorialSequenceEditor : Editor
{
    public override void OnInspectorGUI()
    {
        EditorGUILayout.HelpBox("Playback follows this list from top to bottom. Drag existing Step assets here. stepOrder still controls external gameplay queries, not this list's order.", MessageType.Info);
        DrawDefaultInspector();
        var sequence = (TutorialSequence)target;
        var ids = new HashSet<string>();
        var orders = new HashSet<int>();
        var assets = new HashSet<TutorialStep>();
        foreach (var step in sequence.steps)
        {
            if (step == null) { EditorGUILayout.HelpBox("Missing Step: sequence cannot start.", MessageType.Error); continue; }
            if (!assets.Add(step)) EditorGUILayout.HelpBox("Repeated Step asset: " + step.name, MessageType.Warning);
            if (string.IsNullOrWhiteSpace(step.stepID) || !ids.Add(step.stepID)) EditorGUILayout.HelpBox("Missing or duplicate Step ID: " + step.name, MessageType.Warning);
            if (!orders.Add(step.stepOrder)) EditorGUILayout.HelpBox("Duplicate stepOrder: external gameplay queries may depend on this number.", MessageType.Warning);
            TutorialStepEditor.Validate(step);
        }
    }
}

[CustomEditor(typeof(TutorialStep))]
public class TutorialStepEditor : Editor
{
    public override void OnInspectorGUI()
    {
        EditorGUILayout.HelpBox("Legacy behavior stays enabled until Use Configured Completion is checked. Instruction Text is the dialogue shown in the existing panel.", MessageType.Info);
        DrawDefaultInspector();
        Validate((TutorialStep)target);
    }

    public static void Validate(TutorialStep step)
    {
        if (!step.useConfiguredCompletion)
        {
            if (step.objectiveType == TutorialObjectiveType.FirstPlantReady)
                ValidateReadinessCount(step, step.requiredCount);
            return;
        }
        if (step.waitDuration < 0 || float.IsNaN(step.waitDuration) || float.IsInfinity(step.waitDuration))
            EditorGUILayout.HelpBox(step.name + ": Wait Duration must be finite and nonnegative.", MessageType.Error);
        if (step.waitMode == TutorialWaitMode.Continue && step.objectiveType != TutorialObjectiveType.Wait)
            EditorGUILayout.HelpBox(step.name + ": Continue requires Objective Type = Wait so the existing panel shows Continue.", MessageType.Error);
        if (step.waitMode == TutorialWaitMode.Continue && string.IsNullOrEmpty(step.DisplayText))
            EditorGUILayout.HelpBox(step.name + ": Continue needs visible dialogue.", MessageType.Error);
        bool hidesPanel = step.enterActions != null && step.enterActions.Count > 0 &&
            step.enterActions[step.enterActions.Count - 1] == TutorialStepAction.HidePanel;
        if (hidesPanel && step.waitMode == TutorialWaitMode.Continue)
            EditorGUILayout.HelpBox(step.name + ": Hide Panel cannot be combined with Continue.", MessageType.Error);
        var seen = new HashSet<TutorialObjectiveType>();
        foreach (var condition in step.conditions)
        {
            if (condition.requiredCount < 1) EditorGUILayout.HelpBox(step.name + ": condition counts must be at least one.", MessageType.Error);
            if (!seen.Add(condition.objective)) EditorGUILayout.HelpBox(step.name + ": duplicate condition; one event updates every matching row, not a sequence of rows.", MessageType.Warning);
            switch (condition.objective)
            {
                case TutorialObjectiveType.None:
                case TutorialObjectiveType.Wait:
                case TutorialObjectiveType.ExploreHouse:
                case TutorialObjectiveType.PlantHybrid:
                case TutorialObjectiveType.Welcome:
                case TutorialObjectiveType.FifthNightWarning:
                    EditorGUILayout.HelpBox(step.name + ": " + condition.objective + " has no supported gameplay emitter. Choose another condition.", MessageType.Error);
                    break;
                case TutorialObjectiveType.FirstPlantReady:
                    ValidateReadinessCount(step, condition.requiredCount);
                    break;
                case TutorialObjectiveType.OpenCrafting:
                case TutorialObjectiveType.OpenRestoration:
                    EditorGUILayout.HelpBox(step.name + ": legacy OpenCrafting/OpenRestoration complete on CLOSE events, not opening.", MessageType.Info);
                    break;
            }
        }
    }

    private static void ValidateReadinessCount(TutorialStep step, int count)
    {
        EditorGUILayout.HelpBox(step.name + ": FirstPlantReady observes an actually available harvestable resource once per step, not elapsed growth time.", MessageType.Info);
        if (count != 1)
            EditorGUILayout.HelpBox(step.name + ": FirstPlantReady requires count = 1; repeated readiness observations are not supported.", MessageType.Error);
    }
}
