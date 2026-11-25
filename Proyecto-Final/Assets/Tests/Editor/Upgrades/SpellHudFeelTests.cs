using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using static UpgradeUIArtTests;

// Spell HUD game feel (S3): ready cue, selection tween, cooldown-blocked feedback and mana spend pulse.
// Editor test assembly deliberately enters real Play Mode; reflection preserves the Assembly-CSharp boundary.
// Isolated GameObjects only (no scene load). No lambdas over locals: their closure is lost on the domain reload.
public class SpellHudFeelTests
{
    private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static readonly Color FrameBase = new Color(0.2f, 0.2f, 0.2f, 0.8f);
    private static readonly Color IconBase = new Color(0.9f, 0.8f, 0.7f, 1f);

    [UnityTearDown] public IEnumerator LeavePlayMode()
    {
        if (EditorApplication.isPlaying) yield return new ExitPlayMode();
    }

    private static IEnumerator Frames(int count) { for (int i = 0; i < count; i++) yield return null; }
    private static IEnumerator WaitRealtime(float seconds)
    {
        float end = Time.realtimeSinceStartup + seconds;
        while (Time.realtimeSinceStartup < end) yield return null;
    }

    private static void SetField(object target, string name, object value)
    {
        var field = target.GetType().GetField(name, AnyInstance);
        Assert.That(field, Is.Not.Null, target.GetType().Name + "." + name + " exists");
        field.SetValue(target, value);
    }
    private static void SetProp(object target, string name, object value) => target.GetType().GetProperty(name).SetValue(target, value);
    private static object Invoke(object target, string name, params object[] args) => target.GetType().GetMethod(name, AnyInstance).Invoke(target, args);
    private static Type UiType(string name) => TypeOf("UnityEngine.UI." + name, "UnityEngine.UI");
    private static Color ColorOf(Component graphic) => (Color)Property(graphic, "color");
    private static int Count(object o, string property) => (int)Property(o, property);

    private static void AssertExact(Vector3 actual, Vector3 expected, string message)
    {
        Assert.That(actual.x == expected.x && actual.y == expected.y && actual.z == expected.z, Is.True, message + " (expected " + expected.ToString("F6") + ", was " + actual.ToString("F6") + ")");
    }
    private static void AssertExact(Color actual, Color expected, string message)
    {
        Assert.That(actual.r == expected.r && actual.g == expected.g && actual.b == expected.b && actual.a == expected.a, Is.True,
            message + " (expected " + expected + ", was " + actual + ")");
    }

    private sealed class SlotRig
    {
        public GameObject Root;
        public Component Ui, Frame, Icon, Overlay;
        public Transform IconTransform => Icon.transform;
    }

    private static Component NewImage(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var image = go.AddComponent(UiType("Image"));
        SetProp(image, "color", color);
        return image;
    }

    private static SlotRig NewSlot()
    {
        var rig = new SlotRig { Root = new GameObject("SpellHudFeel Slot", typeof(RectTransform)) };
        rig.Root.SetActive(false);
        rig.Root.transform.localPosition = new Vector3(3f, 4f, 0f);
        rig.Frame = NewImage("Frame", rig.Root.transform, FrameBase);
        rig.Icon = NewImage("Icon", rig.Root.transform, IconBase);
        rig.Overlay = NewImage("Overlay", rig.Root.transform, Color.black);
        rig.Ui = rig.Root.AddComponent(TypeOf("SpellSlotUI"));
        SetField(rig.Ui, "slotBackground", rig.Frame);
        SetField(rig.Ui, "slotIcon", rig.Icon);
        SetField(rig.Ui, "cooldownOverlay", rig.Overlay);
        SetField(rig.Ui, "normalColor", FrameBase);
        rig.Root.SetActive(true);
        Call(rig.Ui, "Initialize", 0);
        return rig;
    }

    private static object NewSpellSlot(float cooldown, float current)
    {
        var slot = Activator.CreateInstance(TypeOf("SpellSlot"));
        SetField(slot, "isUnlocked", true);
        SetField(slot, "cooldown", cooldown);
        SetField(slot, "currentCooldown", current);
        return slot;
    }

    [UnityTest] public IEnumerator ReadyCueFiresOnceWhenCooldownHitsZeroAndSlotReturnsToExactBase()
    {
        yield return new EnterPlayMode();
        var rig = NewSlot();
        var slot = NewSpellSlot(5f, 2f);
        Vector3 rootScale = rig.Root.transform.localScale;
        Vector3 iconScale = rig.IconTransform.localScale;

        Call(rig.Ui, "UpdateSlotDisplay", slot);
        Assert.That(Count(rig.Ui, "ReadyCueCount"), Is.Zero, "S3: no ready cue while the spell is still on cooldown");
        Assert.That((float)Property(rig.Overlay, "fillAmount"), Is.EqualTo(0.4f).Within(1e-4f), "S3: existing radial cooldown display still fills 2/5");

        SetField(slot, "currentCooldown", 0f);
        Call(rig.Ui, "UpdateSlotDisplay", slot);
        Assert.That(Count(rig.Ui, "ReadyCueCount"), Is.EqualTo(1), "S3: the cue fires when cooldown goes from >0 to 0");
        Assert.That((float)Property(rig.Overlay, "fillAmount"), Is.Zero, "S3: existing radial cooldown display clears at 0");

        float peakScale = 0f, peakFlash = 0f;
        float end = Time.realtimeSinceStartup + 0.8f;
        while (Time.realtimeSinceStartup < end)
        {
            peakScale = Mathf.Max(peakScale, rig.IconTransform.localScale.x);
            peakFlash = Mathf.Max(peakFlash, Mathf.Abs(ColorOf(rig.Frame).r - FrameBase.r));
            Call(rig.Ui, "UpdateSlotDisplay", slot); // HUD refreshes keep arriving at cooldown 0: they must not retrigger
            yield return null;
        }
        Assert.That(peakScale, Is.GreaterThan(1.01f), "S3: the icon punches larger while the cue plays");
        Assert.That(peakFlash, Is.GreaterThan(0.05f), "S3: the slot frame flashes while the cue plays");
        Assert.That(Count(rig.Ui, "ReadyCueCount"), Is.EqualTo(1), "S3: the cue fires exactly once, not on every refresh at cooldown 0");
        AssertExact(rig.IconTransform.localScale, iconScale, "S3: the icon returns to its exact base scale");
        AssertExact(rig.Root.transform.localScale, rootScale, "S3: the slot returns to its exact base scale");
        AssertExact(ColorOf(rig.Frame), FrameBase, "S3: the frame returns to its exact base color");
        AssertExact(ColorOf(rig.Icon), IconBase, "S3: the icon returns to its exact base color");

        // A different spell assigned to the same position is not a "cooldown ended" event.
        SetField(slot, "currentCooldown", 3f);
        Call(rig.Ui, "UpdateSlotDisplay", slot);
        var other = NewSpellSlot(5f, 0f);
        Call(rig.Ui, "UpdateSlotDisplay", other);
        Assert.That(Count(rig.Ui, "ReadyCueCount"), Is.EqualTo(1), "S3: swapping the displayed spell does not fire the ready cue");
    }

    [UnityTest] public IEnumerator SelectionTweenStartsFromCurrentScaleAndEndsOnTheOldSnapScales()
    {
        yield return new EnterPlayMode();
        var legacy = NewSlot();
        float selectedScale = (float)Field(legacy.Ui, "selectedScale"), normalScale = (float)Field(legacy.Ui, "normalScale");
        Call(legacy.Ui, "SetSelected", false);
        Assert.That(legacy.Root.transform.localScale.x, Is.EqualTo(normalScale), "S3: unselected slot rests at normalScale");
        Call(legacy.Ui, "SetSelected", true);
        Assert.That(legacy.Root.transform.localScale.x, Is.EqualTo(normalScale), "S3: selecting no longer snaps in the same frame");
        yield return WaitRealtime(0.6f);
        AssertExact(legacy.Root.transform.localScale, new Vector3(selectedScale, selectedScale, 1f), "S3: legacy selection ends at selectedScale, as the old snap did");
        Call(legacy.Ui, "SetSelected", false);
        yield return WaitRealtime(0.6f);
        AssertExact(legacy.Root.transform.localScale, new Vector3(normalScale, normalScale, 1f), "S3: legacy deselection ends at normalScale, as the old snap did");

        // Production HUD path: equipment mode restyles the slot every frame.
        var equipped = NewSlot();
        var identity = ScriptableObject.CreateInstance(TypeOf("SpellDataSO"));
        var spell = NewSpellSlot(5f, 0f);
        Call(equipped.Ui, "UpdateEquipmentDisplay", identity, spell, null, 0);
        Call(equipped.Ui, "SetSelected", false);
        AssertExact(equipped.Root.transform.localScale, Vector3.one, "S3: equipped unselected slot rests at 1");
        Call(equipped.Ui, "SetSelected", true);
        Assert.That(equipped.Root.transform.localScale.x, Is.EqualTo(1f), "S3: equipped selection no longer snaps in the same frame");
        float end = Time.realtimeSinceStartup + 0.6f;
        float peak = 0f;
        while (Time.realtimeSinceStartup < end)
        {
            Call(equipped.Ui, "UpdateEquipmentDisplay", identity, spell, null, 0); // per-frame HUD refresh must not snap the tween
            peak = Mathf.Max(peak, equipped.Root.transform.localScale.x);
            yield return null;
        }
        AssertExact(equipped.Root.transform.localScale, new Vector3(1.2f, 1.2f, 1f), "S3: equipped selection ends at the old snap scale 1.2");
        Assert.That(peak, Is.LessThanOrEqualTo(1.2f), "S3: the tween does not overshoot the old snap scale");
    }

    [UnityTest] public IEnumerator CooldownRefusedCastShakesTheSlotAndNeverCastsOrChangesCooldown()
    {
        yield return new EnterPlayMode();
        var inventoryObject = new GameObject("SpellHudFeel Inventory");
        var inventory = inventoryObject.AddComponent(TypeOf("SpellInventory"));
        Call(inventory, "SelectSlot", 2);
        var identity = ScriptableObject.CreateInstance(TypeOf("SpellDataSO"));
        var spell = NewSpellSlot(5f, 3f);
        var target = NewSlot();
        var bystander = NewSlot();
        Call(target.Ui, "UpdateEquipmentDisplay", identity, spell, inventory, 2);
        Call(bystander.Ui, "UpdateEquipmentDisplay", identity, spell, inventory, 1);
        Vector3 basePosition = target.Root.transform.localPosition;
        Color frameBase = ColorOf(target.Frame), iconBase = ColorOf(target.Icon); // equipment mode owns these colors

        var casterObject = new GameObject("SpellHudFeel Caster");
        casterObject.SetActive(false); // Start needs the full game flow; the refusal feedback entry point does not.
        var caster = casterObject.AddComponent(TypeOf("PlayerSpellController"));
        yield return null;

        Call(caster, "GiveCooldownBlockedFeedback");
        Assert.That(Count(target.Ui, "BlockedFeedbackCount"), Is.EqualTo(1), "S3: a cooldown-refused cast triggers the selected slot's blocked feedback");
        Assert.That(Count(bystander.Ui, "BlockedFeedbackCount"), Is.Zero, "S3: other slots stay quiet");
        Call(caster, "GiveCooldownBlockedFeedback");
        Assert.That(Count(target.Ui, "BlockedFeedbackCount"), Is.EqualTo(1), "S3: a held attack button is throttled, not repeated every call");

        float maxOffset = 0f, flash = 0f;
        float end = Time.realtimeSinceStartup + 0.8f;
        while (Time.realtimeSinceStartup < end)
        {
            maxOffset = Mathf.Max(maxOffset, Mathf.Abs(target.Root.transform.localPosition.x - basePosition.x));
            flash = Mathf.Max(flash, Mathf.Abs(ColorOf(target.Frame).g - frameBase.g));
            yield return null;
        }
        Assert.That(maxOffset, Is.GreaterThan(0.1f), "S3: the slot shakes while the feedback plays");
        Assert.That(flash, Is.GreaterThan(0.05f), "S3: the slot frame flashes while the feedback plays");
        AssertExact(target.Root.transform.localPosition, basePosition, "S3: the slot returns to its exact base position");
        AssertExact(ColorOf(target.Frame), frameBase, "S3: the frame returns to its exact base color");
        AssertExact(ColorOf(target.Icon), iconBase, "S3: the icon returns to its exact base color");

        Assert.That((float)Field(spell, "currentCooldown"), Is.EqualTo(3f), "S3: the refusal does not change the cooldown (casting rules untouched)");
        Assert.That(Call(inventory, "GetSelectedSlotIndex"), Is.EqualTo(2), "S3: the refusal does not change the selection");

        // S6: the controller feedback can be switched off.
        SetField(caster, "cooldownBlockedFeedback", false);
        SetField(caster, "nextCooldownBlockedFeedbackTime", 0f);
        Call(caster, "GiveCooldownBlockedFeedback");
        Assert.That(Count(target.Ui, "BlockedFeedbackCount"), Is.EqualTo(1), "S6: cooldownBlockedFeedback = false silences the refusal feedback");
    }

    private sealed class ManaRig
    {
        public Component System, Ui, Bar, Fill;
        public Transform BarTransform => Bar.transform;
    }

    private static ManaRig NewMana()
    {
        var rig = new ManaRig();
        rig.System = new GameObject("SpellHudFeel Mana").AddComponent(TypeOf("ManaSystem"));
        var ui = new GameObject("SpellHudFeel ManaUI");
        ui.SetActive(false);
        rig.Bar = new GameObject("Bar", typeof(RectTransform)).AddComponent(UiType("Slider"));
        rig.Bar.transform.SetParent(ui.transform, false);
        rig.Fill = NewImage("Fill", ui.transform, Color.white);
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.green, 0f), new GradientColorKey(Color.green, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        rig.Ui = ui.AddComponent(TypeOf("ManaUIController"));
        SetField(rig.Ui, "manaBar", rig.Bar);
        SetField(rig.Ui, "manaFillImage", rig.Fill);
        SetField(rig.Ui, "manaGradient", gradient);
        SetField(rig.Ui, "manaSystem", rig.System);
        ui.SetActive(true);
        Invoke(rig.Ui, "SetupEventListeners");
        Invoke(rig.Ui, "ConfigureInitialState");
        return rig;
    }

    [UnityTest] public IEnumerator ManaSpendPulsesAndReturnsToBaseWhileGainingManaDoesNot()
    {
        yield return new EnterPlayMode();
        var rig = NewMana();
        yield return null;
        Vector3 baseScale = rig.BarTransform.localScale;
        Color baseFill = ColorOf(rig.Fill);

        Assert.That(Call(rig.System, "UseMana", 10f), Is.EqualTo(true));
        Assert.That(Count(rig.Ui, "PulseCount"), Is.EqualTo(1), "S3: spending mana starts one pulse");
        float peakScale = 0f, peakFlash = 0f;
        float end = Time.realtimeSinceStartup + 0.8f;
        while (Time.realtimeSinceStartup < end)
        {
            peakScale = Mathf.Max(peakScale, rig.BarTransform.localScale.x);
            peakFlash = Mathf.Max(peakFlash, Mathf.Abs(ColorOf(rig.Fill).r - baseFill.r));
            yield return null;
        }
        Assert.That(peakScale, Is.GreaterThan(baseScale.x + 0.005f), "S3: the mana bar punches while the pulse plays");
        Assert.That(peakFlash, Is.GreaterThan(0.05f), "S3: the mana fill flashes while the pulse plays");
        AssertExact(rig.BarTransform.localScale, baseScale, "S3: the mana bar returns to its exact base scale");
        AssertExact(ColorOf(rig.Fill), baseFill, "S3: the mana fill returns to its exact base color");

        Call(rig.System, "AddMana", 5f);
        Assert.That(Count(rig.Ui, "PulseCount"), Is.EqualTo(1), "S3: gaining mana does not pulse");
        yield return Frames(3);
        AssertExact(rig.BarTransform.localScale, baseScale, "S3: gaining mana leaves the bar scale untouched");
        Call(rig.System, "SetMana", 100f);
        Assert.That(Count(rig.Ui, "PulseCount"), Is.EqualTo(1), "S3: refilling mana does not pulse");
        Assert.That((float)Property(rig.Bar, "value"), Is.EqualTo(100f), "S3: the existing bar value still follows mana");
    }

    [UnityTest] public IEnumerator EveryHudEffectCanBeSwitchedOff()
    {
        yield return new EnterPlayMode();
        var rig = NewSlot();
        SetField(rig.Ui, "readyCueEnabled", false);
        SetField(rig.Ui, "selectionTweenEnabled", false);
        SetField(rig.Ui, "blockedFeedbackEnabled", false);
        var slot = NewSpellSlot(5f, 2f);
        Call(rig.Ui, "UpdateSlotDisplay", slot);
        SetField(slot, "currentCooldown", 0f);
        Call(rig.Ui, "UpdateSlotDisplay", slot);
        Call(rig.Ui, "PlayBlockedFeedback");
        Call(rig.Ui, "SetSelected", false);
        Call(rig.Ui, "SetSelected", true);
        float selectedScale = (float)Field(rig.Ui, "selectedScale");
        AssertExact(rig.Root.transform.localScale, new Vector3(selectedScale, selectedScale, 1f), "S6: selectionTweenEnabled = false snaps to the selected scale immediately");
        Vector3 position = rig.Root.transform.localPosition;
        Color frameAfterSelect = ColorOf(rig.Frame); // SetSelected owns the frame color; effects must not alter it
        yield return Frames(5);
        Assert.That(Count(rig.Ui, "ReadyCueCount"), Is.Zero, "S6: readyCueEnabled = false plays no ready cue");
        Assert.That(Count(rig.Ui, "BlockedFeedbackCount"), Is.Zero, "S6: blockedFeedbackEnabled = false plays no blocked feedback");
        AssertExact(rig.IconTransform.localScale, Vector3.one, "S6: no punch with the ready cue off");
        AssertExact(rig.Root.transform.localPosition, position, "S6: no shake with the blocked feedback off");
        AssertExact(ColorOf(rig.Frame), frameAfterSelect, "S6: no flash with the effects off");

        // Partial switches: a zero strength/duration removes just that part.
        var partial = NewSlot();
        SetField(partial.Ui, "readyPunchStrength", 0f);
        SetField(partial.Ui, "readyFlashDuration", 0f);
        var partialSlot = NewSpellSlot(5f, 2f);
        Call(partial.Ui, "UpdateSlotDisplay", partialSlot);
        SetField(partialSlot, "currentCooldown", 0f);
        Call(partial.Ui, "UpdateSlotDisplay", partialSlot);
        yield return Frames(3);
        AssertExact(partial.IconTransform.localScale, Vector3.one, "S6: readyPunchStrength = 0 removes the punch");
        AssertExact(ColorOf(partial.Frame), FrameBase, "S6: readyFlashDuration = 0 removes the flash");

        var mana = NewMana();
        SetField(mana.Ui, "spendPulseEnabled", false);
        Call(mana.System, "UseMana", 10f);
        yield return Frames(3);
        Assert.That(Count(mana.Ui, "PulseCount"), Is.Zero, "S6: spendPulseEnabled = false plays no mana pulse");
        AssertExact(mana.BarTransform.localScale, Vector3.one, "S6: no mana punch with the pulse off");
    }

    [UnityTest] public IEnumerator DisablingMidEffectKillsTweensAndRestoresExactBaseState()
    {
        yield return new EnterPlayMode();
        var rig = NewSlot();
        var slot = NewSpellSlot(5f, 2f);
        Call(rig.Ui, "UpdateSlotDisplay", slot);
        SetField(slot, "currentCooldown", 0f);
        Call(rig.Ui, "UpdateSlotDisplay", slot);
        Call(rig.Ui, "PlayBlockedFeedback");
        yield return Frames(2);
        rig.Root.SetActive(false);
        AssertExact(rig.IconTransform.localScale, Vector3.one, "S3: disabling mid-cue restores the icon base scale");
        AssertExact(rig.Root.transform.localPosition, new Vector3(3f, 4f, 0f), "S3: disabling mid-shake restores the base position");
        AssertExact(ColorOf(rig.Frame), FrameBase, "S3: disabling mid-flash restores the frame color");
        AssertExact(ColorOf(rig.Icon), IconBase, "S3: disabling mid-flash restores the icon color");

        var mana = NewMana();
        yield return null;
        Color baseFill = ColorOf(mana.Fill);
        Call(mana.System, "UseMana", 10f);
        yield return Frames(2);
        mana.Ui.gameObject.SetActive(false);
        AssertExact(mana.BarTransform.localScale, Vector3.one, "S3: disabling mid-pulse restores the mana bar scale");
        AssertExact(ColorOf(mana.Fill), baseFill, "S3: disabling mid-pulse restores the mana fill color");
    }
}
