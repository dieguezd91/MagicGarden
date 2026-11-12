using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DefensePlant : Plant
{
    [Header("Defense Settings")]
    public float attractionRadius = 10f;
    public float reflectRadius = 2f;
    public float reflectDamage = 15f;
    public float reflectInterval = 0.5f;
    public LayerMask enemyLayer;
    public Color attractionColor = new Color(0.5f, 1f, 0.5f, 0.3f);
    public Color reflectColor = new Color(1f, 0.5f, 0.5f, 0.5f);

    public override MagicGarden.Target DescribeUpgradeTarget()
    {
        var target = base.DescribeUpgradeTarget();
        target.Capability = "DefensePlant";
        target.Bases[MagicGarden.Stat.Damage] = reflectDamage;
        target.Bases[MagicGarden.Stat.AttackSpeed] = 1 / Mathf.Max(0.001f, reflectInterval);
        target.Bases[MagicGarden.Stat.Range] = attractionRadius;
        target.Bases[MagicGarden.Stat.Area] = reflectRadius;
        return target;
    }
    private float reflectTimer = 0f;
    private bool canReflect = false;
    private Dictionary<EnemyBase, Coroutine> attractedEnemies = new Dictionary<EnemyBase, Coroutine>();

    protected override void Start()
    {
        base.Start();

        LifeController lifeController = GetComponent<LifeController>();
        if (lifeController != null)
        {
            lifeController.maxHealth = 150f;
            lifeController.currentHealth = lifeController.maxHealth;
            lifeController.onDamaged.AddListener(OnDamageTaken);
        }
    }

    private void OnDamageTaken(float damage, LifeController.DamageType damageType)
    {
        if (canReflect && damage > 0 && animator != null)
        {
            animator.SetTrigger("Attack");
        }
    }

    protected override void Update()
    {
        if (UpgradeRuntime.GameplayBlocked) return;
        base.Update();

        canReflect = IsFullyGrown();

        if (canReflect)
        {
            AttractEnemies();
            reflectTimer += Time.deltaTime;
            if (reflectTimer >= UpgradedCooldown(reflectInterval))
            {
                ReflectDamage();
                reflectTimer = 0f;
            }
        }
    }

    void AttractEnemies()
    {
        Collider2D[] enemiesInRange = Physics2D.OverlapCircleAll(transform.position, Upgraded(MagicGarden.Stat.Range, attractionRadius), enemyLayer);

        foreach (Collider2D enemyCollider in enemiesInRange)
        {
            EnemyBase enemy = enemyCollider.GetComponent<EnemyBase>();

            if (enemy != null && enemy is GardenGnome)
                continue;

            if (enemy != null && !attractedEnemies.ContainsKey(enemy))
            {
                if (enemy.GetCurrentTarget() != transform)
                {
                    enemy.SetTargetOverride(transform);

                    Coroutine restoreCoroutine = StartCoroutine(RestoreTargetAfterTime(enemy, 10f));
                    attractedEnemies.Add(enemy, restoreCoroutine);
                }
            }
        }

        List<EnemyBase> enemiesToRemove = new List<EnemyBase>();
        foreach (var kvp in attractedEnemies)
        {
            if (kvp.Key == null || !kvp.Key.gameObject.activeInHierarchy)
            {
                enemiesToRemove.Add(kvp.Key);
            }
        }

        foreach (var enemy in enemiesToRemove)
        {
            if (attractedEnemies.ContainsKey(enemy))
            {
                if (attractedEnemies[enemy] != null)
                    StopCoroutine(attractedEnemies[enemy]);

                attractedEnemies.Remove(enemy);
            }
        }
    }

    IEnumerator RestoreTargetAfterTime(EnemyBase enemy, float time)
    {
        yield return new WaitForSeconds(time);

        if (enemy != null && enemy.gameObject.activeInHierarchy)
        {
            enemy.ClearTargetOverride();
            attractedEnemies.Remove(enemy);
        }
    }

    void ReflectDamage()
    {
        Collider2D[] enemiesInRange = Physics2D.OverlapCircleAll(transform.position, Upgraded(MagicGarden.Stat.Area, reflectRadius), enemyLayer);

        bool hitAny = false;
        foreach (Collider2D enemyCollider in enemiesInRange)
        {
            LifeController enemyHealth = enemyCollider.GetComponent<LifeController>();
            if (enemyHealth != null)
            {
                enemyHealth.TakeDamage(Upgraded(MagicGarden.Stat.Damage, reflectDamage));
                hitAny = true;
            }
        }
        if (hitAny) PlaySound(PlantSoundType.Hit); // thorns lashing back
    }

    protected override void OnMature()
    {
        base.OnMature();

        LifeController lifeController = GetComponent<LifeController>();
        if (lifeController != null)
        {
            lifeController.maxHealth *= 2f;
            lifeController.currentHealth = lifeController.maxHealth;
            lifeController.onHealthChanged?.Invoke(lifeController.currentHealth, lifeController.maxHealth);
        }
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();

        foreach (var kvp in attractedEnemies)
        {
            if (kvp.Key != null && kvp.Key.gameObject.activeInHierarchy)
            {
                kvp.Key.ClearTargetOverride();
            }

            if (kvp.Value != null)
                StopCoroutine(kvp.Value);
        }
        attractedEnemies.Clear();

        LifeController lifeController = GetComponent<LifeController>();
        if (lifeController != null)
        {
            lifeController.onDamaged.RemoveListener(OnDamageTaken);
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = attractionColor;
        Gizmos.DrawWireSphere(transform.position, attractionRadius);

        Gizmos.color = reflectColor;
        Gizmos.DrawWireSphere(transform.position, reflectRadius);
    }
}