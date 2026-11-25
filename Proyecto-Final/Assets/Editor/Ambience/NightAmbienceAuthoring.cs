using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Idempotent authoring for the night ambience: transition tuning and night fireflies around the play area.
/// The Firefly instances get prefab-instance overrides (sorting layer Effects above Ground, slow start speed, prewarm)
/// that are re-applied on every run. There is no player light and the designer-disabled lamps under
/// BaseLevel5/Lights are deliberately left untouched.
/// Run from the menu or headless with: -executeMethod NightAmbienceAuthoring.Apply
/// Other values on components that already exist are never overwritten, so designer tuning survives re-runs.
/// </summary>
public static class NightAmbienceAuthoring
{
    private const string FireflyPrefabPath = "Assets/Prefabs/Scenery/Firefly.prefab";
    private const string GameScenePath = "Assets/Scenes/Game.unity";

    private const string AmbienceRootName = "Night Ambience";
    private const string FirefliesName = "Fireflies";
    private const string FireflySortingLayer = "Effects"; // the prefab's Default layer is drawn below Ground
    private const int FireflySortingOrder = 50;
    private const float FireflyMinSpeed = 0.15f;
    private const float FireflyMaxSpeed = 0.5f;
    // GlowEffect is lit by the 2D lights, so the dark-blue night light turns the fireflies almost black.
    // FireflyGlow reuses GlowEffect's texture with URP's unlit sprite shader so they glow at night.
    private const string GlowMaterialPath = "Assets/Sprites/SpriteSheets/ShaderEffects/GlowEffect.mat";
    private const string FireflyMaterialPath = "Assets/Sprites/SpriteSheets/ShaderEffects/FireflyGlow.mat";
    private const string UnlitSpriteShader = "Universal Render Pipeline/2D/Sprite-Unlit-Default";
    private static readonly Color FireflyColor = new Color(1f, 0.95f, 0.55f, 1f); // warm, like the harvest sparkles
    // Around the play area: the Player spawns near (0.06, 1.45).
    private static readonly Vector3[] FireflyPositions = { new Vector3(-7f, 4f, 0f), new Vector3(7f, 4f, 0f), new Vector3(0f, -4f, 0f) };

    [MenuItem("Tools/Magic Garden/Ambience/Apply night ambience")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Apply the night ambience outside Play Mode.");
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        Scene scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
        ConfigureController(scene);
        int fireflies = ConfigureAmbienceRoot(scene, out int firefliesAdded);

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
            throw new InvalidOperationException("Could not save " + GameScenePath);

        Debug.Log("NIGHT_AMBIENCE applied fireflies=" + fireflies + " firefliesInstantiated=" + firefliesAdded);
    }

    // ---- Game scene ----------------------------------------------------------------------------

    private static void ConfigureController(Scene scene)
    {
        DayNightLightController controller = FindInScene<DayNightLightController>(scene).FirstOrDefault();
        if (controller == null) throw new InvalidOperationException("Game scene has no DayNightLightController.");

        var serialized = new SerializedObject(controller);
        SetFloat(serialized, "transitionDuration", 3.5f);
        SetFloat(serialized, "nightGlobalVolumeIntensity", 2f);
        serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(controller);
    }

    private static int ConfigureAmbienceRoot(Scene scene, out int instantiated)
    {
        GameObject root = scene.GetRootGameObjects().FirstOrDefault(r => r.name == AmbienceRootName);
        if (root == null)
        {
            root = new GameObject(AmbienceRootName);
            SceneManager.MoveGameObjectToScene(root, scene);
        }
        root.SetActive(true);

        Transform firefliesTransform = root.transform.Find(FirefliesName);
        if (firefliesTransform == null)
        {
            var go = new GameObject(FirefliesName);
            go.transform.SetParent(root.transform, false);
            firefliesTransform = go.transform;
        }
        GameObject fireflies = firefliesTransform.gameObject;
        fireflies.SetActive(true); // NightOnlyObject hides it by day at runtime

        NightOnlyObject nightOnly = root.GetComponent<NightOnlyObject>();
        if (nightOnly == null) nightOnly = root.AddComponent<NightOnlyObject>();
        var serialized = new SerializedObject(nightOnly);
        SerializedProperty target = serialized.FindProperty("targetObject");
        if (target == null) throw new InvalidOperationException("NightOnlyObject has no targetObject field.");
        if (target.objectReferenceValue == null)
        {
            target.objectReferenceValue = fireflies;
            serialized.ApplyModifiedProperties();
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FireflyPrefabPath);
        if (prefab == null) throw new InvalidOperationException("Missing prefab " + FireflyPrefabPath);

        instantiated = 0;
        if (FireflyInstances(firefliesTransform, prefab).Count == 0)
        {
            foreach (Vector3 position in FireflyPositions)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, firefliesTransform);
                instance.transform.position = position;
                instantiated++;
            }
        }

        // Re-applied on every run, also to instances that already existed.
        List<GameObject> instances = FireflyInstances(firefliesTransform, prefab);
        foreach (GameObject instance in instances) ApplyFireflyOverrides(instance);

        EditorUtility.SetDirty(root);
        EditorUtility.SetDirty(fireflies);
        return instances.Count;
    }

    private static List<GameObject> FireflyInstances(Transform parent, GameObject prefab)
    {
        return parent.Cast<Transform>()
            .Select(c => c.gameObject)
            .Where(g => PrefabUtility.GetCorrespondingObjectFromSource(g) == prefab)
            .ToList();
    }

    // Instance overrides on the scene's Firefly objects; the prefab asset itself stays untouched.
    private static Material FireflyMaterial()
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(FireflyMaterialPath);
        if (material != null) return material;
        Shader shader = Shader.Find(UnlitSpriteShader);
        if (shader == null) throw new InvalidOperationException("Missing shader " + UnlitSpriteShader);
        material = new Material(shader) { name = "FireflyGlow" };
        var glow = AssetDatabase.LoadAssetAtPath<Material>(GlowMaterialPath);
        if (glow != null && glow.HasProperty("_MainTex")) material.mainTexture = glow.GetTexture("_MainTex");
        AssetDatabase.CreateAsset(material, FireflyMaterialPath);
        return material;
    }

    private static void ApplyFireflyOverrides(GameObject instance)
    {
        Material material = FireflyMaterial();
        foreach (ParticleSystemRenderer renderer in instance.GetComponentsInChildren<ParticleSystemRenderer>(true))
        {
            renderer.sharedMaterial = material;
            renderer.sortingLayerName = FireflySortingLayer;
            renderer.sortingOrder = FireflySortingOrder;
            PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
        }
        foreach (ParticleSystem system in instance.GetComponentsInChildren<ParticleSystem>(true))
        {
            ParticleSystem.MainModule main = system.main;
            main.startSpeed = new ParticleSystem.MinMaxCurve(FireflyMinSpeed, FireflyMaxSpeed);
            main.prewarm = true; // already flying when the night appears
            main.startColor = FireflyColor;
            PrefabUtility.RecordPrefabInstancePropertyModifications(system);
        }
    }

    // ---- Helpers -------------------------------------------------------------------------------

    private static IEnumerable<T> FindInScene<T>(Scene scene) where T : Component
    {
        return scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true));
    }

    private static string HierarchyPath(Transform transform)
    {
        var parts = new List<string>();
        for (Transform t = transform; t != null; t = t.parent)
            parts.Add(t.GetSiblingIndex().ToString("D5") + t.name);
        parts.Reverse();
        return string.Join("/", parts);
    }

    private static void SetFloat(SerializedObject serialized, string name, float value)
    {
        SerializedProperty property = serialized.FindProperty(name);
        if (property == null) throw new InvalidOperationException(serialized.targetObject.GetType().Name + " has no field " + name);
        property.floatValue = value;
    }
}
