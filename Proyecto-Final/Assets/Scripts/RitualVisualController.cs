using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class RitualVisualController : MonoBehaviour
{
    [Header("Visual Effects")]
    [SerializeField] private Light2D[] candleLights;
    [SerializeField] private Color candleColor = new Color(0.7f, 0.3f, 1f);
    [SerializeField] private Light2D DoorLight;
    [SerializeField] private float ritualLightFadeDuration = 2f;

    [Header("Candle Settings")]
    [SerializeField] private float candleFlickerSpeed = 2f;
    [SerializeField] private float candleFlickerAmount = 0.3f;
    [SerializeField] private float candleIgnitionDelay = 0.3f;

    [Header("Post Processing")]
    [SerializeField] private float ritualVignetteIntensity = 0.6f;
    [SerializeField] private bool centerVignetteOnPlayer = true;
    [SerializeField] private float vignetteFadeDuration = 1.5f;

    private const float DAY_VIGNETTE_INTENSITY = 0.0f;
    private const float NIGHT_VIGNETTE_INTENSITY = 0.1f;
    private const float RITUAL_LIGHT_DIM = 0.05f;
    private const float RITUAL_PULSE_SPEED = 1.5f;

    [Header("References")]
    private DayNightLightController lightController;
    private WorldTransitionAnimator worldTransition;
    private Camera mainCamera;
    private Vignette vignetteComponent;
    private GameObject player;

    private bool ritualActive = false;
    private bool candlesLitAfterRitual = false;
    private bool interiorLightsDimmed = false;
    private float doorLightOriginalIntensity;
    private Coroutine activeDoorLightCoroutine;
    private bool isSubscribed = false;

    private void Start()
    {
        CacheReferences();
        InitializeComponents();
        SubscribeToEvents();
    }

    private void OnEnable()
    {
        if (lightController != null || worldTransition != null || player != null)
        {
            SubscribeToEvents();
        }
    }

    private void OnDisable()
    {
        UnsubscribeFromEvents();
    }

    private void OnDestroy()
    {
        UnsubscribeFromEvents();
    }

    private void CacheReferences()
    {
        player = GameObject.FindGameObjectWithTag("Player");
        lightController = FindObjectOfType<DayNightLightController>();
        worldTransition = FindObjectOfType<WorldTransitionAnimator>();
        mainCamera = Camera.main;

        if (DoorLight != null)
        {
            doorLightOriginalIntensity = DoorLight.intensity > 0.1f ? DoorLight.intensity : 1.5f;
            DoorLight.intensity = 0f;
        }
    }

    private void InitializeComponents()
    {
        SetupVignette();
        TurnOffAllCandles();
    }

    private void SetupVignette()
    {
        if (lightController?.globalVolume == null) return;

        if (lightController.globalVolume.profile.TryGet<Vignette>(out vignetteComponent))
        {
            vignetteComponent.center.overrideState = true;
            vignetteComponent.intensity.overrideState = true;
        }
    }

    private void SubscribeToEvents()
    {
        if (isSubscribed) return;

        if (GameFlowController.Instance != null)
        {
            GameFlowController.Instance.OnPhaseChanged += OnGameStateChangedHandler;
        }

        if (worldTransition != null)
        {
            worldTransition.OnStateChanged += HandleWorldStateChanged;
        }

        isSubscribed = true;
    }

    private void UnsubscribeFromEvents()
    {
        if (!isSubscribed) return;

        if (worldTransition != null)
        {
            worldTransition.OnStateChanged -= HandleWorldStateChanged;
        }

        if (GameFlowController.Instance != null)
        {
            GameFlowController.Instance.OnPhaseChanged -= OnGameStateChangedHandler;
        }

        isSubscribed = false;
    }

    private void OnGameStateChangedHandler(GamePhase newPhase)
    {
        if (newPhase == GamePhase.Day)
        {
            SetDoorLightIntensity(0f, 0.5f);
        }
    }

    private void HandleWorldStateChanged(WorldState newWorldState)
    {
        if (newWorldState == WorldState.Interior)
        {
            if (activeDoorLightCoroutine != null)
            {
                StopCoroutine(activeDoorLightCoroutine);
                activeDoorLightCoroutine = null;
            }

            if (DoorLight != null)
            {
                DoorLight.intensity = 0f;
                DoorLight.gameObject.SetActive(false);
            }

            if (candlesLitAfterRitual)
            {
                StartCoroutine(ExtinguishCandlesGradually());
                candlesLitAfterRitual = false;
            }

            if (interiorLightsDimmed && worldTransition != null)
            {
                worldTransition.RestoreInteriorLightIntensity(vignetteFadeDuration);
                interiorLightsDimmed = false;
            }
        }
        else
        {
            if (candlesLitAfterRitual)
            {
                StartCoroutine(ExtinguishCandlesGradually());
                candlesLitAfterRitual = false;
            }

            if (interiorLightsDimmed && worldTransition != null)
            {
                worldTransition.RestoreInteriorLightIntensity(vignetteFadeDuration);
                interiorLightsDimmed = false;
            }

            GamePhase currentPhase = GameFlowController.Instance != null ? GameFlowController.Instance.CurrentPhase : GamePhase.Day;
            if (currentPhase == GamePhase.Night)
            {
                StartCoroutine(FadeInRitualLight(0.5f));
            }
            else
            {
                SetDoorLightIntensity(0f, 0.5f);
            }
        }
    }

    public void BeginRitual(float ritualDuration)
    {
        ritualActive = true;

        StartCoroutine(RitualLightingSequence(ritualDuration));

        if (centerVignetteOnPlayer)
        {
            StartCoroutine(UpdateVignetteContinuously());
        }

        if (worldTransition != null && worldTransition.IsInInterior)
        {
            worldTransition.SetInteriorLightIntensity(0.1f, vignetteFadeDuration);
            interiorLightsDimmed = true;
        }

        if (DoorLight != null && DoorLight.gameObject.activeInHierarchy)
        {
            StartCoroutine(FadeOutDoorLight());
        }
    }

    public void StartEndRitualEffects()
    {
        SoundManager.Instance?.Play("CandleOff", SoundSourceType.Localized, transform); // heard near the altar only

        StartCoroutine(RestoreVignetteCoroutine());

        if (lightController != null)
        {
            lightController.RestoreLightAfterRitual(GamePhase.Night, vignetteFadeDuration);
        }
    }

    public IEnumerator FinishEndRitualEffects()
    {
        yield return FadeInRitualLight();
        ritualActive = false;
        candlesLitAfterRitual = true;
    }

    public void ForceStopAndRestore()
    {
        StopAllCoroutines();
        activeDoorLightCoroutine = null;
        ritualActive = false;

        TurnOffAllCandles();
        RestoreEnvironment();
    }

    private void RestoreEnvironment()
    {
        RestoreVignetteImmediate();

        if (lightController != null)
        {
            GamePhase currentPhase = GameFlowController.Instance?.CurrentPhase ?? GamePhase.Day;
            lightController.RestoreLightAfterRitual(currentPhase, 0.5f);
        }

        if (worldTransition != null && worldTransition.IsInInterior && interiorLightsDimmed)
        {
            worldTransition.RestoreInteriorLightIntensity(0.5f);
            interiorLightsDimmed = false;
        }

        if (DoorLight != null)
        {
            GamePhase currentPhase = GameFlowController.Instance != null ? GameFlowController.Instance.CurrentPhase : GamePhase.Day;

            if (currentPhase == GamePhase.Night)
            {
                StartCoroutine(FadeInRitualLight(0.5f));
            }
            else
            {
                SetDoorLightIntensity(0f, 0.5f);
            }
        }
    }

    private IEnumerator RitualLightingSequence(float ritualDuration)
    {
        StartCoroutine(ApplyRitualVignette());
        lightController?.DimLightForRitual(RITUAL_LIGHT_DIM, vignetteFadeDuration);

        yield return new WaitForSeconds(vignetteFadeDuration);
        yield return LightCandlesSequentially(ritualDuration);
    }

    private IEnumerator LightCandlesSequentially(float ritualDuration)
    {
        if (candleLights == null || candleLights.Length == 0) yield break;

        for (int i = 0; i < candleLights.Length; i++)
        {
            IgniteCandle(i, ritualDuration);
            yield return new WaitForSeconds(candleIgnitionDelay);
        }
    }

    private void IgniteCandle(int index, float ritualDuration)
    {
        if (candleLights[index] == null) return;

        Light2D candle = candleLights[index];
        candle.gameObject.SetActive(true);
        candle.color = candleColor;

        SoundManager.Instance?.Play("CandleOn", SoundSourceType.Localized, transform);
        StartCoroutine(FlickerCandle(candle, index, ritualDuration));
    }

    private IEnumerator FlickerCandle(Light2D candle, int candleIndex, float ritualDuration)
    {
        if (candle == null) yield break;

        float originalIntensity = candle.intensity;
        float randomOffset = candleIndex * 0.5f;
        float maxDuration = ritualDuration - (candleIndex * candleIgnitionDelay);
        float elapsed = 0f;

        while (elapsed < maxDuration && ritualActive)
        {
            elapsed += Time.deltaTime;

            float flicker = Mathf.Sin((elapsed + randomOffset) * candleFlickerSpeed) * candleFlickerAmount;
            float ritualPulse = Mathf.PingPong(elapsed * RITUAL_PULSE_SPEED, 1f) * 0.5f;

            candle.intensity = originalIntensity + flicker + ritualPulse;
            yield return null;
        }

        if (candlesLitAfterRitual || ritualActive)
        {
            StartCoroutine(FlickerCandlePostRitual(candle, candleIndex));
        }
        else
        {
            candle.intensity = originalIntensity;
        }
    }

    private IEnumerator FlickerCandlePostRitual(Light2D candle, int candleIndex)
    {
        if (candle == null) yield break;

        float originalIntensity = candle.intensity;
        float randomOffset = candleIndex * 0.5f;
        float elapsed = 0f;

        while ((candlesLitAfterRitual || ritualActive) && candle.gameObject.activeInHierarchy)
        {
            elapsed += Time.deltaTime;

            float flicker = Mathf.Sin((elapsed + randomOffset) * candleFlickerSpeed) * candleFlickerAmount;
            candle.intensity = originalIntensity + flicker;

            yield return null;
        }

        candle.intensity = originalIntensity;
    }

    private IEnumerator ExtinguishCandlesGradually()
    {
        if (candleLights == null || candleLights.Length == 0) yield break;

        yield return new WaitForSeconds(1f);

        for (int i = candleLights.Length - 1; i >= 0; i--)
        {
            if (candleLights[i] != null && candleLights[i].gameObject.activeInHierarchy)
            {
                candleLights[i].gameObject.SetActive(false);
            }

            yield return new WaitForSeconds(candleIgnitionDelay * 0.5f);
        }
    }

    private void TurnOffAllCandles()
    {
        if (candleLights == null) return;

        foreach (Light2D candle in candleLights)
        {
            if (candle != null)
            {
                candle.gameObject.SetActive(false);
            }
        }

        candlesLitAfterRitual = false;
    }

    private IEnumerator ApplyRitualVignette()
    {
        if (!IsVignetteAvailable()) yield break;

        float currentIntensity = vignetteComponent.intensity.value;

        if (centerVignetteOnPlayer && player != null && mainCamera != null)
        {
            CenterVignetteOnPlayer();
        }

        yield return AnimateVignetteIntensity(currentIntensity, ritualVignetteIntensity, vignetteFadeDuration);
    }

    private void CenterVignetteOnPlayer()
    {
        if (player == null)
            player = GameObject.FindGameObjectWithTag("Player");
        if (mainCamera == null)
            mainCamera = Camera.main;

        if (player == null || mainCamera == null || vignetteComponent == null) return;

        Vector3 playerScreenPos = mainCamera.WorldToViewportPoint(player.transform.position);
        vignetteComponent.center.value = new Vector2(
            Mathf.Clamp01(playerScreenPos.x),
            Mathf.Clamp01(playerScreenPos.y)
        );
    }

    private IEnumerator UpdateVignetteContinuously()
    {
        if (!IsVignetteAvailable() || player == null || mainCamera == null) yield break;

        while (ritualActive)
        {
            CenterVignetteOnPlayer();
            yield return null;
        }
    }

    private IEnumerator AnimateVignetteIntensity(float from, float to, float duration)
    {
        if (!IsVignetteAvailable()) yield break;

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            vignetteComponent.intensity.value = Mathf.Lerp(from, to, t);
            yield return null;
        }

        vignetteComponent.intensity.value = to;
    }

    private IEnumerator RestoreVignetteCoroutine()
    {
        if (!IsVignetteAvailable()) yield break;

        GamePhase currentPhase = GameFlowController.Instance != null ? GameFlowController.Instance.CurrentPhase : GamePhase.Night;
        float targetIntensity = GetVignetteIntensityForState(currentPhase);

        vignetteComponent.center.value = new Vector2(0.5f, 0.5f);
        yield return AnimateVignetteIntensity(vignetteComponent.intensity.value, targetIntensity, 1f);
    }

    private void RestoreVignetteImmediate()
    {
        if (!IsVignetteAvailable()) return;

        GamePhase currentPhase = GameFlowController.Instance?.CurrentPhase ?? GamePhase.Day;
        float targetIntensity = GetVignetteIntensityForState(currentPhase);

        vignetteComponent.center.value = new Vector2(0.5f, 0.5f);
        vignetteComponent.intensity.value = targetIntensity;
    }

    private float GetVignetteIntensityForState(GamePhase phase)
    {
        return phase == GamePhase.Night ? NIGHT_VIGNETTE_INTENSITY : DAY_VIGNETTE_INTENSITY;
    }

    private bool IsVignetteAvailable()
    {
        return lightController != null &&
               lightController.globalVolume != null &&
               vignetteComponent != null;
    }

    private void SetDoorLightIntensity(float targetIntensity, float duration)
    {
        if (DoorLight == null)
        {
            return;
        }

        if (targetIntensity > 0f && !DoorLight.gameObject.activeInHierarchy)
        {
            DoorLight.gameObject.SetActive(true);
        }
        else if (targetIntensity <= 0f && !DoorLight.gameObject.activeInHierarchy)
        {
            DoorLight.intensity = 0f;
            return;
        }

        if (activeDoorLightCoroutine != null)
        {
            StopCoroutine(activeDoorLightCoroutine);
        }

        activeDoorLightCoroutine = StartCoroutine(AnimateDoorLight(targetIntensity, duration));
    }

    private IEnumerator AnimateDoorLight(float targetIntensity, float duration)
    {
        if (DoorLight == null)
        {
            yield break;
        }

        float startIntensity = DoorLight.intensity;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            DoorLight.intensity = Mathf.Lerp(startIntensity, targetIntensity, t);
            yield return null;
        }

        DoorLight.intensity = targetIntensity;

        if (Mathf.Approximately(targetIntensity, 0f))
        {
            DoorLight.gameObject.SetActive(false);
        }

        activeDoorLightCoroutine = null;
    }

    private IEnumerator FadeOutDoorLight()
    {
        SetDoorLightIntensity(0.1f, vignetteFadeDuration);
        yield return new WaitForSeconds(vignetteFadeDuration);
    }

    private IEnumerator FadeInRitualLight(float duration = -1f)
    {
        if (DoorLight == null) yield break;

        float fadeDuration = duration > 0f ? duration : ritualLightFadeDuration;

        if (!DoorLight.gameObject.activeInHierarchy)
        {
            DoorLight.gameObject.SetActive(true);
        }

        SetDoorLightIntensity(doorLightOriginalIntensity, fadeDuration);
        yield return new WaitForSeconds(fadeDuration);
    }
}
