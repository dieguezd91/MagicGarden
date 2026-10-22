using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MagicGarden.LoadoutUI
{
    // Presentation only: callers supply content and state, never inventory or rules.
    public abstract class LoadoutEquipmentView : MonoBehaviour, IPointerEnterHandler,
        IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler
    {
        [SerializeField] private Button selectButton;
        [SerializeField] private Image background;
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text stateLabel;
        [SerializeField] private Sprite normalSprite;
        [SerializeField] private Sprite hoverSprite;
        [SerializeField] private Sprite pressedSprite;
        [SerializeField] private Color selectedColor = new Color(.65f, .85f, 1f);
        [SerializeField] private Color invalidColor = new Color(1f, .4f, .4f);
        [SerializeField] private Color disabledColor = new Color(.45f, .45f, .45f);
        private bool hasContent, assigned, selected, highlighted, invalid, readOnly, locked;
        private bool hovered, pressed, focused;
        private bool dropCompatible, dropHovered, dropAllowed;

        public Button SelectButton => selectButton;
        public bool HasContent => hasContent;
        public Sprite IconSprite => icon != null ? icon.sprite : null;
        public string DisplayName => nameLabel != null ? nameLabel.text : string.Empty;

        public void SetDropFeedback(bool compatible, bool hoveredTarget, bool allowed)
        {
            if (dropCompatible == compatible && dropHovered == hoveredTarget && dropAllowed == allowed) return;
            dropCompatible = compatible; dropHovered = hoveredTarget; dropAllowed = allowed; Refresh();
        }

        public void SetContent(Sprite sprite, string displayName)
        {
            hasContent = !string.IsNullOrWhiteSpace(displayName);
            icon.sprite = sprite;
            icon.enabled = hasContent && sprite != null;
            nameLabel.text = hasContent ? displayName : "Empty";
            Refresh();
        }

        public void SetAssigned(bool value) { assigned = value; Refresh(); }
        public void SetSelected(bool value) { selected = value; Refresh(); }
        public void SetHighlighted(bool value) { highlighted = value; Refresh(); }
        public void SetInvalidDropTarget(bool value) { invalid = value; Refresh(); }
        public void SetReadOnly(bool value)
        {
            if (readOnly != value) ResetPointer();
            readOnly = value; Refresh();
        }
        protected void SetLockedState(bool value)
        {
            if (locked != value) ResetPointer();
            locked = value; Refresh();
        }
        protected virtual bool AllowEmptySelection => true;

        public void OnPointerEnter(PointerEventData data) { hovered = true; Refresh(); }
        public void OnPointerExit(PointerEventData data) { ResetPointer(); Refresh(); }
        public void OnPointerDown(PointerEventData data)
        {
            if (data.button == PointerEventData.InputButton.Left) { pressed = true; Refresh(); }
        }
        public void OnPointerUp(PointerEventData data) { pressed = false; Refresh(); }
        public void OnSelect(BaseEventData data) { focused = true; Refresh(); }
        public void OnDeselect(BaseEventData data) { focused = false; Refresh(); }
        private void OnDisable() { ResetPointer(); focused = false; }
        private void OnEnable() { Refresh(); }
        private void ResetPointer() { hovered = false; pressed = false; }

        private void Refresh()
        {
            // Authoring may add this component before its serialized references are set.
            if (selectButton == null || background == null || icon == null || nameLabel == null || stateLabel == null) return;
            bool disabled = readOnly || locked || (!AllowEmptySelection && !hasContent);
            selectButton.interactable = !disabled;
            background.sprite = !disabled && pressed ? pressedSprite :
                !disabled && (hovered || highlighted || focused || dropCompatible || dropHovered) ? hoverSprite : normalSprite;
            bool invalidTarget = invalid || (dropHovered && !dropAllowed);
            Color tint = disabled ? disabledColor : invalidTarget ? invalidColor :
                dropHovered ? new Color(.5f, 1f, .6f) : dropCompatible ? new Color(.8f, 1f, .85f) : selected ? selectedColor : Color.white;
            background.color = tint;
            icon.color = disabled ? disabledColor : Color.white;
            // Pointer, drop validity and editability remain visual/input feedback, never extra written states.
            stateLabel.text = assigned || selected ? "Selected" : hasContent ? "Available" : "Empty";
        }
    }
}
