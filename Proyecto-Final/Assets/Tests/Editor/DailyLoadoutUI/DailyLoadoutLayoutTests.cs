using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using F = DailyLoadoutUIFixture;

public sealed class DailyLoadoutLayoutTests
{
    [TestCase("LoadoutSlot", "LoadoutSlotView")]
    [TestCase("LoadoutOption", "LoadoutOptionView")]
    public void WrittenFeedbackIsOneExclusiveStateAcrossPointerAndDragFeedback(string prefabName, string type)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/DailyLoadout/" + prefabName + ".prefab");
        var root = UnityEngine.Object.Instantiate(prefab);
        try
        {
            var view = root.GetComponent(F.Production("MagicGarden.LoadoutUI." + type));
            F.Call(view, "SetContent", F.Field(view, "normalSprite"), "Equipment");
            Assert.That(F.State(view), Is.EqualTo("Available"));
            F.Call(view, "SetAssigned", true);
            F.Call(view, "SetSelected", true);
            F.Call(view, "SetHighlighted", true);
            F.Call(view, "OnPointerEnter", new PointerEventData(null));
            foreach (bool valid in new[] { true, false })
            {
                F.Call(view, "SetDropFeedback", true, true, valid);
                Assert.That(F.State(view), Is.EqualTo("Selected"));
            }
            F.Call(view, "SetReadOnly", true);
            Assert.That(F.State(view), Is.EqualTo("Selected"));
            F.Call(view, "SetAssigned", false); F.Call(view, "SetSelected", false);
            Assert.That(F.State(view), Is.EqualTo("Available"));
            F.Call(view, "SetContent", null, null);
            Assert.That(F.State(view), Is.EqualTo("Empty"));
            F.Call(view, "SetSelected", true);
            Assert.That(F.State(view), Is.EqualTo("Selected"), "Selected position takes precedence, even when empty");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    [Test]
    public void DistinctCategoryPanelsSwitchThroughTabsAndPreserveDraftAndBindings()
    {
        using (var f = new F())
        {
            var plants = (GameObject)F.Field(f.View, "plantOptionsPanel");
            var spells = (GameObject)F.Field(f.View, "spellOptionsPanel");
            var plantTab = (Button)F.Field(f.View, "plantTabButton");
            var spellTab = (Button)F.Field(f.View, "spellTabButton");
            Assert.That(plants, Is.Not.SameAs(spells));
            int bindingCount = -1;
            for (int cycle = 0; cycle < 3; cycle++)
            {
                Assert.That(f.Open(), Is.True);
                int count = ((IList)F.Field(f.Presenter, "bindings")).Count;
                if (bindingCount < 0) bindingCount = count;
                Assert.That(count, Is.EqualTo(bindingCount));
                Assert.That(plants.activeSelf, Is.True); Assert.That(spells.activeSelf, Is.False);
                Assert.That(f.Option(0).transform.IsChildOf(plants.transform), Is.True);
                f.Assign(0);
                spellTab.onClick.Invoke();
                Assert.That(F.Property(f.Presenter, "IsPlantCategory"), Is.False);
                Assert.That(plants.activeSelf, Is.False); Assert.That(spells.activeSelf, Is.True);
                Assert.That(f.Option(0).transform.IsChildOf(spells.transform), Is.True);
                Assert.That(F.Name(f.Option(0)), Is.EqualTo("First authored spell"));
                f.Assign(0);
                Assert.That(f.HasValue(true, false, 0), Is.True); Assert.That(f.HasValue(false, false, 0), Is.True);
                Assert.That(f.HasValue(true, true, 0), Is.False); Assert.That(f.HasValue(false, true, 0), Is.False);
                plantTab.onClick.Invoke();
                Assert.That(F.State(f.Option(0)), Is.EqualTo("Selected"));
                f.Action("Cancel").onClick.Invoke();
                Assert.That(((IList)F.Field(f.Presenter, "bindings")).Count, Is.Zero);
                Assert.That(f.HasValue(true, false, 0), Is.False); Assert.That(f.HasValue(false, false, 0), Is.False);
            }
        }
    }

    [Test]
    public void ReferenceLayoutGroupsEquipmentAboveCatalogWithSeparatePositionDetail()
    {
        using (var f = new F())
        {
            f.Open(); Canvas.ForceUpdateCanvases();
            var equipment = f.Panel.transform.Find("Stone window/Main stone table");
            var catalog = f.Panel.transform.Find("Stone window/Available panel");
            var detail = f.Panel.transform.Find("Stone window/Selected position panel");
            Assert.That(equipment, Is.Not.Null); Assert.That(catalog, Is.Not.Null); Assert.That(detail, Is.Not.Null);
            Assert.That(f.Panel.GetComponentsInChildren<TMPro.TMP_Text>(true)
                .Any(t => t.text.StartsWith("AVAILABLE OPTIONS", StringComparison.Ordinal)), Is.False, "No obsolete floating catalog heading");
            Assert.That(((RectTransform)equipment).anchoredPosition.y, Is.GreaterThan(((RectTransform)catalog).anchoredPosition.y));
            Assert.That(((RectTransform)detail).anchoredPosition.x, Is.GreaterThan(((RectTransform)catalog).anchoredPosition.x));
            f.Select(false, 2);
            var title = (TMPro.TMP_Text)F.Field(f.View, "selectedPositionLabel");
            Assert.That(title.text, Is.EqualTo("Spell 3"));
            var name = (TMPro.TMP_Text)F.Field(f.View, "selectedEquipmentLabel");
            Assert.That(name.text, Is.EqualTo("Empty"));
            f.Assign(0);
            Assert.That(name.text, Is.EqualTo(F.Name(f.Slot(false, 2))));
            f.Action("Confirm").onClick.Invoke();
            ((Button)F.Field(f.View, "plantTabButton")).onClick.Invoke();
            Assert.That(F.Property(f.Presenter, "IsPlantCategory"), Is.True);
            Assert.That(f.Action("Confirm").interactable, Is.False);
        }
    }

    [Test]
    public void NativeLayoutAuthoringIsIdempotentAndPreservesBoundEquipmentAndAssets()
    {
        var root = PrefabUtility.LoadPrefabContents("Assets/Prefabs/UI/DailyLoadout/DailyLoadoutPanel.prefab");
        try
        {
            var view = root.GetComponent(F.Production("MagicGarden.LoadoutUI.DailyLoadoutPanelView"));
            var slots = ((IList)F.Property(view, "PlantSlots")).Cast<Component>()
                .Concat(((IList)F.Property(view, "SpellSlots")).Cast<Component>()).ToArray();
            var authoring = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "Assembly-CSharp-Editor")
                .GetType("DailyLoadoutLayoutAuthoring", true);
            authoring.GetMethod("Configure").Invoke(null, new object[] { view });
            var transforms = root.GetComponentsInChildren<RectTransform>(true).ToDictionary(t => t,
                t => new[] { t.anchorMin, t.anchorMax, t.anchoredPosition, t.sizeDelta, t.pivot });
            var images = root.GetComponentsInChildren<Image>(true).ToDictionary(i => i, i => i.sprite);
            authoring.GetMethod("Configure").Invoke(null, new object[] { view });
            Assert.That(root.GetComponentsInChildren<RectTransform>(true).Length, Is.EqualTo(transforms.Count));
            foreach (var pair in transforms) CollectionAssert.AreEqual(pair.Value,
                new[] { pair.Key.anchorMin, pair.Key.anchorMax, pair.Key.anchoredPosition, pair.Key.sizeDelta, pair.Key.pivot });
            foreach (var pair in images) Assert.That(pair.Key.sprite, Is.SameAs(pair.Value));
            CollectionAssert.AreEqual(slots, ((IList)F.Property(view, "PlantSlots")).Cast<Component>()
                .Concat(((IList)F.Property(view, "SpellSlots")).Cast<Component>()));
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
