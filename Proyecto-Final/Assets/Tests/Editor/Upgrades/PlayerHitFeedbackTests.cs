using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using static UpgradeUIArtTests;

// Player hit feel (hitstop, camera shake, health bar punch, no player damage numbers). Editor test assembly deliberately enters real Play Mode.
// Reflection preserves the Assembly-CSharp boundary. No lambdas capturing locals: closures are lost on the domain reload.
public class PlayerHitFeedbackTests
{
    private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private const string PlayerPrefabPath = "Assets/Prefabs/Characters/Player.prefab";

    [UnityTearDown] public IEnumerator LeavePlayModeAfterFailure()
    {
        if (EditorApplication.isPlaying) { Time.timeScale = 1f; yield return new ExitPlayMode(); }
    }

    private static void Set(object target, string name, object value) => target.GetType().GetField(name, AnyInstance).SetValue(target, value);
    private static float Float(object value) => Convert.ToSingle(value);
    private static IEnumerator Frames(int count) { for (int i = 0; i < count; i++) yield return null; }
    private static IEnumerator WaitRealtime(float seconds)
    {
        float end = Time.realtimeSinceStartup + seconds;
        while (Time.realtimeSinceStartup < end) yield return null;
    }

    // A player stand-in: LifeController (plenty of health, so a hit never kills) plus the feedback component.
    private static Component NewPlayerFeedback()
    {
        var go = new GameObject("TestPlayer");
        var life = go.AddComponent(TypeOf("LifeController"));
        Set(life, "maxHealth", 1000f);
        var feedback = go.AddComponent(TypeOf("PlayerHitFeedback"));
        Set(feedback, "hitStopDuration", 0.1f);
        return feedback;
    }

    // damageType: 0 = SingleTick, 1 = DamageOverTime.
    private static void Hit(Component owner, float amount, int damageType = 0)
    {
        var type = TypeOf("LifeController");
        var life = owner.GetComponent(type);
        type.GetMethod("TakeDamage").Invoke(life, new object[] { amount, Enum.ToObject(type.GetNestedType("DamageType"), damageType), Enum.ToObject(type.GetNestedType("DamageElement"), 0) });
    }

    // ---------- Hitstop (S1, S6) ----------

    [UnityTest] public IEnumerator DamagingHitRequestsHitStopThenTimeScaleReturnsToExactlyOne()
    {
        yield return new EnterPlayMode();
        Time.timeScale = 1f;
        var feedback = NewPlayerFeedback();
        yield return Frames(2); // LifeController.Start fills the health
        Hit(feedback, 10f);
        Assert.That(Time.timeScale, Is.LessThan(1f), "S1: a damaging hit requests a hitstop, so the game slows down");
        Assert.That(Time.timeScale, Is.GreaterThan(0f), "S6: the hitstop never freezes the game completely");
        yield return WaitRealtime(0.4f);
        Assert.That(Time.timeScale, Is.EqualTo(1f), "S6: the exact previous timeScale (1) comes back after the hitstop");
    }

    // ---------- Editable values (S5) ----------

    [UnityTest] public IEnumerator DefaultsAreTheSpecifiedEditableValues()
    {
        yield return new EnterPlayMode();
        var feedback = NewPlayerFeedback();
        Assert.That(Float(Field(feedback, "shakeIntensity")), Is.EqualTo(0.25f), "S5: default shake intensity is 0.25");
        Assert.That(Float(Field(feedback, "shakeDuration")), Is.EqualTo(0.2f), "S5: default shake duration is 0.2");
        Assert.That((bool)Field(feedback, "enableHitStop"), Is.True, "S5: hitstop is on by default");
        Assert.That((bool)Field(feedback, "enableCameraShake"), Is.True, "S5: shake is on by default");
        var fresh = new GameObject("FreshPlayer");
        fresh.AddComponent(TypeOf("LifeController"));
        var defaults = fresh.AddComponent(TypeOf("PlayerHitFeedback"));
        Assert.That(Float(Field(defaults, "hitStopDuration")), Is.EqualTo(0.06f), "S5: default hitstop duration is 0.06");
    }

    [UnityTest] public IEnumerator ZeroValuesDisableEachEffect()
    {
        yield return new EnterPlayMode();
        Time.timeScale = 1f;
        var feedback = NewPlayerFeedback();
        Set(feedback, "hitStopDuration", 0f);
        Set(feedback, "shakeIntensity", 0f);
        yield return Frames(2);
        Hit(feedback, 10f);
        Assert.That(Time.timeScale, Is.EqualTo(1f), "S5: hitStopDuration 0 disables the hitstop");
        yield return WaitRealtime(0.3f);
        Assert.That(Time.timeScale, Is.EqualTo(1f), "S5: the time scale stays 1");
    }

    [UnityTest] public IEnumerator DisabledTogglesDisableEachEffect()
    {
        yield return new EnterPlayMode();
        Time.timeScale = 1f;
        var feedback = NewPlayerFeedback();
        Set(feedback, "enableHitStop", false);
        Set(feedback, "enableCameraShake", false);
        yield return Frames(2);
        Hit(feedback, 10f);
        Assert.That(Time.timeScale, Is.EqualTo(1f), "S5: enableHitStop off disables the hitstop");
        yield return WaitRealtime(0.3f);
        Assert.That(Time.timeScale, Is.EqualTo(1f), "S5: the time scale stays 1");
    }

    [UnityTest] public IEnumerator DamageOverTimeAndTinyHitsGiveNoFeedbackByDefault()
    {
        yield return new EnterPlayMode();
        Time.timeScale = 1f;
        var feedback = NewPlayerFeedback();
        Set(feedback, "minDamage", 1f);
        yield return Frames(2);
        Hit(feedback, 10f, 1); // burn tick
        Assert.That(Time.timeScale, Is.EqualTo(1f), "S5: a damage-over-time tick gives no hitstop by default");
        Hit(feedback, 0.5f);   // below the editable minimum
        Assert.That(Time.timeScale, Is.EqualTo(1f), "S5: a hit under the minimum damage gives no hitstop");
        yield return WaitRealtime(0.3f);
        Assert.That(Time.timeScale, Is.EqualTo(1f), "S5: the time scale is still 1 after ticks and tiny hits");

        Set(feedback, "reactToDamageOverTime", true);
        Hit(feedback, 10f, 1);
        Assert.That(Time.timeScale, Is.LessThan(1f), "S5: enabling reactToDamageOverTime makes ticks give feedback");
        yield return WaitRealtime(0.5f);
    }

    // ---------- Pause (S1, S6) ----------

    [UnityTest] public IEnumerator NoFeedbackWhileThePausedTimeScaleIsZero()
    {
        yield return new EnterPlayMode();
        Time.timeScale = 1f;
        var feedback = NewPlayerFeedback();
        yield return Frames(2);
        Time.timeScale = 0f; // paused
        Hit(feedback, 10f);
        Assert.That(Time.timeScale, Is.EqualTo(0f), "S6: a hit while paused leaves timeScale at 0");
        yield return Frames(2);
        Assert.That(Time.timeScale, Is.EqualTo(0f), "S6: timeScale is still 0, never written by the feedback");
    }

    // ---------- Health bar punch (S1) ----------

    private static Component NewHealthBar(out Component bar, Vector3 baseScale)
    {
        var barObject = new GameObject("TestHealthBar", typeof(RectTransform));
        barObject.transform.localScale = baseScale;
        bar = barObject.AddComponent(Type.GetType("UnityEngine.UI.Slider, UnityEngine.UI", true));
        var controller = new GameObject("TestHealthUI").AddComponent(TypeOf("HealthUIController"));
        Set(controller, "healthBar", bar);
        Set(controller, "punchDuration", 0.4f);
        return controller;
    }

    private static void Health(Component controller, float current) => Call(controller, "UpdatePlayerHealth", current, 100f);

    private static IEnumerator WatchScale(Transform target, Vector3 baseScale, float seconds, float[] deviation)
    {
        float end = Time.realtimeSinceStartup + seconds;
        while (Time.realtimeSinceStartup < end)
        {
            deviation[0] = Mathf.Max(deviation[0], (target.localScale - baseScale).magnitude);
            yield return null;
        }
    }

    [UnityTest] public IEnumerator HealthDecreasePunchesTheBarAndReturnsToTheExactBaseScale()
    {
        yield return new EnterPlayMode();
        Time.timeScale = 1f;
        var baseScale = new Vector3(1.5f, 2f, 1f);
        Component bar;
        var controller = NewHealthBar(out bar, baseScale);
        Health(controller, 100f);
        Health(controller, 60f);
        var deviation = new float[1];
        yield return WatchScale(bar.transform, baseScale, 0.25f, deviation);
        Assert.That(deviation[0], Is.GreaterThan(1e-3f), "S1: losing health punches the bar");
        yield return WaitRealtime(0.8f);
        Assert.That(bar.transform.localScale.x, Is.EqualTo(baseScale.x).Within(1e-6f), "S1: scale x returns to the exact base");
        Assert.That(bar.transform.localScale.y, Is.EqualTo(baseScale.y).Within(1e-6f), "S1: scale y returns to the exact base");
        Assert.That(bar.transform.localScale.z, Is.EqualTo(baseScale.z).Within(1e-6f), "S1: scale z returns to the exact base");
    }

    [UnityTest] public IEnumerator HealthIncreaseDoesNotPunchTheBar()
    {
        yield return new EnterPlayMode();
        Time.timeScale = 1f;
        var baseScale = new Vector3(1.5f, 2f, 1f);
        Component bar;
        var controller = NewHealthBar(out bar, baseScale);
        Health(controller, 50f);
        Health(controller, 90f);
        var deviation = new float[1];
        yield return WatchScale(bar.transform, baseScale, 0.5f, deviation);
        Assert.That(deviation[0], Is.EqualTo(0f), "S1: gaining health leaves the bar scale untouched");
    }

    [UnityTest] public IEnumerator DisablingThePunchLeavesTheBarUntouched()
    {
        yield return new EnterPlayMode();
        Time.timeScale = 1f;
        var baseScale = new Vector3(1.5f, 2f, 1f);
        Component bar;
        var controller = NewHealthBar(out bar, baseScale);
        Set(controller, "punchOnDamage", false);
        Health(controller, 100f);
        Health(controller, 40f);
        var deviation = new float[1];
        yield return WatchScale(bar.transform, baseScale, 0.4f, deviation);
        Assert.That(deviation[0], Is.EqualTo(0f), "S5: punchOnDamage off disables the bar punch");

        Set(controller, "punchOnDamage", true);
        Set(controller, "punchScale", 0f);
        Health(controller, 10f);
        yield return WatchScale(bar.transform, baseScale, 0.4f, deviation);
        Assert.That(deviation[0], Is.EqualTo(0f), "S5: punchScale 0 disables the bar punch");
    }

// ---------- Player received-damage numbers (S7) ----------

    // The FeedbackUIController is built in isolation: Initialize/Setup (player lookup by tag, scene wiring) are skipped
    // and the private fields it needs are stubbed by reflection: the player, a damage-screen CanvasGroup and a
    // floating-damage template. The template is an active child of an inactive root, so the original never updates
    // and Instantiate(template) gives an active, parentless clone (FloatingDamageText.Awake runs and finds its TMP text).
    private static Component NewFeedbackUI(out GameObject player, out CanvasGroup screen)
    {
        player = new GameObject("TestUIPlayer");
        var root = new GameObject("TestFloatingTemplateRoot");
        root.SetActive(false);
        var template = new GameObject("TestFloatingTemplate", typeof(RectTransform));
        template.transform.SetParent(root.transform, false);
        template.AddComponent(TypeOf("FloatingDamageText"));
        template.AddComponent(Type.GetType("TMPro.TextMeshProUGUI, Unity.TextMeshPro", true));

        screen = new GameObject("TestDamagedScreen").AddComponent<CanvasGroup>();
        screen.alpha = 0f;

        var ui = new GameObject("TestFeedbackUI").AddComponent(TypeOf("FeedbackUIController"));
        Set(ui, "player", player);
        Set(ui, "floatingDamagePrefab", template);
        Set(ui, "damagedScreenCanvasGroup", screen);
        return ui;
    }

    private static int PlayerNumbers(GameObject player) => player.GetComponentsInChildren(TypeOf("FloatingDamageText"), true).Length;

    private static float DamageScreenTarget(Component ui) => Float(Field(ui, "targetDamageAlpha"));

    [UnityTest] public IEnumerator PlayerDamageNumbersAreOffByDefaultButTheDamageScreenStillShows()
    {
        yield return new EnterPlayMode();
        Time.timeScale = 1f;
        GameObject player;
        CanvasGroup screen;
        var ui = NewFeedbackUI(out player, out screen);
        Assert.That((bool)Field(ui, "showPlayerDamageNumbers"), Is.False, "S7: player damage numbers default to off");
        Assert.That(DamageScreenTarget(ui), Is.EqualTo(0f), "S7: fixture: no damage screen target before the hit");

        Call(ui, "ShowDamageEffect", 10f, player.transform.position);
        yield return Frames(1); // the floating text (if any) is created synchronously; one frame proves it stays absent

        Assert.That(PlayerNumbers(player), Is.EqualTo(0), "S7: no floating damage number appears over the player");
        Assert.That(GameObject.FindObjectsOfType(TypeOf("FloatingDamageText")).Length, Is.EqualTo(0), "S7: no floating damage number exists anywhere");
        Assert.That(DamageScreenTarget(ui), Is.EqualTo(0.3f + 0.2f * 0.4f).Within(1e-5f), "S7: the damage screen keeps its target alpha (0.3 + damage/50 * 0.4)");

        for (int i = 0; i < 3; i++) { Call(ui, "HandleUpdate"); yield return null; }
        Assert.That(screen.alpha, Is.GreaterThan(0f), "S7: the damage screen alpha still rises after the hit");
        Assert.That(screen.alpha, Is.LessThanOrEqualTo(DamageScreenTarget(ui) + 1e-4f), "S7: the damage screen alpha never overshoots its target");
    }

    [UnityTest] public IEnumerator EnablingPlayerDamageNumbersShowsTheNumberAgain()
    {
        yield return new EnterPlayMode();
        Time.timeScale = 1f;
        GameObject player;
        CanvasGroup screen;
        var ui = NewFeedbackUI(out player, out screen);
        Set(ui, "showPlayerDamageNumbers", true);

        Call(ui, "ShowDamageEffect", 10f, player.transform.position);
        yield return Frames(1);

        Assert.That(PlayerNumbers(player), Is.EqualTo(1), "S7: with the toggle on, one floating damage number appears over the player");
        Assert.That(DamageScreenTarget(ui), Is.EqualTo(0.3f + 0.2f * 0.4f).Within(1e-5f), "S7: the damage screen still works with the toggle on");

        Call(ui, "ShowDamageEffect", 5f, player.transform.position);
        Assert.That(PlayerNumbers(player), Is.EqualTo(1), "S7: a second hit adds to the same number instead of creating another");
    }

    // ---------- Prefab wiring (S1) ----------

    [Test] public void PlayerPrefabHasHitFeedbackOnTheSameObjectAsItsLifeController()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        Assert.That(prefab, Is.Not.Null, "S1: fixture: Player.prefab exists");
        var life = prefab.GetComponent(TypeOf("LifeController"));
        Assert.That(life, Is.Not.Null, "S1: fixture: the Player prefab root has the LifeController");
        var feedback = prefab.GetComponent(TypeOf("PlayerHitFeedback"));
        Assert.That(feedback, Is.Not.Null, "S1: the Player prefab root has a PlayerHitFeedback");
        Assert.That(feedback.gameObject, Is.SameAs(life.gameObject), "S1: PlayerHitFeedback sits on the same GameObject as the LifeController");
        Assert.That(prefab.GetComponents(TypeOf("PlayerHitFeedback")).Length, Is.EqualTo(1), "S1: exactly one PlayerHitFeedback");
    }
}
