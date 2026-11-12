using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class LevelUpPanelView : MonoBehaviour
{
    [SerializeField] private GameObject root;
    [SerializeField] private Image background;
    [SerializeField] private TMP_Text headingLabel, helpLabel, emptyLabel;
    [SerializeField] private Button continueButton;
    [SerializeField] private LevelUpCardView[] cards;
    [Header("Open/close tween (the window pops over the dimmed screen)")]
    [SerializeField] private RectTransform content;
    [SerializeField] private CanvasGroup group;
    [SerializeField] private ModalTween tween = new ModalTween();
    private bool closing;
    public event Action<int> Selected;
    public event Action Hidden;
    public bool IsConfigured
    {
        get
        {
            if (root != gameObject || background == null || headingLabel == null || helpLabel == null ||
                emptyLabel == null || continueButton == null || cards == null || cards.Length != 3) return false;
            for (int i = 0; i < cards.Length; i++) if (cards[i] == null || !cards[i].IsConfigured) return false;
            return true;
        }
    }
    // Stays true while the close tween runs.
    public bool IsVisible => root != null && root.activeInHierarchy;
    public bool IsAnimating => tween != null && tween.IsRunning;
    private RectTransform Content => content != null ? content : (RectTransform)transform;
    private CanvasGroup Group
    {
        get
        {
            if (group == null && !TryGetComponent(out group)) group = gameObject.AddComponent<CanvasGroup>(); // authored in the prefab; this only covers a hand-removed one
            return group;
        }
    }
    public bool Show(IList<LevelUpCardData> offers, int pending)
    {
        if (!IsConfigured || offers == null || offers.Count > cards.Length) return false;
        headingLabel.text = $"LEVEL UP — Choose a blessing ({pending} pending)";
        continueButton.onClick.RemoveListener(Continue);
        continueButton.onClick.AddListener(Continue);
        bool empty = offers.Count == 0;
        emptyLabel.gameObject.SetActive(empty); continueButton.gameObject.SetActive(empty);
        for (int i = 0; i < cards.Length; i++)
        {
            if (i < offers.Count) cards[i].Bind(offers[i], i, Select);
            else cards[i].Hide();
        }
        bool fresh = !root.activeSelf || tween.IsClosing;
        closing = false;
        root.SetActive(true);
        if (fresh) tween.Open(Content, Group);
        EventSystem.current?.SetSelectedGameObject(empty ? continueButton.gameObject : cards[0].SelectButton.gameObject);
        return true;
    }
    public void FocusFirst()
    {
        if (!IsVisible) return;
        var target = continueButton.gameObject.activeSelf ? continueButton.gameObject : cards[0].SelectButton.gameObject;
        EventSystem.current?.SetSelectedGameObject(target);
    }
    private void Select(int index) => Selected?.Invoke(index);
    private void Continue() => Selected?.Invoke(-1);
    // Animated close in Play Mode; immediate when inactive or outside Play Mode. Closing itself never raises Hidden.
    public void Hide()
    {
        if (root == null || tween.IsClosing) return;
        if (!Application.isPlaying || !root.activeInHierarchy) { HideImmediate(); return; }
        tween.Close(Content, Group, Finish);
    }
    public void HideImmediate()
    {
        tween.Kill();
        if (root == null || !root.activeSelf) return;
        closing = true;
        root.SetActive(false);
        closing = false;
    }
    private void Finish() { closing = true; root.SetActive(false); closing = false; }
    private void OnDisable() { tween?.Kill(); if (!closing) Hidden?.Invoke(); }
    private void OnDestroy() { tween?.Kill(); if (continueButton != null) continueButton.onClick.RemoveListener(Continue); }
}
