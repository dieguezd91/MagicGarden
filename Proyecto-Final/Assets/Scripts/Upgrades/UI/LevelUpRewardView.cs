using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Shows the seeds and spells unlocked by one level, alone over a dimmed backdrop, until the player presses Continue.
public sealed class LevelUpRewardView : MonoBehaviour
{
    [SerializeField] private GameObject root;
    [SerializeField] private UIBackdrop backdrop;
    [SerializeField] private TMP_Text headingLabel;
    [SerializeField] private GameObject seedSlot, spellSlot;
    [SerializeField] private Image seedIcon, spellIcon;
    [SerializeField] private TMP_Text seedLabel, spellLabel;
    [SerializeField] private Button continueButton;
    [Header("Open/close tween (the card pops over the backdrop)")]
    [SerializeField] private RectTransform content;
    [SerializeField] private CanvasGroup group;
    [SerializeField] private ModalTween tween = new ModalTween();
    private Action afterClose;
    public event Action Continued;

    public bool IsConfigured => root == gameObject && backdrop != null && headingLabel != null && seedSlot != null && spellSlot != null &&
        seedIcon != null && spellIcon != null && seedLabel != null && spellLabel != null && continueButton != null;
    // Stays true while the open/close tween runs, so card selection stays blocked until the reward is fully gone.
    public bool IsVisible => root != null && root.activeSelf;
    public bool IsAnimating => tween != null && tween.IsRunning;
    private RectTransform Content => content != null ? content : (RectTransform)transform;
    private CanvasGroup Group
    {
        get
        {
            if (group == null && !Content.TryGetComponent(out group)) group = Content.gameObject.AddComponent<CanvasGroup>(); // authored in the prefab; this only covers a hand-removed one
            return group;
        }
    }

    public bool Show(LevelUnlockReward reward)
    {
        if (!IsConfigured || reward == null) return false;
        continueButton.onClick.RemoveListener(Continue);
        continueButton.onClick.AddListener(Continue);
        headingLabel.text = $"LEVEL {reward.Level} — New unlocks";
        Fill(seedSlot, seedIcon, seedLabel, "Seed", Names(reward.Plants, p => p != null ? p.plantName : null),
            reward.Plants.Count > 0 && reward.Plants[0] != null ? reward.Plants[0].plantIcon : null);
        Fill(spellSlot, spellIcon, spellLabel, "Spell", Names(reward.Spells, s => s != null ? s.SpellName : null),
            reward.Spells.Count > 0 && reward.Spells[0] != null ? reward.Spells[0].SpellIcon : null);
        bool fresh = !root.activeSelf || tween.IsClosing;
        afterClose = null;
        root.SetActive(true);
        backdrop.Show();
        if (fresh) tween.Open(Content, Group);
        EventSystem.current?.SetSelectedGameObject(continueButton.gameObject);
        return true;
    }

    // Animated close in Play Mode; immediate when inactive or outside Play Mode.
    public void Hide()
    {
        afterClose = null;
        Close();
    }

    public void HideImmediate()
    {
        afterClose = null;
        tween.Kill();
        if (backdrop != null) backdrop.HideImmediate();
        if (root != null) root.SetActive(false);
    }

    private void Close()
    {
        if (root == null || tween.IsClosing) return;
        if (backdrop != null) backdrop.Hide();
        if (!Application.isPlaying || !root.activeInHierarchy) { Finish(); return; }
        tween.Close(Content, Group, Finish);
    }

    private void Finish()
    {
        tween.Kill();
        if (root != null) root.SetActive(false);
        var next = afterClose;
        afterClose = null;
        next?.Invoke();
    }

    private void Continue()
    {
        if (!IsVisible || tween.IsClosing) return;
        afterClose = RaiseContinued;
        Close();
    }

    private void RaiseContinued() => Continued?.Invoke();

    private static void Fill(GameObject slot, Image icon, TMP_Text label, string kind, string names, Sprite sprite)
    {
        slot.SetActive(!string.IsNullOrEmpty(names));
        label.text = $"{kind}: {names}";
        icon.sprite = sprite; icon.enabled = sprite != null;
    }

    private static string Names<T>(IReadOnlyList<T> items, Func<T, string> name)
    {
        var names = new List<string>();
        foreach (var item in items) { string value = name(item); if (!string.IsNullOrEmpty(value)) names.Add(value); }
        return string.Join(", ", names);
    }

    private void OnDestroy() { tween?.Kill(); if (continueButton != null) continueButton.onClick.RemoveListener(Continue); }
}
