using UnityEngine;

public class HammerSpell : Spell
{
    [Header("SETTINGS")]
    [SerializeField] private float attackRadius = 2f;
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField] private float knockbackForce = 10f;

    [Header("VFX")]
    [SerializeField] private GameObject slashEffectPrefab;
    [SerializeField] private float effectDuration = 0.5f;

    public override MagicGarden.Target DescribeUpgradeTarget(string id, string displayName)
    {
        var target = base.DescribeUpgradeTarget(id, displayName);
        target.Bases[MagicGarden.Stat.Range] = UpgradeRuntime.Value(id, MagicGarden.Stat.Area, attackRadius, GetType().Name);
        target.Bases[MagicGarden.Stat.Area] = UpgradeRuntime.Value(id, MagicGarden.Stat.Range, attackRadius, GetType().Name);
        target.Bases[MagicGarden.Stat.Knockback] = knockbackForce;
        return target;
    }
    private float EffectiveRadius => Upgraded(MagicGarden.Stat.Range, Upgraded(MagicGarden.Stat.Area, attackRadius));
    private bool hasExecuted = false;

    public override void Cast(Vector2 direction, Vector3 spawnPosition)
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");

        if (player != null)
        {
            transform.position = player.transform.position;
        }
        else
        {
            transform.position = spawnPosition;
        }

        ExecuteMeleeAttack();

        Destroy(gameObject, effectDuration);
    }

    private void ExecuteMeleeAttack()
    {
        if (hasExecuted) return;

        hasExecuted = true;

        Collider2D[] hitEnemies = Physics2D.OverlapCircleAll(
            transform.position,
            EffectiveRadius,
            enemyLayer
        );

        foreach (Collider2D enemy in hitEnemies)
        {
            if (enemy == null)
                continue;

            LifeController life = enemy.GetComponent<LifeController>();

            if (life != null && life.IsAlive())
            {
                ApplyDamage(enemy);
            }
        }

        ShowHammerEffect();
        // Swing/slam sound is the prefab's castSound, played by PlayerSpellController.
    }

    private void ShowHammerEffect()
    {
        if (slashEffectPrefab == null) return;

        GameObject effect = Instantiate(
            slashEffectPrefab,
            transform.position,
            Quaternion.identity
        );

        Destroy(effect, effectDuration);
    }

    protected override void ApplyKnockback(Collider2D target)
    {
        var knockback = target.GetComponent<KnockbackReceiver>();

        if (knockback != null)
        {
            Vector2 knockbackDirection =
                (target.transform.position - transform.position).normalized;

            knockback.ApplyKnockback(
                knockbackDirection,
                Upgraded(MagicGarden.Stat.Knockback, knockbackForce)
            );
        }
    }

    public void SetDirection(Vector2 newDirection)
    {
        Cast(newDirection, transform.position);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRadius);
    }
}