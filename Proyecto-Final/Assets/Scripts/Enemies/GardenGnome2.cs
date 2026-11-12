using UnityEngine;
using System.Collections;

public class GardenGnome2 : EnemyBase
{
    [Header("Gnome Data")]
    [SerializeField] private GardenGnomeEnemyDataSO gnomeData;

    [Header("Combat References")]
    [SerializeField] private LayerMask plantLayerMask;
    [SerializeField] private ParticleSystem explosionParticles;

    private float acceleration;
    private float stopDistance;
    private float chaseYOffset;
    private float clingDuration;
    private float minExplosionDamage;
    private float maxExplosionDamage;

    private Vector2 velocity;
    private bool isClinging = false;

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

    protected override void UpdateTargeting()
    {
        if (hasOverrideTarget && overrideTarget != null)
        {
            currentTarget = overrideTarget;
            currentTargetType = "override";
            return;
        }

        Collider2D[] plants = Physics2D.OverlapCircleAll(
            transform.position,
            detectionRange,
            plantLayerMask
        );

        Transform closestPlant = null;
        float closestDistance = Mathf.Infinity;

        foreach (Collider2D plant in plants)
        {
            LifeController plantLife = plant.GetComponent<LifeController>();

            if (plantLife == null || !plantLife.IsTargetable())
                continue;

            float distance = Vector2.Distance(
                transform.position,
                plant.transform.position
            );

            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestPlant = plant.transform;
                targetLife = plantLife;
            }
        }

        if (closestPlant != null)
        {
            currentTarget = closestPlant;
            currentTargetType = "plant";
        }
        else
        {
            currentTarget = null;
            currentTargetType = "none";
            targetLife = null;
        }
    }

    public void PerformAttack()
    {
        if (isClinging || currentTarget == null || targetLife == null)
            return;

        if (!targetLife.IsTargetable())
            return;

        float distance = Vector2.Distance(
            transform.position,
            currentTarget.position
        );

        if (distance <= stopDistance)
        {
            StartCoroutine(ClingAndExplode());
        }
    }

    private IEnumerator ClingAndExplode()
    {
        isClinging = true;

        rb.velocity = Vector2.zero;
        rb.isKinematic = true;
        Debug.Log($"Gnomo agarrándose a: {currentTarget.name}");
        Transform gripPoint = currentTarget.Find("GnomeGripPoint");
        Debug.Log($"GripPoint encontrado: {gripPoint != null}");

        if (gripPoint != null)
        {
            transform.SetParent(gripPoint, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
        }
        else
        {
            Debug.LogWarning(
                $"[{gameObject.name}] No se encontró GnomeGripPoint en {currentTarget.name}"
            );

            transform.SetParent(currentTarget, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
        }

        float dirX = currentTarget.position.x - transform.position.x;

        if (spriteRenderer != null)
            spriteRenderer.flipX = dirX > 0f;

        if (animator != null)
            animator.SetBool("IsClinging", true);

        yield return new WaitForSeconds(clingDuration);

        Explode();
    }

    public void Explode()
    {
        PlayEnemySound(EnemySoundType.Impact, SoundSourceType.Localized, transform);
        if (targetLife != null && targetLife.IsTargetable())
        {
            float damage = Random.Range(
                minExplosionDamage,
                maxExplosionDamage
            );

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

            Destroy(
                explosionParticles.gameObject,
                main.duration + main.startLifetime.constantMax
            );
        }

        LifeController selfLife = GetComponent<LifeController>();

        if (selfLife != null)
            selfLife.Die();

        Destroy(gameObject);
    }

    protected override void ProcessMovement()
    {
    }
}