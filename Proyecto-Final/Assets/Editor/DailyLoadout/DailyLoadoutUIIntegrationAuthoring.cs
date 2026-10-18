using System;
using MagicGarden.LoadoutUI;
using UnityEditor;
using UnityEngine;

/// <summary>Additive authoring and isolated-test support; preserves existing visual properties.</summary>
public static class DailyLoadoutUIIntegrationAuthoring
{
    public const int OptionCapacity = 6; // Maximum distinct identities per category in the current catalog.
    private const string PanelPath = "Assets/Prefabs/UI/DailyLoadout/DailyLoadoutPanel.prefab";

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

}
