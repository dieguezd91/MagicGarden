using System.Collections;
using UnityEngine;

public class PoisonEffect : MonoBehaviour
{
    private LifeController lifeController;

    private Coroutine poisonRoutine;
    private float totalDuration;
    private float tickInterval;
    private float damagePerTick;
    private SoundClipData tickSound;

    private bool isPoisoned = false;

    private void Awake()
    {
        lifeController = GetComponent<LifeController>();
    }

    public void ApplyPoison(float duration, float tickTime, float damage, SoundClipData tickSound = null)
    {
        this.tickSound = tickSound;
        totalDuration = duration;
        tickInterval = tickTime;
        damagePerTick = damage;

        if (poisonRoutine != null)
        {
            StopCoroutine(poisonRoutine);
        }

        poisonRoutine = StartCoroutine(PoisonRoutine());
    }

    private IEnumerator PoisonRoutine()
    {
        isPoisoned = true;
        float elapsed = 0f;

        while (elapsed < totalDuration)
        {
            if (lifeController != null)
            {
                lifeController.TakeDamage(damagePerTick, LifeController.DamageType.DamageOverTime);
                // Cooldown is per SoundClipData, which every poisoned enemy shares, so a crowd doesn't stack ticks.
                if (tickSound != null && tickSound.CanPlay() && SoundManager.Instance != null)
                {
                    SoundManager.Instance.PlayClip(tickSound, SoundSourceType.Localized, transform);
                    tickSound.SetLastPlayTime();
                }
            }

            yield return new WaitForSeconds(tickInterval);
            elapsed += tickInterval;
        }

        isPoisoned = false;
        poisonRoutine = null;

        //Destroy(this);
    }

    public bool IsPoisoned() => isPoisoned;
}