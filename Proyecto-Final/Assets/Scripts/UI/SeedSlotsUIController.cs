using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class SeedSlotsUIController : UIControllerBase
{
    [Header("Seed Slots")]
    [SerializeField] private SeedSlot[] seedSlots = new SeedSlot[9];
    [SerializeField] private CanvasGroup seedSlotsCanvasGroup;

    private int hoveredSlotIndex = -1;
    private Coroutine fadeCoroutine;

    private GameObject dragIcon;
    private int dragSourceIndex = -1;

    private SeedInventory seedInventory;
    private PlayerAbilitySystem abilitySystem;
    private GameFlowController gameFlowController;

    protected override void SetupEventListeners()
    {
        if (seedInventory != null)
        {
            seedInventory.onSlotSelected += UpdateSelectedSlotUI;
            seedInventory.onInventoryChanged += UpdateSeedCounts;
        }

        if (abilitySystem != null)
        {
            abilitySystem.OnAbilityChanged += OnAbilityChanged;
        }

        if (gameFlowController != null)
        {
            gameFlowController.OnPhaseChanged += OnPhaseChanged;
        }

        SetupSlotEventListeners();
    }

    protected override void ConfigureInitialState()
    {
        InitializeSlots();

        if (gameFlowController != null)
        {
            UpdateVisibilityBasedOnPhase(gameFlowController.CurrentPhase);
        }
        else
        {
            if (abilitySystem?.CurrentAbility != PlayerAbility.Planting && seedSlotsCanvasGroup != null)
            {
                seedSlotsCanvasGroup.alpha = 0.1f;
            }
        }
    }

    public override void HandleUpdate()
    {
        // Numeric gameplay selection is routed once through InputReader.
    }

    private void OnPhaseChanged(GamePhase newPhase)
    {
        UpdateVisibilityBasedOnPhase(newPhase);
    }

    private void UpdateVisibilityBasedOnPhase(GamePhase phase)
    {
        if (seedSlotsCanvasGroup == null) return;

        if (phase == GamePhase.Night)
        {
            if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);

            seedSlotsCanvasGroup.alpha = 0f;
            seedSlotsCanvasGroup.interactable = false;
            seedSlotsCanvasGroup.blocksRaycasts = false;
        }
        else if (IsDayPhase(phase))
        {
            bool shouldShow = abilitySystem?.CurrentAbility == PlayerAbility.Planting;

            if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);

            float targetAlpha = shouldShow ? 1f : 0.1f;

            if (gameObject.activeInHierarchy)
            {
                fadeCoroutine = StartCoroutine(FadeToAlpha(targetAlpha, 0.35f, shouldShow));
            }
            else
            {
                seedSlotsCanvasGroup.alpha = targetAlpha;
                seedSlotsCanvasGroup.interactable = shouldShow;
                seedSlotsCanvasGroup.blocksRaycasts = shouldShow;
            }
        }
    }

    private bool IsDayPhase(GamePhase phase)
    {
        return phase == GamePhase.Day;
    }

    private void SetupSlotEventListeners()
    {
        for (int i = 0; i < seedSlots.Length; i++)
        {
            if (seedSlots[i] == null) continue;

            var slot = seedSlots[i];
            slot.OnSlotClicked += OnSlotClicked;
            slot.OnDragStarted += BeginDragIcon;
            slot.OnSlotDragged += OnDragIcon;
            slot.OnDragEnded += EndDragIcon;
            slot.OnSlotHovered += OnSlotHovered;
            slot.OnSlotUnhovered += OnSlotUnhovered;
        }
    }

    public void InitializeSlots()
    {
        for (int i = 0; i < seedSlots.Length; i++)
        {
            if (seedSlots[i] == null) continue;

            seedSlots[i].Initialize(i);
            UpdateSlotDisplay(i);
        }

        if (seedInventory != null)
            UpdateSelectedSlotUI(seedInventory.GetSelectedSlotIndex());
    }

    private void UpdateSlotDisplay(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= seedSlots.Length || seedSlots[slotIndex] == null) return;

        PlantSlot plantSlot = seedInventory?.GetPlantSlot(slotIndex);
        seedSlots[slotIndex].UpdateSlotDisplay(plantSlot);
    }

    public void UpdateSeedCounts()
    {
        for (int i = 0; i < seedSlots.Length; i++)
        {
            UpdateSlotDisplay(i);
        }
    }

    private void UpdateSelectedSlotUI(int selectedIndex)
    {
        for (int i = 0; i < seedSlots.Length; i++)
        {
            if (seedSlots[i] == null) continue;

            bool isSelected = (i == selectedIndex);
            seedSlots[i].SetSelected(isSelected);
        }
    }

    private void OnAbilityChanged(PlayerAbility newAbility)
    {
        if (seedSlotsCanvasGroup == null || !isActiveAndEnabled) return;

        if (gameFlowController != null && gameFlowController.CurrentPhase == GamePhase.Night)
        {
            return;
        }

        bool shouldShow = newAbility == PlayerAbility.Planting;
        if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);

        float targetAlpha = shouldShow ? 1f : 0.1f;
        fadeCoroutine = StartCoroutine(FadeToAlpha(targetAlpha, 0.35f, shouldShow));
    }

    private IEnumerator FadeToAlpha(float targetAlpha, float duration, bool makeInteractable)
    {
        if (seedSlotsCanvasGroup == null) yield break;

        float startAlpha = seedSlotsCanvasGroup.alpha;
        float time = 0f;

        while (time < duration)
        {
            time += Time.deltaTime;
            float t = time / duration;
            seedSlotsCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
            yield return null;
        }

        seedSlotsCanvasGroup.alpha = targetAlpha;

        seedSlotsCanvasGroup.interactable = makeInteractable;
        seedSlotsCanvasGroup.blocksRaycasts = makeInteractable;
    }

    private void OnSlotClicked(int slotIndex)
    {
        SelectSlotInternal(slotIndex);

        abilitySystem?.SetAbility(PlayerAbility.Planting);

        UIManager.Instance.InterfaceSounds?.PlaySound(InterfaceSoundType.MenuButtonHover);
    }

    private void OnSlotHovered(int slotIndex)
    {
        // Handle slot hover if needed
    }

    private void OnSlotUnhovered(int slotIndex)
    {
        // Handle slot unhover if needed
    }

    private bool HasSlot(int slotIndex)
    {
        return slotIndex >= 0 && slotIndex < seedSlots.Length && seedSlots[slotIndex] != null;
    }

    private void BeginDragIcon(int slotIndex)
    {
        if (dragSourceIndex >= 0 || !HasSlot(slotIndex) || !seedSlots[slotIndex].CanStartDrag()) return;

        InitializeDrag(slotIndex);
        ActivatePlantingAbility();
    }

    private void InitializeDrag(int slotIndex)
    {
        dragSourceIndex = slotIndex;
        CreateDragIcon(slotIndex);
        seedSlots[slotIndex].SetDragSource(true);
    }

    private void ActivatePlantingAbility()
    {
        abilitySystem?.SetAbility(PlayerAbility.Planting);
    }

    private void CreateDragIcon(int slotIndex)
    {
        dragIcon = new GameObject("DragIcon");
        SetupDragIconTransform(slotIndex);
        SetupDragIconImage(slotIndex);
    }

    private void SetupDragIconTransform(int slotIndex)
    {
        var rt = dragIcon.AddComponent<RectTransform>();
        rt.SetParent(transform.root, false);
        rt.sizeDelta = seedSlots[slotIndex].GetComponent<RectTransform>().sizeDelta;
    }

    private void SetupDragIconImage(int slotIndex)
    {
        var img = dragIcon.AddComponent<Image>();
        img.raycastTarget = false;
        img.sprite = seedSlots[slotIndex].GetSlotIcon();
        img.color = new Color(1f, 1f, 1f, 0.6f);
    }

    private void OnDragIcon(int slotIndex, PointerEventData data)
    {
        if (dragIcon == null || slotIndex != dragSourceIndex || data == null || EventSystem.current == null) return;

        var rt = dragIcon.GetComponent<RectTransform>();
        rt.position = data.position;

        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(data, results);
        int newHoverIndex = GetHoveredSlotIndex(results);

        if (newHoverIndex != hoveredSlotIndex)
        {
            ClearSlotHighlight();
            if (newHoverIndex >= 0 && newHoverIndex != dragSourceIndex)
            {
                hoveredSlotIndex = newHoverIndex;
                HighlightSlot(newHoverIndex, true);
            }
        }
    }

    private int GetHoveredSlotIndex(List<RaycastResult> results)
    {
        foreach (var result in results)
        {
            var slot = result.gameObject.GetComponent<SeedSlot>();
            if (slot != null) return slot.SlotIndex;

            slot = result.gameObject.GetComponentInParent<SeedSlot>();
            if (slot != null) return slot.SlotIndex;
        }
        return -1;
    }

    private void EndDragIcon(int slotIndex, PointerEventData data)
    {
        // Unity may send end-drag even when the begin was rejected.
        if (!HasSlot(dragSourceIndex) || slotIndex != dragSourceIndex) return;
        try
        {
            var results = new List<RaycastResult>();
            if (data != null && EventSystem.current != null)
                EventSystem.current.RaycastAll(data, results);
            ProcessDragResult(GetHoveredSlotIndex(results));
        }
        finally
        {
            CleanupDrag();
        }
    }

    private void DestroyDragIcon()
    {
        if (dragIcon != null)
        {
            Destroy(dragIcon);
            dragIcon = null;
        }
    }

    private void ProcessDragResult(int hitIndex)
    {
        if (hitIndex != dragSourceIndex && hitIndex >= 0)
            ExecuteDragSwap(hitIndex);
    }

    private void ExecuteDragSwap(int targetIndex)
    {
        if (seedInventory != null && seedInventory.SwapSlots(dragSourceIndex, targetIndex))
        {
            RefreshSlotsAfterSwap(targetIndex);
        }
    }

    private void RefreshSlotsAfterSwap(int newSelectedSlot)
    {
        ForceLayoutRebuild();
        SelectNewSlotAfterSwap(newSelectedSlot);
    }

    private void ForceLayoutRebuild()
    {
        var seedSlotsParent = GameObject.Find("SeedSlots")?.GetComponent<RectTransform>();
        if (seedSlotsParent != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(seedSlotsParent);
    }

    private void SelectNewSlotAfterSwap(int slotIndex)
    {
        seedInventory?.SelectSlot(slotIndex);
    }

    private void CleanupDrag()
    {
        DestroyDragIcon();
        if (HasSlot(dragSourceIndex))
            seedSlots[dragSourceIndex].SetDragSource(false);
        // Swap notifications run synchronously while the source is hidden.
        // Render from the current storage entries after clearing that state,
        // including rejected/cancelled swaps which emitted no notification.
        UpdateSeedCounts();
        ClearSlotHighlight();
        dragSourceIndex = -1;
    }

    private void OnDisable()
    {
        if (dragSourceIndex >= 0) CleanupDrag();
    }

    private void HighlightSlot(int slotIndex, bool highlight)
    {
        if (slotIndex < 0 || slotIndex >= seedSlots.Length || seedSlots[slotIndex] == null) return;
        seedSlots[slotIndex].SetHighlighted(highlight);
    }

    private void ClearSlotHighlight()
    {
        if (HasSlot(hoveredSlotIndex))
        {
            seedSlots[hoveredSlotIndex].SetHighlighted(false);
            hoveredSlotIndex = -1;
        }
    }

    private void SelectSlotInternal(int slotIndex)
    {
        seedInventory?.SelectSlot(slotIndex);
        UIManager.Instance.InterfaceSounds?.PlaySound(InterfaceSoundType.OnSeedSelect);
    }

    public void SelectSlot(int slotIndex)
    {
        if (seedInventory != null)
        {
            seedInventory.SelectSlot(slotIndex);
        }
    }

    public int GetSelectedSlotIndex()
    {
        return seedInventory?.GetSelectedSlotIndex() ?? 0;
    }

    public void RefreshSlotDisplay(int slotIndex)
    {
        UpdateSlotDisplay(slotIndex);
    }

    public void RefreshAllSlots()
    {
        InitializeSlots();
    }

    public bool IsSlotEmpty(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= seedSlots.Length) return true;

        PlantSlot slot = seedInventory?.GetPlantSlot(slotIndex);
        return slot == null || slot.seedCount <= 0;
    }

    public PlantSlot GetSlotData(int slotIndex)
    {
        return seedInventory?.GetPlantSlot(slotIndex);
    }

    protected override void CleanupEventListeners()
    {
        if (seedInventory != null)
        {
            seedInventory.onSlotSelected -= UpdateSelectedSlotUI;
            seedInventory.onInventoryChanged -= UpdateSeedCounts;
        }

        if (abilitySystem != null)
        {
            abilitySystem.OnAbilityChanged -= OnAbilityChanged;
        }

        if (gameFlowController != null)
        {
            gameFlowController.OnPhaseChanged -= OnPhaseChanged;
        }

        for (int i = 0; i < seedSlots.Length; i++)
        {
            if (seedSlots[i] == null) continue;

            var slot = seedSlots[i];
            slot.OnSlotClicked -= OnSlotClicked;
            slot.OnDragStarted -= BeginDragIcon;
            slot.OnSlotDragged -= OnDragIcon;
            slot.OnDragEnded -= EndDragIcon;
            slot.OnSlotHovered -= OnSlotHovered;
            slot.OnSlotUnhovered -= OnSlotUnhovered;
        }

        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
            fadeCoroutine = null;
        }

        if (dragSourceIndex >= 0) CleanupDrag();
        else DestroyDragIcon();
    }

    protected override void CacheReferences()
    {
        seedInventory = SeedInventory.Instance;

        if (seedInventory == null)
            seedInventory = FindObjectOfType<SeedInventory>();

        abilitySystem = FindObjectOfType<PlayerAbilitySystem>();

        gameFlowController = GameFlowController.Instance;

        if (gameFlowController == null)
            gameFlowController = FindObjectOfType<GameFlowController>();
    }
}
