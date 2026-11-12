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

        ApplyMeleeDamage();
        PlayEnemySound(EnemySoundType.Attack, SoundSourceType.Localized, transform);
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

        isCurrentlyAttacking = false;
        yield return new WaitForSeconds(bossData.attackCooldown);

    }

    private void DealDamageTo(GameObject target, int damage, bool isSpecialAttack = false) 
    { 
        PlayEnemySound(EnemySoundType.Impact, SoundSourceType.Localized, target.transform);
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

    public void OnSpawnMinionsAnimationEvent()
    {
        PlayEnemySound(
            EnemySoundType.Special,
            SoundSourceType.Localized,
            transform
        );

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
