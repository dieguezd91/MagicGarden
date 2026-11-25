#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public class RitualPresentationTests
{
    private static readonly Vector2 Center = new Vector2(2f, 0f);
    private const string RitualPrefabPath = "Assets/Prefabs/Interactables/Ritual.prefab";
    private static Type RuntimeType(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Single(a => a.GetName().Name == "Assembly-CSharp").GetType(name, true);

    private readonly List<GameObject> objects = new List<GameObject>();
    private readonly Dictionary<PropertyInfo, object> singletons = new Dictionary<PropertyInfo, object>();
    private Component flow;
    private Component world;
    private Component dayCycle;
    private Component altar;
    private Component movement;
    private Component trigger;
    private Rigidbody2D body;
    private Animator animator;
    private GameObject hand;
    private Component doorLight;
    private Component[] candles;
    private EventInfo ritualUsed;
    private Action onRitualUsed;
    private int ritualUses;

    private string Phase => Get(flow, "CurrentPhase").ToString();
    private int Nights => (int)Get(dayCycle, "TotalNights");
    private bool CanInteract => (bool)Call(altar, "CanInteract");
    private bool InState(string name) => animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer." + name);

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        foreach (string name in new[] { "GameFlowController", "LevelManager", "DayCycleController", "TutorialManager" })
        {
            var instance = RuntimeType(name).GetProperty("Instance");
            singletons.Add(instance, instance.GetValue(null));
            instance.SetValue(null, null);
        }

        flow = Create("Ritual test flow").AddComponent(RuntimeType("GameFlowController"));
        dayCycle = flow.gameObject.AddComponent(RuntimeType("DayCycleController"));
        flow.gameObject.AddComponent(RuntimeType("LevelManager"));
        world = flow.gameObject.AddComponent(RuntimeType("WorldTransitionAnimator"));
        Set(world, "useFade", false);
        Set(world, "autoFindHouse", false);
        Set(world, "autoFindInteriorLights", false);

        var player = Create("Ritual test player");
        player.tag = "Player";
        player.AddComponent<SpriteRenderer>();
        player.AddComponent<CircleCollider2D>().radius = 0.1f;
        body = player.AddComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        body.constraints = RigidbodyConstraints2D.FreezeRotation;
        // The production hierarchy exists when the Animator binds the clip's PlayerHand curve.
        hand = Create("PlayerHand");
        hand.transform.SetParent(player.transform, false);
        animator = player.AddComponent<Animator>();
        animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
            "Assets/Animations/Player/Player Body.controller");
        Assert.That(animator.runtimeAnimatorController, Is.Not.Null);
        // The fixture intentionally has no SoundManager; the real clips still animate normally.
        animator.fireEvents = false;
        player.AddComponent(RuntimeType("LifeController"));
        movement = player.AddComponent(RuntimeType("PlayerMovementController"));

        var altarObject = Create("Ritual test altar");
        altarObject.transform.position = Center;
        var zone = altarObject.AddComponent<BoxCollider2D>();
        zone.isTrigger = true;
        zone.size = new Vector2(5f, 1f);
        altar = altarObject.AddComponent(RuntimeType("RitualAltar"));
        Set(altar, "canRestoreHealth", false);
        var visual = altarObject.AddComponent(RuntimeType("RitualVisualController"));
        var door = Create("Ritual test light");
        door.transform.SetParent(altarObject.transform, false);
        var lightType = AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType("UnityEngine.Rendering.Universal.Light2D")).First(t => t != null);
        doorLight = door.AddComponent(lightType);
        Set(visual, "DoorLight", doorLight);
        Set(visual, "ritualLightFadeDuration", 0.6f);
        Set(altar, "ritualDuration", 1.6f);
        var assignedLights = Array.CreateInstance(lightType, 4);
        candles = new Component[assignedLights.Length];
        for (int i = 0; i < candles.Length; i++)
        {
            var candle = Create($"Ritual test candle {i}");
            candle.transform.SetParent(altarObject.transform, false);
            candles[i] = candle.AddComponent(lightType);
            assignedLights.SetValue(candles[i], i);
        }
        Set(visual, "candleLights", assignedLights);
        trigger = altarObject.AddComponent(RuntimeType("InteractionTrigger"));
        Set(trigger, "hidePromptDuringInteraction", true);
        Set(trigger, "completeSound", null);

        ritualUses = 0;
        ritualUsed = RuntimeType("TutorialEvents").GetEvent("OnRitualAltarUsed");
        onRitualUsed = () => ritualUses++;
        ritualUsed.AddEventHandler(null, onRitualUsed);
        yield return new WaitForFixedUpdate();
        yield return null;
        Assert.That(Phase, Is.EqualTo("Day"));
        Assert.That(CanInteract, Is.True);
        Call(world, "EnterHouse");
    }

    [TearDown]
    public void TearDown()
    {
        ritualUsed?.RemoveEventHandler(null, onRitualUsed);
        for (int i = objects.Count - 1; i >= 0; i--)
            if (objects[i] != null) Object.DestroyImmediate(objects[i]);
        objects.Clear();
        foreach (var saved in singletons)
            saved.Key.SetValue(null, saved.Value);
        singletons.Clear();
    }

    [UnityTest]
    public IEnumerator WalksToCenterThenMeditatesThroughTheNightTransitionAndFade()
    {
        Assert.That((bool)Call(trigger, "TryInteract"), Is.True);
        yield return Until(() => InState("Walk"), "Walk never played on approach.");
        Assert.That(body.position.x, Is.LessThan(Center.x));
        Assert.That(Phase, Is.EqualTo("Day"));
        AssertLitCandles(0);
        AssertExitCueHidden();
        Assert.That(CanInteract, Is.False);
        Assert.That((bool)Call(trigger, "TryInteract"), Is.False);
        Call(altar, "Interact");

        yield return Until(() => InState("Meditate"), "Meditate never started at the altar.");
        Assert.That(Phase, Is.EqualTo("OnRitual"));
        Assert.That(Vector2.Distance(body.position, Center), Is.LessThan(0.001f));
        Assert.That(hand.activeSelf, Is.False);
        Assert.That(Nights, Is.Zero);

        yield return Until(() => animator.GetCurrentAnimatorStateInfo(0).normalizedTime >= 1f,
            "Meditation did not reach its held pose.");
        Assert.That(InState("Meditate"), Is.True);
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).loop, Is.False);
        yield return Until(() => Phase == "Night", "The normal ritual did not start night.");
        body.AddForce(Vector2.right * 10f, ForceMode2D.Impulse);
        yield return new WaitForFixedUpdate();
        Assert.That(Vector2.Distance(body.position, Center), Is.LessThan(0.001f));
        Assert.That(InState("Meditate"), Is.True, "Night starts before the ending fade finishes.");
        Assert.That((bool)Get(movement, "IsMovementEnabled"), Is.False);
        Assert.That(Nights, Is.EqualTo(1));
        Assert.That(ritualUses, Is.EqualTo(1));

        // Sample the entire anchored period; the cue also contains a sprite, so zero intensity alone is insufficient.
        float earliestRelease = Time.time + 0.25f;
        yield return Until(() =>
        {
            if (!(bool)Get(movement, "IsRitualMovementActive")) return true;
            AssertExitCueHidden();
            Assert.That(InState("Meditate"), Is.True);
            Assert.That(Vector2.Distance(body.position, Center), Is.LessThan(0.001f));
            return false;
        }, "The ending fade did not release the player.");
        Assert.That(Time.time, Is.GreaterThanOrEqualTo(earliestRelease));
        yield return Until(() => InState("Idle") && doorLight.gameObject.activeSelf && Intensity(doorLight) > 0f,
            "The released player never received the exit cue.");
        Assert.That(hand.activeSelf, Is.True);
        Assert.That(body.constraints, Is.EqualTo(RigidbodyConstraints2D.FreezeRotation));
        Assert.That((bool)Get(movement, "IsMovementEnabled"), Is.True);
        Assert.That(CanInteract, Is.False, "Night must continue to disallow ritual interaction.");
        Call(world, "ExitHouse");
        AssertExitCueHidden();
        yield return Until(() => candles.All(candle => !candle.gameObject.activeSelf),
            "The candles did not extinguish after leaving the house.");
        AssertExitCueHidden();
    }

    [UnityTest]
    public IEnumerator ShippedMeditationPointIsMovableAndUsedAsTheDestination()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RitualPrefabPath);
        var prefabAltar = prefab.GetComponent(RuntimeType("RitualAltar"));
        var point = prefab.transform.Find("MeditationPoint");
        Assert.That(point, Is.Not.Null);
        Assert.That(new SerializedObject(prefabAltar).FindProperty("meditationPoint").objectReferenceValue, Is.EqualTo(point));

        var movedPoint = Create(point.name).transform;
        movedPoint.SetParent(altar.transform, false);
        movedPoint.localPosition = point.localPosition + new Vector3(-0.5f, 0.25f, 0f);
        Set(altar, "meditationPoint", movedPoint);
        Assert.That((bool)Call(trigger, "TryInteract"), Is.True);
        yield return Until(() => InState("Meditate"), "Meditation never started at the moved point.");
        Assert.That(Vector2.Distance(body.position, movedPoint.position), Is.LessThan(0.001f));
    }

    [UnityTest]
    public IEnumerator CandlesIgniteInOrderAcrossMeditationAndFlickerSoftly()
    {
        const float meditationDuration = 2.4f;
        Set(altar, "ritualDuration", meditationDuration);
        body.position = Center;
        AssertLitCandles(0);
        Assert.That((bool)Call(trigger, "TryInteract"), Is.True);
        yield return Until(() => Phase == "OnRitual", "Meditation did not begin.");
        float entered = Time.time;
        AssertLitCandles(1);
        Color violet = (Color)Get(candles[0], "color");
        Assert.That(violet.b, Is.GreaterThan(violet.r));
        Assert.That(violet.r, Is.GreaterThan(violet.g));
        Assert.That(violet.g, Is.GreaterThan(0.15f));
        float firstSample = Intensity(candles[0]);

        for (int i = 1; i < candles.Length; i++)
        {
            int index = i;
            yield return Until(() => candles[index].gameObject.activeSelf, $"Candle {index + 1} did not ignite.");
            AssertLitCandles(i + 1);
            Assert.That(Time.time - entered,
                Is.EqualTo(meditationDuration * i / candles.Length).Within(0.2f));
            Assert.That(Phase, Is.EqualTo("OnRitual"));
            AssertExitCueHidden();
        }

        float laterSample = Intensity(candles[0]);
        Assert.That(Mathf.Abs(laterSample - firstSample), Is.InRange(0.001f, 0.2f));
    }

    [UnityTest]
    public IEnumerator ShippedCandleLightsHaveLocalVioletFalloff()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RitualPrefabPath);
        var visual = new SerializedObject(prefab.GetComponent(RuntimeType("RitualVisualController")));
        var lights = visual.FindProperty("candleLights");
        Assert.That(lights.arraySize, Is.EqualTo(4));
        for (int i = 0; i < lights.arraySize; i++)
        {
            var light = (Component)lights.GetArrayElementAtIndex(i).objectReferenceValue;
            Assert.That(light.gameObject.activeSelf, Is.False);
            Assert.That(Get(light, "lightType").ToString(), Is.EqualTo("Point"));
            Assert.That((float)Get(light, "pointLightOuterRadius"), Is.InRange(0.3f, 0.8f));
            Assert.That((float)Get(light, "pointLightInnerRadius"), Is.LessThan(0.15f));
            Assert.That(Intensity(light), Is.InRange(0.3f, 1f));
            Color violet = (Color)Get(light, "color");
            Assert.That(violet.b, Is.GreaterThan(violet.r));
            Assert.That(violet.r, Is.GreaterThan(violet.g));
        }
        yield break;
    }

    [UnityTest]
    public IEnumerator ExitCueWaitsForTutorialPermissionEvenAfterRelease()
    {
        body.position = Center;
        Assert.That((bool)Call(trigger, "TryInteract"), Is.True);
        yield return Until(() => Phase == "Night", "The ritual did not start night.");
        var tutorial = Create("Ritual test tutorial").AddComponent(RuntimeType("TutorialManager"));
        var gatedStep = ScriptableObject.CreateInstance(RuntimeType("TutorialStep"));
        try
        {
            gatedStep.GetType().GetField("isGatedStep").SetValue(gatedStep, true);
            Set(tutorial, "tutorialActive", true);
            Set(tutorial, "currentStep", gatedStep);
            yield return Until(() => InState("Idle"), "The tutorial gate kept the player anchored.");
            Assert.That((bool)Get(movement, "IsRitualMovementActive"), Is.False);
            AssertExitCueHidden();

            gatedStep.GetType().GetField("isGatedStep").SetValue(gatedStep, false);
            yield return Until(() => doorLight.gameObject.activeSelf && Intensity(doorLight) > 0f,
                "The exit cue did not appear when the tutorial allowed exit.");
        }
        finally
        {
            Object.DestroyImmediate(gatedStep);
        }
    }

    [UnityTest]
    public IEnumerator CenteredRitualSkipsWalkingAndForceStopReleasesPlayer()
    {
        body.position = Center;
        Call(altar, "Interact");
        yield return Until(() => InState("Meditate"), "A centered player should start meditating without a walk.");
        Assert.That(Vector2.Distance(body.position, Center), Is.LessThan(0.001f));
        Assert.That(Phase, Is.EqualTo("OnRitual"));
        Call(altar, "ForceStopRitual");
        yield return Until(() => InState("Idle"), "Force stop did not exit Meditate.");
        Assert.That(hand.activeSelf, Is.True);
        Assert.That(body.constraints, Is.EqualTo(RigidbodyConstraints2D.FreezeRotation));
        Assert.That(body.velocity, Is.EqualTo(Vector2.zero));
        Assert.That(Phase, Is.EqualTo("OnRitual"), "The force-end-night caller still owns the phase transition.");
        AssertLitCandles(0);
        AssertExitCueHidden();
        Assert.That(Nights, Is.Zero);
        Assert.That(ritualUses, Is.Zero);
    }

    [UnityTest]
    public IEnumerator NonNightRitualReleasesWithoutStartingNight()
    {
        Set(altar, "canTransitionToNight", false);
        Set(altar, "ritualDuration", 0.1f);
        body.position = Center;
        Assert.That((bool)Call(trigger, "TryInteract"), Is.True);
        yield return Until(() => InState("Meditate"), "The no-night ritual did not start meditation.");
        Assert.That(Phase, Is.EqualTo("OnRitual"));
        yield return Until(() => CanInteract && InState("Idle"), "The no-night ritual did not release.");
        Assert.That(Phase, Is.EqualTo("Day"));
        AssertLitCandles(0);
        AssertExitCueHidden();
        Assert.That(Nights, Is.Zero);
        Assert.That(ritualUses, Is.Zero);
        Assert.That((bool)Get(movement, "IsMovementEnabled"), Is.True);
    }

    [UnityTest]
    public IEnumerator BlockedWalkTimesOutWithoutStartingTheRitualAndCanRetry()
    {
        var wall = Create("Ritual test wall");
        wall.transform.position = Vector2.right;
        wall.AddComponent<BoxCollider2D>().size = new Vector2(0.2f, 2f);
        Assert.That((bool)Call(trigger, "TryInteract"), Is.True);
        yield return Until(() => InState("Walk"), "The blocked approach did not try to walk.");
        yield return Until(() => CanInteract && InState("Idle"), "Blocked approach did not release.");
        Assert.That(body.position.x, Is.LessThan(1f), "A blocked approach must not teleport through a wall.");
        Assert.That(body.velocity, Is.EqualTo(Vector2.zero));
        Assert.That(body.constraints, Is.EqualTo(RigidbodyConstraints2D.FreezeRotation));
        Assert.That(Phase, Is.EqualTo("Day"));
        Assert.That(Nights, Is.Zero);
        Assert.That(ritualUses, Is.Zero);
        wall.SetActive(false);
        Assert.That((bool)Call(trigger, "TryInteract"), Is.True, "The actual interaction trigger must allow retry.");
        yield return Until(() => InState("Meditate"), "The unobstructed retry failed.");
    }

    [UnityTest]
    public IEnumerator DisablingEitherParticipantReleasesTheRitual()
    {
        Assert.That((bool)Call(trigger, "TryInteract"), Is.True);
        yield return Until(() => InState("Walk"), "Expected the approach before disabling the altar.");
        ((Behaviour)altar).enabled = false;
        yield return Until(() => InState("Idle"), "Disabling the altar did not stop the walk.");
        Assert.That(Phase, Is.EqualTo("Day"));
        Assert.That(CanInteract, Is.False);
        Assert.That(body.velocity, Is.EqualTo(Vector2.zero));

        ((Behaviour)altar).enabled = true;
        body.position = Center;
        Assert.That((bool)Call(trigger, "TryInteract"), Is.True);
        yield return Until(() => InState("Meditate"), "Expected meditation before disabling player movement.");
        ((Behaviour)movement).enabled = false;
        yield return Until(() => InState("Idle") && Phase == "Day", "Disabling player movement did not release the ritual.");
        Assert.That(body.constraints, Is.EqualTo(RigidbodyConstraints2D.FreezeRotation));
        Assert.That(Nights, Is.Zero);
        Assert.That(ritualUses, Is.Zero);
    }

    private void AssertLitCandles(int count)
    {
        for (int i = 0; i < candles.Length; i++)
            Assert.That(candles[i].gameObject.activeSelf, Is.EqualTo(i < count), $"Candle {i + 1}");
    }

    private void AssertExitCueHidden()
    {
        Assert.That(doorLight.gameObject.activeSelf, Is.False);
        Assert.That(Intensity(doorLight), Is.Zero);
    }

    private static float Intensity(Component light) => (float)Get(light, "intensity");

    private static IEnumerator Until(Func<bool> condition, string failure)
    {
        float deadline = Time.realtimeSinceStartup + 4f;
        while (!condition() && Time.realtimeSinceStartup < deadline)
            yield return null;
        Assert.That(condition(), Is.True, failure);
    }

    private GameObject Create(string name)
    {
        var obj = new GameObject(name);
        objects.Add(obj);
        return obj;
    }

    private static object Get(object target, string property) => target.GetType().GetProperty(property).GetValue(target);
    private static object Call(object target, string method) => target.GetType().GetMethod(method).Invoke(target, null);
    private static void Set(object target, string field, object value) => target.GetType()
        .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
}
#endif
