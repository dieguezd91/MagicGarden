using TMPro;
using UnityEngine;

namespace MagicGarden.LoadoutUI
{
    public sealed class LoadoutSlotView : LoadoutEquipmentView
    {
        [SerializeField] private TMP_Text positionLabel;
        public void SetPositionLabel(string text) { positionLabel.text = text ?? string.Empty; }
    }
}
