using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using UnityEngine.EventSystems;

public class SpellSlotUI : ImprovedUIButton, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Slot Components")]
    [SerializeField] private Image slotBackground;
    [SerializeField] private Image slotIcon;
    [SerializeField] private Image cooldownOverlay;
    [SerializeField] private TextMeshProUGUI cooldownText;
    [SerializeField] private TextMeshProUGUI slotNumberText;

    [Header("Visual Settings")]
    [SerializeField] private Color normalColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
    [SerializeField] private Color selectedColor = new Color(0.4f, 0.6f, 1f, 1f);
    [SerializeField] private Color lockedColor = new Color(0.1f, 0.1f, 0.1f, 0.8f);
    [SerializeField] private float normalScale = 1f;
    [SerializeField] private float selectedScale = 1.1f;

    [Header("Cooldown Settings")]
    [SerializeField] private Color cooldownOverlayColor = new Color(0f, 0f, 0f, 0.7f);

    [Header("Ready Cue (cooldown finished)")]
    [Tooltip("Master switch: when off, nothing plays when the cooldown ends.")]
    [SerializeField] private bool readyCueEnabled = true;
    [Tooltip("Extra icon scale of the ready punch (0.25 = grows 25% and returns). 0 disables the punch.")]
    [SerializeField, Min(0f)] private float readyPunchStrength = 0.25f;
    [Tooltip("Seconds the ready punch lasts (unscaled time).")]
    [SerializeField, Min(0f)] private float readyPunchDuration = 0.3f;
    [Tooltip("Color the slot frame and icon flash towards when the spell is ready.")]
    [SerializeField] private Color readyFlashColor = new Color(1f, 0.95f, 0.5f, 1f);
    [Tooltip("Seconds the ready flash lasts (unscaled time). 0 disables the flash.")]
    [SerializeField, Min(0f)] private float readyFlashDuration = 0.25f;
    [Tooltip("SoundManager sound played when the spell becomes ready. Leave empty for no sound.")]
    [SerializeField] private string readySoundName = "UI_Prompt_Show";

    [Header("Selection Tween")]
    [Tooltip("When off, selecting a slot snaps its scale instead of tweening.")]
    [SerializeField] private bool selectionTweenEnabled = true;
    [Tooltip("Seconds the selection scale tween lasts (unscaled time). 0 snaps.")]
    [SerializeField, Min(0f)] private float selectionTweenDuration = 0.12f;
    [Tooltip("Easing of the selection scale tween.")]
    [SerializeField] private Ease selectionTweenEase = Ease.OutQuad;

    [Header("Blocked Cast Feedback (cooldown refusal)")]
    [Tooltip("Master switch: when off, a cast refused for cooldown shows nothing on the slot.")]
    [SerializeField] private bool blockedFeedbackEnabled = true;
    [Tooltip("Horizontal shake distance in local units. 0 disables the shake.")]
    [SerializeField, Min(0f)] private float blockedShakeStrength = 6f;
    [Tooltip("Seconds the blocked shake lasts (unscaled time).")]
    [SerializeField, Min(0f)] private float blockedShakeDuration = 0.25f;
    [Tooltip("Number of back-and-forth swings of the blocked shake.")]
    [SerializeField, Min(1)] private int blockedShakeSwings = 4;
    [Tooltip("Color the slot frame and icon flash towards when a cast is blocked by cooldown.")]
    [SerializeField] private Color blockedFlashColor = new Color(1f, 0.35f, 0.35f, 1f);
    [Tooltip("Seconds the blocked flash lasts (unscaled time). 0 disables the flash.")]
    [SerializeField, Min(0f)] private float blockedFlashDuration = 0.25f;

    public int SlotIndex { get; private set; }
    /// <summary>How many times the ready cue has started (diagnostics and tests).</summary>
    public int ReadyCueCount { get; private set; }
    /// <summary>How many times the blocked feedback has started (diagnostics and tests).</summary>
    public int BlockedFeedbackCount { get; private set; }

    public event Action<int> OnSlotClicked;
    private SpellSlot displayedSlot;
    private SpellInventory cooldownOwner;
    private int inventoryIndex = -1;
    private bool equipmentMode;
    private bool hasAssignment;
    private bool selected;
    private bool equipmentPhaseActive = true;

    // Ready cue tracking: the cue fires only when the same unlocked slot goes from cooldown to ready.
    private SpellSlot trackedCooldownSlot;
    private bool wasOnCooldown;

    // Tween state. Every tween restores its exact base before it ends or is killed.
    private Vector3 restScale;
    private bool hasRestScale;
    private Tween selectionTween;
    private Tween punchTween;
    private Vector3 iconBaseScale;
    private Tween flashTween;
    private float flashAmount;
    private Color flashTarget;
    private Color frameBaseColor;
    private Color iconBaseColor;
    private Tween shakeTween;
    private Vector3 shakeBasePosition;
    private SpellInventory blockedSource;

    public void UpdateEquipmentDisplay(SpellDataSO identity, SpellSlot slot, SpellInventory owner, int index)
    {
        equipmentMode = true;
        hasAssignment = identity != null;
        displayedSlot = slot;
        cooldownOwner = owner;
        inventoryIndex = index;
        ListenForBlockedCasts(owner);
        if (!hasAssignment) { selected = false; trackedCooldownSlot = null; wasOnCooldown = false; ShowLockedSlot(); ApplyEquipmentStyle(); return; }
        if (slotIcon != null)
        {
            slotIcon.sprite = identity.SpellIcon;
            slotIcon.preserveAspect = true;
            slotIcon.gameObject.SetActive(true);
        }
        UpdateCooldownDisplay(slot);
        ApplyEquipmentStyle();
    }

    protected override void Awake()
    {
        base.Awake();
        OnClick.RemoveListener(HandleSlotClick);
        OnClick.AddListener(HandleSlotClick);

        if (cooldownOverlay != null)
        {
            cooldownOverlay.type = Image.Type.Filled;
            cooldownOverlay.fillMethod = Image.FillMethod.Radial360;
            cooldownOverlay.fillOrigin = (int)Image.Origin360.Top;
            cooldownOverlay.fillClockwise = false;
            cooldownOverlay.fillAmount = 0f;
            cooldownOverlay.color = cooldownOverlayColor;
        }
    }

    private void OnDisable()
    {
        StopListeningForBlockedCasts();
        ResetFeedback();
    }

    protected override void OnDestroy()
    {
        OnClick.RemoveListener(HandleSlotClick);
        StopListeningForBlockedCasts();
        ResetFeedback();
        base.OnDestroy();
    }

    public void Initialize(int index)
    {
        SlotIndex = index;

        if (slotNumberText != null)
        {
            slotNumberText.text = (index + 1).ToString();
        }

        if (cooldownOverlay != null)
        {
            cooldownOverlay.fillAmount = 0f;
        }

        if (cooldownText != null)
        {
            cooldownText.gameObject.SetActive(false);
        }
    }

    private void HandleSlotClick()
    {
        OnSlotClicked?.Invoke(SlotIndex);
    }

    public void UpdateSlotDisplay(SpellSlot spellSlot)
    {
        if (spellSlot == null || !spellSlot.isUnlocked)
        {
            ShowLockedSlot();
        }
        else
        {
            ShowUnlockedSlot(spellSlot);
        }
    }

    private void ShowLockedSlot()
    {
        if (slotIcon != null)
        {
            if (equipmentMode) slotIcon.sprite = null;
            slotIcon.gameObject.SetActive(false);
        }

        if (slotBackground != null)
            slotBackground.color = lockedColor;

        if (cooldownText != null)
            cooldownText.gameObject.SetActive(false);

        if (cooldownOverlay != null)
            cooldownOverlay.fillAmount = 0f;
    }

    private void ShowUnlockedSlot(SpellSlot spellSlot)
    {
        if (slotIcon != null && spellSlot.spellIcon != null)
        {
            slotIcon.sprite = spellSlot.spellIcon;
            slotIcon.preserveAspect = true;
            slotIcon.gameObject.SetActive(true);
        }

        UpdateCooldownDisplay(spellSlot);
    }

    public void UpdateCooldownDisplay(SpellSlot spellSlot)
    {
        bool onCooldown = spellSlot != null && spellSlot.isUnlocked && spellSlot.currentCooldown > 0f;
        TrackReady(spellSlot, onCooldown);

        if (cooldownOverlay != null)
        {
            if (onCooldown)
            {
                float progress = equipmentMode
                    ? (cooldownOwner != null && inventoryIndex >= 0 ? cooldownOwner.GetCooldownProgress(inventoryIndex) : 0f)
                    : (spellSlot.cooldown > 0f ? spellSlot.currentCooldown / spellSlot.cooldown : 0f);
                cooldownOverlay.fillAmount = Mathf.Clamp01(progress);
            }
            else
            {
                cooldownOverlay.fillAmount = 0f;
            }
        }

        if (cooldownText != null)
        {
            cooldownText.gameObject.SetActive(onCooldown);
            if (onCooldown)
            {
                cooldownText.text = spellSlot.currentCooldown.ToString("F2");
            }
        }
    }

    public void SetSelected(bool selected)
    {
        this.selected = selected;
        if (equipmentMode) { ApplyEquipmentStyle(); return; }
        if (slotBackground == null) return;

        bool isLocked = equipmentMode ? !hasAssignment : SpellInventory.Instance?.GetSpellSlot(SlotIndex)?.isUnlocked == false;

        if (isLocked)
        {
            slotBackground.color = lockedColor;
        }
        else
        {
            slotBackground.color = selected ? selectedColor : normalColor;
        }

        CaptureFlashBase(false);
        float scale = selected ? selectedScale : normalScale;
        MoveToScale(transform.localScale, new Vector3(scale, scale, 1f));
        ReapplyFlash();
    }

    public void SetEquipmentPhaseActive(bool active)
    {
        equipmentPhaseActive = active;
        ApplyEquipmentStyle();
    }

    private void ApplyEquipmentStyle()
    {
        if (!equipmentMode) return;
        // The shared style snaps the root scale and recolors every call; keep a running tween's value and flash on top of it.
        Vector3 current = transform.localScale;
        EquipmentHudStyle.Apply(transform, slotBackground, slotIcon, slotNumberText, null,
            hasAssignment, selected, equipmentPhaseActive);
        CaptureFlashBase();
        MoveToScale(current, transform.localScale);
        ReapplyFlash();
    }

    #region Feedback
    /// <summary>Starts the ready cue (punch, flash, sound). Each part can be disabled in the Inspector.</summary>
    public void PlayReadyCue()
    {
        if (!readyCueEnabled) return;
        ReadyCueCount++;
        if (!string.IsNullOrEmpty(readySoundName)) SoundManager.Instance?.Play(readySoundName);
        if (!CanAnimate()) return;
        if (readyPunchStrength > 0f && readyPunchDuration > 0f && slotIcon != null) StartIconPunch();
        StartFlash(readyFlashColor, readyFlashDuration);
    }

    /// <summary>Shakes and flashes the slot because a cast was refused for cooldown.</summary>
    public void PlayBlockedFeedback()
    {
        if (!blockedFeedbackEnabled) return;
        BlockedFeedbackCount++;
        if (!CanAnimate()) return;
        if (blockedShakeStrength > 0f && blockedShakeDuration > 0f) StartShake();
        StartFlash(blockedFlashColor, blockedFlashDuration);
    }

    private bool CanAnimate() => Application.isPlaying && isActiveAndEnabled;

    private void TrackReady(SpellSlot spellSlot, bool onCooldown)
    {
        // A different spell assigned to this position is not a "cooldown ended" event.
        if (!ReferenceEquals(spellSlot, trackedCooldownSlot))
        {
            trackedCooldownSlot = spellSlot;
            wasOnCooldown = onCooldown;
            return;
        }
        bool becameReady = wasOnCooldown && !onCooldown && spellSlot != null && spellSlot.isUnlocked;
        wasOnCooldown = onCooldown;
        if (becameReady) PlayReadyCue();
    }

    private void MoveToScale(Vector3 current, Vector3 target)
    {
        bool animate = hasRestScale && CanAnimate() && selectionTweenEnabled && selectionTweenDuration > 0f;
        if (!animate)
        {
            KillSelectionTween();
            restScale = target;
            hasRestScale = true;
            transform.localScale = target;
            return;
        }
        if (restScale == target)
        {
            transform.localScale = selectionTween != null ? current : target;
            return;
        }
        restScale = target;
        if (selectionTween != null) { selectionTween.Kill(); selectionTween = null; }
        transform.localScale = current;
        selectionTween = transform.DOScale(target, selectionTweenDuration)
            .SetEase(selectionTweenEase).SetUpdate(true).SetLink(gameObject)
            .OnComplete(() => { transform.localScale = restScale; selectionTween = null; });
    }

    private void KillSelectionTween()
    {
        if (selectionTween == null) return;
        selectionTween.Kill();
        selectionTween = null;
    }

    private void StartIconPunch()
    {
        KillPunch();
        Transform icon = slotIcon.transform;
        iconBaseScale = icon.localScale;
        punchTween = icon.DOPunchScale(Vector3.one * readyPunchStrength, readyPunchDuration, 1, 1f)
            .SetUpdate(true).SetLink(gameObject)
            .OnComplete(() => { if (slotIcon != null) slotIcon.transform.localScale = iconBaseScale; punchTween = null; });
    }

    private void KillPunch()
    {
        if (punchTween == null) return;
        punchTween.Kill();
        punchTween = null;
        if (slotIcon != null) slotIcon.transform.localScale = iconBaseScale;
    }

    private void StartFlash(Color color, float duration)
    {
        if (duration <= 0f) return;
        KillFlash();
        CaptureFlashBase();
        flashTarget = color;
        flashAmount = 1f;
        ReapplyFlash();
        flashTween = DOVirtual.Float(1f, 0f, duration, v => { flashAmount = v; ReapplyFlash(); })
            .SetEase(Ease.OutQuad).SetUpdate(true).SetLink(gameObject)
            .OnComplete(() => { EndFlash(); flashTween = null; });
    }

    private void KillFlash()
    {
        if (flashTween == null) return;
        flashTween.Kill();
        flashTween = null;
        EndFlash();
    }

    private void EndFlash()
    {
        flashAmount = 0f;
        if (slotBackground != null) slotBackground.color = frameBaseColor;
        if (slotIcon != null) slotIcon.color = iconBaseColor;
    }

    // While no flash runs, remember the colors the style gave; the flash is always blended over these.
    // The frame color is always fresh where this is called; the icon color is only fresh if the caller just restyled it.
    private void CaptureFlashBase(bool iconIsFresh = true)
    {
        if (slotBackground != null) frameBaseColor = slotBackground.color;
        if (slotIcon != null && (iconIsFresh || flashTween == null)) iconBaseColor = slotIcon.color;
    }

    private void ReapplyFlash()
    {
        if (flashTween == null) return;
        if (slotBackground != null) slotBackground.color = Color.Lerp(frameBaseColor, flashTarget, flashAmount);
        if (slotIcon != null) slotIcon.color = Color.Lerp(iconBaseColor, flashTarget, flashAmount);
    }

    private void StartShake()
    {
        KillShake();
        shakeBasePosition = transform.localPosition;
        float strength = blockedShakeStrength;
        float swings = blockedShakeSwings;
        shakeTween = DOVirtual.Float(0f, 1f, blockedShakeDuration, t =>
                transform.localPosition = shakeBasePosition + Vector3.right * (Mathf.Sin(t * swings * 2f * Mathf.PI) * strength * (1f - t)))
            .SetUpdate(true).SetLink(gameObject)
            .OnComplete(() => { transform.localPosition = shakeBasePosition; shakeTween = null; });
    }

    private void KillShake()
    {
        if (shakeTween == null) return;
        shakeTween.Kill();
        shakeTween = null;
        transform.localPosition = shakeBasePosition;
    }

    // Kill every tween and leave the slot at its exact resting scale, position and colors.
    private void ResetFeedback()
    {
        KillPunch();
        KillShake();
        KillFlash();
        KillSelectionTween();
        if (hasRestScale) transform.localScale = restScale;
    }

    private void ListenForBlockedCasts(SpellInventory owner)
    {
        if (blockedSource == owner) return;
        StopListeningForBlockedCasts();
        blockedSource = owner;
        if (blockedSource != null) blockedSource.OnCastBlockedByCooldown += HandleCastBlocked;
    }

    private void StopListeningForBlockedCasts()
    {
        if (blockedSource != null) blockedSource.OnCastBlockedByCooldown -= HandleCastBlocked;
        blockedSource = null;
    }

    private void HandleCastBlocked(int index)
    {
        if (equipmentMode && hasAssignment && index == inventoryIndex) PlayBlockedFeedback();
    }
    #endregion
    public void OnPointerEnter(PointerEventData eventData)
    {
        SpellSlot spell = equipmentMode ? displayedSlot : SpellInventory.Instance?.GetSpellSlot(SlotIndex);
        if (spell != null && spell.isUnlocked)
        {
            UIEvents.RequestSpellTooltip(spell);
            Debug.Log("Hover Spell Slot: " + SlotIndex);

        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        UIEvents.HideSpellTooltip();
    }


}
