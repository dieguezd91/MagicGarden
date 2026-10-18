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
        [SerializeField] private ScrollRect optionsScroll;
        [SerializeField] private TMP_Text emptyOptionsLabel;
        [SerializeField] private TMP_Text allowanceLabel;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button cancelButton;
        [SerializeField] private Button clearButton;

        public IReadOnlyList<LoadoutSlotView> PlantSlots => plantSlots;
        public IReadOnlyList<LoadoutSlotView> SpellSlots => spellSlots;
        public LoadoutOptionView OptionPrefab => optionPrefab;
        public RectTransform OptionContent => optionContent;
        public ScrollRect OptionsScroll => optionsScroll;
        public Button ConfirmButton => confirmButton;
        public Button CancelButton => cancelButton;
        public Button ClearButton => clearButton;

        public void SetAllowanceMessage(string message) { allowanceLabel.text = message ?? string.Empty; }
        public void SetOptionsEmpty(bool empty) { emptyOptionsLabel.gameObject.SetActive(empty); }
        // No confirmation, dismissal, equipment mutation or availability decision occurs here.
        public void SetActionAvailability(bool canConfirm, bool canClear)
        {
            confirmButton.interactable = canConfirm;
            clearButton.interactable = canClear;
        }
    }
}
