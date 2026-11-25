using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using static UpgradeUIArtTests;

// odd/tasks/enemy-attack-feel.md T6: Infernum pacing retuned to 1 s and "strongest shake wins".
// Reflection keeps the Assembly-CSharp boundary of the core test assembly.
public class EnemyAttackTuningTests
{
    private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static readonly string[] InfernumAssets =
    {
        "Assets/Scriptables/Enemies/Infernum.asset",
        "Assets/Scriptables/Enemies/Infernum Variant B.asset",
        "Assets/Scriptables/Enemies/Infernum Variant C.asset",
    };

    [UnityTearDown] public IEnumerator LeavePlayModeAfterFailure()
    {
        if (EditorApplication.isPlaying) yield return new ExitPlayMode();
    }

    private static float Float(object value) => Convert.ToSingle(value);
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, AnyInstance).SetValue(target, value);
    private static float CurrentIntensity(Component shaker) => Float(shaker.GetType().GetField("currentIntensity", AnyInstance).GetValue(shaker));
    private static IEnumerator Frames(int count) { for (int i = 0; i < count; i++) yield return null; }
    private static IEnumerator WaitRealtime(float seconds)
    {
        float end = Time.realtimeSinceStartup + seconds;
        while (Time.realtimeSinceStartup < end) yield return null;
    }

    [TestCaseSource(nameof(InfernumAssets))]
    public void InfernumFiresOncePerSecondAfterTheRetune(string path)
    {
        var data = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
        Assert.That(data, Is.Not.Null, "S3: " + path + " exists");
        Assert.That(Float(Field(data, "fireRate")), Is.EqualTo(1f).Within(1e-4f),
            "S3: " + path + " keeps the previous 1 s pacing now that FireRate (seconds between shots) is honored");
    }

    // S1: every enemy calls its own shake right after TakeDamage; a weaker request must not cut a stronger running shake.
    [UnityTest] public IEnumerator AWeakerShakeDoesNotReplaceAStrongerRunningOne()
    {
        yield return new EnterPlayMode();
        var go = new GameObject("Shaker fixture");
        var shaker = go.AddComponent(TypeOf("CameraShaker"));
        Set(shaker, "shakeCurve", AnimationCurve.Constant(0f, 1f, 1f));
        Time.timeScale = 1f;

        Call(shaker, "Shake", 1f, 1f);
        yield return Frames(2);
        Call(shaker, "Shake", 0.2f, 1f);
        yield return Frames(2);
        Assert.That(CurrentIntensity(shaker), Is.GreaterThan(0.5f), "S1: the stronger shake keeps running");

        Call(shaker, "Shake", 2f, 0.3f);
        yield return Frames(2);
        Assert.That(CurrentIntensity(shaker), Is.GreaterThan(1.5f), "S1: a stronger request replaces the running shake");

        yield return WaitRealtime(0.6f);
        Assert.That(CurrentIntensity(shaker), Is.EqualTo(0f), "S1: the shake ended");
        Call(shaker, "Shake", 0.2f, 0.5f);
        yield return Frames(2);
        Assert.That(CurrentIntensity(shaker), Is.GreaterThan(0f).And.LessThanOrEqualTo(0.2f), "S1: a weak shake runs when nothing stronger is active");
        UnityEngine.Object.Destroy(go);
    }

    [UnityTest] public IEnumerator DisablingStrongestWinsRestoresLastCallWins()
    {
        yield return new EnterPlayMode();
        var go = new GameObject("Shaker fixture");
        var shaker = go.AddComponent(TypeOf("CameraShaker"));
        Set(shaker, "shakeCurve", AnimationCurve.Constant(0f, 1f, 1f));
        Set(shaker, "strongestShakeWins", false);
        Time.timeScale = 1f;

        Call(shaker, "Shake", 1f, 1f);
        yield return Frames(2);
        Call(shaker, "Shake", 0.2f, 1f);
        yield return Frames(2);
        Assert.That(CurrentIntensity(shaker), Is.GreaterThan(0f).And.LessThanOrEqualTo(0.2f), "S5: with the toggle off the last call wins, as before");
        UnityEngine.Object.Destroy(go);
    }
}
