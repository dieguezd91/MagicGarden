using UnityEngine;
using System.Collections;

/// <summary>Existing spell HUD, with live inventory cooldown ownership and committed identities.</summary>
public class SpellSlotsUIController : UIControllerBase
{
    [Header("Spell Slots")]
    [SerializeField] private SpellSlotUI[] spellSlotUIs = new SpellSlotUI[3];
    [SerializeField] private CanvasGroup spellSlotsCanvasGroup;
    [Header("Visibility Settings")]
    [SerializeField, Range(.1f, .9f)] private float inactiveAlpha = .45f;
    [SerializeField] private float fadeDuration = .3f;
    private SpellInventory inventory;
    private PlayerContentUnlockSystem unlocks;
    private PlayerAbilitySystem abilitySystem;
    private DailyLoadoutSession session;
    private GameFlowController flow;
    private Coroutine fadeCoroutine;
    private bool listening;

    protected override void CacheReferences() { }
    protected override void SetupEventListeners()
    {
        if (listening) return;
        listening = true;
        for (int i = 0; i < spellSlotUIs.Length; i++)
        {
            var slot = spellSlotUIs[i];
            if (slot == null) continue;
            slot.gameObject.SetActive(i < 3);
            if (i >= 3) continue;
            slot.Initialize(i);
            slot.OnSlotClicked += OnSlotClicked;
        }
        BindOwners();
    }
    protected override void ConfigureInitialState() { RefreshAllSlots(); ApplyPhase(false); }
    private void OnEnable() { if (isSetup) { SetupEventListeners(); RefreshAllSlots(); } }
    private void OnDisable() => CleanupEventListeners();
    public override void HandleUpdate()
    {
        if (!isSetup || !listening) return;
        BindOwners();
        UpdateSlotCooldown(-1);
    }
    private void BindOwners()
    {
        var owner = EquipmentGameplaySelection.ResolveOwner(unlocks);
        var nextSession = owner != null ? owner.Loadout : null;
        var nextInventory = SpellInventory.Instance;
        var nextFlow = GameFlowController.Instance;
        if (ReferenceEquals(session, nextSession) && inventory == nextInventory && flow == nextFlow && unlocks == owner) return;
        UnbindOwners();
        unlocks = owner; session = nextSession; inventory = nextInventory; flow = nextFlow;
        abilitySystem = owner != null ? owner.GetComponent<PlayerAbilitySystem>() : null;
        if (session != null) session.CommittedChanged += RefreshAllSlots;
        if (inventory != null)
        {
            inventory.onSpellSlotSelected += UpdateSelectedSlotUI;
            inventory.OnSpellInventoryChanged += RefreshAllSlots;
            inventory.onCooldownUpdated += UpdateSlotCooldown;
        }
        if (flow != null) flow.OnPhaseChanged += OnPhaseChanged;
        RefreshAllSlots(); ApplyPhase(false);
    }
    private void UnbindOwners()
    {
        if (session != null) session.CommittedChanged -= RefreshAllSlots;
        if (inventory != null)
        {
            inventory.onSpellSlotSelected -= UpdateSelectedSlotUI;
            inventory.OnSpellInventoryChanged -= RefreshAllSlots;
            inventory.onCooldownUpdated -= UpdateSlotCooldown;
        }
        if (flow != null) flow.OnPhaseChanged -= OnPhaseChanged;
    }
    private void OnPhaseChanged(GamePhase phase) { ApplyPhase(true); UpdateSelectedSlotUI(-1); }
    private void ApplyPhase(bool animate)
    {
        if (spellSlotsCanvasGroup == null) return;
        bool active = flow != null && flow.CurrentPhase == GamePhase.Night;
        spellSlotsCanvasGroup.interactable = active;
        spellSlotsCanvasGroup.blocksRaycasts = active;
        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
        fadeCoroutine = null;
        float minimumAlpha = Mathf.Clamp(inactiveAlpha, .1f, .9f);
        float targetAlpha = active ? 1f : minimumAlpha;
        // Both equipment categories retain readable positions throughout transitions.
        spellSlotsCanvasGroup.alpha = Mathf.Max(spellSlotsCanvasGroup.alpha, minimumAlpha);
        if (animate && Application.isPlaying && isActiveAndEnabled)
            fadeCoroutine = StartCoroutine(FadeCanvasGroup(targetAlpha));
        else spellSlotsCanvasGroup.alpha = targetAlpha;
    }
    private IEnumerator FadeCanvasGroup(float target)
    {
        float start = spellSlotsCanvasGroup.alpha;
        for (float time = 0; time < fadeDuration; time += Time.deltaTime)
        {
            spellSlotsCanvasGroup.alpha = Mathf.Lerp(start, target, time / fadeDuration);
            yield return null;
        }
        spellSlotsCanvasGroup.alpha = target; fadeCoroutine = null;
    }
    private void OnSlotClicked(int position)
    {
        if (flow == null || flow.CurrentPhase != GamePhase.Night) return;
        if (abilitySystem != null) abilitySystem.TrySelectEquipmentPosition(position);
        else new EquipmentGameplaySelection(session, null, inventory).TrySelectPosition(position);
    }
    private void UpdateSelectedSlotUI(int ignored)
    {
        int selected = new EquipmentGameplaySelection(session, null, inventory).GetSelectedPosition(false);
        for (int i = 0; i < spellSlotUIs.Length && i < 3; i++)
            if (spellSlotUIs[i] != null) spellSlotUIs[i].SetSelected(i == selected);
    }
    private void UpdateSlotDisplay(int position)
    {
        if (position < 0 || position >= 3 || position >= spellSlotUIs.Length || spellSlotUIs[position] == null) return;
        var assignment = session != null && session.IsReady ? session.GetCommittedSpell(position) : default;
        var resolution = assignment.HasValue ? session.ResolveSpell(assignment.Identity) : default;
        spellSlotUIs[position].UpdateEquipmentDisplay(assignment.HasValue ? assignment.Identity : null,
            resolution.Slot, inventory, resolution.InventoryIndex);
    }
    private void UpdateSlotCooldown(int ignored)
    {
        // Resolve again: indices are ephemeral, including after reset/owner replacement.
        for (int i = 0; i < spellSlotUIs.Length && i < 3; i++) UpdateSlotDisplay(i);
    }
    public void RefreshAllSlots() { UpdateSlotCooldown(-1); UpdateSelectedSlotUI(-1); }
    protected override void CleanupEventListeners()
    {
        if (!listening) return;
        listening = false; UnbindOwners();
        session = null; inventory = null; flow = null; unlocks = null; abilitySystem = null;
        foreach (var slot in spellSlotUIs) if (slot != null) slot.OnSlotClicked -= OnSlotClicked;
        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
        fadeCoroutine = null;
    }
}
