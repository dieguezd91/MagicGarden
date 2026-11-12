using UnityEngine;

public abstract class EnemyBase : MonoBehaviour, IEnemy
{
    [Header("Visual")]
    [SerializeField] protected SpriteRenderer spriteRenderer;
    [SerializeField] protected bool hasDetectedDefaultFacing = false;
    [SerializeField] protected bool facesRightByDefault = false;
    [SerializeField] private bool invertSpriteDirectionForBoss = false;


    protected float playerPriority;
    protected float plantPriority;
    protected float homePriority;
    protected float moveSpeed;
    protected float detectionRange;
    protected float footstepCooldown;
    private float slowMultiplier = 1f;

    [SerializeField] protected LayerMask plantLayer;

    protected Rigidbody2D rb;
    public Animator animator;
    protected EnemySoundBase soundBase;
    protected LifeController lifeController;
    protected KnockbackReceiver knockbackReceiver;

    protected Transform currentTarget;
    public string currentTargetType = "none";
    public bool isDead = false;
    protected float lastFootstepTime = 0f;

    protected Transform overrideTarget = null;
    protected bool hasOverrideTarget = false;

    public StateMachine StateMachine { get; protected set; }

    public bool isCurrentlyAttacking = false;
    public float nextAttackTime = 0f;
    public float attackCooldown;

    private float nextIdleSoundTime = 0f;
    [SerializeField] private float idleSoundMinTime = 5f;
    [SerializeField] private float idleSoundMaxTime = 15f;


    #region Exposed properties (para estados)
    public float MoveSpeed => moveSpeed * slowMultiplier; 
    public Animator Animator => animator;

    public bool IsDead => isDead;
    #endregion

    #region Unity Lifecycle
    protected virtual void Awake()
    {
        CacheComponents();
        LoadEnemyData();
    }

    protected virtual void Start()
    {
        StateMachine = new StateMachine();
        StateMachine.RegisterState(new EnemyIdleState(this));
        StateMachine.RegisterState(new EnemyChaseState(this));
        StateMachine.RegisterState(new EnemyAttackState(this));
        StateMachine.RegisterState(new EnemyDeadState(this));
        StateMachine.ChangeState<EnemyIdleState>();

        InitializeEnemy();
        PlaySpawnSound();
        ScheduleNextIdleSound();

        // Debug: print idle sound setup status
        if (soundBase == null)
            soundBase = GetComponent<EnemySoundBase>();
        var idleClip = soundBase?.GetSound(EnemySoundType.Idle);
        //if (idleClip == null || idleClip.GetClip() == null)
        //    Debug.LogWarning($"[{gameObject.name}] EnemyBase: Idle sound is NOT assigned. Assign a clip in EnemySoundBase to enable idle sounds.");
        //else
        //    Debug.Log($"[{gameObject.name}] EnemyBase: Idle sound is assigned and will play.");
    }

    protected virtual void Update()
    {
        if (isDead || UpgradeRuntime.GameplayBlocked) return;

        UpdateTargeting();
        StateMachine.Tick();
        UpdateIdleSoundTimer(); // Check idle sound timer
    }

    protected virtual void FixedUpdate()
    {
        if (isDead || UpgradeRuntime.GameplayBlocked || IsBeingKnockedBack()) return;

        StateMachine.FixedTick();
    }

    protected virtual void OnDestroy()
    {
        CleanupListeners();
    }
    #endregion

    #region Initialization
    private void CacheComponents()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        soundBase = GetComponent<EnemySoundBase>();
        lifeController = GetComponent<LifeController>();
        knockbackReceiver = GetComponent<KnockbackReceiver>();

        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();
    }

    protected abstract EnemyDataSO GetEnemyData();

    protected virtual void LoadEnemyData()
    {
        EnemyDataSO data = GetEnemyData();

        if (data == null)
        {
            Debug.LogWarning($"[{gameObject.name}] No EnemyDataSO assigned!");
            return;
        }

        playerPriority = data.PlayerPriority;
        plantPriority = data.PlantPriority;
        homePriority = data.HomePriority;
        moveSpeed = data.MoveSpeed;
        detectionRange = data.DetectionRange;
        footstepCooldown = data.FootstepCooldown;

        if (lifeController != null)
        {
            lifeController.maxHealth = data.MaxHealth;
            lifeController.currentHealth = data.MaxHealth;
            lifeController.manaDropChance = data.ManaDropChance;
            lifeController.ConfigureExperienceDrop(data.ExperienceReward, data.EnemyIcon);
        }
    }

    protected virtual void InitializeEnemy()
    {
        if (lifeController != null)
        {
            lifeController.onDamaged.AddListener(OnDamaged);
        }
    }

    private void CleanupListeners()
    {
        if (lifeController != null)
        {
            lifeController.onDamaged.RemoveListener(OnDamaged);
        }
    }
    #endregion

    #region Targeting
    public Transform GetCurrentTarget()
    {
        return hasOverrideTarget ? overrideTarget : currentTarget;
    }

    protected virtual void UpdateTargeting()
    {
        if (hasOverrideTarget && overrideTarget != null)
        {
            currentTarget = overrideTarget;
            currentTargetType = "override";
            return;
        }

        FindClosestTarget();
    }

    protected void FindClosestTarget()
    {
        float closestScore = Mathf.Infinity;
        Transform bestTarget = null;
        string bestTargetType = "none";

        EvaluatePlayerAsTarget(ref closestScore, ref bestTarget, ref bestTargetType);
        EvaluatePlantsAsTargets(ref closestScore, ref bestTarget, ref bestTargetType);
        EvaluateHomeAsTarget(ref closestScore, ref bestTarget, ref bestTargetType);

        currentTarget = bestTarget;
        currentTargetType = bestTargetType;
    }

    private void EvaluatePlayerAsTarget(ref float closestScore, ref Transform bestTarget, ref string bestTargetType)
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj == null) return;

        LifeController playerLife = playerObj.GetComponent<LifeController>();
        if (playerLife == null || !playerLife.IsTargetable()) return;

        float distance = Vector2.Distance(transform.position, playerObj.transform.position);
        float score = distance / playerPriority;

        if (distance <= detectionRange && score < closestScore)
        {
            closestScore = score;
            bestTarget = playerObj.transform;
            bestTargetType = "player";
        }
    }

    private void EvaluatePlantsAsTargets(ref float closestScore, ref Transform bestTarget, ref string bestTargetType)
    {
        if (plantPriority <= 0f) return;

        Collider2D[] plants = Physics2D.OverlapCircleAll(transform.position, detectionRange, plantLayer);

        foreach (Collider2D plantCollider in plants)
        {
            Plant plant = plantCollider.GetComponent<Plant>();
            if (plant == null) continue;

            float distance = Vector2.Distance(transform.position, plantCollider.transform.position);
            float score = distance / plantPriority;

            if (score < closestScore)
            {
                closestScore = score;
                bestTarget = plantCollider.transform;
                bestTargetType = "plant";
            }
        }
    }

    private void EvaluateHomeAsTarget(ref float closestScore, ref Transform bestTarget, ref string bestTargetType)
    {
        GameObject homeObj = GameObject.FindGameObjectWithTag("Home");
        if (homeObj == null) return;

        float distance = Vector2.Distance(transform.position, homeObj.transform.position);
        float score = distance / homePriority;

        if (distance <= detectionRange && score < closestScore)
        {
            closestScore = score;
            bestTarget = homeObj.transform;
            bestTargetType = "home";
        }
    }

    public void SetTargetOverride(Transform newTarget)
    {
        overrideTarget = newTarget;
        hasOverrideTarget = newTarget != null;
    }

    public void ClearTargetOverride()
    {
        overrideTarget = null;
        hasOverrideTarget = false;
    }
    #endregion

    #region Movement
    protected abstract void ProcessMovement();

    public void MoveTowardsTarget(Vector2 direction, float speed)
    {
        rb.MovePosition(rb.position + direction * speed * Time.fixedDeltaTime);
        SetMovementAnimation(true);
        PlayFootstepSound();
        UpdateSpriteDirection(direction);
    }

    public void StopMovement()
    {
        rb.velocity = Vector2.zero;
        SetMovementAnimation(false);
    }
    #endregion

    protected void UpdateSpriteDirection(Vector2 direction)
    {
        if (Mathf.Abs(direction.x) < 0.1f) return;

        if (!hasDetectedDefaultFacing)
        {
            facesRightByDefault = !spriteRenderer.flipX;
            hasDetectedDefaultFacing = true;
        }

        bool movingRight = direction.x > 0;
        bool shouldFlip = false;

        if (facesRightByDefault)
            shouldFlip = !movingRight;
        else
            shouldFlip = movingRight;

        spriteRenderer.flipX = shouldFlip;
    }



    protected virtual void SetMovementAnimation(bool isMoving)
    {
        if (animator != null)
            animator.SetBool("IsMoving", isMoving);
    }

    protected void PlaySpawnSound()
    {
        PlayEnemySound(EnemySoundType.Spawning, SoundSourceType.Localized);
    }

    protected void PlayFootstepSound()
    {
        if (Time.time - lastFootstepTime < footstepCooldown) return;

        PlayEnemySound(EnemySoundType.Steps, SoundSourceType.Localized);
        lastFootstepTime = Time.time;
    }

    // Centralized sound helper — logs warnings if EnemySoundBase or sound clip is missing
    protected void PlayEnemySound(EnemySoundType soundType, SoundSourceType sourceType = SoundSourceType.Global, Transform parent = null)
    {
        Debug.Log("Sonidoooooo");
        if (soundBase == null)
        {
            soundBase = GetComponent<EnemySoundBase>();
            if (soundBase == null)
            {
                Debug.LogWarning($"[{gameObject.name}] EnemyBase: Missing EnemySoundBase component. Cannot play '{soundType}'.");
                return;
            }
        }

        // EnemySoundBase will itself log detailed warnings if the clip is null or SoundManager is missing
        soundBase.PlaySound(soundType, sourceType, parent == null ? transform : parent);
    }

    protected virtual void OnDamaged(float damage, LifeController.DamageType damageType)
    {
        //Debug.Log($"[EnemyBase] {gameObject.name} OnDamaged invoked. Damage={damage} Type={damageType}");

        // Saw/trails damage every frame and auras every 0.1s; without this the hurt sound restarts constantly.
        if (Time.time < nextHurtSoundTime) return;
        nextHurtSoundTime = Time.time + (damageType == LifeController.DamageType.DamageOverTime ? 0.5f : 0.2f);

        PlayEnemySound(EnemySoundType.Hurt, SoundSourceType.Localized, transform);
    }

    private float nextHurtSoundTime;

    // Called when LifeController invokes onDeath
    private void OnLifeDeath()
    {
        Debug.Log($"[EnemyBase] {gameObject.name} OnLifeDeath invoked.");

        // Ensure MarkAsDead is called so EnemyBase handles death logic and audio
        MarkAsDead();
    }

    public virtual void MarkAsDead()
    {
        if (isDead) return;

        isDead = true;
        rb.velocity = Vector2.zero;
        rb.isKinematic = true;

        PlayEnemySound(EnemySoundType.Die, SoundSourceType.Localized, transform);

        animator.SetTrigger("Death");
        StateMachine.ChangeState<EnemyDeadState>();
    }

    #region Utility
    protected bool IsBeingKnockedBack()
    {
        return knockbackReceiver?.IsBeingKnockedBack() == true;
    }

    public void ApplySlow(float slowAmount)
    {
        slowMultiplier = Mathf.Clamp01(1f - slowAmount);
    }

    public void RemoveSlow()
    {
        slowMultiplier = 1f;
    }

    public float GetDistanceToTarget()
    {
        return currentTarget != null
            ? Vector2.Distance(transform.position, currentTarget.position)
            : Mathf.Infinity;
    }
    #endregion

    private void UpdateIdleSoundTimer()
    {
        if (Time.time >= nextIdleSoundTime && !isDead)
        {
            PlayIdleSound();
            ScheduleNextIdleSound();
        }
    }

    private void ScheduleNextIdleSound()
    {
        nextIdleSoundTime = Time.time + Random.Range(idleSoundMinTime, idleSoundMaxTime);
    }

    private void PlayIdleSound()
    {
        // Defensive: log if idle sound is missing
        if (soundBase == null)
        {
            soundBase = GetComponent<EnemySoundBase>();
            if (soundBase == null)
            {
                Debug.LogWarning($"[{gameObject.name}] EnemyBase: Missing EnemySoundBase component. Cannot play idle sound.");
                return;
            }
        }
        var idleClip = soundBase.GetSound(EnemySoundType.Idle);
        if (idleClip == null || idleClip.GetClip() == null)
        {
            Debug.LogWarning($"[{gameObject.name}] EnemyBase: Idle sound clip is missing in EnemySoundBase. Assign a clip in the inspector.");
            return;
        }
        PlayEnemySound(EnemySoundType.Idle, SoundSourceType.Localized);
    }

    protected virtual void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        if (currentTarget != null)
        {
            Gizmos.color = GetTargetGizmoColor();
            Gizmos.DrawLine(transform.position, currentTarget.position);
        }
    }

    private Color GetTargetGizmoColor()
    {
        return currentTargetType switch
        {
            "player" => Color.blue,
            "plant" => Color.green,
            "home" => Color.magenta,
            "override" => Color.cyan,
            _ => Color.white
        };
    }

    public bool CanMove()
    {
        return !isDead && !IsBeingKnockedBack();
    }
}
