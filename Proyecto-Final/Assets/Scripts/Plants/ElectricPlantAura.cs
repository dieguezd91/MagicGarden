
using System.Collections;
using UnityEngine;

public class ElectricPlantAura : Plant
{
    [Header("Area Settings")]
    [SerializeField] private float radius = 2.5f;
    [SerializeField] private float damagePerSecond = 2f;
    [SerializeField] private float areaCheckInterval = 0.1f;

    [SerializeField] private LayerMask enemyLayer;

    [Header("VFX & Feedback")]
    [SerializeField] private GameObject floatingDamagePrefab;

    [Header("Sound")]
    [Tooltip("Seconds between zap/hit sounds, shared by every orchid so several don't stack.")]
    [SerializeField] private float zapSoundInterval = 1.2f;
    private static float lastZapSoundTime = float.MinValue;

    public void Describe(MagicGarden.Target target)
    {
        target.Capability = "ElectricPlantAura";
        target.Bases[MagicGarden.Stat.Damage] = damagePerSecond;
        target.Bases[MagicGarden.Stat.Area] = radius;
    }

    private void OnEnable()
    {
        StartCoroutine(AreaRoutine());
    }

    private void OnDisable()
    {
        StopAllCoroutines();
    }

    private IEnumerator AreaRoutine()
    {
        while (true)
        {
            // Hum only once grown, matching when the plant is "on".
            if (IsFullyGrown() && SoundBase != null) SoundBase.PlayLoop();
            ApplyAreaEffects();

            yield return new WaitForSeconds(areaCheckInterval);
        }
    }

    private void ApplyAreaEffects()
    {
        if (UpgradeRuntime.GameplayBlocked)
            return;

        Collider2D[] hits = Physics2D.OverlapCircleAll(
            transform.position,
            Upgraded(MagicGarden.Stat.Area, radius),
            enemyLayer
        );

        float damage =
            Upgraded(MagicGarden.Stat.Damage, damagePerSecond) *
            areaCheckInterval;

        bool hitAny = false;
        foreach (var hit in hits)
        {
            LifeController enemyLife = hit.GetComponent<LifeController>();

            if (enemyLife == null || !enemyLife.IsAlive())
                continue;

            enemyLife.TakeDamage(damage);
            hitAny = true;

            ShowDamageText(enemyLife.transform, damage);
        }

        // Damage ticks every areaCheckInterval; sound only once per zapSoundInterval across all orchids.
        // Time.time < last covers a stale static after an editor play session without domain reload.
        if (hitAny && (Time.time - lastZapSoundTime >= zapSoundInterval || Time.time < lastZapSoundTime))
        {
            lastZapSoundTime = Time.time;
            PlaySound(Random.value < 0.6f ? PlantSoundType.Attack : PlantSoundType.Hit);
        }
    }

    private void ShowDamageText(Transform target, float dmg)
    {
        if (floatingDamagePrefab == null)
            return;

        var existing = target.GetComponentInChildren<FloatingDamageText>();

        if (existing != null)
        {
            existing.AddDamage(dmg);
        }
        else
        {
            Vector3 spawnPos = target.position + Vector3.up * 1f;

            var go = Instantiate(
                floatingDamagePrefab,
                spawnPos,
                Quaternion.identity
            );

            var fdt = go.GetComponent<FloatingDamageText>();

            fdt?.Initialize(target);
            fdt?.AddDamage(dmg);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.6f);

        Gizmos.DrawWireSphere(
            transform.position,
            radius
        );
    }
}
