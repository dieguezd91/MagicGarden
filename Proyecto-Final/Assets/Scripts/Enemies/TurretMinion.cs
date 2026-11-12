
using System.Collections;
using UnityEngine;

public class TurretMinion : EnemyBase
{
    [Header("TURRET DATA")]
    [SerializeField] private TurretMinionDataSO turretData;

    [Header("AIM REFERENCES")]
    [SerializeField] private Transform aimPivot;
    [SerializeField] private Transform firePoint;

    [Header("AIM SETTINGS")]
    [SerializeField] private float aimRotationOffset = -90f;

    private Transform playerTarget;

    private Coroutine burstCoroutine;

    private float nextBurstTime = 0f;

    public float AttackRange =>
        turretData != null ? turretData.AttackRange : 4.5f;

    public float BurstCooldown =>
        turretData != null ? turretData.BurstCooldown : 2.5f;

    public float NextBurstTime => nextBurstTime;

    public bool IsBursting =>
        burstCoroutine != null || isCurrentlyAttacking;

    protected override EnemyDataSO GetEnemyData()
    {
        return turretData;
    }

    protected override void LoadEnemyData()
    {
        base.LoadEnemyData();

        if (turretData == null)
        {
            Debug.LogError(
                $"[{name}] Missing TurretMinionDataSO!",
                this
            );

            return;
        }

        detectionRange = Mathf.Max(
            detectionRange,
            turretData.AttackRange
        );
    }

    protected override void InitializeEnemy()
    {
        base.InitializeEnemy();

        if (StateMachine == null)
            return;

        StateMachine.RegisterState(
            new TurretMinionAttackState(this)
        );

        StateMachine.ChangeState<TurretMinionAttackState>();

        if (lifeController != null)
        {
            lifeController.onDeath.AddListener(OnTurretMinionDeath);
        }
    }

    protected override void ProcessMovement()
    {
        // Turret minions do not move.
    }

    protected override void OnDestroy()
    {
        if (lifeController != null)
        {
            lifeController.onDeath.RemoveListener(OnTurretMinionDeath);
        }

        if (burstCoroutine != null)
        {
            StopCoroutine(burstCoroutine);
            burstCoroutine = null;
        }

        base.OnDestroy();
    }

    public Transform GetPlayerTarget()
    {
        if (playerTarget == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");

            if (player != null)
                playerTarget = player.transform;
        }

        if (playerTarget == null)
            return null;

        LifeController playerLife =
            playerTarget.GetComponent<LifeController>();

        if (playerLife == null ||
            !playerLife.IsAlive() ||
            !playerLife.IsTargetable())
        {
            return null;
        }

        return playerTarget;
    }

    public bool IsPlayerInAttackRange(Transform target)
    {
        if (target == null)
            return false;

        return Vector2.Distance(
            transform.position,
            target.position
        ) <= AttackRange;
    }

    public void TryStartBurst(Transform target)
    {
        if (isDead ||
            IsBursting ||
            target == null ||
            turretData == null ||
            turretData.ProjectilePrefab == null)
        {
            return;
        }

        if (Time.time < nextBurstTime)
            return;

        burstCoroutine = StartCoroutine(FireBurstRoutine());
    }

    private IEnumerator FireBurstRoutine()
    {
        isCurrentlyAttacking = true;

        StopMovement();

        int shots = Mathf.Max(1, turretData.ShotsPerBurst);

        for (int i = 0; i < shots; i++)
        {
            while (UpgradeRuntime.GameplayBlocked && !isDead)
                yield return null;

            if (isDead)
                break;

            Transform target = GetPlayerTarget();

            if (target == null || !IsPlayerInAttackRange(target))
                break;

            PlayEnemySound(EnemySoundType.Attack, SoundSourceType.Localized, transform); // one per bullet
            FireAtTarget(target.position);

            if (i < shots - 1)
            {
                yield return new WaitForSeconds(
                    turretData.TimeBetweenShots
                );
            }
        }

        isCurrentlyAttacking = false;

        burstCoroutine = null;

        nextBurstTime = Time.time + BurstCooldown;
    }

    private void FireAtTarget(Vector3 targetPosition)
    {
        Transform origin = firePoint != null
            ? firePoint
            : transform;

        Vector2 direction = (
            (Vector2)targetPosition -
            (Vector2)origin.position
        ).normalized;

        UpdateAim(targetPosition);

        GameObject projectileObject = Instantiate(
            turretData.ProjectilePrefab,
            origin.position,
            Quaternion.identity
        );

        TurretMinionProjectile projectile =
            projectileObject.GetComponent<TurretMinionProjectile>();

        if (projectile == null)
        {
            Debug.LogError(
                "Projectile prefab is missing TurretMinionProjectile.",
                projectileObject
            );

            Destroy(projectileObject);
            return;
        }

        projectile.Initialize(
            transform,
            direction,
            turretData.ProjectileSpeed,
            turretData.ProjectileDamage,
            turretData.ProjectileLifetime
        );
    }

    private void UpdateAim(Vector3 targetPosition)
    {
        if (aimPivot == null)
            return;

        Transform origin = firePoint != null
            ? firePoint
            : aimPivot;

        Vector2 direction = (
            (Vector2)targetPosition -
            (Vector2)origin.position
        ).normalized;

        float angle =
            Mathf.Atan2(direction.y, direction.x) *
            Mathf.Rad2Deg + aimRotationOffset;

        aimPivot.rotation = Quaternion.Euler(
            0f,
            0f,
            angle
        );
    }

    private void OnTurretMinionDeath()
    {
        if (isDead)
            return;

        if (burstCoroutine != null)
        {
            StopCoroutine(burstCoroutine);
            burstCoroutine = null;
        }

        isCurrentlyAttacking = false;

        MarkAsDead();
    }

    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();

        Gizmos.color = Color.red;

        Gizmos.DrawWireSphere(
            transform.position,
            AttackRange
        );
    }
}
