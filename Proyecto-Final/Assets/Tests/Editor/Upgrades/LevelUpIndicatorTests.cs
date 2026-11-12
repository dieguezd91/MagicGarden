using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

// Reflection keeps the Assembly-CSharp boundary of the core test assembly.
public class LevelUpIndicatorTests
{
    private const string PlayerPrefab = "Assets/Prefabs/Characters/Player.prefab";
    private const string SheetGuid = "6b876b38e4597704b84b5e76e54d27a1";
    private readonly List<UnityEngine.Object> created = new List<UnityEngine.Object>();
    private static Type Production(string name) => Type.GetType(name + ", Assembly-CSharp", true);
    private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static object Call(object o, string name, params object[] args) => o.GetType().GetMethod(name, Any).Invoke(o, args);
    private static object Property(object o, string name) => o.GetType().GetProperty(name, Any).GetValue(o);
    private static void SetField(object o, string name, object value) => o.GetType().GetField(name, Any).SetValue(o, value);
    private static object Field(object o, string name) => o.GetType().GetField(name, Any).GetValue(o);
    [TearDown] public void Cleanup()
    {
        for (int i = created.Count - 1; i >= 0; i--) if (created[i] != null) UnityEngine.Object.DestroyImmediate(created[i]);
        created.Clear();
    }

    private static Sprite[] Sprites() => AssetDatabase.LoadAllAssetsAtPath(UpgradeUIArtTests.Sheet).OfType<Sprite>().ToArray();

    [Test] public void LevelUpIconSpriteIsNamedKeepsIdentityAndItsRectBoundsTheLabel()
    {
        var sprites = Sprites();
        Assert.That(sprites.Count(s => s.name == "LevelUpIcon"), Is.EqualTo(1));
        Assert.That(sprites.Any(s => s.name == "UISprites_33"), Is.False, "renamed, not duplicated");
        var icon = sprites.Single(s => s.name == "LevelUpIcon");
        Assert.That(icon.rect, Is.EqualTo(new Rect(880, 50, 31, 13)));
        Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(icon, out string guid, out long id), Is.True);
        Assert.That(guid, Is.EqualTo(SheetGuid)); Assert.That(id, Is.EqualTo(1786493580), "references to the sprite must survive");
        var meta = File.ReadAllText(UpgradeUIArtTests.Sheet + ".meta");
        Assert.That(meta, Does.Match(@"name: LevelUpIcon\s+rect:[\s\S]*?spriteID: 32a92da4ea4bf404a9688886f822ec52\s+internalID: 1786493580"));

        // No clipping: ink touches every rect edge and the pixels just outside the rect are transparent.
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false); created.Add(texture);
        Assert.That(ImageConversion.LoadImage(texture, File.ReadAllBytes(UpgradeUIArtTests.Sheet)), Is.True);
        int x0 = 880, y0 = 50, x1 = 880 + 31 - 1, y1 = 50 + 13 - 1;
        bool Ink(int x, int y) => x >= 0 && y >= 0 && x < texture.width && y < texture.height && texture.GetPixel(x, y).a > 0;
        Assert.That(Enumerable.Range(y0, 13).Any(y => Ink(x0, y)), Is.True, "left edge");
        Assert.That(Enumerable.Range(y0, 13).Any(y => Ink(x1, y)), Is.True, "right edge");
        Assert.That(Enumerable.Range(x0, 31).Any(x => Ink(x, y0)), Is.True, "bottom edge");
        Assert.That(Enumerable.Range(x0, 31).Any(x => Ink(x, y1)), Is.True, "top edge");
        Assert.That(Enumerable.Range(y0 - 1, 15).Any(y => Ink(x0 - 1, y) || Ink(x1 + 1, y)), Is.False, "ink outside left/right");
        Assert.That(Enumerable.Range(x0 - 1, 33).Any(x => Ink(x, y0 - 1) || Ink(x, y1 + 1)), Is.False, "ink outside top/bottom");
    }

    [Test] public void SheetImporterIsPointFilteredAtSixteenPixelsPerUnit()
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(UpgradeUIArtTests.Sheet);
        Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point));
        Assert.That(importer.spritePixelsPerUnit, Is.EqualTo(16));
    }

    [Test] public void PlayerPrefabCarriesAWorldSpaceIndicatorAboveThePlayerSprite()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab);
        var indicator = prefab.GetComponentInChildren(Production("LevelUpIndicator"), true);
        Assert.That(indicator, Is.Not.Null);
        Assert.That(indicator.transform, Is.Not.SameAs(prefab.transform));
        Assert.That(indicator.GetComponentInParent<Canvas>(true), Is.Null, "world-space SpriteRenderer, not UI");
        var icon = (SpriteRenderer)Field(indicator, "icon");
        Assert.That(icon, Is.Not.Null); Assert.That(icon.sprite, Is.Not.Null); Assert.That(icon.sprite.name, Is.EqualTo("LevelUpIcon"));
        var player = prefab.GetComponent<SpriteRenderer>();
        Assert.That(icon.sortingLayerID, Is.EqualTo(player.sortingLayerID));
        Assert.That(icon.sortingOrder, Is.GreaterThan(player.sortingOrder));
        Assert.That(indicator.transform.localPosition.y, Is.GreaterThanOrEqualTo(player.sprite.bounds.max.y), "starts above the head");
        Assert.That(Field(indicator, "experience"), Is.SameAs(prefab.GetComponent(Production("PlayerExperienceSystem"))));
    }

    private const string GlowMaterial = "Assets/Sprites/SpriteSheets/ShaderEffects/GlowEffect.mat";

    // The world sprites that stand out (player body, Spider) use this unlit glow material instead of the lit sprite default,
    // so the global day/night Light2D does not dim them. The indicator must use it as well.
    [Test] public void IndicatorUsesTheSameUnlitGlowMaterialAsThePlayerBody()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab);
        var indicator = prefab.GetComponentInChildren(Production("LevelUpIndicator"), true);
        var icon = (SpriteRenderer)Field(indicator, "icon");
        var glow = AssetDatabase.LoadAssetAtPath<Material>(GlowMaterial);
        Assert.That(glow, Is.Not.Null);
        Assert.That(prefab.GetComponent<SpriteRenderer>().sharedMaterial, Is.SameAs(glow), "premise: the player body uses GlowEffect");
        Assert.That(icon.sharedMaterial, Is.SameAs(glow));
    }

    [Test] public void PopOvershootsTheRestingScaleThenSettlesAtIt()
    {
        var indicator = Rig(out _, out var icon);
        float rest = indicator.transform.localScale.x;
        Call(indicator, "Play");
        Call(indicator, "Tick", 0.12f); // 60% of the 0.2s pop: the peak
        Assert.That(indicator.transform.localScale.x, Is.GreaterThan(rest * 1.15f), "visible overshoot");
        Call(indicator, "Tick", 0.08f);
        Assert.That(indicator.transform.localScale.x, Is.EqualTo(rest).Within(.001f), "settled at the end of the pop");
    }

    [Test] public void HoldKeepsBobbingAndBreathingAroundTheRisenPositionUntilItFades()
    {
        var indicator = Rig(out _, out var icon);
        Call(indicator, "Play");
        Call(indicator, "Tick", 0.6f); // pop + rise done: hold starts
        var samples = new List<Vector3>();
        var scales = new List<float>();
        for (int i = 0; i < 6; i++) { Call(indicator, "Tick", 0.05f); samples.Add(indicator.transform.localPosition); scales.Add(indicator.transform.localScale.x); }
        Assert.That(samples.Select(p => p.y).Distinct().Count(), Is.GreaterThan(3), "bobs while holding");
        Assert.That(scales.Max() - scales.Min(), Is.GreaterThan(.02f), "breathes while holding");
        Assert.That(samples.All(p => p.y > 1.5f), Is.True, "stays above the resting position");
        Assert.That(icon.color.a, Is.EqualTo(1f).Within(.001f));
    }

    private Component Rig(out Component experience, out SpriteRenderer icon)
    {
        var player = new GameObject("Indicator test player"); created.Add(player);
        experience = player.AddComponent(Production("PlayerExperienceSystem"));
        var progression = ScriptableObject.CreateInstance(Production("ExperienceProgressionDataSO")); created.Add(progression);
        SetField(experience, "progressionData", progression);
        var child = new GameObject("Indicator"); child.transform.SetParent(player.transform, false);
        child.transform.localPosition = new Vector3(0, 1.5f, 0);
        icon = child.AddComponent<SpriteRenderer>();
        var indicator = child.AddComponent(Production("LevelUpIndicator"));
        SetField(indicator, "experience", experience); SetField(indicator, "icon", icon);
        Call(indicator, "Awake"); Call(indicator, "OnEnable");
        return indicator;
    }

    [Test] public void IndicatorPlaysOnLevelUpAndStopsAfterItsSerializedDuration()
    {
        var indicator = Rig(out var experience, out var icon);
        float duration = (float)Property(indicator, "Duration");
        Assert.That(duration, Is.InRange(1.0f, 1.4f));
        Assert.That(Property(indicator, "IsPlaying"), Is.False); Assert.That(icon.enabled, Is.False);
        Call(experience, "AddExperience", 100);
        Assert.That(Property(indicator, "IsPlaying"), Is.True);
        Assert.That(icon.enabled, Is.True);
        float restY = 1.5f;
        Call(indicator, "Tick", duration * .6f);
        Assert.That(Property(indicator, "IsPlaying"), Is.True);
        Assert.That(indicator.transform.localPosition.y, Is.GreaterThan(restY), "rises");
        Assert.That(icon.color.a, Is.EqualTo(1f).Within(.001f), "holds opaque before fading");
        Call(indicator, "Tick", duration * .3f);
        Assert.That(Property(indicator, "IsPlaying"), Is.True);
        Assert.That(icon.color.a, Is.LessThan(1f), "fading");
        Call(indicator, "Tick", duration * .11f);
        Assert.That(Property(indicator, "IsPlaying"), Is.False);
        Assert.That(icon.enabled, Is.False);
        Assert.That(indicator.transform.localPosition.y, Is.EqualTo(restY).Within(.0001f));
    }

    [Test] public void SeveralLevelsAtOnceRestartOneIndicatorInsteadOfStacking()
    {
        var indicator = Rig(out var experience, out var icon);
        float duration = (float)Property(indicator, "Duration");
        Call(experience, "AddExperience", 450); // levels 2, 3 and 4 in one call
        Assert.That(Property(indicator, "IsPlaying"), Is.True);
        Assert.That(indicator.GetComponents(Production("LevelUpIndicator")).Length, Is.EqualTo(1));
        Call(indicator, "Tick", duration * .9f);
        Call(experience, "AddExperience", 300); // level 5 while still playing
        Call(indicator, "Tick", duration * .2f);
        Assert.That(Property(indicator, "IsPlaying"), Is.True, "restarted");
        Assert.That(icon.color.a, Is.EqualTo(1f).Within(.001f));
        Call(indicator, "Tick", duration);
        Assert.That(Property(indicator, "IsPlaying"), Is.False);
    }

    [Test] public void ProgressionResetStopsThePlayingIndicator()
    {
        var indicator = Rig(out var experience, out var icon);
        Call(experience, "AddExperience", 100);
        Assert.That(Property(indicator, "IsPlaying"), Is.True);
        Call(experience, "ResetProgression");
        Assert.That(Property(indicator, "IsPlaying"), Is.False); Assert.That(icon.enabled, Is.False);
    }
}
