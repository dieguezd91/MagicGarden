using UnityEngine;

public class AttackPlant : Plant
{
    [Header("Attack Configuration")]
    public GameObject projectile;
    public Transform firePoint;
    public float cooldown = 2f;
    public float detectionRange = 8f;
    public LayerMask enemyLayer;

    public override MagicGarden.Target DescribeUpgradeTarget()
    {
        var target = base.DescribeUpgradeTarget();
        var spell = projectile != null ? projectile.GetComponent<Spell>() : null;
        if (spell == null) { target.Bases.Clear(); target.Capability = "Plant"; return target; }
        var projectileTarget = spell.DescribeUpgradeTarget(UpgradeId, target.Name);
        foreach (var value in projectileTarget.Bases) target.Bases[value.Key] = value.Value;
        target.Capability = "AttackPlant";
        target.Bases[MagicGarden.Stat.AttackSpeed] = 1 / Mathf.Max(0.001f, cooldown);
        target.Bases[MagicGarden.Stat.Range] = detectionRange;
        return target;
    }
    private float attackTimer = 0f;
    private bool canShoot = false;
    private Transform target;

    private bool isPerformingAttack = false;
    private Transform queuedTarget;

    private SpriteRenderer spriteRenderer;
    private Vector3 originalFirePointLocalPos;
    private bool isFlipped = false;
    private bool spriteFacesLeft = false;

    protected override void Start()
    {
        base.Start();

        spriteRenderer = GetComponent<SpriteRenderer>();

        if (plantData != null)
        {
            spriteFacesLeft = plantData.spriteFacesLeft;
        }

        if (firePoint == null)
        {
            GameObject point = new GameObject("FirePoint");
            point.transform.parent = transform;
            point.transform.localPosition = new Vector3(0, 0.5f, 0);
            firePoint = point.transform;
        }

        originalFirePointLocalPos = firePoint.localPosition;

        LifeController lifeController = GetComponent<LifeController>();
        if (lifeController != null)
        {
            lifeController.maxHealth = 100f;
            lifeController.currentHealth = lifeController.maxHealth;
        }
    }

    protected override void Update()
    {
        if (UpgradeRuntime.GameplayBlocked) return;
        base.Update();
        canShoot = IsFullyGrown();

        if (canShoot && !isPerformingAttack)
        {
            DetectEnemies();

            if (target != null)
            {
                attackTimer += Time.deltaTime;
                if (attackTimer >= UpgradedCooldown(cooldown))
                {
                    StartAttackSequence();
                    attackTimer = 0f;
                }

                Vector3 direction = target.position - transform.position;

                UpdateSpriteFlipAndFirePoint(direction);

                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
                firePoint.rotation = Quaternion.Euler(0, 0, angle);
            }
        }
    }

    private void UpdateSpriteFlipAndFirePoint(Vector3 direction)
    {
        if (spriteRenderer == null) return;

        bool shouldFlip;
        if (spriteFacesLeft)
        {
            shouldFlip = direction.x > 0;
        }
        else
        {
            shouldFlip = direction.x < 0;
        }

        if (shouldFlip != isFlipped)
        {
            isFlipped = shouldFlip;
            spriteRenderer.flipX = isFlipped;

            Vector3 adjustedPos = originalFirePointLocalPos;
            adjustedPos.x *= isFlipped ? -1 : 1;
            firePoint.localPosition = adjustedPos;
        }
    }

    void DetectEnemies()
    {
        Collider2D[] enemiesInRange = Physics2D.OverlapCircleAll(transform.position, Upgraded(MagicGarden.Stat.Range, detectionRange), enemyLayer);
        float closestDistance = Mathf.Infinity;
        Transform closestEnemy = null;

        foreach (Collider2D enemy in enemiesInRange)
        {
            if (enemy.GetComponent<GardenGnome>() != null)
                continue;

            float distance = Vector2.Distance(transform.position, enemy.transform.position);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestEnemy = enemy.transform;
            }
        }

        target = closestEnemy;
    }

    void StartAttackSequence()
    {
        if (target != null && animator != null)
        {
            isPerformingAttack = true;
            queuedTarget = target;
            animator.SetTrigger("Attack");
        }
    }

    public void OnShootAnimationEvent()
    {
        if (UpgradeRuntime.GameplayBlocked || projectile == null || queuedTarget == null || !queuedTarget.gameObject.activeInHierarchy) return;
        PlaySound(PlantSoundType.Attack);
        ShootAt(queuedTarget);
        int additional = Mathf.Max(Mathf.RoundToInt(UpgradeRuntime.EffectValue(UpgradeId, MagicGarden.EffectKind.AdditionalTargets)), 0);
        if (additional <= 0) return;
        var seen = new System.Collections.Generic.HashSet<LifeController>();
        var primary = queuedTarget.GetComponent<LifeController>();
        if (primary != null) seen.Add(primary);
        var enemies = Physics2D.OverlapCircleAll(transform.position, Upgraded(MagicGarden.Stat.Range, detectionRange), enemyLayer);
        foreach (var enemy in enemies)
        {
            var life = enemy.GetComponent<LifeController>();
            if (life == null || !life.IsAlive() || enemy.GetComponent<GardenGnome>() != null || !seen.Add(life)) continue;
            ShootAt(enemy.transform);
            if (--additional == 0) break;
        }
    }
    private void ShootAt(Transform enemy)
    {
        Vector2 direction = (enemy.position - firePoint.position).normalized;
        var spell = projectile.GetComponent<Spell>();
        if (spell == null) return;
        var description = spell.DescribeUpgradeTarget(UpgradeId, "");
        UpgradeCasting.Cast(projectile, firePoint.position, direction, UpgradeId, "AttackPlant",
            description.Bases.ContainsKey(MagicGarden.Stat.Quantity));
    }

    public void OnAttackAnimationEnd()
    {
        isPerformingAttack = false;
        queuedTarget = null;
    }

    protected override void OnMature()
    {
        base.OnMature();

        cooldown *= 0.7f;
        detectionRange *= 1.2f;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
    }
}