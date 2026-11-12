using UnityEngine;
using System.Collections;

public class GardenGnome : EnemyBase
{
    [Header("Gnome Data")]
    [SerializeField] private GardenGnomeEnemyDataSO gnomeData;

    [Header("Combat References")]
    [SerializeField] private LayerMask playerLayerMask;
    [SerializeField] private ParticleSystem explosionParticles;

    private float acceleration;
    private float stopDistance;
    private float chaseYOffset;
    private float clingDuration;
    private float minExplosionDamage;
    private float maxExplosionDamage;

    private Vector2 velocity;
    private bool isClinging = false;
    private Transform player;
    private LifeController playerLife;
    private LifeController targetLife;

    protected override void Awake()
    {
        base.Awake();
        rb.gravityScale = 0f;
        rb.drag = 0.5f;

        if (explosionParticles != null)
        {
            explosionParticles.Stop();
            explosionParticles.gameObject.SetActive(false);
        }
    }

    protected override EnemyDataSO GetEnemyData() => gnomeData;

    protected override void LoadEnemyData()
    {
        base.LoadEnemyData();

        if (gnomeData != null)
        {
            acceleration = gnomeData.Acceleration;
            stopDistance = gnomeData.StopDistance;
            chaseYOffset = gnomeData.ChaseYOffset;
            clingDuration = gnomeData.ClingDuration;
            minExplosionDamage = gnomeData.MinExplosionDamage;
            maxExplosionDamage = gnomeData.MaxExplosionDamage;
        }
        else
        {
            Debug.LogWarning($"[{gameObject.name}] No GnomeEnemyDataSO assigned!");
        }
    }

    protected override void Start()
    {
        base.Start();
        CachePlayerReference();       
    }

    protected override void Update()
    {
        base.Update();

        if (player == null || playerLife == null)
        {
            CachePlayerReference();
        }
        else if (!playerLife.IsTargetable())
        {
            player = null;
            playerLife = null;
        }
    }

    protected override void UpdateTargeting()
    {
        if (hasOverrideTarget && overrideTarget != null)
        {
            currentTarget = overrideTarget;
            currentTargetType = "override";
            return;
        }

        if (player != null && playerLife != null && playerLife.IsTargetable())
        {
            float distance = Vector2.Distance(transform.position, player.position);

            if (distance <= detectionRange)
            {
                currentTarget = player;
                currentTargetType = "player";
                return;
            }
        }

        currentTarget = null;
        currentTargetType = "none";
    }

    public void PerformAttack()
    {
        if (isClinging || player == null || playerLife == null) return;

        float distance = Vector2.Distance(transform.position, player.position);
        if (distance <= stopDistance)
        {
            StartCoroutine(ClingAndExplode(player.GetComponent<Rigidbody2D>()));
        }
    }

    private IEnumerator ClingAndExplode(Rigidbody2D playerRb)
    {
        isClinging = true;
        PlayEnemySound(EnemySoundType.Attack, SoundSourceType.Localized, transform);
        rb.velocity = Vector2.zero;
        rb.isKinematic = true;

        Transform gripPoint = playerRb.transform.Find("GnomeGripPoint");
        if (gripPoint != null)
        {
            transform.SetParent(gripPoint, true);
            transform.position = gripPoint.position;
        }
        else
        {
            transform.SetParent(playerRb.transform, true);
        }

        float dirX = playerRb.transform.position.x - transform.position.x;
        spriteRenderer.flipX = dirX > 0f;

        if (animator != null)
            animator.SetBool("IsClinging", true);

        targetLife = playerRb.GetComponent<LifeController>();

        yield return new WaitForSeconds(clingDuration);

        Explode();
    }

    public void Explode()
    {
        PlayEnemySound(EnemySoundType.Impact, SoundSourceType.Localized, transform);
        // 1. Lógica de Daño
        if (targetLife != null && targetLife.IsTargetable())
        {
            float damage = Random.Range(minExplosionDamage, maxExplosionDamage);
            targetLife.TakeDamage(damage);
            CameraShaker.Instance?.Shake(0.3f, 0.3f);
        }

        if (explosionParticles != null)
        {
            explosionParticles.transform.SetParent(null);

            explosionParticles.transform.position = transform.position;
            explosionParticles.transform.rotation = Quaternion.identity;

            explosionParticles.gameObject.SetActive(true);
            explosionParticles.Play();

            var main = explosionParticles.main;
            Destroy(explosionParticles.gameObject, main.duration + main.startLifetime.constantMax);
        }

        // 3. Muerte del Gnomo
        LifeController selfLife = GetComponent<LifeController>();
        if (selfLife != null) selfLife.Die();

        Destroy(gameObject);
    }

    private void CachePlayerReference()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj == null) return;

        LifeController life = playerObj.GetComponent<LifeController>();
        if (life != null && life.IsTargetable())
        {
            player = playerObj.transform;
            playerLife = life;
        }
    }

    protected override void ProcessMovement()
    {
    }
}
