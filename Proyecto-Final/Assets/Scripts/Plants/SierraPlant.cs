using UnityEngine;

public class SierraPlant : Plant
{
    [Header("DETECTION")]
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField] private float detectionRange = 6f;

    [Header("PROJECTILE")]
    [SerializeField] private SierraProjectile projectilePrefab;
    [SerializeField] private Transform firePoint;
    [SerializeField] private float projectileSpeed = 12f;
    [SerializeField] private float returnSpeed = 8f;

    [Header("ATTACK")]
    [SerializeField] private float attackDamagePerSecond = 5f;
    [SerializeField] private float attackDuration = 3f;
    [SerializeField] private float attackCooldown = 2f;

    [Header("ATTACHMENT")]
    [SerializeField] private string gripPointName = "PlantGripPoint";
    [SerializeField] private Vector3 fallbackAttachOffset = Vector3.zero;

    private SierraProjectile activeProjectile;

    private float cooldownTimer = 0f;
    private float previousEffectiveCooldown;

    public override MagicGarden.Target DescribeUpgradeTarget()
    {
        var target = base.DescribeUpgradeTarget();

        target.Bases[MagicGarden.Stat.Damage] = attackDamagePerSecond;
        target.Bases[MagicGarden.Stat.AttackSpeed] =
            1f / Mathf.Max(0.001f, attackCooldown);
        target.Bases[MagicGarden.Stat.Range] = detectionRange;

        return target;
    }

    protected override void Update()
    {
        if (UpgradeRuntime.GameplayBlocked)
            return;

        base.Update();

        if (!IsFullyGrown())
            return;

        float effectiveCooldown = UpgradedCooldown(attackCooldown);

        if (previousEffectiveCooldown > 0f &&
            !Mathf.Approximately(previousEffectiveCooldown, effectiveCooldown))
        {
            cooldownTimer *= effectiveCooldown / previousEffectiveCooldown;
        }

        previousEffectiveCooldown = effectiveCooldown;

        if (cooldownTimer > 0f)
        {
            cooldownTimer -= Time.deltaTime;
            return;
        }

        if (activeProjectile != null)
            return;

        DetectAndShootTarget();
    }

    private void DetectAndShootTarget()
    {
        float effectiveRange = Upgraded(
            MagicGarden.Stat.Range,
            detectionRange
        );

        Collider2D[] enemies = Physics2D.OverlapCircleAll(
            transform.position,
            effectiveRange,
            enemyLayer
        );

        float closestDistance = Mathf.Infinity;
        LifeController closestLife = null;

        foreach (Collider2D enemy in enemies)
        {
            if (enemy == null)
                continue;

            LifeController life = enemy.GetComponentInParent<LifeController>();

            if (life == null || !life.IsAlive())
                continue;

            float distance = Vector2.Distance(
                transform.position,
                life.transform.position
            );

            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestLife = life;
            }
        }

        if (closestLife == null || projectilePrefab == null)
            return;

        ShootAtTarget(closestLife);
    }

    private void ShootAtTarget(LifeController targetLife)
    {
        if (targetLife == null || !targetLife.IsAlive())
            return;

        Transform spawnTransform = firePoint != null
            ? firePoint
            : transform;

        Vector3 spawnPosition = spawnTransform.position;

        Vector2 direction = (
            (Vector2)targetLife.transform.position -
            (Vector2)spawnPosition
        ).normalized;

        SierraProjectile projectile = Instantiate(
            projectilePrefab,
            spawnPosition,
            Quaternion.identity
        );

        activeProjectile = projectile;

        float effectiveDamage = Upgraded(
            MagicGarden.Stat.Damage,
            attackDamagePerSecond
        );

        float effectiveRange = Upgraded(
            MagicGarden.Stat.Range,
            detectionRange
        );

        projectile.Initialize(
            this,
            spawnTransform,
            targetLife.transform,
            effectiveDamage,
            attackDuration,
            effectiveRange,
            projectileSpeed,
            returnSpeed,
            gripPointName,
            fallbackAttachOffset
        );

        projectile.Cast(direction, spawnPosition);
    }

    public void NotifyProjectileFinished(SierraProjectile projectile)
    {
        if (activeProjectile != projectile)
            return;

        activeProjectile = null;

        cooldownTimer = UpgradedCooldown(attackCooldown);
        previousEffectiveCooldown = cooldownTimer;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;

        Gizmos.DrawWireSphere(
            transform.position,
            detectionRange
        );
    }
}