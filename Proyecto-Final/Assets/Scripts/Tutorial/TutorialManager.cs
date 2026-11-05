using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class TutorialManager : MonoBehaviour
{
    [SerializeField] private PauseController pauseController;
    public static TutorialManager Instance { get; private set; }

    [Header("SETTINGS")]
    [SerializeField] private List<TutorialStep> tutorialSteps;
    [SerializeField] private TutorialUI tutorialUI;
    [SerializeField] private bool enableTutorial = true;
    [SerializeField] private float bufferProcessDelay = 0.3f;

    [Tooltip("Optional explicit list order. Empty sequence falls back to legacy stepOrder sorting.")]
    [SerializeField] private TutorialSequence sequence;
    private int[] conditionProgress;
    private bool configuredConditionsMet;
    private bool configuredWaitFinished;
    private float configuredElapsed;
    private bool configuredClockStarted;
    private bool firstPlantReadyObserved;
    private bool loadoutConfirmedObserved;
    private bool houseExitObserved;
    private bool houseExitedDuringTutorial;

    private TutorialStep currentStep;
    private int currentStepIndex = 0;
    private int currentProgress = 0;
    private bool tutorialActive = false;
    private bool isTransitioning = false;
    private bool canAcceptInput = false;
    private bool _completeAfterTypingSubscribed = false;
    private bool _enableInputAfterTypingSubscribed = false;
    private bool _houseExitReleaseSubscribed = false;

    private bool isPausedByMenu = false;
    private UIFlowController observedModalFlow;
    private bool nextStepDeferred = false;
    private bool nextStepPending = false;

    private Queue<TutorialObjectiveType> eventBuffer = new Queue<TutorialObjectiveType>();
    private PlayerController playerController;
    private PlayerMovementController playerMovementController;

    public bool IsTutorialActive() => tutorialActive;
    public bool RequiresUsableLoadout => tutorialActive && sequence != null && sequence.requireUsableLoadout;

    public bool CanStartTutorialNight(int legacyMinimumStepOrder)
    {
        if (!tutorialActive) return true;
        if (sequence != null && sequence.useExplicitNightPermission)
            return currentStep != null && currentStep.allowNightTransition;
        return GetCurrentStepOrder() >= legacyMinimumStepOrder;
    }
    public bool CanAcceptPlayerInput()
    {
        if (!tutorialActive) return true;

        if (isPausedByMenu || !canAcceptInput) return false;

        if (currentStep != null && currentStep.objectiveType == TutorialObjectiveType.UseRitualAltar && tutorialUI != null && tutorialUI.IsTyping)
            return false;

        return true;
    }
    
    private void ApplyInputGatingForStep(TutorialStep step)
    {
        if (step == null) return;

        if (step.objectiveType == TutorialObjectiveType.Move)
        {
            if (playerMovementController != null) playerMovementController.SetTutorialMovementBlocked(false);
            if (playerController != null) playerController.SetTutorialActionBlocked(false);
            return;
        }

        if (step.isGatedStep)
        {
            if (playerMovementController != null) playerMovementController.SetTutorialMovementBlocked(true);
            if (playerController != null) playerController.SetTutorialActionBlocked(true);
        }
        else
        {
            if (playerMovementController != null) playerMovementController.SetTutorialMovementBlocked(false);
            if (playerController != null) playerController.SetTutorialActionBlocked(false);
        }
    }
    
    private void ArmMoveTriggerIfNeeded(TutorialStep step)
    {
        if (step == null) return;

        if (step.objectiveType != TutorialObjectiveType.Move) return;

        if (playerMovementController != null) playerMovementController.ResetHasMovedForTutorial();
        if (playerMovementController != null && playerMovementController.IsMoving)
        {
            TutorialEvents.InvokePlayerMoved();
        }
    }
    
    private void PreApplyGatingForUpcomingStep()
    {
        int nextIndex = currentStepIndex + 1;
        if (tutorialSteps == null || nextIndex >= tutorialSteps.Count) return;

        var nextStep = tutorialSteps[nextIndex];
        if (nextStep == null) return;

        if (nextStep.isGatedStep)
        {
            if (playerController != null)
            {
                if (playerMovementController != null) playerMovementController.SetTutorialMovementBlocked(true);
                if (playerController != null) playerController.SetTutorialActionBlocked(true);
            }
            
            canAcceptInput = false;
        }
    }
    
    private void DisplayStep(TutorialStep step, bool force = false)
    {
        if (step == null) return;
        if (isPausedByMenu) return;

        ApplyInputGatingForStep(step);
        ArmMoveTriggerIfNeeded(step);

        bool hide = step.enterActions != null && step.enterActions.Count > 0 &&
            step.enterActions[step.enterActions.Count - 1] == TutorialStepAction.HidePanel;
        if (tutorialUI != null)
        {
            if (hide) tutorialUI.HideStepImmediate();
            else if (force) tutorialUI.ForceShowStep(step);
            else tutorialUI.ShowStep(step);
        }

        // Remove stale movement events that might have occurred before the step became visible
        RemoveBufferedObjective(TutorialObjectiveType.Move);

        // Special case: for the Ritual Altar step we must prevent player input until the UI finishes typing
        // A deliberately hidden panel cannot emit TypingFinished; do not arm a typing gate.
        if (!hide && step.objectiveType == TutorialObjectiveType.UseRitualAltar && tutorialUI != null)
        {
            // Subscribe before disabling input to avoid missing the event
            if (!_enableInputAfterTypingSubscribed)
            {
                tutorialUI.TypingFinished += OnUITypingFinishedToEnableInput;
                _enableInputAfterTypingSubscribed = true;
            }

            // Ensure input is gated until typing finishes
            canAcceptInput = false;
            if (playerController != null)
                if (playerController != null) playerController.SetTutorialActionBlocked(true);
            
            // Cancel any scheduled buffer processing that might re-enable input prematurely
            CancelInvoke(nameof(ProcessBufferAndEnableInput));
            // Don't schedule processing of the buffer now; it will be scheduled when typing finishes
            return;
        }

        // Schedule processing of the event buffer after a small delay so UI/LevelManager states settle
        Invoke(nameof(ProcessBufferAndEnableInput), bufferProcessDelay);
    }

    private void Awake()
    {
        if (pauseController == null) pauseController = FindObjectOfType<PauseController>();
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        InitializePlayerController();

        if (enableTutorial && ((sequence != null && sequence.steps.Count > 0) || (tutorialSteps != null && tutorialSteps.Count > 0)))
        {
            Invoke(nameof(StartTutorial), 0.5f);
        }
    }

    private void OnEnable()
    {
        SubscribeToEvents();
    }

    private void OnDisable()
    {
        UnsubscribeFromEvents();
        ResetStepWork();
        tutorialActive = false;
        currentStep = null;
        if (playerController != null) playerController.SetTutorialActionBlocked(false);
        if (playerMovementController != null) playerMovementController.SetTutorialMovementBlocked(false);
        if (tutorialUI != null) tutorialUI.HideStepImmediate();
    }

    private void ResetStepWork()
    {
        CancelInvoke();
        StopAllCoroutines();
        ClearTypingSubscriptionIfNeeded();
        UIEvents.OnInventoryClosed -= ShowPendingStepFromMenu;
        UIEvents.OnPauseMenuClosed -= ShowPendingStepFromMenu;
        if (tutorialUI != null) tutorialUI.CancelPendingStep();
        TutorialEvents.OnHouseExited -= OnHouseExitedToReleaseNextStep;
        _houseExitReleaseSubscribed = false;
        eventBuffer.Clear();
        firstPlantReadyObserved = false;
        loadoutConfirmedObserved = houseExitObserved = houseExitedDuringTutorial = false;
        nextStepDeferred = nextStepPending = isPausedByMenu = false;
    }

    private void SubscribeToEvents()
    {
        // Split subscriptions into tutorial-related and UI-related groups for clarity
        SubscribeTutorialEventHandlers();
        SubscribeUIEventHandlers();
    }

    private void SubscribeTutorialEventHandlers()
    {
        TutorialEvents.OnPlayerMoved += CheckObjective_PlayerMoved;
        TutorialEvents.OnGroundDug += CheckObjective_GroundDug;
        TutorialEvents.OnPlantHarvested += CheckObjective_PlantHarvested;
        TutorialEvents.OnSpellCasted += CheckObjective_SpellCasted;
        TutorialEvents.OnNightStarted += CheckObjective_NightStarted;
        TutorialEvents.OnEnemyDefeated += CheckObjective_EnemyDefeated;
        TutorialEvents.OnNightSurvived += CheckObjective_NightSurvived;

        TutorialEvents.OnInventoryOpened += CheckObjective_InventoryOpened;
        TutorialEvents.OnHouseEntered += CheckObjective_HouseEntered;
        TutorialEvents.OnHouseExited += CheckObjective_HouseExited;
        TutorialEvents.OnCraftingClosed += CheckObjective_CraftingOpened;
        TutorialEvents.OnRestorationClosed += CheckObjective_RestorationOpened;
        TutorialEvents.OnDailyLoadoutOpened += CheckObjective_DailyLoadoutOpened;
        TutorialEvents.OnDailyLoadoutProximity += CheckObjective_DailyLoadoutProximity;
        TutorialEvents.OnRitualAltarUsed += CheckObjective_RitualAltarUsed;
        TutorialEvents.OnSeedTooltipDisplayed += CheckObjective_SeedTooltipDisplayed;

        TutorialEvents.OnProductionPlantPlanted += CheckObjective_ProductionPlantPlanted;

        TutorialEvents.OnDefensivePlantPlanted += CheckObjective_DefensivePlantPlanted;

        TutorialEvents.OnCraftingProximity += CheckObjective_CraftingProximity;
        TutorialEvents.OnRestorationProximity += CheckObjective_RestorationProximity;
        TutorialEvents.OnRitualAltarProximity += CheckObjective_RitualAltarProximity;

        TutorialEvents.OnFirstPlantReadyToHarvest += CheckObjective_FirstPlantReady;
        TutorialEvents.OnAbilityChanged += CheckObjective_AbilityChanged;
        TutorialEvents.OnTeleportCasted += CheckObjective_TeleportCasted;
    }

    private void RefreshModalSubscription()
    {
        var flow = UIManager.Instance != null ? UIManager.Instance.Flow : null;
        if (observedModalFlow == flow) return;
        if (observedModalFlow != null) observedModalFlow.ModalChanged -= OnModalChanged;
        observedModalFlow = flow;
        if (observedModalFlow != null) observedModalFlow.ModalChanged += OnModalChanged;
    }

    private void OnModalChanged(UIModal modal)
    {
        if (!tutorialActive) return;
        if (modal != UIModal.None) PauseTutorial();
        else if (isPausedByMenu && !IsLevelPausedOrInInventory()) ShowPendingStepFromMenu();
    }

    private void SubscribeUIEventHandlers()
    {
        RefreshModalSubscription();
        UIEvents.OnPauseMenuRequested += PauseTutorial;
        UIEvents.OnPauseMenuClosed += ResumeTutorial;
        UIEvents.OnInventoryOpened += PauseTutorial;
        UIEvents.OnInventoryClosed += ResumeTutorial;
    }

    private void UnsubscribeFromEvents()
    {
        UnsubscribeTutorialEventHandlers();
        UnsubscribeUIEventHandlers();
    }

    private void UnsubscribeTutorialEventHandlers()
    {
        TutorialEvents.OnPlayerMoved -= CheckObjective_PlayerMoved;
        TutorialEvents.OnGroundDug -= CheckObjective_GroundDug;
        TutorialEvents.OnPlantHarvested -= CheckObjective_PlantHarvested;
        TutorialEvents.OnSpellCasted -= CheckObjective_SpellCasted;
        TutorialEvents.OnNightStarted -= CheckObjective_NightStarted;
        TutorialEvents.OnEnemyDefeated -= CheckObjective_EnemyDefeated;
        TutorialEvents.OnNightSurvived -= CheckObjective_NightSurvived;

        TutorialEvents.OnInventoryOpened -= CheckObjective_InventoryOpened;
        TutorialEvents.OnHouseEntered -= CheckObjective_HouseEntered;
        TutorialEvents.OnHouseExited -= CheckObjective_HouseExited;
        TutorialEvents.OnCraftingClosed -= CheckObjective_CraftingOpened;
        TutorialEvents.OnRestorationClosed -= CheckObjective_RestorationOpened;
        TutorialEvents.OnDailyLoadoutOpened -= CheckObjective_DailyLoadoutOpened;
        TutorialEvents.OnDailyLoadoutProximity -= CheckObjective_DailyLoadoutProximity;
        TutorialEvents.OnRitualAltarUsed -= CheckObjective_RitualAltarUsed;
        TutorialEvents.OnSeedTooltipDisplayed -= CheckObjective_SeedTooltipDisplayed;

        TutorialEvents.OnProductionPlantPlanted -= CheckObjective_ProductionPlantPlanted;

        TutorialEvents.OnDefensivePlantPlanted -= CheckObjective_DefensivePlantPlanted;

        TutorialEvents.OnCraftingProximity -= CheckObjective_CraftingProximity;
        TutorialEvents.OnRestorationProximity -= CheckObjective_RestorationProximity;
        TutorialEvents.OnRitualAltarProximity -= CheckObjective_RitualAltarProximity;

        TutorialEvents.OnFirstPlantReadyToHarvest -= CheckObjective_FirstPlantReady;
        TutorialEvents.OnAbilityChanged -= CheckObjective_AbilityChanged;
        TutorialEvents.OnTeleportCasted -= CheckObjective_TeleportCasted;

        // Clean up any dynamic subscription we may have added for waiting on house exit
        if (_houseExitReleaseSubscribed)
        {
            TutorialEvents.OnHouseExited -= OnHouseExitedToReleaseNextStep;
            _houseExitReleaseSubscribed = false;
        }
    }

    private void UnsubscribeUIEventHandlers()
    {
        if (observedModalFlow != null) observedModalFlow.ModalChanged -= OnModalChanged;
        observedModalFlow = null;
        UIEvents.OnPauseMenuRequested -= PauseTutorial;
        UIEvents.OnPauseMenuClosed -= ResumeTutorial;
        UIEvents.OnInventoryOpened -= PauseTutorial;
        UIEvents.OnInventoryClosed -= ResumeTutorial;
    }

    public void StartTutorial()
    {
        ResetStepWork();
        tutorialActive = false;
        currentStep = null;
        if (sequence != null && sequence.steps.Count > 0)
            tutorialSteps = new List<TutorialStep>(sequence.steps);
        if (tutorialSteps == null || tutorialSteps.Count == 0 || tutorialSteps.Any(s => s == null))
        {
            return;
        }

        TutorialEvents.ResetTutorialEventFlags();

        tutorialActive = true;
        RefreshModalSubscription();
        if (sequence == null || sequence.steps.Count == 0)
            tutorialSteps = tutorialSteps.OrderBy(s => s.stepOrder).ToList();

        TutorialEvents.InvokeTutorialStarted();
        ShowStep(0);
    }

    private void CompleteCurrentStep()
    {
        if (isTransitioning) return;
        isTransitioning = true;
        canAcceptInput = false;

        TutorialEvents.InvokeStepCompleted(currentStep);
        PreApplyGatingForUpcomingStep();

        // If the just-completed step was entering the house and the following step
        // is "CastSpell", defer showing the CastSpell step until the player exits the house.
        int nextIndex = currentStepIndex + 1;
        if (currentStep != null && currentStep.objectiveType == TutorialObjectiveType.EnterHouse
            && tutorialSteps != null && nextIndex < tutorialSteps.Count
            && tutorialSteps[nextIndex].objectiveType == TutorialObjectiveType.CastSpell
            && !UsesExplicitHouseFlow())
        {
            DeferNextStep();
            if (!_houseExitReleaseSubscribed)
            {
                TutorialEvents.OnHouseExited += OnHouseExitedToReleaseNextStep;
                _houseExitReleaseSubscribed = true;
            }
        }

        // Centralized scheduling for showing the next step
        ScheduleShowNextStep();
    }

    private bool UsesExplicitHouseFlow()
    {
        return sequence != null && (sequence.useExplicitNightPermission ||
            sequence.steps.Any(step => step != null && (step.objectiveType == TutorialObjectiveType.ExitHouse ||
                (step.useConfiguredCompletion && step.conditions != null && step.conditions.Any(condition =>
                    condition != null && condition.objective == TutorialObjectiveType.ExitHouse)))));
    }

    private void OnHouseExitedToReleaseNextStep()
    {
        if (_houseExitReleaseSubscribed)
        {
            TutorialEvents.OnHouseExited -= OnHouseExitedToReleaseNextStep;
            _houseExitReleaseSubscribed = false;
        }

        // Release deferred next step now that the player has exited the house
        ReleaseDeferredNextStep();
    }

    private void ScheduleShowNextStep()
    {
        if (tutorialUI != null)
        {
            tutorialUI.HideStep();
            if (nextStepDeferred)
            {
                nextStepPending = true;
            }
            else
            {
                Invoke(nameof(ShowNextStepDelayed), 0.6f);
            }
        }
        else
        {
            if (nextStepDeferred)
            {
                nextStepPending = true;
            }
            else
            {
                ShowStep(currentStepIndex + 1);
            }
        }
    }
    
    public void DeferNextStep()
    {
        nextStepDeferred = true;
    }

    public void ReleaseDeferredNextStep(bool immediate = false)
    {
        if (!nextStepDeferred && !nextStepPending) return;

        nextStepDeferred = false;

        if (!nextStepPending) return;

        nextStepPending = false;

        if (immediate)
        {

            ShowNextStepDelayed();
        }
        else
        {
            Invoke(nameof(ShowNextStepDelayed), 0.6f);
        }
    }
    
    private void ShowStep(int index)
    {
        CancelInvoke(nameof(ShowNextStepDelayed));
        CancelInvoke(nameof(DelayedForceShowCurrentStep));
        CancelInvoke(nameof(ProcessBufferAndEnableInput));
        StopAllCoroutines();
        ClearTypingSubscriptionIfNeeded();
        eventBuffer.Clear();

        if (index >= tutorialSteps.Count)
        {
            CompleteTutorial();
            return;
        }

        currentStepIndex = index;
        currentStep = tutorialSteps[index];
        currentProgress = 0;
        firstPlantReadyObserved = false;
        loadoutConfirmedObserved = houseExitObserved = false;
        conditionProgress = new int[currentStep.conditions == null ? 0 : currentStep.conditions.Count];
        configuredConditionsMet = conditionProgress.Length == 0;
        configuredWaitFinished = currentStep.waitMode == TutorialWaitMode.None;
        configuredElapsed = 0f;
        configuredClockStarted = false;
        isTransitioning = false;
        canAcceptInput = false;
        if (currentStep.useConfiguredCompletion) StartCoroutine(ConfiguredStepClock());
        if (tutorialUI == null)
        {
            ApplyInputGatingForStep(currentStep);
            Invoke(nameof(ProcessBufferAndEnableInput), bufferProcessDelay);
            return;
        }
        
        if (tutorialUI != null && string.IsNullOrEmpty(currentStep.DisplayText))
        {
            HandleTrackingOnlyStep(currentStep);
            return;
        }

        if (tutorialUI != null)
        {
            if (!string.IsNullOrEmpty(currentStep.DisplayText))
            {
                if (isPausedByMenu || IsLevelPausedOrInInventory())
                {
                    RegisterPendingStepShowHandlers();
                }
                else
                {
                    DisplayStep(currentStep);
                }
            }
        }
    }

    private void RegisterPendingStepShowHandlers()
    {
        isPausedByMenu = true;
        UIEvents.OnInventoryClosed += ShowPendingStepFromMenu;
        UIEvents.OnPauseMenuClosed += ShowPendingStepFromMenu;
    }

    private void ClearTypingSubscriptionIfNeeded()
    {
        if (tutorialUI != null)
        {
            if (_completeAfterTypingSubscribed)
            {
                tutorialUI.TypingFinished -= OnUITypingFinishedToCompleteStep;
                _completeAfterTypingSubscribed = false;
            }

            if (_enableInputAfterTypingSubscribed)
            {
                tutorialUI.TypingFinished -= OnUITypingFinishedToEnableInput;
                _enableInputAfterTypingSubscribed = false;
            }
        }
    }

    private bool IsLevelPausedOrInInventory()
    {
        return (UIManager.Instance?.Flow != null && UIManager.Instance.Flow.HasOpenModal) ||
            (pauseController != null && pauseController.IsPaused);
    }

    private void HandleTrackingOnlyStep(TutorialStep step)
    {
        tutorialUI.HideStepImmediate();
        ApplyInputGatingForStep(step);
        ArmMoveTriggerIfNeeded(step);
        RemoveBufferedObjective(TutorialObjectiveType.Move);
        Invoke(nameof(ProcessBufferAndEnableInput), bufferProcessDelay);
    }

    private void ProcessBufferAndEnableInput()
    {
        if (!tutorialActive || currentStep == null || isTransitioning || isPausedByMenu) return;
        canAcceptInput = true;
        if (currentStep.useConfiguredCompletion)
        {
            configuredClockStarted = true;
            while (eventBuffer.Count > 0 && !isTransitioning)
                RecordConfiguredEvent(eventBuffer.Dequeue());
            TryCompleteConfiguredStep();
            return;
        }

        List<TutorialObjectiveType> remainingEvents = new List<TutorialObjectiveType>();

        while (eventBuffer.Count > 0)
        {
            TutorialObjectiveType bufferedEvent = eventBuffer.Dequeue();

            if (bufferedEvent == currentStep.objectiveType)
            {
                currentProgress++;

                if (currentProgress >= currentStep.requiredCount)
                {
                    if (tutorialUI != null && tutorialUI.IsTyping)
                    {
                        SubscribeTypingFinishedIfNeeded(remainingEvents);
                        return;
                    }

                    CompleteCurrentStep();
                    return;
                }
            }
            else
            {
                remainingEvents.Add(bufferedEvent);
            }
        }

        foreach (var evt in remainingEvents)
        {
            eventBuffer.Enqueue(evt);
        }
    }

    private void SubscribeTypingFinishedIfNeeded(List<TutorialObjectiveType> remainingEvents)
    {
        if (!_completeAfterTypingSubscribed)
        {
            tutorialUI.TypingFinished += OnUITypingFinishedToCompleteStep;
            _completeAfterTypingSubscribed = true;
        }
        
        foreach (var evt in remainingEvents)
        {
            eventBuffer.Enqueue(evt);
        }
    }

    public void ConfirmWaitStep()
    {
        if (tutorialActive && !isTransitioning && !isPausedByMenu && currentStep != null && currentStep.objectiveType == TutorialObjectiveType.Wait)
        {
            if (currentStep.useConfiguredCompletion)
            {
                if (!configuredConditionsMet || currentStep.waitMode != TutorialWaitMode.Continue || (tutorialUI != null && tutorialUI.IsTyping)) return;
                configuredWaitFinished = true;
                TryCompleteConfiguredStep();
                return;
            }
            CompleteCurrentStep();
        }
    }

    private void CheckObjective(TutorialObjectiveType type)
    {
        if (!tutorialActive || currentStep == null) return;

        if (currentStep.useConfiguredCompletion)
        {
            if (isTransitioning) return;
            if (isPausedByMenu)
            {
                // The station emits its opening objective after acquiring the modal.
                // Keep that observation without advancing configured lessons behind it.
                if (type == TutorialObjectiveType.OpenDailyLoadout) eventBuffer.Enqueue(type);
                return;
            }
            if (!canAcceptInput) eventBuffer.Enqueue(type);
            else
            {
                RecordConfiguredEvent(type);
                TryCompleteConfiguredStep();
            }
            return;
        }

        if (currentStep.objectiveType == TutorialObjectiveType.Wait) return;

        if (currentStep.objectiveType != type) return;

        if (isTransitioning) return;

        if (!canAcceptInput)
        {
            eventBuffer.Enqueue(type);
            return;
        }

        currentProgress++;

        if (currentProgress >= currentStep.requiredCount)
        {
            CompleteCurrentStep();
        }
    }

    private void RecordConfiguredEvent(TutorialObjectiveType type)
    {
        for (int i = 0; i < conditionProgress.Length; i++)
        {
            var condition = currentStep.conditions[i];
            bool plantAlias = condition.objective == TutorialObjectiveType.Plant &&
                (type == TutorialObjectiveType.PlantProduction || type == TutorialObjectiveType.PlantDefensive);
            if (condition.objective == type || plantAlias)
                conditionProgress[i] = Mathf.Min(conditionProgress[i] + 1, Mathf.Max(1, condition.requiredCount));
        }
        configuredConditionsMet = conditionProgress.Length == 0 ||
            (currentStep.conditionCombination == TutorialConditionCombination.All
                ? Enumerable.Range(0, conditionProgress.Length).All(i => conditionProgress[i] >= Mathf.Max(1, currentStep.conditions[i].requiredCount))
                : Enumerable.Range(0, conditionProgress.Length).Any(i => conditionProgress[i] >= Mathf.Max(1, currentStep.conditions[i].requiredCount)));
    }

    private void TryCompleteConfiguredStep()
    {
        if (!tutorialActive || isTransitioning || isPausedByMenu || !configuredClockStarted) return;
        if (configuredConditionsMet && configuredWaitFinished && (tutorialUI == null || !tutorialUI.IsTyping))
            CompleteCurrentStep();
    }

    private IEnumerator ConfiguredStepClock()
    {
        var owner = currentStep;
        while (tutorialActive && currentStep == owner && !isTransitioning)
        {
            yield return null;
            if (isPausedByMenu || !configuredClockStarted || (tutorialUI != null && tutorialUI.IsTyping)) continue;
            if (configuredConditionsMet && owner.waitMode == TutorialWaitMode.Duration)
            {
                configuredElapsed += Time.deltaTime;
                configuredWaitFinished = configuredElapsed >= Mathf.Max(0f, owner.waitDuration);
            }
            TryCompleteConfiguredStep();
        }
    }

    private void ShowNextStepDelayed()
    {
        ShowStep(currentStepIndex + 1);
    }

    private void CompleteTutorial()
    {
        ResetStepWork();
        tutorialActive = false;
        canAcceptInput = false;
        currentStep = null;

        if (tutorialUI != null)
        {
            tutorialUI.HideStep();
        }

        if (playerController != null)
            playerController.SetTutorialActionBlocked(false);

        if (playerMovementController != null)
            playerMovementController.SetTutorialMovementBlocked(false);

        TutorialEvents.InvokeTutorialCompleted();
    }

    private void CheckObjective_PlayerMoved() => CheckObjective(TutorialObjectiveType.Move);
    private void CheckObjective_GroundDug() => CheckObjective(TutorialObjectiveType.Dig);
    private void CheckObjective_SeedPlanted() => CheckObjective(TutorialObjectiveType.Plant);
    private void CheckObjective_PlantHarvested() => CheckObjective(TutorialObjectiveType.Harvest);
    private void CheckObjective_SpellCasted() => CheckObjective(TutorialObjectiveType.CastSpell);
    private void CheckObjective_NightStarted() => CheckObjective(TutorialObjectiveType.StartNight);
    private void CheckObjective_EnemyDefeated() => CheckObjective(TutorialObjectiveType.DefeatEnemy);
    private void CheckObjective_NightSurvived() => CheckObjective(TutorialObjectiveType.SurviveNight);
    private void CheckObjective_HouseEntered()
    {
        houseExitedDuringTutorial = false;
        CheckObjective(TutorialObjectiveType.EnterHouse);
    }

    private void CheckObjective_HouseExited()
    {
        if (!tutorialActive) return;
        // Remember the actual callback across display delays, but never consume it while paused/typing.
        houseExitedDuringTutorial = true;
    }
    private void CheckObjective_CraftingOpened() => CheckObjective(TutorialObjectiveType.OpenCrafting);
    private void CheckObjective_RestorationOpened() => CheckObjective(TutorialObjectiveType.OpenRestoration);
    private void CheckObjective_DailyLoadoutOpened() => CheckObjective(TutorialObjectiveType.OpenDailyLoadout);
    private void CheckObjective_DailyLoadoutProximity() => CheckObjective(TutorialObjectiveType.DailyLoadoutProximity);
    private void CheckObjective_RitualAltarUsed() => CheckObjective(TutorialObjectiveType.UseRitualAltar);
    private void CheckObjective_ProductionPlantPlanted() => CheckPlantEvent(TutorialObjectiveType.PlantProduction);
    private void CheckObjective_DefensivePlantPlanted() => CheckPlantEvent(TutorialObjectiveType.PlantDefensive);
    private void CheckPlantEvent(TutorialObjectiveType type)
    {
        // One physical planting event; generic Plant is an alias, not a second event.
        CheckObjective(currentStep != null && !currentStep.useConfiguredCompletion && currentStep.objectiveType == TutorialObjectiveType.Plant
            ? TutorialObjectiveType.Plant : type);
    }
    private void CheckObjective_CraftingProximity() => CheckObjective(TutorialObjectiveType.CraftingProximity);
    private void CheckObjective_RestorationProximity() => CheckObjective(TutorialObjectiveType.RestorationProximity);
    private void CheckObjective_RitualAltarProximity() => CheckObjective(TutorialObjectiveType.RitualAltarProximity);
    private void CheckObjective_FirstPlantReady() => CheckObjective(TutorialObjectiveType.FirstPlantReady);
    private void CheckObjective_TeleportCasted() => CheckObjective(TutorialObjectiveType.TeleportSpell);
    private void CheckObjective_InventoryOpened() => CheckObjective(TutorialObjectiveType.OpenInventory);
    private void CheckObjective_AbilityChanged() => CheckObjective(TutorialObjectiveType.AbilityChanged);
    private void CheckObjective_SeedTooltipDisplayed() => CheckObjective(TutorialObjectiveType.SeedTooltipDisplayed);

    public void SkipTutorial()
    {
        if (!tutorialActive) return;
        StopAllCoroutines();
        CancelInvoke();

        isTransitioning = false;
        canAcceptInput = false;
        eventBuffer.Clear();
        currentStep = null;

        // Ensure dynamic subscriptions are cleaned up
        if (_houseExitReleaseSubscribed)
        {
            TutorialEvents.OnHouseExited -= OnHouseExitedToReleaseNextStep;
            _houseExitReleaseSubscribed = false;
        }

        CompleteTutorial();
    }

    public TutorialObjectiveType GetCurrentObjectiveType()
    {
        if (!tutorialActive || currentStep == null)
        {
            return TutorialObjectiveType.None;
        }

        return currentStep.objectiveType;
    }

    public int GetCurrentStepOrder()
    {
        if (!tutorialActive || currentStep == null)
        {
            return 9999;
        }
        return currentStep.stepOrder;
    }

    public bool IsPlayerGated()
    {
        if (!tutorialActive || currentStep == null)
        {
            return false;
        }

        return currentStep.isGatedStep;
    }

    public void PauseTutorial()
    {
        if (!tutorialActive) return;
        CancelInvoke(nameof(DelayedForceShowCurrentStep));
        if (isPausedByMenu) return;

        isPausedByMenu = true;

        if (tutorialUI != null)
        {
            tutorialUI.HideStepImmediate();
        }
    }

    public void ResumeTutorial()
    {
        if (!tutorialActive || !isPausedByMenu || IsLevelPausedOrInInventory()) return;

        isPausedByMenu = false;

        if (currentStep != null && !isTransitioning)
        {
            DisplayStep(currentStep, true);
        }
    }

    private void ShowPendingStepFromMenu()
    {
        if (!tutorialActive || IsLevelPausedOrInInventory()) return;
        UIEvents.OnInventoryClosed -= ShowPendingStepFromMenu;
        UIEvents.OnPauseMenuClosed -= ShowPendingStepFromMenu;

        isPausedByMenu = false;

        if (tutorialUI != null && currentStep != null && !string.IsNullOrEmpty(currentStep.DisplayText))
        {
            Invoke(nameof(DelayedForceShowCurrentStep), 0.05f);
        }
    }

    private void DelayedForceShowCurrentStep()
    {
        if (IsLevelPausedOrInInventory())
        {
            PauseTutorial();
            return;
        }
        if (tutorialActive && !isTransitioning && currentStep != null)
            DisplayStep(currentStep, true);
    }
    
    private void RemoveBufferedObjective(TutorialObjectiveType type)
    {
        if (eventBuffer == null || eventBuffer.Count == 0) return;
        var newQueue = new Queue<TutorialObjectiveType>();
        while (eventBuffer.Count > 0)
        {
            var evt = eventBuffer.Dequeue();
            if (evt != type) newQueue.Enqueue(evt);
        }
        eventBuffer = newQueue;
    }

    private void ReconcileFirstPlantReady()
    {
        // Wait until this step has initialized and can consume progress. In particular,
        // do not spend the observation during typing, menu pause or house-exit deferral.
        if (!tutorialActive || currentStep == null || firstPlantReadyObserved ||
            isTransitioning || isPausedByMenu || !canAcceptInput || nextStepDeferred || nextStepPending ||
            (tutorialUI != null && tutorialUI.IsTyping)) return;

        bool needsReadiness = currentStep.useConfiguredCompletion
            ? currentStep.conditions != null && currentStep.conditions.Any(condition =>
                condition != null && condition.objective == TutorialObjectiveType.FirstPlantReady)
            : currentStep.objectiveType == TutorialObjectiveType.FirstPlantReady;
        if (!needsReadiness || PlantManager.Instance == null) return;
        if (PlantManager.Instance.GetHarvestablePlants().Count == 0) return;

        // Availability is one state observation per step, never a count of frames.
        // Use existing progression without consuming the public event's global one-shot flag.
        firstPlantReadyObserved = true;
        CheckObjective(TutorialObjectiveType.FirstPlantReady);
    }

    private bool NeedsObjective(TutorialObjectiveType objective)
    {
        return currentStep.useConfiguredCompletion
            ? currentStep.conditions != null && currentStep.conditions.Any(condition =>
                condition != null && condition.objective == objective)
            : currentStep.objectiveType == objective;
    }

    private void ReconcileLoadoutAndHouseState()
    {
        if (!tutorialActive || currentStep == null || isTransitioning || isPausedByMenu ||
            !canAcceptInput || nextStepDeferred || nextStepPending ||
            (tutorialUI != null && tutorialUI.IsTyping)) return;

        if (!loadoutConfirmedObserved && NeedsObjective(TutorialObjectiveType.ConfirmDailyLoadout))
        {
            // Read the run owner's real transaction state, never CommittedChanged (also initialization).
            // Include inactive owners so presentation activation is not an equipment prerequisite.
            var owner = FindObjectOfType<PlayerContentUnlockSystem>(true);
            var session = owner != null ? owner.Loadout : null;
            if (session != null && session.IsReady && session.HasConfirmedToday && session.HasUsableCommitted)
            {
                loadoutConfirmedObserved = true;
                CheckObjective(TutorialObjectiveType.ConfirmDailyLoadout);
                // Completion can synchronously replace currentStep when no panel is installed.
                return;
            }
        }

        if (!houseExitObserved && NeedsObjective(TutorialObjectiveType.ExitHouse))
        {
            var world = FindObjectOfType<WorldTransitionAnimator>();
            bool outside = world != null ? !world.IsInInterior && !world.IsTransitioning : houseExitedDuringTutorial;
            if (outside)
            {
                houseExitObserved = true;
                CheckObjective(TutorialObjectiveType.ExitHouse);
            }
        }
    }

    private void Update()
    {
        RefreshModalSubscription();
        if (tutorialActive && observedModalFlow != null && observedModalFlow.HasOpenModal) PauseTutorial();
        ReconcileFirstPlantReady();
        ReconcileLoadoutAndHouseState();
        if (!isPausedByMenu) return;

        var lm = LevelManager.Instance;
        if (lm == null) return;
        
        if ((pauseController == null || !pauseController.IsPaused) && !(UIManager.Instance?.Flow != null && UIManager.Instance.Flow.HasOpenModal))
        {
            ShowPendingStepFromMenu();
        }

        if (Input.GetKeyDown(KeyCode.L))
        {
            CompleteCurrentStep();
        }
    }

    private void OnUITypingFinishedToCompleteStep()
    {
        // Unsubscribe to avoid repeated triggers
        if (tutorialUI != null) tutorialUI.TypingFinished -= OnUITypingFinishedToCompleteStep;
        _completeAfterTypingSubscribed = false;

        // Delay a bit to allow the player to read final characters
        StartCoroutine(DelayedCompleteAfterTypingCoroutine());
    }
    
    private void OnUITypingFinishedToEnableInput()
    {
        if (tutorialUI != null) tutorialUI.TypingFinished -= OnUITypingFinishedToEnableInput;
        _enableInputAfterTypingSubscribed = false;

        // Re-apply input gating for the current step (this will re-enable input when appropriate)
        ApplyInputGatingForStep(currentStep);

        // Give a short delay to let UI settle before processing buffered events
        Invoke(nameof(ProcessBufferAndEnableInput), bufferProcessDelay);
    }

    private IEnumerator DelayedCompleteAfterTypingCoroutine()
    {
        yield return new WaitForSeconds(1f);
        if (!isTransitioning)
        {
            CompleteCurrentStep();
        }
    }

    private void InitializePlayerController()
    {
        playerController = FindObjectOfType<PlayerController>();
        playerMovementController = FindObjectOfType<PlayerMovementController>();
    }
}
