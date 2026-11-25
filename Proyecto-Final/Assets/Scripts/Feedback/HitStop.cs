using UnityEngine;

// Brief slow-motion "freeze" on impact. Static entry point: HitStop.Request(duration).
// Safety rules (they keep pause and modal flows intact):
// - never runs when the game is paused or timeScale is 0, and never uses 0 itself;
// - runs on unscaled time and restores exactly the timeScale it replaced;
// - if something else (PauseController, UpgradeRuntime) changes timeScale during a stop, that value is left alone;
// - a minimum interval after each stop prevents rapid hits (e.g. damage ticks) from chaining stops.
public class HitStop : MonoBehaviour
{
    public const float DefaultTimeScale = 0.05f;
    // Real seconds that must pass after a stop ends before another one may start.
    public const float MinInterval = 0.08f;
    public const float MaxDuration = 0.5f;
    private const float MinTimeScale = 0.01f;
    private const float MaxTimeScale = 0.99f;

    private static HitStop instance;

    public static bool IsActive => instance != null && instance.active;

    private bool active;
    private bool pendingHeal;
    private float normalScale = 1f;
    private float restoreScale = 1f;
    private float appliedScale;
    private float endTime;
    private float lastEndTime = float.NegativeInfinity;

    // duration <= 0 disables the effect. timeScale is clamped so the game never freezes completely.
    public static void Request(float duration, float timeScale = DefaultTimeScale)
    {
        if (duration <= 0f || !Application.isPlaying || Time.timeScale <= 0f) return;

        if (instance == null)
        {
            var go = new GameObject("HitStop") { hideFlags = HideFlags.HideInHierarchy };
            DontDestroyOnLoad(go);
            instance = go.AddComponent<HitStop>();
            instance.normalScale = Time.timeScale;
        }
        instance.Begin(Mathf.Min(duration, MaxDuration), Mathf.Clamp(timeScale, MinTimeScale, MaxTimeScale));
    }

    private void Begin(float duration, float scale)
    {
        float now = Time.unscaledTime;

        if (active)
        {
            // A stronger hit extends the current stop; it never starts a new one, so stops cannot chain.
            if (Mathf.Approximately(Time.timeScale, appliedScale)) endTime = Mathf.Max(endTime, now + duration);
            return;
        }

        float current = Time.timeScale;
        if (pendingHeal) return;
        if (Mathf.Abs(current - normalScale) > 0.0001f) return; // another system already changed the time scale
        if (current <= scale) return;
        if (now - lastEndTime < MinInterval) return;

        active = true;
        restoreScale = current;
        appliedScale = scale;
        endTime = now + duration;
        Time.timeScale = scale;
    }

    private void Update()
    {
        float current = Time.timeScale;

        if (active)
        {
            if (!Mathf.Approximately(current, appliedScale))
            {
                // Someone else took over timeScale: leave their value alone.
                active = false;
                lastEndTime = Time.unscaledTime;
                // A pause that captured our slowed value would restore it afterwards: undo that once it resumes.
                pendingHeal = current <= 0f;
                return;
            }
            if (Time.unscaledTime >= endTime) EndStop();
        }
        else if (pendingHeal)
        {
            if (current > 0f)
            {
                if (Mathf.Approximately(current, appliedScale)) Time.timeScale = restoreScale;
                pendingHeal = false;
                normalScale = Time.timeScale;
            }
        }
        else if (current > 0f)
        {
            normalScale = current;
        }
    }

    private void EndStop()
    {
        if (!active) return;
        active = false;
        lastEndTime = Time.unscaledTime;
        if (Mathf.Approximately(Time.timeScale, appliedScale)) Time.timeScale = restoreScale;
    }

    private void OnDisable() => EndStop();

    private void OnDestroy()
    {
        EndStop();
        if (instance == this) instance = null;
    }
}
