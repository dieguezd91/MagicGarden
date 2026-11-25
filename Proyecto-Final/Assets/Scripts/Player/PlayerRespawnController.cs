using UnityEngine;
using System.Collections;

public class PlayerRespawnController : MonoBehaviour
{
    [SerializeField] private float respawnDelay = 5f;

    [Header("Sound")]
    [SerializeField] private SoundClipData deathSound;
    [Tooltip("Voice layered on top of deathSound.")]
    [SerializeField] private SoundClipData deathVoiceSound;
    [Tooltip("Pained grunt layered on top of the hit sound.")]
    [SerializeField] private SoundClipData hurtVoiceSound;
    [SerializeField] private float hurtVoiceMinInterval = 0.35f;
    private float nextHurtVoiceTime;
    [Tooltip("Played when the dead (ghost/slime) form starts.")]
    [SerializeField] private SoundClipData ghostSound;
    [Tooltip("Played repeatedly while moving in the dead form.")]
    [SerializeField] private SoundClipData slimeStepSound;
    [SerializeField] private float slimeStepInterval = 0.35f;
    [SerializeField] private SoundClipData reviveSound;
    [Tooltip("Animation event on PlayerSleep (frame 1): sleepy breath, tips over and lands in the leaves.")]
    [SerializeField] private SoundClipData sleepSound;
    [Tooltip("Animation event on PlayerWakeUp (frame 1): stirs, springs up and shakes its head.")]
    [SerializeField] private SoundClipData wakeUpSound;

    private Rigidbody2D rb;
    private float nextSlimeStepTime;

    private LifeController lifeController;
    private PlayerSpellController playerSpellController;
    private PlayerMovementController playerMovementController;

    public bool IsRespawning { get; private set; }
    public float RespawnDelay => respawnDelay;

    private void Awake()
    {
        lifeController = GetComponent<LifeController>();
        playerSpellController = GetComponent<PlayerSpellController>();
        playerMovementController = GetComponent<PlayerMovementController>();
        rb = GetComponent<Rigidbody2D>();
    }

    private void Update()
    {
        if (!IsRespawning || rb == null || rb.velocity.sqrMagnitude < 0.01f || Time.time < nextSlimeStepTime) return;
        nextSlimeStepTime = Time.time + slimeStepInterval;
        PlaySound(slimeStepSound);
    }

    public void PlayDeathSound()
    {
        PlaySound(deathSound);
        PlaySound(deathVoiceSound);
    }

    public void PlayHurtVoice()
    {
        if (Time.time < nextHurtVoiceTime) return;
        nextHurtVoiceTime = Time.time + hurtVoiceMinInterval;
        PlaySound(hurtVoiceSound);
    }

    // Animation event on "Player Death" (frame 11): the body breaks and the ghost appears.
    public void PlayGhostSound() => PlaySound(ghostSound);

    // Non-positional: the camera is moving to frame the player during the night-to-day transition.
    public void PlaySleepSound() => SoundManager.Instance?.PlayClip(sleepSound, SoundSourceType.Global, transform);
    public void PlayWakeUpSound() => SoundManager.Instance?.PlayClip(wakeUpSound, SoundSourceType.Global, transform);

    // The player's own sounds play centred at full level (2D, stereo kept): the camera follows the player anyway.
    private void PlaySound(SoundClipData data) => SoundManager.Instance?.PlayClip(data, SoundSourceType.Global, transform);

    public void BeginRespawn()
    {
        StartCoroutine(RespawnRoutine());
    }

    private IEnumerator RespawnRoutine()
    {
        if (UIManager.Instance != null)
        {
            UIManager.Instance.AnimateRespawnRecovery(respawnDelay);
        }

        yield return new WaitForSeconds(0.5f);

        IsRespawning = true;
        if (playerMovementController != null)
        {
            playerMovementController.SetMovementEnabled(true);
        }

        yield return new WaitForSeconds(respawnDelay - 0.5f);

        IsRespawning = false;
        PlaySound(reviveSound);

        if (lifeController != null)
        {
            lifeController.ResetLife();
        }

        if (playerMovementController != null)
        {
            playerMovementController.SetMovementEnabled(true);
        }
    }
    public void OnReviveAnimationEnd()
    {
        if (playerSpellController != null) playerSpellController.RefreshHandNightness();
    }
}
