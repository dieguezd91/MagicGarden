using System;
using MagicGarden.LoadoutUI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Touches only existing catalog card internals and grid bindings; never rebuilds the user's layout.</summary>
public static class DailyLoadoutCatalogCardAuthoring
{
    [MenuItem("Tools/Magic Garden/Daily Loadout/Refine catalog cards (preserve manual layout)")]
    public static void BindPrefabs()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage() != null)
            throw new InvalidOperationException("Save and close Play/Prefab Mode before refining catalog cards.");
        CheckClean(DailyLoadoutDragAuthoring.OptionPath); CheckClean(DailyLoadoutDragAuthoring.PanelPath);
        var option = PrefabUtility.LoadPrefabContents(DailyLoadoutDragAuthoring.OptionPath);
        try
        {
            ConfigureCard(option.GetComponent<LoadoutOptionView>());
            PrefabUtility.SaveAsPrefabAsset(option, DailyLoadoutDragAuthoring.OptionPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(option); }
        var panel = PrefabUtility.LoadPrefabContents(DailyLoadoutDragAuthoring.PanelPath);
        try
        {
            ConfigurePanel(panel.GetComponent<DailyLoadoutPanelView>());
            PrefabUtility.SaveAsPrefabAsset(panel, DailyLoadoutDragAuthoring.PanelPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(panel); }
    }

    public static void ConfigurePanel(DailyLoadoutPanelView view)
    {
        foreach (var option in view.PlantOptionViews) ConfigureCard(option);
        foreach (var option in view.SpellOptionViews) ConfigureCard(option);
        var data = new SerializedObject(view);
        BindGrid((RectTransform)data.FindProperty("optionContent").objectReferenceValue,
            (ScrollRect)data.FindProperty("optionsScroll").objectReferenceValue, view.OptionPrefab);
        BindGrid((RectTransform)data.FindProperty("spellOptionContent").objectReferenceValue,
            (ScrollRect)data.FindProperty("spellOptionsScroll").objectReferenceValue, view.OptionPrefab);
    }

    private static void ConfigureCard(LoadoutOptionView option)
    {
        var data = new SerializedObject(option);
        var state = (TMP_Text)data.FindProperty("stateLabel").objectReferenceValue;
        var name = (TMP_Text)data.FindProperty("nameLabel").objectReferenceValue;
        var icon = (Image)data.FindProperty("icon").objectReferenceValue;
        // Retain serialized references and shared styling, but never display an extra catalog caption.
        state.gameObject.SetActive(false);
        var frame = (RectTransform)icon.transform.parent;
        frame.anchorMin = frame.anchorMax = new Vector2(.5f, .75f); frame.pivot = new Vector2(.5f, .5f);
        frame.anchoredPosition = Vector2.zero; frame.sizeDelta = new Vector2(64, 64);
        var label = (RectTransform)name.transform;
        label.anchorMin = Vector2.zero; label.anchorMax = new Vector2(1, .32f); label.pivot = new Vector2(.5f, 0);
        label.anchoredPosition = new Vector2(0, 7); label.sizeDelta = new Vector2(-12, -8);
        name.alignment = TextAlignmentOptions.Center; name.enableWordWrapping = true;
    }

    private static void BindGrid(RectTransform content, ScrollRect scroll, LoadoutOptionView template)
    {
        var layout = content.GetComponent<LoadoutOptionGridLayout>();
        if (layout == null || scroll == null || template == null)
            throw new InvalidOperationException("Expected existing catalog layout/scroll/card references.");
        var data = new SerializedObject(layout);
        data.FindProperty("grid").objectReferenceValue = content.GetComponent<GridLayoutGroup>();
        data.FindProperty("optionTemplate").objectReferenceValue = template.transform;
        data.FindProperty("scroll").objectReferenceValue = scroll;
        data.ApplyModifiedPropertiesWithoutUndo();
        layout.RefreshLayout(); LayoutRebuilder.ForceRebuildLayoutImmediate(content); layout.RefreshLayout();
    }

    private static void CheckClean(string path)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (asset == null) throw new InvalidOperationException("Missing authored prefab: " + path);
        foreach (var component in asset.GetComponentsInChildren<Component>(true))
            if (component != null && (EditorUtility.IsDirty(component) || EditorUtility.IsDirty(component.gameObject)))
                throw new InvalidOperationException("Save/review unsaved prefab edits before refining: " + path);
    }
}
