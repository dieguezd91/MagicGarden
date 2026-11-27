using System.Collections;
using UnityEngine;
using System.Collections.Generic;

public class HouseDoor : MonoBehaviour
{
    [Header("Interaction")]
    [SerializeField] private float cooldown = 0.5f;

    [SerializeField] private Collider2D outsideCollider;
    [SerializeField] private Collider2D insideCollider;
    [SerializeField] private Collider2D colliderToDisable;

    [Header("Spawn Points")]
    [SerializeField] private Transform outsideSpawn;
    [SerializeField] private Transform insideSpawn;

    [Header("Door Lock Visual")]
    [SerializeField] private SimpleAnimator doorLockAnimator;

    [Header("Tutorial Gating")]
    [SerializeField] private GameObject tutorialGatedFeedback;
    [SerializeField] private float feedbackDuration = 2.5f;
    private Coroutine feedbackCoroutine;

    [Header("Door Trigger")]
    [SerializeField] private Collider2D interactionTrigger;
    [SerializeField] private float rearmClearance = 0.3f;

    private Collider2D[] playerColliders;
    private bool waitingForPlayerToClearDoor = false;

    private WorldTransitionAnimator worldTransition;
    private float lastUseTime;
    private Transform player;
    private bool canEnterAtNight = true;
    private LevelManager levelManager;
    private NightToDayTransitionController nightTransition;

    private bool hasEnteredOnce = false;
    private Coroutine tutorialCoroutine;

    private bool pendingDoorVisualAnimation = false;
    private bool pendingDoorVisualState = false;



    private bool pendingLockUpdate = false;

    private void Awake()
    {
        worldTransition = FindObjectOfType<WorldTransitionAnimator>();
        if (tutorialGatedFeedback != null) tutorialGatedFeedback.SetActive(false);

        if (interactionTrigger == null)
            interactionTrigger = GetComponent<Collider2D>();
    }

    private void Start()
    {
        levelManager = LevelManager.Instance;
        nightTransition = FindObjectOfType<NightToDayTransitionController>();

        if (insideCollider != null) insideCollider.isTrigger = false;
        if (outsideCollider != null) outsideCollider.isTrigger = false;

        if (levelManager != null)
        {
            GameFlowController.Instance.OnPhaseChanged += OnPhaseChanged;
        }

        if (worldTransition != null)
        {
            worldTransition.OnStateChanged += OnWorldStateChanged;
        }

        UpdateDoorColliders();

        if (GameFlowController.Instance != null)
        {
            bool shouldBeLocked =
                GameFlowController.Instance.CurrentPhase == GamePhase.Night;

            canEnterAtNight = !shouldBeLocked;

            if (doorLockAnimator != null)
                doorLockAnimator.SetVisualState(shouldBeLocked);
        }
    }


    private void Update()
    {
        if (pendingDoorVisualAnimation &&
            doorLockAnimator != null &&
            doorLockAnimator.isActiveAndEnabled)
        {
            pendingDoorVisualAnimation = false;

            doorLockAnimator.SetVisualStateAnimated(
                pendingDoorVisualState
            );
        }

        // Day gameplay is already active under black; show the exterior opening only after the wake is visible.
        if (pendingLockUpdate &&
            (worldTransition == null || !worldTransition.IsInInterior) &&
            (nightTransition == null || !nightTransition.IsTransitioning))
        {
            pendingLockUpdate = false;

            if (GameFlowController.Instance != null)
            {
                bool shouldBeLocked =
                    GameFlowController.Instance.CurrentPhase == GamePhase.Night;

                canEnterAtNight = !shouldBeLocked;

                UpdateLockVisual(shouldBeLocked);
            }
        }
        if (!waitingForPlayerToClearDoor || player == null || interactionTrigger == null)
            return;

        if (worldTransition != null && worldTransition.IsTransitioning)
            return;

        if (IsPlayerClearOfDoor())
        {
            waitingForPlayerToClearDoor = false;
            player = null;
            playerColliders = null;
        }
    }

    private void OnDestroy()
    {
        if (levelManager != null)
        {
            GameFlowController.Instance.OnPhaseChanged -= OnPhaseChanged;
        }

        if (worldTransition != null)
        {
            worldTransition.OnStateChanged -= OnWorldStateChanged;
        }

        if (tutorialCoroutine != null)
        {
            StopCoroutine(tutorialCoroutine);
            tutorialCoroutine = null;
        }
    }

    private void OnPhaseChanged(GamePhase newPhase)
    {
        bool shouldBeLocked = newPhase == GamePhase.Night;

        canEnterAtNight = !shouldBeLocked;

        if ((worldTransition != null && worldTransition.IsInInterior) ||
            (newPhase == GamePhase.Day &&
             nightTransition != null &&
             nightTransition.IsTransitioning))
        {
            pendingLockUpdate = true;
        }
        else
        {
            UpdateLockVisual(shouldBeLocked);
        }
    }

    private void UpdateLockVisual(bool shouldBeLocked)
    {
        if (doorLockAnimator == null)
            return;

        if (!doorLockAnimator.isActiveAndEnabled)
        {
            pendingDoorVisualAnimation = true;
            pendingDoorVisualState = shouldBeLocked;
            return;
        }

        pendingDoorVisualAnimation = false;

        doorLockAnimator.SetVisualStateAnimated(shouldBeLocked);
    }

    private void OnWorldStateChanged(WorldState newWorldState)
    {
        bool isInInterior = newWorldState == WorldState.Interior;

        UpdateDoorColliders(isInInterior);

        if (!isInInterior && tutorialCoroutine != null)
        {
            StopCoroutine(tutorialCoroutine);
            tutorialCoroutine = null;
        }
    }

    private IEnumerator UpdateLockAfterExitDelay(bool shouldBeLocked)
    {
        yield return new WaitForSeconds(0.3f);
        if (nightTransition != null && nightTransition.IsTransitioning)
        {
            pendingLockUpdate = true;
            yield break;
        }
        if (GameFlowController.Instance != null)
            UpdateLockVisual(GameFlowController.Instance.WorldPhase == GamePhase.Night);
    }


    private bool IsPlayerClearOfDoor()
    {
        if (playerColliders == null || playerColliders.Length == 0)
            return false;

        Bounds safeBounds = interactionTrigger.bounds;
        safeBounds.Expand(rearmClearance * 2f);

        foreach (Collider2D playerCollider in playerColliders)
        {
            if (playerCollider == null ||
                !playerCollider.enabled ||
                playerCollider.isTrigger)
                continue;

            if (safeBounds.Intersects(playerCollider.bounds))
                return false;
        }

        return true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        if (waitingForPlayerToClearDoor)
            return;



        if (worldTransition == null || worldTransition.IsTransitioning) return;

        if (!TutorialManager.Allows(TutorialAction.HouseDoor))
        {
            if (tutorialGatedFeedback != null)
            {
                if (feedbackCoroutine != null)
                    StopCoroutine(feedbackCoroutine);

                feedbackCoroutine = StartCoroutine(ShowGatedFeedback());
            }

            return;
        }

        player = other.transform;
        bool goingInside = !worldTransition.IsInInterior;

        if (!goingInside)
        {
            var tutorial = TutorialManager.Instance;

            if (tutorial != null && !tutorial.UsesActionLock && tutorial.IsPlayerGated())
            {
                if (tutorialGatedFeedback != null)
                {
                    if (feedbackCoroutine != null)
                        StopCoroutine(feedbackCoroutine);

                    feedbackCoroutine = StartCoroutine(ShowGatedFeedback());
                }

                return;
            }

            if (colliderToDisable != null)
            {
                colliderToDisable.enabled = true;
            }
        }

        if (goingInside && !canEnterAtNight)
        {
            return;
        }

        if (goingInside && colliderToDisable != null)
        {
            colliderToDisable.enabled = false;
        }

        if (goingInside && GameFlowController.Instance.CurrentPhase == GamePhase.Night)
        {
            canEnterAtNight = false;
        }

        playerColliders = player.GetComponentsInChildren<Collider2D>();

        waitingForPlayerToClearDoor = true;

        HandleTransition(goingInside);

        Physics2D.SyncTransforms();
    }

    


    private IEnumerator ShowGatedFeedback()
    {
        if (tutorialGatedFeedback != null)
        {
            tutorialGatedFeedback.SetActive(true);
            yield return new WaitForSeconds(feedbackDuration);
            tutorialGatedFeedback.SetActive(false);
            feedbackCoroutine = null;
        }
    }

    private void HandleTransition(bool goingInside)
    {
        if (player == null) return;

        if (goingInside && insideSpawn != null)
            player.position = insideSpawn.position;
        else if (!goingInside && outsideSpawn != null)
            player.position = outsideSpawn.position;

        if (goingInside)
        {
            if (doorLockAnimator != null && doorLockAnimator.IsAnimating)
            {
                bool shouldBeLocked = GameFlowController.Instance != null &&
                    GameFlowController.Instance.CurrentPhase == GamePhase.Night;
                doorLockAnimator.SetVisualState(shouldBeLocked);
            }

            worldTransition.EnterHouse();
            if (UIManager.Instance != null)
                UIManager.Instance.ShowInteriorHUD();

            if (tutorialCoroutine != null)
            {
                StopCoroutine(tutorialCoroutine);
            }

            tutorialCoroutine = StartCoroutine(InvokeHouseEnteredDelayed());
        }
        else
        {
            worldTransition.ExitHouse();
            if (UIManager.Instance != null)
                UIManager.Instance.ShowExteriorHUD();

            if (levelManager != null && GameFlowController.Instance.CurrentPhase == GamePhase.Night)
            {
                GameplayEvents.InvokePlayerExitedHouseDuringNight();
            }

            // Notify tutorial system that the player has exited the house (exterior)
            TutorialEvents.InvokeHouseExited();
        }
    }

    private IEnumerator InvokeHouseEnteredDelayed()
    {
        yield return new WaitForSeconds(0.5f);

        if (worldTransition != null && worldTransition.IsInInterior)
        {
            hasEnteredOnce = true;
            TutorialEvents.InvokeHouseEntered();
        }

        tutorialCoroutine = null;
    }

    private void UpdateDoorColliders()
    {
        if (worldTransition == null) return;
        UpdateDoorColliders(worldTransition.IsInInterior);
    }

    private void UpdateDoorColliders(bool isInterior)
    {
        if (insideCollider != null) insideCollider.gameObject.SetActive(isInterior);
        if (outsideCollider != null) outsideCollider.gameObject.SetActive(!isInterior);
    }
}

