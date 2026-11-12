using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StormRoseReactiveAura : MonoBehaviour
{
    [Header("Area Settings")]
    [SerializeField] private float radius = 2.5f;
    [SerializeField] private float initialDamage = 2f;
    [SerializeField] private float cooldown = 3f;
    [SerializeField] private float areaDuration = 2.5f;
    [SerializeField] private float areaCheckInterval = 0.1f;

    [Header("Poison (Damage Over Time)")]
    [SerializeField] private float poisonDamagePerTick = 1f;
    [SerializeField] private float poisonDuration = 3f;
    [SerializeField] private float poisonTickRate = 1f;

    [Header("Sound")]
    [SerializeField] private SoundClipData pulseSound;
    [SerializeField] private SoundClipData poisonTickSound;

    [SerializeField] private LayerMask enemyLayer;

    [Header("VFX & Feedback")]
    [SerializeField] private GameObject floatingDamagePrefab;


    public void Describe(MagicGarden.Target target)
    {
        target.Capability = "StormRoseReactiveAura";
        target.Bases[MagicGarden.Stat.Damage] = initialDamage;
        target.Bases[MagicGarden.Stat.Area] = radius;
        target.Bases[MagicGarden.Stat.AttackSpeed] = 1 / Mathf.Max(0.001f, cooldown);
    }
    private Plant plant;
    private LifeController lifeController;
    private float cooldownTimer = 0f;
    private float previousEffectiveCooldown;
    private bool areaActive = false;

    private HashSet<LifeController> damagedTargets =
        new HashSet<LifeController>();

    private void Awake()
    {
        lifeController = GetComponent<LifeController>();
        plant = GetComponent<Plant>();
    }

    private void OnEnable()
    {
        if (lifeController != null)
            lifeController.onDamaged.AddListener(OnDamaged);
    }

    private void OnDisable()
    {
        if (lifeController != null)
            lifeController.onDamaged.RemoveListener(OnDamaged);
    }

    private void Update()
    {
        float effective = plant != null ? plant.UpgradedCooldown(cooldown) : cooldown;
        if (previousEffectiveCooldown > 0 && previousEffectiveCooldown != effective)
            cooldownTimer *= effective / previousEffectiveCooldown;
        previousEffectiveCooldown = effective;
        if (cooldownTimer > 0f)
            cooldownTimer -= Time.deltaTime;
    }

    private void OnDamaged(float damage, LifeController.DamageType damageType)
    {
        if (UpgradeRuntime.GameplayBlocked) return;
        if (cooldownTimer > 0f || areaActive)
            return;

        StartCoroutine(AreaRoutine());
        SoundManager.Instance?.PlayClip(pulseSound, SoundSourceType.Localized, transform);
        cooldownTimer = plant != null ? plant.UpgradedCooldown(cooldown) : cooldown;
        previousEffectiveCooldown = cooldownTimer;
    }

    private IEnumerator AreaRoutine()
    {
        areaActive = true;
        damagedTargets.Clear();

        float elapsed = 0f;

        while (elapsed < areaDuration)
        {
            ApplyAreaEffects();
            elapsed += areaCheckInterval;
            Debug.Log("Area activada");
            yield return new WaitForSeconds(areaCheckInterval);
        }

        areaActive = false;
    }

    private void ApplyAreaEffects()
    {
        if (UpgradeRuntime.GameplayBlocked) return;
        Collider2D[] hits = Physics2D.OverlapCircleAll(
            transform.position,
            plant != null ? plant.Upgraded(MagicGarden.Stat.Area, radius) : radius,
            enemyLayer
        );

        foreach (var hit in hits)
        {
            LifeController enemyLife = hit.GetComponent<LifeController>();
            if (enemyLife == null || !enemyLife.IsAlive())
                continue;

            if (!damagedTargets.Contains(enemyLife))
            {
                float upgradedDamage = plant != null ? plant.Upgraded(MagicGarden.Stat.Damage, initialDamage) : initialDamage;
                enemyLife.TakeDamage(upgradedDamage);
                ShowDamageText(enemyLife.transform, upgradedDamage);

                damagedTargets.Add(enemyLife);
            }

            PoisonEffect poison = enemyLife.GetComponent<PoisonEffect>();

            if (poison == null)
            {
                poison = enemyLife.gameObject.AddComponent<PoisonEffect>();
            }

            poison.ApplyPoison(poisonDuration, poisonTickRate,
                plant != null ? plant.Upgraded(MagicGarden.Stat.Damage, poisonDamagePerTick) : poisonDamagePerTick, poisonTickSound);
        }
    }

    private void ShowDamageText(Transform target, float dmg)
    {
        if (floatingDamagePrefab == null) return;

        var existing = target.GetComponentInChildren<FloatingDamageText>();

        if (existing != null)
        {
            existing.AddDamage(dmg);
        }
        else
        {
            Vector3 spawnPos = target.position + Vector3.up * 1f;
            var go = Instantiate(floatingDamagePrefab, spawnPos, Quaternion.identity);
            var fdt = go.GetComponent<FloatingDamageText>();
            fdt?.Initialize(target);
            fdt?.AddDamage(dmg);
        }
    }


    private void OnDrawGizmosSelected()
    {
        Gizmos.color = areaActive
            ? new Color(0.2f, 1f, 0.4f, 0.8f)
            : new Color(0.4f, 0.8f, 1f, 0.4f);

        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
