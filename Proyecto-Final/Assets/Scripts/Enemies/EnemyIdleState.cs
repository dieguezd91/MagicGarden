using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyIdleState : IState
{
    private readonly EnemyBase enemy;

    public EnemyIdleState(EnemyBase enemy) => this.enemy = enemy;

    public void OnEnter()
    {
        enemy.StopMovement();
    }

    public void OnUpdate()
    {
        if (enemy.GetCurrentTarget() != null)
            enemy.StateMachine.ChangeState<EnemyChaseState>();
    }

    public void OnFixedUpdate() { }

    public void OnExit() { }
}

public class EnemyChaseState : IState
{
    private readonly EnemyBase enemy;
    private float attackRange = 1.5f;

    public EnemyChaseState(EnemyBase enemy)
    {
        this.enemy = enemy;
        DetectAttackRange();
    }

    private void DetectAttackRange()
    {
        if (enemy is SniperBoss sniperBoss) 
            attackRange = sniperBoss.SniperAttackRange;
        if (enemy is Skeleton skeleton)
            attackRange = skeleton.AttackDistance;
        else if (enemy is Infernum infernum)
            attackRange = infernum.GetType().GetField("shootingRange", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(infernum) as float? ?? 5f;
        else if (enemy is GardenGnome gnome)
            attackRange = gnome.GetType().GetField("stopDistance", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(gnome) as float? ?? 0.3f;
    }

    public void OnEnter() { }

    public void OnUpdate()
    {
        var target = enemy.GetCurrentTarget();

        if (target == null)
        {
            enemy.StateMachine.ChangeState<EnemyIdleState>();
            return;
        }

        if (enemy.GetDistanceToTarget() <= attackRange)
        {
            if (enemy is SniperBoss)
            {
                enemy.StateMachine.ChangeState<SniperBossAttackState>();
            }
            else
            {
                enemy.StateMachine.ChangeState<EnemyAttackState>();
            }
        }
    }

    public void OnFixedUpdate()
    {
        var target = enemy.GetCurrentTarget();
        if (target == null || !enemy.CanMove()) return;

        var knockback = enemy.GetComponent<KnockbackReceiver>();
        if (knockback != null && knockback.IsBeingKnockedBack()) return;

        Vector2 dir = (target.position - enemy.transform.position).normalized;
        enemy.MoveTowardsTarget(dir, enemy.MoveSpeed);
    }

    public void OnExit() => enemy.StopMovement();
}

public class EnemyAttackState : IState
{
    private EnemyBase enemy;
    private Skeleton skeleton;
    private Infernum infernum;
    private GardenGnome gnome;
    private Boss boss;

    private float attackRange = 1.5f;
    private float meleeDelay;
    private float nextAttackTime;

    public EnemyAttackState(EnemyBase enemy)
    {
        this.enemy = enemy;
        DetectAttackRange();
    }

    private void DetectAttackRange()
    {
        if (enemy is Skeleton skeleton)
        {
            this.skeleton = skeleton;
            attackRange = skeleton.AttackDistance;
        }
        else if (enemy is Infernum infernum)
        {
            this.infernum = infernum;
            attackRange = infernum.GetType().GetField("shootingRange",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.GetValue(infernum) as float? ?? 5f;
        }
        else if (enemy is GardenGnome gnome)
        {
            this.gnome = gnome;
            attackRange = gnome.GetType().GetField("stopDistance",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.GetValue(gnome) as float? ?? 0.3f;
        }
        else if (enemy is Boss boss)
        {
            this.boss = boss;

            if (boss.bossData != null)
            {
                attackRange = boss.bossData.meleeRadius;
                meleeDelay = boss.bossData.meleeDelay;
            }
            else
            {
                attackRange = 3f;
                meleeDelay = 0.6f;
            }
        }
    }

    public void OnEnter()
    {
        enemy.StopMovement();
        enemy.isCurrentlyAttacking = false;
        nextAttackTime = Time.time;

        // The skeleton turns its swing on itself when it actually starts one (see Skeleton.PerformAttack).
        if (enemy.animator != null && skeleton == null)
            enemy.animator.SetBool("isAttacking", true);
    }

    public void OnUpdate()
    {
        // Prevent state changes while attack is in progress
        if (enemy.isCurrentlyAttacking)
        {
            // Only allow transition to dead state if enemy dies
            if (enemy.isDead)
            {
                enemy.StateMachine.ChangeState<EnemyDeadState>();
                Debug.Log($"[{enemy.gameObject.name}] EnemyAttackState: Transitioning to DeadState during attack.");
            }
            // Otherwise, stay in attack state
            return;
        }

        if (enemy.GetDistanceToTarget() > attackRange + 0.5f)
        {
            enemy.StateMachine.ChangeState<EnemyChaseState>();
            return;
        }

        if (skeleton != null)
        {
            if (skeleton.CanAttack())
                skeleton.PerformAttack();
        }
        else if (boss != null && boss.bossData != null)
        {
            // The golem only has the melee thrust (and the summon, handled elsewhere); there is no special attack
            // animation, so out of melee reach it walks closer instead of hitting from afar.
            if (enemy.GetDistanceToTarget() > boss.bossData.meleeRadius)
            {
                enemy.StateMachine.ChangeState<EnemyChaseState>();
                return;
            }

            if (Time.time >= nextAttackTime && !enemy.isCurrentlyAttacking)
            {
                enemy.isCurrentlyAttacking = true;
                boss.StartCoroutine(BossMeleeRoutine());
                nextAttackTime = Time.time + meleeDelay;
            }
        }
        else
        {
            if (Time.time >= nextAttackTime)
            {
                var method = enemy.GetType().GetMethod("PerformAttack",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);

                if (method != null)
                    method.Invoke(enemy, null);

                nextAttackTime = Time.time + enemy.attackCooldown;
            }

            if (infernum != null && infernum.CanShootNow)
                infernum.PerformAttack();
        }
    }

    // Waits for the boss's own attack to finish. Clearing isCurrentlyAttacking earlier made the golem's
    // strike event (which needs it) skip the damage, and let the state walk away mid-thrust.
    private IEnumerator BossMeleeRoutine()
    {
        yield return new WaitForSeconds(0.2f);
        boss.TryStartMeleeAttack();

        yield return new WaitUntil(() => boss == null || boss.isDead || !boss.IsMeleeAttacking);
        enemy.isCurrentlyAttacking = false;
    }

    public void OnFixedUpdate() { }

    public void OnExit()
    {
        enemy.isCurrentlyAttacking = false;

        if (enemy.animator != null)
            enemy.animator.SetBool("isAttacking", false);
    }
}

public class EnemyDeadState : IState
{
    private readonly EnemyBase enemy;
    private Coroutine destroyCoroutine;
    private const float DefaultDestroyDelay =0.1f;
    private readonly LifeController lifeController;

    public EnemyDeadState(EnemyBase enemy) => this.enemy = enemy;

    public void OnEnter()
    {
        enemy.StopMovement();

        Animator anim = enemy.GetComponent<Animator>();
        if (anim != null)
        {
            anim.SetTrigger("Death");
        }

        var cols = enemy.GetComponents<Collider2D>();
        foreach (var c in cols) c.enabled = false;

        var rb = enemy.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.velocity = Vector2.zero;
            rb.isKinematic = true;
        }

        destroyCoroutine = enemy.StartCoroutine(DestroyAfterDelay(DefaultDestroyDelay));
    }

    private IEnumerator DestroyAfterDelay(float delay)
    {
        
        yield return new WaitForSeconds(delay);
        
        if (enemy.animator != null) enemy.animator.SetTrigger("Death");
    }

    public void OnUpdate()
    {
    }

    public void OnFixedUpdate()
    {
    }

    public void OnExit()
    {
        
    }
}

public class EnemySpawnMinionState : IState
{
    private readonly Boss boss;
    private Coroutine spawnRoutine;

    public EnemySpawnMinionState(Boss boss)
    {
        this.boss = boss;
    }

    public void OnEnter()
    {
        boss.StopMovement();
        boss.isCurrentlyAttacking = false;
        boss.isSpawningMinions = true;

        spawnRoutine = boss.StartCoroutine(SpawnRoutine());
    }

    private IEnumerator SpawnRoutine()
    {
        boss.KillAllMinions();

        yield return new WaitForSeconds(boss.MinionCleanupDelay);

        if (boss.IsDead)
            yield break;

        if (boss.animator != null)
        {
            boss.animator.SetTrigger("SpawnMinions");
        }

        yield return new WaitForSeconds(1.5f);

        if (boss.IsDead)
            yield break;

        boss.ResetSpawnCooldown();
        boss.isSpawningMinions = false;

        spawnRoutine = null;

        boss.StateMachine.ChangeState<EnemyIdleState>();
    }

    public void OnUpdate() { }

    public void OnFixedUpdate() { }

    public void OnExit()
    {
        boss.isSpawningMinions = false;

        if (spawnRoutine != null)
        {
            boss.StopCoroutine(spawnRoutine);
            spawnRoutine = null;
        }
    }
}




public class SniperBossAttackState : IState
{
    private readonly SniperBoss boss;
    private float nextAttackTime;

    public SniperBossAttackState(SniperBoss boss)
    {
        this.boss = boss;
    }

    public void OnEnter()
    {
        boss.StopMovement();
        boss.isCurrentlyAttacking = false;

        nextAttackTime = Time.time;
    }

    public void OnUpdate()
    {
        if (boss.IsDead)
        {
            boss.StateMachine.ChangeState<EnemyDeadState>();
            return;
        }

        if (boss.isCurrentlyAttacking)
            return;

        Transform target = boss.GetCurrentTarget();

        if (target == null)
        {
            boss.StateMachine.ChangeState<EnemyIdleState>();
            return;
        }

        float distance = boss.GetDistanceToTarget();

        if (distance > boss.SniperAttackRange + 0.5f)
        {
            boss.StateMachine.ChangeState<EnemyChaseState>();
            return;
        }

        if (Time.time >= nextAttackTime)
        {
            boss.TryStartSniperAttack(target);

            nextAttackTime = Time.time + boss.ShotInterval;
        }
    }

    public void OnFixedUpdate()
    {
        // The sniper boss never moves while attacking.
    }

    public void OnExit()
    {
        boss.CancelSniperAttack();
        boss.StopMovement();
    }
}





public class TurretMinionAttackState : IState
{
    private readonly TurretMinion minion;

    public TurretMinionAttackState(TurretMinion minion)
    {
        this.minion = minion;
    }

    public void OnEnter()
    {
        minion.StopMovement();
    }

    public void OnUpdate()
    {
        if (minion.IsDead || minion.IsBursting)
            return;

        Transform player = minion.GetPlayerTarget();

        if (player == null)
            return;

        if (!minion.IsPlayerInAttackRange(player))
            return;

        if (Time.time >= minion.NextBurstTime)
        {
            minion.TryStartBurst(player);
        }
    }

    public void OnFixedUpdate()
    {
        minion.StopMovement();
    }

    public void OnExit()
    {
        minion.StopMovement();
    }
}

