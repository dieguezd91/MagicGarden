using UnityEngine;

// World-space "level up" feedback shown above the player before the unlock/upgrade modals open.
// Runs on unscaled time; several levels gained at once restart the same animation instead of stacking.
public sealed class LevelUpIndicator : MonoBehaviour
{
    [SerializeField] private PlayerExperienceSystem experience;
    [SerializeField] private SpriteRenderer icon;
    [Tooltip("SoundManager sound played once per level-up burst. Empty disables the sound.")]
    [SerializeField] private string levelUpSound = "Level_Up";
    [Header("Timing (seconds, unscaled)")]
    [Tooltip("Length of the pop at the start (squash, peak and settle all happen inside it).")]
    [SerializeField] private float popDuration = 0.25f;
    [Tooltip("Seconds the icon keeps rising after the pop.")]
    [SerializeField] private float riseDuration = 0.4f;
    [Tooltip("Seconds the icon stays at full opacity (bobbing and breathing) after rising.")]
    [SerializeField] private float holdDuration = 0.9f;
    [Tooltip("Seconds of the final fade out.")]
    [SerializeField] private float fadeDuration = 0.35f;
    [Header("Motion")]
    [Tooltip("World units the icon rises above its resting position.")]
    [SerializeField] private float riseDistance = 0.6f;
    [Tooltip("Peak scale of the pop, relative to the resting scale.")]
    [SerializeField] private float popOvershoot = 1.25f;
    [Header("Hold effect")]
    [Tooltip("Vertical bob while holding, in world units.")]
    [SerializeField] private float bobAmplitude = 0.05f;
    [Tooltip("Bob and breathe cycles per second.")]
    [SerializeField] private float bobFrequency = 2.5f;
    [Tooltip("Relative scale swing of the breathing while holding.")]
    [SerializeField] private float pulseAmount = 0.08f;
    [Header("Squash & stretch (inside the pop)")]
    [Tooltip("Fraction of the pop at which the icon reaches its peak size and lands (squashed). The ring starts here too.")]
    [Range(0.1f, 0.9f)] [SerializeField] private float popPeakFraction = 0.6f;
    [Tooltip("How much shorter than wide the icon gets when it lands (0.2 = 20% less height).")]
    [Range(0f, 0.6f)] [SerializeField] private float squashAmount = 0.2f;
    [Tooltip("How much taller than wide the icon gets right after landing, before it settles (0.15 = 15% more height).")]
    [Range(0f, 0.6f)] [SerializeField] private float stretchAmount = 0.15f;
    [Tooltip("Fraction of the pop at which the stretch is at its maximum. Must be after the peak; the icon is at rest scale at 1.")]
    [Range(0.2f, 0.98f)] [SerializeField] private float stretchPointFraction = 0.85f;
    [Header("Peak ring")]
    [Tooltip("Optional sprite renderer (child of this object) used as the ring that expands when the icon reaches the pop peak. Empty = no ring.")]
    [SerializeField] private SpriteRenderer ring;
    [Tooltip("Seconds (unscaled) the ring takes to expand and fade out.")]
    [SerializeField] private float ringDuration = 0.5f;
    [Tooltip("Ring scale (world, relative to the sprite size) when it appears.")]
    [SerializeField] private float ringStartScale = 3f;
    [Tooltip("Ring scale (world, relative to the sprite size) when it has fully faded.")]
    [SerializeField] private float ringEndScale = 8f;
    [Tooltip("Ring color; its alpha is the opacity when the ring appears (it fades to 0).")]
    [SerializeField] private Color ringColor = new Color(1f, 0.92f, 0.55f, 0.9f);

    private bool queuedForGold;
    private float elapsed;
    private Vector3 restPosition;
    private Vector3 restScale = Vector3.one;
    private bool restCaptured;

    public bool IsPlaying { get; private set; }
    public bool HasPendingPresentation => isActiveAndEnabled && (queuedForGold || IsPlaying);
    private bool GoldFeedbackPlaying => FloatingTextController.Instance != null && FloatingTextController.Instance.IsShowingGoldFeedback;
    public float Duration => Mathf.Max(0.01f, popDuration) + Mathf.Max(0f, riseDuration) + Mathf.Max(0f, holdDuration) + Mathf.Max(0f, fadeDuration);

    private void Awake()
    {
        CaptureRest();
        if (experience == null) experience = GetComponentInParent<PlayerExperienceSystem>();
        ApplyHidden();
    }

    private void OnEnable()
    {
        if (experience == null) experience = GetComponentInParent<PlayerExperienceSystem>();
        if (experience == null) return;
        experience.OnLevelUp += HandleLevelUp;
        experience.OnProgressionReset += Stop;
    }

    private void OnDisable()
    {
        if (experience != null)
        {
            experience.OnLevelUp -= HandleLevelUp;
            experience.OnProgressionReset -= Stop;
        }
        Stop();
    }

    // One jingle per burst: several levels gained at once restart the icon but do not stack the sound.
    private void HandleLevelUp(int level)
    {
        if (Application.isPlaying && GoldFeedbackPlaying)
        {
            Stop();
            queuedForGold = true;
            return;
        }
        StartFeedback();
    }

    private void StartFeedback()
    {
        if (!IsPlaying && !string.IsNullOrEmpty(levelUpSound)) SoundManager.Instance?.PlayOneShot(levelUpSound);
        Play();
    }

    // Restarts when already playing so a burst of levels shows a single indicator.
    public void Play()
    {
        queuedForGold = false;
        CaptureRest();
        elapsed = 0f;
        IsPlaying = true;
        Apply(0f);
    }

    public void Stop()
    {
        queuedForGold = false;
        IsPlaying = false;
        elapsed = 0f;
        if (restCaptured) ApplyHidden();
    }

    private void Update()
    {
        if (queuedForGold)
        {
            if (GoldFeedbackPlaying) return;
            queuedForGold = false;
            StartFeedback();
        }
        Tick(Time.unscaledDeltaTime);
    }

    public void Tick(float deltaTime)
    {
        if (!IsPlaying) return;
        elapsed += Mathf.Max(0f, deltaTime);
        if (elapsed >= Duration) { Stop(); return; }
        Apply(elapsed);
    }

    private void CaptureRest()
    {
        if (restCaptured) return;
        restPosition = transform.localPosition;
        restScale = transform.localScale;
        restCaptured = true;
    }

    private void Apply(float time)
    {
        if (icon == null) return;
        float pop = Mathf.Max(0.01f, popDuration);
        float climb = pop + Mathf.Max(0f, riseDuration);
        float fadeStart = climb + Mathf.Max(0f, holdDuration);

        float popT = Mathf.Clamp01(time / pop);
        // After the pop the icon keeps bobbing and breathing until it fades, so it reads as alive rather than a static label.
        float wave = Mathf.Sin(Mathf.Max(0f, time - pop) * Mathf.PI * 2f * Mathf.Max(0f, bobFrequency));
        float live = 1f - Mathf.Clamp01((time - fadeStart) / Mathf.Max(0.01f, fadeDuration));
        float peak = Mathf.Clamp(popPeakFraction, 0.1f, 0.9f);
        float scale = PopScale(popT, popOvershoot, peak) * (1f + pulseAmount * wave * live * (popT >= 1f ? 1f : 0f));
        float scaleY = scale * SquashStretch(popT, peak);
        float riseT = climb > 0f ? Mathf.Clamp01(time / climb) : 1f;
        float rise = 1f - (1f - riseT) * (1f - riseT);
        float alpha = time <= fadeStart || fadeDuration <= 0f ? 1f : 1f - Mathf.Clamp01((time - fadeStart) / fadeDuration);

        transform.localPosition = restPosition + Vector3.up * (riseDistance * rise + bobAmplitude * wave * live * (popT >= 1f ? 1f : 0f));
        transform.localScale = new Vector3(restScale.x * scale, restScale.y * scaleY, restScale.z * scale);
        var color = icon.color; color.a = alpha; icon.color = color;
        icon.enabled = true;
        ApplyRing(time - pop * peak);
    }

    // The ring expands and fades from the moment the pop peaks. It is a child, so it cancels the icon's (squashed) scale to stay round.
    private void ApplyRing(float ringTime)
    {
        if (ring == null) return;
        float length = Mathf.Max(0.01f, ringDuration);
        if (ringTime < 0f || ringTime >= length) { HideRing(); return; }
        float t = ringTime / length;
        float eased = 1f - (1f - t) * (1f - t);
        float size = Mathf.Lerp(ringStartScale, ringEndScale, eased);
        var parent = transform.localScale;
        ring.transform.localScale = new Vector3(SafeDivide(size, parent.x), SafeDivide(size, parent.y), 1f);
        var color = ringColor; color.a = ringColor.a * (1f - t);
        ring.color = color;
        ring.enabled = true;
    }

    private void HideRing()
    {
        if (ring == null) return;
        ring.enabled = false;
        var color = ringColor; color.a = 0f; ring.color = color;
        ring.transform.localScale = new Vector3(ringStartScale, ringStartScale, 1f);
    }

    private static float SafeDivide(float value, float divisor) => Mathf.Abs(divisor) < 0.0001f ? value : value / divisor;

    private void ApplyHidden()
    {
        transform.localPosition = restPosition;
        transform.localScale = restScale;
        HideRing();
        if (icon == null) return;
        var color = icon.color; color.a = 0f; icon.color = color;
        icon.enabled = false;
    }

    // 0 -> peak with an ease-out, then settles back to 1: a visible overshoot (EaseOutBack alone only overshoots a few percent).
    private static float PopScale(float t, float peak, float peakFraction)
    {
        if (t <= peakFraction) { float u = t / peakFraction; return peak * (1f - (1f - u) * (1f - u) * (1f - u)); }
        float settle = Mathf.SmoothStep(0f, 1f, (t - peakFraction) / (1f - peakFraction));
        return Mathf.Lerp(peak, 1f, settle);
    }

    // Height multiplier relative to the width: 1 -> squashed (wide and short) on landing at the peak -> stretched (tall and narrow) -> 1 at the end of the pop.
    private float SquashStretch(float t, float peakFraction)
    {
        float stretchAt = Mathf.Clamp(stretchPointFraction, peakFraction + 0.01f, 0.99f);
        float squashed = 1f - Mathf.Clamp(squashAmount, 0f, 0.9f);
        float stretched = 1f + Mathf.Max(0f, stretchAmount);
        if (t <= peakFraction) return Mathf.Lerp(1f, squashed, Mathf.SmoothStep(0f, 1f, t / peakFraction));
        if (t <= stretchAt) return Mathf.Lerp(squashed, stretched, Mathf.SmoothStep(0f, 1f, (t - peakFraction) / (stretchAt - peakFraction)));
        return Mathf.Lerp(stretched, 1f, Mathf.SmoothStep(0f, 1f, (t - stretchAt) / (1f - stretchAt)));
    }
}
