using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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

        public IReadOnlyList<LoadoutSlotView> PlantSlots => plantSlots ?? System.Array.Empty<LoadoutSlotView>();
        public IReadOnlyList<LoadoutSlotView> SpellSlots => spellSlots ?? System.Array.Empty<LoadoutSlotView>();
        public LoadoutOptionView OptionPrefab => optionPrefab;
        public RectTransform OptionContent => optionContent;
        public IReadOnlyList<LoadoutOptionView> OptionViews => optionViews ?? System.Array.Empty<LoadoutOptionView>();
        public int OptionCapacity => optionViews?.Length ?? 0;
        public ScrollRect OptionsScroll => optionsScroll;
        public Button ConfirmButton => confirmButton;
        public Button CancelButton => cancelButton;
        public Button ClearButton => clearButton;

        public void SetAllowanceMessage(string message) { if (allowanceLabel != null) allowanceLabel.text = message ?? string.Empty; }
        public void SetOptionsEmpty(bool empty) { if (emptyOptionsLabel != null) emptyOptionsLabel.gameObject.SetActive(empty); }
        // No confirmation, dismissal, equipment mutation or availability decision occurs here.
        public void SetActionAvailability(bool canConfirm, bool canClear)
        {
            if (confirmButton != null) confirmButton.interactable = canConfirm;
            if (clearButton != null) clearButton.interactable = canClear;
        }
    }
}
