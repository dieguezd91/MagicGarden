using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using F = DailyLoadoutUIFixture;

public sealed class DailyLoadoutCatalogCardTests
{
    private static Component[] Options(Component view, bool plants) =>
        ((IList)F.Property(view, plants ? "PlantOptionViews" : "SpellOptionViews")).Cast<Component>().ToArray();
    private static Component Layout(RectTransform content) => content.GetComponent(F.Production("MagicGarden.LoadoutUI.LoadoutOptionGridLayout"));
    private static void RefreshGrid(RectTransform content)
    {
        content.SendMessage("OnRectTransformDimensionsChange", SendMessageOptions.DontRequireReceiver);
        LayoutRebuilder.ForceRebuildLayoutImmediate(content); Canvas.ForceUpdateCanvases();
    }

    [Test]
    public void CatalogShowsOnlyNameAndIconEvenAfterPointerRefreshAndReadOnlyTransition()
    {
        using (var f = new F())
        {
            f.Open();
            bool equipmentCaption = ((TMP_Text)F.Field(f.Slot(true, 0), "stateLabel")).gameObject.activeSelf;
            foreach (bool plants in new[] { true, false })
            {
                f.Select(plants, 0);
                foreach (var option in Options(f.View, plants))
                {
                    var name = (TMP_Text)F.Field(option, "nameLabel");
                    var state = (TMP_Text)F.Field(option, "stateLabel");
                    Assert.That(state.gameObject.activeSelf, Is.False);
                    Assert.That(name.gameObject.activeSelf, Is.True);
                    Assert.That(option.GetComponentsInChildren<TMP_Text>(true).Count(t => t.gameObject.activeSelf), Is.EqualTo(1));
                }
                F.Call(f.Option(0), "OnPointerEnter", new PointerEventData(null));
                f.Assign(0); f.Refresh();
                Assert.That(((TMP_Text)F.Field(f.Option(0), "stateLabel")).gameObject.activeSelf, Is.False);
            }
            f.Action("Confirm").onClick.Invoke();
            Assert.That(((TMP_Text)F.Field(f.Option(0), "stateLabel")).gameObject.activeSelf, Is.False);
            Assert.That(((TMP_Text)F.Field(f.Slot(true, 0), "stateLabel")).gameObject.activeSelf, Is.EqualTo(equipmentCaption),
                "Equipment-position captions are outside the catalog-only simplification");
        }
    }

    [TestCase(true)] [TestCase(false)]
    public void CardsHonorSquarePrefabSizeAndRetainEachCategoryColumnConfiguration(bool plants)
    {
        using (var f = new F())
        {
            f.Open(); f.Select(plants, 0);
            var content = (RectTransform)F.Property(f.View, "OptionContent");
            var grid = content.GetComponent<GridLayoutGroup>(); int columns = grid.constraintCount;
            var source = (Component)F.Property(f.View, "OptionPrefab");
            var desired = ((RectTransform)source.transform).sizeDelta;
            RefreshGrid(content);
            Assert.That(grid.cellSize.x, Is.EqualTo(grid.cellSize.y).Within(.01f));
            Assert.That(grid.cellSize.x, Is.LessThanOrEqualTo(desired.x + .01f));
            Assert.That(grid.constraintCount, Is.EqualTo(columns));
            var icon = (Image)F.Field(f.Option(0), "icon");
            var iconBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(f.Option(0).transform, icon.transform.parent);
            var card = ((RectTransform)f.Option(0).transform).rect;
            Assert.That(iconBounds.min.x, Is.GreaterThanOrEqualTo(card.xMin - 1));
            Assert.That(iconBounds.max.x, Is.LessThanOrEqualTo(card.xMax + 1));
            Assert.That(iconBounds.max.y, Is.LessThanOrEqualTo(card.yMax + 1));
        }
    }

    [TestCase(true)] [TestCase(false)]
    public void ScrollingOnlyRunsForOverflowAndResetsWhenVisibleOptionsFitAgain(bool plants)
    {
        using (var f = new F())
        {
            f.Open(); f.Select(plants, 0);
            var content = (RectTransform)F.Property(f.View, "OptionContent");
            var scroll = (ScrollRect)F.Property(f.View, "OptionsScroll");
            var options = Options(f.View, plants);
            for (int i = 0; i < options.Length; i++) options[i].gameObject.SetActive(i == 0);
            RefreshGrid(content);
            Assert.That(scroll.vertical, Is.False, "A fully visible row cannot scroll");
            foreach (var option in options) option.gameObject.SetActive(true);
            RefreshGrid(content);
            Assert.That(content.rect.height, Is.GreaterThan(scroll.viewport.rect.height));
            Assert.That(scroll.vertical, Is.True);
            scroll.verticalNormalizedPosition = 0; scroll.velocity = new Vector2(0, 400);
            for (int i = 0; i < options.Length; i++) options[i].gameObject.SetActive(i == 0);
            RefreshGrid(content);
            Assert.That(scroll.vertical, Is.False); Assert.That(scroll.velocity, Is.EqualTo(Vector2.zero));
            Assert.That(content.anchoredPosition.y, Is.EqualTo(0).Within(.01f));
            var before = content.anchoredPosition;
            ExecuteEvents.Execute(scroll.gameObject, new PointerEventData(null) { scrollDelta = new Vector2(0, -1) }, ExecuteEvents.scrollHandler);
            Assert.That(content.anchoredPosition, Is.EqualTo(before));
            Assert.That(scroll.horizontal, Is.False);
        }
    }

    [Test]
    public void NarrowNativeAuthoringPreservesAllNonCatalogGeometryAndManualGridConfiguration()
    {
        var root = PrefabUtility.LoadPrefabContents("Assets/Prefabs/UI/DailyLoadout/DailyLoadoutPanel.prefab");
        try
        {
            var view = root.GetComponent(F.Production("MagicGarden.LoadoutUI.DailyLoadoutPanelView"));
            var options = Options(view, true).Concat(Options(view, false)).ToArray();
            var contents = new[] { (RectTransform)F.Field(view, "optionContent"), (RectTransform)F.Field(view, "spellOptionContent") };
            var protectedRects = root.GetComponentsInChildren<RectTransform>(true)
                .Where(r => !contents.Contains(r) && !options.Any(o => r.IsChildOf(o.transform))).ToDictionary(r => r,
                    r => new[] { r.anchorMin, r.anchorMax, r.anchoredPosition, r.sizeDelta, r.pivot });
            var grids = contents.Select(c => c.GetComponent<GridLayoutGroup>()).ToDictionary(g => g,
                g => new object[] { g.constraint, g.constraintCount, g.spacing, g.padding.left, g.padding.right, g.padding.top, g.padding.bottom, g.childAlignment });
            int count = root.GetComponentsInChildren<Component>(true).Length;
            var authoring = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "Assembly-CSharp-Editor")
                .GetType("DailyLoadoutCatalogCardAuthoring", true);
            authoring.GetMethod("ConfigurePanel").Invoke(null, new object[] { view });
            authoring.GetMethod("ConfigurePanel").Invoke(null, new object[] { view });
            Assert.That(root.GetComponentsInChildren<Component>(true).Length, Is.EqualTo(count));
            foreach (var pair in protectedRects) CollectionAssert.AreEqual(pair.Value,
                new[] { pair.Key.anchorMin, pair.Key.anchorMax, pair.Key.anchoredPosition, pair.Key.sizeDelta, pair.Key.pivot }, pair.Key.name);
            foreach (var pair in grids) CollectionAssert.AreEqual(pair.Value, new object[] { pair.Key.constraint,
                pair.Key.constraintCount, pair.Key.spacing, pair.Key.padding.left, pair.Key.padding.right,
                pair.Key.padding.top, pair.Key.padding.bottom, pair.Key.childAlignment });
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
