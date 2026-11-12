
using System.Collections;
using UnityEngine;

public class SniperBoss : Boss
{
    [Header("SNIPER REFERENCES")]
    [SerializeField] private Transform aimPivot;
    [SerializeField] private Transform firePoint;

    [Header("AIM VISUAL")]
    [Tooltip("Offset for sprites that do not face upward by default.")]
    [SerializeField] private float aimRotationOffset = -90f;

    private SniperBossDataSO sniperData;
    private Coroutine sniperAttackCoroutine;

    public float SniperAttackRange =>
        sniperData != null ? sniperData.SniperAttackRange : 8f;

    public float ShotInterval =>
        sniperData != null ? sniperData.ShotInterval : 3.5f;

    protected override bool UseTimedMinionWaves => true;

    protected override void LoadEnemyData()
    {
        base.LoadEnemyData();

        sniperData = bossData as SniperBossDataSO;

        if (sniperData == null)
        {
            Debug.LogError(
                $"[{name}] The assigned Boss Data must be a SniperBossDataSO.",
                this
            );

            return;
        }

        detectionRange = Mathf.Max(
            detectionRange,
            sniperData.SniperAttackRange
        );
    }

    protected override void InitializeEnemy()
    {
        base.InitializeEnemy();

        if (StateMachine != null)
        {
            StateMachine.RegisterState(
                new SniperBossAttackState(this)
            );
        }
    }

    public void TryStartSniperAttack(Transform target)
    {
        if (isDead ||
            isCurrentlyAttacking ||
            sniperAttackCoroutine != null ||
            target == null ||
            sniperData == null)
        {
            return;
        }

        sniperAttackCoroutine = StartCoroutine(
            SniperAttackRoutine(target)
        );
    }

    private IEnumerator SniperAttackRoutine(Transform target)
    {
        isCurrentlyAttacking = true;
        StopMovement();

        // The Attack clip (Boss_Attack_Invoke) swells to its peak ~0.6 s in; start it so the peak lands on the shot.
        const float AttackClipPeak = 0.6f;
        bool attackSoundPlayed = false;

        float elapsed = 0f;

        while (elapsed < sniperData.AimDuration)
        {
            if (isDead || !IsTargetValid(target))
                break;

            UpdateAim(target.position);

            if (!attackSoundPlayed && elapsed >= sniperData.AimDuration - AttackClipPeak)
            {
                attackSoundPlayed = true;
                PlayEnemySound(EnemySoundType.Attack, SoundSourceType.Localized, transform);
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (!isDead && IsTargetValid(target))
        {
            FireAtTarget(target.position);
        }

        yield return new WaitForSeconds(
            sniperData.RecoveryDuration
        );

        isCurrentlyAttacking = false;
        sniperAttackCoroutine = null;
    }

    private bool IsTargetValid(Transform target)
    {
        if (target == null)
            return false;

        if (target.CompareTag("Home"))
            return true;

        LifeController life =
            target.GetComponent<LifeController>() ??
            target.GetComponentInParent<LifeController>();

        return life == null || life.IsAlive();
    }

    private void UpdateAim(Vector3 targetPosition)
    {
        if (aimPivot == null)
            return;

        Transform origin = firePoint != null
            ? firePoint
            : aimPivot;

        Vector2 direction =
            (targetPosition - origin.position).normalized;

        float angle =
            Mathf.Atan2(direction.y, direction.x) *
            Mathf.Rad2Deg + aimRotationOffset;

        aimPivot.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    private void FireAtTarget(Vector3 targetPosition)
    {
        if (sniperData.ProjectilePrefab == null)
        {
            Debug.LogWarning(
                $"[{name}] No sniper projectile prefab assigned.",
                this
            );

            return;
        }

        Transform origin = firePoint != null
            ? firePoint
            : transform;

        Vector2 direction =
            (targetPosition - origin.position).normalized;

        GameObject projectileObject = Instantiate(
            sniperData.ProjectilePrefab,
            origin.position,
            Quaternion.identity
        );

        SniperBossProjectile projectile =
            projectileObject.GetComponent<SniperBossProjectile>();

        if (projectile == null)
        {
            Debug.LogError(
                "Sniper projectile prefab is missing SniperBossProjectile.",
                projectileObject
            );

            Destroy(projectileObject);
            return;
        }

        projectile.Initialize(
            transform,
            direction,
            sniperData.ProjectileSpeed,
            sniperData.ProjectileDamage,
            sniperData.ProjectileLifetime
        );
    }

    public void CancelSniperAttack()
    {
        if (sniperAttackCoroutine != null)
        {
            StopCoroutine(sniperAttackCoroutine);
            sniperAttackCoroutine = null;
        }

        isCurrentlyAttacking = false;
    }
}
