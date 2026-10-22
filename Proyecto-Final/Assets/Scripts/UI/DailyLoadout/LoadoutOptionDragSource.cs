using UnityEngine;
using UnityEngine.EventSystems;

namespace MagicGarden.LoadoutUI
{
    /// <summary>Only the existing icon frame owns equipment dragging; card bodies still drag the ScrollRect.</summary>
    [DisallowMultipleComponent]
    public sealed class LoadoutOptionDragSource : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private DailyLoadoutPresenter owner;
        private int optionIndex;
        public void Bind(DailyLoadoutPresenter presenter, int index) { owner = presenter; optionIndex = index; }
        public void OnBeginDrag(PointerEventData data)
        {
            if (data.button == PointerEventData.InputButton.Left && owner != null && owner.BeginOptionDrag(this, optionIndex, data))
                data.eligibleForClick = false;
        }
        public void OnDrag(PointerEventData data) { if (owner != null) owner.MoveOptionDrag(this, data); }
        public void OnEndDrag(PointerEventData data) { if (owner != null) owner.EndOptionDrag(this, data); }
        private void OnDisable() { if (owner != null) owner.CancelOptionDrag(this); }
        private void OnDestroy() { if (owner != null) owner.CancelOptionDrag(this); }
    }
}
