using UnityEngine;

// World-space "level up" feedback shown above the player before the unlock/upgrade modals open.
// Runs on unscaled time; several levels gained at once restart the same animation instead of stacking.
public sealed class LevelUpIndicator : MonoBehaviour
{
    [SerializeField] private PlayerExperienceSystem experience;
    [SerializeField] private SpriteRenderer icon;
    [Header("Timing (seconds, unscaled)")]
    [SerializeField] private float popDuration = 0.2f;
    [SerializeField] private float riseDuration = 0.4f;
    [SerializeField] private float holdDuration = 0.35f;
    [SerializeField] private float fadeDuration = 0.25f;
    [Header("Motion")]
    [SerializeField] private float riseDistance = 0.6f;
    [SerializeField] private float popOvershoot = 1.25f; // peak scale of the pop, relative to the resting scale
    [Header("Hold effect")]
    [SerializeField] private float bobAmplitude = 0.05f; // world units
    [SerializeField] private float bobFrequency = 2.5f;  // cycles per second
    [SerializeField] private float pulseAmount = 0.08f;  // relative scale swing

    private float elapsed;
    private Vector3 restPosition;
    private Vector3 restScale = Vector3.one;
    private bool restCaptured;

    public bool IsPlaying { get; private set; }
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

    private void HandleLevelUp(int level) => Play();

    // Restarts when already playing so a burst of levels shows a single indicator.
    public void Play()
    {
        CaptureRest();
        elapsed = 0f;
        IsPlaying = true;
        Apply(0f);
    }

    public void Stop()
    {
        IsPlaying = false;
        elapsed = 0f;
        if (restCaptured) ApplyHidden();
    }

    private void Update() => Tick(Time.unscaledDeltaTime);

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
        float scale = PopScale(popT, popOvershoot) * (1f + pulseAmount * wave * live * (popT >= 1f ? 1f : 0f));
        float riseT = climb > 0f ? Mathf.Clamp01(time / climb) : 1f;
        float rise = 1f - (1f - riseT) * (1f - riseT);
        float alpha = time <= fadeStart || fadeDuration <= 0f ? 1f : 1f - Mathf.Clamp01((time - fadeStart) / fadeDuration);

        transform.localPosition = restPosition + Vector3.up * (riseDistance * rise + bobAmplitude * wave * live * (popT >= 1f ? 1f : 0f));
        transform.localScale = restScale * scale;
        var color = icon.color; color.a = alpha; icon.color = color;
        icon.enabled = true;
    }

    private void ApplyHidden()
    {
        transform.localPosition = restPosition;
        transform.localScale = restScale;
        if (icon == null) return;
        var color = icon.color; color.a = 0f; icon.color = color;
        icon.enabled = false;
    }

    // 0 -> peak with an ease-out, then settles back to 1: a visible overshoot (EaseOutBack alone only overshoots a few percent).
    private static float PopScale(float t, float peak)
    {
        const float rise = 0.6f;
        if (t <= rise) { float u = t / rise; return peak * (1f - (1f - u) * (1f - u) * (1f - u)); }
        float settle = Mathf.SmoothStep(0f, 1f, (t - rise) / (1f - rise));
        return Mathf.Lerp(peak, 1f, settle);
    }
}
