using System;
using System.Collections;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using F = DailyLoadoutUIFixture;

public sealed class DailyLoadoutEquippedHudTests
{
    private F f;
    [SetUp] public void Setup() { f = new F(); }
    [TearDown] public void Cleanup() { f?.Dispose(); }
    private static Image Icon(Component slot) => (Image)F.Field(slot, "slotIcon");
    private static TMP_Text Count(Component slot) => (TMP_Text)F.Field(slot, "seedCountText");

    [TestCase(true, true)] [TestCase(false, true)]
    [TestCase(true, false)] [TestCase(false, false)]
    public void EmptyKeyboardAndClickAttemptsLeaveSelectionUnchanged(bool plants, bool initiallySelected)
    {
        f.EquipInitial();
        if (plants) Assert.That(F.Call(f.Seeds, "TryGrantUnlockedPlant", f.FirstPlant, 3), Is.True);
        var ability = f.Unlocks.gameObject.AddComponent(F.Production("PlayerAbilitySystem"));
        F.Call(ability, "Awake"); F.Set(ability, "seedInventory", f.Seeds);
        if (!plants) F.Call(f.Days, "StartNight");
        var hud = f.Hud(plants);
        var owner = plants ? f.Seeds : f.Spells;
        F.Call(owner, "ClearSelection");
        if (initiallySelected) Assert.That(F.Call(ability, "TrySelectEquipmentPosition", 0), Is.True);
        var before = F.Call(owner, "GetSelectedSlotIndex");
        F.Call(ability, "HandleEquipmentSlotInput", plants ? 2 : 5);
        Assert.That(F.Call(owner, "GetSelectedSlotIndex"), Is.EqualTo(before));
        if (plants) F.Call(hud, "SelectSlot", 2);
        else ((UnityEngine.Events.UnityEvent)F.Field(F.HudSlot(hud, false, 2), "OnClick")).Invoke();
        Assert.That(F.Call(owner, "GetSelectedSlotIndex"), Is.EqualTo(before));
    }

    [TestCase(0)] [TestCase(1)] [TestCase(2)]
    [TestCase(3)] [TestCase(4)] [TestCase(5)]
    public void NumericShortcutsSelectOnlyTheirOwnCategoryAndRespectPhase(int keyIndex)
    {
        f.EquipInitial();
        var ability = f.Unlocks.gameObject.AddComponent(F.Production("PlayerAbilitySystem"));
        F.Call(ability, "Awake"); F.Set(ability, "seedInventory", f.Seeds);
        bool plants = keyIndex < 3;
        int position = keyIndex % 3;
        if (!plants) F.Call(f.Days, "StartNight");
        F.Call(f.Seeds, "ClearSelection"); F.Call(f.Spells, "ClearSelection");
        var owner = plants ? f.Seeds : f.Spells;
        int expected = -1;
        if (position < 2)
        {
            object identity = plants ? F.Seed(position == 0 ? 7 : 3) : position == 0 ? f.FirstSpell : f.SecondSpell;
            expected = (int)F.Property(F.Call(f.Session, plants ? "ResolvePlant" : "ResolveSpell", identity), "InventoryIndex");
        }
        F.Call(ability, "HandleEquipmentSlotInput", keyIndex);
        Assert.That(F.Call(owner, "GetSelectedSlotIndex"), Is.EqualTo(expected));
        Assert.That(F.Call(plants ? f.Spells : f.Seeds, "GetSelectedSlotIndex"), Is.EqualTo(-1));
        F.Call(f.Days, plants ? "StartNight" : "StartDay");
        var seedBefore = F.Call(f.Seeds, "GetSelectedSlotIndex");
        var spellBefore = F.Call(f.Spells, "GetSelectedSlotIndex");
        F.Call(ability, "HandleEquipmentSlotInput", keyIndex);
        Assert.That(F.Call(f.Seeds, "GetSelectedSlotIndex"), Is.EqualTo(seedBefore));
        Assert.That(F.Call(f.Spells, "GetSelectedSlotIndex"), Is.EqualTo(spellBefore));
    }

    [TestCase(-1)] [TestCase(6)] [TestCase(8)]
    public void NumericShortcutsOutsideTheSixEquipmentKeysAreIgnored(int keyIndex)
    {
        f.EquipInitial();
        var ability = f.Unlocks.gameObject.AddComponent(F.Production("PlayerAbilitySystem"));
        F.Call(ability, "Awake"); F.Set(ability, "seedInventory", f.Seeds);
        foreach (bool night in new[] { false, true })
        {
            if (night) F.Call(f.Days, "StartNight");
            var seedBefore = F.Call(f.Seeds, "GetSelectedSlotIndex");
            var spellBefore = F.Call(f.Spells, "GetSelectedSlotIndex");
            F.Call(ability, "HandleEquipmentSlotInput", keyIndex);
            Assert.That(F.Call(f.Seeds, "GetSelectedSlotIndex"), Is.EqualTo(seedBefore));
            Assert.That(F.Call(f.Spells, "GetSelectedSlotIndex"), Is.EqualTo(spellBefore));
        }
    }

    [Test]
    public void NativeSpellLabelAuthoringIsIdempotentAndRetainsFunctionalBindings()
    {
        var root = PrefabUtility.LoadPrefabContents("Assets/Prefabs/UI/Slots/Spell Slot.prefab");
        try
        {
            var slot = root.GetComponent(F.Production("SpellSlotUI"));
            var icon = F.Field(slot, "slotIcon"); var overlay = F.Field(slot, "cooldownOverlay"); var cooldown = F.Field(slot, "cooldownText");
            var rect = (RectTransform)root.transform; var size = rect.sizeDelta;
            Type authoring = null;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) authoring = authoring ?? assembly.GetType("DailyLoadoutHudAuthoring");
            var method = authoring.GetMethod("ConfigureSpellPositionLabel");
            method.Invoke(null, new object[] { root }); method.Invoke(null, new object[] { root });
            Assert.That(F.Field(slot, "slotNumberText"), Is.SameAs(root.transform.Find("Slot_Text").GetComponent<TMP_Text>()));
            Assert.That(root.transform.Find("SpellCount_Text").gameObject.activeSelf, Is.False);
            Assert.That(F.Field(slot, "slotIcon"), Is.SameAs(icon)); Assert.That(F.Field(slot, "cooldownOverlay"), Is.SameAs(overlay));
            Assert.That(F.Field(slot, "cooldownText"), Is.SameAs(cooldown)); Assert.That(rect.sizeDelta, Is.EqualTo(size));
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    [TestCase(true)]
    [TestCase(false)]
    public void NonEquipmentSlotsRetainTheirExistingSelectionColorsAndScale(bool plants)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(plants
            ? "Assets/Prefabs/UI/Seeds/Generic Seed Button.prefab" : "Assets/Prefabs/UI/Slots/Spell Slot.prefab");
        var root = UnityEngine.Object.Instantiate(prefab);
        try
        {
            var slot = root.GetComponent(F.Production(plants ? "SeedSlot" : "SpellSlotUI"));
            F.Call(slot, "SetSelected", true);
            Assert.That(((Image)F.Field(slot, "slotBackground")).color, Is.EqualTo((Color)F.Field(slot, "selectedColor")));
            Assert.That(root.transform.localScale.x, Is.EqualTo((float)F.Field(slot, "selectedScale")));
            F.Call(slot, "SetSelected", false);
            Assert.That(((Image)F.Field(slot, "slotBackground")).color, Is.EqualTo((Color)F.Field(slot, "normalColor")));
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    [Test]
    public void EquipmentPositionsUseTheSameFrameIconGeometryAndSingleBottomRightNumber()
    {
        var plants = f.Hud(true); var spells = f.Hud(false);
        for (int i = 0; i < 3; i++)
        {
            var plant = F.HudSlot(plants, true, i); var spell = F.HudSlot(spells, false, i);
            var pf = (Image)F.Field(plant, "slotBackground"); var sf = (Image)F.Field(spell, "slotBackground");
            Assert.That(sf.sprite, Is.SameAs(pf.sprite)); Assert.That(sf.type, Is.EqualTo(pf.type));
            Assert.That(sf.sprite.border, Is.EqualTo(pf.sprite.border));
            Assert.That(((RectTransform)spell.transform).sizeDelta, Is.EqualTo(((RectTransform)plant.transform).sizeDelta));
            Assert.That(Icon(spell).rectTransform.sizeDelta, Is.EqualTo(Icon(plant).rectTransform.sizeDelta));
            Assert.That(Icon(spell).rectTransform.anchoredPosition, Is.EqualTo(Icon(plant).rectTransform.anchoredPosition));
            var pn = (TMP_Text)F.Field(plant, "slotNumber"); var sn = (TMP_Text)F.Field(spell, "slotNumberText");
            Assert.That(sn.name, Is.EqualTo("Slot_Text"), "Use the existing bottom-right position, not the green count-shaped label");
            Assert.That(sn.text, Is.EqualTo((i + 4).ToString())); Assert.That(pn.text, Is.EqualTo((i + 1).ToString()));
            Assert.That(sn.font, Is.SameAs(pn.font)); Assert.That(sn.fontSize, Is.EqualTo(pn.fontSize));
            Assert.That(sn.alignment, Is.EqualTo(pn.alignment));
            Assert.That(sn.rectTransform.anchorMin, Is.EqualTo(pn.rectTransform.anchorMin));
            Assert.That(sn.rectTransform.anchoredPosition, Is.EqualTo(pn.rectTransform.anchoredPosition));
            Assert.That(spell.transform.Find("SpellCount_Text").gameObject.activeSelf, Is.False);
        }
    }

    [Test]
    public void EmptyEquipmentIsIdenticalAndCannotKeepSelectionOrCooldownAfterReset()
    {
        f.EquipInitial(); var plants = f.Hud(true); var spells = f.Hud(false);
        F.Call(f.Spells, "StartCooldown", 4);
        F.Call(f.Session, "ResetForNewRun");
        for (int i = 0; i < 3; i++)
        {
            var p = F.HudSlot(plants, true, i); var s = F.HudSlot(spells, false, i);
            F.Call(p, "SetSelected", true); F.Call(s, "SetSelected", true);
            Assert.That(((Image)F.Field(p, "slotBackground")).color, Is.EqualTo(new Color(.32f, .34f, .36f, 1)));
            Assert.That(((Image)F.Field(s, "slotBackground")).color, Is.EqualTo(((Image)F.Field(p, "slotBackground")).color));
            Assert.That(p.transform.localScale, Is.EqualTo(Vector3.one)); Assert.That(s.transform.localScale, Is.EqualTo(Vector3.one));
            Assert.That(Icon(p).sprite, Is.Null); Assert.That(Icon(s).sprite, Is.Null);
            Assert.That(Count(p).enabled, Is.False);
            Assert.That(((Image)F.Field(s, "cooldownOverlay")).fillAmount, Is.Zero);
            Assert.That(((TMP_Text)F.Field(s, "cooldownText")).gameObject.activeSelf, Is.False);
        }
    }

    [Test]
    public void OccupiedEquipmentUsesSharedOpaquePhaseTintsAndSelectionDoesNotOverrideInactiveTone()
    {
        f.EquipInitial(); var plants = f.Hud(true); var spells = f.Hud(false);
        var p = F.HudSlot(plants, true, 0); var s = F.HudSlot(spells, false, 0);
        F.Call(p, "SetSelected", true); F.Call(s, "SetSelected", true);
        Assert.That(((Image)F.Field(p, "slotBackground")).color, Is.EqualTo(Color.white));
        Assert.That(((Image)F.Field(s, "slotBackground")).color, Is.EqualTo(new Color(.42f, .44f, .46f, 1)));
        Assert.That(Icon(p).color, Is.EqualTo(Color.white));
        Assert.That(Icon(s).color, Is.EqualTo(new Color(.5f, .52f, .54f, 1)));
        F.Call(f.Days, "StartNight");
        F.Call(s, "SetSelected", true);
        Assert.That(((Image)F.Field(p, "slotBackground")).color, Is.EqualTo(new Color(.42f, .44f, .46f, 1)));
        Assert.That(((Image)F.Field(s, "slotBackground")).color, Is.EqualTo(Color.white));
        Assert.That(s.transform.localScale, Is.EqualTo(new Vector3(1.2f, 1.2f, 1)));
        Assert.That(((CanvasGroup)F.Field(plants, "seedSlotsCanvasGroup")).alpha, Is.EqualTo(1));
        Assert.That(((CanvasGroup)F.Field(spells, "spellSlotsCanvasGroup")).alpha, Is.EqualTo(1));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void LocationHidesBothBarsAndRestoresTheCurrentExteriorPhase(bool night)
    {
        var plants = f.Hud(true); var spells = f.Hud(false);
        var location = f.HudLocationOwner(plants, spells);
        F.Call(location, "ShowInteriorHUD");
        Assert.That(plants.gameObject.activeInHierarchy, Is.False);
        Assert.That(spells.gameObject.activeInHierarchy, Is.False);
        // Phase events while hidden must not override location ownership.
        F.Call(f.Days, "StartNight");
        if (!night) F.Call(f.Days, "StartDay");
        Assert.That(plants.gameObject.activeSelf, Is.False);
        Assert.That(spells.gameObject.activeSelf, Is.False);
        F.Call(location, "ShowExteriorHUD");
        Assert.That(plants.gameObject.activeInHierarchy, Is.True);
        Assert.That(spells.gameObject.activeInHierarchy, Is.True);
        var pg = (CanvasGroup)F.Field(plants, "seedSlotsCanvasGroup");
        var sg = (CanvasGroup)F.Field(spells, "spellSlotsCanvasGroup");
        Assert.That(pg.alpha, Is.GreaterThan(0)); Assert.That(sg.alpha, Is.GreaterThan(0));
        Assert.That(pg.alpha, Is.EqualTo(1)); Assert.That(sg.alpha, Is.EqualTo(1));
        Assert.That(pg.interactable, Is.EqualTo(!night)); Assert.That(pg.blocksRaycasts, Is.EqualTo(!night));
        Assert.That(sg.interactable, Is.EqualTo(night)); Assert.That(sg.blocksRaycasts, Is.EqualTo(night));
    }

    [Test]
    public void LoadoutResetWhileInsideDoesNotReshowBarsAndExteriorRefreshesEmptyAssignments()
    {
        f.EquipInitial();
        var plants = f.Hud(true); var spells = f.Hud(false);
        var location = f.HudLocationOwner(plants, spells);
        F.Call(location, "ShowInteriorHUD");
        F.Call(f.Session, "ResetForNewRun");
        Assert.That(plants.gameObject.activeSelf, Is.False);
        Assert.That(spells.gameObject.activeSelf, Is.False);
        F.Call(location, "ShowExteriorHUD");
        Assert.That(plants.gameObject.activeInHierarchy, Is.True);
        Assert.That(spells.gameObject.activeInHierarchy, Is.True);
        for (int i = 0; i < 3; i++)
        {
            Assert.That(Icon(F.HudSlot(plants, true, i)).gameObject.activeSelf, Is.False);
            Assert.That(Icon(F.HudSlot(spells, false, i)).gameObject.activeSelf, Is.False);
        }
    }

    [Test]
    public void EmptyRunShowsOnlyThreeEmptyPositionsPerCategoryAndOpeningCatalogDoesNotProjectDraft()
    {
        var plants = f.Hud(true); var spells = f.Hud(false);
        foreach (bool category in new[] { true, false })
        {
            var hud = category ? plants : spells;
            var slots = (IList)F.Field(hud, category ? "seedSlots" : "spellSlotUIs");
            for (int i = 0; i < slots.Count; i++)
            {
                var slot = (Component)slots[i];
                Assert.That(slot.gameObject.activeSelf, Is.EqualTo(i < 3));
                if (i < 3) Assert.That(Icon(slot).gameObject.activeSelf, Is.False);
            }
        }
        Assert.That(f.Open(), Is.True);
        Assert.That(F.Name(f.Option(0)), Is.EqualTo("First authored plant"));
        Assert.That(F.Name(f.Option(1)), Is.EqualTo("Second authored plant"));
        f.Assign(0); f.Select(false, 2); f.Assign(1);
        Assert.That(Icon(F.HudSlot(plants, true, 0)).gameObject.activeSelf, Is.False);
        Assert.That(Icon(F.HudSlot(spells, false, 2)).gameObject.activeSelf, Is.False);
        f.Action("Cancel").onClick.Invoke();
        Assert.That(Icon(F.HudSlot(plants, true, 0)).gameObject.activeSelf, Is.False);
        Assert.That(f.Open(), Is.True); f.Assign(0); f.Select(false, 2); f.Assign(1);
        int stock = (int)F.Call(f.Seeds, "GetSeedCountInSlot", 0);
        f.Action("Confirm").onClick.Invoke();
        Assert.That(Icon(F.HudSlot(plants, true, 0)).sprite, Is.SameAs(F.Field(f.FirstPlant, "plantIcon")));
        Assert.That(Icon(F.HudSlot(plants, true, 0)).gameObject.activeSelf, Is.True);
        Assert.That(Icon(F.HudSlot(spells, false, 2)).sprite, Is.SameAs(F.Property(f.SecondSpell, "SpellIcon")));
        Assert.That(Icon(F.HudSlot(plants, true, 1)).gameObject.activeSelf, Is.False);
        Assert.That(Icon(F.HudSlot(spells, false, 0)).gameObject.activeSelf, Is.False);
        Assert.That(F.Call(f.Seeds, "GetSeedCountInSlot", 0), Is.EqualTo(stock));
        f.AdvanceCatalog();
        Assert.That(Icon(F.HudSlot(plants, true, 1)).gameObject.activeSelf, Is.False);
        F.Call(f.Days, "StartNight"); F.Call(f.Days, "StartDay");
        Assert.That(Icon(F.HudSlot(plants, true, 0)).gameObject.activeSelf, Is.True);
        F.Call(f.Session, "ResetForNewRun");
        Assert.That(Icon(F.HudSlot(plants, true, 0)).gameObject.activeSelf, Is.False);
        Assert.That(Icon(F.HudSlot(spells, false, 2)).gameObject.activeSelf, Is.False);
    }

    [Test]
    public void ReorderedStockAndClicksResolveIdentityAndZeroStockRetainsIconAndLiteralZero()
    {
        f.EquipInitial();
        Assert.That(F.Call(f.Seeds, "TryGrantUnlockedPlant", f.FirstPlant, 3), Is.True);
        var hud = f.Hud(true);
        F.Call(f.Seeds, "SwapSlots", 0, 5);
        F.Call(hud, "SelectSlot", 0);
        Assert.That(F.Call(f.Seeds, "GetSelectedSlotIndex"), Is.EqualTo(5));
        Assert.That(F.Call(hud, "GetSelectedSlotIndex"), Is.Zero);
        Assert.That(F.Call(F.HudSlot(hud, true, 0), "CanStartDrag"), Is.False);
        int quantity = (int)F.Call(f.Seeds, "GetSeedCountInSlot", 5);
        Assert.That(Count(F.HudSlot(hud, true, 0)).text, Is.EqualTo(quantity.ToString()));
        F.Call(hud, "SelectSlot", 2);
        Assert.That(F.Call(f.Seeds, "GetSelectedSlotIndex"), Is.EqualTo(5));
        F.Call(f.Seeds, "RemoveSeedFromSlot", 5);
        Assert.That(Count(F.HudSlot(hud, true, 0)).text, Is.EqualTo("0"));
        Assert.That(Icon(F.HudSlot(hud, true, 0)).sprite, Is.SameAs(F.Field(f.FirstPlant, "plantIcon")));
        Assert.That(Icon(F.HudSlot(hud, true, 0)).gameObject.activeSelf, Is.True);
    }

    [Test]
    public void SpellCooldownUsesResolvedOwnerAndEffectiveInventoryRatioAndRefreshDoesNotDuplicateClicks()
    {
        f.EquipInitial(); var hud = f.Hud(false);
        F.Call(f.Spells, "StartCooldown", 4);
        var slot = F.Call(f.Spells, "GetSpellSlot", 4);
        F.Set(slot, "currentCooldown", 2f);
        F.Call(hud, "RefreshAllSlots"); F.Call(hud, "RefreshAllSlots");
        var view = F.HudSlot(hud, false, 0);
        Assert.That(F.Field(view, "displayedSlot"), Is.SameAs(slot));
        Assert.That(((Image)F.Field(view, "cooldownOverlay")).fillAmount,
            Is.EqualTo(Mathf.Clamp01((float)F.Call(f.Spells, "GetCooldownProgress", 4))));
        F.Call(f.Days, "StartNight");
        int events = 0; Action<int> callback = _ => events++;
        f.Spells.GetType().GetEvent("onSpellSlotSelected").AddEventHandler(f.Spells, callback);
        try
        {
            ((UnityEngine.Events.UnityEvent)F.Field(view, "OnClick")).Invoke();
            Assert.That(F.Call(f.Spells, "GetSelectedSlotIndex"), Is.EqualTo(4));
            Assert.That(events, Is.EqualTo(1));
        }
        finally { f.Spells.GetType().GetEvent("onSpellSlotSelected").RemoveEventHandler(f.Spells, callback); }
    }

    [Test]
    public void PhaseVisibilityKeepsBothCategoriesVisibleAndOnlyTheMatchingCategoryInteractive()
    {
        var plants = f.Hud(true); var spells = f.Hud(false);
        var pg = (CanvasGroup)F.Field(plants, "seedSlotsCanvasGroup");
        var sg = (CanvasGroup)F.Field(spells, "spellSlotsCanvasGroup");
        Assert.That(pg.alpha, Is.EqualTo(1)); Assert.That(pg.interactable, Is.True);
        Assert.That(sg.alpha, Is.EqualTo(1)); Assert.That(sg.interactable, Is.False);
        Assert.That(sg.blocksRaycasts, Is.False);
        F.Call(f.Days, "StartNight");
        Assert.That(pg.alpha, Is.EqualTo(1)); Assert.That(pg.blocksRaycasts, Is.False);
        Assert.That(pg.interactable, Is.False);
        Assert.That(sg.alpha, Is.EqualTo(1)); Assert.That(sg.interactable, Is.True);
        Assert.That(F.HudSlot(plants, true, 0).gameObject.activeSelf, Is.True);
        Assert.That(F.HudSlot(spells, false, 0).gameObject.activeSelf, Is.True);
        F.Call(f.Days, "StartDay");
        Assert.That(pg.alpha, Is.EqualTo(1)); Assert.That(pg.interactable, Is.True);
        Assert.That(pg.blocksRaycasts, Is.True);
        Assert.That(sg.alpha, Is.EqualTo(1)); Assert.That(sg.interactable, Is.False);
        Assert.That(sg.blocksRaycasts, Is.False);
    }

    [Test]
    public void PhaseTintDoesNotMakeTheEquipmentGroupsTransparent()
    {
        var plants = f.Hud(true); var spells = f.Hud(false);
        for (int i = 0; i < 3; i++)
        {
            F.Call(f.Days, "StartNight"); F.Call(f.Days, "StartDay");
            Assert.That(((CanvasGroup)F.Field(plants, "seedSlotsCanvasGroup")).alpha, Is.EqualTo(1));
            Assert.That(((CanvasGroup)F.Field(spells, "spellSlotsCanvasGroup")).alpha, Is.EqualTo(1));
        }
    }
    [TestCase(1920, 1080)]
    [TestCase(1280, 720)]
    [TestCase(2560, 1440)]
    [TestCase(960, 600)]
    [TestCase(1024, 768)]
    public void SavedEquipmentGroupsAreSeparatedAndFitScaledTargetViewport(int width, int height)
    {
        var root = PrefabUtility.LoadPrefabContents("Assets/Prefabs/UI/Canvas/Canvas Game UI.prefab");
        try
        {
            var scaler = root.GetComponent<CanvasScaler>();
            var plants = (RectTransform)root.GetComponentInChildren(F.Production("SeedSlotsUIController"), true).transform;
            var spells = (RectTransform)root.GetComponentInChildren(F.Production("SpellSlotsUIController"), true).transform;
            Assert.That(plants.parent, Is.SameAs(spells.parent));
            Assert.That(plants.anchorMin, Is.EqualTo(new Vector2(.5f, 0f)));
            Assert.That(spells.anchorMin, Is.EqualTo(plants.anchorMin));
            Assert.That(plants.anchorMax, Is.EqualTo(plants.anchorMin));
            Assert.That(spells.anchorMax, Is.EqualTo(spells.anchorMin));
            float left = plants.anchoredPosition.x + plants.rect.xMin;
            float plantRight = plants.anchoredPosition.x + plants.rect.xMax;
            float spellLeft = spells.anchoredPosition.x + spells.rect.xMin;
            float right = spells.anchoredPosition.x + spells.rect.xMax;
            Assert.That(plantRight, Is.LessThan(spellLeft));
            float scale = Mathf.Pow(2f, Mathf.Lerp(Mathf.Log(width / scaler.referenceResolution.x, 2f),
                Mathf.Log(height / scaler.referenceResolution.y, 2f), scaler.matchWidthOrHeight));
            Assert.That(left * scale, Is.GreaterThanOrEqualTo(-width / 2f));
            Assert.That(right * scale, Is.LessThanOrEqualTo(width / 2f));
            Assert.That((plants.anchoredPosition.y + plants.rect.yMax) * scale, Is.LessThan(height));
            Assert.That((spells.anchoredPosition.y + spells.rect.yMax) * scale, Is.LessThan(height));
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    [Test]
    public void NumbersAndClicksUseSamePlayerActionGatesAndEquipmentMapping()
    {
        f.EquipInitial();
        F.Call(f.Seeds, "TryGrantUnlockedPlant", f.FirstPlant, 3);
        F.Call(f.Seeds, "SwapSlots", 0, 5);
        var ability = f.Unlocks.gameObject.AddComponent(F.Production("PlayerAbilitySystem"));
        F.Call(ability, "Awake");
        var plants = f.Hud(true); var spells = f.Hud(false);
        F.Call(ability, "HandleEquipmentSlotInput", 0);
        Assert.That(F.Call(f.Seeds, "GetSelectedSlotIndex"), Is.EqualTo(5));
        Assert.That(F.Property(ability, "CurrentAbility").ToString(), Is.EqualTo("Planting"));
        F.Set(ability, "isDigging", true);
        F.Call(plants, "SelectSlot", 2);
        Assert.That(F.Call(f.Seeds, "GetSelectedSlotIndex"), Is.EqualTo(5));
        F.Call(ability, "HandleEquipmentSlotInput", 2);
        Assert.That(F.Call(f.Seeds, "GetSelectedSlotIndex"), Is.EqualTo(5));
        F.Set(ability, "isDigging", false);
        F.Call(plants, "SelectSlot", 2);
        Assert.That(F.Call(f.Seeds, "GetSelectedSlotIndex"), Is.EqualTo(5));
        F.Call(ability, "HandleEquipmentSlotInput", 0);
        F.Set(f.Flow, "<IsChoosingUpgrade>k__BackingField", true);
        F.Call(plants, "SelectSlot", 2);
        Assert.That(F.Call(f.Seeds, "GetSelectedSlotIndex"), Is.EqualTo(5));
        F.Set(f.Flow, "<IsChoosingUpgrade>k__BackingField", false);
        F.Call(f.Days, "StartNight");
        F.Call(ability, "HandleEquipmentSlotInput", 3);
        Assert.That(F.Call(f.Spells, "GetSelectedSlotIndex"), Is.EqualTo(4));
        ((UnityEngine.Events.UnityEvent)F.Field(F.HudSlot(spells, false, 1), "OnClick")).Invoke();
        Assert.That(F.Call(f.Spells, "GetSelectedSlotIndex"), Is.Zero);
        F.Call(plants, "SelectSlot", 2); // Wrong-category click cannot clear either selection.
        Assert.That(F.Call(f.Seeds, "GetSelectedSlotIndex"), Is.EqualTo(5));
        Assert.That(F.Call(f.Spells, "GetSelectedSlotIndex"), Is.Zero);
        Assert.That(F.Call(f.Seeds, "GetSeedCountInSlot", 5), Is.EqualTo(3));
    }

    [Test]
    public void NativeAuthoringIsIdempotentRetainsNestedReferencesAndTransforms()
    {
        var root = PrefabUtility.LoadPrefabContents("Assets/Prefabs/UI/Canvas/Canvas Game UI.prefab");
        try
        {
            var authoring = AppDomain.CurrentDomain.GetAssemblies();
            Type type = null;
            foreach (var assembly in authoring) type = type ?? assembly.GetType("DailyLoadoutHudAuthoring");
            Assert.That(type, Is.Not.Null);
            var seed = root.GetComponentInChildren(F.Production("SeedSlotsUIController"), true);
            var spell = root.GetComponentInChildren(F.Production("SpellSlotsUIController"), true);
            var retained = new Component[6]; var positions = new Vector3[6];
            var sources = new UnityEngine.Object[6];
            var extras = new System.Collections.Generic.List<Component>();
            for (int category = 0; category < 2; category++)
            {
                bool plants = category == 0;
                var slots = (IList)F.Field(plants ? seed : spell, plants ? "seedSlots" : "spellSlotUIs");
                for (int i = 0; i < slots.Count; i++)
                {
                    var slot = (Component)slots[i];
                    if (i >= 3) { extras.Add(slot); continue; }
                    int index = category * 3 + i;
                    retained[index] = slot; positions[index] = slot.transform.localPosition;
                    sources[index] = PrefabUtility.GetCorrespondingObjectFromOriginalSource(slot);
                }
            }
            int children = root.GetComponentsInChildren<Transform>(true).Length;
            type.GetMethod("ConfigureCanvas").Invoke(null, new object[] { root });
            type.GetMethod("ConfigureCanvas").Invoke(null, new object[] { root });
            var plantRect = (RectTransform)seed.transform;
            var spellRect = (RectTransform)spell.transform;
            Assert.That(plantRect.anchoredPosition.x, Is.EqualTo(-165f));
            Assert.That(spellRect.anchoredPosition.x, Is.EqualTo(165f));
            Assert.That(plantRect.sizeDelta, Is.EqualTo(new Vector2(310f, 100f)));
            Assert.That(spellRect.sizeDelta, Is.EqualTo(new Vector2(310f, 100f)));
            Assert.That(plantRect.anchoredPosition.y, Is.EqualTo(spellRect.anchoredPosition.y));
            Assert.That(((IList)F.Field(seed, "seedSlots")).Count, Is.EqualTo(3));
            Assert.That(((IList)F.Field(spell, "spellSlotUIs")).Count, Is.EqualTo(3));
            Assert.That(root.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(children));
            foreach (var extra in extras) { Assert.That(extra, Is.Not.Null); Assert.That(extra.gameObject.activeSelf, Is.False); }
            for (int i = 0; i < 6; i++)
            {
                Assert.That(F.HudSlot(i < 3 ? seed : spell, i < 3, i % 3), Is.SameAs(retained[i]));
                Assert.That(retained[i].transform.localPosition, Is.EqualTo(positions[i]));
                Assert.That(PrefabUtility.GetCorrespondingObjectFromOriginalSource(retained[i]), Is.SameAs(sources[i]));
            }
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
