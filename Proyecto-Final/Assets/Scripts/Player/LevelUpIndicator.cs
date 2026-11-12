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
    [SerializeField] private float popOvershoot = 1.25f;

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
        float scale = EaseOutBack(popT, popOvershoot);
        float riseT = climb > 0f ? Mathf.Clamp01(time / climb) : 1f;
        float rise = 1f - (1f - riseT) * (1f - riseT);
        float alpha = time <= fadeStart || fadeDuration <= 0f ? 1f : 1f - Mathf.Clamp01((time - fadeStart) / fadeDuration);

        transform.localPosition = restPosition + Vector3.up * (riseDistance * rise);
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

    private static float EaseOutBack(float t, float overshoot)
    {
        float c1 = Mathf.Max(0f, overshoot - 1f) * 4f;
        float c3 = c1 + 1f;
        float u = t - 1f;
        return 1f + c3 * u * u * u + c1 * u * u;
    }
}
