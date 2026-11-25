#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using static DailyLoadoutUIFixture;
using Object = UnityEngine.Object;

public sealed class RitualAttackPlayModeTests
{
    private readonly List<GameObject> objects = new List<GameObject>();
    private readonly Dictionary<FieldInfo, object> saved = new Dictionary<FieldInfo, object>();
    private Component flow, days, world, input, movement, altar, spells, mana;
    private Animator bodyAnimator, handAnimator;
    private object session;
    private ScriptableObject range;
    private EventInfo primaryPressed, spellCasted;
    private int presses, casts;
    private float previousScale, previousCapture;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        if (Object.FindObjectOfType(Production("WorldTransitionAnimator")) != null ||
            Object.FindObjectOfType(Production("InputReader")) != null || Camera.main != null ||
            GameObject.FindGameObjectWithTag("Player") != null)
            Assert.Ignore("Run the ritual attack comparison from an empty scene.");

        previousScale = Time.timeScale;
        previousCapture = Time.captureDeltaTime;
        Time.timeScale = 1f;
        Time.captureDeltaTime = 0.02f;
        foreach (string name in new[] { "GameFlowController", "DayCycleController", "LevelManager", "TutorialManager",
                     "SpellInventory", "SeedInventory", "SoundManager", "UIManager" })
        {
            var type = Production(name);
            Isolate(type.GetField("Instance", BindingFlags.Static | BindingFlags.Public) ??
                type.GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic));
        }
        Isolate(Production("ManaSystem").GetField("InfiniteMana"), false);

        var context = Create("World");
        flow = context.AddComponent(Production("GameFlowController"));
        days = context.AddComponent(Production("DayCycleController"));
        world = context.AddComponent(Production("WorldTransitionAnimator"));
        Set(world, "useFade", false);
        Set(world, "autoFindHouse", false);
        Set(world, "autoFindInteriorLights", false);
        context.AddComponent(Production("LevelManager"));
        spells = context.AddComponent(Production("SpellInventory"));
        var sound = context.AddComponent(Production("SoundManager"));
        Set(sound, "sounds", Array.CreateInstance(sound.GetType().GetField("sounds").FieldType.GetElementType(), 0));
        Set(sound, "initialPoolSize", 0);
        input = context.AddComponent(Production("InputReader"));

        var camera = Create("Camera").AddComponent<Camera>();
        camera.gameObject.tag = "MainCamera";
        camera.orthographic = true;
        camera.transform.position = new Vector3(0f, 0f, -10f);

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Characters/Player.prefab");
        Assert.That(prefab, Is.Not.Null);
        var player = Create("Player");
        player.SetActive(false);
        player.tag = "Player";
        player.AddComponent<SpriteRenderer>();
        var body = player.AddComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        body.constraints = RigidbodyConstraints2D.FreezeRotation;
        var hand = Object.Instantiate(prefab.transform.Find("PlayerHand").gameObject, player.transform, false);
        hand.name = "PlayerHand"; // Keep the actual PlayerMeditate active-state binding.
        handAnimator = hand.GetComponent<Animator>();
        Assert.That(hand.GetComponent(Production("HandAnimatorEvents")), Is.Not.Null);
        bodyAnimator = player.AddComponent<Animator>();
        bodyAnimator.runtimeAnimatorController = prefab.GetComponent<Animator>().runtimeAnimatorController;
        bodyAnimator.fireEvents = false; // Omit body audio events; hand attack clip events stay enabled.

        var life = player.AddComponent(Production("LifeController"));
        // A fresh component has no serialized UnityEvent; the shipped player prefab does.
        Set(life, "onHealthChanged", new UnityEngine.Events.UnityEvent<float, float>());
        player.AddComponent(Production("PlayerController"));
        movement = player.AddComponent(Production("PlayerMovementController"));
        mana = player.AddComponent(Production("ManaSystem"));
        Set(mana, "baseDayRegenerationRate", 0f);
        Set(mana, "baseNightRegenerationRate", 0f);
        var experience = player.AddComponent(Production("PlayerExperienceSystem"));
        Set(experience, "progressionData", AssetDatabase.LoadAssetAtPath<ScriptableObject>(
            "Assets/Scriptables/Progression/Experience Progression.asset"));
        var unlocks = player.AddComponent(Production("PlayerContentUnlockSystem"));
        Set(unlocks, "playerExperienceSystem", experience);
        Set(unlocks, "spellInventory", spells);
        var caster = player.AddComponent(Production("PlayerSpellController"));
        Set(caster, "handAnimator", handAnimator);
        Set(caster, "handRenderer", hand.GetComponent<SpriteRenderer>());
        Set(caster, "handObject", hand);
        var firePoint = new GameObject("FirePoint").transform;
        firePoint.SetParent(player.transform, false);
        firePoint.localPosition = prefab.transform.Find("FirePoint").localPosition;
        Set(caster, "firePoint", firePoint);
        player.SetActive(true);

        var altarObject = Create("Altar");
        altar = altarObject.AddComponent(Production("RitualAltar"));
        Set(altar, "canRestoreHealth", false);
        Set(altar, "ritualDuration", 1.6f);
        var ritualVisuals = altarObject.AddComponent(Production("RitualVisualController"));
        Set(ritualVisuals, "ritualLightFadeDuration", 0.6f);

        primaryPressed = input.GetType().GetEvent("OnPrimaryPressed");
        primaryPressed.AddEventHandler(input, (Action)CountPress);
        spellCasted = Production("TutorialEvents").GetEvent("OnSpellCasted");
        spellCasted.AddEventHandler(null, (Action)CountCast);
        yield return null;

        Assert.That(Phase, Is.EqualTo("Day"));
        range = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/Scriptables/Spells/Spell_Range.asset");
        Assert.That(range, Is.Not.Null);
        session = Property(unlocks, "Loadout");
        Assert.That(Call(session, "TrySetDraftSpell", 0, range), Is.True);
        Assert.That(Call(session, "TryConfirm"), Is.True);
        Call(spells, "SelectSlot", Property(range, "SlotIndex"));
        Call(world, "EnterHouse");
        Assert.That(Property(world, "IsInInterior"), Is.True);
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (saved.Count == 0) yield break;
        try
        {
            primaryPressed?.RemoveEventHandler(input, (Action)CountPress);
            spellCasted?.RemoveEventHandler(null, (Action)CountCast);
            if (range != null)
                foreach (var projectile in Projectiles()) Object.Destroy(projectile.gameObject);
            for (int i = objects.Count - 1; i >= 0; i--)
                if (objects[i] != null) Object.Destroy(objects[i]);
            yield return null;
        }
        finally
        {
            foreach (var pair in saved) pair.Key.SetValue(null, pair.Value);
            saved.Clear();
            objects.Clear();
            primaryPressed = spellCasted = null;
            presses = casts = 0;
            Time.captureDeltaTime = previousCapture;
            Time.timeScale = previousScale;
        }
    }

    [UnityTest]
    public IEnumerator EquippedRangeSpellFiresOutsideAfterMeditation()
    {
        Assert.That(Call(altar, "TryInteract"), Is.True);
        yield return Until(() => InBodyState("Meditate") && !handAnimator.gameObject.activeSelf,
            "PlayerMeditate did not hide the real PlayerHand.");
        yield return Until(() => Phase == "Night", "Meditation did not transition to night.");
        Assert.That(Property(movement, "IsRitualMovementActive"), Is.True,
            "Night should begin while the ritual is still holding the character.");
        yield return Until(() => InBodyState("Idle") && !(bool)Property(movement, "IsRitualMovementActive"),
            "The character did not finish the ritual and return to idle.");
        yield return ExitAndCast();
    }

    [UnityTest]
    public IEnumerator EquippedRangeSpellFiresOutsideOnNightWithoutMeditation()
    {
        Call(days, "StartNight");
        Assert.That(Phase, Is.EqualTo("Night"));
        yield return ExitAndCast();
    }

    private IEnumerator ExitAndCast()
    {
        Call(world, "ExitHouse");
        Assert.That(Property(world, "IsInInterior"), Is.False);
        Assert.That(Property(world, "IsNightMode"), Is.True);
        var slotIndex = (int)Property(range, "SlotIndex");
        Assert.That(Call(spells, "GetSelectedSlotIndex"), Is.EqualTo(slotIndex));
        var admission = Property(session, "Admission");
        Assert.That(admission.GetType().GetMethod("CheckSpellInventoryIndex")
            .Invoke(admission, new object[] { slotIndex, null }).ToString(), Is.EqualTo("Allowed"));
        Assert.That(Call(spells, "CanCastSelectedSpell"), Is.True);
        float before = (float)Call(mana, "GetCurrentMana");
        int cost = (int)Field(range, "manaCost");
        Assert.That(before, Is.GreaterThanOrEqualTo(cost));
        Assert.That(Projectiles(), Is.Empty);
        Assert.That(casts, Is.Zero);
        Assert.That(presses, Is.Zero);

        // Let the hand settle from activation/summon without overriding any Animator parameters.
        yield return new WaitForSeconds(0.75f);
        // Batch mode has no Game View mouse. Dispatch at the input event boundary;
        // the real subscribers, animator event, projectile and mana path still run.
        ((Action)input.GetType().GetField("OnPrimaryPressed", BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(input)).Invoke();

        float deadline = Time.realtimeSinceStartup + 4f;
        while (casts == 0 && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(casts, Is.EqualTo(1), "Click reached InputReader; expected one real cast. " +
            $"Hand active={handAnimator.gameObject.activeInHierarchy}, IsNight={HandBool("IsNight")}, " +
            $"BaseSpell={HandBool("BaseSpell")}, IsAttacking={HandBool("IsAttacking")}, " +
            $"mana={Call(mana, "GetCurrentMana")}, projectiles={Projectiles().Length}.");
        Assert.That(presses, Is.EqualTo(1));
        var projectiles = Projectiles();
        Assert.That(projectiles.Length, Is.EqualTo(1));
        Assert.That(Property(projectiles[0], "UpgradeTargetId"), Is.EqualTo("spell:" + slotIndex));
        Assert.That(Call(mana, "GetCurrentMana"), Is.EqualTo(before - cost));
    }

    private Component[] Projectiles() => Array.ConvertAll(Object.FindObjectsOfType(Production("BasicRangeSpell")),
        value => (Component)value);
    private string HandBool(string name) => handAnimator.isInitialized ? handAnimator.GetBool(name).ToString() : "uninitialized";
    private string Phase => Property(flow, "CurrentPhase").ToString();
    private bool InBodyState(string state) => bodyAnimator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer." + state);
    private void CountPress() => presses++;
    private void CountCast() => casts++;

    private static IEnumerator Until(Func<bool> condition, string failure)
    {
        float deadline = Time.realtimeSinceStartup + 4f;
        while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(condition(), Is.True, failure);
    }

    private GameObject Create(string name)
    {
        var obj = new GameObject("Ritual attack " + name);
        objects.Add(obj);
        return obj;
    }

    private void Isolate(FieldInfo field, object value = null)
    {
        Assert.That(field, Is.Not.Null);
        saved.Add(field, field.GetValue(null));
        field.SetValue(null, value);
    }
}
#endif
