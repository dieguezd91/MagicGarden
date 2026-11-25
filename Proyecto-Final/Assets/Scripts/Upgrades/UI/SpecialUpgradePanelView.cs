using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class SpecialUpgradePanelView : MonoBehaviour
{
    [SerializeField] private GameObject root;
    [SerializeField] private Image background;
    [SerializeField] private TMP_Text headingLabel, emptyLabel;
    [SerializeField] private Button closeButton;
    [SerializeField] private ScrollRect scroll;
    [SerializeField] private RectTransform content;
    [SerializeField] private SpecialUpgradeItemView itemPrefab;
    private readonly List<SpecialUpgradeItemView> rows = new List<SpecialUpgradeItemView>();
    public event Action<int> Purchased;
    public event Action Closed;
    public event Action Hidden;
    public bool IsConfigured => root == gameObject && background != null && headingLabel != null && emptyLabel != null &&
        closeButton != null && scroll != null && content != null && scroll.content == content && scroll.viewport != null &&
        itemPrefab != null && itemPrefab.IsConfigured;
    // Keep Show's original signature: it denotes a new shop opening, never a refresh.
    public bool Show(IList<SpecialUpgradeItemData> items) => Display(items, true);
    public bool Refresh(IList<SpecialUpgradeItemData> items) => Display(items, false);
    private bool Display(IList<SpecialUpgradeItemData> items, bool newOpen)
    {
        if (!IsConfigured || items == null) return false;
        newOpen |= !root.activeInHierarchy;
        float offset = newOpen ? 0 : content.anchoredPosition.y;
        scroll.StopMovement();
        closeButton.onClick.RemoveListener(Close);
        closeButton.onClick.AddListener(Close);
        UpgradeUiSounds.AddHover(closeButton);
        // The only runtime visual instantiation: an already-authored shop item prefab.
        while (rows.Count < items.Count) rows.Add(Instantiate(itemPrefab, content, false));
        for (int i = 0; i < rows.Count; i++)
        {
            if (i < items.Count) rows[i].Bind(items[i], i, Purchase);
            else rows[i].Hide();
        }
        emptyLabel.gameObject.SetActive(items.Count == 0);
        root.SetActive(true);
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        SetOffset(offset);
        scroll.Rebuild(CanvasUpdate.PostLayout);
        ConfigureNavigation();
        var events = EventSystem.current;
        var selected = events != null && events.currentSelectedGameObject != null
            ? events.currentSelectedGameObject.GetComponent<Selectable>() : null;
        bool hiddenScrollbar = selected == scroll.verticalScrollbar && content.rect.height <= scroll.viewport.rect.height;
        if (newOpen || selected == null || !selected.IsActive() || !selected.IsInteractable() || hiddenScrollbar)
            events?.SetSelectedGameObject(closeButton.gameObject);
        return true;
    }
    private void SetOffset(float offset)
    {
        scroll.StopMovement();
        var position = content.anchoredPosition;
        position.y = Mathf.Clamp(offset, 0, Mathf.Max(0, content.rect.height - scroll.viewport.rect.height));
        content.anchoredPosition = position;
    }
    private void ConfigureNavigation()
    {
        var eligible = rows.FindAll(row => row.gameObject.activeSelf && row.PurchaseButton.IsInteractable());
        var bar = scroll.verticalScrollbar;
        bool overflow = content.rect.height > scroll.viewport.rect.height;
        var closeNavigation = new Navigation { mode = Navigation.Mode.Explicit,
            selectOnRight = overflow ? bar : null,
            selectOnUp = eligible.Count > 0 ? eligible[eligible.Count - 1].PurchaseButton : null };
        closeButton.navigation = closeNavigation;
        if (bar != null)
            // Null vertical targets leave Up/Down to native Scrollbar.OnMove value changes.
            bar.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnLeft = closeButton,
                selectOnRight = eligible.Count > 0 ? eligible[0].PurchaseButton : closeButton };
        for (int i = 0; i < eligible.Count; i++)
            eligible[i].ConfigureNavigation(new Navigation { mode = Navigation.Mode.Explicit,
                selectOnUp = i > 0 ? eligible[i - 1].PurchaseButton : closeButton,
                selectOnDown = i + 1 < eligible.Count ? eligible[i + 1].PurchaseButton : closeButton,
                selectOnLeft = closeButton, selectOnRight = overflow && bar != null ? (Selectable)bar : closeButton }, EnsureVisible);
    }
    private void EnsureVisible(RectTransform row)
    {
        // Selection only adjusts the existing native ScrollRect's pixel offset.
        var corners = new Vector3[4];
        row.GetWorldCorners(corners);
        float bottom = scroll.viewport.InverseTransformPoint(corners[0]).y;
        float top = scroll.viewport.InverseTransformPoint(corners[1]).y;
        float offset = content.anchoredPosition.y;
        if (bottom < scroll.viewport.rect.yMin) offset += scroll.viewport.rect.yMin - bottom;
        else if (top > scroll.viewport.rect.yMax) offset -= top - scroll.viewport.rect.yMax;
        SetOffset(offset);
    }
    private void Purchase(int index) => Purchased?.Invoke(index);
    private void Close()
    {
        UpgradeUiSounds.Click();
        Closed?.Invoke();
    }
    public void Hide()
    {
        if (scroll != null) scroll.StopMovement();
        foreach (var row in rows) if (row != null) row.Hide();
        if (root != null) root.SetActive(false);
    }
    private void OnDisable() => Hidden?.Invoke();
    private void OnDestroy() { if (closeButton != null) closeButton.onClick.RemoveListener(Close); }
}
