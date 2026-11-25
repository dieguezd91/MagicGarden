
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

public class NightToDayTransitionController : MonoBehaviour
{
    [Header("REFERENCES")]
    [SerializeField] private Image blackScreen;
    [SerializeField] private PlayerMovementController playerMovement;
    [SerializeField] private MoveCamera moveCamera;
    [SerializeField] private DayNightLightController dayNightLightController;
    [SerializeField] private Animator playerAnimator;

    [Header("TRANSITION TIMING")]
    [SerializeField] private float freeMovementDuration = 2f;
    [SerializeField] private float cameraFocusTimeout = 3f;
    [SerializeField] private float beforeFadeDelay = 0.3f;
    [SerializeField] private float fadeInDuration = 1.5f;
    [SerializeField] private float blackScreenDuration = 1f;
    [SerializeField] private float fadeOutDuration = 0.8f;
    [Tooltip("Seconds after fade-out reserved for visible waking before movement and the house-door opening are released.")]
    [SerializeField, Min(0f)] private float wakeHoldDuration = 2.2f;

    [Header("NIGHT REWARD PRESENTATION")]
    [Tooltip("Optional gold feedback to wait for, including its reading hold and fade. Empty = find the active presenter.")]
    [SerializeField] private FloatingTextController collectedFeedback;
    [Tooltip("Optional XP bar to wait for after the gold feedback. Empty = find the active presenter.")]
    [SerializeField] private ExperienceUIController experienceFeedback;
    [Tooltip("Optional queued/playing LvUp indicator. Empty = find the active presenter.")]
    [SerializeField] private LevelUpIndicator levelIndicator;

    [Header("LEVEL UP")]
    [SerializeField] private LevelUpPanelView levelUpPanel;
    [Tooltip("Unscaled quiet time after all feedback, choices, modal close and backdrop fade finish. 0 still waits for completion.")]
    [SerializeField, Min(0f)] private float levelUpReleaseBuffer = 0.25f;

    [Header("ANIMATION EVENT")]
    [SerializeField] private UnityEvent onPlayerStoppedBeforeFade;

    private Coroutine transitionCoroutine;
    private bool isTransitioning;
    private bool presentationCommitted;
    private static readonly int IsSleepingHash = Animator.StringToHash("isSleeping");
    public bool IsTransitioning => isTransitioning;
    public bool IsPlayingAnimation => isActiveAndEnabled && presentationCommitted;

    private void Awake()
    {
        if (blackScreen != null)
        {
            SetAlpha(0f);
            blackScreen.raycastTarget = false;
            blackScreen.gameObject.SetActive(false);
        }
    }

    public void StartTransition()
    {
        if (!isActiveAndEnabled || isTransitioning)
            return;

        if (GameFlowController.Instance == null ||
            GameFlowController.Instance.CurrentPhase != GamePhase.Night)
        {
            return;
        }

        if (collectedFeedback == null) collectedFeedback = FindObjectOfType<FloatingTextController>();
        if (experienceFeedback == null) experienceFeedback = FindObjectOfType<ExperienceUIController>();
        if (levelIndicator == null) levelIndicator = FindObjectOfType<LevelUpIndicator>();
        presentationCommitted = false;
        isTransitioning = true;
        transitionCoroutine = StartCoroutine(TransitionRoutine());
    }

    private IEnumerator WaitForLevelUpToFinish()
    {
        float clearTimer = 0f;

        while (true)
        {
            bool levelUpVisible =
                levelUpPanel != null && levelUpPanel.IsVisible;

            bool gameplayBlocked = UpgradeRuntime.GameplayBlocked;

            var runtime = UpgradeRuntime.Current;
            bool pending = (collectedFeedback != null && collectedFeedback.IsShowingGoldFeedback) ||
                (experienceFeedback != null && experienceFeedback.IsPresentationBusy) ||
                (levelIndicator != null && levelIndicator.HasPendingPresentation) ||
                (runtime != null && runtime.HasPendingLevelPresentation);

            if (levelUpVisible || gameplayBlocked || pending)
            {
                clearTimer = 0f;
            }
            else
            {
                clearTimer += Time.unscaledDeltaTime;
                if (clearTimer >= Mathf.Max(0f, levelUpReleaseBuffer)) yield break;
            }

            yield return null;
        }
    }

    private IEnumerator TransitionRoutine()
    {

        yield return WaitForLevelUpToFinish();
        yield return new WaitForSeconds(freeMovementDuration);
        // Late gains during free movement get their turn too. Commit without yielding after the final clear.
        yield return WaitForLevelUpToFinish();
        presentationCommitted = true;

        if (playerMovement != null)
            playerMovement.SetDayTransitionBlocked(true);

        if (moveCamera != null)
        {
            moveCamera.BeginDayTransitionFocus();

            float timeout = cameraFocusTimeout;

            while (!moveCamera.HasReachedDayTransitionFocus &&
                   timeout > 0f)
            {
                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }
        }

        onPlayerStoppedBeforeFade?.Invoke();

        if (playerAnimator != null)
            playerAnimator.SetBool(IsSleepingHash, true);

        yield return new WaitForSecondsRealtime(beforeFadeDelay);

        if (blackScreen != null)
        {
            blackScreen.gameObject.SetActive(true);
            blackScreen.raycastTarget = true;

            musicCoroutine = StartCoroutine(StartDayMusicAtMidpoint(fadeInDuration + blackScreenDuration * 0.5f));
            yield return FadeScreen(1f, fadeInDuration);
        }

        yield return new WaitForSecondsRealtime(blackScreenDuration);

        if (DayCycleController.Instance != null)
        {
            DayCycleController.Instance.StartDay();
        }
        else if (GameFlowController.Instance != null)
        {
            GameFlowController.Instance.SetPhase(GamePhase.Day);
        }

        if (playerAnimator != null)
            playerAnimator.SetBool(IsSleepingHash, false);

        if (dayNightLightController != null)
            dayNightLightController.ApplyDayVisualsImmediately();

        if (blackScreen != null)
            yield return FadeScreen(0f, fadeOutDuration);

        if (blackScreen != null)
        {
            blackScreen.raycastTarget = false;
            blackScreen.gameObject.SetActive(false);
        }

        if (moveCamera != null)
            moveCamera.EndDayTransitionFocus();


        if (playerMovement != null)
        {
            yield return new WaitForSecondsRealtime(Mathf.Max(0f, wakeHoldDuration));
            playerMovement.SetDayTransitionBlocked(false);
        }

        presentationCommitted = false;
        isTransitioning = false;
        transitionCoroutine = null;
    }

    private void Update()
    {
        var flow = GameFlowController.Instance;
        if (isTransitioning && flow != null && (flow.WorldPhase == GamePhase.GameOver || flow.WorldPhase == GamePhase.MainMenu))
            CancelTransition();
    }

    private void OnDisable() => CancelTransition();

    private Coroutine musicCoroutine;

    private IEnumerator StartDayMusicAtMidpoint(float secondsToMidpoint)
    {
        var music = MusicManager.Instance;
        if (music == null) yield break;
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, secondsToMidpoint - music.CrossfadeSeconds * 0.5f));
        musicCoroutine = null;
        if (MusicManager.Instance != null) MusicManager.Instance.BeginDayTransition();
    }

    private void CancelTransition()
    {
        if (!isTransitioning && transitionCoroutine == null) return;
        if (transitionCoroutine != null) StopCoroutine(transitionCoroutine);
        transitionCoroutine = null;
        if (musicCoroutine != null) StopCoroutine(musicCoroutine);
        musicCoroutine = null;
        if (MusicManager.Instance != null) MusicManager.Instance.CancelEarlyTransition();
        presentationCommitted = false;
        isTransitioning = false;
        SetAlpha(0f);
        if (blackScreen != null)
        {
            blackScreen.raycastTarget = false;
            blackScreen.gameObject.SetActive(false);
        }
        if (playerMovement != null) playerMovement.SetDayTransitionBlocked(false);
        if (moveCamera != null) moveCamera.EndDayTransitionFocus();
        if (playerAnimator != null) playerAnimator.SetBool(IsSleepingHash, false);
    }

    private IEnumerator FadeScreen(float targetAlpha, float duration)
    {
        if (blackScreen == null)
            yield break;

        Color color = blackScreen.color;
        float startAlpha = color.a;

        if (duration <= 0f)
        {
            SetAlpha(targetAlpha);
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(elapsed / duration);
            t = Mathf.SmoothStep(0f, 1f, t);

            color.a = Mathf.Lerp(startAlpha, targetAlpha, t);
            blackScreen.color = color;

            yield return null;
        }

        SetAlpha(targetAlpha);
    }

    private void SetAlpha(float alpha)
    {
        if (blackScreen == null)
            return;

        Color color = blackScreen.color;
        color.a = alpha;
        blackScreen.color = color;
    }
}
