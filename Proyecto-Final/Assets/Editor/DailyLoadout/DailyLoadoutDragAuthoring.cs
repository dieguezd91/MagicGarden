using System;
using MagicGarden.LoadoutUI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Additive, explicit prefab authoring. Never rebuilds the approved panel or runs on import.</summary>
public static class DailyLoadoutDragAuthoring
{
    public const string PanelPath = "Assets/Prefabs/UI/DailyLoadout/DailyLoadoutPanel.prefab";
    public const string OptionPath = "Assets/Prefabs/UI/DailyLoadout/LoadoutOption.prefab";

    [MenuItem("Tools/Magic Garden/Daily Loadout/Bind icon drag and drop (preserve design)")]
    public static void BindPrefabs()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage() != null)
            throw new InvalidOperationException("Close Play Mode and Prefab Mode before authoring loadout dragging.");
        CheckClean(OptionPath); CheckClean(PanelPath);
        var option = PrefabUtility.LoadPrefabContents(OptionPath);
        try { ConfigureOption(option.GetComponent<LoadoutOptionView>()); PrefabUtility.SaveAsPrefabAsset(option, OptionPath); }
        finally { PrefabUtility.UnloadPrefabContents(option); }
        var panel = PrefabUtility.LoadPrefabContents(PanelPath);
        try { ConfigurePanel(panel.GetComponent<DailyLoadoutPanelView>()); PrefabUtility.SaveAsPrefabAsset(panel, PanelPath); }
        finally { PrefabUtility.UnloadPrefabContents(panel); }
        Debug.Log("Loadout dragging authored: existing icon-frame sources and one inactive non-raycast preview. Layout and artwork preserved.");
    }

    private static void CheckClean(string path)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (asset == null) throw new InvalidOperationException("Missing existing prefab: " + path);
        foreach (var component in asset.GetComponentsInChildren<Component>(true))
            if (component != null && (EditorUtility.IsDirty(component) || EditorUtility.IsDirty(component.gameObject)))
                throw new InvalidOperationException("Save/review unsaved prefab edits first: " + path);
    }

    public static void ConfigureOption(LoadoutOptionView option)
    {
        var serialized = new SerializedObject(option);
        var icon = (Image)serialized.FindProperty("icon").objectReferenceValue;
        if (icon == null || icon.transform.parent.GetComponent<Image>() == null)
            throw new InvalidOperationException("Expected the existing option icon frame.");
        var frame = icon.transform.parent;
        frame.GetComponent<Image>().raycastTarget = true;
        var source = frame.GetComponent<LoadoutOptionDragSource>();
        if (source == null) source = frame.gameObject.AddComponent<LoadoutOptionDragSource>();
        serialized.FindProperty("dragSource").objectReferenceValue = source;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    public static void ConfigurePanel(DailyLoadoutPanelView view)
    {
        var preview = view.transform.Find("Drag Preview") as RectTransform;
        if (preview == null)
        {
            var go = new GameObject("Drag Preview", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            preview = (RectTransform)go.transform; preview.SetParent(view.transform, false);
            preview.anchorMin = preview.anchorMax = preview.pivot = new Vector2(.5f, .5f);
            preview.sizeDelta = new Vector2(96, 96);
        }
        var image = preview.GetComponent<Image>(); var group = preview.GetComponent<CanvasGroup>();
        if (image == null || group == null) throw new InvalidOperationException("Existing preview has an unexpected structure.");
        image.preserveAspect = true; image.raycastTarget = false; image.color = new Color(1, 1, 1, .92f); image.sprite = null;
        group.interactable = false; group.blocksRaycasts = false;
        preview.gameObject.layer = view.gameObject.layer;
        preview.SetAsLastSibling(); preview.gameObject.SetActive(false);
        var serialized = new SerializedObject(view);
        serialized.FindProperty("dragPreview").objectReferenceValue = preview;
        serialized.FindProperty("dragPreviewIcon").objectReferenceValue = image;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
