
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class TurretMinionProjectile : MonoBehaviour
{
    [Header("Sound")]
    [SerializeField] private SoundClipData impactSound;

    private void PlayImpactSound()
    {
        // Positioned where it lands; the projectile is destroyed right after.
        SoundManager.Instance?.PlayClip(impactSound, SoundSourceType.Localized, transform);
    }
    private Rigidbody2D rb;

    private Transform owner;
    private Vector2 direction;

    private float speed;
    private float damage;

    private bool initialized;
    private bool hasHit;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;

        Collider2D projectileCollider = GetComponent<Collider2D>();
        projectileCollider.isTrigger = true;
    }

    public void Initialize(
        Transform projectileOwner,
        Vector2 shootDirection,
        float projectileSpeed,
        float projectileDamage,
        float lifetime)
    {
        owner = projectileOwner;
        direction = shootDirection.normalized;

        speed = projectileSpeed;
        damage = projectileDamage;

        initialized = true;

        if (direction != Vector2.zero)
        {
            float angle =
                Mathf.Atan2(direction.y, direction.x) *
                Mathf.Rad2Deg - 90f;

            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        if (lifetime > 0f)
            Destroy(gameObject, lifetime);
        else
            Destroy(gameObject, 4f);
    }

    private void FixedUpdate()
    {
        if (!initialized || hasHit)
            return;

        Vector2 nextPosition =
            rb.position + direction * speed * Time.fixedDeltaTime;

        rb.MovePosition(nextPosition);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!initialized || hasHit)
            return;

        Transform player = FindPlayerRoot(collision.transform);

        if (player == null)
            return;

        if (owner != null && player.root == owner.root)
            return;

        LifeController life = player.GetComponent<LifeController>();

        hasHit = true;
        PlayImpactSound();

        if (life != null && life.IsAlive())
        {
            life.TakeDamage(damage);
            CameraShaker.Instance?.Shake(0.3f, 0.3f);
        }

        Destroy(gameObject);
    }

    private Transform FindPlayerRoot(Transform current)
    {
        while (current != null)
        {
            if (current.CompareTag("Player"))
                return current;

            current = current.parent;
        }

        return null;
    }
}
