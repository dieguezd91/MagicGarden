using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using F = DailyLoadoutUIFixture;

public sealed class DailyLoadoutFunctionalPrefabTests
{
    [Test]
    public void AuthoredCategoryCardsHaveIndependentBindingsAndPanelCanOpen()
    {
        using (var f = new F())
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Path);
            var view = prefab.GetComponent(F.Production("MagicGarden.LoadoutUI.DailyLoadoutPanelView"));
            var data = new SerializedObject(view);
            var plants = data.FindProperty("optionViews");
            var spells = data.FindProperty("spellOptionViews");
            var seen = new System.Collections.Generic.HashSet<UnityEngine.Object>();
            foreach (var list in new[] { plants, spells })
                for (int i = 0; i < list.arraySize; i++)
                {
                    var card = list.GetArrayElementAtIndex(i).objectReferenceValue;
                    Assert.That(card, Is.Not.Null);
                    Assert.That(seen.Add(card), Is.True, "Each category must reference its own authored cards");
                    Assert.That(PrefabUtility.GetCorrespondingObjectFromOriginalSource((Component)card),
                        Is.SameAs(F.Property(view, "OptionPrefab")), "Both categories must use the same card prefab");
                    var content = (Transform)data.FindProperty(list == plants ? "optionContent" : "spellOptionContent")
                        .objectReferenceValue;
                    Assert.That(((Component)card).transform.IsChildOf(content), Is.True);
                }
            f.Open();
            Assert.That((bool)F.Property(f.Presenter, "IsOpen"), Is.True);
        }
    }

    private const string Path = "Assets/Prefabs/UI/DailyLoadout/DailyLoadoutPanel.prefab";

    [Test]
    public void ProductionPrefabHasBoundPresenterAndExplicitCatalogCapacity()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Path);
        Assert.That(prefab, Is.Not.Null);
        var view = prefab.GetComponent(F.Production("MagicGarden.LoadoutUI.DailyLoadoutPanelView"));
        Assert.That(prefab.GetComponent(F.Production("MagicGarden.LoadoutUI.DailyLoadoutPresenter")), Is.Not.Null,
            "Run Bind functional UI (preserve visuals); do not regenerate Stage 4A.");
        var serialized = new SerializedObject(view);
        var pool = serialized.FindProperty("optionViews");
        Assert.That(pool.arraySize, Is.GreaterThanOrEqualTo(6));
        var content = (RectTransform)F.Property(view, "OptionContent");
        var unique = new System.Collections.Generic.HashSet<UnityEngine.Object>();
        for (int i = 0; i < pool.arraySize; i++)
        {
            var option = (Component)pool.GetArrayElementAtIndex(i).objectReferenceValue;
            Assert.That(option, Is.Not.Null); Assert.That(unique.Add(option), Is.True);
            Assert.That(option.transform.parent, Is.SameAs(content));
            Assert.That(option.gameObject.activeSelf, Is.True, "Authored preview remains visible outside Play Mode; the presenter projects real runtime availability");
            Assert.That(F.SelectButton(option), Is.Not.Null);
        }
        foreach (var behaviour in prefab.GetComponentsInChildren<MonoBehaviour>(true)) Assert.That(behaviour, Is.Not.Null);
        foreach (var button in prefab.GetComponentsInChildren<Button>(true))
            Assert.That(button.onClick.GetPersistentEventCount(), Is.Zero);
    }

    [Test]
    public void AdditiveAuthoringPreservesExistingTransformsArtSlotsAndIsIdempotent()
    {
        var root = PrefabUtility.LoadPrefabContents(Path);
        try
        {
            var transforms = root.GetComponentsInChildren<RectTransform>(true).ToDictionary(t => t,
                t => new[] { t.anchorMin, t.anchorMax, t.anchoredPosition, t.sizeDelta, t.pivot });
            var images = root.GetComponentsInChildren<Image>(true).ToDictionary(i => i, i => i.sprite);
            var view = root.GetComponent(F.Production("MagicGarden.LoadoutUI.DailyLoadoutPanelView"));
            var slots = new SerializedObject(view);
            var originalSlots = new UnityEngine.Object[6];
            for (int i = 0; i < 6; i++) originalSlots[i] = slots.FindProperty(i < 3 ? "plantSlots" : "spellSlots")
                .GetArrayElementAtIndex(i % 3).objectReferenceValue;
            var authoring = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "Assembly-CSharp-Editor")
                .GetType("DailyLoadoutUIIntegrationAuthoring", true);
            authoring.GetMethod("Configure").Invoke(null, new object[] { view });
            int count = root.GetComponentsInChildren<Transform>(true).Length;
            authoring.GetMethod("Configure").Invoke(null, new object[] { view });
            Assert.That(root.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(count));
            Assert.That(root.GetComponents(F.Production("MagicGarden.LoadoutUI.DailyLoadoutPresenter")).Length, Is.EqualTo(1));
            foreach (var pair in transforms)
                CollectionAssert.AreEqual(pair.Value, new[] { pair.Key.anchorMin, pair.Key.anchorMax,
                    pair.Key.anchoredPosition, pair.Key.sizeDelta, pair.Key.pivot }, pair.Key.name);
            foreach (var pair in images) Assert.That(pair.Key.sprite, Is.SameAs(pair.Value), pair.Key.name);
            slots.Update();
            for (int i = 0; i < 6; i++)
                Assert.That(slots.FindProperty(i < 3 ? "plantSlots" : "spellSlots").GetArrayElementAtIndex(i % 3)
                    .objectReferenceValue, Is.SameAs(originalSlots[i]));
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
