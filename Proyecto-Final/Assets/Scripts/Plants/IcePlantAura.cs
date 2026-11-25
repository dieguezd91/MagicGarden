using System.Collections.Generic;
using UnityEngine;

public class IceAuraPlant : Plant
{
    [Header("ICE AURA CONFIGURATION")]
    [SerializeField] private float radius = 2f;
    [SerializeField] private float scanInterval = 0.15f;
    [SerializeField] private LayerMask enemyLayer;

    [Header("GROWTH / DEBUG")]
    [SerializeField] private bool startFullyGrown = false;

    [Header("SLOW")]
    [SerializeField, Range(0f, 1f)]
    private float slowAmount = 0.3f;

    [Header("DAMAGE")]
    [SerializeField] private float damage = 3f;

    [Header("Sound")]
    [Tooltip("Each damage tick plays the hit sound on the enemy. Minimum seconds between any two hit sounds, shared by every Ice Thorn, so a crowd does not turn into noise.")]
    [SerializeField] private float minHitSoundSpacing = 0.08f;
    private static float lastHitSoundTime = float.MinValue;
    [Tooltip("The blizzard loop keeps playing this long after the last enemy leaves the aura, then fades out.")]
    [SerializeField] private float loopReleaseDelay = 0.6f;
    private float lastEnemyInAuraTime = float.MinValue;
    [SerializeField] private float damageInterval = 1f;

    [Header("VISUAL")]
    [SerializeField] private GameObject auraVisual;
    private GameObject auraVisualInstance;
    private ParticleSystem auraParticleSystem;

    [Header("VFX & Feedback")]
    [SerializeField] private GameObject floatingDamagePrefab;

    private float scanTimer;
    private LifeController myLifeController;

    private readonly HashSet<EnemyBase> affectedEnemies = new();
    private readonly HashSet<EnemyBase> currentEnemies = new();
    private readonly List<EnemyBase> enemiesToRemove = new();

    private readonly Dictionary<EnemyBase, float> nextDamageTimes = new();

    public override MagicGarden.Target DescribeUpgradeTarget()
    {
        var target = base.DescribeUpgradeTarget();

        target.Capability = "IceAuraPlant";

        target.Bases[MagicGarden.Stat.Area] = radius;
        target.Bases[MagicGarden.Stat.Damage] = damage;
        target.Bases[MagicGarden.Stat.AttackSpeed] =
            1f / Mathf.Max(0.001f, damageInterval);

        return target;
    }

    protected override void Start()
    {
        base.Start();

        if (DayCycleController.Instance != null)
        {
            plantingDay = DayCycleController.Instance.CurrentDay;
        }

        if (startFullyGrown && !IsFullyGrown())
        {
            OnNewDay(plantingDay + daysToGrow + 1);
        }

        myLifeController = GetComponent<LifeController>();

        if (myLifeController != null)
        {
            myLifeController.maxHealth = 100f;
            myLifeController.currentHealth = myLifeController.maxHealth;
        }

        SetupAuraVisual();
    }

    private void SetupAuraVisual()
    {
        if (auraVisual == null || !auraVisual.scene.IsValid())
        {
            Transform child = transform.Find("SnowStorm");
            if (child != null)
                auraVisual = child.gameObject;
        }

        if (auraVisual == null)
            return;

        auraVisualInstance = auraVisual;
        auraParticleSystem = auraVisualInstance.GetComponentInChildren<ParticleSystem>(true);
        auraVisualInstance.SetActive(false);
    }

    protected override void Update()
    {
        if (UpgradeRuntime.GameplayBlocked)
            return;

        base.Update();

        if (myLifeController != null && !myLifeController.IsAlive())
        {
            ClearAffectedEnemies();
            return;
        }

        if (!IsFullyGrown())
        {
            if (affectedEnemies.Count > 0)
                ClearAffectedEnemies();
            return;
        }

        scanTimer += Time.deltaTime;

        if (scanTimer < scanInterval)
            return;

        scanTimer = 0f;

        DetectEnemies();
    }

    private void DetectEnemies()
    {
        currentEnemies.Clear();

        float currentRadius = Upgraded(
            MagicGarden.Stat.Area,
            radius
        );

        Collider2D[] enemies = Physics2D.OverlapCircleAll(
            transform.position,
            currentRadius,
            enemyLayer
        );

        foreach (Collider2D collider in enemies)
        {
            if (collider == null)
                continue;

            EnemyBase enemy = collider.GetComponentInParent<EnemyBase>();

            if (enemy == null || enemy.IsDead)
                continue;

            if (!currentEnemies.Add(enemy))
                continue;

            ApplyAuraEffect(enemy);
        }

        RemoveEnemiesOutsideAura();
        UpdateAuraVisualState();
    }

    private void ApplyAuraEffect(EnemyBase enemy)
    {
        enemy.ApplySlow(slowAmount);
        affectedEnemies.Add(enemy);

        if (!nextDamageTimes.TryGetValue(enemy, out float nextDamageTime) ||
            Time.time >= nextDamageTime)
        {
            LifeController lifeController =
                enemy.GetComponent<LifeController>();

            if (lifeController == null)
                lifeController = enemy.GetComponentInChildren<LifeController>();

            if (lifeController != null && lifeController.IsAlive())
            {
                float currentDamage = Upgraded(
                    MagicGarden.Stat.Damage,
                    damage
                );

                lifeController.TakeDamage(
                    currentDamage,
                    LifeController.DamageType.DamageOverTime,
                    LifeController.DamageElement.Ice
                );

                ShowDamageText(enemy.transform, currentDamage);

                if (Time.time - lastHitSoundTime >= minHitSoundSpacing || Time.time < lastHitSoundTime)
                {
                    lastHitSoundTime = Time.time;
                    if (SoundBase != null)
                        SoundBase.PlaySound(PlantSoundType.Hit, SoundSourceType.Localized, enemy.transform);
                }
            }

            float currentDamageInterval = UpgradedCooldown(damageInterval);

            nextDamageTimes[enemy] = Time.time +
                Mathf.Max(0.05f, currentDamageInterval);
        }
    }

    private void RemoveEnemiesOutsideAura()
    {
        enemiesToRemove.Clear();

        foreach (EnemyBase enemy in affectedEnemies)
        {
            if (enemy == null || !currentEnemies.Contains(enemy))
                enemiesToRemove.Add(enemy);
        }

        foreach (EnemyBase enemy in enemiesToRemove)
        {
            if (enemy != null)
                enemy.RemoveSlow();

            affectedEnemies.Remove(enemy);
            nextDamageTimes.Remove(enemy);
        }
    }

    private void ClearAffectedEnemies()
    {
        foreach (EnemyBase enemy in affectedEnemies)
        {
            if (enemy != null)
                enemy.RemoveSlow();
        }

        affectedEnemies.Clear();
        currentEnemies.Clear();
        nextDamageTimes.Clear();

        UpdateAuraVisualState();
    }

    private void UpdateAuraVisualState()
    {
        bool isAlive = (myLifeController == null || myLifeController.IsAlive());
        bool hasEnemies = isAlive && affectedEnemies.Count > 0;

        if (animator != null)
        {
            animator.SetBool("Attack", hasEnemies);
        }

        if (SoundBase != null)
        {
            if (hasEnemies)
            {
                lastEnemyInAuraTime = Time.time;
                SoundBase.PlayLoop();
            }
            else if (!isAlive || Time.time - lastEnemyInAuraTime >= loopReleaseDelay)
            {
                SoundBase.StopLoop();
            }
        }

        if (auraVisualInstance != null)
        {
            if (hasEnemies)
            {
                if (!auraVisualInstance.activeSelf)
                    auraVisualInstance.SetActive(true);

                if (auraParticleSystem != null && !auraParticleSystem.isPlaying)
                    auraParticleSystem.Play(true);
            }
            else
            {
                if (auraParticleSystem != null && auraParticleSystem.isPlaying)
                    auraParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmitting);

                if (auraVisualInstance.activeSelf)
                    auraVisualInstance.SetActive(false);
            }
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

    private void OnDisable()
    {
        ClearAffectedEnemies();

        if (animator != null)
        {
            animator.SetBool("Attack", false);
        }

        if (auraVisualInstance != null)
        {
            auraVisualInstance.SetActive(false);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.5f);

        Gizmos.DrawWireSphere(
            transform.position,
            radius
        );
    }
}