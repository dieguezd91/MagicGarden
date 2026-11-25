using UnityEngine;

public class SierraProjectile : Spell
{
    private enum ProjectileState
    {
        Idle,
        Flying,
        Attached,
        Returning
    }

    [Header("PROJECTILE")]
    [SerializeField] private float speed = 12f;
    [SerializeField] private float returnSpeed = 8f;

    [Header("ATTACHMENT")]
    [SerializeField] private string gripPointName = "PlantGripPoint";
    [SerializeField] private Vector3 fallbackAttachOffset = Vector3.zero;

    [Header("VFX")]
    [SerializeField] private ParticleSystem attackParticles;

    [Header("SAW SOUND")]
    [Tooltip("Loops while the saw is grinding an enemy. Must have Loop on.")]
    [SerializeField] private SoundClipData grindLoopSound;
    [Tooltip("The impact (bite) sound plays on contact and again every this many seconds while grinding.")]
    [SerializeField] private float biteInterval = 0.6f;
    private AudioSource grindSource;
    private float grindVolume;
    private float nextBiteTime;

    private ProjectileState state = ProjectileState.Idle;

    private SierraPlant ownerPlant;
    private Transform returnPoint;

    private Transform initialTarget;
    private LifeController attachedTargetLife;

    private Vector2 direction;

    private float damagePerSecond;
    private float attackDuration;
    private float maxTravelDistance;
    private float distanceTravelled;
    private float attachedTime;

    private Rigidbody2D projectileBody;
    private bool originalBodySimulation;

    private bool ownerNotified;

    protected override void Awake()
    {
        projectileBody = GetComponent<Rigidbody2D>();

        if (projectileBody != null)
            originalBodySimulation = projectileBody.simulated;

        lifeTime = 0f;
        base.Awake();
    }

    public void Initialize(
        SierraPlant owner,
        Transform returnTransform,
        Transform target,
        float damage,
        float duration,
        float maxDistance,
        float projectileSpeed,
        float projectileReturnSpeed,
        string gripName,
        Vector3 fallbackOffset)
    {
        ownerPlant = owner;
        returnPoint = returnTransform;
        initialTarget = target;

        damagePerSecond = damage;
        attackDuration = duration;
        maxTravelDistance = maxDistance;

        speed = projectileSpeed;
        returnSpeed = projectileReturnSpeed;

        gripPointName = gripName;
        fallbackAttachOffset = fallbackOffset;
    }

    public override void Cast(Vector2 castDirection, Vector3 spawnPosition)
    {
        transform.position = spawnPosition;

        direction = castDirection.normalized;
        distanceTravelled = 0f;
        attachedTime = 0f;

        state = direction == Vector2.zero
            ? ProjectileState.Returning
            : ProjectileState.Flying;

        RotateTowardsDirection();
    }

    private void Update()
    {
        if (IsOwnGrindSource())
            grindSource.volume = PlantSoundBase.GameFrozen ? 0f : grindVolume;

        if (UpgradeRuntime.GameplayBlocked)
            return;

        switch (state)
        {
            case ProjectileState.Flying:
                UpdateFlying();
                break;

            case ProjectileState.Attached:
                UpdateAttached();
                break;

            case ProjectileState.Returning:
                UpdateReturning();
                break;
        }
    }

    private void UpdateFlying()
    {
        if (initialTarget == null)
        {
            BeginReturn();
            return;
        }

        Vector2 targetDirection =
            (Vector2)initialTarget.position - (Vector2)transform.position;

        if (targetDirection.sqrMagnitude > 0.001f)
            direction = targetDirection.normalized;

        float movement = speed * Time.deltaTime;

        transform.position += (Vector3)(direction * movement);
        distanceTravelled += movement;

        RotateTowardsDirection();

        if (maxTravelDistance > 0f &&
            distanceTravelled >= maxTravelDistance)
        {
            BeginReturn();
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (state != ProjectileState.Flying)
            return;

        Transform enemyRoot = FindEnemyRoot(collision.transform);

        if (enemyRoot == null)
            return;

        LifeController life = collision.GetComponentInParent<LifeController>();

        if (life == null || !life.IsAlive())
            return;

        AttachToEnemy(life, enemyRoot);
    }

    private Transform FindEnemyRoot(Transform current)
    {
        while (current != null)
        {
            if (current.CompareTag("Enemy"))
                return current;

            current = current.parent;
        }

        return null;
    }

    private void AttachToEnemy(LifeController life, Transform enemyRoot)
    {
        if (life == null || !life.IsAlive())
        {
            BeginReturn();
            return;
        }

        attachedTargetLife = life;
        initialTarget = null;

        Transform gripPoint = FindChildRecursive(enemyRoot, gripPointName);

        Transform attachParent = gripPoint != null
            ? gripPoint
            : enemyRoot;

        if (projectileBody != null)
        {
            projectileBody.velocity = Vector2.zero;
            projectileBody.angularVelocity = 0f;
            projectileBody.simulated = false;
        }

        transform.SetParent(attachParent, false);

        if (gripPoint != null)
            transform.localPosition = Vector3.zero;
        else
            transform.localPosition = fallbackAttachOffset;

        transform.localRotation = Quaternion.identity;

        attachedTime = 0f;
        state = ProjectileState.Attached;

        if (attackParticles != null)
            attackParticles.Play();

        PlayImpactSound();
        nextBiteTime = Time.time + biteInterval;
        StartGrindLoop();
    }

    private void StartGrindLoop()
    {
        StopGrindLoop();
        if (SoundManager.Instance == null || grindLoopSound == null || grindLoopSound.GetClip() == null) return;
        grindSource = SoundManager.Instance.PlayClip(grindLoopSound, SoundSourceType.Localized, transform);
        if (grindSource != null)
        {
            grindSource.loop = true;
            grindVolume = grindSource.volume;
        }
    }

    private void UpdateGrindSound()
    {
        if (IsOwnGrindSource())
        {
            Vector3 p = grindSource.transform.position;
            grindSource.transform.position = new Vector3(transform.position.x, transform.position.y, p.z);
        }

        if (biteInterval > 0f && Time.time >= nextBiteTime)
        {
            nextBiteTime = Time.time + biteInterval;
            PlayImpactSound();
        }
    }

    private bool IsOwnGrindSource() =>
        grindSource != null && grindSource.loop && grindLoopSound?.clips != null &&
        System.Array.IndexOf(grindLoopSound.clips, grindSource.clip) >= 0;

    private void StopGrindLoop()
    {
        if (IsOwnGrindSource()) grindSource.Stop();
        grindSource = null;
    }

    private void UpdateAttached()
    {
        if (attachedTargetLife == null || !attachedTargetLife.IsAlive())
        {
            BeginReturn();
            return;
        }

        float deltaTime = Time.deltaTime;

        if (damagePerSecond > 0f)
        {
            attachedTargetLife.TakeDamage(
                damagePerSecond * deltaTime,
                LifeController.DamageType.DamageOverTime
            );
        }

        UpdateGrindSound();

        attachedTime += deltaTime;

        if (attachedTime >= attackDuration)
            BeginReturn();
    }

    private void BeginReturn()
    {
        if (state == ProjectileState.Returning)
            return;

        state = ProjectileState.Returning;
        StopGrindLoop();

        attachedTargetLife = null;
        initialTarget = null;

        transform.SetParent(null, true);

        if (projectileBody != null)
        {
            projectileBody.simulated = originalBodySimulation;
            projectileBody.velocity = Vector2.zero;
            projectileBody.angularVelocity = 0f;
        }

        if (attackParticles != null)
        {
            attackParticles.Stop(
                true,
                ParticleSystemStopBehavior.StopEmittingAndClear
            );
        }

        if (returnPoint == null)
            FinishProjectile();
    }

    private void UpdateReturning()
    {
        if (returnPoint == null)
        {
            FinishProjectile();
            return;
        }

        Vector3 destination = returnPoint.position;

        transform.position = Vector3.MoveTowards(
            transform.position,
            destination,
            returnSpeed * Time.deltaTime
        );

        if (Vector3.Distance(transform.position, destination) <= 0.08f)
            FinishProjectile();
    }

    private void FinishProjectile()
    {
        NotifyOwner();
        Destroy(gameObject);
    }

    private void NotifyOwner()
    {
        if (ownerNotified)
            return;

        ownerNotified = true;

        if (ownerPlant != null)
            ownerPlant.NotifyProjectileFinished(this);

        ownerPlant = null;
    }

    private void OnDestroy()
    {
        StopGrindLoop();

        if (attackParticles != null)
        {
            attackParticles.Stop(
                true,
                ParticleSystemStopBehavior.StopEmittingAndClear
            );
        }

        NotifyOwner();
    }

    private void RotateTowardsDirection()
    {
        if (direction == Vector2.zero)
            return;

        float angle =
            Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;

        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    private Transform FindChildRecursive(Transform parent, string childName)
    {
        if (parent == null)
            return null;

        foreach (Transform child in parent)
        {
            if (child.name == childName)
                return child;

            Transform result = FindChildRecursive(child, childName);

            if (result != null)
                return result;
        }

        return null;
    }
}