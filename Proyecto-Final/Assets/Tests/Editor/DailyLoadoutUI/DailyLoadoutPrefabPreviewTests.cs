using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using F = DailyLoadoutUIFixture;

public sealed class DailyLoadoutPrefabPreviewTests
{
    private const string Path = "Assets/Prefabs/UI/DailyLoadout/DailyLoadoutPanel.prefab";

    [TestCase("PlantOptionViews")]
    [TestCase("SpellOptionViews")]
    public void PrefabSerializesVisibleCatalogNamesIconsAndExclusiveStateWithoutPlayMode(string category)
    {
        Assert.That(Application.isPlaying, Is.False);
        var root = AssetDatabase.LoadAssetAtPath<GameObject>(Path);
        var view = root.GetComponent(F.Production("MagicGarden.LoadoutUI.DailyLoadoutPanelView"));
        var options = ((IList)F.Property(view, category)).Cast<Component>().ToArray();
        Assert.That(options.Length, Is.EqualTo(6));
        int populated = 0;
        foreach (var option in options)
        {
            Assert.That(option.gameObject.activeSelf, Is.True, "Authored option must be visible when its category panel is enabled");
            Assert.That(F.Name(option), Is.Not.Empty);
            CollectionAssert.Contains(new[] { "Selected", "Available", "Empty" }, F.State(option));
            var icon = (Image)F.Field(option, "icon");
            if (icon.sprite == null) continue;
            populated++;
            Assert.That(icon.enabled, Is.True);
            Assert.That(F.Name(option), Is.Not.EqualTo("Empty"));
        }
        Assert.That(populated, Is.GreaterThan(0), "Real authored catalog information must be visible, not only blank placeholders");
    }

    [Test]
    public void SerializedPreviewSurvivesEditModeInstantiationAndCanvasRebuild()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Path);
        var authored = prefab.GetComponentsInChildren<TMP_Text>(true).Select(t => t.text).ToArray();
        var root = UnityEngine.Object.Instantiate(prefab);
        try
        {
            Canvas.ForceUpdateCanvases();
            CollectionAssert.AreEqual(authored, root.GetComponentsInChildren<TMP_Text>(true).Select(t => t.text));
            var view = root.GetComponent(F.Production("MagicGarden.LoadoutUI.DailyLoadoutPanelView"));
            var detail = (TMP_Text)F.Field(view, "selectedEquipmentLabel");
            Assert.That(detail.text, Is.Not.Empty.And.Not.EqualTo("Empty"));
            Assert.That(((Image)F.Field(view, "selectedEquipmentIcon")).enabled, Is.True);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    [Test]
    public void RuntimeProjectionReusesEveryAuthoredComponentAndNeverEquipsPreviewData()
    {
        using (var f = new F())
        {
            var components = f.Panel.GetComponentsInChildren<Component>(true).Select(c => c.GetInstanceID()).ToArray();
            Assert.That(f.Open(), Is.True);
            foreach (bool plants in new[] { true, false })
            {
                f.Select(plants, 0);
                var options = ((IList)F.Property(f.View, "OptionViews")).Cast<Component>().ToArray();
                Assert.That(options.Count(o => o.gameObject.activeSelf), Is.EqualTo(2), "The real unlocked catalog replaces the Editor preview");
                for (int i = 0; i < 3; i++)
                {
                    Assert.That(f.HasValue(plants, false, i), Is.False);
                    Assert.That(f.HasValue(plants, true, i), Is.False);
                    Assert.That(F.Name(f.Slot(plants, i)), Is.EqualTo("Empty"));
                }
            }
            f.Assign(0); f.Action("Cancel").onClick.Invoke(); f.Open(); f.Refresh();
            CollectionAssert.AreEqual(components, f.Panel.GetComponentsInChildren<Component>(true).Select(c => c.GetInstanceID()));
            Assert.That(f.HasValue(false, true, 0), Is.False);
        }
    }

    [Test]
    public void ExplicitPreviewAuthoringIsIdempotentAndCreatesNoComponentsOrGeometry()
    {
        var root = PrefabUtility.LoadPrefabContents(Path);
        try
        {
            var view = root.GetComponent(F.Production("MagicGarden.LoadoutUI.DailyLoadoutPanelView"));
            var components = root.GetComponentsInChildren<Component>(true).Select(c => c.GetInstanceID()).ToArray();
            var rects = root.GetComponentsInChildren<RectTransform>(true).ToDictionary(r => r,
                r => new[] { r.anchorMin, r.anchorMax, r.anchoredPosition, r.sizeDelta, r.pivot });
            var authoring = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "Assembly-CSharp-Editor")
                .GetType("DailyLoadoutPrefabPreviewAuthoring", true);
            authoring.GetMethod("Populate").Invoke(null, new object[] { view });
            var captions = root.GetComponentsInChildren<TMP_Text>(true).Select(t => t.text).ToArray();
            authoring.GetMethod("Populate").Invoke(null, new object[] { view });
            CollectionAssert.AreEqual(components, root.GetComponentsInChildren<Component>(true).Select(c => c.GetInstanceID()));
            CollectionAssert.AreEqual(captions, root.GetComponentsInChildren<TMP_Text>(true).Select(t => t.text));
            foreach (var pair in rects) CollectionAssert.AreEqual(pair.Value,
                new[] { pair.Key.anchorMin, pair.Key.anchorMax, pair.Key.anchoredPosition, pair.Key.sizeDelta, pair.Key.pivot }, pair.Key.name);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
