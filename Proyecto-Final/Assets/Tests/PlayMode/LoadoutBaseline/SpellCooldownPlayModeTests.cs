using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// Reflection preserves the existing Assembly-CSharp serialization boundary.
public sealed class SpellCooldownPlayModeTests
{
    private readonly List<GameObject> created = new List<GameObject>();
    private readonly Dictionary<FieldInfo, object> saved = new Dictionary<FieldInfo, object>();
    private float previousTimeScale;
    private float previousCaptureDeltaTime;

    private static Type Production(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Single(a => a.GetName().Name == "Assembly-CSharp").GetType(name, true);
    private static object Call(object value, string name, params object[] args) => value.GetType().GetMethods()
        .Single(m => m.Name == name && m.GetParameters().Length == args.Length).Invoke(value, args);
    private static object Field(object value, string name) => value.GetType().GetField(name).GetValue(value);

    [SetUp]
    public void IsolateRuntime()
    {
        previousTimeScale = Time.timeScale;
        previousCaptureDeltaTime = Time.captureDeltaTime;
        Time.timeScale = 1;
        Time.captureDeltaTime = 0.02f; // Supported fixed frame delta; production still runs real Update.
        foreach (string name in new[] { "SpellInventory", "GameFlowController" })
        {
            var field = Production(name).GetField("<Instance>k__BackingField", BindingFlags.NonPublic | BindingFlags.Static);
            saved.Add(field, field.GetValue(null));
            field.SetValue(null, null);
        }
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        try
        {
            foreach (var go in created) if (go != null) UnityEngine.Object.Destroy(go);
            yield return null; // No owned inventory can continue updating after restoration.
        }
        finally
        {
            created.Clear();
            foreach (var entry in saved) entry.Key.SetValue(null, entry.Value);
            saved.Clear();
            Time.captureDeltaTime = previousCaptureDeltaTime;
            Time.timeScale = previousTimeScale;
        }
    }

    private GameObject Object(string name)
    {
        var go = new GameObject(name);
        go.SetActive(false);
        created.Add(go);
        return go;
    }

    private Component Inventory(float cooldown)
    {
        var go = Object("Cooldown PlayMode fixture");
        var spells = go.AddComponent(Production("SpellInventory"));
        go.SetActive(true); // Unity invokes Awake and Update; never dispatch them manually here.
        var prefab = Object("Cooldown spell prefab");
        var range = Enum.ToObject(Production("SpellType"), 1);
        LogAssert.Expect(LogType.Log, "Spell unlocked: Spell 1 in slot 2");
        Call(spells, "UnlockSpell", 1, range, "Spell 1", null, prefab, 7, cooldown);
        LogAssert.Expect(LogType.Log, "Spell unlocked: Spell 4 in slot 5");
        Call(spells, "UnlockSpell", 4, range, "Spell 4", null, prefab, 7, 20f);
        return spells;
    }

    [UnityTest]
    public IEnumerator RealFramesAdvanceOnlyOriginalCooldownAndEmitItsNotifications()
    {
        var spells = Inventory(3600);
        var updates = new List<int>();
        int changed = 0;
        spells.GetType().GetEvent("onCooldownUpdated").AddEventHandler(spells, (Action<int>)updates.Add);
        spells.GetType().GetEvent("OnSpellInventoryChanged").AddEventHandler(spells, (Action)(() => changed++));
        Call(spells, "StartCooldown", 1);
        Call(spells, "SelectSlot", 4);
        var original = Call(spells, "GetSpellSlot", 1);
        Assert.That(Field(original, "currentCooldown"), Is.EqualTo(3600f));
        yield return null;
        yield return null;
        float remaining = (float)Field(original, "currentCooldown");
        Assert.That(remaining, Is.GreaterThan(0).And.LessThan(3600));
        Assert.That(Call(spells, "GetCooldownProgress", 1), Is.EqualTo(remaining / 3600f).Within(0.00001f));
        Assert.That(updates.Count, Is.GreaterThanOrEqualTo(2));
        Assert.That(remaining, Is.EqualTo(3600f - (updates.Count - 1) * 0.02f).Within(0.002f));
        Assert.That(updates.All(i => i == 1), Is.True);
        Assert.That(Field(Call(spells, "GetSpellSlot", 4), "currentCooldown"), Is.EqualTo(0f));
        Assert.That(Call(spells, "GetSelectedSlotIndex"), Is.EqualTo(4));
        Assert.That(changed, Is.Zero);
        LogAssert.NoUnexpectedReceived();
    }

    [UnityTest]
    public IEnumerator ExpiredCooldownIsClampedAndStopsNotifying()
    {
        var spells = Inventory(0.001f);
        int updates = 0;
        spells.GetType().GetEvent("onCooldownUpdated").AddEventHandler(spells, (Action<int>)(_ => updates++));
        Call(spells, "StartCooldown", 1);
        yield return null;
        yield return null;
        Assert.That(Field(Call(spells, "GetSpellSlot", 1), "currentCooldown"), Is.EqualTo(0f));
        Assert.That(updates, Is.GreaterThanOrEqualTo(2)); // Start plus the final update.
        int atExpiry = updates;
        yield return null;
        Assert.That(updates, Is.EqualTo(atExpiry));
        LogAssert.NoUnexpectedReceived();
    }
}
