using System.Collections;
using UnityEngine;

public class FireSpell : Spell
{
    [Header("FLAMETHROWER SETTINGS")]
    [SerializeField] private float duration = 2f;
    [SerializeField] private float fireRate = 0.05f;
    [SerializeField] private float projectileSpeed = 12f;
    [SerializeField] private float projectileLifeTime = 0.5f;

    [Header("DAMAGE SETTINGS")]
    [SerializeField] private float damagePerHit = 5f;

    [Header("FLAMETHROWER SOUND")]
    [Tooltip("Must have Loop on. Plays while the stream is active (castSound is the ignition).")]
    [SerializeField] private SoundClipData flameLoopSound;
    [SerializeField] private SoundClipData flameEndSound;
    [SerializeField] private float hitSoundInterval = 0.12f;

    [Header("HIT FEEDBACK")]
    [Tooltip("Show the floating damage number on each hit. Ticks add up into one number per enemy. Off = no numbers.")]
    [SerializeField] private bool showDamageNumbers = true;
    [Tooltip("Spawn impact particles on hit (uses the Impact Particles Prefab). Off = no particles.")]
    [SerializeField] private bool spawnImpactParticles = true;
    [Tooltip("Minimum seconds between impact particle bursts, shared by every flame (the stream ticks ~20 times per second). 0 = a burst on every tick.")]
    [SerializeField] private float impactParticleInterval = 0.12f;
    [Tooltip("Knockback pushed on a burning enemy per tick, along the flame direction. Small on purpose: it repeats every tick. 0 = no knockback.")]
    [SerializeField] private float hitKnockback = 3f;

    private AudioSource flameLoopSource;
    // Shared by every flame projectile: ~20 flames/s would otherwise stack sizzles.
    private static float lastHitSoundTime = float.MinValue;
    // Same rule for the impact particles: one burst per interval for the whole stream.
    private static float lastImpactParticlesTime = float.MinValue;

    // A tick every 0.05 s: a hitstop or camera shake per tick would freeze/shake the screen continuously.
    protected override bool AllowsHitStop => false;
    protected override bool AllowsImpactShake => false;

    public override MagicGarden.Target DescribeUpgradeTarget(string id, string displayName)
    {
        var target = new MagicGarden.Target { Id = id, Name = displayName, Capability = GetType().Name };
        target.Bases[MagicGarden.Stat.Damage] = damagePerHit;
        target.Bases[MagicGarden.Stat.Range] = projectileSpeed * projectileLifeTime;
        return target;
    }
    private bool isActive = false;
    private int spellSlotIndex = -1;
    private PlayerSpellController playerSpellController;
    private Rigidbody2D rb;

    [HideInInspector] public bool isProjectile = false;

    protected override void Awake()
    {
        // Intentionally do not call base.Awake: FireRoutine and ProjectileLifetime
        // own flame lifetimes; the generic lifetime could destroy the controller early.
        rb = GetComponent<Rigidbody2D>();
    }


    public override void Cast(Vector2 direction, Vector3 spawnPosition)
    {
        transform.position = spawnPosition;

        if (isActive)
            return;

        isActive = true;

        if (!isProjectile)
        {
            
            if (playerSpellController == null)
            {
                playerSpellController = FindObjectOfType<PlayerSpellController>();
            }

            if (playerSpellController != null)
            {
                playerSpellController.SetFireSpellActive(true);

                transform.SetParent(playerSpellController.transform, true); 
            }

            if (rb != null)
            {
                rb.isKinematic = true;
                rb.velocity = Vector2.zero;
            }

            StartCoroutine(FireRoutine());
        }
        else
        {

            if (rb != null)
            {
                rb.isKinematic = false;
                rb.velocity = direction.normalized * projectileSpeed;
            }

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
            transform.rotation = Quaternion.Euler(0, 0, angle);

            StartCoroutine(ProjectileLifetime());
        }
    }

    private IEnumerator ProjectileLifetime()
    {
        float elapsed = 0;
        while (elapsed < Upgraded(MagicGarden.Stat.Range, projectileLifeTime))
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
        Destroy(gameObject);
    }

    public void SetSpellSlotIndex(int slotIndex)
    {
        spellSlotIndex = slotIndex;
    }

    private IEnumerator FireRoutine()
    {
        float timer = 0f;
        Camera mainCam = Camera.main;
        flameLoopSource = SoundManager.Instance?.PlayClip(flameLoopSound);

        while (timer < duration)
        {
            Vector3 mousePos = mainCam.ScreenToWorldPoint(Input.mousePosition);
            mousePos.z = 0f;
            Vector2 currentMouseDirection = (mousePos - transform.position).normalized;

            GameObject flameProj = Instantiate(gameObject, transform.position, Quaternion.identity);
            flameProj.transform.SetParent(null);

            FireSpell flameScript = flameProj.GetComponent<FireSpell>();
            flameScript.SetUpgradeTarget(UpgradeTargetId, UpgradeCapability);
            flameScript.isProjectile = true;
            flameScript.isActive = false;

            flameScript.Cast(currentMouseDirection, transform.position);

            yield return new WaitForSeconds(fireRate);
            timer += fireRate;
        }

        if (playerSpellController != null)
            playerSpellController.SetFireSpellActive(false);

        StopFlameLoop();
        SoundManager.Instance?.PlayClip(flameEndSound, SoundSourceType.Localized, transform);
        StartCooldown();
        Destroy(gameObject);
    }

    private void StopFlameLoop()
    {
        // The pooled source may already have been stopped and reused for another clip (e.g. StopAll on scene change).
        if (flameLoopSource != null && flameLoopSource.loop && flameLoopSource.clip == flameLoopSound?.GetClip())
            flameLoopSource.Stop();
        flameLoopSource = null;
    }

    private void StartCooldown()
    {
        if (SpellInventory.Instance == null || spellSlotIndex < 0 || isProjectile)
            return;

        SpellInventory.Instance.StartCooldown(spellSlotIndex);
    }

    private void OnDestroy()
    {
        if (!isProjectile && playerSpellController != null)
            playerSpellController.SetFireSpellActive(false);
        if (!isProjectile)
            StopFlameLoop();
    }

    // Particles only, throttled. The impact sound keeps its own throttle in OnTriggerEnter2D and
    // the base camera shake is deliberately skipped (see AllowsImpactShake).
    protected override void PlayImpactEffects(Vector3 position)
    {
        if (!spawnImpactParticles || impactParticlesPrefab == null) return;

        // Time.time < last covers a stale static after an editor play session without domain reload.
        if (Time.time - lastImpactParticlesTime < impactParticleInterval && Time.time >= lastImpactParticlesTime) return;
        lastImpactParticlesTime = Time.time;

        var particles = Instantiate(impactParticlesPrefab, position, Quaternion.identity);
        var ps = particles.GetComponent<ParticleSystem>();
        Destroy(particles, ps != null ? ps.main.duration : 1.5f);
    }

    protected override void ApplyKnockback(Collider2D target)
    {
        if (hitKnockback <= 0f) return;

        var knockback = target.GetComponent<KnockbackReceiver>();
        if (knockback == null) return;

        Vector2 direction = rb != null && rb.velocity.sqrMagnitude > 0.0001f
            ? rb.velocity.normalized
            : (Vector2)(target.transform.position - transform.position);
        knockback.ApplyKnockback(direction, Upgraded(MagicGarden.Stat.Knockback, hitKnockback));
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!isActive || !isProjectile)
            return;

        if (collision.CompareTag("Enemy"))
        {
            LifeController life = collision.GetComponent<LifeController>();

            if (life != null && life.IsAlive())
            {
                float damage = Upgraded(MagicGarden.Stat.Damage, damagePerHit);

                if (RitualBuffManager.Instance != null)
                {
                    damage *= RitualBuffManager.Instance.GetDamageMultiplier();
                }

                life.TakeDamage(damage);

                if (showDamageNumbers)
                    ShowDamageText(collision.transform, damage);
                PlayImpactEffects(collision.transform.position);
                ApplyKnockback(collision);

                // Time.time < last covers a stale static after an editor play session without domain reload.
                if (Time.time - lastHitSoundTime >= hitSoundInterval || Time.time < lastHitSoundTime)
                {
                    lastHitSoundTime = Time.time;
                    PlayImpactSound();
                }
            }
        }
    }
}