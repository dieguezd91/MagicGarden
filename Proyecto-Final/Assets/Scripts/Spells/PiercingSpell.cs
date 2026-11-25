using System.Collections.Generic;
using UnityEngine;

public class PiercingSpell : Spell
{
    [Header("SETTINGS")]
    [SerializeField] private float speed = 10f;

    [Header("PIERCING DAMAGE")]
    [SerializeField] private float firstHitDamage = 30f;
    [SerializeField] private float followingHitDamage = 15f;

    private int extensionHits;
    public override MagicGarden.Target DescribeUpgradeTarget(string id, string displayName)
    {
        var target = base.DescribeUpgradeTarget(id, displayName);
        target.Bases[MagicGarden.Stat.Damage] = firstHitDamage;
        target.Bases[MagicGarden.Stat.Range] = speed * lifeTime;
        target.Bases[MagicGarden.Stat.Quantity] = 1;
        return target;
    }
    protected override bool ExtendLifetime()
    {
        extensionHits = Mathf.RoundToInt(Special(MagicGarden.EffectKind.ExtraPierce));
        if (extensionHits <= 0) return false;
        // Baseline is unlimited piercing inside normal travel range. Preserve it;
        // purchased pierces are additional unique hits beyond that range.
        Destroy(gameObject, UpgradeRuntime.Current.Balance.pierceGraceSeconds);
        return true;
    }
    private Vector2 direction;
    private bool isInitialized = false;

    private readonly HashSet<LifeController> hitEnemies = new HashSet<LifeController>();

    private bool hasHitFirstEnemy = false;

    public override void Cast(Vector2 castDirection, Vector3 spawnPosition)
    {
        direction = castDirection.normalized;
        transform.position = spawnPosition;

        hitEnemies.Clear();
        hasHitFirstEnemy = false;

        isInitialized = true;
    }

    private void Update()
    {
        if (!isInitialized) return;

        transform.position += (Vector3)(direction * speed * Time.deltaTime);

        if (direction != Vector2.zero)
        {
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
            transform.rotation = Quaternion.Euler(0, 0, angle);
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!collision.CompareTag("Enemy"))
            return;

        LifeController life = collision.GetComponent<LifeController>();

        if (life == null || !life.IsAlive())
            return;

        if (hitEnemies.Contains(life))
            return;

        hitEnemies.Add(life);

        float currentDamage;

        if (!hasHitFirstEnemy)
        {
            currentDamage = firstHitDamage;
            hasHitFirstEnemy = true;
        }
        else
        {
            currentDamage = followingHitDamage;
        }

        currentDamage = Upgraded(MagicGarden.Stat.Damage, currentDamage);
        if (hitEnemies.Count > 1) currentDamage *= 1 + Special(MagicGarden.EffectKind.TraversalDamage);
        if (RitualBuffManager.Instance != null)
        {
            currentDamage *= RitualBuffManager.Instance.GetDamageMultiplier();
        }

        bool wasAlive = life.IsAlive();
        life.TakeDamage(currentDamage);
        lastHitKilled = wasAlive && !life.IsAlive(); // kill shake/hitstop like ApplyDamage

        ShowDamageText(collision.transform, currentDamage);
        PlayImpactEffects(collision.transform.position);
        ApplyKnockback(collision);
        if (extensionHits > 0 && --extensionHits == 0) Destroy(gameObject);

    }

    public void SetDirection(Vector2 newDirection)
    {
        Cast(newDirection, transform.position);
    }
}