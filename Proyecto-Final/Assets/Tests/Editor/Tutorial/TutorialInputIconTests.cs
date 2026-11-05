using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class TutorialInputIconTests
{
    static Type TypeOf(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
    static object Get(object value, string name)
    {
        var property = value.GetType().GetProperty(name);
        return property != null ? property.GetValue(value) : value.GetType().GetField(name).GetValue(value);
    }

    [TestCase("32923ab09714fac4797e4fee3fcfd529", "SpaceKey", @"(?i)\bSpace\b")]
    [TestCase("32923ab09714fac4797e4fee3fcfd529", "TABKey", @"(?i)\bTab\b")]
    [TestCase("32923ab09714fac4797e4fee3fcfd529", "IKey", @"\bI\b")]
    [TestCase("32923ab09714fac4797e4fee3fcfd529", "MouseClick", @"(?i)left[- ]click")]
    [TestCase("16ffda535a901394d8ffed95633194d3", "ScrollMouse", @"(?i)mouse\s+wheel")]
    public void AvailableInputUsesItsIconInsteadOfLiteralBinding(string guid, string icon, string literal)
    {
        var step = AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(guid));
        Assert.That(step, Is.Not.Null);
        var text = (string)Get(step, "DisplayText");
        Assert.That(text, Does.Contain("<sprite name=\"" + icon + "\">"));
        var visibleWords = System.Text.RegularExpressions.Regex.Replace(text, "<[^>]+>", "");
        Assert.That(visibleWords, Does.Not.Match(literal), "Do not duplicate the icon with a literal binding.");
    }

    [TestCase("SpaceKey")]
    [TestCase("TABKey")]
    [TestCase("IKey")]
    [TestCase("ScrollMouse")]
    [TestCase("ArrowKeys")]
    [TestCase("WASDKeys")]
    [TestCase("QKey")]
    [TestCase("EKey")]
    [TestCase("FKey")]
    [TestCase("MouseClick")]
    public void InstalledTutorialSpriteHasRenderableAtlasGlyph(string name)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/Canvas/Game/Panel Tutorial.prefab");
        var label = prefab.GetComponentsInChildren(TypeOf("TMPro.TextMeshProUGUI"), true)
            .Single(c => c.name == "InstructionText");
        var asset = (UnityEngine.Object)Get(label, "spriteAsset");
        Assert.That(AssetDatabase.GetAssetPath(asset), Is.EqualTo("Assets/Sprites/SpriteSheets/UISprites.asset"));
        var table = (System.Collections.IEnumerable)Get(asset, "spriteCharacterTable");
        var character = table.Cast<object>().SingleOrDefault(c => (string)Get(c, "name") == name);
        Assert.That(character, Is.Not.Null, $"{name} must be registered in the actual tutorial TMP asset, not merely sliced in the PNG.");
        var glyph = Get(character, "glyph");
        Assert.That(glyph, Is.Not.Null);
        var rect = Get(glyph, "glyphRect");
        var atlas = (Texture)Get(asset, "spriteSheet");
        Assert.That(atlas, Is.Not.Null);
        Assert.That(Get(Get(asset, "material"), "mainTexture"), Is.SameAs(atlas));
        var x = (int)Get(rect, "x"); var y = (int)Get(rect, "y");
        var width = (int)Get(rect, "width"); var height = (int)Get(rect, "height");
        Assert.That(width, Is.GreaterThan(0)); Assert.That(height, Is.GreaterThan(0));
        Assert.That(x, Is.GreaterThanOrEqualTo(0)); Assert.That(y, Is.GreaterThanOrEqualTo(0));
        Assert.That(x + width, Is.LessThanOrEqualTo(atlas.width));
        Assert.That(y + height, Is.LessThanOrEqualTo(atlas.height));
        Assert.That(Convert.ToSingle(Get(Get(glyph, "metrics"), "horizontalAdvance")), Is.GreaterThan(0));
        var sprite = (Sprite)Get(glyph, "sprite");
        Assert.That(sprite, Is.Not.Null, "Reuse the already imported atlas slice.");
        Assert.That(sprite.name, Is.EqualTo(name));
        Assert.That(sprite.texture, Is.SameAs(atlas));
        Assert.That(sprite.rect, Is.EqualTo(new Rect(x, y, width, height)));
    }
}
