using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MagicGarden.LoadoutUI
{
    public sealed class LoadoutSlotView : LoadoutEquipmentView, IDropHandler, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        private DailyLoadoutPresenter dropOwner;
        private bool plants;
        private int position;
        public void BindDrop(DailyLoadoutPresenter presenter, bool plantCategory, int index)
        {
            dropOwner = presenter; plants = plantCategory; position = index;
        }
        public void OnDrop(PointerEventData data) { if (dropOwner != null) dropOwner.DropOption(this, plants, position, data); }
        // Right-click empties this position; left clicks stay with the select button.
        public void OnPointerClick(PointerEventData data)
        {
            if (dropOwner != null && data.button == PointerEventData.InputButton.Right) dropOwner.ClearPosition(plants, position);
        }
        void IPointerEnterHandler.OnPointerEnter(PointerEventData data)
        {
            base.OnPointerEnter(data);
            if (dropOwner != null) dropOwner.HoverDropTarget(this, data, true);
        }
        void IPointerExitHandler.OnPointerExit(PointerEventData data)
        {
            base.OnPointerExit(data);
            if (dropOwner != null) dropOwner.HoverDropTarget(this, data, false);
        }
        [SerializeField] private TMP_Text positionLabel;
        public void SetPositionLabel(string text) { positionLabel.text = text ?? string.Empty; }
    }
}
