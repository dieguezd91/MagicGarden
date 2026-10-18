using UnityEngine;
using UnityEngine.EventSystems;

public class PlantSlotTooltipHandler : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public int slotIndex;
    // Equipment HUD supplies a freshly resolved storage index, never its visual position.
    public bool equipmentMode;

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!equipmentMode || slotIndex >= 0) UIManager.Instance?.ShowTooltipForSlot(slotIndex);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        UIManager.Instance?.HideTooltip();
    }
}
