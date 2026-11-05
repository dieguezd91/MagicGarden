using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class TutorialScenePrerequisiteTests
{
    static Type Production(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Single(a => a.GetName().Name == "Assembly-CSharp").GetType(name, true);
    static object Property(object target, string name) => target.GetType().GetProperty(name).GetValue(target);
    static object Field(object target, string name) => target.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(target);
    static object Call(object target, string name, int index) => target.GetType().GetMethod(name)
        .Invoke(target, new object[] { index });
    static Component Single(Scene scene, string type) => scene.GetRootGameObjects()
        .SelectMany(root => root.GetComponentsInChildren(Production(type), true)).Single();

    [Test]
    public void GameSceneLevelOneChoicesHaveAuthoredStockAndResolvableInventoryOwnership()
    {
        Assert.That(EditorApplication.isPlayingOrWillChangePlaymode, Is.False);
        var active = SceneManager.GetActiveScene();
        var loaded = Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt)
            .Select(scene => scene.handle).ToArray();
        Scene inspectedScene = SceneManager.GetSceneByPath("Assets/Scenes/Game.unity");
        bool openedScene = false;
        try
        {
            // Resolve overrides outside Play Mode without replacing or saving loaded scenes.
            // Never call Awake/Start, rebuild unlocks, confirm equipment or save this scene.
            if (!inspectedScene.IsValid() || !inspectedScene.isLoaded)
            {
                inspectedScene = EditorSceneManager.OpenScene("Assets/Scenes/Game.unity", OpenSceneMode.Additive);
                openedScene = true;
            }
            var owner = Single(inspectedScene, "PlayerContentUnlockSystem");
            var experience = Single(inspectedScene, "PlayerExperienceSystem");
            var seeds = Single(inspectedScene, "SeedInventory");
            var spells = Single(inspectedScene, "SpellInventory");
            var authoredExperience = (Component)Field(owner, "playerExperienceSystem");
            Assert.That(authoredExperience != null ? authoredExperience : owner.GetComponent(Production("PlayerExperienceSystem")),
                Is.EqualTo(experience), "Unlock owner must resolve the scene player's experience system.");
            var authoredSpells = (Component)Field(owner, "spellInventory");
            Assert.That(authoredSpells == null || authoredSpells == spells, Is.True,
                "A missing serialized spell reference uses the unique scene inventory at natural Start.");
            Assert.That(Property(experience, "CurrentLevel"), Is.EqualTo(1));
            var progression = Property(experience, "ProgressionData");
            Assert.That(progression, Is.Not.Null);
            var levelOne = Call(progression, "GetLevelData", 1);
            Assert.That(levelOne, Is.Not.Null);
            var plants = ((IEnumerable)Property(levelOne, "PlantUnlocks")).Cast<object>().ToArray();
            var starterSpells = ((IEnumerable)Property(levelOne, "SpellUnlocks")).Cast<object>().ToArray();
            Assert.That(plants, Is.Not.Empty);
            Assert.That(starterSpells, Is.Not.Empty);
            var slots = Enumerable.Range(0, (int)Property(seeds, "PlantSlotsCount"))
                .Select(i => Call(seeds, "GetPlantSlot", i)).Where(slot => slot != null).ToArray();
            foreach (var plant in plants)
            {
                Assert.That(plant, Is.Not.Null);
                var prefab = Field(plant, "plantPrefab");
                Assert.That(prefab, Is.Not.Null);
                var matches = slots.Where(slot => Equals(Field(slot, "seedType"), Field(plant, "seedType"))).ToArray();
                Assert.That(matches, Is.Not.Empty, "Each level-one choice needs scene-resolved seed stock.");
                Assert.That(matches.Any(slot => Equals(Field(slot, "plantPrefab"), prefab) &&
                    (int)Field(slot, "seedCount") >= 1), Is.True, ((UnityEngine.Object)plant).name);
            }
            var indices = starterSpells.Select(spell => (int)Property(spell, "SlotIndex")).ToArray();
            Assert.That(indices.Distinct().Count(), Is.EqualTo(indices.Length));
            foreach (var spell in starterSpells)
            {
                Assert.That(spell, Is.Not.Null);
                Assert.That(Property(spell, "SpellPrefab"), Is.Not.Null);
                Assert.That(Call(spells, "GetSpellSlot", (int)Property(spell, "SlotIndex")), Is.Not.Null,
                    "Starter spell index must resolve an authored inventory slot, not runtime readiness.");
            }
        }
        finally
        {
            if (openedScene && inspectedScene.IsValid()) EditorSceneManager.CloseScene(inspectedScene, true);
            if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active);
        }
        CollectionAssert.AreEqual(loaded, Enumerable.Range(0, SceneManager.sceneCount)
            .Select(SceneManager.GetSceneAt).Select(scene => scene.handle).ToArray(), "Loaded scene context must be unchanged.");
        Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(active));
    }
}
