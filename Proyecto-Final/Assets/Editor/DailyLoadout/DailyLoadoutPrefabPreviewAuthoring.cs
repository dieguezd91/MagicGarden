using System;
using System.Linq;
using MagicGarden.LoadoutUI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Bakes real catalog examples into existing prefab components. Never runs at runtime or on import.</summary>
public static class DailyLoadoutPrefabPreviewAuthoring
{
    [MenuItem("Tools/Magic Garden/Daily Loadout/Populate authored prefab preview")]
    public static void PopulatePrefab()
    {
        RequireEditMode();
        if (PrefabStageUtility.GetCurrentPrefabStage() != null)
            throw new InvalidOperationException("Save and close Prefab Mode before refreshing the saved preview.");
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(DailyLoadoutDragAuthoring.PanelPath);
        foreach (var component in asset.GetComponentsInChildren<Component>(true))
            if (component != null && (EditorUtility.IsDirty(component) || EditorUtility.IsDirty(component.gameObject)))
                throw new InvalidOperationException("Save/review unsaved loadout prefab edits before refreshing preview data.");
        var root = PrefabUtility.LoadPrefabContents(DailyLoadoutDragAuthoring.PanelPath);
        try
        {
            Populate(root.GetComponent<DailyLoadoutPanelView>());
            PrefabUtility.SaveAsPrefabAsset(root, DailyLoadoutDragAuthoring.PanelPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    public static void Populate(DailyLoadoutPanelView view)
    {
        RequireEditMode();
        if (view == null || view.PlantSlots.Count != 3 || view.SpellSlots.Count != 3 || view.OptionCapacity == 0)
            throw new InvalidOperationException("Preview requires the existing bound equipment and option components.");
        // Preview examples are not equipment defaults, unlocks, inventory or run state.
        var plants = Assets<PlantDataSO>().Where(p => p.seedType != SeedsEnum.None && p.plantIcon != null &&
            !string.IsNullOrWhiteSpace(p.plantName)).GroupBy(p => p.seedType).Select(g => g.First()).ToArray();
        var spells = Assets<SpellDataSO>().Where(s => s.SpellIcon != null && !string.IsNullOrWhiteSpace(s.SpellName)).ToArray();
        if (plants.Length == 0 || spells.Length == 0)
            throw new InvalidOperationException("Existing plant and spell art/name assets are required for prefab preview.");

        for (int i = 0; i < view.PlantOptionViews.Count; i++)
        {
            var plant = i < plants.Length ? plants[i] : null;
            var option = view.PlantOptionViews[i];
            Fill(option, plant != null ? plant.plantIcon : null, plant != null ? plant.plantName : null,
                plant != null && i < 3, false);
            option.SetLocked(false); option.gameObject.SetActive(true);
        }
        for (int i = 0; i < view.SpellOptionViews.Count; i++)
        {
            var spell = i < spells.Length ? spells[i] : null;
            var option = view.SpellOptionViews[i];
            Fill(option, spell != null ? spell.SpellIcon : null, spell != null ? spell.SpellName : null,
                spell != null && i < 3, false);
            option.SetLocked(false); option.gameObject.SetActive(true);
        }
        for (int i = 0; i < 3; i++)
        {
            var plant = i < plants.Length ? plants[i] : null;
            var spell = i < spells.Length ? spells[i] : null;
            Fill(view.PlantSlots[i], plant != null ? plant.plantIcon : null, plant != null ? plant.plantName : null, plant != null, i == 0);
            Fill(view.SpellSlots[i], spell != null ? spell.SpellIcon : null, spell != null ? spell.SpellName : null, spell != null, false);
        }
        view.ShowCategory(true);
        view.SetSelection(true, 0, view.PlantSlots[0].IconSprite, view.PlantSlots[0].DisplayName, "Selected");
        view.SetAllowanceMessage("1 CHANGE PER DAY\nAVAILABLE");
        view.SetFeedbackMessage("Changes remain a draft until you confirm your Loadout.");
        view.SetOptionsEmpty(false); view.SetActionAvailability(false, false); view.HideDragPreview();
        Debug.Log("Saved prefab preview uses existing components: " + view.PlantOptionViews.Count + " plant options, " +
            view.SpellOptionViews.Count + " spell options. Preview examples never initialize a run loadout.");
    }

    private static void Fill(LoadoutEquipmentView target, Sprite sprite, string name, bool assigned, bool selected)
    {
        target.SetContent(sprite, name); target.SetAssigned(assigned); target.SetSelected(selected);
        target.SetHighlighted(false); target.SetInvalidDropTarget(false); target.SetDropFeedback(false, false, false);
        target.SetReadOnly(false);
    }

    private static T[] Assets<T>() where T : ScriptableObject => AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { "Assets" })
        .Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p, StringComparer.Ordinal)
        .Select(AssetDatabase.LoadAssetAtPath<T>).Where(a => a != null).ToArray();

    private static void RequireEditMode()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Prefab preview authoring is Editor-only and cannot run in Play Mode.");
    }
}
