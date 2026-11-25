using System.Collections.Generic;
using UnityEngine;

public class Skeleton : EnemyBase
{
    [Header("Melee Data")]
    [SerializeField] private MeleeEnemyDataSO meleeData;

    [Header("Combat References")]
    public Transform attackPoint;
    public LayerMask attackableLayers;

    private float attackDistance;
    // How close it walks before swinging (to the target's edge). attackRange is the sword's hit radius around attackPoint.
    public float AttackDistance => attackDistance;
    public float minDamage;
    public float maxDamage;
    public float attackRange;

    [Tooltip("If the attack animation never reports its end (interrupted), the swing is released after this many seconds.")]
    [SerializeField] private float swingFailsafeSeconds = 2f;

    private Vector3 attackPointOffset;
    private float attackStartedAt;
    private Transform measuredTarget;
    private Collider2D[] targetColliders = System.Array.Empty<Collider2D>();

    protected override EnemyDataSO GetEnemyData() => meleeData;

    protected override void Awake()
    {
        base.Awake();
        LoadEnemyData();
        if (attackPoint != null)
            attackPointOffset = attackPoint.localPosition;
    }

    protected override void LoadEnemyData()
    {
        base.LoadEnemyData();

        if (meleeData != null)
        {
            attackDistance = meleeData.AttackDistance;
            minDamage = meleeData.MinDamage;
            maxDamage = meleeData.MaxDamage;
            attackCooldown = meleeData.AttackCooldown;
            attackRange = meleeData.AttackRange;
        }
        else
        {
            Debug.LogWarning($"[{gameObject.name}] No MeleeEnemyDataSO assigned!");
        }
    }

    protected override void Update()
    {
        base.Update();

        if (isDead)
        {
           return;
        }

        if (isCurrentlyAttacking && Time.time - attackStartedAt > swingFailsafeSeconds)
            OnAttackAnimationEnd();
    }

    protected override void FixedUpdate()
    {
        base.FixedUpdate();
        StateMachine?.CurrentState?.OnFixedUpdate();
    }

    

    protected override void ProcessMovement()
    {
    }


    // Distance to the nearest edge of the target's solid colliders, not its centre. Large targets (the house)
    // and bodies blocking each other kept the centres apart, so the skeleton walked in place without attacking.
    public override float GetDistanceToTarget()
    {
        if (currentTarget == null) return Mathf.Infinity;
        if (measuredTarget != currentTarget)
        {
            measuredTarget = currentTarget;
            targetColliders = currentTarget.GetComponentsInChildren<Collider2D>();
        }

        Vector2 from = transform.position;
        float best = Mathf.Infinity;
        foreach (var col in targetColliders)
        {
            if (col == null || !col.enabled || col.isTrigger) continue;
            best = Mathf.Min(best, Vector2.Distance(from, col.ClosestPoint(from)));
        }
        return best < Mathf.Infinity ? best : Vector2.Distance(from, currentTarget.position);
    }

    public bool CanAttack()
    {
        return !isCurrentlyAttacking && Time.time >= nextAttackTime;
    }

    // Called by EnemyAttackState. Once started the swing always plays to the end, even if the target walks away.
    public void PerformAttack()
    {
        if (!CanAttack()) return;
        FaceCurrentTarget();
        StartAttack();
    }

    public void StartAttack()
    {
        if (isCurrentlyAttacking) return;

        isCurrentlyAttacking = true;
        attackStartedAt = Time.time;
        nextAttackTime = Time.time + attackCooldown;

        animator?.SetBool("isAttacking", true);
    }

    // The sprite turns with flipX, which does not move children, so the attack point is mirrored by hand
    // to the side the skeleton is facing. Otherwise the swing hits behind it whenever it faces the other way.
    private void FaceCurrentTarget()
    {
        if (currentTarget == null) return;
        float dx = currentTarget.position.x - transform.position.x;
        if (Mathf.Abs(dx) < 0.01f) return;
        float side = Mathf.Sign(dx);
        UpdateSpriteDirection(new Vector2(side, 0f));
        if (attackPoint != null)
            attackPoint.localPosition = new Vector3(Mathf.Abs(attackPointOffset.x) * side, attackPointOffset.y, attackPointOffset.z);
    }

    // Animation event a couple of frames before PerformSwordHit, so the whoosh leads the blade.
    public void PlaySwingSound()
    {
        PlayEnemySound(EnemySoundType.Attack, SoundSourceType.Localized, transform);
    }

    public void PerformSwordHit()
    {

        Collider2D[] hitTargets = Physics2D.OverlapCircleAll(attackPoint.position, attackRange, attackableLayers);
        HashSet<GameObject> damagedTargets = new HashSet<GameObject>();

        foreach (Collider2D target in hitTargets)
        {
            GameObject obj = target.gameObject;
            if (damagedTargets.Contains(obj)) continue;

            damagedTargets.Add(obj);
            DealDamageTo(obj);
        }
    }

    private void DealDamageTo(GameObject target)
    {
        float damage = Random.Range(minDamage, maxDamage);

        PlayEnemySound(EnemySoundType.Impact, SoundSourceType.Localized, target.transform);

        if (target.TryGetComponent(out LifeController life))
        {
            life.TakeDamage(damage);

            if (currentTargetType == "player")
            {
                CameraShaker.Instance?.Shake(0.3f, 0.3f);
            }
        }
        else if (target.TryGetComponent(out HouseLifeController houseLife))
        {
            houseLife.TakeDamage(damage);
        }
    }

    public void OnAttackAnimationEnd()
    {
        isCurrentlyAttacking = false;
        animator?.SetBool("isAttacking", false);

        if (isDead)
        {
            StateMachine.ChangeState<EnemyDeadState>();
        }
        else if (StateMachine.CurrentState is EnemyAttackState)
        {
            // Stay put: the attack state swings again when ready, or starts chasing if the target got away.
            // Going through the chase state first made the skeleton take a walking step after every swing.
        }
        else if (currentTarget != null && GetDistanceToTarget() <= detectionRange)
        {
            StateMachine.ChangeState<EnemyChaseState>();
        }
        else
        {
            StateMachine.ChangeState<EnemyIdleState>();
        }
    }

    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackDistance);

        if (attackPoint != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(attackPoint.position, attackRange);
        }
    }
}
