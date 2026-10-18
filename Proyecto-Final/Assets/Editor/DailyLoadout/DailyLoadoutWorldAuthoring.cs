using System;
using System.Collections.Generic;
using System.Linq;
using MagicGarden.LoadoutUI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Explicit native authoring; never runs on import and never regenerates Stage 4A.</summary>
public static class DailyLoadoutWorldAuthoring
{
    public const string CanvasPath = "Assets/Prefabs/UI/Canvas/Canvas Game UI.prefab";
    public const string AltarPath = "Assets/Prefabs/Interactables/Restoration Altar.prefab";
    public const string PanelPath = "Assets/Prefabs/UI/DailyLoadout/DailyLoadoutPanel.prefab";
    public const string GamePath = "Assets/Scenes/Game.unity";
    public const string FindStepPath = "Assets/Scriptables/Tutorial/Tutorial Steps/SubObjectives/12-FindRestorationAltar.asset";
    public const string OpenStepPath = "Assets/Scriptables/Tutorial/Tutorial Steps/SubObjectives/13-OpenRestoration.asset";
    public const string HintPath = "Assets/Scriptables/Tutorial/Hints/Restoration.asset";
    public const string FindText = "Seek the Sacred Tree to prepare your daily loadout.";
    public const string OpenText = "Press F at the Sacred Tree to inspect and configure your daily loadout. Confirm once per day, or close without confirming. You can inspect your confirmed loadout again.";

    [MenuItem("Tools/Magic Garden/Daily Loadout/Wire Sacred Tree (Stage 4C)")]
    public static void Wire()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Wire Stage 4C outside Play Mode.");
        if (PrefabStageUtility.GetCurrentPrefabStage() != null)
            throw new InvalidOperationException("Close Prefab Mode before wiring Stage 4C; unsaved prefab edits must be preserved.");
        foreach (var path in new[] { CanvasPath, AltarPath, PanelPath, FindStepPath, OpenStepPath, HintPath })
        {
            var asset = AssetDatabase.LoadMainAssetAtPath(path);
            bool dirty = asset != null && EditorUtility.IsDirty(asset);
            if (asset is GameObject prefab)
                dirty |= prefab.GetComponentsInChildren<Transform>(true).Any(t => EditorUtility.IsDirty(t.gameObject)) ||
                    prefab.GetComponentsInChildren<Component>(true).Any(c => c != null && EditorUtility.IsDirty(c));
            if (asset == null || dirty)
                throw new InvalidOperationException("Missing or unsaved asset; save/review it first: " + path);
        }
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
            if (scene.path == GamePath && scene.isDirty)
                throw new InvalidOperationException("Save/review the dirty Game scene before wiring its prefabs.");
        }
        ValidateTutorialMigration(FindStepPath, TutorialObjectiveType.RestorationProximity,
            TutorialObjectiveType.DailyLoadoutProximity, "Battle will leave its scars. Now, seek the Restoration Altar.", FindText);
        ValidateTutorialMigration(OpenStepPath, TutorialObjectiveType.OpenRestoration,
            TutorialObjectiveType.OpenDailyLoadout, "This is a hallowed place. Here you may offer resources to heal the heart of the Sacred Tree.", OpenText);
        var hintTitle = new SerializedObject(AssetDatabase.LoadMainAssetAtPath(HintPath)).FindProperty("title").stringValue;
        if (hintTitle != "Open Restoration" && hintTitle != "Open Daily Loadout")
            throw new InvalidOperationException("Custom hint text found; review before migration.");
        // Preflight all authoring on isolated prefab contents before saving any changes.
        var canvas = PrefabUtility.LoadPrefabContents(CanvasPath);
        var altar = PrefabUtility.LoadPrefabContents(AltarPath);
        try
        {
            ConfigureCanvas(canvas);
            ConfigureAltar(altar);
            ValidateCanvas(canvas);
            ValidateAltar(altar);
            PrefabUtility.SaveAsPrefabAsset(canvas, CanvasPath);
            PrefabUtility.SaveAsPrefabAsset(altar, AltarPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(altar);
            PrefabUtility.UnloadPrefabContents(canvas);
        }
        MigrateTutorial();
        ValidateInstalled();
        Debug.Log("Stage 4C wired. One inactive nested DailyLoadoutPanel is owned by gameplay UIManager. " +
            "Sacred Tree proximity/tutorial assets migrated. Game placement, restoration assets and HUD preserved. Run EditMode/PlayMode tests and the Stage4C gameplay checklist.");
    }

    // Public for tests on unsaved contents; neither method saves assets.
    public static void ConfigureCanvas(GameObject root)
    {
        var owners = root.GetComponentsInChildren<UIManager>(true);
        if (owners.Length != 1 || root.GetComponent<Canvas>() == null)
            throw new InvalidOperationException("Expected one UIManager under the gameplay Canvas root.");
        var panels = root.GetComponentsInChildren<DailyLoadoutPresenter>(true);
        if (panels.Length > 1) throw new InvalidOperationException("Multiple loadout presenters found; resolve intentionally, do not delete user work.");
        DailyLoadoutPresenter panel;
        if (panels.Length == 0)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PanelPath);
            ValidatePanel(prefab.GetComponent<DailyLoadoutPresenter>());
            panel = ((GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform)).GetComponent<DailyLoadoutPresenter>();
        }
        else panel = panels[0];
        if (PrefabUtility.GetCorrespondingObjectFromOriginalSource(panel.gameObject) != AssetDatabase.LoadAssetAtPath<GameObject>(PanelPath))
            throw new InvalidOperationException("Existing presenter is not the approved nested DailyLoadoutPanel prefab.");
        var owner = owners[0];
        var ui = new SerializedObject(owner);
        var flow = ui.FindProperty("uiFlowController").objectReferenceValue as UIFlowController;
        if (flow == null) flow = owner.GetComponent<UIFlowController>();
        if (flow == null) flow = owner.gameObject.AddComponent<UIFlowController>();
        ui.FindProperty("uiFlowController").objectReferenceValue = flow;
        ui.FindProperty("dailyLoadout").objectReferenceValue = panel;
        ui.ApplyModifiedPropertiesWithoutUndo();
        var presenter = new SerializedObject(panel);
        presenter.FindProperty("view").objectReferenceValue = panel.GetComponent<DailyLoadoutPanelView>();
        presenter.FindProperty("modals").objectReferenceValue = flow;
        presenter.ApplyModifiedPropertiesWithoutUndo();
        panel.gameObject.SetActive(false);
        panel.transform.SetAsLastSibling();
    }

    public static void ConfigureAltar(GameObject root)
    {
        var triggers = root.GetComponentsInChildren<TutorialProximityTrigger>(true);
        if (triggers.Length != 1) throw new InvalidOperationException("Expected the existing Sacred Tree proximity trigger.");
        var so = new SerializedObject(triggers[0]);
        var route = so.FindProperty("eventToFire");
        if (route.intValue != (int)TutorialProximityTrigger.TutorialProximityEvent.RestorationProximity &&
            route.intValue != (int)TutorialProximityTrigger.TutorialProximityEvent.DailyLoadoutProximity)
            throw new InvalidOperationException("Unexpected proximity route; refusing to overwrite custom user wiring.");
        route.intValue = (int)TutorialProximityTrigger.TutorialProximityEvent.DailyLoadoutProximity;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ValidateTutorialMigration(string path, TutorialObjectiveType previous,
        TutorialObjectiveType next, string previousText, string nextText)
    {
        var step = AssetDatabase.LoadAssetAtPath<TutorialStep>(path);
        if ((step.objectiveType != previous && step.objectiveType != next) ||
            (step.instructionText != previousText && step.instructionText != nextText))
            throw new InvalidOperationException("Custom tutorial objective/text found; review before migration: " + path);
    }

    private static void MigrateTutorial()
    {
        MigrateStep(FindStepPath, TutorialObjectiveType.RestorationProximity, TutorialObjectiveType.DailyLoadoutProximity, FindText);
        MigrateStep(OpenStepPath, TutorialObjectiveType.OpenRestoration, TutorialObjectiveType.OpenDailyLoadout, OpenText);
        var hint = AssetDatabase.LoadMainAssetAtPath(HintPath);
        var so = new SerializedObject(hint);
        so.FindProperty("title").stringValue = "Open Daily Loadout";
        so.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssetIfDirty(hint);
    }

    private static void MigrateStep(string path, TutorialObjectiveType previous, TutorialObjectiveType next, string text)
    {
        var step = AssetDatabase.LoadAssetAtPath<TutorialStep>(path);
        if (step.objectiveType != previous && step.objectiveType != next)
            throw new InvalidOperationException("Unexpected objective; refusing to overwrite custom tutorial wiring: " + path);
        step.objectiveType = next; step.instructionText = text;
        EditorUtility.SetDirty(step); AssetDatabase.SaveAssetIfDirty(step);
    }

    public static void ValidateCanvas(GameObject root)
    {
        var owners = root.GetComponentsInChildren<UIManager>(true);
        var panels = root.GetComponentsInChildren<DailyLoadoutPresenter>(true);
        if (owners.Length != 1 || panels.Length != 1 || owners[0].DailyLoadout != panels[0] || panels[0].gameObject.activeSelf)
            throw new InvalidOperationException("Expected one inactive panel explicitly bound to gameplay UIManager.");
        var so = new SerializedObject(panels[0]);
        if (owners[0].Flow == null || so.FindProperty("modals").objectReferenceValue != owners[0].Flow ||
            so.FindProperty("view").objectReferenceValue != panels[0].GetComponent<DailyLoadoutPanelView>())
            throw new InvalidOperationException("Presenter view/modal references are invalid.");
        ValidatePanel(panels[0]);
    }

    private static void ValidatePanel(DailyLoadoutPresenter panel)
    {
        if (panel == null) throw new InvalidOperationException("Stage 4B presenter must already be authored.");
        var view = panel.GetComponent<DailyLoadoutPanelView>();
        if (view == null || view.PlantSlots == null || view.SpellSlots == null ||
            view.PlantSlots.Count != 3 || view.SpellSlots.Count != 3 || view.OptionCapacity < 6 ||
            view.OptionContent == null || view.ConfirmButton == null || view.CancelButton == null || view.ClearButton == null)
            throw new InvalidOperationException("Stage 4B authored view references/pool are invalid. Do not regenerate Stage 4A.");
        var buttons = new HashSet<Button>();
        foreach (var button in new[] { view.ConfirmButton, view.CancelButton, view.ClearButton }
            .Concat(view.PlantSlots.Select(s => s != null ? s.SelectButton : null))
            .Concat(view.SpellSlots.Select(s => s != null ? s.SelectButton : null))
            .Concat(view.OptionViews.Select(s => s != null ? s.SelectButton : null)))
            if (button == null || !buttons.Add(button) || button.onClick.GetPersistentEventCount() != 0)
                throw new InvalidOperationException("Missing/duplicate button or unexpected persistent listener.");
    }

    public static void ValidateAltar(GameObject root)
    {
        if (root.GetComponentsInChildren<RestorationAltar>(true).Length != 1 ||
            root.GetComponentInChildren<HouseRestorationSystem>(true) == null ||
            root.GetComponentInChildren<Collider2D>(true) == null)
            throw new InvalidOperationException("Original Sacred Tree/restoration components must remain.");
        var trigger = root.GetComponentInChildren<InteractionTrigger>(true);
        if (trigger == null || new SerializedObject(trigger).FindProperty("interactionKey").intValue != (int)KeyCode.F ||
            new SerializedObject(trigger).FindProperty("interactionPromptCanvas").objectReferenceValue == null)
            throw new InvalidOperationException("Existing F interaction/prompt references must remain.");
        var proximity = root.GetComponentInChildren<TutorialProximityTrigger>(true);
        if (proximity == null || new SerializedObject(proximity).FindProperty("eventToFire").intValue != 4)
            throw new InvalidOperationException("Sacred Tree must route Daily Loadout proximity.");
    }

    [MenuItem("Tools/Magic Garden/Daily Loadout/Validate Sacred Tree (Stage 4C)")]
    public static void ValidateInstalled()
    {
        ValidateCanvas(AssetDatabase.LoadAssetAtPath<GameObject>(CanvasPath));
        ValidateAltar(AssetDatabase.LoadAssetAtPath<GameObject>(AltarPath));
        if (AssetDatabase.LoadAssetAtPath<TutorialStep>(FindStepPath).objectiveType != TutorialObjectiveType.DailyLoadoutProximity ||
            AssetDatabase.LoadAssetAtPath<TutorialStep>(OpenStepPath).objectiveType != TutorialObjectiveType.OpenDailyLoadout)
            throw new InvalidOperationException("Stage 4C tutorial migration is missing.");
        // Unity 2022.3 supports additive authoring, not OpenPreviewScene. Never replace/save the user's scene.
        var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(GamePath);
        bool openedHere = !scene.IsValid() || !scene.isLoaded;
        if (openedHere) scene = EditorSceneManager.OpenScene(GamePath, OpenSceneMode.Additive);
        try
        {
            var roots = scene.GetRootGameObjects();
            var panels = roots.SelectMany(r => r.GetComponentsInChildren<DailyLoadoutPresenter>(true)).ToArray();
            var altars = roots.SelectMany(r => r.GetComponentsInChildren<RestorationAltar>(true)).ToArray();
            if (panels.Length != 1 || altars.Length != 1)
                throw new InvalidOperationException("Game must contain exactly one loadout panel and the existing Sacred Tree.");
            var owners = roots.SelectMany(r => r.GetComponentsInChildren<UIManager>(true)).ToArray();
            if (owners.Length != 1) throw new InvalidOperationException("Game UI owner is missing or duplicated.");
            ValidateCanvas(owners[0].GetComponentInParent<Canvas>().gameObject);
            ValidateAltar(altars[0].gameObject);
            foreach (var root in roots)
                foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                    if (behaviour == null) throw new InvalidOperationException("Game contains a missing MonoBehaviour reference.");
        }
        finally
        {
            if (openedHere) EditorSceneManager.CloseScene(scene, true);
            if (active.IsValid() && active.isLoaded) UnityEngine.SceneManagement.SceneManager.SetActiveScene(active);
        }
    }
}
