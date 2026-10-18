using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System;

public class SeedSlot : ImprovedUIButton, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerExitHandler
{
    [Header("UI References")]
    [SerializeField] private Image slotIcon;
    [SerializeField] private Image slotBackground;
    [SerializeField] private TextMeshProUGUI slotNumber;
    [SerializeField] private TextMeshProUGUI seedCountText;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Visual Settings")]
    [SerializeField] private Color normalColor = new Color(0.7f, 0.7f, 0.7f, 0.7f);
    [SerializeField] private Color selectedColor = Color.white;
    [SerializeField] private Color highlightColor = new Color(1f, 1f, 0.5f, 1f);
    [SerializeField] private float selectedScale = 1.2f;
    [SerializeField] private float normalScale = 1.0f;

    public int SlotIndex { get; private set; }
    
    public event Action<int> OnSlotClicked;
    public event Action<int> OnDragStarted;
    public event Action<int, PointerEventData> OnSlotDragged;
    public event Action<int, PointerEventData> OnDragEnded;
    public event Action<int> OnSlotHovered;
    public event Action<int> OnSlotUnhovered;

    private PlantSlotTooltipHandler tooltipHandler;
    private bool isDragSource = false;
    private PlantSlot displayedSlot;
    private bool equipmentMode;
    private bool selected;
    private int inventoryIndex = -1;

    public void InitializeEquipment(int position)
    {
        equipmentMode = true;
        SlotIndex = position;
        tooltipHandler = GetComponent<PlantSlotTooltipHandler>();
        if (tooltipHandler != null) { tooltipHandler.equipmentMode = true; tooltipHandler.slotIndex = -1; }
        SetSlotNumber();
        SetupButtonEvents();
    }

    public void UpdateEquipmentDisplay(PlantDataSO data, int count, int storageIndex)
    {
        inventoryIndex = storageIndex;
        if (tooltipHandler != null) tooltipHandler.slotIndex = storageIndex;
        if (data == null) { HideSlotContent(); return; }
        if (slotIcon != null)
        {
            slotIcon.sprite = data.plantIcon;
            slotIcon.preserveAspect = true;
            slotIcon.gameObject.SetActive(true);
        }
        if (seedCountText != null) { seedCountText.text = count.ToString(); seedCountText.enabled = true; }
    }

    public void Initialize(int slotIndex)
    {
        SlotIndex = slotIndex;
        
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
            
        SetupTooltipHandler();
        SetSlotNumber();
        SetupButtonEvents();
    }

    private void SetupTooltipHandler()
    {
        tooltipHandler = GetComponent<PlantSlotTooltipHandler>();
        if (tooltipHandler == null)
            tooltipHandler = gameObject.AddComponent<PlantSlotTooltipHandler>();
        tooltipHandler.slotIndex = SlotIndex;
    }

    private void SetSlotNumber()
    {
        if (slotNumber != null)
            slotNumber.text = (SlotIndex + 1).ToString();
    }

    private void SetupButtonEvents()
    {
        // Subscribe to ImprovedUIButton events
        OnClick.RemoveAllListeners();
        OnClick.AddListener(() => OnSlotClicked?.Invoke(SlotIndex));
        
        OnHover.RemoveAllListeners();
        OnHover.AddListener(() => {
            OnSlotHovered?.Invoke(SlotIndex);
            int index = equipmentMode ? inventoryIndex : SlotIndex;
            if (index >= 0) UIEvents.TriggerTooltipRequested(index);
        });
    }

    // Implement IPointerExitHandler to add our custom logic alongside ImprovedUIButton's exit handling
    public new void OnPointerExit(PointerEventData eventData)
    {
        // Call the base ImprovedUIButton implementation first
        base.OnPointerExit(eventData);
        
        // Add our custom logic
        OnSlotUnhovered?.Invoke(SlotIndex);
        UIEvents.TriggerTooltipHide();
    }

    public void UpdateSlotDisplay(PlantSlot plantSlot)
    {
        displayedSlot = plantSlot;
        if (HasValidSeed(plantSlot))
        {
            ShowSlotContent(plantSlot);
        }
        else
        {
            HideSlotContent();
        }
    }

    private bool HasValidSeed(PlantSlot plantSlot)
    {
        return plantSlot != null && plantSlot.seedCount > 0 && plantSlot.plantPrefab != null;
    }

    private void ShowSlotContent(PlantSlot plantSlot)
    {
        if (slotIcon != null)
        {
            slotIcon.sprite = plantSlot.plantIcon;
            slotIcon.preserveAspect = true;
            slotIcon.gameObject.SetActive(!isDragSource);
        }

        if (seedCountText != null)
        {
            seedCountText.text = plantSlot.seedCount.ToString();
            seedCountText.enabled = !isDragSource;
        }
    }

    private void HideSlotContent()
    {
        if (slotIcon != null)
        {
            slotIcon.sprite = null;
            slotIcon.gameObject.SetActive(false);
        }

        if (seedCountText != null)
        {
            seedCountText.text = "";
            seedCountText.enabled = false;
        }
    }

    public void SetSelected(bool selected)
    {
        this.selected = selected;
        if (slotBackground == null) return;

        slotBackground.color = selected ? selectedColor : normalColor;
        float scale = selected ? selectedScale : normalScale;
        transform.localScale = new Vector3(scale, scale, 1f);
    }

    public void SetHighlighted(bool highlighted)
    {
        if (slotBackground == null) return;

        if (highlighted)
        {
            slotBackground.color = highlightColor;
        }
        else
        {
            // Restore to selected or normal state
            bool isSelected = equipmentMode ? selected : SeedInventory.Instance?.GetSelectedSlotIndex() == SlotIndex;
            slotBackground.color = isSelected ? selectedColor : normalColor;
        }
    }

    public void SetDragSource(bool isDragSource)
    {
        this.isDragSource = isDragSource;
        UpdateSlotDisplay(displayedSlot);
    }

    public bool CanStartDrag()
    {
        if (equipmentMode) return false;
        PlantSlot slot = SeedInventory.Instance?.GetPlantSlot(SlotIndex);
        return slot != null && slot.data != null && slot.seedCount > 0;
    }

    public Sprite GetSlotIcon()
    {
        return slotIcon?.sprite;
    }

    // Drag and Drop handlers (these don't conflict with ImprovedUIButton)
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!CanStartDrag()) return;
        OnDragStarted?.Invoke(SlotIndex);
    }

    public void OnDrag(PointerEventData eventData)
    {
        OnSlotDragged?.Invoke(SlotIndex, eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        OnDragEnded?.Invoke(SlotIndex, eventData);
    }
}
