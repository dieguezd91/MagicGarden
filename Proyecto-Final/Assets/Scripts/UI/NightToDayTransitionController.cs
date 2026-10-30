
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

    [Header("TRANSITION TIMING")]
    [SerializeField] private float freeMovementDuration = 2f;
    [SerializeField] private float cameraFocusTimeout = 3f;
    [SerializeField] private float beforeFadeDelay = 0.3f;
    [SerializeField] private float fadeInDuration = 1.5f;
    [SerializeField] private float blackScreenDuration = 1f;
    [SerializeField] private float fadeOutDuration = 0.8f;

    [Header("LEVEL UP")]
    [SerializeField] private LevelUpPanelView levelUpPanel;
    [SerializeField] private float levelUpReleaseBuffer = 0.25f;

    [Header("ANIMATION EVENT")]
    [SerializeField] private UnityEvent onPlayerStoppedBeforeFade;

    private Coroutine transitionCoroutine;
    private bool isTransitioning;

    public bool IsTransitioning => isTransitioning;

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
        if (isTransitioning)
            return;

        if (GameFlowController.Instance == null ||
            GameFlowController.Instance.CurrentPhase != GamePhase.Night)
        {
            return;
        }

        isTransitioning = true;
        transitionCoroutine = StartCoroutine(TransitionRoutine());
    }

    private IEnumerator WaitForLevelUpToFinish()
    {
        float clearTimer = 0f;

        while (clearTimer < levelUpReleaseBuffer)
        {
            bool levelUpVisible =
                levelUpPanel != null && levelUpPanel.IsVisible;

            bool gameplayBlocked = UpgradeRuntime.GameplayBlocked;

            if (levelUpVisible || gameplayBlocked)
            {
                clearTimer = 0f;
            }
            else
            {
                clearTimer += Time.unscaledDeltaTime;
            }

            yield return null;
        }
    }

    private IEnumerator TransitionRoutine()
    {

        yield return WaitForLevelUpToFinish();
        yield return new WaitForSeconds(freeMovementDuration);

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

        yield return new WaitForSecondsRealtime(beforeFadeDelay);

        if (blackScreen != null)
        {
            blackScreen.gameObject.SetActive(true);
            blackScreen.raycastTarget = true;

            yield return FadeScreen(1f, fadeInDuration);
        }

        yield return new WaitForSecondsRealtime(blackScreenDuration);

        if (DayCycleController.Instance != null)
        {
            DayCycleController.Instance.StartDay();
        }
        else
        {
            GameFlowController.Instance.SetPhase(GamePhase.Day);
        }

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
            playerMovement.SetDayTransitionBlocked(false);

        isTransitioning = false;
        transitionCoroutine = null;
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
