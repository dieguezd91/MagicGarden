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
    public bool IsVisible => root != null && root.activeInHierarchy;
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
        root.SetActive(true);
        EventSystem.current?.SetSelectedGameObject(empty ? continueButton.gameObject : cards[0].SelectButton.gameObject);
        return true;
    }
    private void Select(int index) => Selected?.Invoke(index);
    private void Continue() => Selected?.Invoke(-1);
    public void Hide() { if (root != null) root.SetActive(false); }
    private void OnDisable() => Hidden?.Invoke();
    private void OnDestroy() { if (continueButton != null) continueButton.onClick.RemoveListener(Continue); }
}
