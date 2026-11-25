using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class SpecialUpgradeItemView : MonoBehaviour, ISelectHandler
{
    [SerializeField] private Button purchaseButton;
    [SerializeField] private Image background, icon;
    [SerializeField] private TMP_Text targetLabel, nameLabel, descriptionLabel, costsLabel, prerequisitesLabel, stacksLabel;
    private Action<int> purchased;
    private int index;
    private Action<RectTransform> selected;
    public Button PurchaseButton => purchaseButton;
    public void ConfigureNavigation(Navigation navigation, Action<RectTransform> onSelected)
    {
        purchaseButton.navigation = navigation;
        selected = onSelected;
    }
    public void OnSelect(BaseEventData eventData)
    {
        if (purchaseButton != null && purchaseButton.IsInteractable()) selected?.Invoke((RectTransform)transform);
    }
    public bool IsConfigured => purchaseButton != null && background != null && icon != null && targetLabel != null &&
        nameLabel != null && descriptionLabel != null && costsLabel != null && prerequisitesLabel != null && stacksLabel != null;
    public bool Bind(SpecialUpgradeItemData data, int row, Action<int> callback)
    {
        if (!IsConfigured || data == null) return false;
        index = row; purchased = callback;
        purchaseButton.onClick.RemoveListener(Purchase);
        purchaseButton.onClick.AddListener(Purchase);
        UpgradeUiSounds.AddHover(purchaseButton);
        icon.sprite = data.Icon; icon.enabled = data.Icon != null;
        targetLabel.text = data.Target; nameLabel.text = data.Name; descriptionLabel.text = data.Description;
        costsLabel.text = data.Costs; prerequisitesLabel.text = data.Prerequisites; stacksLabel.text = data.Stacks;
        purchaseButton.interactable = data.CanPurchase && callback != null;
        var tint = data.CanPurchase ? Color.white : Color.gray;
        targetLabel.color = tint; nameLabel.color = tint; descriptionLabel.color = tint;
        costsLabel.color = tint; prerequisitesLabel.color = tint; stacksLabel.color = tint;
        gameObject.SetActive(true);
        return true;
    }
    private void Purchase() { if (purchaseButton.interactable) purchased?.Invoke(index); }
    public void Hide() { purchased = null; selected = null; gameObject.SetActive(false); }
    private void OnDestroy() { if (purchaseButton != null) purchaseButton.onClick.RemoveListener(Purchase); }
}
