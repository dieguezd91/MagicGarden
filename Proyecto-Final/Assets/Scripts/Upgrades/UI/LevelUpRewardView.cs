using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Shows the seeds, spells and plant limit increase granted by one level, alone, until the player presses Continue.
// The blur behind it belongs to the level-up session (UpgradePanel.sessionBackdrop), which also stays behind the upgrade choices.
public sealed class LevelUpRewardView : MonoBehaviour
{
    [SerializeField] private GameObject root;
    [SerializeField] private TMP_Text headingLabel;
    [SerializeField] private GameObject seedSlot, spellSlot;
    [SerializeField] private Image seedIcon, spellIcon;
    [SerializeField] private TMP_Text seedLabel, spellLabel;
    [SerializeField] private GameObject plantLimitSlot;
    [SerializeField] private TMP_Text plantLimitLabel;
    // Designer-editable texts. {0}/{1} are filled in at runtime; an empty or invalid format falls back to the default.
    private const string DefaultHeadingFormat = "LEVEL {0} — Rewards", DefaultSeedFormat = "Seed: {0}", DefaultSpellFormat = "Spell: {0}",
        DefaultPlantLimitFormat = "Plant limit: +{0} ({1})", DefaultPlantLimitBonusOnlyFormat = "Plant limit: +{0}";
    [Header("Texts ({0}, {1} are filled in at runtime)")]
    [Tooltip("{0} = level reached")]
    [SerializeField] private string headingFormat = DefaultHeadingFormat;
    [Tooltip("{0} = names of the unlocked seeds")]
    [SerializeField] private string seedFormat = DefaultSeedFormat;
    [Tooltip("{0} = names of the unlocked spells")]
    [SerializeField] private string spellFormat = DefaultSpellFormat;
    [Tooltip("{0} = plant limit bonus, {1} = new plant limit")]
    [SerializeField] private string plantLimitFormat = DefaultPlantLimitFormat;
    [Tooltip("{0} = plant limit bonus. Used when the new plant limit is unknown")]
    [SerializeField] private string plantLimitBonusOnlyFormat = DefaultPlantLimitBonusOnlyFormat;
    [SerializeField] private Button continueButton;
    [Header("Open/close tween (the card pops over the backdrop)")]
    [SerializeField] private RectTransform content;
    [SerializeField] private CanvasGroup group;
    [SerializeField] private ModalTween tween = new ModalTween();
    // Rows enter one by one after the card pops in. Everything runs on unscaled time (timeScale is 0 during a level-up).
    [Header("Rows (enter one by one after the card pops in)")]
    [Tooltip("Reading pause after a row finishes entering, bouncing and counting, before the next row enters")]
    [SerializeField, Min(0f)] private float rowReadingGap = 0.35f;
    [Tooltip("Seconds to read the final reward state after all row effects finish, before Continue unlocks")]
    [SerializeField, Min(0f)] private float finalHoldDuration = 0.5f;
    [Tooltip("Seconds each row takes to fade and scale in")]
    [SerializeField, Min(0f)] private float rowDuration = 0.25f;
    [Tooltip("Scale a row starts from before growing to 1")]
    [SerializeField, Range(0f, 1f)] private float rowStartScale = 0.6f;
    [Tooltip("Ease of the row scale")]
    [SerializeField] private Ease rowEase = Ease.OutBack;
    [Header("Seed and spell icon bounce (when their row appears)")]
    [Tooltip("Peak scale of the icon during its bounce (1 = no bounce)")]
    [SerializeField, Min(1f)] private float iconBounceScale = 1.25f;
    [Tooltip("Seconds the icon bounce lasts")]
    [SerializeField, Min(0f)] private float iconBounceDuration = 0.3f;
    [Header("Plant limit (counts from the previous limit to the new one, then punches)")]
    [Tooltip("Seconds the plant limit number takes to count from the previous limit to the new one")]
    [SerializeField, Min(0f)] private float plantLimitCountDuration = 0.5f;
    [Tooltip("Peak scale of the plant limit text during its punch (1 = no punch)")]
    [SerializeField, Min(1f)] private float plantLimitPunchScale = 1.3f;
    [Tooltip("Seconds the plant limit punch lasts")]
    [SerializeField, Min(0f)] private float plantLimitPunchDuration = 0.25f;
    private Action afterClose;
    private Sequence rowSequence;
    private bool rowsRunning;
    private int rewardBonus, rewardLimit;
    private string finalPlantLimitText;
    // plantLimitFormat resolved once per Show, so an invalid designer format warns once, not on every counter step.
    private string resolvedPlantLimitFormat;
    public event Action Continued;

    public bool IsConfigured => root == gameObject && headingLabel != null && seedSlot != null && spellSlot != null &&
        seedIcon != null && spellIcon != null && seedLabel != null && spellLabel != null && continueButton != null &&
        plantLimitSlot != null && plantLimitLabel != null;
    // Stays true while the open/close tween or the row animations run, so Continue and card selection stay blocked until they finish.
    public bool IsVisible => root != null && root.activeSelf;
    public bool IsAnimating => (tween != null && tween.IsRunning) || rowsRunning;
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
        UpgradeUiSounds.AddHover(continueButton);
        headingLabel.text = TextFormat.Apply(headingFormat, DefaultHeadingFormat, reward.Level);
        Fill(seedSlot, seedIcon, seedLabel, seedFormat, DefaultSeedFormat, Names(reward.Plants, p => p != null ? p.plantName : null),
            reward.Plants.Count > 0 && reward.Plants[0] != null ? reward.Plants[0].plantIcon : null);
        Fill(spellSlot, spellIcon, spellLabel, spellFormat, DefaultSpellFormat, Names(reward.Spells, s => s != null ? s.SpellName : null),
            reward.Spells.Count > 0 && reward.Spells[0] != null ? reward.Spells[0].SpellIcon : null);
        plantLimitSlot.SetActive(reward.PlantLimitBonus > 0);
        rewardBonus = reward.PlantLimitBonus; rewardLimit = reward.PlantLimit;
        // Only resolved when it is used (known limit), so an unused invalid format never warns.
        resolvedPlantLimitFormat = rewardLimit > 0 ? TextFormat.Resolve(plantLimitFormat, DefaultPlantLimitFormat, rewardBonus, rewardLimit) : DefaultPlantLimitFormat;
        finalPlantLimitText = rewardLimit > 0
            ? TextFormat.Apply(resolvedPlantLimitFormat, DefaultPlantLimitFormat, rewardBonus, rewardLimit)
            : TextFormat.Apply(plantLimitBonusOnlyFormat, DefaultPlantLimitBonusOnlyFormat, rewardBonus);
        plantLimitLabel.text = finalPlantLimitText;
        afterClose = null;
        tween.Kill();
        KillRows(true);
        root.SetActive(true);
        PrepareRows();
        tween.Open(Content, Group, StartRows);
        UpgradeUiSounds.RewardOpen();
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
        KillRows(true);
        tween.Kill();
        if (root != null) root.SetActive(false);
    }

    private void Close()
    {
        if (root == null || tween.IsClosing) return;
        KillRows(true);
        if (!Application.isPlaying || !root.activeInHierarchy) { Finish(); return; }
        tween.Close(Content, Group, Finish);
        UpgradeUiSounds.RewardClose();
    }

    private void Finish()
    {
        tween.Kill();
        var next = afterClose;
        afterClose = null;
        if (root != null) root.SetActive(false);
        next?.Invoke();
    }

    private void Continue()
    {
        if (!IsVisible || IsAnimating) return;
        UpgradeUiSounds.Click();
        afterClose = RaiseContinued;
        Close();
    }

    private void RaiseContinued() => Continued?.Invoke();

    private void PrepareRows()
    {
        if (!Application.isPlaying) return; // outside Play Mode the rows are already in their final state
        continueButton.interactable = false;
        foreach (var slot in ActiveRows())
        {
            RowGroup(slot).alpha = 0f;
            slot.transform.localScale = Vector3.one * rowStartScale;
        }
        if (plantLimitSlot.activeSelf && rewardLimit > 0)
            plantLimitLabel.text = TextFormat.Apply(resolvedPlantLimitFormat, DefaultPlantLimitFormat, rewardBonus, PreviousLimit);
    }

    private int PreviousLimit => Mathf.Max(0, rewardLimit - rewardBonus);

    private IEnumerable<GameObject> ActiveRows()
    {
        if (seedSlot.activeSelf) yield return seedSlot;
        if (spellSlot.activeSelf) yield return spellSlot;
        if (plantLimitSlot.activeSelf) yield return plantLimitSlot;
    }

    private static CanvasGroup RowGroup(GameObject slot) => slot.TryGetComponent(out CanvasGroup group) ? group : slot.AddComponent<CanvasGroup>();

    // Called once the card has popped in: seed, spell and plant limit enter one by one (scale + alpha), icons bounce as their row appears,
    // and the plant limit counts from the previous limit to the new one and punches.
    private void StartRows()
    {
        KillRows(false);
        if (!Application.isPlaying) { SnapRows(); return; }
        continueButton.interactable = false;
        var sequence = DOTween.Sequence().SetUpdate(true);
        float enter = Mathf.Max(0.001f, rowDuration), at = 0f;
        foreach (var slot in ActiveRows())
        {
            var group = RowGroup(slot);
            sequence.InsertCallback(at, UpgradeUiSounds.RewardItem);
            sequence.Insert(at, slot.transform.DOScale(1f, enter).SetEase(rowEase));
            sequence.Insert(at, group.DOFade(1f, enter).SetEase(Ease.OutQuad));
            if (slot == seedSlot) AddIconBounce(sequence, seedIcon, at);
            else if (slot == spellSlot) AddIconBounce(sequence, spellIcon, at);
            else AddPlantLimit(sequence, at + enter);
            // Include the actual icon/count/punch end, not just the row's entrance.
            at = sequence.Duration() + Mathf.Max(0f, rowReadingGap);
        }
        // No inter-row gap after the last row: the final reading hold takes its place.
        sequence.AppendInterval(Mathf.Max(0f, finalHoldDuration));
        if (sequence.Duration() <= 0f) { sequence.Kill(); SnapRows(); return; }
        rowSequence = sequence;
        rowsRunning = true;
        sequence.OnComplete(() => { rowSequence = null; rowsRunning = false; SnapRows(); });
    }

    private void AddIconBounce(Sequence sequence, Image icon, float at)
    {
        if (icon == null || !icon.enabled || iconBounceScale <= 1f) return;
        sequence.Insert(at, icon.rectTransform.DOPunchScale(Vector3.one * (iconBounceScale - 1f), Mathf.Max(0.001f, iconBounceDuration), 1, 0f));
    }

    private void AddPlantLimit(Sequence sequence, float at)
    {
        float punchAt = at;
        if (rewardLimit > 0 && rewardBonus > 0)
        {
            int from = PreviousLimit, shown = from;
            float count = Mathf.Max(0.001f, plantLimitCountDuration);
            sequence.Insert(at, DOTween.To(() => 0f, t =>
            {
                int value = Mathf.RoundToInt(Mathf.Lerp(from, rewardLimit, t));
                if (value == shown) return;
                shown = value;
                plantLimitLabel.text = TextFormat.Apply(resolvedPlantLimitFormat, DefaultPlantLimitFormat, rewardBonus, value);
            }, 1f, count).SetEase(Ease.Linear).OnComplete(() => plantLimitLabel.text = finalPlantLimitText));
            punchAt += count;
        }
        if (plantLimitPunchScale > 1f)
            sequence.Insert(punchAt, plantLimitLabel.rectTransform.DOPunchScale(Vector3.one * (plantLimitPunchScale - 1f), Mathf.Max(0.001f, plantLimitPunchDuration), 1, 0f));
    }

    // Final resting state of every row: scale 1, alpha 1, final texts.
    private void SnapRows()
    {
        foreach (var slot in new[] { seedSlot, spellSlot, plantLimitSlot })
        {
            slot.transform.localScale = Vector3.one;
            if (slot.TryGetComponent(out CanvasGroup group)) group.alpha = 1f;
        }
        if (seedIcon != null) seedIcon.rectTransform.localScale = Vector3.one;
        if (spellIcon != null) spellIcon.rectTransform.localScale = Vector3.one;
        plantLimitLabel.rectTransform.localScale = Vector3.one;
        if (finalPlantLimitText != null) plantLimitLabel.text = finalPlantLimitText;
        continueButton.interactable = true;
    }

    private void KillRows(bool snap)
    {
        if (rowSequence != null) { rowSequence.Kill(); rowSequence = null; }
        rowsRunning = false;
        if (snap && IsConfigured) SnapRows();
    }

    private static void Fill(GameObject slot, Image icon, TMP_Text label, string format, string defaultFormat, string names, Sprite sprite)
    {
        slot.SetActive(!string.IsNullOrEmpty(names));
        label.text = TextFormat.Apply(format, defaultFormat, names);
        icon.sprite = sprite; icon.enabled = sprite != null;
    }

    private static string Names<T>(IReadOnlyList<T> items, Func<T, string> name)
    {
        var names = new List<string>();
        foreach (var item in items) { string value = name(item); if (!string.IsNullOrEmpty(value)) names.Add(value); }
        return string.Join(", ", names);
    }

    private void OnDisable()
    {
        afterClose = null;
        tween?.Kill();
        KillRows(true);
        if (IsConfigured) tween?.Snap(Content, Group, true);
    }

    private void OnDestroy() { tween?.Kill(); KillRows(true); if (continueButton != null) continueButton.onClick.RemoveListener(Continue); }
}
