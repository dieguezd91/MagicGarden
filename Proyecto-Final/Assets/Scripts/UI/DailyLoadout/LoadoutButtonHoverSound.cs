using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MagicGarden.LoadoutUI
{
    /// <summary>Hover sound for loadout buttons (they don't use ImprovedUIButton). Added at runtime by the presenter.</summary>
    public class LoadoutButtonHoverSound : MonoBehaviour, IPointerEnterHandler
    {
        private Button button;

        private void Awake() => button = GetComponent<Button>();

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (button != null && !button.interactable) return;
            UIManager.Instance?.InterfaceSounds?.PlaySound(InterfaceSoundType.MenuButtonHover);
        }
    }
}
