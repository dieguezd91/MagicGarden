using System.Collections;
using UnityEngine;

public class RitualAltar : MonoBehaviour, IInteractable
{
    [Header("Ritual Configuration")]
    [SerializeField] private float ritualDuration = 10f;
    [SerializeField] private bool canTransitionToNight = true;
    [SerializeField] private bool canRestoreHealth = true;

    [Header("Sprite Change")]
    [SerializeField] private SpriteRenderer altarSpriteRenderer;
    [SerializeField] private Sprite defaultSprite;
    [SerializeField] private Sprite nearSprite;

    [Header("Visual Controller")]
    [SerializeField] private RitualVisualController ritualVisualController;

    [Header("References")]
    private LevelManager levelManager;
    private LifeController playerLife;
    private HouseLifeController houseLife;

    private bool isPerformingRitual = false;
    private Coroutine mainRitualCoroutine;
    [SerializeField] private int tutorialStepOrderToUnlock = 9;
    private bool tutorialInteractionPending = false;
    // Whether we already fired the ritual-used tutorial event at interaction time
    private bool tutorialEventFiredOnInteract = false;

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
        }
    }

    private void InitializeComponents()
    {
        UpdateAltarAppearance();
    }

    public void Interact()
    {
        if (!CanInteract()) return;
        var tm = TutorialManager.Instance;
        if (tm != null && tm.IsTutorialActive())
        {
            tm.DeferNextStep();
            TutorialEvents.InvokeRitualAltarUsed();
            tm.ConfirmWaitStep();

            tutorialInteractionPending = true;
            tutorialEventFiredOnInteract = true;
        }

        mainRitualCoroutine = StartCoroutine(PerformRitual());
    }

    public bool CanInteract()
    {
        if (isPerformingRitual || levelManager == null || playerLife == null)
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

    private IEnumerator PerformRitual()
    {
        BeginRitual();

        yield return new WaitForSeconds(ritualDuration);

        yield return CompleteRitual();

        EndRitual();
    }

    private void BeginRitual()
    {
        isPerformingRitual = true;
        HandleRestorationAttempt();
        GameFlowController.Instance.SetPhase(GamePhase.OnRitual);

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
                if (tutorialEventFiredOnInteract)
                {
                    Debug.Log("[RitualAltar] Ritual completed - releasing deferred tutorial next step (event already fired on interact).");
                }
                else
                {
                    Debug.Log("[RitualAltar] Ritual completed - confirming tutorial progression now.");
                    TutorialEvents.InvokeRitualAltarUsed();
                    TutorialManager.Instance?.ConfirmWaitStep();
                }
            }
            else
            {
                Debug.Log("[RitualAltar] Ritual completed - invoking ritual-used (non-tutorial pending).");
                TutorialEvents.InvokeRitualAltarUsed();
            }
            TutorialEvents.InvokeNightStarted();
            RitualBuffManager.Instance?.ActivateRitualBuff();
            DayCycleController.Instance.StartNight();
        }
    }

    private void EndRitual()
    {
        if (mainRitualCoroutine != null)
            StartCoroutine(EndRitualSequence());
    }

    private IEnumerator EndRitualSequence()
    {
        ritualVisualController?.StartEndRitualEffects();
        UpdateAltarAppearance();

        // Release the deferred tutorial next-step now so the next instruction appears
        // immediately after the ritual end effects (do not wait for the visual fade).
        if (tutorialInteractionPending || tutorialEventFiredOnInteract)
        {
            TutorialManager.Instance?.ReleaseDeferredNextStep(true);
        }

        if (ritualVisualController != null)
        {
            yield return ritualVisualController.FinishEndRitualEffects();
        }

        isPerformingRitual = false;
        mainRitualCoroutine = null;

        tutorialInteractionPending = false;
        tutorialEventFiredOnInteract = false;
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

    public void ForceStopRitual()
    {
        StopRitualCoroutine();
        CleanupRitualState();
        ritualVisualController?.ForceStopAndRestore();
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

    private void CleanupRitualState()
    {
        isPerformingRitual = false;
        UIManager.Instance?.HideRitualOverlay();
        UpdateAltarAppearance();
        tutorialInteractionPending = false;
        tutorialEventFiredOnInteract = false;
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
