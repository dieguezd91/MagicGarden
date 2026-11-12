
using UnityEngine;

public class SniperBossProjectile : MonoBehaviour
{
    [Header("Sound")]
    [SerializeField] private SoundClipData impactSound;

    private void PlayImpactSound()
    {
        // Positioned where it lands; the projectile is destroyed right after.
        SoundManager.Instance?.PlayClip(impactSound, SoundSourceType.Localized, transform);
    }
    private Transform owner;
    private Vector2 direction;

    private float speed;
    private float damage;
    private float lifetime;

    private bool initialized;
    private bool hasHit;

    public void Initialize(
        Transform projectileOwner,
        Vector2 shootDirection,
        float projectileSpeed,
        float projectileDamage,
        float projectileLifetime)
    {
        owner = projectileOwner;
        direction = shootDirection.normalized;

        speed = projectileSpeed;
        damage = projectileDamage;
        lifetime = projectileLifetime;

        initialized = true;

        if (direction != Vector2.zero)
        {
            float angle =
                Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;

            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        if (lifetime > 0f)
            Destroy(gameObject, lifetime);
    }

    private void Update()
    {
        if (!initialized || hasHit)
            return;

        transform.position +=
            (Vector3)(direction * speed * Time.deltaTime);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!initialized || hasHit)
            return;

        if (owner != null && collision.transform.root == owner.root)
            return;

        Transform target = FindValidTarget(collision.transform);

        if (target == null)
            return;

        hasHit = true;
        PlayImpactSound();

        if (target.CompareTag("Home"))
        {
            HouseLifeController home =
                target.GetComponent<HouseLifeController>() ??
                target.GetComponentInChildren<HouseLifeController>();

            if (home != null)
                home.TakeDamage(damage);
        }
        else
        {
            LifeController life =
                target.GetComponent<LifeController>() ??
                target.GetComponentInChildren<LifeController>();

            if (life != null && life.IsAlive())
            {
                life.TakeDamage(damage);

                if (target.CompareTag("Player"))
                    CameraShaker.Instance?.Shake(0.4f, 0.4f);
            }
        }

        Destroy(gameObject);
    }

    private Transform FindValidTarget(Transform current)
    {
        while (current != null)
        {
            if (current.CompareTag("Player") ||
                current.CompareTag("Plant") ||
                current.CompareTag("Home"))
            {
                return current;
            }

            current = current.parent;
        }

        return null;
    }
}
