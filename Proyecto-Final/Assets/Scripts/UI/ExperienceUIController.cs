using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ExperienceUIController : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Slider xpSlider;
    [SerializeField] private TextMeshProUGUI levelText;
    [SerializeField] private TextMeshProUGUI xpText;
    [SerializeField] private PlayerExperienceSystem playerExperienceSystem;
    [Tooltip("Graphic tinted by the flash when the bar fills. Empty = the Graphic on the slider's Fill Rect")]
    [SerializeField] private Graphic fillGraphic;
    [Tooltip("Transform scaled by the punch when XP is gained. Empty = the slider's own transform")]
    [SerializeField] private Transform punchTarget;

    [Header("Smooth Fill")]
    [Tooltip("Seconds the bar takes to fill to the new XP value (each segment when a level is gained). 0 = jump")]
    [SerializeField, Min(0f)] private float fillDuration = 0.5f;
    [Tooltip("Easing curve of the bar fill")]
    [SerializeField] private Ease fillEase = Ease.OutCubic;

    [Header("Punch On XP Gain")]
    [Tooltip("Scale added at the peak of the punch (0.1 = 10% bigger). 0 = no punch")]
    [SerializeField, Min(0f)] private float punchStrength = 0.12f;
    [Tooltip("Seconds the punch lasts")]
    [SerializeField, Min(0.01f)] private float punchDuration = 0.35f;
    [Tooltip("Minimum seconds between two punches, so many XP orbs in a row do not make the bar vibrate")]
    [SerializeField, Min(0f)] private float punchMinInterval = 0.2f;

    [Header("Flash When Full")]
    [Tooltip("Color the fill flashes to when the bar reaches its maximum, right before the level changes")]
    [SerializeField] private Color fullFlashColor = Color.white;
    [Tooltip("Seconds to go from the fill color to the flash color, and the same again to come back")]
    [SerializeField, Min(0.01f)] private float fullFlashDuration = 0.2f;
    [Tooltip("Seconds the completed bar stays full after its flash, before showing the next level's range.")]
    [SerializeField, Min(0f)] private float fullHoldDuration = 0.3f;

    private bool isSubscribed;
    private Tween fillTween;
    private Tween punchTween;
    private Tween flashTween;
    private Vector3 punchBaseScale;
    private Color fillBaseColor;
    private float lastPunchTime = float.NegativeInfinity;
    private int shownLevel = -1;
    private int shownXp = -1;
    private bool hasDeferredExperience;
    private int deferredLevel, deferredXp, deferredRequiredXp;

    public bool IsPresentationBusy => isActiveAndEnabled && (hasDeferredExperience ||
        IsRunning(fillTween) || IsRunning(punchTween) || IsRunning(flashTween));
    private static bool IsRunning(Tween tween) => tween != null && tween.IsActive() && !tween.IsComplete();
    private bool GoldFeedbackPlaying => FloatingTextController.Instance != null && FloatingTextController.Instance.IsShowingGoldFeedback;

    private void OnEnable()
    {
        KillTweens();
        EnsurePlayerExperienceSystem();
        SubscribeToEvents();
        UpdateUIFromCurrentState();
    }

    private void Start()
    {
        EnsurePlayerExperienceSystem();
        SubscribeToEvents();
        UpdateUIFromCurrentState();
    }

    private void OnDisable()
    {
        UnsubscribeFromEvents();
        KillTweens();
    }

    private void OnDestroy()
    {
        UnsubscribeFromEvents();
        KillTweens();
    }

    private void EnsurePlayerExperienceSystem()
    {
        if (playerExperienceSystem == null)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerExperienceSystem = player.GetComponent<PlayerExperienceSystem>();
            }

            if (playerExperienceSystem == null)
            {
                playerExperienceSystem = FindObjectOfType<PlayerExperienceSystem>();
            }
        }
    }

    private void SubscribeToEvents()
    {
        if (playerExperienceSystem != null && !isSubscribed)
        {
            playerExperienceSystem.OnExperienceChanged += HandleExperienceChanged;
            playerExperienceSystem.OnProgressionReset += HandleProgressionReset;
            isSubscribed = true;
        }
    }

    private void UnsubscribeFromEvents()
    {
        if (isSubscribed)
        {
            if (playerExperienceSystem != null)
            {
                playerExperienceSystem.OnExperienceChanged -= HandleExperienceChanged;
                playerExperienceSystem.OnProgressionReset -= HandleProgressionReset;
            }
            isSubscribed = false;
        }
    }

    // Normal enable snaps; enabling during collected feedback queues the current model, not an old payload.
    private void UpdateUIFromCurrentState()
    {
        if (playerExperienceSystem == null) return;
        if (Application.isPlaying && GoldFeedbackPlaying)
        {
            HandleExperienceChanged(playerExperienceSystem.CurrentLevel,
                playerExperienceSystem.CurrentExperience, playerExperienceSystem.ExperienceRequired);
            return;
        }
        ApplyExperience(playerExperienceSystem.CurrentLevel,
            playerExperienceSystem.CurrentExperience, playerExperienceSystem.ExperienceRequired, false);
    }

    private void Update()
    {
        if (!hasDeferredExperience || GoldFeedbackPlaying) return;
        hasDeferredExperience = false;
        ApplyExperience(deferredLevel, deferredXp, deferredRequiredXp, true);
    }

    private void HandleProgressionReset()
    {
        KillTweens();
        // Reset is not a delayed gain: discard the old payload and show the reset state even during gold.
        if (playerExperienceSystem != null)
            ApplyExperience(playerExperienceSystem.CurrentLevel,
                playerExperienceSystem.CurrentExperience, playerExperienceSystem.ExperienceRequired, false);
    }

    private void HandleExperienceChanged(int level, int currentXp, int requiredXp)
    {
        bool decreased = level < shownLevel || currentXp < shownXp ||
            (hasDeferredExperience && (level < deferredLevel || currentXp < deferredXp));
        if (decreased)
        {
            KillTweens();
            ApplyExperience(level, currentXp, requiredXp, false);
            return;
        }
        if (Application.isPlaying && isActiveAndEnabled && GoldFeedbackPlaying)
        {
            deferredLevel = level;
            deferredXp = currentXp;
            deferredRequiredXp = requiredXp;
            hasDeferredExperience = true;
            return;
        }
        hasDeferredExperience = false;
        ApplyExperience(level, currentXp, requiredXp, true);
    }

    // Night gains defer all visuals (including texts) until the collected feedback ends. Other texts update immediately.
    // A reset or any decrease of level or XP snaps instead of animating.
    private void ApplyExperience(int level, int currentXp, int requiredXp, bool animate)
    {
        if (levelText != null)
        {
            levelText.text = $"LV. {level}";
        }

        if (xpText != null)
        {
            xpText.text = requiredXp > 0 ? $"{currentXp} / {requiredXp} XP" : $"{currentXp} XP (MAX)";
        }

        bool canAnimate = animate && Application.isPlaying && isActiveAndEnabled && xpSlider != null
            && shownLevel >= 0 && level >= shownLevel && currentXp >= shownXp;
        bool gainedXp = canAnimate && currentXp > shownXp;
        bool levelGained = canAnimate && level > shownLevel;
        shownXp = currentXp;

        if (xpSlider == null)
        {
            shownLevel = level;
            return;
        }

        int floor = playerExperienceSystem != null && playerExperienceSystem.ProgressionData != null
            ? playerExperienceSystem.ProgressionData.GetLevelThreshold(level) : 0;
        float max = requiredXp > floor ? requiredXp : floor + 1;
        float target = requiredXp > 0 ? currentXp : floor + 1;

        KillFillTweens();
        if (!canAnimate)
        {
            SetRange(floor, max);
            xpSlider.value = target;
            shownLevel = level;
            return;
        }

        if (gainedXp) TryPunch();

        if (levelGained)
        {
            PlayLevelChange(level, floor, max, target);
        }
        else
        {
            shownLevel = level;
            SetRange(floor, max);
            if (!Mathf.Approximately(xpSlider.value, target)) fillTween = CreateValueTween(xpSlider.value, target);
        }
    }

    // Fill to the end of the range being shown, flash, then show the new level's range from its floor up to the final value.
    // Several levels gained at once end the same way: the intermediate levels are not part of the event payload.
    private void PlayLevelChange(int level, float floor, float max, float target)
    {
        var sequence = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
        sequence.Append(CreateValueTween(xpSlider.value, xpSlider.maxValue));
        var flash = CreateFlashSequence();
        if (flash != null) sequence.Append(flash);
        sequence.AppendInterval(Mathf.Max(0f, fullHoldDuration));
        sequence.AppendCallback(() =>
        {
            shownLevel = level;
            SetRange(floor, max);
            xpSlider.value = floor;
        });
        sequence.Append(CreateValueTween(floor, target));
        fillTween = sequence;
    }

    private void SetRange(float floor, float max)
    {
        xpSlider.minValue = floor;
        xpSlider.maxValue = max;
    }

    private Tween CreateValueTween(float from, float to)
    {
        // Assign an exact endpoint on completion, distinct from a merely near-target intermediate frame.
        return DOVirtual.Float(from, to, fillDuration, v => xpSlider.value = v)
            .SetEase(fillEase).SetUpdate(true)
            .OnComplete(() => { if (xpSlider != null) xpSlider.value = to; });
    }

    private Tween CreateFlashSequence()
    {
        var graphic = ResolveFillGraphic();
        if (graphic == null) return null;
        fillBaseColor = graphic.color;
        var sequence = DOTween.Sequence().SetUpdate(true);
        sequence.Append(graphic.DOColor(fullFlashColor, fullFlashDuration));
        sequence.Append(graphic.DOColor(fillBaseColor, fullFlashDuration));
        flashTween = sequence;
        return sequence;
    }

    private Graphic ResolveFillGraphic()
    {
        if (fillGraphic == null && xpSlider != null && xpSlider.fillRect != null)
        {
            fillGraphic = xpSlider.fillRect.GetComponent<Graphic>();
        }
        return fillGraphic;
    }

    private Transform PunchTransform()
    {
        return punchTarget != null ? punchTarget : (xpSlider != null ? xpSlider.transform : null);
    }

    // Throttled; always restarts from the stored base scale so repeated punches never drift the scale.
    private void TryPunch()
    {
        var target = PunchTransform();
        if (target == null || punchStrength <= 0f) return;
        if (Time.unscaledTime - lastPunchTime < punchMinInterval) return;
        lastPunchTime = Time.unscaledTime;

        KillPunch();
        punchBaseScale = target.localScale;
        punchTween = target.DOPunchScale(Vector3.one * punchStrength, punchDuration, 1, 1f)
            .SetUpdate(true).SetLink(gameObject)
            .OnComplete(() => { if (target != null) target.localScale = punchBaseScale; });
    }

    private void KillFillTweens()
    {
        // The flash lives inside the fill sequence: read its state before the parent kills it, and restore the color it was left with.
        bool flashWasActive = flashTween != null && flashTween.IsActive();
        if (fillTween != null) { fillTween.Kill(); fillTween = null; }
        if (flashTween != null)
        {
            flashTween.Kill();
            flashTween = null;
            if (flashWasActive && fillGraphic != null) fillGraphic.color = fillBaseColor;
        }
    }

    private void KillPunch()
    {
        if (punchTween == null) return;
        bool wasActive = punchTween.IsActive();
        punchTween.Kill();
        punchTween = null;
        var target = PunchTransform();
        if (wasActive && target != null) target.localScale = punchBaseScale;
    }

    private void KillTweens()
    {
        hasDeferredExperience = false;
        KillFillTweens();
        KillPunch();
        // Keep the displayed baseline across disable; enable rebuilds deferred data from the current model.
    }
}
