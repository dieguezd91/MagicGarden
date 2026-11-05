using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using F = DailyLoadoutUIFixture;

public sealed class TutorialLoadoutFlowTests
{
    F f;
    GameObject root;
    Component tutorial, resources;
    ScriptableObject sequence, step;
    object previous;
    int commits, stockChanges;
    Action committed, stockChanged;

    [SetUp]
    public void Setup()
    {
        commits = stockChanges = 0;
        f = new F();
        var instance = F.Production("TutorialManager").GetProperty("Instance");
        previous = instance.GetValue(null);
        instance.GetSetMethod(true).Invoke(null, new object[] { null });
        root = new GameObject("Isolated tutorial loadout policy"); root.SetActive(false);
        tutorial = root.AddComponent(F.Production("TutorialManager"));
        F.Set(tutorial, "enableTutorial", false);
        resources = root.AddComponent(F.Production("InventoryManager"));
        F.Call(resources, "SetGold", 17);
        F.Call(resources, "AddMaterial", Enum.ToObject(F.Production("MaterialType"), 1), 11);
        Assert.That(F.Call(f.Seeds, "TryGrantUnlockedPlant", f.FirstPlant, 3), Is.True);
        sequence = ScriptableObject.CreateInstance(F.Production("TutorialSequence"));
        step = ScriptableObject.CreateInstance(F.Production("TutorialStep"));
        F.Set(step, "objectiveType", Enum.Parse(F.Production("TutorialObjectiveType"), "Dig"));
        ((IList)F.Field(sequence, "steps")).Add(step);
        F.Set(tutorial, "sequence", sequence);
        F.Call(tutorial, "Awake");
        // EditMode does not promise engine lifecycle dispatch. Pair both explicitly.
        F.Call(tutorial, "OnEnable");
        stockChanged = () => stockChanges++;
        var stockField = f.Seeds.GetType().GetField("onInventoryChanged");
        stockField.SetValue(f.Seeds, Delegate.Combine((Delegate)stockField.GetValue(f.Seeds), stockChanged));
        f.Spells.GetType().GetEvent("OnSpellInventoryChanged").AddEventHandler(f.Spells, stockChanged);
        committed = () => commits++;
        f.Session.GetType().GetEvent("CommittedChanged").AddEventHandler(f.Session, committed);
    }

    [TearDown]
    public void Cleanup()
    {
        if (f != null && committed != null)
            f.Session.GetType().GetEvent("CommittedChanged").RemoveEventHandler(f.Session, committed);
        if (f != null && stockChanged != null)
        {
            var field = f.Seeds.GetType().GetField("onInventoryChanged");
            field.SetValue(f.Seeds, Delegate.Remove((Delegate)field.GetValue(f.Seeds), stockChanged));
            f.Spells.GetType().GetEvent("OnSpellInventoryChanged").RemoveEventHandler(f.Spells, stockChanged);
        }
        if (tutorial != null) F.Call(tutorial, "OnDisable");
        UnityEngine.Object.DestroyImmediate(root);
        UnityEngine.Object.DestroyImmediate(sequence); UnityEngine.Object.DestroyImmediate(step);
        F.Production("TutorialManager").GetProperty("Instance").GetSetMethod(true).Invoke(null, new[] { previous });
        f?.Dispose();
    }

    void Begin(bool required)
    {
        if (required)
        {
            var field = sequence.GetType().GetField("requireUsableLoadout");
            Assert.That(field, Is.Not.Null, "TutorialSequence.requireUsableLoadout is an opt-in serialized bool.");
            Assert.That(field.FieldType, Is.EqualTo(typeof(bool)));
            field.SetValue(sequence, true);
        }
        F.Call(tutorial, "StartTutorial");
    }

    string[] Assignments(bool committedState) => Enumerable.Range(0, 6).Select(i =>
        f.HasValue(i < 3, committedState, i % 3) ? f.Identity(i < 3, committedState, i % 3).ToString() : "<empty>").ToArray();

    object[] PublicState() => new object[] {
        string.Join("|", Assignments(false)), string.Join("|", Assignments(true)),
        f.Confirmed, F.Property(f.Session, "CanConfigure"), F.Property(f.Session, "IsDay"),
        F.Property(f.Days, "TotalNights"), commits, stockChanges,
        string.Join("|", Enumerable.Range(0, (int)F.Property(f.Seeds, "PlantSlotsCount"))
            .Select(i => F.Call(f.Seeds, "GetSeedCountInSlot", i).ToString())),
        F.Call(f.Seeds, "GetSelectedSlotIndex"), F.Call(f.Spells, "GetSelectedSlotIndex"),
        F.Call(resources, "GetGold"),
        F.Call(resources, "GetMaterialAmount", Enum.ToObject(F.Production("MaterialType"), 1)) };

    [TestCase(false, false)] [TestCase(true, false)] [TestCase(false, true)]
    public void TutorialRejectsMissingCategoryWithoutAnyStateOrAllowanceMutation(bool plant, bool spell)
    {
        Begin(true); Assert.That(f.Open(), Is.True);
        if (plant) Assert.That(F.Call(f.Session, "TrySetDraftPlant", 2, F.Seed(7)), Is.True);
        if (spell) Assert.That(F.Call(f.Session, "TrySetDraftSpell", 1, f.FirstSpell), Is.True);
        var before = PublicState();
        Assert.That(F.Call(f.Session, "TryConfirm"), Is.False, "Public session cannot bypass tutorial policy.");
        CollectionAssert.AreEqual(before, PublicState());
        F.Call(f.Presenter, "Confirm");
        CollectionAssert.AreEqual(before, PublicState());
        Assert.That(f.Status, Does.EndWith("Select at least one plant and one spell before confirming your tutorial loadout."));
        Assert.That(f.Action("Confirm").interactable, Is.True);
        Assert.That(f.IsOpen, Is.True);
    }

    [Test]
    public void OneEachCommitsOnlyChosenSlotsLocksTodayAndActualNextDayRestoresEditing()
    {
        Begin(true); Assert.That(f.Open(), Is.True);
        Assert.That(F.Call(f.Session, "TrySetDraftPlant", 2, F.Seed(7)), Is.True);
        Assert.That(F.Call(f.Session, "TrySetDraftSpell", 1, f.FirstSpell), Is.True);
        var expected = Assignments(false);
        F.Call(f.Presenter, "Confirm");
        CollectionAssert.AreEqual(expected, Assignments(true));
        Assert.That(expected.Count(x => x != "<empty>"), Is.EqualTo(2));
        Assert.That(f.Confirmed, Is.True); Assert.That(commits, Is.EqualTo(1));
        Assert.That(f.Status, Does.EndWith("Loadout confirmed."));
        var before = PublicState();
        Assert.That(F.Call(f.Session, "TrySetDraftPlant", 0, F.Seed(3)), Is.False);
        Assert.That(F.Call(f.Session, "TrySetDraftSpell", 0, f.SecondSpell), Is.False);
        Assert.That(F.Call(f.Session, "TryConfirm"), Is.False);
        F.Call(f.Days, "StartDay"); CollectionAssert.AreEqual(before, PublicState());
        F.Call(f.Presenter, "Close"); Assert.That(f.Open(), Is.True);
        Assert.That(f.Confirmed, Is.True);
        F.Call(f.Days, "StartNight"); F.Call(f.Days, "StartDay");
        Assert.That(f.Confirmed, Is.False); Assert.That(F.Property(f.Session, "CanConfigure"), Is.True);
        Assert.That(F.Call(f.Session, "TrySetDraftPlant", 0, F.Seed(3)), Is.True);
        CollectionAssert.AreEqual(expected, Assignments(true));
    }

    [TestCase(false)] [TestCase(true)]
    public void DefaultSequenceAndSkippedTutorialPreserveManualEmptyConfirmation(bool skip)
    {
        Begin(skip);
        if (skip) F.Call(tutorial, "SkipTutorial");
        Assert.That(f.Open(), Is.True);
        Assert.That(f.Confirmed, Is.False, "Opening never auto-equips or auto-confirms.");
        Assert.That(Assignments(true), Is.All.EqualTo("<empty>"));
        F.Call(f.Presenter, "Confirm");
        Assert.That(f.Confirmed, Is.True); Assert.That(commits, Is.EqualTo(1));
        Assert.That(Assignments(true), Is.All.EqualTo("<empty>"));
    }

    [Test]
    public void AuthoredInitialSeedInventoryHasRealPlantingStockThroughPublicInterface()
    {
        var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/SeedInventory.prefab");
        Assert.That(prefab, Is.Not.Null);
        var inventory = prefab.GetComponent(F.Production("SeedInventory"));
        foreach (int identity in new[] { 7, 3 })
        {
            var slots = Enumerable.Range(0, (int)F.Property(inventory, "PlantSlotsCount"))
                .Select(i => new { Index = i, Slot = F.Call(inventory, "GetPlantSlot", i) });
            var match = slots.Single(x => F.Field(x.Slot, "seedType").Equals(F.Seed(identity)));
            Assert.That(F.Call(inventory, "GetSeedCountInSlot", match.Index), Is.EqualTo(3));
            Assert.That(F.Field(match.Slot, "plantPrefab"), Is.Not.Null);
        }
    }

    [Test]
    public void ExistingSerializedObjectiveNumbersRemainStableAndNewObjectivesAppend()
    {
        var type = F.Production("TutorialObjectiveType");
        Assert.That(Convert.ToInt32(Enum.Parse(type, "EnterHouse")), Is.EqualTo(6));
        Assert.That(Convert.ToInt32(Enum.Parse(type, "FirstPlantReady")), Is.EqualTo(22));
        Assert.That(Convert.ToInt32(Enum.Parse(type, "DailyLoadoutProximity")), Is.EqualTo(29));
        Assert.That(Enum.GetNames(type), Does.Contain("ConfirmDailyLoadout"));
        Assert.That(Convert.ToInt32(Enum.Parse(type, "ConfirmDailyLoadout")), Is.EqualTo(30));
        Assert.That(Enum.GetNames(type), Does.Contain("ExitHouse"));
        Assert.That(Convert.ToInt32(Enum.Parse(type, "ExitHouse")), Is.EqualTo(31));
    }
}
