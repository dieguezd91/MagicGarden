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

    private AudioSource flameLoopSource;
    // Shared by every flame projectile: ~20 flames/s would otherwise stack sizzles.
    private static float lastHitSoundTime = float.MinValue;

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