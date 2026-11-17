using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class LevelUpCardView : MonoBehaviour
{
    [SerializeField] private Button selectButton;
    [SerializeField] private Image background;
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text targetLabel, nameLabel, statLabel, bonusLabel, rarityLabel, previewLabel;
    private Action<int> selected;
    private int index;
    public Button SelectButton => selectButton;
    public bool IsConfigured => selectButton != null && background != null && icon != null &&
        targetLabel != null && nameLabel != null && statLabel != null && bonusLabel != null && rarityLabel != null && previewLabel != null;

    public bool Bind(LevelUpCardData data, int slot, Action<int> callback)
    {
        if (!IsConfigured || data == null) return false;
        index = slot; selected = callback;
        selectButton.onClick.RemoveListener(Select);
        selectButton.onClick.AddListener(Select);
        UpgradeUiSounds.AddHover(selectButton);
        icon.sprite = data.Icon; icon.enabled = data.Icon != null;
        targetLabel.text = data.Target; nameLabel.text = data.Name;
        statLabel.text = data.Stat; bonusLabel.text = data.Bonus;
        rarityLabel.text = data.Rarity; previewLabel.text = data.Preview;
        rarityLabel.color = data.Tint; bonusLabel.color = data.Tint;
        selectButton.interactable = callback != null;
        gameObject.SetActive(true);
        return true;
    }
    private void Select() { if (selectButton.interactable) selected?.Invoke(index); }
    public void Hide() { selected = null; gameObject.SetActive(false); }
    private void OnDestroy() { if (selectButton != null) selectButton.onClick.RemoveListener(Select); }
}
