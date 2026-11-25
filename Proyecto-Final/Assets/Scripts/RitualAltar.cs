using System;
using System.Collections;
using UnityEngine;

public class RitualAltar : MonoBehaviour, IInteractionLifecycle
{
    [Header("Ritual Configuration")]
    [SerializeField] private float ritualDuration = 10f;
    [SerializeField] private bool canTransitionToNight = true;
    [SerializeField] private bool canRestoreHealth = true;
    [Tooltip("Optional point where the player stops and meditates. If unassigned, uses the altar's position.")]
    [SerializeField] private Transform meditationPoint;

    [Header("Sprite Change")]
    [SerializeField] private SpriteRenderer altarSpriteRenderer;
    [SerializeField] private Sprite defaultSprite;
    [SerializeField] private Sprite nearSprite;

    [Header("Visual Controller")]
    [SerializeField] private RitualVisualController ritualVisualController;

    [Header("References")]
    private LevelManager levelManager;
    private LifeController playerLife;
    private PlayerMovementController playerMovement;
    private HouseLifeController houseLife;

    public event Action InteractionCompleted;

    private bool isPerformingRitual = false;
    private bool ritualStarted;
    private Coroutine mainRitualCoroutine;
    [SerializeField] private int tutorialStepOrderToUnlock = 9;
    private bool tutorialInteractionPending = false;

    [SerializeField, Range(0f, 100f)] private float houseMissingHealthRestorePercent = 25f;

    private void Start()
    {
        CacheReferences();
        InitializeComponents();
    }

    private void CacheReferences()
    {
        levelManager = LevelManager.Instance;

        if (ritualVisualController == null)
        {
            ritualVisualController = GetComponent<RitualVisualController>();
        }

        if (GameObject.FindGameObjectWithTag("Home") != null)
            houseLife = GameObject.FindGameObjectWithTag("Home").GetComponent<HouseLifeController>();

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerLife = player.GetComponent<LifeController>();
            playerMovement = player.GetComponent<PlayerMovementController>();
        }
    }

    private void InitializeComponents()
    {
        UpdateAltarAppearance();
    }

    public void Interact() => TryInteract();

    public bool TryInteract()
    {
        if (!isActiveAndEnabled || !CanInteract() || playerMovement == null ||
            !playerMovement.TryStartRitualApproach(meditationPoint != null ? meditationPoint.position : transform.position,
                out float timeout))
            return false;

        isPerformingRitual = true;
        mainRitualCoroutine = StartCoroutine(PerformRitual(timeout));
        return true;
    }

    public bool CanInteract()
    {
        if (!enabled || isPerformingRitual || levelManager == null || playerLife == null || !playerLife.IsAlive() ||
            (playerMovement != null && !playerMovement.CanStartRitualMovement) || GameFlowController.Instance == null)
            return false;

        if (!TutorialManager.Allows(TutorialAction.RitualAltar))
            return false;

        // Respect tutorial gating: if the tutorial is active and currently blocking input, disallow interaction
        if (TutorialManager.Instance != null && !TutorialManager.Instance.CanAcceptPlayerInput())
        {
            return false;
        }

        if (TutorialManager.Instance != null && TutorialManager.Instance.IsTutorialActive())
        {
            if (!TutorialManager.Instance.CanStartTutorialNight(tutorialStepOrderToUnlock))
            {
                return false;
            }
        }

        return IsValidPhaseForRitual(GameFlowController.Instance.CurrentPhase);
    }

    private bool IsValidPhaseForRitual(GamePhase phase)
    {
        return phase == GamePhase.Day;
    }

    private IEnumerator PerformRitual(float approachTimeout)
    {
        float deadline = Time.time + approachTimeout;
        // Let TryInteract return and the interaction trigger latch before running any callbacks.
        yield return null;
        while (CanContinueApproach() && Time.time < deadline)
        {
            if (playerMovement.TryStartRitualMeditation())
            {
                BeginRitual();
                if (!isPerformingRitual) yield break;

                yield return new WaitForSeconds(ritualDuration);
                yield return CompleteRitual();
                if (isPerformingRitual) yield return EndRitualSequence();
                yield break;
            }

            yield return null;
        }

        ForceStopRitual();
    }

    private bool CanContinueApproach() => HasRitualPlayer() &&
        GameFlowController.Instance != null && IsValidPhaseForRitual(GameFlowController.Instance.CurrentPhase);

    private bool HasRitualPlayer() => playerLife != null && playerLife.IsAlive() &&
        playerMovement != null && playerMovement.IsRitualMovementActive;

    private void Update()
    {
        if (isPerformingRitual && !HasRitualPlayer())
            CancelInteraction();
    }

    private void BeginRitual()
    {
        var tm = TutorialManager.Instance;
        if (tm != null && tm.IsTutorialActive())
        {
            // Do not advance the tutorial until the approach actually succeeds.
            tutorialInteractionPending = true;
            tm.DeferNextStep();
            TutorialEvents.InvokeRitualAltarUsed();
            if (!isPerformingRitual) return;
            tm.ConfirmWaitStep();
        }

        HandleRestorationAttempt();
        if (!isPerformingRitual) return;
        ritualStarted = true;
        GameFlowController.Instance.SetPhase(GamePhase.OnRitual);
        if (!isPerformingRitual) return;

        if (canTransitionToNight && LunarCycleManager.Instance != null)
        {
            LunarCycleManager.Instance.NotifyNightStarted();
        }

        ritualVisualController?.BeginRitual(ritualDuration);
        UIManager.Instance?.ShowRitualOverlay();
    }

    private IEnumerator CompleteRitual()
    {
        ApplyRitualBenefits();

        UIManager.Instance?.HideRitualOverlay();
        yield return new WaitForSeconds(0.1f);

        if (canTransitionToNight)
        {
            if (tutorialInteractionPending)
            {
                Debug.Log("[RitualAltar] Ritual completed - releasing deferred tutorial next step (event already fired at ritual start).");
            }
            else
            {
                Debug.Log("[RitualAltar] Ritual completed - invoking ritual-used (non-tutorial pending).");
                TutorialEvents.InvokeRitualAltarUsed();
            }
            if (!isPerformingRitual) yield break;

            TutorialEvents.InvokeNightStarted();
            if (!isPerformingRitual) yield break;
            RitualBuffManager.Instance?.ActivateRitualBuff();
            DayCycleController.Instance.StartNight();
        }
    }

    private IEnumerator EndRitualSequence()
    {
        ritualVisualController?.StartEndRitualEffects();
        UpdateAltarAppearance();

        // Show the next instruction during the fade, but do not light the exit while the player is anchored.
        ReleaseTutorialDeferral();
        if (!isPerformingRitual) yield break;

        if (ritualVisualController != null && ritualVisualController.EndFadeDuration > 0f)
            yield return new WaitForSeconds(ritualVisualController.EndFadeDuration);

        RestoreDayIfStillInRitual();
        if (isPerformingRitual) ReleaseRitualPlayer(completedNormally: true);
    }

    private void ApplyRitualBenefits()
    {
        if (!canRestoreHealth || playerLife == null) return;

        playerLife.currentHealth = playerLife.maxHealth;
        playerLife.onHealthChanged?.Invoke(playerLife.currentHealth, playerLife.maxHealth);

        ManaSystem playerMana = playerLife.GetComponent<ManaSystem>();
        if (playerMana != null)
        {
            playerMana.SetMana(playerMana.modifiedMaxMana);
        }
    }

    private void OnDisable() => CancelInteraction();

    public void CancelInteraction()
    {
        if (!isPerformingRitual) return;

        RestoreDayIfStillInRitual();
        ForceStopRitual();
    }

    public void ForceStopRitual()
    {
        // Existing force-end-night callers own the phase change; OnRitual is a valid input to EndNight.
        StopRitualCoroutine();
        if (ritualStarted || !isPerformingRitual)
        {
            UIManager.Instance?.HideRitualOverlay();
            if (ritualVisualController != null && ritualVisualController.gameObject.activeInHierarchy)
                ritualVisualController.ForceStopAndRestore();
        }
        UpdateAltarAppearance();
        if (isPerformingRitual)
        {
            ReleaseTutorialDeferral();
            ReleaseRitualPlayer();
        }
    }

    private void StopRitualCoroutine()
    {
        if (mainRitualCoroutine != null)
        {
            StopCoroutine(mainRitualCoroutine);
            mainRitualCoroutine = null;
        }

        StopAllCoroutines();
    }

    private void RestoreDayIfStillInRitual()
    {
        var flow = GameFlowController.Instance;
        if (ritualStarted && flow != null && flow.WorldPhase == GamePhase.OnRitual)
            flow.SetPhase(GamePhase.Day);
    }

    private void ReleaseTutorialDeferral()
    {
        if (!tutorialInteractionPending) return;

        tutorialInteractionPending = false;
        TutorialManager.Instance?.ReleaseDeferredNextStep(true);
    }

    private void ReleaseRitualPlayer(bool completedNormally = false)
    {
        if (playerMovement != null)
            playerMovement.EndRitualMovement();

        isPerformingRitual = false;
        ritualStarted = false;
        mainRitualCoroutine = null;
        if (completedNormally && ritualVisualController != null)
            ritualVisualController.OnPlayerReleased(canTransitionToNight);

        if (gameObject.activeInHierarchy)
            InteractionCompleted?.Invoke();
    }

    private void UpdateAltarAppearance()
    {
        if (altarSpriteRenderer != null && defaultSprite != null)
        {
            altarSpriteRenderer.sprite = defaultSprite;
        }
    }

    private void RestoreHealth()
    {
        if (houseLife == null) return;

        float missingHealth = houseLife.MaxHealth - houseLife.CurrentHealth;
        float healthToRestore = missingHealth * (houseMissingHealthRestorePercent / 100f);

        houseLife.Restore(healthToRestore);
    }

    private void HandleRestorationAttempt()
    {
        if (houseLife == null) return;
        RestoreHealth();
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player") && altarSpriteRenderer != null && nearSprite != null)
        {
            altarSpriteRenderer.sprite = nearSprite;
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            UpdateAltarAppearance();
        }
    }
}
