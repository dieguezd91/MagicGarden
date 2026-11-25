using UnityEngine;
using UnityEngine.Events;

// Game feel when the player takes damage: brief hitstop and camera shake.
// - The red damage screen already exists in FeedbackUIController, so this script adds no screen flash.
// - Subscribes to LifeController.onDamaged on the same GameObject (the player).
// - Hitstop goes only through HitStop.Request, which never writes Time.timeScale by itself unsafely
//   (pause/modals stay intact). This script never touches Time.timeScale.
// - Every effect is editable and can be switched off (bool, or a value of 0).
[DisallowMultipleComponent]
[RequireComponent(typeof(LifeController))]
public class PlayerHitFeedback : MonoBehaviour
{
    [Header("Filter")]
    [Tooltip("Hits that deal less damage than this are ignored (no hitstop or shake).")]
    [SerializeField] private float minDamage = 0.1f;
    [Tooltip("React to damage over time (burn and other ticks). Off = only single hits give feedback, so burning does not keep shaking the camera.")]
    [SerializeField] private bool reactToDamageOverTime = false;

    [Header("Hitstop")]
    [Tooltip("Turns the hitstop on or off.")]
    [SerializeField] private bool enableHitStop = true;
    [Tooltip("Real seconds of slow motion on a hit. 0 disables it. Goes through HitStop.Request (max 0.5 s).")]
    [SerializeField] private float hitStopDuration = 0.06f;

    [Header("Camera Shake")]
    [Tooltip("Turns the camera shake on or off.")]
    [SerializeField] private bool enableCameraShake = true;
    [Tooltip("Shake strength. 0 disables it. CameraShaker restarts on every call, so the last shake in a frame wins: if an enemy requests its own shake after this one, that one is the one you see.")]
    [SerializeField] private float shakeIntensity = 0.25f;
    [Tooltip("Shake length in seconds. 0 disables it.")]
    [SerializeField] private float shakeDuration = 0.2f;

    private LifeController life;

    private void Awake()
    {
        life = GetComponent<LifeController>();
    }

    private void OnEnable()
    {
        if (life == null) life = GetComponent<LifeController>();
        if (life == null) return;

        // LifeController.onDamaged has no initializer: a component added at runtime (AddComponent) has it null.
        // Create it so the feedback still works instead of skipping the subscription.
        if (life.onDamaged == null) life.onDamaged = new UnityEvent<float, LifeController.DamageType>();
        life.onDamaged.RemoveListener(OnDamaged); // never subscribe twice
        life.onDamaged.AddListener(OnDamaged);
    }

    private void OnDisable()
    {
        // Both the LifeController and its event can already be destroyed/null during teardown or scene unload.
        if (life != null && life.onDamaged != null) life.onDamaged.RemoveListener(OnDamaged);
    }

    private void OnDamaged(float damage, LifeController.DamageType damageType)
    {
        if (damage < minDamage || damage <= 0f) return;
        if (damageType == LifeController.DamageType.DamageOverTime && !reactToDamageOverTime) return;
        if (Time.timeScale <= 0f) return; // paused: no feedback
        if (!life.IsAlive() || life.currentHealth <= 0f) return; // dead (or the killing blow): the death sequence takes over

        if (enableHitStop && hitStopDuration > 0f)
            HitStop.Request(hitStopDuration);

        if (enableCameraShake && shakeIntensity > 0f && shakeDuration > 0f && CameraShaker.Instance != null)
            CameraShaker.Instance.Shake(shakeIntensity, shakeDuration);
    }
}
