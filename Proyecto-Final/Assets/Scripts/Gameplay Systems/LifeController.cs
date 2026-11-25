using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;

[Serializable]
public class LootItem
{
    public string name;
    public GameObject itemPrefab;
    [Range(0f, 100f)] public float dropChance;
}

public class LifeController : MonoBehaviour
{
    public enum DamageType
    {
        SingleTick,
        DamageOverTime
    }

    public enum DamageElement
    {
        Normal,
        Fire,
        Ice,
    }

    // God mode cheat (CheatManager): protects the player and the house. Always false in release builds.
    public static bool PlayerInvulnerable;

    [Header("HEALTH SETTINGS")]
    public float maxHealth = 100f;
    public float currentHealth;

    [Header("VISUAL FEEDBACK")]
    public bool flashOnDamage = true;
    public float flashDuration = 0.1f;
    public int numberOfFlashes = 3;
    public Color flashColor = new Color(1f, 0f, 0f, 0.5f);

    [Header("HIT PUNCH")]
    [Tooltip("Enemies (and plants, see Punch Plants) pop in size when they take damage. Untick to disable.")]
    public bool punchOnDamage = true;
    [Tooltip("Plants also pop in size when they take damage, using the same punch settings. Untick to keep plants still.")]
    public bool punchPlants = true;
    [Tooltip("Scale added by the punch (0.15 = +15%). 0 disables it.")]
    public float punchScale = 0.15f;
    [Tooltip("Punch duration in seconds.")]
    public float punchDuration = 0.2f;
    [Tooltip("How many times the punch oscillates.")]
    public int punchVibrato = 6;
    [Tooltip("Minimum real seconds between two punches, so rapid ticks do not shake the sprite constantly.")]
    public float punchMinInterval = 0.1f;
    [Tooltip("Transform that gets punched. Empty = this object.")]
    [SerializeField] private Transform punchTarget;

    [Header("EVENTS")]
    public UnityEvent onDeath;
    public UnityEvent<float, float> onHealthChanged;
    public UnityEvent<float, DamageType> onDamaged;
    public DamageElement LastDamageElement { get; private set; }

    [Header("MANA DROP")]
    [SerializeField] private GameObject manaPickupPrefab;
    [SerializeField] public float manaDropChance = 1f;

    [Header("OBJECT DROP SYSTEM")]
    [SerializeField] private List<LootItem> lootTable;
    [SerializeField] private float dropScatterDistance = 0.5f;
    [SerializeField] private float explosionForce = 3f;

    [Header("AUDIO SETTINGS")]
    [SerializeField] private float dotSoundCooldown = 0.2f;

    private SpriteRenderer spriteRenderer;
    private Color originalColor;
    private bool isDead = false;
    private bool dropsSpawned;
    private int experienceDrop;
    private Sprite experienceIcon;
    public void ConfigureExperienceDrop(int amount, Sprite icon)
    {
        experienceDrop = Mathf.Max(0, amount);
        experienceIcon = icon;
    }
    [SerializeField] private bool isEnemy;
    [SerializeField] private bool isPlayer;
    [SerializeField] private bool isPlant;
    private Animator animator;
    [SerializeField] private bool hasDeathAnimation;

    private float lastDotSoundTime = 0f;

    void Start()
    {
        animator = GetComponent<Animator>();
        currentHealth = maxHealth;

        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            originalColor = spriteRenderer.color;
        }

        onHealthChanged?.Invoke(currentHealth, maxHealth);

        isPlayer = GetComponent<PlayerController>() != null;
        if (isPlayer) onHealthChanged.AddListener(UpdateLowHealthHeartbeat);
    }

    private const float LowHealthFraction = 0.25f;
    private const string HeartbeatSound = "Player_Low_Health_Heartbeat_Loop";

    private void UpdateLowHealthHeartbeat(float current, float max)
    {
        if (SoundManager.Instance == null) return;
        bool low = current > 0f && max > 0f && current / max <= LowHealthFraction;
        if (low) SoundManager.Instance.PlayLoop(HeartbeatSound); // no-op if already looping
        else SoundManager.Instance.Stop(HeartbeatSound);
    }

    public void TakeDamage(float damage, DamageType damageType = DamageType.SingleTick, DamageElement damageElement = DamageElement.Normal)
    {
        if (isDead || UpgradeRuntime.GameplayBlocked) return;
        LastDamageElement = damageElement;
        if (isPlayer && PlayerInvulnerable) return;

        currentHealth -= damage;
        currentHealth = Mathf.Max(0f, currentHealth);

        if (damage > 0)
        {
            onDamaged?.Invoke(damage, damageType);

            if (isPlayer)
            {
                // Not on the killing blow: the death voice plays instead.
                if (damageType == DamageType.SingleTick && currentHealth > 0f && TryGetComponent(out PlayerRespawnController voice))
                    voice.PlayHurtVoice();

                if (damageType == DamageType.SingleTick)
                {
                    switch (damageElement)
                    {
                        case DamageElement.Normal:
                            SoundManager.Instance.Play("PlayerHit");
                            break;
                        
                        case DamageElement.Fire:
                            SoundManager.Instance.Play("PlayerHitBurn");
                            break;

                        case DamageElement.Ice:
                            SoundManager.Instance.Play("PlayerHitBurn");
                            break;

                        default:
                            SoundManager.Instance.Play("PlayerHit");
                            break;
                    }
                }
                
                else if (damageType == DamageType.DamageOverTime)
                {
                    if (Time.time - lastDotSoundTime >= dotSoundCooldown)
                    {
                        SoundManager.Instance.Play("PlayerBurn");
                        lastDotSoundTime = Time.time;
                    }
                }
            }
            
            if (flashOnDamage && spriteRenderer != null)
            {
                StartCoroutine(FlashRoutine());
            }

            if (isEnemy || (punchPlants && isPlant)) TryPunch(); // the player never punches
        }

        onHealthChanged?.Invoke(currentHealth, maxHealth);

        if (currentHealth <= 0 && !isDead)
        {
            Die();
        }
    }

    public virtual void Die()
    {
        if (isDead) return;

        isDead = true;
        // Some gnomes destroy themselves immediately after Die instead of waiting
        // for an animation event. Spawn once here, through the existing drop path.
        if (isEnemy) Drop();

        if (isPlayer)
        {
            if (TryGetComponent(out PlayerRespawnController respawn)) respawn.PlayDeathSound();
            var playerMovementController = GetComponent<PlayerMovementController>();
            if (playerMovementController != null)
            {
                playerMovementController.SetMovementEnabled(false);
            }
        }

        foreach (var col in GetComponents<Collider2D>())
            col.enabled = false;

        onDeath?.Invoke();

        if (isEnemy)
        {
            GetComponent<IEnemy>()?.MarkAsDead();

            if (hasDeathAnimation && animator != null && animator.runtimeAnimatorController != null)
            {
                animator.SetTrigger("Death");
            }
            else
            {
                EnemyDeath();
            }
        }
        else if (isPlayer)
        {
            GetComponent<ManaSystem>()?.SetMana(0f);

            if (animator != null && animator.runtimeAnimatorController != null)
            {
                UIManager.Instance?.SetGrayscaleGhostEffect(true);
                animator.SetTrigger("Death");
                animator.SetBool("IsDead", true);

                var playerMovementController = GetComponent<PlayerMovementController>();
                if (playerMovementController != null)
                {
                    playerMovementController.SetMovementEnabled(false);
                }
                
                var rb = GetComponent<Rigidbody2D>();
                if (rb != null)
                {
                    rb.velocity = Vector2.zero;
                }
            }
            else
            {
                var playerMovementController = GetComponent<PlayerMovementController>();
                if (playerMovementController != null)
                {
                    playerMovementController.SetMovementEnabled(false);
                }
            }
        }
        else if (isPlant)
        {
            if (hasDeathAnimation && animator != null && animator.runtimeAnimatorController != null)
            {
                animator.SetTrigger("Death");
            }
            else
            {
                PlantDeath();
            }
        }
    }

    private Tween punchTween;
    private Vector3 punchBaseScale;
    private float lastPunchTime = float.NegativeInfinity;

    // Always restarts from the stored base scale, so repeated punches never leave the scale drifted.
    private void TryPunch()
    {
        if (!punchOnDamage || punchScale <= 0f || punchDuration <= 0f) return;
        var target = punchTarget != null ? punchTarget : transform;
        if (Time.unscaledTime - lastPunchTime < punchMinInterval) return;
        lastPunchTime = Time.unscaledTime;

        KillPunch();
        punchBaseScale = target.localScale;
        punchTween = target.DOPunchScale(Vector3.one * punchScale, punchDuration, Mathf.Max(1, punchVibrato), 1f)
            .SetUpdate(true).SetLink(gameObject)
            .OnComplete(() => { if (target != null) target.localScale = punchBaseScale; });
    }

    private void KillPunch()
    {
        if (punchTween == null || !punchTween.IsActive()) { punchTween = null; return; }
        var target = punchTarget != null ? punchTarget : transform;
        punchTween.Kill();
        punchTween = null;
        if (target != null) target.localScale = punchBaseScale;
    }

    void OnDisable() => KillPunch();

    void OnDestroy() => KillPunch();

    IEnumerator FlashRoutine()
    {
        for (int i = 0; i < numberOfFlashes; i++)
        {
            spriteRenderer.color = flashColor;
            yield return new WaitForSeconds(flashDuration);
            spriteRenderer.color = originalColor;

            if (i < numberOfFlashes - 1)
                yield return new WaitForSeconds(flashDuration);
        }
    }

    public float GetHealthPercentage()
    {
        return currentHealth / maxHealth;
    }

    public bool IsAlive()
    {
        return !isDead;
    }

    public void Kill()
    {
        currentHealth = 0;
        TakeDamage(float.MaxValue);
    }

    public void OnDeathAnimationEnd()
    {
        if (isEnemy)
        {
            EnemyDeath();
        }
        else if (isPlayer)
        {
            var playerRespawnController = GetComponent<PlayerRespawnController>();
            if (playerRespawnController != null)
            {
                playerRespawnController.BeginRespawn();
            }
            else
            {
                Debug.LogError("PlayerRespawnController missing on player!");
            }
        }
        else if (isPlant)
        {
            PlantDeath();
        }
        else
        {
            Destroy(gameObject, 1f);
        }
    }

    private void PlantDeath()
    {
        Destroy(gameObject);
    }

    public void ConfigureAsPlant(bool hasAnimation)
    {
        isPlant = true;
        hasDeathAnimation = hasAnimation;
        isEnemy = false;
        isPlayer = false;
    }

    private void EnemyDeath()
    {
        Drop();
        Destroy(gameObject);
    }

    public void Drop()
    {
        if (!isDead || dropsSpawned) return;
        dropsSpawned = true;
        if (experienceDrop > 0)
        {
            var pickup = ExperiencePickup.Spawn(transform.position, experienceDrop, experienceIcon);
            Scatter(pickup);
        }
        if (lootTable != null && lootTable.Count > 0)
        {
            foreach (var loot in lootTable)
            {
                if (UnityEngine.Random.Range(0f, 100f) <= loot.dropChance)
                {
                    if (loot.itemPrefab != null)
                    {
                        SpawnAndExplode(loot.itemPrefab);
                    }
                }
            }
        }

        if (manaPickupPrefab != null && UnityEngine.Random.value < manaDropChance)
        {
            SpawnAndExplode(manaPickupPrefab);
        }
    }

    private void SpawnAndExplode(GameObject prefabToSpawn)
    {
        GameObject newItem = Instantiate(prefabToSpawn, transform.position, Quaternion.identity);

        Scatter(newItem);
    }

    private void Scatter(GameObject newItem)
    {
        Rigidbody2D rb = newItem.GetComponent<Rigidbody2D>();

        if (rb != null)
        {
            Vector2 randomDirection = UnityEngine.Random.insideUnitCircle.normalized;

            rb.AddForce(randomDirection * explosionForce, ForceMode2D.Impulse);
        }
    }

    public void ResetLife()
    {
        isDead = false;
        dropsSpawned = false;

        currentHealth = maxHealth;
        onHealthChanged?.Invoke(currentHealth, maxHealth);

        foreach (var col in GetComponents<Collider2D>())
            col.enabled = true;

        animator.SetBool("IsDead", false);
        animator.ResetTrigger("Death");
        animator.SetTrigger("Revive");
        UIManager.Instance?.SetGrayscaleGhostEffect(false);
    }

    public bool IsTargetable()
    {
        bool isPlayerRespawning = false;
        if (isPlayer)
        {
            var prc = GetComponent<PlayerRespawnController>();
            if (prc != null && prc.IsRespawning)
            {
                isPlayerRespawning = true;
            }
        }
        return !isPlayerRespawning && !isDead;
    }
}

