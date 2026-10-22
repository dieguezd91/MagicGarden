using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace MagicGarden.LoadoutUI
{
    public sealed class DailyLoadoutPanelView : MonoBehaviour
    {
        [SerializeField] private LoadoutSlotView[] plantSlots;
        [SerializeField] private LoadoutSlotView[] spellSlots;
        [SerializeField] private LoadoutOptionView optionPrefab;
        [SerializeField] private RectTransform optionContent;
        [Tooltip("Editor-authored reusable instances. Runtime never instantiates option UI.")]
        [SerializeField] private LoadoutOptionView[] optionViews = new LoadoutOptionView[0];
        [SerializeField] private ScrollRect optionsScroll;
        [SerializeField] private TMP_Text emptyOptionsLabel;
        [SerializeField] private TMP_Text allowanceLabel;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button cancelButton;
        [SerializeField] private Button clearButton;
        [SerializeField] private RectTransform dragPreview;
        [SerializeField] private Image dragPreviewIcon;
        [SerializeField] private GameObject plantOptionsPanel;
        [SerializeField] private GameObject spellOptionsPanel;
        [SerializeField] private LoadoutOptionView[] spellOptionViews = new LoadoutOptionView[0];
        [SerializeField] private RectTransform spellOptionContent;
        [SerializeField] private ScrollRect spellOptionsScroll;
        [SerializeField] private TMP_Text spellEmptyOptionsLabel;
        [SerializeField] private Button plantTabButton;
        [SerializeField] private Button spellTabButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private TMP_Text selectedPositionLabel;
        [SerializeField] private TMP_Text selectedEquipmentLabel;
        [SerializeField] private TMP_Text selectedStateLabel;
        [SerializeField] private Image selectedEquipmentIcon;
        [SerializeField] private TMP_Text feedbackLabel;

        public bool HasDragPreview => dragPreview != null && dragPreviewIcon != null;
        public void ShowDragPreview(Sprite sprite, PointerEventData data)
        {
            if (!HasDragPreview) return;
            dragPreviewIcon.sprite = sprite;
            dragPreview.gameObject.SetActive(true);
            MoveDragPreview(data);
        }
        public void MoveDragPreview(PointerEventData data)
        {
            if (!HasDragPreview) return;
            var canvas = GetComponentInParent<Canvas>();
            var camera = canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : data.pressEventCamera ?? canvas.worldCamera;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)dragPreview.parent, data.position, camera, out var point))
                dragPreview.anchoredPosition = point;
        }
        public void HideDragPreview()
        {
            if (dragPreview != null) dragPreview.gameObject.SetActive(false);
            if (dragPreviewIcon != null) dragPreviewIcon.sprite = null;
        }

        public IReadOnlyList<LoadoutSlotView> PlantSlots => plantSlots ?? System.Array.Empty<LoadoutSlotView>();
        public IReadOnlyList<LoadoutSlotView> SpellSlots => spellSlots ?? System.Array.Empty<LoadoutSlotView>();
        public LoadoutOptionView OptionPrefab => optionPrefab;
        private bool ShowingSpells => spellOptionsPanel != null && spellOptionsPanel.activeSelf;
        public RectTransform OptionContent => ShowingSpells ? spellOptionContent : optionContent;
        public IReadOnlyList<LoadoutOptionView> PlantOptionViews => optionViews ?? System.Array.Empty<LoadoutOptionView>();
        public IReadOnlyList<LoadoutOptionView> SpellOptionViews => spellOptionViews ?? System.Array.Empty<LoadoutOptionView>();
        public IReadOnlyList<LoadoutOptionView> OptionViews => ShowingSpells ? SpellOptionViews : PlantOptionViews;
        public int OptionCapacity => Mathf.Min(PlantOptionViews.Count, SpellOptionViews.Count);
        public ScrollRect OptionsScroll => ShowingSpells ? spellOptionsScroll : optionsScroll;
        public Button PlantTabButton => plantTabButton;
        public Button SpellTabButton => spellTabButton;
        public Button CloseButton => closeButton;
        public Button ConfirmButton => confirmButton;
        public Button CancelButton => cancelButton;
        public Button ClearButton => clearButton;

        public void ShowCategory(bool plants)
        {
            if (plantOptionsPanel != null) plantOptionsPanel.SetActive(plants);
            if (spellOptionsPanel != null) spellOptionsPanel.SetActive(!plants);
            if (plantTabButton != null) plantTabButton.image.color = plants ? new Color(.65f, .85f, 1) : Color.white;
            if (spellTabButton != null) spellTabButton.image.color = !plants ? new Color(.65f, .85f, 1) : Color.white;
        }

        public void SetSelection(bool plants, int position, Sprite sprite, string equipment, string state)
        {
            if (selectedPositionLabel != null) selectedPositionLabel.text = (plants ? "Plant " : "Spell ") + (position + 1);
            if (selectedEquipmentLabel != null) selectedEquipmentLabel.text = equipment;
            if (selectedStateLabel != null) selectedStateLabel.text = state;
            if (selectedEquipmentIcon != null) { selectedEquipmentIcon.sprite = sprite; selectedEquipmentIcon.enabled = sprite != null; }
        }

        public void SetFeedbackMessage(string message) { if (feedbackLabel != null) feedbackLabel.text = message; }

        public void SetAllowanceMessage(string message) { if (allowanceLabel != null) allowanceLabel.text = message ?? string.Empty; }
        public void SetOptionsEmpty(bool empty)
        {
            var label = ShowingSpells ? spellEmptyOptionsLabel : emptyOptionsLabel;
            if (label != null) label.gameObject.SetActive(empty);
        }
        // No confirmation, dismissal, equipment mutation or availability decision occurs here.
        public void SetActionAvailability(bool canConfirm, bool canClear)
        {
            if (confirmButton != null) confirmButton.interactable = canConfirm;
            if (clearButton != null) clearButton.interactable = canClear;
        }
    }
}
