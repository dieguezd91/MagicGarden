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

    private const float RitualArrivalDistance = 0.025f;
    private const float RitualApproachGrace = 0.5f;
    private static readonly int MeditateState = Animator.StringToHash("Base Layer.Meditate");
    private Vector2 ritualCenter;
    private bool holdingRitualPosition;
    private RigidbodyConstraints2D preRitualConstraints;

    public float BaseMoveSpeed => maxSpeed;
    public bool IsMovementEnabled => movementEnabled && !IsRitualMovementActive;
    public bool IsMoving => currentVelocity.sqrMagnitude > 0.01f;
    public Vector2 MoveInput => moveInput;
    public bool IsRitualMovementActive { get; private set; }
    public bool CanStartRitualMovement => isActiveAndEnabled && rb != null && rb.simulated && !IsRitualMovementActive;

    private float AccelerationStep => accelerationRate * Time.fixedDeltaTime;

    private void Start()
    {
        if (GameFlowController.Instance != null)
        {
            GameFlowController.Instance.OnPhaseChanged += OnPhaseChanged;
            OnPhaseChanged(GameFlowController.Instance.CurrentPhase);
        }
    }

    private void OnDisable() => EndRitualMovement();

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
        if (IsRitualMovementActive)
        {
            UpdateRitualMovement();
            return;
        }

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

        if (tutorialBlocksMovement || !TutorialManager.Allows(TutorialAction.Move))
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

        Vector2 targetVelocity = GetMovementTargetVelocity(moveInput);
        if (moveInput.sqrMagnitude > 0.01f)
        {
            currentVelocity = Accelerate(currentVelocity, targetVelocity);
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

        UpdateMovementAnimation(moveInput);
    }

    private Vector2 GetMovementTargetVelocity(Vector2 direction)
    {
        Vector2 targetVelocity = direction * UpgradeRuntime.Value("player", MagicGarden.Stat.MoveSpeed, maxSpeed, "Player") * iceSlowMultiplier;
        if (Time.time < attackSlowEndTime)
        {
            float remainingTime = attackSlowEndTime - Time.time;
            float lerpFactor = remainingTime / attackSlowDuration;
            targetVelocity *= Mathf.Lerp(1f, attackMovementPenalty, lerpFactor);
        }
        return targetVelocity;
    }

    private Vector2 Accelerate(Vector2 velocity, Vector2 targetVelocity) =>
        Vector2.Lerp(velocity, targetVelocity, AccelerationStep);

    private void UpdateMovementAnimation(Vector2 direction)
    {
        if (animator == null || spriteRenderer == null) return;

        if (direction != Vector2.zero)
        {
            float animAimX = direction.x;

            if (direction.x > 0.01f)
            {
                spriteRenderer.flipX = true;
                animAimX = -direction.x;
            }
            else if (direction.x < -0.01f)
            {
                spriteRenderer.flipX = false;
            }

            animator.SetBool("IsMoving", true);
            animator.SetFloat("aimX", animAimX);
            animator.SetFloat("aimY", direction.y);
        }
        else
        {
            animator.SetBool("IsMoving", IsMoving);
        }
    }

    public bool TryStartRitualApproach(Vector2 center, out float timeout)
    {
        timeout = 0f;
        if (!CanStartRitualMovement) return false;

        ritualCenter = center;
        IsRitualMovementActive = true;
        StopRitualVelocity();
        timeout = EstimateRitualApproachTimeout(ritualCenter - rb.position);
        return true;
    }

    private float EstimateRitualApproachTimeout(Vector2 remaining)
    {
        float distance = remaining.magnitude;
        if (distance <= RitualArrivalDistance) return RitualApproachGrace;

        float step = Time.fixedDeltaTime;
        float blend = Mathf.Clamp01(AccelerationStep);
        Vector2 direction = remaining / distance;
        // Budget for damped travel, plus the distance lost to the acceleration ramp.
        float cruiseSpeed = Vector2.Dot(GetMovementTargetVelocity(direction), direction) * Mathf.Clamp01(1f - rb.drag * step);
        if (cruiseSpeed <= 0f || blend <= 0f) return RitualApproachGrace;

        float rampDelay = step * (1f - blend) / blend;
        return distance / cruiseSpeed + rampDelay + RitualApproachGrace;
    }

    public bool TryStartRitualMeditation()
    {
        if (!IsRitualMovementActive || rb == null || !rb.simulated || !HasReachedRitualCenter()) return false;

        StopRitualVelocity();
        rb.position = ritualCenter;
        preRitualConstraints = rb.constraints;
        rb.constraints = RigidbodyConstraints2D.FreezeAll;
        holdingRitualPosition = true;

        if (animator != null)
        {
            animator.SetBool("IsMoving", false);
            // The non-looping clip enters the pose once and holds its last frame.
            animator.Play(MeditateState, 0, 0f);
        }
        return true;
    }

    public void EndRitualMovement()
    {
        if (!IsRitualMovementActive) return;

        StopRitualVelocity();
        bool wasMeditating = holdingRitualPosition;
        if (wasMeditating && rb != null)
            rb.constraints = preRitualConstraints;

        holdingRitualPosition = false;
        IsRitualMovementActive = false;
        // Leave the controller's Any State death transition alone.
        if (animator != null && animator.isActiveAndEnabled && (lifeController == null || lifeController.IsAlive()))
        {
            ResetAnimator();
            if (wasMeditating && GameFlowController.Instance?.CurrentPhase == GamePhase.Night)
            {
                // Sample Idle before restoring spell parameters: Meditate disables the child hand Animator.
                animator.Update(0f);
                GetComponent<PlayerSpellController>()?.RefreshHandNightness();
            }
        }
    }

    private void UpdateRitualMovement()
    {
        if (rb == null || !rb.simulated) return;

        if (holdingRitualPosition || HasReachedRitualCenter())
        {
            StopRitualVelocity();
            if (holdingRitualPosition && rb.position != ritualCenter)
                rb.position = ritualCenter;
            else if (!holdingRitualPosition)
                UpdateMovementAnimation(Vector2.zero);
            return;
        }

        Vector2 remaining = ritualCenter - rb.position;
        float distance = remaining.magnitude;
        Vector2 direction = remaining / distance;
        // Steer toward the center after contacts without carrying sideways momentum into the stop.
        Vector2 forwardVelocity = direction * Mathf.Max(0f, Vector2.Dot(currentVelocity, direction));
        currentVelocity = Vector2.ClampMagnitude(Accelerate(forwardVelocity, GetMovementTargetVelocity(direction)),
            distance / Time.fixedDeltaTime);
        rb.velocity = currentVelocity;
        UpdateMovementAnimation(currentVelocity.normalized);
    }

    private bool HasReachedRitualCenter() =>
        (rb.position - ritualCenter).sqrMagnitude <= RitualArrivalDistance * RitualArrivalDistance;

    private void StopRitualVelocity()
    {
        moveInput = Vector2.zero;
        currentVelocity = Vector2.zero;
        if (rb != null)
            rb.velocity = Vector2.zero;
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
                SoundSourceType.Global,
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