
using System.Collections;
using UnityEngine;

public class ElectricPlantAura : Plant
{
    [Header("Area Settings")]
    [SerializeField] private float radius = 2.5f;
    [SerializeField] private float damagePerSecond = 2f;
    [SerializeField] private float areaCheckInterval = 0.1f;
    [Header("Plant Health")]
    [SerializeField] private LifeController plantLife;

    [SerializeField] private LayerMask enemyLayer;

    [Header("VFX & Feedback")]
    [SerializeField] private GameObject floatingDamagePrefab;

    [Header("Sound")]
    [Tooltip("The hum keeps going this long after the last enemy is hit, so it does not flicker between damage ticks.")]
    [SerializeField] private float humReleaseSeconds = 0.5f;
    [Tooltip("Each enemy in the aura plays the hit sound on contact and then again every this many seconds while it stays inside.")]
    [SerializeField] private float enemyHitSoundInterval = 0.45f;
    [Tooltip("Minimum seconds between any two hit sounds, shared by every orchid, so a crowd does not turn into noise.")]
    [SerializeField] private float minHitSoundSpacing = 0.08f;
    private static float lastHitSoundTime = float.MinValue;
    private readonly System.Collections.Generic.Dictionary<LifeController, float> nextEnemyHitSound = new();
    private readonly System.Collections.Generic.List<LifeController> staleEnemies = new();
    private bool attacking;
    private float lastHitTime = float.MinValue;

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
        attacking = false;
    }

    private IEnumerator AreaRoutine()
    {
        while (true)
        {
            ApplyAreaEffects();

            yield return new WaitForSeconds(areaCheckInterval);
        }
    }

    private void ApplyAreaEffects()
    {

        if (!IsFullyGrown() || UpgradeRuntime.GameplayBlocked)
        {
            SetAttackAnimation(false);
            UpdateAttackSound(false);
            return;
        }

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

            enemyLife.TakeDamage(damage, LifeController.DamageType.DamageOverTime);
            hitAny = true;

            ShowDamageText(enemyLife.transform, damage);
            PlayEnemyHitSound(enemyLife);
        }

        SetAttackAnimation(hitAny);
        UpdateAttackSound(hitAny);
        ForgetEnemiesOutOfRange();
    }

    private void PlayEnemyHitSound(LifeController enemy)
    {
        float now = Time.time;
        if (nextEnemyHitSound.TryGetValue(enemy, out float next) && now < next)
            return;
        if (now - lastHitSoundTime < minHitSoundSpacing && now >= lastHitSoundTime)
            return;

        nextEnemyHitSound[enemy] = now + enemyHitSoundInterval;
        lastHitSoundTime = now;
        if (SoundBase != null)
            SoundBase.PlaySound(PlantSoundType.Hit, SoundSourceType.Localized, enemy.transform);
    }

    private void ForgetEnemiesOutOfRange()
    {
        if (nextEnemyHitSound.Count == 0) return;
        float range = Upgraded(MagicGarden.Stat.Area, radius) + 0.5f;
        staleEnemies.Clear();
        foreach (var pair in nextEnemyHitSound)
        {
            if (pair.Key == null || !pair.Key.IsAlive() ||
                (pair.Key.transform.position - transform.position).sqrMagnitude > range * range)
                staleEnemies.Add(pair.Key);
        }
        foreach (var enemy in staleEnemies)
            nextEnemyHitSound.Remove(enemy);
    }

    private void UpdateAttackSound(bool hitAny)
    {
        bool alive = plantLife == null || plantLife.IsAlive();
        if (hitAny && alive)
        {
            lastHitTime = Time.time;
            attacking = true;
            if (SoundBase != null) SoundBase.PlayLoop();
            return;
        }

        if (attacking && (!alive || Time.time - lastHitTime >= humReleaseSeconds))
        {
            attacking = false;
            if (SoundBase != null) SoundBase.StopLoop();
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


    private void SetAttackAnimation(bool isAttacking)
    {
        if (animator == null)
            return;

        if (plantLife == null)
            plantLife = GetComponent<LifeController>();

        bool plantIsAlive = plantLife != null && plantLife.IsAlive();

        animator.SetBool("Attack", isAttacking && plantIsAlive);
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
