using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MagicGarden.LoadoutUI
{
    public sealed class LoadoutSlotView : LoadoutEquipmentView, IDropHandler, IPointerEnterHandler, IPointerExitHandler
    {
        private DailyLoadoutPresenter dropOwner;
        private bool plants;
        private int position;
        public void BindDrop(DailyLoadoutPresenter presenter, bool plantCategory, int index)
        {
            dropOwner = presenter; plants = plantCategory; position = index;
        }
        public void OnDrop(PointerEventData data) { if (dropOwner != null) dropOwner.DropOption(this, plants, position, data); }
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
