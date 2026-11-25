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

    [Header("FLAME HIT AREA (damage projectiles, not drawn)")]
    [Tooltip("The damage projectile of each tick starts small at the hand and grows while it travels. Off = it keeps the prefab scale. It only scales the trigger collider: the particles are drawn by the stream emitter (see STREAM PARTICLES).")]
    [SerializeField] private bool growWhileTraveling = true;
    [Tooltip("Hit area size at the hand, as a multiplier of the prefab scale (scales the damage collider; it follows the transform).")]
    [SerializeField, Min(0.01f)] private float flameStartScale = 0.35f;
    [Tooltip("Hit area size at the end of its travel, as a multiplier of the prefab scale.")]
    [SerializeField, Min(0.01f)] private float flameEndScale = 1.3f;
    [Tooltip("Growth over the projectile's life: time 0..1 on X, 0 = start scale and 1 = end scale on Y.")]
    [SerializeField] private AnimationCurve flameGrowthCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    [Header("STREAM PARTICLES (what is drawn)")]
    [Tooltip("On = the visible stream starts at the center of the hand sprite's mesh and follows it (unreliable when the hand frames are full-rect cells: it then lands on the player's center). Off (default) = start at PlayerSpellController.FirePoint, which is on the hand's swing path.")]
    [SerializeField] private bool streamStartsAtHand = false;
    [Tooltip("The first particle at the hand has the size of the hand (Hand World Size). Off = keep the Start Size saved on the particle system.")]
    [SerializeField] private bool startSizeMatchesHand = true;
    [Tooltip("World size (units) of the player's hand. Measured on the idle hand sprite (HandAnimation_32): 5x6 opaque pixels at 16 pixels per unit = 0.31 x 0.38 units, player and hand scale 1; 0.34 is their mean.")]
    [SerializeField, Min(0.01f)] private float handWorldSize = 0.34f;
    [Tooltip("Particle size grows exponentially with its age: Size over Lifetime = e^(k t), 1 at birth. Off = keep the Size over Lifetime curve saved on the particle system.")]
    [SerializeField] private bool exponentialGrowth = true;
    [Tooltip("Size at the end of a particle's life as a multiple of its size at the hand. The curve is e^(k t) with k = ln(multiplier), so it starts at 1 and reaches this value at t = 1.")]
    [SerializeField, Min(1.01f)] private float endSizeMultiplier = 5f;
    [Tooltip("Particles travel at Projectile Speed and live as long as the flame lifetime, so the drawn stream reaches as far as the damage. Off = keep the speed and lifetime saved on the particle system.")]
    [SerializeField] private bool streamMatchesProjectile = true;
    [Tooltip("Draw the flames at Flame Sorting Order. Off = keep the sorting saved on the particle renderer.")]
    [SerializeField] private bool applyFlameSorting = true;
    [Tooltip("Sorting order of the flame particles on their own sorting layer (Characters). Must stay BELOW the player body sprite (order 5 in Player.prefab) so flames never draw over the player.")]
    [SerializeField] private int flameSortingOrder = 3;

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
    private ParticleSystem flameParticles;
    private Collider2D flameCollider;
    private Transform firePoint;
    private Vector3 prefabScale = Vector3.one;
    private bool streaming;
    private bool fireSpellReleased;
    private Vector2 aimOverride;

    [HideInInspector] public bool isProjectile = false;

    protected override void Awake()
    {
        // Intentionally do not call base.Awake: FireRoutine and ProjectileLifetime
        // own flame lifetimes; the generic lifetime could destroy the controller early.
        rb = GetComponent<Rigidbody2D>();
        flameParticles = GetComponent<ParticleSystem>();
        flameCollider = GetComponent<Collider2D>();
        prefabScale = transform.localScale;

        if (applyFlameSorting && flameParticles != null)
        {
            var particleRenderer = flameParticles.GetComponent<ParticleSystemRenderer>();
            if (particleRenderer != null) particleRenderer.sortingOrder = flameSortingOrder;
        }
    }

    /// <summary>Where every flame spawns and keeps following from. Defaults to the PlayerSpellController fire point (the one the other projectile spells use).</summary>
    public void SetFirePoint(Transform point)
    {
        firePoint = point;
    }

    /// <summary>Aims the stream at a fixed direction instead of the mouse (zero = follow the mouse). For captures and tests.</summary>
    public void SetAimDirection(Vector2 direction)
    {
        aimOverride = direction;
    }

    // The visible hand when the stream starts at the hand, else the fire point (shared by every spell), else this object.
    private Vector3 CurrentOrigin()
    {
        if (streamStartsAtHand && playerSpellController != null && playerSpellController.TryGetHandOrigin(out Vector3 hand))
            return hand;
        return firePoint != null ? firePoint.position : transform.position;
    }

    private Vector2 AimDirection(Vector3 origin)
    {
        if (aimOverride.sqrMagnitude > 0.0001f) return aimOverride.normalized;

        Camera cam = Camera.main;
        if (cam == null) return Vector2.right;

        Vector3 mousePos = cam.ScreenToWorldPoint(Input.mousePosition);
        mousePos.z = 0f;
        Vector2 toMouse = mousePos - origin;
        return toMouse.sqrMagnitude > 0.000001f ? toMouse.normalized : Vector2.right;
    }

    /// <summary>Size over Lifetime curve e^(k t) with k = ln(endMultiplier): 1 at t = 0, endMultiplier at t = 1, slopes follow the exponential.</summary>
    public static AnimationCurve ExponentialGrowthCurve(float endMultiplier, int keyCount = 8)
    {
        float k = Mathf.Log(Mathf.Max(endMultiplier, 1.0001f));
        keyCount = Mathf.Max(keyCount, 2);
        var keys = new Keyframe[keyCount];
        for (int i = 0; i < keyCount; i++)
        {
            float t = i / (float)(keyCount - 1);
            float value = Mathf.Exp(k * t);
            keys[i] = new Keyframe(t, value, k * value, k * value);
        }
        return new AnimationCurve(keys);
    }

    // Applies the editable look to the stream emitter. World space + Shape scaling: every particle keeps the size it gets
    // from its own age (a moving or scaled transform neither drags nor resizes particles already emitted).
    private void ConfigureStreamParticles()
    {
        if (flameParticles == null) return;

        var main = flameParticles.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Shape;
        if (startSizeMatchesHand) main.startSize = handWorldSize;
        if (streamMatchesProjectile)
        {
            main.startSpeed = projectileSpeed;
            main.startLifetime = Upgraded(MagicGarden.Stat.Range, projectileLifeTime);
        }

        if (exponentialGrowth)
        {
            var size = flameParticles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, ExponentialGrowthCurve(endSizeMultiplier));
        }
    }

    // The stream emitter follows the hand and the aim every frame, not once per damage tick.
    private void LateUpdate()
    {
        if (isProjectile || !streaming) return;

        Vector3 origin = CurrentOrigin();
        Vector2 aim = AimDirection(origin);
        transform.SetPositionAndRotation(origin, Quaternion.LookRotation(new Vector3(aim.x, aim.y, 0f), Vector3.forward));
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

                if (firePoint == null)
                    firePoint = playerSpellController.FirePoint;

                transform.SetParent(playerSpellController.transform, true); 
            }

            // The controller is the visible stream: configure it, then start it clean at the hand.
            if (flameParticles != null)
            {
                flameParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ConfigureStreamParticles();
                transform.localScale = Vector3.one; // emitter only: the prefab scale is for the damage projectiles
                streaming = true;
                LateUpdate();
                flameParticles.Play(true);
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

            // Damage-only projectile: the particles are the stream emitter's, a second system here would draw stray flames.
            if (flameParticles != null)
                flameParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ApplyFlameScale(0f);
            StartCoroutine(ProjectileLifetime());
        }
    }

    // Scales the whole transform of a damage projectile: only its trigger collider is affected (the particle system is
    // stopped and the stream's particles ignore transform scale), the larger hit area is intended: a bigger flame reaches further.
    private void ApplyFlameScale(float t)
    {
        if (!growWhileTraveling) return;

        float curve = flameGrowthCurve != null ? flameGrowthCurve.Evaluate(Mathf.Clamp01(t)) : t;
        transform.localScale = prefabScale * Mathf.LerpUnclamped(flameStartScale, flameEndScale, curve);
    }

    private IEnumerator ProjectileLifetime()
    {
        float elapsed = 0;
        float life = Upgraded(MagicGarden.Stat.Range, projectileLifeTime);
        while (elapsed < life)
        {
            elapsed += Time.deltaTime;
            ApplyFlameScale(life > 0f ? elapsed / life : 1f);
            yield return null;
        }

        // Damage ends with the flame's life. Nothing is drawn by this object, so nothing visible is cut.
        isActive = false;
        if (flameCollider != null) flameCollider.enabled = false;
        Destroy(gameObject);
    }

    public void SetSpellSlotIndex(int slotIndex)
    {
        spellSlotIndex = slotIndex;
    }

    private IEnumerator FireRoutine()
    {
        float timer = 0f;
        flameLoopSource = SoundManager.Instance?.PlayClip(flameLoopSound);

        while (timer < duration)
        {
            // Every flame starts at the fire point (it follows the hand while the stream lasts), not at the player's pivot.
            Vector3 origin = CurrentOrigin();
            transform.position = origin;

            Vector2 currentMouseDirection = AimDirection(origin);

            GameObject flameProj = Instantiate(gameObject, origin, Quaternion.identity);
            flameProj.transform.SetParent(null);
            // The emitter runs at unit scale (uniform cone): the damage projectile needs the prefab scale back.
            flameProj.transform.localScale = prefabScale;

            FireSpell flameScript = flameProj.GetComponent<FireSpell>();
            flameScript.prefabScale = prefabScale;
            flameScript.SetUpgradeTarget(UpgradeTargetId, UpgradeCapability);
            flameScript.isProjectile = true;
            flameScript.isActive = false;

            flameScript.Cast(currentMouseDirection, origin);

            yield return new WaitForSeconds(fireRate);
            timer += fireRate;
        }

        streaming = false;
        ReleaseFireSpell();

        StopFlameLoop();
        SoundManager.Instance?.PlayClip(flameEndSound, SoundSourceType.Global, transform);
        StartCooldown();

        // The stream ends but its particles are not cut: stop emitting and let them finish before the emitter goes away.
        if (flameParticles != null)
        {
            flameParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            while (flameParticles.IsAlive(true))
                yield return null;
        }
        Destroy(gameObject);
    }

    private void ReleaseFireSpell()
    {
        if (fireSpellReleased) return;
        fireSpellReleased = true;
        if (playerSpellController != null)
            playerSpellController.SetFireSpellActive(false);
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
        if (!isProjectile)
        {
            ReleaseFireSpell();
            StopFlameLoop();
        }
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