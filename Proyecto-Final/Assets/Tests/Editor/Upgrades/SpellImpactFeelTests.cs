using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using static UpgradeUIArtTests;

// Spell combat feel (hitstop, shake, enemy punch, kill pop). Editor test assembly deliberately enters real Play Mode.
// Reflection preserves the Assembly-CSharp boundary. No lambdas capturing locals: closures are lost on the domain reload.
public class SpellImpactFeelTests
{
    private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    [UnityTearDown] public IEnumerator LeavePlayModeAfterFailure()
    {
        if (EditorApplication.isPlaying) { Time.timeScale = 1f; yield return new ExitPlayMode(); }
    }

    private static void Request(float duration) => TypeOf("HitStop").GetMethod("Request").Invoke(null, new object[] { duration, 0.05f });
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, AnyInstance).SetValue(target, value);
    private static float Float(object value) => Convert.ToSingle(value);
    private static IEnumerator Frames(int count) { for (int i = 0; i < count; i++) yield return null; }
    private static IEnumerator WaitRealtime(float seconds)
    {
        float end = Time.realtimeSinceStartup + seconds;
        while (Time.realtimeSinceStartup < end) yield return null;
    }
    private static Component Add(GameObject go, string type) => go.AddComponent(TypeOf(type));
    private static object Method(object target, string name, params object[] args) => target.GetType().GetMethod(name, AnyInstance).Invoke(target, args);

    private static Component NewEnemy(Vector3 scale)
    {
        var go = new GameObject("TestEnemy");
        go.transform.localScale = scale;
        go.AddComponent<SpriteRenderer>();
        go.AddComponent<BoxCollider2D>();
        var life = Add(go, "LifeController");
        Set(life, "isEnemy", true);
        return life;
    }

    private static void Hit(Component life, float amount)
    {
        var type = TypeOf("LifeController");
        type.GetMethod("TakeDamage").Invoke(life, new object[] { amount, Enum.ToObject(type.GetNestedType("DamageType"), 0), Enum.ToObject(type.GetNestedType("DamageElement"), 0) });
    }

    private static void AssertScale(Transform t, Vector3 expected, string message)
    {
        Assert.That(t.localScale.x, Is.EqualTo(expected.x).Within(1e-6f), message + " (x)");
        Assert.That(t.localScale.y, Is.EqualTo(expected.y).Within(1e-6f), message + " (y)");
        Assert.That(t.localScale.z, Is.EqualTo(expected.z).Within(1e-6f), message + " (z)");
    }

    // ---------- HitStop (S1, S6, S7) ----------

    [UnityTest] public IEnumerator HitStopSlowsTimeThenRestoresExactlyTheScaleItReplaced()
    {
        yield return new EnterPlayMode();
        Time.timeScale = 1f;
        yield return Frames(2);
        Request(0.1f);
        Assert.That(Time.timeScale, Is.EqualTo(0.05f).Within(1e-6f), "S1: a hitstop request slows the game right away");
        yield return WaitRealtime(0.35f);
        Assert.That(Time.timeScale, Is.EqualTo(1f), "S7: the exact previous timeScale (1) comes back after the stop");
    }

    [UnityTest] public IEnumerator HitStopRestoresANonDefaultScaleExactly()
    {
        yield return new EnterPlayMode();
        Time.timeScale = 0.75f;
        yield return Frames(2);
        Request(0.1f);
        Assert.That(Time.timeScale, Is.LessThan(0.75f), "S7: the stop is slower than the current scale");
        yield return WaitRealtime(0.35f);
        Assert.That(Time.timeScale, Is.EqualTo(0.75f), "S7: restores exactly the scale it replaced, not a hardcoded 1");
    }

    [UnityTest] public IEnumerator HitStopNeverOverridesAPausedTimeScale()
    {
        yield return new EnterPlayMode();
        Time.timeScale = 0f;
        yield return Frames(2);
        Request(0.1f);
        Assert.That(Time.timeScale, Is.EqualTo(0f), "S7: a paused game (timeScale 0) is left untouched by the request");
        yield return WaitRealtime(0.3f);
        Assert.That(Time.timeScale, Is.EqualTo(0f), "S7: the pause is still 0 after the stop would have ended");
    }

    [UnityTest] public IEnumerator HitStopLeavesATimeScaleChangedByAnotherSystemDuringTheStop()
    {
        yield return new EnterPlayMode();
        Time.timeScale = 1f;
        yield return Frames(2);
        Request(0.4f);
        yield return Frames(2);
        Assert.That(Time.timeScale, Is.LessThan(1f), "S7: the stop is running");
        Time.timeScale = 0.5f; // another system took over
        yield return WaitRealtime(0.7f);
        Assert.That(Time.timeScale, Is.EqualTo(0.5f), "S7: a timeScale set meanwhile by someone else is not overwritten");
    }

    [UnityTest] public IEnumerator HitStopDoesNotLeaveTheSlowScaleBehindWhenAPauseCapturedIt()
    {
        yield return new EnterPlayMode();
        Time.timeScale = 1f;
        yield return Frames(2);
        Request(0.5f);
        yield return Frames(2);
        Time.timeScale = 0f;          // pause opens while the stop runs (it captured 0.05 as "previous")
        yield return Frames(2);
        Time.timeScale = 0.05f;       // pause closes and restores what it captured
        yield return Frames(3);
        Assert.That(Time.timeScale, Is.EqualTo(1f), "S7: the game does not stay stuck in slow motion after pause/resume during a stop");
    }

    [UnityTest] public IEnumerator HitStopMinimumIntervalPreventsChainingRapidHits()
    {
        yield return new EnterPlayMode();
        Time.timeScale = 1f;
        yield return Frames(2);
        Request(0.05f);
        while (Time.timeScale < 1f) yield return null; // resumes right after the frame in which the stop ended
        Request(0.05f);
        Assert.That(Time.timeScale, Is.EqualTo(1f), "S7: a request right after a stop ended is ignored, so ticks cannot chain stops");
        yield return WaitRealtime(0.25f);
        Request(0.05f);
        Assert.That(Time.timeScale, Is.LessThan(1f), "S7: after the minimum interval a new stop is allowed");
    }

    [UnityTest] public IEnumerator HitStopWithZeroDurationDoesNothing()
    {
        yield return new EnterPlayMode();
        Time.timeScale = 1f;
        yield return Frames(2);
        Request(0f);
        Assert.That(Time.timeScale, Is.EqualTo(1f), "S6: duration 0 disables the hitstop");
    }

    // ---------- Spell shake / kill feel (S1, S4, S6) ----------

    [UnityTest] public IEnumerator SpellDefaultsKeepTheFormerShakeAndAddKillValues()
    {
        yield return new EnterPlayMode();
        var go = new GameObject("TestSpell");
        var spell = Add(go, "BasicAreaSpell");
        Assert.That(Float(Field(spell, "impactShakeIntensity")), Is.EqualTo(0.3f), "S1: default impact shake intensity is the former hardcoded 0.3");
        Assert.That(Float(Field(spell, "impactShakeDuration")), Is.EqualTo(0.25f), "S1: default impact shake duration is the former hardcoded 0.25");
        Assert.That(Float(Field(spell, "hitStopDuration")), Is.GreaterThan(0f), "S1: a short hitstop is on by default");
        Assert.That(Float(Field(spell, "killShakeIntensity")), Is.GreaterThan(Float(Field(spell, "impactShakeIntensity"))), "S4: a kill shakes harder");
        Assert.That(Float(Field(spell, "killHitStopDuration")), Is.GreaterThan(Float(Field(spell, "hitStopDuration"))), "S4: a kill freezes longer");
        Assert.That(Float(Field(spell, "killShakeDuration")), Is.GreaterThan(0f), "S4: a kill shake lasts");
    }

    [UnityTest] public IEnumerator BasicAreaSpellExplosionDefaultsKeepTheFormerShake()
    {
        yield return new EnterPlayMode();
        var spell = Add(new GameObject("TestAreaSpell"), "BasicAreaSpell");
        Assert.That(Float(Field(spell, "explosionShakeIntensity")), Is.EqualTo(0.5f), "S1: explosion shake intensity is the former hardcoded 0.5");
        Assert.That(Float(Field(spell, "explosionShakeDuration")), Is.EqualTo(0.4f), "S1: explosion shake duration is the former hardcoded 0.4");
        Assert.That(Float(Field(spell, "explosionHitStopDuration")), Is.GreaterThan(0f), "S1: the explosion has a short hitstop by default");
    }

    [UnityTest] public IEnumerator SpellImpactFeelUsesKillValuesOnlyWhenTheHitKills()
    {
        yield return new EnterPlayMode();
        var spell = Add(new GameObject("TestSpell"), "BasicAreaSpell");
        var normal = Method(spell, "GetImpactFeel", false);
        var kill = Method(spell, "GetImpactFeel", true);
        Assert.That(Float(Field(normal, "shakeIntensity")), Is.EqualTo(Float(Field(spell, "impactShakeIntensity"))), "S1: normal hit uses the impact shake intensity");
        Assert.That(Float(Field(normal, "shakeDuration")), Is.EqualTo(Float(Field(spell, "impactShakeDuration"))), "S1: normal hit uses the impact shake duration");
        Assert.That(Float(Field(normal, "hitStop")), Is.EqualTo(Float(Field(spell, "hitStopDuration"))), "S1: normal hit uses the hitstop duration");
        Assert.That(Float(Field(kill, "shakeIntensity")), Is.EqualTo(Float(Field(spell, "killShakeIntensity"))), "S4: a kill uses the kill shake intensity");
        Assert.That(Float(Field(kill, "shakeDuration")), Is.EqualTo(Float(Field(spell, "killShakeDuration"))), "S4: a kill uses the kill shake duration");
        Assert.That(Float(Field(kill, "hitStop")), Is.EqualTo(Float(Field(spell, "killHitStopDuration"))), "S4: a kill uses the kill hitstop duration");
    }

    [UnityTest] public IEnumerator OnlyAKillingHitTriggersTheKillHitStopAndZeroDisablesIt()
    {
        yield return new EnterPlayMode();
        Time.timeScale = 1f;
        var spell = Add(new GameObject("TestSpell"), "BasicAreaSpell");
        var life = NewEnemy(Vector3.one);
        Set(life, "maxHealth", 1000f);
        Set(life, "isEnemy", false); // the kill must not destroy the fixture object
        yield return Frames(2); // LifeController.Start fills the health
        var collider = life.GetComponent<Collider2D>();
        Set(spell, "hitStopDuration", 0f);           // S6: no stop on a normal hit
        Set(spell, "killHitStopDuration", 0.3f);
        Set(spell, "damage", 1f);

        Method(spell, "ApplyDamage", collider);
        Assert.That((bool)Call(life, "IsAlive"), Is.True, "S4: fixture: the first hit does not kill");
        Assert.That(Time.timeScale, Is.EqualTo(1f), "S6: hitStopDuration 0 means a normal hit does not slow the game");

        Set(spell, "damage", 5000f);
        Method(spell, "ApplyDamage", collider);
        Assert.That((bool)Call(life, "IsAlive"), Is.False, "S4: fixture: the second hit kills");
        Assert.That(Time.timeScale, Is.LessThan(1f), "S4: the killing hit triggers the kill hitstop");
        yield return WaitRealtime(0.6f);
        Assert.That(Time.timeScale, Is.EqualTo(1f), "S7: the game returns to normal speed after the kill stop");
    }

    // ---------- Enemy punch scale (S1, S6) ----------

    [UnityTest] public IEnumerator EnemyPunchesOnDamageAndReturnsToTheExactOriginalScale()
    {
        yield return new EnterPlayMode();
        Time.timeScale = 1f;
        var original = new Vector3(2f, 3f, 1f);
        var life = NewEnemy(original);
        Set(life, "punchMinInterval", 0f);
        Set(life, "maxHealth", 1000f);
        yield return Frames(2);

        Hit(life, 1f);
        float maxDeviation = 0f;
        for (int i = 0; i < 8; i++)
        {
            yield return null;
            maxDeviation = Mathf.Max(maxDeviation, Mathf.Abs(life.transform.localScale.x - original.x));
            if (i == 3) Hit(life, 1f); // a second hit mid-punch must not drift the scale
        }
        Assert.That(maxDeviation, Is.GreaterThan(0.01f), "S1: the hit punches the enemy's scale");
        yield return WaitRealtime(0.6f);
        AssertScale(life.transform, original, "S1: the enemy returns to its exact original scale");
    }

    [UnityTest] public IEnumerator EnemyPunchRestoresScaleWhenTheComponentIsDisabledMidPunch()
    {
        yield return new EnterPlayMode();
        var original = new Vector3(1.5f, 1.5f, 1f);
        var life = NewEnemy(original);
        Set(life, "maxHealth", 1000f);
        yield return Frames(2);
        Hit(life, 1f);
        yield return Frames(3);
        ((Behaviour)life).enabled = false;
        AssertScale(life.transform, original, "S1: disabling mid-punch restores the original scale");
    }

    [UnityTest] public IEnumerator EnemyPunchCanBeDisabledWithTheBoolAndWithZeroScale()
    {
        yield return new EnterPlayMode();
        Time.timeScale = 1f;
        var original = new Vector3(2f, 2f, 1f);
        var off = NewEnemy(original);
        Set(off, "punchOnDamage", false);
        var zero = NewEnemy(original);
        Set(zero, "punchScale", 0f);
        yield return Frames(2);
        Hit(off, 1f);
        Hit(zero, 1f);
        for (int i = 0; i < 5; i++)
        {
            yield return null;
            AssertScale(off.transform, original, "S6: punchOnDamage = false leaves the scale untouched");
            AssertScale(zero.transform, original, "S6: punchScale = 0 leaves the scale untouched");
        }
    }

    // ---------- Floating damage text kill pop (S4, S6) ----------

    private static string TextOf(Component damageText) => (string)Property(damageText.GetComponentInChildren(TypeOf("TMPro.TMP_Text", "Unity.TextMeshPro")), "text");

    private static Component NewDamageText(Component target, bool popOnKill)
    {
        var go = new GameObject("TestDamageText");
        var textGo = new GameObject("Text");
        textGo.transform.SetParent(go.transform, false);
        textGo.AddComponent(TypeOf("TMPro.TextMeshProUGUI", "Unity.TextMeshPro"));
        var text = Add(go, "FloatingDamageText");
        Set(text, "popOnKill", popOnKill);
        Call(text, "Initialize", target.transform);
        return text;
    }

    [UnityTest] public IEnumerator DamageNumberPopsBiggerWhenTheTargetDies()
    {
        yield return new EnterPlayMode();
        Time.timeScale = 1f;
        var alive = NewEnemy(Vector3.one);
        var dead = NewEnemy(Vector3.one);
        Set(alive, "isEnemy", false); // Die has no side effects on a plain object
        Set(dead, "isEnemy", false);
        yield return Frames(2);
        Call(dead, "Kill");
        var aliveText = NewDamageText(alive, true);
        var deadText = NewDamageText(dead, true);
        Call(aliveText, "AddDamage", 20f);
        Call(deadText, "AddDamage", 20f);
        yield return null;
        float normal = aliveText.transform.localScale.x;
        float popped = deadText.transform.localScale.x;
        Assert.That(popped, Is.GreaterThan(normal * 1.2f), "S4: the number of the killing hit is visibly bigger (" + popped + " vs " + normal + ")");
        Assert.That(TextOf(deadText), Is.EqualTo("20"), "S4: the accumulated number is still shown");
        Call(deadText, "AddDamage", 5f);
        Assert.That(TextOf(deadText), Is.EqualTo("25"), "S4: damage keeps accumulating on the same text");
    }

    [UnityTest] public IEnumerator DamageNumberKillPopCanBeDisabled()
    {
        yield return new EnterPlayMode();
        Time.timeScale = 1f;
        var alive = NewEnemy(Vector3.one);
        var dead = NewEnemy(Vector3.one);
        Set(alive, "isEnemy", false);
        Set(dead, "isEnemy", false);
        yield return Frames(2);
        Call(dead, "Kill");
        var aliveText = NewDamageText(alive, true);
        var deadText = NewDamageText(dead, false);
        Call(aliveText, "AddDamage", 20f);
        Call(deadText, "AddDamage", 20f);
        yield return null;
        Assert.That(deadText.transform.localScale.x, Is.EqualTo(aliveText.transform.localScale.x).Within(1e-4f), "S6: popOnKill = false keeps the normal size");
    }
}
