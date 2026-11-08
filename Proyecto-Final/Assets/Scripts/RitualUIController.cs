using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class RitualUIController : UIControllerBase
{
    [Header("Ritual Overlay")]
    [SerializeField] private Image ritualOverlayImage;
    [SerializeField] private Sprite[] ritualMoonPhaseSprites = new Sprite[7];
    [SerializeField] private float overlayFadeDuration = 1.2f;

    private Coroutine ritualOverlayCoroutine;

    protected override void CacheReferences() { }

    protected override void ConfigureInitialState()
    {
        SetupRitualOverlay();
    }

    private void SetupRitualOverlay()
    {
        if (ritualOverlayImage == null)
        {
            return;
        }

        Color color = ritualOverlayImage.color;
        color.a = 0f;
        ritualOverlayImage.color = color;
        ritualOverlayImage.gameObject.SetActive(false);
    }

    public void ShowRitualOverlay()
    {
        if (ritualOverlayImage == null)
        {
            return;
        }

        UpdateRitualSpriteForCurrentMoonPhase();

        if (ritualOverlayCoroutine != null)
        {
            StopCoroutine(ritualOverlayCoroutine);
        }

        if (!ritualOverlayImage.gameObject.activeInHierarchy)
        {
            ritualOverlayImage.gameObject.SetActive(true);
        }

        ritualOverlayCoroutine = StartCoroutine(FadeRitualOverlay(0f, 1f));
    }

    public void HideRitualOverlay()
    {
        if (ritualOverlayImage == null)
        {
            return;
        }

        if (ritualOverlayCoroutine != null)
        {
            StopCoroutine(ritualOverlayCoroutine);
        }

        float currentAlpha = ritualOverlayImage.color.a;

        ritualOverlayCoroutine = StartCoroutine(FadeRitualOverlay(currentAlpha, 0f));
    }


    private void UpdateRitualSpriteForCurrentMoonPhase()
    {
        if (ritualOverlayImage == null ||
            ritualMoonPhaseSprites == null ||
            ritualMoonPhaseSprites.Length == 0)
        {
            return;
        }

        if (DayCycleController.Instance == null)
        {
            Debug.LogWarning(
                "[RitualUIController] DayCycleController not found."
            );
            return;
        }

        int totalNights = DayCycleController.Instance.TotalNights;

        GamePhase currentPhase = GameFlowController.Instance != null
            ? GameFlowController.Instance.CurrentPhase
            : GamePhase.Day;

        int targetNight = currentPhase == GamePhase.Night
            ? totalNights
            : totalNights + 1;

        if (targetNight < 1)
            targetNight = 1;

        int phaseIndex = (targetNight - 1) % ritualMoonPhaseSprites.Length;

        Sprite moonSprite = ritualMoonPhaseSprites[phaseIndex];

        if (moonSprite == null)
        {
            Debug.LogWarning(
                $"[RitualUIController] Missing moon sprite for night {targetNight}, index {phaseIndex}."
            );
            return;
        }

        ritualOverlayImage.sprite = moonSprite;

        Debug.Log(
            $"[RitualUIController] Showing moon for night {targetNight}. " +
            $"Sprite index: {phaseIndex}"
        );
    }


    private IEnumerator FadeRitualOverlay(float from, float to)
    {
        if (ritualOverlayImage == null)
        {
            yield break;
        }

        if (to > 0f && !ritualOverlayImage.gameObject.activeInHierarchy)
        {
            ritualOverlayImage.gameObject.SetActive(true);
        }

        float elapsed = 0f;
        Color color = ritualOverlayImage.color;

        while (elapsed < overlayFadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / overlayFadeDuration;

            color.a = Mathf.Lerp(from, to, t);
            ritualOverlayImage.color = color;

            yield return null;
        }

        color.a = to;
        ritualOverlayImage.color = color;

        if (Mathf.Approximately(to, 0f))
        {
            ritualOverlayImage.gameObject.SetActive(false);
        }

        ritualOverlayCoroutine = null;
    }

    public void ForceUpdateRitualSprite()
    {
        UpdateRitualSpriteForCurrentMoonPhase();
    }
}