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
    private bool nextStepDeferred = false;
    private bool nextStepPending = false;

    private Queue<TutorialObjectiveType> eventBuffer = new Queue<TutorialObjectiveType>();
    private PlayerController playerController;
    private PlayerMovementController playerMovementController;

    public bool IsTutorialActive() => tutorialActive;
    public bool CanAcceptPlayerInput()
    {
        if (!tutorialActive) return true;

        if (!canAcceptInput) return false;

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

        ApplyInputGatingForStep(step);
        ArmMoveTriggerIfNeeded(step);

        if (tutorialUI != null)
        {
            if (force)
                tutorialUI.ForceShowStep(step);
            else
                tutorialUI.ShowStep(step);
        }

        // Remove stale movement events that might have occurred before the step became visible
        RemoveBufferedObjective(TutorialObjectiveType.Move);

        // Special case: for the Ritual Altar step we must prevent player input until the UI finishes typing
        if (step.objectiveType == TutorialObjectiveType.UseRitualAltar && tutorialUI != null)
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

        if (enableTutorial && tutorialSteps != null && tutorialSteps.Count > 0)
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
        TutorialEvents.OnCraftingClosed += CheckObjective_CraftingOpened;
        TutorialEvents.OnRestorationClosed += CheckObjective_RestorationOpened;
        TutorialEvents.OnDailyLoadoutOpened += CheckObjective_DailyLoadoutOpened;
        TutorialEvents.OnDailyLoadoutProximity += CheckObjective_DailyLoadoutProximity;
        TutorialEvents.OnRitualAltarUsed += CheckObjective_RitualAltarUsed;
        TutorialEvents.OnSeedTooltipDisplayed += CheckObjective_SeedTooltipDisplayed;

        TutorialEvents.OnProductionPlantPlanted += CheckObjective_ProductionPlantPlanted;
        TutorialEvents.OnProductionPlantPlanted += CheckObjective_SeedPlanted;

        TutorialEvents.OnDefensivePlantPlanted += CheckObjective_DefensivePlantPlanted;
        TutorialEvents.OnDefensivePlantPlanted += CheckObjective_SeedPlanted;

        TutorialEvents.OnCraftingProximity += CheckObjective_CraftingProximity;
        TutorialEvents.OnRestorationProximity += CheckObjective_RestorationProximity;
        TutorialEvents.OnRitualAltarProximity += CheckObjective_RitualAltarProximity;

        TutorialEvents.OnFirstPlantReadyToHarvest += CheckObjective_FirstPlantReady;
        TutorialEvents.OnAbilityChanged += CheckObjective_AbilityChanged;
        TutorialEvents.OnTeleportCasted += CheckObjective_TeleportCasted;
    }

    private void SubscribeUIEventHandlers()
    {
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
        TutorialEvents.OnCraftingClosed -= CheckObjective_CraftingOpened;
        TutorialEvents.OnRestorationClosed -= CheckObjective_RestorationOpened;
        TutorialEvents.OnDailyLoadoutOpened -= CheckObjective_DailyLoadoutOpened;
        TutorialEvents.OnDailyLoadoutProximity -= CheckObjective_DailyLoadoutProximity;
        TutorialEvents.OnRitualAltarUsed -= CheckObjective_RitualAltarUsed;
        TutorialEvents.OnSeedTooltipDisplayed -= CheckObjective_SeedTooltipDisplayed;

        TutorialEvents.OnProductionPlantPlanted -= CheckObjective_ProductionPlantPlanted;
        TutorialEvents.OnProductionPlantPlanted -= CheckObjective_SeedPlanted;

        TutorialEvents.OnDefensivePlantPlanted -= CheckObjective_DefensivePlantPlanted;
        TutorialEvents.OnDefensivePlantPlanted -= CheckObjective_SeedPlanted;

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
        UIEvents.OnPauseMenuRequested -= PauseTutorial;
        UIEvents.OnPauseMenuClosed -= ResumeTutorial;
        UIEvents.OnInventoryOpened -= PauseTutorial;
        UIEvents.OnInventoryClosed -= ResumeTutorial;
    }

    public void StartTutorial()
    {
        if (tutorialSteps == null || tutorialSteps.Count == 0)
        {
            return;
        }

        TutorialEvents.ResetTutorialEventFlags();

        tutorialActive = true;
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
            && tutorialSteps[nextIndex].objectiveType == TutorialObjectiveType.CastSpell)
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
        ClearTypingSubscriptionIfNeeded();

        if (index >= tutorialSteps.Count)
        {
            CompleteTutorial();
            return;
        }

        currentStepIndex = index;
        currentStep = tutorialSteps[index];
        currentProgress = 0;
        isTransitioning = false;
        canAcceptInput = false;
        
        if (tutorialUI != null && string.IsNullOrEmpty(currentStep.instructionText))
        {
            HandleTrackingOnlyStep(currentStep);
            return;
        }

        if (tutorialUI != null)
        {
            if (!string.IsNullOrEmpty(currentStep.instructionText))
            {
                if (IsLevelPausedOrInInventory())
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
        var lm = LevelManager.Instance;
        return lm != null && ((UIManager.Instance?.Flow != null && UIManager.Instance.Flow.HasOpenModal) || (pauseController != null && pauseController.IsPaused));
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
        canAcceptInput = true;

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
        if (tutorialActive && !isTransitioning && currentStep != null && currentStep.objectiveType == TutorialObjectiveType.Wait)
        {
            CompleteCurrentStep();
        }
    }

    private void CheckObjective(TutorialObjectiveType type)
    {
        if (!tutorialActive || currentStep == null) return;

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

    private void ShowNextStepDelayed()
    {
        ShowStep(currentStepIndex + 1);
    }

    private void CompleteTutorial()
    {
        tutorialActive = false;
        canAcceptInput = false;

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
    private void CheckObjective_HouseEntered() => CheckObjective(TutorialObjectiveType.EnterHouse);
    private void CheckObjective_CraftingOpened() => CheckObjective(TutorialObjectiveType.OpenCrafting);
    private void CheckObjective_RestorationOpened() => CheckObjective(TutorialObjectiveType.OpenRestoration);
    private void CheckObjective_DailyLoadoutOpened() => CheckObjective(TutorialObjectiveType.OpenDailyLoadout);
    private void CheckObjective_DailyLoadoutProximity() => CheckObjective(TutorialObjectiveType.DailyLoadoutProximity);
    private void CheckObjective_RitualAltarUsed() => CheckObjective(TutorialObjectiveType.UseRitualAltar);
    private void CheckObjective_ProductionPlantPlanted() => CheckObjective(TutorialObjectiveType.PlantProduction);
    private void CheckObjective_DefensivePlantPlanted() => CheckObjective(TutorialObjectiveType.PlantDefensive);
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
        if (!tutorialActive || isPausedByMenu) return;

        isPausedByMenu = true;

        if (tutorialUI != null)
        {
            tutorialUI.HideStepImmediate();
        }
    }

    public void ResumeTutorial()
    {
        if (!tutorialActive || !isPausedByMenu) return;

        isPausedByMenu = false;

        if (tutorialUI != null && currentStep != null)
        {
            ApplyInputGatingForStep(currentStep);
            ArmMoveTriggerIfNeeded(currentStep);

            tutorialUI.ForceShowStep(currentStep);
            Invoke(nameof(ProcessBufferAndEnableInput), bufferProcessDelay);
        }
    }

    private void ShowPendingStepFromMenu()
    {
        UIEvents.OnInventoryClosed -= ShowPendingStepFromMenu;
        UIEvents.OnPauseMenuClosed -= ShowPendingStepFromMenu;

        isPausedByMenu = false;

        if (tutorialUI != null && currentStep != null && !string.IsNullOrEmpty(currentStep.instructionText))
        {
            Invoke(nameof(DelayedForceShowCurrentStep), 0.05f);
        }
    }

    private void DelayedForceShowCurrentStep()
    {
        if (tutorialUI != null && currentStep != null && !string.IsNullOrEmpty(currentStep.instructionText))
        {
            // Re-arm move trigger using existing helper to avoid code duplication
            ArmMoveTriggerIfNeeded(currentStep);

            tutorialUI.ForceShowStep(currentStep);
            RemoveBufferedObjective(TutorialObjectiveType.Move);
            // If this is the ritual altar step we must delay processing until typing finishes
            if (currentStep.objectiveType == TutorialObjectiveType.UseRitualAltar)
            {
                if (!_enableInputAfterTypingSubscribed)
                {
                    tutorialUI.TypingFinished += OnUITypingFinishedToEnableInput;
                    _enableInputAfterTypingSubscribed = true;
                }

                canAcceptInput = false;
                if (playerController != null)
                    if (playerController != null) playerController.SetTutorialActionBlocked(true);
                // Cancel any scheduled buffer processing that might re-enable input prematurely
                CancelInvoke(nameof(ProcessBufferAndEnableInput));
                return;
            }

             // Otherwise, schedule buffer processing normally
             Invoke(nameof(ProcessBufferAndEnableInput), bufferProcessDelay);
         }
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

    private void Update()
    {
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
