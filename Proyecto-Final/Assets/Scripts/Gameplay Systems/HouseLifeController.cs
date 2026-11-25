using UnityEngine;
using UnityEngine.Events;

public class HouseLifeController : MonoBehaviour
{
    [Header("Sound")]
    [Tooltip("Random clip per hit.")]
    [SerializeField] private SoundClipData hitSound;
    [Header("HEALTH SETTINGS")]
    public float maxHealth = 1000f;
    public float currentHealth;

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;

    [Header("ANIMATION")]
    public Animator animator;

    [Header("EVENTS")]
    public UnityEvent onHouseDestroyed;
    public UnityEvent<float, float> onHealthChanged;
    public UnityEvent<float> onDamaged;

    [Header("FX")]
    public ParticleSystem damageParticles;
    private float lastParticleTime = -1f;
    public float minParticleInterval = 0.1f;

    [Header("CAMERA SHAKE")]
    [Tooltip("Camera shake strength when the house takes damage. 0 disables the camera shake.")]
    [SerializeField] private float cameraShakeIntensity = 0.15f;
    [Tooltip("Camera shake duration in seconds when the house takes damage. 0 disables the camera shake.")]
    [SerializeField] private float cameraShakeDuration = 0.15f;
    [Tooltip("Minimum seconds between two camera shakes, so a swarm hitting the house does not shake the camera constantly.")]
    [SerializeField] private float cameraShakeMinInterval = 0.4f;
    private float lastCameraShakeTime = float.NegativeInfinity;

    private bool isDestroyed = false;

    void Awake()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();
        currentHealth = maxHealth;
        UpdateAnimator();
        onHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    void Start()
    {
        UpdateAnimator();
    }

    public void TakeDamage(float damage)
    {
        if (isDestroyed || LifeController.PlayerInvulnerable) // god mode cheat covers the house too
            return;

        currentHealth -= damage;
        currentHealth = Mathf.Max(0, currentHealth);

        if (damage > 0f)
        {
            onDamaged?.Invoke(damage);
            if (SoundManager.Instance != null && hitSound != null && hitSound.CanPlay())
            {
                SoundManager.Instance.PlayClip(hitSound, SoundSourceType.Localized, transform);
                hitSound.SetLastPlayTime();
            }
            PlayDamageParticles();
            ShakeCamera();
        }

        UpdateAnimator();

        onHealthChanged?.Invoke(currentHealth, maxHealth);

        if (currentHealth <= 0 && !isDestroyed)
            DestroyHouse();
    }

    public void Restore(float heal)
    {
        if (isDestroyed)
            return;

        currentHealth += heal;
        currentHealth = Mathf.Min(maxHealth, currentHealth);

        UpdateAnimator();

        onHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    void UpdateAnimator()
    {
        if (animator != null)
        {
            float value = currentHealth / maxHealth;
            animator.SetFloat("Health", value);
        }
    }

    void DestroyHouse()
    {
        isDestroyed = true;
        onHouseDestroyed?.Invoke();
    }

    private void ShakeCamera()
    {
        if (cameraShakeIntensity <= 0f || cameraShakeDuration <= 0f || CameraShaker.Instance == null) return;
        if (Time.unscaledTime - lastCameraShakeTime < cameraShakeMinInterval) return;

        lastCameraShakeTime = Time.unscaledTime;
        CameraShaker.Instance.Shake(cameraShakeIntensity, cameraShakeDuration);
    }

    public void PlayDamageParticles()
    {
        if (damageParticles != null && (Time.time - lastParticleTime) > minParticleInterval)
        {
            damageParticles.Play();
            lastParticleTime = Time.time;
        }
    }

    public void ResetLife()
    {
        currentHealth = maxHealth;
        UpdateAnimator();
        onHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public float GetHealthPercent() => currentHealth / maxHealth;
}
