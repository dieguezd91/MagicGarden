using UnityEngine;

public class PlayerMovementController : MonoBehaviour
{
    private void Awake()
    {
        if (pauseController == null) pauseController = FindObjectOfType<PauseController>();
        if (input == null) input = FindObjectOfType<InputReader>();
        lifeController = GetComponent<LifeController>();
        playerRespawnController = GetComponent<PlayerRespawnController>();
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        knockbackReceiver = GetComponent<KnockbackReceiver>();
        playerAbilitySystem = GetComponent<PlayerAbilitySystem>();
    }

    [Header("INPUT")]
    [SerializeField] private InputReader input;

    [Header("MOVEMENT ACCELERATION")]
    [SerializeField] private float accelerationRate = 8f;
    [SerializeField] private float decelerationRate = 10f;
    [SerializeField] private float maxSpeed = 5f;
    private Vector2 currentVelocity = Vector2.zero;

    [Header("ICE SLOW")]
    private float iceSlowMultiplier = 1f;

    [Header("ATTACK MOVEMENT PENALTY")]
    [SerializeField] private float attackMovementPenalty = 0.5f;
    [SerializeField] private float attackSlowDuration = 0.3f;
    private float attackSlowEndTime = 0f;

    [Header("REFERENCES")]
    private PauseController pauseController;
    private LifeController lifeController;
    private PlayerRespawnController playerRespawnController;
    private PlayerAbilitySystem playerAbilitySystem;
    private KnockbackReceiver knockbackReceiver;
    private Rigidbody2D rb;
    private Animator animator;
    private SpriteRenderer spriteRenderer;

    private float lastFootstepTime = 0f;
    private float footstepCooldown = 0.2f;
    [SerializeField] private SurfaceDetector surfaceDetector;

    private Vector2 moveInput;
    private bool movementEnabled = true;
    private bool hasMovedForTutorial = false;
    private bool tutorialBlocksMovement;
    private bool dayTransitionBlocked = false;

    public float BaseMoveSpeed => maxSpeed;
    public bool IsMovementEnabled => movementEnabled;
    public bool IsMoving => currentVelocity.sqrMagnitude > 0.01f;
    public Vector2 MoveInput => moveInput;

    private void Start()
    {
        if (GameFlowController.Instance != null)
        {
            GameFlowController.Instance.OnPhaseChanged += OnPhaseChanged;
            OnPhaseChanged(GameFlowController.Instance.CurrentPhase);
        }
    }

    private void OnDestroy()
    {
        if (GameFlowController.Instance != null)
            GameFlowController.Instance.OnPhaseChanged -= OnPhaseChanged;
    }

    private void OnPhaseChanged(GamePhase newPhase)
    {
        bool playerIsAliveAndNotRespawning =
            (lifeController != null &&
             lifeController.IsAlive() &&
             !(playerRespawnController != null && playerRespawnController.IsRespawning));

        bool gameIsPaused =
            (pauseController != null && pauseController.IsPaused);

        movementEnabled = ShouldAllowMovementForPhase(newPhase) &&
                          playerIsAliveAndNotRespawning &&
                          !gameIsPaused;

        if (!movementEnabled)
        {
            currentVelocity = Vector2.zero;

            if (rb != null)
                rb.velocity = Vector2.zero;

            if (animator != null)
                animator.SetBool("IsMoving", false);
        }
    }

    private bool ShouldAllowMovementForPhase(GamePhase phase)
    {
        return phase == GamePhase.Day || phase == GamePhase.Night;
    }

    void FixedUpdate()
    {
        if (dayTransitionBlocked)
        {
            moveInput = Vector2.zero;
            currentVelocity = Vector2.zero;

            if (rb != null)
                rb.velocity = Vector2.zero;

            if (animator != null)
                animator.SetBool("IsMoving", false);

            return;
        }

        if (UpgradeRuntime.GameplayBlocked || (UIManager.Instance?.Flow != null && UIManager.Instance.Flow.HasOpenModal))
        {
            currentVelocity = Vector2.zero;

            if (rb != null)
                rb.velocity = Vector2.zero;

            if (animator != null)
                animator.SetBool("IsMoving", false);

            return;
        }

        if (tutorialBlocksMovement)
        {
            currentVelocity = Vector2.zero;

            if (rb != null)
                rb.velocity = Vector2.zero;

            if (animator != null)
                animator.SetBool("IsMoving", false);

            return;
        }

        if (playerAbilitySystem != null && playerAbilitySystem.IsBusy())
        {
            currentVelocity = Vector2.zero;

            if (rb != null)
                rb.velocity = Vector2.zero;

            if (animator != null)
                animator.SetBool("IsMoving", false);

            return;
        }

        if (lifeController != null &&
            !lifeController.IsAlive() &&
            !(playerRespawnController != null && playerRespawnController.IsRespawning))
        {
            currentVelocity = Vector2.zero;

            if (rb != null)
                rb.velocity = Vector2.zero;

            if (animator != null)
                animator.SetBool("IsMoving", false);

            return;
        }

        if (!movementEnabled)
        {
            currentVelocity = Vector2.Lerp(
                currentVelocity,
                Vector2.zero,
                decelerationRate * Time.fixedDeltaTime
            );

            if (rb != null)
                rb.velocity = currentVelocity;

            if (animator != null)
                animator.SetBool("IsMoving", false);

            return;
        }

        if (knockbackReceiver != null && knockbackReceiver.IsBeingKnockedBack())
        {
            if (animator != null)
                animator.SetBool("IsMoving", false);

            return;
        }

        moveInput = input != null ? input.MoveInput : Vector2.zero;

        if (!hasMovedForTutorial && moveInput.sqrMagnitude > 0.01f)
        {
            hasMovedForTutorial = true;
            TutorialEvents.InvokePlayerMoved();
        }

        // Velocidad base modificada por el hielo
        Vector2 targetVelocity = moveInput * UpgradeRuntime.Value("player", MagicGarden.Stat.MoveSpeed, maxSpeed, "Player") * iceSlowMultiplier;

        if (Time.time < attackSlowEndTime)
        {
            float remainingTime = attackSlowEndTime - Time.time;
            float lerpFactor = remainingTime / attackSlowDuration;
            float speedMultiplier = Mathf.Lerp(1f, attackMovementPenalty, lerpFactor);

            targetVelocity *= speedMultiplier;
        }

        if (moveInput.sqrMagnitude > 0.01f)
        {
            currentVelocity = Vector2.Lerp(
                currentVelocity,
                targetVelocity,
                accelerationRate * Time.fixedDeltaTime
            );
        }
        else
        {
            currentVelocity = Vector2.Lerp(
                currentVelocity,
                Vector2.zero,
                decelerationRate * Time.fixedDeltaTime
            );
        }

        if (rb != null)
            rb.velocity = currentVelocity;

        bool isMoving = currentVelocity.sqrMagnitude > 0.01f;

        if (animator != null && spriteRenderer != null)
        {
            if (moveInput != Vector2.zero)
            {
                float animAimX = moveInput.x;
                float animAimY = moveInput.y;

                if (moveInput.x > 0.01f)
                {
                    spriteRenderer.flipX = true;
                    animAimX = -moveInput.x;
                }
                else if (moveInput.x < -0.01f)
                {
                    spriteRenderer.flipX = false;
                }

                animator.SetBool("IsMoving", true);
                animator.SetFloat("aimX", animAimX);
                animator.SetFloat("aimY", animAimY);
            }
            else
            {
                animator.SetBool("IsMoving", isMoving);
            }
        }
    }

    public void ApplyAttackMovementPenalty()
    {
        attackSlowEndTime = Time.time + attackSlowDuration;
    }

    public void ApplyIceSlow(float slowAmount)
    {
        iceSlowMultiplier = Mathf.Clamp01(1f - slowAmount);
    }

    public void RemoveIceSlow()
    {
        iceSlowMultiplier = 1f;
    }

    public void SetTutorialMovementBlocked(bool blocked)
    {
        tutorialBlocksMovement = blocked;

        if (blocked)
        {
            currentVelocity = Vector2.zero;

            if (rb != null)
                rb.velocity = Vector2.zero;

            if (animator != null)
                animator.SetBool("IsMoving", false);
        }
    }

    public void SetMovementEnabled(bool enabled)
    {
        if (enabled && pauseController != null && pauseController.IsPaused)
        {
            return;
        }

        movementEnabled = enabled;

        if (!enabled)
        {
            if (rb != null)
            {
                rb.velocity = Vector2.zero;
                currentVelocity = Vector2.zero;
            }
        }
    }

    public void PlayFootstep()
    {
        if (Time.time - lastFootstepTime >= footstepCooldown)
        {
            string surface = surfaceDetector != null
                ? surfaceDetector.DetectSurfaceTag()
                : "Default";

            string soundName;

            switch (surface)
            {
                case "Grass":
                    soundName = "Step_Grass";
                    break;

                case "Land":
                    soundName = "Step_Land";
                    break;

                case "Wood":
                    soundName = "Step_Wood";
                    break;

                default:
                    soundName = "Default";
                    break;
            }

            SoundManager.Instance.Play(
                soundName,
                SoundSourceType.Localized,
                transform
            );

            lastFootstepTime = Time.time;
        }
    }

    public void ResetAnimator()
    {
        if (animator != null)
        {
            animator.SetBool("IsMoving", false);
            animator.SetFloat("aimX", 0f);
            animator.SetFloat("aimY", -1f);
            animator.Play("Idle");
        }
    }

    public void ResetHasMovedForTutorial()
    {
        hasMovedForTutorial = false;
    }

    public void SetDayTransitionBlocked(bool blocked)
    {
        dayTransitionBlocked = blocked;

        if (blocked)
        {
            moveInput = Vector2.zero;
            currentVelocity = Vector2.zero;

            if (rb != null)
                rb.velocity = Vector2.zero;

            if (animator != null)
                animator.SetBool("IsMoving", false);
        }
    }
}