using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using static UpgradeUIArtTests;

// Editor test assembly deliberately enters real Play Mode. Reflection preserves the Assembly-CSharp boundary.
public class NightAmbiencePlayModeChecks
{
    private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private const string GameScene = "Assets/Scenes/Game.unity";
    private static readonly string Output = UpgradeUICompactLayoutTests.Output;

    [UnityTearDown] public IEnumerator LeavePlayModeAfterFailure()
    {
        if (EditorApplication.isPlaying) yield return new ExitPlayMode();
    }

    private static Component Find(string type) => (Component)UnityEngine.Object.FindObjectOfType(TypeOf(type));
    private static Type ProjectType(string name, string spec)
    {
        var type = Type.GetType(name + ", Assembly-CSharp", false);
        Assert.That(type, Is.Not.Null, spec + ": a " + name + " component exists in Assembly-CSharp");
        return type;
    }
    private static object Phase(string name) => Enum.Parse(TypeOf("GamePhase"), name);
    private static float Float(object value) => Convert.ToSingle(value);
    private static void InvokePrivate(object target, string name, params object[] args) => target.GetType().GetMethod(name, AnyInstance).Invoke(target, args);
    private static void SetPrivateField(object target, string name, object value) => target.GetType().GetField(name, AnyInstance).SetValue(target, value);

    // Same fixture preparation as UpgradeUIArtPlayModeChecks.SetupGame, without the upgrade systems.
    private static Component SetupDay()
    {
        var tutorial = Find("TutorialManager"); if (tutorial != null) Call(tutorial, "SkipTutorial"); // Test fixture bypass only.
        var flow = Find("GameFlowController"); Call(flow, "SetPhase", Phase("Day"));
        var modal = Property(Find("UIManager"), "Flow");
        var currentModal = Property(modal, "CurrentModal"); if (currentModal.ToString() != "None") Call(modal, "Close", currentModal);
        Time.timeScale = 1;
        return flow;
    }
    private static IEnumerator Frames(int count) { for (int i = 0; i < count; i++) yield return null; }
    // No lambdas before EnterPlayMode: a closure over test locals is gone after the domain reload.
    private static IEnumerator WaitRealtime(float seconds)
    {
        float end = Time.realtimeSinceStartup + seconds;
        while (Time.realtimeSinceStartup < end) yield return null;
    }
    private static Component[] SceneComponents(Type type)
    {
        var scene = SceneManager.GetActiveScene();
        return Resources.FindObjectsOfTypeAll(type).Cast<Component>().Where(c => c.gameObject.scene == scene).ToArray();
    }
    private static void CaptureWorld(Camera camera, string name, int width, int height)
    {
        Directory.CreateDirectory(Output);
        var rt = new RenderTexture(width, height, 24); var previous = RenderTexture.active; var oldTarget = camera.targetTexture;
        var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0); texture.Apply();
            File.WriteAllBytes(Output + name + "-" + width + "x" + height + ".png", texture.EncodeToPNG());
            Debug.Log("NIGHT_AMBIENCE_CAPTURE " + name + "-" + width + "x" + height + " world");
        }
        finally
        {
            camera.targetTexture = oldTarget; RenderTexture.active = previous; rt.Release();
            UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(texture);
        }
    }

    [UnityTest] public IEnumerator NightfallEasesPostExposureWithTheOtherNightValues()
    {
        yield return new EnterPlayMode();
        yield return EditorSceneManager.LoadSceneAsyncInPlayMode(GameScene, new LoadSceneParameters(LoadSceneMode.Single));
        for (int i = 0; i < 15; i++) yield return null;
        SetupDay();
        var controller = Find("DayNightLightController");
        Assert.That(controller, Is.Not.Null, "S1: scene has a DayNightLightController");
        float authoredDuration = Float(Field(controller, "transitionDuration"));

        SetPrivateField(controller, "transitionDuration", 1f); SetPrivateField(controller, "dayExposure", 0f); SetPrivateField(controller, "nightExposure", -0.6f);
        var adjustments = Field(controller, "colorAdjustmentsComponent");
        Assert.That(adjustments, Is.Not.Null, "S1: volume profile has ColorAdjustments");
        var vignetteComponent = Field(controller, "vignetteComponent");
        Assert.That(vignetteComponent, Is.Not.Null, "S1: volume profile has Vignette");
        var postExposure = Field(adjustments, "postExposure");
        var light = Field(controller, "globalLight");
        var vignette = Field(vignetteComponent, "intensity");

        InvokePrivate(controller, "UpdateLightBasedOnGameState", Phase("Day"), false);
        Assert.That(Float(Property(postExposure, "value")), Is.EqualTo(0f).Within(1e-4f), "S1: day snaps exposure to dayExposure");

        InvokePrivate(controller, "UpdateLightBasedOnGameState", Phase("Night"), true);
        yield return Frames(2);
        float mid = Float(Property(postExposure, "value"));
        Assert.That(mid, Is.LessThan(0f).And.GreaterThan(-0.6f), "S1: exposure eases toward night, neither snapped nor untouched (was " + mid + ")");
        yield return WaitRealtime(1.5f);
        Assert.That(Float(Property(postExposure, "value")), Is.EqualTo(-0.6f).Within(1e-4f), "S1: exposure reaches nightExposure");
        Assert.That(Float(Property(light, "intensity")), Is.EqualTo(Float(Field(controller, "nightLightIntensity"))).Within(1e-4f), "S1: global light still eases to nightLightIntensity");
        Assert.That(Float(Property(vignette, "value")), Is.EqualTo(Float(Field(controller, "nightVignetteIntensity"))).Within(1e-4f), "S1: vignette still eases to nightVignetteIntensity");

        InvokePrivate(controller, "UpdateLightBasedOnGameState", Phase("Day"), true);
        yield return WaitRealtime(1.5f);
        Assert.That(Float(Property(postExposure, "value")), Is.EqualTo(0f).Within(1e-4f), "S1: exposure eases back to dayExposure at daybreak");
        InvokePrivate(controller, "UpdateLightBasedOnGameState", Phase("Day"), false); // leave the shared volume profile asset at day values
        Assert.That(authoredDuration, Is.InRange(3f, 4f), "S1: authored transitionDuration is the tuned 3-4 s");
    }

    [UnityTest] public IEnumerator FirefliesOnlyFlyAtNightAndCrittersReturnByDay()
    {
        yield return new EnterPlayMode();
        yield return EditorSceneManager.LoadSceneAsyncInPlayMode(GameScene, new LoadSceneParameters(LoadSceneMode.Single));
        for (int i = 0; i < 15; i++) yield return null;
        var flow = SetupDay();
        // Existing fauna first: Critters must rest at night and come back by day.
        var critters = GameObject.Find("Critters");
        Assert.That(critters, Is.Not.Null, "S3: scene has the 'Critters' object while it is day");
        yield return Frames(3);
        Assert.That(critters.activeInHierarchy, Is.True, "S3: butterflies and birds fly by day");
        Call(flow, "SetPhase", Phase("Night"));
        yield return Frames(3);
        Assert.That(critters.activeInHierarchy, Is.False, "S3: critters rest at night (existing DayOnlyObject)");
        Call(flow, "SetPhase", Phase("Day"));
        yield return Frames(3);
        Assert.That(critters.activeInHierarchy, Is.True, "S3: butterflies and birds come back by day");

        var nightOnly = ProjectType("NightOnlyObject", "S3");
        var owners = SceneComponents(nightOnly);
        Assert.That(owners.Length, Is.GreaterThanOrEqualTo(1), "S3: scene has a NightOnlyObject");
        var targets = owners.Select(o => (GameObject)Field(o, "targetObject")).ToArray();
        foreach (var t in targets) Assert.That(t, Is.Not.Null, "S3: NightOnlyObject has a target");
        var fireflies = targets.SelectMany(t => t.GetComponentsInChildren<ParticleSystem>(true)).Where(p => p.gameObject.name.StartsWith("Firefly")).ToArray();
        Assert.That(fireflies.Length, Is.GreaterThanOrEqualTo(1), "S3: the night target holds Firefly particle systems");

        yield return Frames(3);
        foreach (var t in targets) Assert.That(t.activeInHierarchy, Is.False, "S3: fireflies are hidden by day");

        Call(flow, "SetPhase", Phase("Night"));
        yield return Frames(3);
        foreach (var t in targets) Assert.That(t.activeInHierarchy, Is.True, "S3: fireflies appear at night");
        foreach (var f in fireflies) Assert.That(f.isPlaying, Is.True, "S3: " + f.gameObject.name + " is playing at night");
        Assert.That(critters.activeInHierarchy, Is.False, "S3: critters rest at night (existing DayOnlyObject)");

        // Visibly seen: above the Ground sorting layer and flying around the Player.
        int groundLayer = SortingLayer.GetLayerValueFromName("Ground");
        foreach (var f in fireflies)
        {
            var renderer = f.GetComponent<ParticleSystemRenderer>();
            Assert.That(renderer, Is.Not.Null, "S3: " + f.gameObject.name + " has a ParticleSystemRenderer");
            Assert.That(SortingLayer.GetLayerValueFromID(renderer.sortingLayerID), Is.GreaterThan(groundLayer),
                "S3: " + f.gameObject.name + " is drawn above the Ground sorting layer (was '" + renderer.sortingLayerName + "')");
            Assert.That(renderer.sharedMaterial.shader.name, Does.Contain("Unlit"),
                "S3: " + f.gameObject.name + " glows unlit, so the dark night light does not blacken it");
        }
        var player = GameObject.FindWithTag("Player");
        Assert.That(player, Is.Not.Null, "S3: scene has a Player");
        // No lambdas over locals: their closure is created before EnterPlayMode and lost on the domain reload.
        var nearPlayer = new List<ParticleSystem>();
        foreach (var f in fireflies)
            if (Vector2.Distance(f.transform.position, player.transform.position) <= 10f) nearPlayer.Add(f);
        Assert.That(nearPlayer.Count, Is.GreaterThanOrEqualTo(1), "S3: at least one firefly emitter is within 10 units of the Player");
        yield return WaitRealtime(1f);
        int alive = 0;
        foreach (var f in nearPlayer) alive += f.particleCount;
        Assert.That(alive, Is.GreaterThanOrEqualTo(5), "S3: at least 5 fireflies are flying within 10 units of the Player (was " + alive + ")");
        yield return WaitRealtime(4f); // let the 3.5 s day-to-night light transition finish so the capture shows the real night look
        CaptureWorld(Camera.main, "night-ambience-fireflies", 1280, 720);

        Call(flow, "SetPhase", Phase("Day"));
        yield return Frames(3);
        foreach (var t in targets) Assert.That(t.activeInHierarchy, Is.False, "S3: fireflies disappear again at daybreak");
        Assert.That(critters.activeInHierarchy, Is.True, "S3: butterflies and birds come back by day");
    }
}
