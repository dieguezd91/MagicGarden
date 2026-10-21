using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Explicit native fallback while the Editor endpoint is unavailable. Never runs on import.</summary>
public static class DailyLoadoutHudAuthoring
{
    public const string CanvasPath = "Assets/Prefabs/UI/Canvas/Canvas Game UI.prefab";
    public const string GamePath = "Assets/Scenes/Game.unity";

    [MenuItem("Tools/Magic Garden/Daily Loadout/Wire Equipment HUD (Stage 4D)")]
    public static void Wire()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage() != null)
            throw new InvalidOperationException("Close Play Mode and Prefab Mode before wiring equipment HUD.");
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(CanvasPath);
        if (asset == null) throw new InvalidOperationException("Missing gameplay Canvas prefab.");
        foreach (var component in asset.GetComponentsInChildren<Component>(true))
            if (component != null && (EditorUtility.IsDirty(component) || EditorUtility.IsDirty(component.gameObject)))
                throw new InvalidOperationException("Save/review unsaved gameplay Canvas edits before wiring equipment HUD.");
        if (EditorUtility.IsDirty(asset)) throw new InvalidOperationException("Save/review the gameplay Canvas first.");
        var game = SceneManager.GetSceneByPath(GamePath);
        if (game.IsValid() && game.isDirty) throw new InvalidOperationException("Save/review the dirty Game scene first.");
        ValidateGame(false); // Validate existing scene references before saving anything.
        var root = PrefabUtility.LoadPrefabContents(CanvasPath);
        try
        {
            ConfigureCanvas(root);
            ValidateCanvas(root, true);
            PrefabUtility.SaveAsPrefabAsset(root, CanvasPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        ValidateGame(true);
        Debug.Log("Equipment HUD wired: three existing plant slots on the left, three spell slots on the right. " +
            "No slots, art or inventory resources created/replaced. Run HUD tests and the Game checklist.");
    }

    // Public for unsaved isolated-content tests. Does not save assets or destroy extra slots.
    public static void ConfigureCanvas(GameObject root)
    {
        ValidateCanvas(root, false);
        var owner = root.GetComponentInChildren<UIManager>(true);
        Configure(owner.SeedSlots, "seedSlots");
        Configure(owner.SpellSlots, "spellSlotUIs");
        ConfigureLayout(owner.SeedSlots, -165f);
        ConfigureLayout(owner.SpellSlots, 165f);
    }
    private static void ConfigureLayout(Component controller, float x)
    {
        var rect = (RectTransform)controller.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, 0f);
        rect.sizeDelta = new Vector2(310f, 100f); // Three existing 100-wide slots, spacing 5.
        rect.anchoredPosition = new Vector2(x, 72f); // A 20-unit gap between categories.
        PrefabUtility.RecordPrefabInstancePropertyModifications(rect);
    }
    private static void Configure(Component controller, string field)
    {
        var serialized = new SerializedObject(controller);
        var slots = serialized.FindProperty(field);
        for (int i = 0; i < slots.arraySize; i++)
            ((Component)slots.GetArrayElementAtIndex(i).objectReferenceValue).gameObject.SetActive(i < 3);
        // Trimming serialized references does not delete the nested prefab instances.
        slots.arraySize = 3;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
    public static void ValidateCanvas(GameObject root, bool configured)
    {
        if (root == null || root.GetComponent<Canvas>() == null)
            throw new InvalidOperationException("Expected gameplay Canvas root.");
        var owners = root.GetComponentsInChildren<UIManager>(true);
        var seeds = root.GetComponentsInChildren<SeedSlotsUIController>(true);
        var spells = root.GetComponentsInChildren<SpellSlotsUIController>(true);
        if (owners.Length != 1 || seeds.Length != 1 || spells.Length != 1 ||
            owners[0].SeedSlots != seeds[0] || owners[0].SpellSlots != spells[0])
            throw new InvalidOperationException("Expected one existing seed/spell HUD explicitly bound to UIManager.");
        ValidateSlots(seeds[0], "seedSlots", "seedSlotsCanvasGroup", configured);
        ValidateSlots(spells[0], "spellSlotUIs", "spellSlotsCanvasGroup", configured);
        if (configured)
        {
            var plantRect = (RectTransform)seeds[0].transform;
            var spellRect = (RectTransform)spells[0].transform;
            // Prefab Canvas roots can have zero scale before the native Canvas initializes.
            // Compare in the shared HUD parent's space, not collapsed world coordinates.
            if (plantRect.parent != spellRect.parent ||
                plantRect.localPosition.x + plantRect.rect.xMax * plantRect.localScale.x >=
                spellRect.localPosition.x + spellRect.rect.xMin * spellRect.localScale.x)
                throw new InvalidOperationException("Plant HUD must remain left of the spell HUD without overlap.");
        }
    }
    private static void ValidateSlots(Component controller, string field, string group, bool configured)
    {
        var serialized = new SerializedObject(controller);
        var slots = serialized.FindProperty(field);
        if (slots == null || slots.arraySize < 3 || (configured && slots.arraySize != 3) ||
            serialized.FindProperty(group).objectReferenceValue == null)
            throw new InvalidOperationException("Missing equipment HUD slots/CanvasGroup: " + controller.name);
        var seen = new HashSet<Component>();
        for (int i = 0; i < slots.arraySize; i++)
        {
            var slot = slots.GetArrayElementAtIndex(i).objectReferenceValue as Component;
            if (slot == null || !seen.Add(slot) || !slot.transform.IsChildOf(controller.transform))
                throw new InvalidOperationException("Missing, duplicated or external equipment HUD slot reference.");
            if (configured && !slot.gameObject.activeSelf)
                throw new InvalidOperationException("The three equipment positions must remain visible.");
        }
    }
    [MenuItem("Tools/Magic Garden/Daily Loadout/Validate Equipment HUD (Stage 4D)")]
    public static void ValidateInstalled()
    {
        ValidateCanvas(AssetDatabase.LoadAssetAtPath<GameObject>(CanvasPath), true);
        ValidateGame(true);
    }
    private static void ValidateGame(bool configured)
    {
        var active = SceneManager.GetActiveScene();
        var scene = SceneManager.GetSceneByPath(GamePath);
        bool opened = !scene.IsValid() || !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(GamePath, OpenSceneMode.Additive);
        try
        {
            UIManager owner = null;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var candidate in root.GetComponentsInChildren<UIManager>(true))
                {
                    if (owner != null) throw new InvalidOperationException("Game has multiple UI owners.");
                    owner = candidate;
                }
            if (owner == null || owner.GetComponentInParent<Canvas>() == null)
                throw new InvalidOperationException("Game gameplay Canvas/UIManager references are missing.");
            ValidateCanvas(owner.GetComponentInParent<Canvas>().gameObject, configured);
        }
        finally
        {
            if (opened) EditorSceneManager.CloseScene(scene, true);
            if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active);
        }
    }
}
