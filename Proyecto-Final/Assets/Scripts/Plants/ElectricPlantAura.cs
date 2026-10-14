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

    public void Describe(MagicGarden.Target target)
    {
        target.Capability = "ElectricPlantAura";
        target.Bases[MagicGarden.Stat.Damage] = damagePerSecond;
        target.Bases[MagicGarden.Stat.Area] = radius;
    }
    private Plant plant;
    private LifeController lifeController;

    private void Awake()
    {
        lifeController = GetComponent<LifeController>();
        plant = GetComponent<Plant>();
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
            ApplyAreaEffects();

            yield return new WaitForSeconds(areaCheckInterval);
        }
    }

    private void ApplyAreaEffects()
    {
        if (UpgradeRuntime.GameplayBlocked) return;
        Collider2D[] hits = Physics2D.OverlapCircleAll(
            transform.position,
            plant != null ? plant.Upgraded(MagicGarden.Stat.Area, radius) : radius,
            enemyLayer
        );

        float damage = (plant != null ? plant.Upgraded(MagicGarden.Stat.Damage, damagePerSecond) : damagePerSecond) * areaCheckInterval;

        foreach (var hit in hits)
        {
            LifeController enemyLife = hit.GetComponent<LifeController>();

            if (enemyLife == null || !enemyLife.IsAlive())
                continue;

            enemyLife.TakeDamage(damage);

            ShowDamageText(enemyLife.transform, damage);
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
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}