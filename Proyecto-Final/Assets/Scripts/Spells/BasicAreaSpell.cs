using UnityEngine;

public class BasicAreaSpell : Spell
{
    [Header("SETTINGS")]
    [SerializeField] private float speed;
    [SerializeField] private float explosionRadius;
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField] private float knockbackForce;

    [Header("VFX")]
    [SerializeField] private GameObject explosionEffectPrefab;
    [SerializeField] private float explosionEffectDuration;

    public override MagicGarden.Target DescribeUpgradeTarget(string id, string displayName)
    {
        var target = base.DescribeUpgradeTarget(id, displayName);
        target.Bases[MagicGarden.Stat.Range] = speed * lifeTime;
        target.Bases[MagicGarden.Stat.Area] = explosionRadius;
        target.Bases[MagicGarden.Stat.Knockback] = knockbackForce;
        target.Bases[MagicGarden.Stat.Quantity] = 1;
        return target;
    }
    private float EffectiveRadius => Upgraded(MagicGarden.Stat.Area, explosionRadius) * (1 + Special(MagicGarden.EffectKind.AreaMultiplier));
    private Vector2 direction;
    private bool hasExploded = false;
    private bool isInitialized = false;

    [SerializeField] private float armingTime;
    private float spawnTime;

    public override void Cast(Vector2 castDirection, Vector3 spawnPosition)
    {
        direction = castDirection.normalized;
        transform.position = spawnPosition;
        isInitialized = true;
        spawnTime = Time.time;
    }

    private void Update()
    {
        if (hasExploded || !isInitialized) return;

        transform.position += (Vector3)(direction * speed * Time.deltaTime);

        if (direction != Vector2.zero)
        {
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
            transform.rotation = Quaternion.Euler(0, 0, angle);
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (Time.time - spawnTime < armingTime) return;

        if (collision.CompareTag("Player")) return;

        if (!hasExploded)
        {
            Explode();
        }
    }

    private void Explode()
    {
        if (hasExploded) return;
        hasExploded = true;
        PlayImpactSound(); // the explosion itself; impactSound's cooldown stops per-enemy repeats below

        Collider2D[] hitEnemies = Physics2D.OverlapCircleAll(transform.position, EffectiveRadius, enemyLayer);

        if (hitEnemies.Length > 0)
        {
            foreach (Collider2D enemy in hitEnemies)
            {
                ApplyDamage(enemy);
            }
        }

        ShowExplosionEffect();

        if (Time.timeScale > 0f && CameraShaker.Instance != null)
        {
            CameraShaker.Instance.Shake(0.5f, 0.4f);
        }

        Destroy(gameObject);
    }

    private void ShowExplosionEffect()
    {
        if (explosionEffectPrefab == null) return;

        GameObject explosion = Instantiate(explosionEffectPrefab, transform.position, Quaternion.identity);

        float scale = EffectiveRadius;
        explosion.transform.localScale = Vector3.one * scale;

        Destroy(explosion, explosionEffectDuration);
    }

    protected override void ApplyKnockback(Collider2D target)
    {
        var knockback = target.GetComponent<KnockbackReceiver>();
        if (knockback != null)
        {
            Vector2 knockbackDirection = (target.transform.position - transform.position).normalized;
            knockback.ApplyKnockback(knockbackDirection, Upgraded(MagicGarden.Stat.Knockback, knockbackForce));
        }
    }

    public void SetDirection(Vector2 newDirection)
    {
        Cast(newDirection, transform.position);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }
}