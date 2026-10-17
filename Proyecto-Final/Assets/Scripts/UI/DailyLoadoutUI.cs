using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>One slot-key subscriber for the unified equipment bar; drafts never select gameplay items.</summary>
public sealed class DailyLoadoutUI : MonoBehaviour
{
    [SerializeField] private DailyLoadoutController loadout;
    [SerializeField] private GameObject menu;
    [SerializeField] private LoadoutSlotView[] hudSlots, draftSlots;
    [SerializeField] private TMP_Text status;
    [SerializeField] private Button openButton, closeButton, confirmButton, clearButton;
    [SerializeField] private Transform catalogRoot;
    [SerializeField] private LoadoutSlotView catalogTemplate;
    private readonly List<LoadoutSlotView> catalog = new List<LoadoutSlotView>();
    private InputReader input;
    private UIFlowController uiFlow;
    private GameFlowController gameFlow;
    private int editingSlot;
    private bool ready;
    public bool IsConfigured => menu != null && status != null && openButton != null && closeButton != null &&
        confirmButton != null && clearButton != null && catalogRoot != null && catalogTemplate != null &&
        Valid(hudSlots) && Valid(draftSlots);
    private static bool Valid(LoadoutSlotView[] slots)
    {
        if (slots == null || slots.Length != 6) return false;
        for (int i = 0; i < slots.Length; i++) if (slots[i] == null || !slots[i].IsConfigured) return false;
        return true;
    }

    private void Start()
    {
        if (!IsConfigured) { Debug.LogError("Daily Loadout requires the wired DailyLoadout prefab.", this); enabled = false; return; }
        if (loadout == null) loadout = FindObjectOfType<DailyLoadoutController>();
        input = FindObjectOfType<InputReader>();
        uiFlow = UIManager.Instance != null ? UIManager.Instance.Flow : null;
        gameFlow = GameFlowController.Instance;
        if (loadout == null || uiFlow == null || input == null) { enabled = false; return; }
        menu.SetActive(false);
        catalogTemplate.gameObject.SetActive(false);
        for (int i = 0; i < 6; i++)
        {
            int index = i;
            hudSlots[i].Button.onClick.AddListener(() => Select(index));
            hudSlots[i].Hovered += () => ShowTooltip(index);
            hudSlots[i].Unhovered += HideTooltip;
            draftSlots[i].Button.onClick.AddListener(() => EditSlot(index));
        }
        openButton.onClick.AddListener(Toggle);
        closeButton.onClick.AddListener(Close);
        confirmButton.onClick.AddListener(Confirm);
        clearButton.onClick.AddListener(Clear);
        input.OnSlotSelected += Select;
        input.OnLoadoutToggled += Toggle;
        input.OnEscapePressed += Escape;
        loadout.Changed += Refresh;
        loadout.Spells.onCooldownUpdated += CooldownChanged;
        uiFlow.ModalChanged += ModalChanged;
        if (gameFlow != null) gameFlow.OnPhaseChanged += PhaseChanged;
        ready = true;
        Refresh();
    }
    private void OnDisable()
    {
        if (!ready) return;
        Close();
    }
    private void OnDestroy()
    {
        if (!ready) return;
        if (input != null) { input.OnSlotSelected -= Select; input.OnLoadoutToggled -= Toggle; input.OnEscapePressed -= Escape; }
        if (loadout != null)
        {
            loadout.Changed -= Refresh;
            if (loadout.Spells != null) loadout.Spells.onCooldownUpdated -= CooldownChanged;
        }
        if (uiFlow != null) uiFlow.ModalChanged -= ModalChanged;
        if (gameFlow != null) gameFlow.OnPhaseChanged -= PhaseChanged;
    }
    private void PhaseChanged(GamePhase phase)
    {
        HideTooltip();
        if (!loadout.CanConfigure) Close();
        Refresh();
    }
    private void ModalChanged(UIModal modal)
    {
        HideTooltip();
        RefreshHud();
    }
    private void ShowTooltip(int index)
    {
        bool spell = index < 3;
        string id = loadout.GetEquipped(spell, index % 3);
        if (!loadout.CanUse(spell, id)) return;
        if (spell) UIEvents.RequestSpellTooltip(loadout.Spells.GetSpellSlot(loadout.FindSpell(id)));
        else UIManager.Instance?.ShowTooltipForSlot(loadout.FindSeed(id));
    }
    private void HideTooltip()
    {
        UIEvents.HideSpellTooltip();
        UIManager.Instance?.HideTooltip();
    }
    private void CooldownChanged(int index) => RefreshHud();
    private void Escape()
    {
        if (!menu.activeSelf) return;
        InputConsumptionManager.ConsumeEscape();
        Close();
    }
    public void Toggle()
    {
        if (menu.activeSelf) { Close(); return; }
        if (!loadout.CanConfigure || !uiFlow.Open(UIModal.Loadout)) return;
        editingSlot = 0;
        menu.SetActive(true);
        Refresh();
    }
    public void Close()
    {
        if (menu != null) menu.SetActive(false);
        if (uiFlow != null) uiFlow.Close(UIModal.Loadout);
        if (loadout != null) RefreshHud();
    }
    private void Confirm()
    {
        if (!loadout.TryConfirm()) status.text = "Cannot confirm: configuration is locked or an item is no longer unlocked.";
        // Keep the panel open so the daily lock is explicit; Close resumes play.
    }
    private void Clear() => loadout.TryEdit(editingSlot < 3, editingSlot % 3, null);
    private void EditSlot(int index) { editingSlot = index; Refresh(); }
    private void Select(int index)
    {
        if (index < 0 || index >= 6 || (menu != null && menu.activeSelf)) return;
        bool spell = index < 3;
        string id = loadout.GetEquipped(spell, index % 3);
        if (!loadout.CanUse(spell, id)) return;
        if (spell) loadout.Spells.SelectSlot(loadout.FindSpell(id));
        else loadout.Seeds.SelectSlot(loadout.FindSeed(id));
    }
    private void Refresh()
    {
        RefreshHud();
        if (!menu.activeSelf) return;
        bool editable = !loadout.IsConfirmed && loadout.CanConfigure;
        status.text = loadout.IsConfirmed ? "CONFIRMED — Locked until the next day." :
            "DRAFT — Choose a slot, then an unlocked item. Empty slots are allowed. Confirm once per day.";
        confirmButton.interactable = clearButton.interactable = editable;
        for (int i = 0; i < 6; i++) Render(draftSlots[i], i, true, editable);
        RefreshCatalog(editable);
    }
    private void RefreshHud()
    {
        if (loadout == null || !IsConfigured) return;
        for (int i = 0; i < 6; i++) Render(hudSlots[i], i, false, false);
        openButton.interactable = loadout.CanConfigure;
    }
    private void Render(LoadoutSlotView view, int i, bool draft, bool editable)
    {
        bool spell = i < 3;
        string id = draft ? loadout.GetDraft(spell, i % 3) : loadout.GetEquipped(spell, i % 3);
        GetDisplay(spell, id, out var title, out var sprite, out var info, out float progress);
        bool selected = draft ? editingSlot == i : id != null && id == (spell ? loadout.SelectedSpellId : loadout.SelectedPlantId);
        view.Render((draft ? "" : (i + 1) + " ") + (spell ? "Spell: " : "Plant: ") + title, sprite, info,
            draft ? 0f : progress, draft ? editable : loadout.CanUse(spell, id), selected);
    }
    private void GetDisplay(bool spell, string id, out string title, out Sprite sprite, out string info, out float progress)
    {
        title = "Empty"; sprite = null; info = ""; progress = 0;
        if (id == null) return;
        if (spell)
        {
            int index = loadout.FindSpell(id);
            var slot = loadout.Spells.GetSpellSlot(index);
            if (slot == null) { title = id; return; }
            title = slot.spellName; sprite = slot.spellIcon;
            info = slot.currentCooldown > 0 ? slot.currentCooldown.ToString("0.0") + "s" : slot.manaCost + " mana";
            progress = loadout.Spells.GetCooldownProgress(index);
        }
        else
        {
            var data = loadout.GetPlantData(id);
            title = data != null ? data.plantName : id;
            sprite = data != null ? data.plantIcon : null;
            // Quantity lookup can fail after depletion; identity and icon stay equipped.
            info = "Seeds: " + loadout.Seeds.GetSeedCountInSlot(loadout.FindSeed(id));
        }
    }
    private void RefreshCatalog(bool editable)
    {
        foreach (var view in catalog) Destroy(view.gameObject);
        catalog.Clear();
        bool spell = editingSlot < 3;
        if (loadout.Unlocks == null) return;
        if (spell)
        {
            foreach (var data in loadout.Unlocks.UnlockedSpells)
                if (data != null) AddCatalogItem(true, data.UpgradeTargetId, editable);
        }
        else
        {
            foreach (var seed in loadout.Unlocks.UnlockedPlants)
                AddCatalogItem(false, DailyLoadoutController.PlantId(seed), editable);
        }
    }
    private void AddCatalogItem(bool spell, string id, bool editable)
    {
        var view = Instantiate(catalogTemplate, catalogRoot);
        catalog.Add(view);
        view.gameObject.SetActive(true);
        GetDisplay(spell, id, out var title, out var sprite, out var info, out _);
        view.Render(title, sprite, info, 0, editable, false);
        view.Button.onClick.AddListener(() =>
        {
            if (!loadout.TryEdit(spell, editingSlot % 3, id))
                status.text = "Cannot equip: this item is already in another slot, is locked, or is no longer unlocked.";
        });
    }
}
