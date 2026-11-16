
using UnityEngine;

public class IceSlowEffect : MonoBehaviour
{
    private float slowMultiplier = 1f;
    private float slowEndTime;

    public float SpeedMultiplier
    {
        get
        {
            return Time.time < slowEndTime
                ? slowMultiplier
                : 1f;
        }
    }

    public bool IsSlowed => Time.time < slowEndTime;

    public void ApplySlow(float multiplier, float duration)
    {
        multiplier = Mathf.Clamp(multiplier, 0.1f, 1f);

        if (Time.time >= slowEndTime)
        {
            slowMultiplier = multiplier;
        }
        else
        {
            slowMultiplier = Mathf.Min(
                slowMultiplier,
                multiplier
            );
        }

        slowEndTime = Mathf.Max(
            slowEndTime,
            Time.time + duration
        );
    }
}
