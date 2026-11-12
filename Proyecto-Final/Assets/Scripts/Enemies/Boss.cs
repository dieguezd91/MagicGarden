using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class Boss : EnemyBase
{
    [SerializeField] public BossEnemyDataSO bossData;

    [Header("Combat References")]
    [SerializeField] private Transform attackPoint;
    [SerializeField] private Transform[] minionSpawnPoints;
    private Coroutine currentAttackCoroutine;

    [Header("MINION WAVES")]
    [SerializeField] private float minionCleanupDelay = 0.25f;

    public float MinionCleanupDelay => minionCleanupDelay;

    protected virtual bool UseTimedMinionWaves => false;

    public Transform[] MinionSpawnPoints => minionSpawnPoints;
    public GameObject MinionPrefab => minionPrefab;
    public List<GameObject> CurrentMinions => currentMinions;

    public bool isSpawningMinions { get; set; } = false;

    private float spawnCooldownTimer = 0f;

    private float minAttackDistance;
    private float spawnDelayAfterMinionsDie;
    private GameObject minionPrefab;

    private List<GameObject> currentMinions = new List<GameObject>();
    private HashSet<GameObject> meleeDamagedObjects = new HashSet<GameObject>(); 
    private HashSet<GameObject> specialDamagedObjects = new HashSet<GameObject>();

    protected override EnemyDataSO GetEnemyData() => bossData;

    protected override void LoadEnemyData()
    {
        base.LoadEnemyData();

        if (bossData != null)
        {
            minAttackDistance = bossData.MinAttackDistance;
            spawnDelayAfterMinionsDie = bossData.SpawnDelayAfterMinionsDie;
            minionPrefab = bossData.MinionPrefab;

            if (lifeController != null)
            {
                lifeController.maxHealth = bossData.BossMaxHealth;
                lifeController.currentHealth = bossData.BossMaxHealth;
            }
        }
    }

    protected override void Start()
    {
        base.Start(); 

        if (StateMachine == null)
        {
            Debug.LogError("StateMachine es null en Boss!");
            return;
        }

        StateMachine.RegisterState(new EnemySpawnMinionState(this));
    }

    protected override void InitializeEnemy()
    {
        base.InitializeEnemy();

        if (StateMachine == null)
        {
            Debug.LogError($"{name} → StateMachine is null in InitializeEnemy()");
            return;
        }

        if (lifeController != null)
        {
            lifeController.onDeath.AddListener(OnBossDeath);
        }
        else
        {
            Debug.LogWarning($"{name} → LifeController not found in Boss");
        }

        StateMachine.ChangeState<EnemyIdleState>();

    }

    protected override void Update()
    {
        base.Update();
        if (isDead) return;

        UpdateMinionManagement();
    }

    private void UpdateMinionManagement()
    {
        currentMinions.RemoveAll(m => m == null);

        if (isDead || isCurrentlyAttacking || isSpawningMinions)
            return;

        if (UseTimedMinionWaves)
        {
            spawnCooldownTimer += Time.deltaTime;

            if (spawnCooldownTimer >= spawnDelayAfterMinionsDie)
            {
                StateMachine.ChangeState<EnemySpawnMinionState>();
            }
        }
        else
        {
            // Original behavior for the other bosses.
            if (currentMinions.Count == 0)
            {
                spawnCooldownTimer += Time.deltaTime;

                if (spawnCooldownTimer >= spawnDelayAfterMinionsDie)
                {
                    StateMachine.ChangeState<EnemySpawnMinionState>();
                }
            }
        }
    }

    public void ResetSpawnCooldown()
    {
        spawnCooldownTimer = 0f;
    }

    private void OnBossDeath()
    {
        if (isDead) return;

        isDead = true;
        rb.velocity = Vector2.zero;
        PlayEnemySound(EnemySoundType.Die);

        KillAllMinions();
        StateMachine.ChangeState<EnemyDeadState>();
    }

    public void KillAllMinions()
    {
        foreach (GameObject minion in currentMinions)
        {
            if (minion == null)
                continue;

            LifeController life = minion.GetComponent<LifeController>();

            if (life != null)
                life.Die();
            else
                Destroy(minion);
        }

        currentMinions.Clear();
    }

    protected override void ProcessMovement()
    {
        throw new System.NotImplementedException();
    }

    public void TryStartMeleeAttack()
    {
        if (currentAttackCoroutine == null)
            currentAttackCoroutine = StartCoroutine(PerformMeleeAttack());
    }

    public IEnumerator PerformMeleeAttack()
    {
        isCurrentlyAttacking = true;
        rb.velocity = Vector2.zero;
        animator.SetTrigger("attack");

        // Damage + impact land on the thrust frame via the "Golem Attack" OnAttackStrike event (0.68 s),
        // not here at the start of the wind-up.
        pendingImpactAt = null;
        yield return new WaitForSeconds(1f);

        isCurrentlyAttacking = false;
        currentAttackCoroutine = null;
    }

    private void ApplyMeleeDamage()
    {
        meleeDamagedObjects.Clear();

        Collider2D[] hits = Physics2D.OverlapCircleAll(attackPoint.position, bossData.meleeRadius);
        foreach (Collider2D hit in hits)
        {
            if (!meleeDamagedObjects.Contains(hit.gameObject))
            {
                DealDamageTo(hit.gameObject, bossData.meleeDamage);
                meleeDamagedObjects.Add(hit.gameObject);
            }
        }
    }

    public IEnumerator PerformSpecialAttack()
    {
        if (isCurrentlyAttacking) yield break;
        isCurrentlyAttacking = true;

        rb.velocity = Vector2.zero;
        animator.SetTrigger("attackSpecial");
        yield return new WaitForSeconds(bossData.SpecialDelay);

        specialDamagedObjects.Clear();

        Collider2D[] hits = Physics2D.OverlapCircleAll(attackPoint.position, bossData.SpecialRadius);

        foreach (Collider2D hit in hits)
        {
            if (!specialDamagedObjects.Contains(hit.gameObject))
            {
                DealDamageTo(hit.gameObject, bossData.SpecialDamage, true);
                specialDamagedObjects.Add(hit.gameObject);
            }
        }
        PlayGroundSlam(); // the special attack has no animation state; the slam makes it readable

        isCurrentlyAttacking = false;
        yield return new WaitForSeconds(bossData.attackCooldown);

    }

    private void DealDamageTo(GameObject target, int damage, bool isSpecialAttack = false) 
    { 
        // One impact per attack, at the first thing hit; played on the thrust frame (OnAttackStrike).
        if (!isSpecialAttack && pendingImpactAt == null && (target.CompareTag("Player") || target.CompareTag("Plant") || target.CompareTag("Home")))
            pendingImpactAt = target.transform;
        if (target.CompareTag("Player") || target.CompareTag("Plant")) 
        { 
            LifeController life = target.GetComponent<LifeController>(); 
            if (life != null) 
            { 
                life.TakeDamage(damage); 
                if (target.CompareTag("Player")) 
                { 
                    float shakeIntensity = isSpecialAttack ? 0.5f : 0.3f; 
                    CameraShaker.Instance?.Shake(shakeIntensity, shakeIntensity); 
                } 
            } 
        } 
        else if (target.CompareTag("Home")) 
        { 
            HouseLifeController home = target.GetComponent<HouseLifeController>(); 
            if (home != null) 
            { 
                home.TakeDamage(damage); 
            } 
        } 
    }

    [Header("Boss Sound")]
    [Tooltip("Crystal driven into the ground: summon burst and the special area attack.")]
    [SerializeField] private SoundClipData groundSlamSound;
    private Transform pendingImpactAt;

    protected override bool UsesAnimationFootsteps => true; // "Golem Walk" marks both footfalls
    protected override float HurtSoundInterval => 0.9f;     // Boss_Hurt is a 2 s clip; faster re-triggers piled up

    // "Golem Spawn Minion" events. The Special clip (Boss_Attack_Invoke) is authored as a swell that peaks on the
    // plunge/burst, so it starts on frame 1; the slam only adds weight under the plunge.
    public void PlaySummonSound() => PlayEnemySound(EnemySoundType.Special, SoundSourceType.Localized, transform);
    public void PlaySummonPlunge() => PlayGroundSlam();

    private void PlayGroundSlam() => SoundManager.Instance?.PlayClip(groundSlamSound, SoundSourceType.Localized, transform);

    // Animation event on "Golem Attack" frame 1: the Attack clip (Boss_Attack_Melee) is a charge swell
    // authored to peak on the thrust (~0.55 s), so it must start with the animation.
    public void PlayAttackSwing() => PlayEnemySound(EnemySoundType.Attack, SoundSourceType.Localized, transform);

    // Animation event on "Golem Attack" (0.68 s): the crystal thrust lands, so damage and impact happen now.
    public void OnAttackStrike()
    {
        if (!isCurrentlyAttacking || isDead) return;
        pendingImpactAt = null;
        ApplyMeleeDamage();
        if (pendingImpactAt != null)
            PlayEnemySound(EnemySoundType.Impact, SoundSourceType.Localized, pendingImpactAt);
        pendingImpactAt = null;
    }

    public void OnSpawnMinionsAnimationEvent()
    {
        // Summon sounds are their own events (PlaySummonPlunge/Rumble/Burst) so they follow the animation.

        if (isSpawningMinions && !isDead)
        {
            SpawnMinions();
        }
    }

    private void SpawnMinions()
    {
        if (isDead)
            return;

        foreach (Transform spawnPoint in minionSpawnPoints)
        {
            if (spawnPoint == null || minionPrefab == null)
                continue;

            GameObject minion = Instantiate(
                minionPrefab,
                spawnPoint.position,
                Quaternion.identity
            );

            currentMinions.Add(minion);
        }
    }
}
