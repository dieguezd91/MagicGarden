using UnityEngine;
using System.Collections;
using MagicGarden;

public abstract class Spell : MonoBehaviour
{
    [Header("SETTINGS")]
    [SerializeField] protected float damage;
    [SerializeField] protected float lifeTime;

    [Header("VFX & FEEDBACK")]
    [SerializeField] protected GameObject floatingDamagePrefab;
    [SerializeField] protected GameObject impactParticlesPrefab;

    [Header("IMPACT FEEL")]
    [Tooltip("Camera shake intensity on hit. 0 disables the shake.")]
    [SerializeField] protected float impactShakeIntensity = 0.3f;
    [Tooltip("Camera shake duration on hit, in seconds.")]
    [SerializeField] protected float impactShakeDuration = 0.25f;
    [Tooltip("Brief slow-motion freeze on hit, in real seconds. 0 disables it.")]
    [SerializeField] protected float hitStopDuration = 0.05f;
    [Tooltip("Camera shake intensity when the hit kills the target. 0 disables the shake.")]
    [SerializeField] protected float killShakeIntensity = 0.5f;
    [Tooltip("Camera shake duration when the hit kills the target, in seconds.")]
    [SerializeField] protected float killShakeDuration = 0.3f;
    [Tooltip("Slow-motion freeze when the hit kills the target, in real seconds. 0 disables it.")]
    [SerializeField] protected float killHitStopDuration = 0.09f;

    [Header("SOUND")]
    [Tooltip("Played when cast. Empty = the shared \"ShootSpell\" sound.")]
    [SerializeField] protected SoundClipData castSound;
    [Tooltip("Played on hit. Use cooldownTime to play it once per cast when many enemies are hit together.")]
    [SerializeField] protected SoundClipData impactSound;

    public bool HasCastSound => castSound != null && castSound.clips != null && castSound.clips.Length > 0;

    // No cooldown check: this is called on the prefab asset, whose cooldown timer would survive editor play sessions.
    public void PlayCastSound(Transform at) => SoundManager.Instance?.PlayClip(castSound, SoundSourceType.Localized, at);

    protected void PlayImpactSound()
    {
        if (SoundManager.Instance == null || impactSound == null || !impactSound.CanPlay()) return;
        SoundManager.Instance.PlayClip(impactSound, SoundSourceType.Localized, transform);
        impactSound.SetLastPlayTime();
    }

    public string UpgradeTargetId { get; private set; }
    public string UpgradeCapability { get; private set; }
    public void SetUpgradeTarget(string id, string capability)
    {
        UpgradeTargetId = id;
        UpgradeCapability = capability;
    }
    protected float Upgraded(Stat stat, float basis) => UpgradeRuntime.Value(UpgradeTargetId, stat, basis, UpgradeCapability ?? GetType().Name);
    protected float Special(EffectKind kind) => UpgradeRuntime.EffectValue(UpgradeTargetId, kind);
    protected float DamageMultiplier => damage > 0 ? Upgraded(Stat.Damage, damage) / damage : Upgraded(Stat.Damage, 1);
    protected virtual void Awake()
    {
        if (lifeTime > 0f) StartCoroutine(Lifetime());
    }
    private IEnumerator Lifetime()
    {
        float elapsed = 0;
        while (elapsed < Upgraded(Stat.Range, lifeTime))
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
        if (!ExtendLifetime()) Destroy(gameObject);
    }
    protected virtual bool ExtendLifetime() => false;
    public virtual Target DescribeUpgradeTarget(string id, string displayName)
    {
        var target = new Target { Id = id, Name = displayName, Capability = GetType().Name };
        if (damage > 0) target.Bases[Stat.Damage] = damage;
        target.Bases[Stat.Knockback] = 8;
        return target;
    }

    public abstract void Cast(Vector2 direction, Vector3 spawnPosition);

    protected virtual void ApplyDamage(Collider2D target)
    {
        float dmg = Random.Range(damage, damage + 5f) * DamageMultiplier;

        if (RitualBuffManager.Instance != null)
        {
            dmg *= RitualBuffManager.Instance.GetDamageMultiplier();
        }

        var life = target.GetComponent<LifeController>();
        bool wasAlive = life != null && life.IsAlive();
        life?.TakeDamage(dmg);
        lastHitKilled = wasAlive && !life.IsAlive();

        ShowDamageText(target.transform, dmg);
        PlayImpactEffects(target.transform.position);
        ApplyKnockback(target);
    }

    // Set by ApplyDamage right before ShowDamageText/PlayImpactEffects run, so they can react to a killing blow.
    protected bool lastHitKilled;

    // Subclasses with very frequent hits (damage ticks) turn these off.
    protected virtual bool AllowsHitStop => true;
    protected virtual bool AllowsImpactShake => true;

    protected readonly struct ImpactFeel
    {
        public readonly float shakeIntensity;
        public readonly float shakeDuration;
        public readonly float hitStop;

        public ImpactFeel(float shakeIntensity, float shakeDuration, float hitStop)
        {
            this.shakeIntensity = shakeIntensity;
            this.shakeDuration = shakeDuration;
            this.hitStop = hitStop;
        }
    }

    protected ImpactFeel GetImpactFeel(bool killed) => killed
        ? new ImpactFeel(killShakeIntensity, killShakeDuration, killHitStopDuration)
        : new ImpactFeel(impactShakeIntensity, impactShakeDuration, hitStopDuration);

    // Shake + hitstop for one hit; each part is skipped when its value is 0 or the spell does not allow it.
    protected void PlayImpactFeel(bool killed)
    {
        if (Time.timeScale <= 0f) return;
        var feel = GetImpactFeel(killed);

        if (AllowsImpactShake && feel.shakeIntensity > 0f && feel.shakeDuration > 0f && CameraShaker.Instance != null)
            CameraShaker.Instance.Shake(feel.shakeIntensity, feel.shakeDuration);

        if (AllowsHitStop && feel.hitStop > 0f)
            HitStop.Request(feel.hitStop);
    }

    protected virtual void ShowDamageText(Transform target, float dmg)
    {
        if (floatingDamagePrefab == null) return;

        var existing = target.GetComponentInChildren<FloatingDamageText>();

        if (existing != null)
        {
            existing.AddDamage(dmg);
        }
        else
        {
            Vector3 spawnPos = target.position + Vector3.up * 1f;
            var go = Instantiate(floatingDamagePrefab, spawnPos, Quaternion.identity);
            var fdt = go.GetComponent<FloatingDamageText>();
            fdt?.Initialize(target);
            fdt?.AddDamage(dmg);
        }
    }

    protected virtual void PlayImpactEffects(Vector3 position)
    {
        PlayImpactSound();
        PlayImpactFeel(lastHitKilled);
        if (impactParticlesPrefab == null) return;

        var particles = Instantiate(impactParticlesPrefab, position, Quaternion.identity);
        var ps = particles.GetComponent<ParticleSystem>();

        if (ps != null)
            Destroy(particles, ps.main.duration);
        else
            Destroy(particles, 1.5f);
    }

    protected virtual void ApplyKnockback(Collider2D target)
    {
        var knockback = target.GetComponent<KnockbackReceiver>();
        if (knockback != null)
        {
            Vector2 direction = (target.transform.position - transform.position).normalized;
            knockback.ApplyKnockback(direction, Upgraded(Stat.Knockback, 8f));
        }
    }
}