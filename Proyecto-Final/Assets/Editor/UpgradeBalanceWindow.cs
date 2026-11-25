using System;
using System.IO;
using MagicGarden;
using UnityEditor;
using UnityEngine;

internal sealed class UpgradeBalanceWindow : EditorWindow
{
    private const string AssetPath = "Assets/Scriptables/Upgrades/Resources/MagicGardenBalance.json";

    [SerializeField] private Balance balance;
    [SerializeField] private string loadedText;
    [SerializeField] private string savedDraft;
    [SerializeField] private int tab;
    private Vector2 scroll;
    private string error;

    private static string FullPath => Path.Combine(Application.dataPath, "..", AssetPath);

    [MenuItem("Tools/Magic Garden/Upgrade Balance")]
    private static void Open()
    {
        var window = GetWindow<UpgradeBalanceWindow>("Upgrade Balance");
        window.minSize = new Vector2(440, 300);
    }

    private void OnEnable()
    {
        saveChangesMessage = "Save changes to MagicGardenBalance.json?";
        if (loadedText == null || balance == null) Reload();
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox("Save to update MagicGardenBalance.json. Changes take effect the next time the game scene loads.", MessageType.Info);
        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(!hasUnsavedChanges || EditorApplication.isPlayingOrWillChangePlaymode))
                if (GUILayout.Button("Save")) SaveChanges();

            if (GUILayout.Button("Reload") && (!hasUnsavedChanges ||
                EditorUtility.DisplayDialog("Reload Balance", "Discard unsaved edits and reload from disk?", "Reload", "Cancel")))
            {
                GUI.FocusControl(null);
                Reload();
            }
        }

        if (!string.IsNullOrEmpty(error)) EditorGUILayout.HelpBox(error, MessageType.Error);
        if (balance == null) return;

        var draft = new SerializedObject(this);
        var root = draft.FindProperty(nameof(balance));
        tab = GUILayout.Toolbar(tab, new[] { "Stats", "Rarities", "Scaling", "Specials", "General" });
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
        using (var view = new EditorGUILayout.ScrollViewScope(scroll))
        {
            scroll = view.scrollPosition;
            switch (tab)
            {
                case 0: DrawEntries(root.FindPropertyRelative("stats"), "stat"); break;
                case 1:
                    EditorGUILayout.LabelField("Bonus is a fraction: 0.10 means +10% before scaling.", EditorStyles.wordWrappedLabel);
                    DrawEntries(root.FindPropertyRelative("rarities"), "rarity");
                    break;
                case 2: DrawEntries(root.FindPropertyRelative("profiles"), "capability", DrawProfile); break;
                case 3: DrawEntries(root.FindPropertyRelative("specials"), "displayName", DrawSpecial); break;
                case 4:
                    Field(root, "lateralAngle");
                    Field(root, "pierceGraceSeconds");
                    Field(root, "seedsOnPlantUnlock");
                    break;
            }
        }
        draft.ApplyModifiedProperties();
        hasUnsavedChanges = JsonUtility.ToJson(balance) != savedDraft;
    }

    private static void DrawEntries(SerializedProperty list, string labelField, Action<SerializedProperty> draw = null)
    {
        list.arraySize = Mathf.Max(0, EditorGUILayout.DelayedIntField("Count", list.arraySize));
        for (int i = 0; i < list.arraySize; i++)
        {
            var entry = list.GetArrayElementAtIndex(i);
            var field = entry.FindPropertyRelative(labelField);
            string label = field.propertyType == SerializedPropertyType.Enum &&
                field.enumValueIndex >= 0 && field.enumValueIndex < field.enumDisplayNames.Length
                ? field.enumDisplayNames[field.enumValueIndex]
                : field.propertyType == SerializedPropertyType.String ? field.stringValue : "";
            var title = new GUIContent(string.IsNullOrWhiteSpace(label) ? $"Entry {i + 1}" : label);
            if (draw == null)
            {
                EditorGUILayout.PropertyField(entry, title, true);
                continue;
            }

            entry.isExpanded = EditorGUILayout.Foldout(entry.isExpanded, title, true);
            if (entry.isExpanded)
                using (new EditorGUI.IndentLevelScope()) draw(entry);
        }
    }

    private static void DrawProfile(SerializedProperty profile)
    {
        Field(profile, "capability");
        Field(profile, "targetId");
        var ratios = profile.FindPropertyRelative("ratios");
        foreach (Stat stat in Enum.GetValues(typeof(Stat)))
        {
            if (RunState.Additive(stat)) continue;
            int index = (int)stat;
            float current = index < ratios.arraySize ? ratios.GetArrayElementAtIndex(index).floatValue : 0;
            float next = EditorGUILayout.FloatField(ObjectNames.NicifyVariableName(stat.ToString()), current);
            if (next.Equals(current)) continue;
            if (index >= ratios.arraySize)
            {
                int previousSize = ratios.arraySize;
                ratios.arraySize = index + 1;
                for (int i = previousSize; i <= index; i++) ratios.GetArrayElementAtIndex(i).floatValue = 0;
            }
            ratios.GetArrayElementAtIndex(index).floatValue = next;
        }
    }

    private static void DrawSpecial(SerializedProperty special)
    {
        Field(special, "id");
        Field(special, "capability");
        Field(special, "displayName");
        Field(special, "description");
        Field(special, "maxStacks");
        Field(special, "prerequisites");
        Field(special, "effects");

        DrawCosts(special);
        var overrides = special.FindPropertyRelative("costOverrides");
        overrides.isExpanded = EditorGUILayout.Foldout(overrides.isExpanded, "Target and Purchase Cost Overrides", true);
        if (!overrides.isExpanded) return;
        using (new EditorGUI.IndentLevelScope())
        {
            EditorGUILayout.LabelField("An exact target ID and prior stack count (0 for the first purchase) replace the default costs.",
                EditorStyles.wordWrappedLabel);
            DrawEntries(overrides, "targetId", entry =>
            {
                Field(entry, "targetId");
                Field(entry, "priorStacks");
                DrawCosts(entry);
            });
        }
    }

    private static void DrawCosts(SerializedProperty parent)
    {
        var costs = parent.FindPropertyRelative("costs");
        costs.isExpanded = EditorGUILayout.Foldout(costs.isExpanded, "Costs", true);
        if (!costs.isExpanded) return;
        using (new EditorGUI.IndentLevelScope())
        {
            costs.arraySize = Mathf.Max(0, EditorGUILayout.DelayedIntField("Count", costs.arraySize));
            for (int i = 0; i < costs.arraySize; i++)
            {
                var cost = costs.GetArrayElementAtIndex(i);
                var material = cost.FindPropertyRelative("material");
                material.intValue = (int)(MaterialType)EditorGUILayout.EnumPopup("Material", (MaterialType)material.intValue);
                Field(cost, "amount");
            }
        }
    }

    private static void Field(SerializedProperty parent, string name) =>
        EditorGUILayout.PropertyField(parent.FindPropertyRelative(name), true);

    private void Reload()
    {
        try
        {
            string text = File.ReadAllText(FullPath);
            var parsed = JsonUtility.FromJson<Balance>(text);
            if (parsed == null || parsed.stats == null || parsed.rarities == null ||
                parsed.profiles == null || parsed.specials == null)
                throw new InvalidDataException("The JSON must contain stats, rarities, profiles, and specials.");

            balance = parsed;
            loadedText = text;
            savedDraft = JsonUtility.ToJson(balance);
            base.DiscardChanges();
            error = null;
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException)
        {
            error = ex.Message;
        }
    }

    public override void DiscardChanges()
    {
        if (!string.IsNullOrEmpty(savedDraft)) balance = JsonUtility.FromJson<Balance>(savedDraft);
        base.DiscardChanges();
    }

    public override void SaveChanges()
    {
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before saving.");
            string text = JsonUtility.ToJson(balance, true) + Environment.NewLine;
            WriteBalance(FullPath, loadedText, text);
            loadedText = text;
            savedDraft = JsonUtility.ToJson(balance);
            base.SaveChanges();
            error = null;
            AssetDatabase.ImportAsset(AssetPath);
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is InvalidOperationException)
        {
            error = ex.Message;
        }
    }

    internal static void WriteBalance(string path, string expected, string replacement)
    {
        if (File.ReadAllText(path) != expected)
            throw new IOException("The balance changed on disk. Reload before saving to avoid overwriting those changes.");

        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, replacement);
            File.Replace(temporary, path, null);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}
