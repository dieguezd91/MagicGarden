using System.Collections;
using UnityEngine;

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

    private WorldTransitionAnimator worldTransition;
    private float lastUseTime;
    private Transform player;
    private bool canEnterAtNight = true;
    private LevelManager levelManager;

    private bool hasEnteredOnce = false;
    private Coroutine tutorialCoroutine;

    private bool pendingLockUpdate = false;

    private void Awake()
    {
        worldTransition = FindObjectOfType<WorldTransitionAnimator>();
        if (tutorialGatedFeedback != null) tutorialGatedFeedback.SetActive(false);
    }

    private void Start()
    {
        levelManager = LevelManager.Instance;

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

        if (levelManager != null && doorLockAnimator != null)
        {
            bool shouldBeLocked = GameFlowController.Instance.CurrentPhase == GamePhase.Night;
            doorLockAnimator.SetVisualState(shouldBeLocked);
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
        bool shouldBeLocked = false;

        if (newPhase == GamePhase.Day)
        {
            canEnterAtNight = true;
            shouldBeLocked = false;
        }
        else if (newPhase == GamePhase.Night)
        {
            canEnterAtNight = false;
            shouldBeLocked = true;
        }

        if (worldTransition != null && worldTransition.IsInInterior)
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
        {
            return;
        }

        bool currentlyShowingClosed = doorLockAnimator.IsShowingClosedState();

        if (currentlyShowingClosed != shouldBeLocked)
        {
            doorLockAnimator.TriggerAnimation();
        }
    }

    private void OnWorldStateChanged(WorldState newWorldState)
    {
        bool isInInterior = (newWorldState == WorldState.Interior);
        UpdateDoorColliders(isInInterior);

        if (!isInInterior)
        {
            if (pendingLockUpdate)
            {
                pendingLockUpdate = false;

                if (levelManager != null)
                {
                    bool shouldBeLocked = GameFlowController.Instance.CurrentPhase == GamePhase.Night;
                    StartCoroutine(UpdateLockAfterExitDelay(shouldBeLocked));
                }
            }
        }

        if (!isInInterior && tutorialCoroutine != null)
        {
            StopCoroutine(tutorialCoroutine);
            tutorialCoroutine = null;
        }
    }

    private IEnumerator UpdateLockAfterExitDelay(bool shouldBeLocked)
    {
        yield return new WaitForSeconds(0.3f);

        UpdateLockVisual(shouldBeLocked);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        if (worldTransition == null || worldTransition.IsTransitioning) return;
        if (Time.time - lastUseTime < cooldown) return;

        // Progressive tutorial lock: the door works in both directions once the tutorial taught it.
        if (!TutorialManager.Allows(TutorialAction.HouseDoor))
        {
            if (tutorialGatedFeedback != null)
            {
                if (feedbackCoroutine != null) StopCoroutine(feedbackCoroutine);
                feedbackCoroutine = StartCoroutine(ShowGatedFeedback());
            }

            return;
        }

        player = other.transform;
        bool goingInside = !worldTransition.IsInInterior;

        if (!goingInside)
        {
            // Without the progressive lock (legacy lists, unlocked sequences) gated steps still block leaving.
            var tutorial = TutorialManager.Instance;
            if (tutorial != null && !tutorial.UsesActionLock && tutorial.IsPlayerGated())
            {
                if (tutorialGatedFeedback != null)
                {
                    if (feedbackCoroutine != null) StopCoroutine(feedbackCoroutine);
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

        HandleTransition(goingInside);
        lastUseTime = Time.time;
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

