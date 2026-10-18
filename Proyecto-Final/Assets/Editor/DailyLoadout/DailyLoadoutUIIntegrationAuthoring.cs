using System;
using MagicGarden.LoadoutUI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Additive authoring only; never calls the Stage 4A generator or changes visual properties.</summary>
public static class DailyLoadoutUIIntegrationAuthoring
{
    public const int OptionCapacity = 6; // Maximum distinct identities per category in the current catalog.
    private const string PanelPath = DailyLoadoutUIPrefabAuthoring.Folder + "/DailyLoadoutPanel.prefab";

    [MenuItem("Tools/Magic Garden/Daily Loadout/Bind functional UI (preserve visuals)")]
    public static void BindPrefab()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Configure the loadout prefab outside Play Mode.");
        var root = PrefabUtility.LoadPrefabContents(PanelPath);
        try
        {
            Configure(root.GetComponent<DailyLoadoutPanelView>());
            PrefabUtility.SaveAsPrefabAsset(root, PanelPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    /// <summary>Also usable by isolated tests on unsaved instances. Explicit capacity, no runtime UI creation.</summary>
    public static void Configure(DailyLoadoutPanelView view)
    {
        if (view == null || view.OptionContent == null || view.OptionPrefab == null)
            throw new InvalidOperationException("Existing Stage 4A view references are required.");
        if (view.GetComponent<DailyLoadoutPresenter>() == null) view.gameObject.AddComponent<DailyLoadoutPresenter>();
        // Preserve already-authored pools, including intentional larger capacities.
        if (view.OptionCapacity >= OptionCapacity) return;
        var serialized = new SerializedObject(view);
        var pool = serialized.FindProperty("optionViews");
        int existing = pool.arraySize;
        pool.arraySize = OptionCapacity;
        for (int i = existing; i < OptionCapacity; i++)
        {
            var option = (GameObject)PrefabUtility.InstantiatePrefab(view.OptionPrefab.gameObject, view.OptionContent);
            option.name = "Catalog option " + (i + 1);
            option.SetActive(false);
            pool.GetArrayElementAtIndex(i).objectReferenceValue = option.GetComponent<LoadoutOptionView>();
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    [MenuItem("Tools/Magic Garden/Daily Loadout/Open isolated functional harness")]
    public static void OpenHarness()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Create the isolated harness outside Play Mode.");
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        // New unsaved scene only. Never open or save Game, or wire any altar/interactable.
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var canvas = new GameObject("Isolated loadout canvas", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
        var panel = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PanelPath), canvas.transform);
        Configure(panel.GetComponent<DailyLoadoutPanelView>());
        panel.SetActive(false);
        new GameObject("Event system", typeof(EventSystem), typeof(StandaloneInputModule));
        var flow = new GameObject("Isolated game flow").AddComponent<GameFlowController>();
        var days = new GameObject("Isolated day cycle").AddComponent<DayCycleController>();
        var modal = new GameObject("Isolated UI flow").AddComponent<UIFlowController>();
        // Configure while inactive so Awake sees the actual progression asset on entering Play Mode.
        var player = new GameObject("Isolated progression owner"); player.SetActive(false);
        var xp = player.AddComponent<PlayerExperienceSystem>();
        var serialized = new SerializedObject(xp);
        serialized.FindProperty("progressionData").objectReferenceValue = AssetDatabase.LoadAssetAtPath<ExperienceProgressionDataSO>(
            "Assets/Scriptables/Progression/Experience Progression.asset");
        serialized.ApplyModifiedPropertiesWithoutUndo();
        var unlocks = player.AddComponent<PlayerContentUnlockSystem>();
        var harness = new GameObject("Daily loadout harness").AddComponent<DailyLoadoutPlayModeHarness>();
        var refs = new SerializedObject(harness);
        Set(refs, "presenter", panel.GetComponent<DailyLoadoutPresenter>());
        Set(refs, "owner", unlocks); Set(refs, "experience", xp); Set(refs, "flow", flow);
        Set(refs, "days", days); Set(refs, "modals", modal); refs.ApplyModifiedPropertiesWithoutUndo();
        player.SetActive(true);
        Selection.activeGameObject = harness.gameObject;
        Debug.Log("Unsaved isolated harness ready. Enter Play Mode; use its Open / Night / New day / Reset controls. Game scene and altar are unchanged.");
    }

    private static void Set(SerializedObject target, string name, UnityEngine.Object value)
        => target.FindProperty(name).objectReferenceValue = value;
}
