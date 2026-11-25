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

public class RitualWalkCadenceTests
{
    private readonly Dictionary<PropertyInfo, object> singletons = new Dictionary<PropertyInfo, object>();
    private GameObject player;
    private Rigidbody2D body;
    private Component movement;
    private Behaviour input;
    private float deceleration;

    private static Type RuntimeType(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Single(a => a.GetName().Name == "Assembly-CSharp").GetType(name, true);

    [SetUp]
    public void SetUp()
    {
        foreach (var name in new[] { "GameFlowController", "TutorialManager", "UIManager" })
        {
            var instance = RuntimeType(name).GetProperty("Instance");
            singletons.Add(instance, instance.GetValue(null));
            instance.SetValue(null, null);
        }

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Characters/Player.prefab");
        var movementType = RuntimeType("PlayerMovementController");
        var prefabBody = prefab.GetComponent<Rigidbody2D>();
        var prefabMovement = prefab.GetComponent(movementType);
        player = new GameObject("Ritual walk cadence player");
        body = player.AddComponent<Rigidbody2D>();
        body.drag = prefabBody.drag;
        body.gravityScale = prefabBody.gravityScale;
        body.constraints = prefabBody.constraints;
        body.collisionDetectionMode = prefabBody.collisionDetectionMode;

        input = (Behaviour)player.AddComponent(RuntimeType("InputReader"));
        input.enabled = false; // Inject keyboard state without letting Update overwrite it.
        movement = player.AddComponent(movementType);
        var serialized = new SerializedObject(prefabMovement);
        foreach (var name in new[] { "accelerationRate", "decelerationRate", "maxSpeed" })
            movementType.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(movement, serialized.FindProperty(name).floatValue);
        movementType.GetField("input", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(movement, input);
        deceleration = serialized.FindProperty("decelerationRate").floatValue;
    }

    [TearDown]
    public void TearDown()
    {
        if (player != null) Object.DestroyImmediate(player);
        foreach (var saved in singletons)
            saved.Key.SetValue(null, saved.Value);
        singletons.Clear();
    }

    [UnityTest]
    public IEnumerator RitualUsesTheKeyboardAccelerationAndCruiseSpeed() => CompareCadence(0f);

    [UnityTest]
    public IEnumerator SlowedRitualKeepsTheKeyboardPaceAndArrivesBeforeItsDeadline() => CompareCadence(0.4f);

    private IEnumerator CompareCadence(float slow)
    {
        Call("ApplyIceSlow", slow);
        SetInput(Vector2.right);
        var manualSpeeds = new float[35];
        Vector2 lastPosition = body.position;
        for (int i = 0; i < manualSpeeds.Length; i++)
        {
            yield return new WaitForFixedUpdate();
            manualSpeeds[i] = (body.position.x - lastPosition.x) / Time.fixedDeltaTime;
            lastPosition = body.position;
        }
        Assert.That(manualSpeeds[0], Is.GreaterThan(0f));
        Assert.That(manualSpeeds[manualSpeeds.Length - 1], Is.GreaterThan(manualSpeeds[0]));

        body.position = Vector2.zero;
        lastPosition = body.position;
        SetInput(Vector2.left); // Player input must not steer an active approach.
        var center = Vector2.right * 4f;
        object[] start = { center, 0f };
        Assert.That((bool)Call("TryStartRitualApproach", start), Is.True);
        float deadline = Time.time + (float)start[1];
        for (int i = 0; i < manualSpeeds.Length; i++)
        {
            yield return new WaitForFixedUpdate();
            float speed = (body.position.x - lastPosition.x) / Time.fixedDeltaTime;
            Assert.That(speed, Is.EqualTo(manualSpeeds[i]).Within(0.01f), $"Physics step {i + 1}");
            Assert.That(body.position.y, Is.EqualTo(lastPosition.y));
            lastPosition = body.position;
        }

        bool arrived = false;
        while (Time.time < deadline && !(arrived = (bool)Call("TryStartRitualMeditation")))
            yield return new WaitForFixedUpdate();
        Assert.That(arrived, Is.True, "The advertised timeout must allow the effective walking pace to reach the center.");
        Assert.That(body.position, Is.EqualTo(center));
        Assert.That(body.velocity, Is.EqualTo(Vector2.zero));
        Call("EndRitualMovement");
        yield return new WaitForFixedUpdate();
        Assert.That((body.position.x - center.x) / Time.fixedDeltaTime,
            Is.EqualTo(-manualSpeeds[0]).Within(0.01f));
    }

    [UnityTest]
    public IEnumerator ReleasingKeyboardInputStillDecelerates()
    {
        SetInput(Vector2.right);
        yield return new WaitForFixedUpdate();
        float moving = body.velocity.x;
        Assert.That(moving, Is.GreaterThan(0f));
        SetInput(Vector2.zero);
        yield return new WaitForFixedUpdate();
        Assert.That(body.velocity.x, Is.EqualTo(moving * (1f - deceleration * Time.fixedDeltaTime)).Within(0.01f));
    }

    private object Call(string method, params object[] arguments) => movement.GetType().GetMethod(method).Invoke(movement, arguments);

    private void SetInput(Vector2 direction) => input.GetType().GetProperty("MoveInput").GetSetMethod(true)
        .Invoke(input, new object[] { direction });
}
#endif
