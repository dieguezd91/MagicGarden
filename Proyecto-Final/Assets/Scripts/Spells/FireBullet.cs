using UnityEngine;
using System.Collections;

public class FireBullet : MonoBehaviour
{
    public enum BulletType { Fire, Ice }

    [Header("Elemental Settings")]
    public BulletType bulletType;

    [Header("FireBall Settings")]
    public float minDamage = 20f;
    public float maxDamage = 30f;
    public float lifeTime = 3f;
    public float speed = 10f;
    private Vector2 direction;
    private Rigidbody2D rb;
    private float lifeTimer;

    [Header("Trail Settings")]
    public GameObject fireTrailPrefab;
    public float trailSpawnRate = 0.01f;
    private float trailTimer;

    [Header("Impact Effects")]
    [SerializeField] private GameObject impactParticlesPrefab;

    [Header("Sound")]
    [SerializeField] private SoundClipData impactSound;

    private void PlayImpactSound()
    {
        // Positioned where it lands; the projectile is destroyed right after.
        SoundManager.Instance?.PlayClip(impactSound, SoundSourceType.Localized, transform);
    }
    [SerializeField] private float cameraShakeIntensity = 0.3f;
    [SerializeField] private float cameraShakeDuration = 0.3f;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void OnEnable()
    {
        lifeTimer = lifeTime;
        trailTimer = 0f;
    }

    void Update()
    {
        if (rb.velocity.magnitude > 0.1f)
        {
            float angle = Mathf.Atan2(rb.velocity.y, rb.velocity.x) * Mathf.Rad2Deg - 90f;
            transform.rotation = Quaternion.Euler(0, 0, angle);
        }

        trailTimer += Time.deltaTime;
        if (trailTimer >= trailSpawnRate)
        {
            SpawnTrail();
            trailTimer = 0f;
        }

        lifeTimer -= Time.deltaTime;
        if (lifeTimer <= 0f)
        {
            DeactivateBullet();
        }
    }

    void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player") || collision.CompareTag("Plant") || collision.CompareTag("Home"))
        {
            float dmg = Random.Range(minDamage, maxDamage);
            bool hitPlayer = collision.CompareTag("Player");

            var life = collision.GetComponent<LifeController>();
            if (life != null)
            {
                LifeController.DamageElement element = (bulletType == BulletType.Ice) ? LifeController.DamageElement.Ice : LifeController.DamageElement.Fire;

                life.TakeDamage(dmg, damageElement: element);
            }
            else
            {
                var houseLife = collision.GetComponent<HouseLifeController>();
                if (houseLife != null)
                {
                    houseLife.TakeDamage(dmg);
                }
            }

            PlayImpactSound();
            SpawnImpactEffects(collision.transform.position, hitPlayer);

            DeactivateBullet();
        }
    }

    public void SetDirection(Vector2 newDirection)
    {
        direction = newDirection.normalized;
        if (rb != null)
        {
            rb.velocity = direction * speed;

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
            transform.rotation = Quaternion.Euler(0, 0, angle);
        }
    }

    void SpawnTrail()
    {
        if (fireTrailPrefab != null)
        {
            Instantiate(fireTrailPrefab, transform.position, Quaternion.identity);
        }
    }

    void SpawnImpactEffects(Vector3 impactPosition, bool isPlayerHit)
    {
        if (impactParticlesPrefab != null)
        {
            var particles = Instantiate(impactParticlesPrefab, impactPosition, Quaternion.identity);
            var ps = particles.GetComponent<ParticleSystem>();

            if (ps != null)
                Destroy(particles, ps.main.duration + 0.2f);
            else
                Destroy(particles, 1f);
        }

        if (isPlayerHit && Time.timeScale > 0f && CameraShaker.Instance != null)
        {
            CameraShaker.Instance.Shake(cameraShakeIntensity, cameraShakeDuration);
        }
    }

    private void DeactivateBullet()
    {
        if (bulletType == BulletType.Ice)
        {
            BulletPool.Instance.ReturnIceBall(this);
        }
        else
        {
            BulletPool.Instance.ReturnBullet(this);
        }
    }
}