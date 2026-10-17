using System;
using System.Reflection;
using MagicGarden;
using NUnit.Framework;

public class DailyLoadoutTests
{
    // Reflection allows RED to run before the equipment model exists, without
    // breaking compilation of the rest of the existing test assembly.
    private object state;
    private Type type;
    private object Call(string method, params object[] args) => type.GetMethod(method).Invoke(state, args);
    private string Item(string method, bool spell, int slot) => (string)Call(method, spell, slot);
    private bool Confirm() => (bool)Call("TryConfirm", (Func<string, bool>)(id => id.StartsWith("spell:")),
        (Func<string, bool>)(id => id.StartsWith("plant:")));

    [SetUp] public void Setup()
    {
        type = typeof(Balance).Assembly.GetType("MagicGarden.DailyLoadoutState");
        Assert.That(type, Is.Not.Null, "Daily equipment authority must exist.");
        state = Activator.CreateInstance(type);
        Call("BeginDay", 0);
    }

    [Test] public void ThreeSlotsPerCategoryAllowEmptyAndRejectDuplicatesAndOverflow()
    {
        Assert.That(Call("TryEdit", true, 0, "spell:a"), Is.True);
        Assert.That(Call("TryEdit", true, 1, "spell:a"), Is.False);
        Assert.That(Call("TryEdit", true, 3, "spell:b"), Is.False);
        Assert.That(Call("TryEdit", false, -1, "plant:a"), Is.False);
        Assert.That(Call("TryEdit", false, 2, "plant:a"), Is.True);
        Assert.That(Confirm(), Is.True);
        Assert.That(Item("GetEquipped", true, 0), Is.EqualTo("spell:a"));
        Assert.That(Item("GetEquipped", true, 1), Is.Null);
        Assert.That(Item("GetEquipped", false, 2), Is.EqualTo("plant:a"));
    }

    [Test] public void MultipleDraftEditsDoNotEquipUntilOneAtomicConfirmation()
    {
        Call("TryEdit", true, 0, "spell:a");
        Call("TryEdit", true, 0, "spell:b");
        Call("TryEdit", false, 1, "plant:a");
        Assert.That(Item("GetEquipped", true, 0), Is.Null);
        Assert.That(Confirm(), Is.True);
        Assert.That(Item("GetEquipped", true, 0), Is.EqualTo("spell:b"));
        Assert.That(Call("TryEdit", true, 0, null), Is.False);
        Assert.That(Call("TryEdit", false, 0, "plant:new"), Is.False);
        Assert.That(Confirm(), Is.False);
        Assert.That(Item("GetDraft", true, 0), Is.EqualTo("spell:b"));
    }

    [Test] public void LockedOrInvalidItemsRejectTheWholeConfigurationWithoutEffects()
    {
        Call("TryEdit", true, 0, "spell:a");
        Call("TryEdit", false, 0, "locked");
        Assert.That(Confirm(), Is.False);
        Assert.That(Item("GetEquipped", true, 0), Is.Null);
        Assert.That(type.GetProperty("IsConfirmed").GetValue(state), Is.False);
        Call("TryEdit", false, 0, null);
        Assert.That(Confirm(), Is.True);
    }

    [Test] public void OnlyANewerDayReopensEditingAndRunResetClearsEverything()
    {
        Call("TryEdit", true, 0, "spell:a"); Confirm();
        Assert.That(Call("BeginDay", 0), Is.False);
        Assert.That(Call("BeginDay", -1), Is.False);
        Assert.That(Call("TryEdit", true, 0, null), Is.False);
        Assert.That(Call("BeginDay", 1), Is.True);
        Assert.That(Item("GetDraft", true, 0), Is.EqualTo("spell:a"));
        Assert.That(Call("TryEdit", true, 0, "spell:b"), Is.True);
        Assert.That(Confirm(), Is.True);
        Call("ResetRun");
        Assert.That(Item("GetEquipped", true, 0), Is.Null);
        Assert.That(Item("GetDraft", true, 0), Is.Null);
        Assert.That(type.GetProperty("IsConfirmed").GetValue(state), Is.False);
        Assert.That(Call("BeginDay", 0), Is.True);
    }

    [Test] public void ConfirmationLocksBeforePublishingAndNeverExposesMutableArrays()
    {
        Call("TryEdit", true, 0, "spell:a");
        int events = 0;
        Action changed = () =>
        {
            events++;
            Assert.That(Item("GetEquipped", true, 0), Is.EqualTo("spell:a"));
            Assert.That(Call("TryEdit", true, 0, "spell:b"), Is.False);
            Assert.That(Confirm(), Is.False);
        };
        type.GetEvent("Confirmed").AddEventHandler(state, changed);
        Assert.That(Confirm(), Is.True);
        Assert.That(events, Is.EqualTo(1));
        foreach (var property in type.GetProperties()) Assert.That(property.PropertyType.IsArray, Is.False);
    }
}
