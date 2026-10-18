using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using F = DailyLoadoutUIFixture;

public sealed class DailyLoadoutWorldAuthoringTests
{
    private static Type Authoring => AppDomain.CurrentDomain.GetAssemblies()
        .Single(a => a.GetName().Name == "Assembly-CSharp-Editor").GetType("DailyLoadoutWorldAuthoring", true);
    private static void Invoke(string name, params object[] args) => Authoring.GetMethod(name).Invoke(null, args);
    private const string CanvasPath = "Assets/Prefabs/UI/Canvas/Canvas Game UI.prefab";
    private const string AltarPath = "Assets/Prefabs/Interactables/Restoration Altar.prefab";

    [Test]
    public void NativeCanvasAuthoringIsIdempotentAndPreservesExistingVisualsAndRestoration()
    {
        var root = PrefabUtility.LoadPrefabContents(CanvasPath);
        try
        {
            var transforms = root.GetComponentsInChildren<RectTransform>(true).ToDictionary(t => t,
                t => new[] { t.anchorMin, t.anchorMax, t.anchoredPosition, t.sizeDelta, t.pivot });
            var images = root.GetComponentsInChildren<UnityEngine.UI.Image>(true).ToDictionary(i => i, i => i.sprite);
            var restoration = root.GetComponentsInChildren(F.Production("RestorationAltarUIManager"), true);
            Invoke("ConfigureCanvas", root); Invoke("ValidateCanvas", root);
            int count = root.GetComponentsInChildren<Transform>(true).Length;
            Invoke("ConfigureCanvas", root); Invoke("ValidateCanvas", root);
            Assert.That(root.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(count));
            foreach (var pair in transforms)
                CollectionAssert.AreEqual(pair.Value, new[] { pair.Key.anchorMin, pair.Key.anchorMax,
                    pair.Key.anchoredPosition, pair.Key.sizeDelta, pair.Key.pivot }, pair.Key.name);
            foreach (var pair in images) Assert.That(pair.Key.sprite, Is.SameAs(pair.Value), pair.Key.name);
            CollectionAssert.AreEqual(restoration, root.GetComponentsInChildren(F.Production("RestorationAltarUIManager"), true));
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    [Test]
    public void NativeTreeAuthoringPreservesCollidersPromptFAndAllRestorationOptions()
    {
        var root = PrefabUtility.LoadPrefabContents(AltarPath);
        try
        {
            var collider = root.GetComponentInChildren<Collider2D>(true);
            string collision = EditorJsonUtility.ToJson(collider);
            var trigger = root.GetComponentInChildren(F.Production("InteractionTrigger"), true);
            string interaction = EditorJsonUtility.ToJson(trigger);
            var restoration = root.GetComponentInChildren(F.Production("HouseRestorationSystem"), true);
            string options = EditorJsonUtility.ToJson(restoration);
            Invoke("ConfigureAltar", root); Invoke("ValidateAltar", root);
            Invoke("ConfigureAltar", root); Invoke("ValidateAltar", root);
            Assert.That(EditorJsonUtility.ToJson(collider), Is.EqualTo(collision));
            Assert.That(EditorJsonUtility.ToJson(trigger), Is.EqualTo(interaction));
            Assert.That(EditorJsonUtility.ToJson(restoration), Is.EqualTo(options));
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    [Test]
    public void InstalledGameAndTutorialReferencesAreValid()
    {
        Assert.DoesNotThrow(() => Invoke("ValidateInstalled"),
            "Run Tools/Magic Garden/Daily Loadout/Wire Sacred Tree (Stage 4C) first. This test does not modify production assets.");
        foreach (string name in new[] { "12-FindRestorationAltar", "13-OpenRestoration" })
        {
            var step = AssetDatabase.LoadMainAssetAtPath("Assets/Scriptables/Tutorial/Tutorial Steps/SubObjectives/" + name + ".asset");
            string text = (string)F.Field(step, "instructionText");
            Assert.That(text, Does.Contain("loadout")); Assert.That(text, Does.Not.Contain("heal"));
        }
    }

    [Test]
    public void ExistingEnumValuesRemainStableAndNewObjectivesAreAppended()
    {
        var objectives = F.Production("TutorialObjectiveType");
        Assert.That(Convert.ToInt32(Enum.Parse(objectives, "OpenRestoration")), Is.EqualTo(13));
        Assert.That(Convert.ToInt32(Enum.Parse(objectives, "RestorationProximity")), Is.EqualTo(20));
        Assert.That(Convert.ToInt32(Enum.Parse(objectives, "AbilityChanged")), Is.EqualTo(27));
        Assert.That(Convert.ToInt32(Enum.Parse(objectives, "OpenDailyLoadout")), Is.EqualTo(28));
        Assert.That(Convert.ToInt32(Enum.Parse(objectives, "DailyLoadoutProximity")), Is.EqualTo(29));
    }

    [Test]
    public void TutorialCompletesReachAndOpenObjectivesUsingOnlyLoadoutEvents()
    {
        var go = new GameObject("Isolated tutorial subscription owner"); go.SetActive(false);
        var manager = go.AddComponent(F.Production("TutorialManager"));
        var proximityObject = new GameObject("Isolated tree proximity"); proximityObject.SetActive(false);
        var proximity = proximityObject.AddComponent(F.Production("TutorialProximityTrigger"));
        var player = new GameObject("Tutorial player overlap"); player.tag = "Player";
        var first = ScriptableObject.CreateInstance(F.Production("TutorialStep"));
        var second = ScriptableObject.CreateInstance(F.Production("TutorialStep"));
        try
        {
            var type = F.Production("TutorialObjectiveType");
            F.Set(first, "objectiveType", Enum.Parse(type, "DailyLoadoutProximity"));
            F.Set(second, "objectiveType", Enum.Parse(type, "OpenDailyLoadout"));
            F.Set(first, "stepOrder", 11); F.Set(second, "stepOrder", 12);
            F.Set(manager, "tutorialSteps", F.List("TutorialStep", first, second));
            F.Call(manager, "OnEnable"); F.Call(manager, "StartTutorial");
            F.Call(manager, "ProcessBufferAndEnableInput");
            F.Set(proximity, "eventToFire", Enum.Parse(F.Production("TutorialProximityTrigger+TutorialProximityEvent"), "DailyLoadoutProximity"));
            // Restoration events must not advance the migrated objective.
            F.Production("TutorialEvents").GetMethod("InvokeRestorationProximity").Invoke(null, null);
            Assert.That(F.Call(manager, "GetCurrentObjectiveType"), Is.EqualTo(Enum.Parse(type, "DailyLoadoutProximity")));
            F.Call(proximity, "OnTriggerEnter2D", player.AddComponent<BoxCollider2D>());
            Assert.That(F.Call(manager, "GetCurrentObjectiveType"), Is.EqualTo(Enum.Parse(type, "OpenDailyLoadout")));
            F.Call(manager, "ProcessBufferAndEnableInput");
            F.Production("TutorialEvents").GetMethod("InvokeRestorationClosed").Invoke(null, null);
            Assert.That(F.Call(manager, "IsTutorialActive"), Is.True);
            F.Production("TutorialEvents").GetMethod("InvokeDailyLoadoutOpened").Invoke(null, null);
            Assert.That(F.Call(manager, "IsTutorialActive"), Is.False);
        }
        finally
        {
            F.Call(manager, "OnDisable");
            UnityEngine.Object.DestroyImmediate(go); UnityEngine.Object.DestroyImmediate(proximityObject);
            UnityEngine.Object.DestroyImmediate(player); UnityEngine.Object.DestroyImmediate(first); UnityEngine.Object.DestroyImmediate(second);
        }
    }
}
