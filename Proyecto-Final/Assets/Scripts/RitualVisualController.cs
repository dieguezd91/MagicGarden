using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class RitualVisualController : MonoBehaviour
{
    [Header("Visual Effects")]
    [SerializeField] private Light2D[] candleLights;
    [SerializeField] private Color candleColor = new Color(0.66f, 0.38f, 1f);
    [SerializeField] private Light2D DoorLight;
    [Tooltip("Length of the environment fade before releasing the player.")]
    [SerializeField, Min(0f)] private float ritualLightFadeDuration = 2f;

    [Header("Candle Settings")]
    [SerializeField, Min(0f)] private float candleFlickerSpeed = 2f;
    [SerializeField, Range(0f, 0.25f)] private float candleFlickerAmount = 0.08f;
    [SerializeField, Min(0f)] private float candleExtinguishInterval = 0.15f;

    [Header("Post Processing")]
    [SerializeField] private float ritualVignetteIntensity = 0.6f;
    [SerializeField] private bool centerVignetteOnPlayer = true;
    [SerializeField] private float vignetteFadeDuration = 1.5f;

    private const float DAY_VIGNETTE_INTENSITY = 0.0f;
    private const float NIGHT_VIGNETTE_INTENSITY = 0.1f;
    private const float RITUAL_LIGHT_DIM = 0.05f;
    private const float EXIT_CUE_FADE_DURATION = 0.5f;

    [Header("References")]
    private DayNightLightController lightController;
    private WorldTransitionAnimator worldTransition;
    private Camera mainCamera;
    private Vignette vignetteComponent;
    private GameObject player;

    private bool ritualActive = false;
    private bool candlesLitAfterRitual = false;
    private bool interiorLightsDimmed = false;
    private bool exitCueReleased;
    private float[] candleBaseIntensities;
    private float doorLightOriginalIntensity;
    private Coroutine activeDoorLightCoroutine;
    private bool isSubscribed = false;

    public float EndFadeDuration => Mathf.Max(0f, ritualLightFadeDuration);

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
        ResetRitualLights();
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
            doorLightOriginalIntensity = DoorLight.intensity > 0.1f ? DoorLight.intensity : 1.5f;
    }

    private void InitializeComponents()
    {
        SetupVignette();
        HideExitCue();
        if (candleLights == null) return;

        candleBaseIntensities = new float[candleLights.Length];
        for (int i = 0; i < candleLights.Length; i++)
            if (candleLights[i] != null) candleBaseIntensities[i] = candleLights[i].intensity;

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
        if (newPhase != GamePhase.Night)
        {
            exitCueReleased = false;
            HideExitCue();
        }

        if (newPhase == GamePhase.Day)
        {
            TurnOffAllCandles(playSound: true);
            RestoreInteriorLighting(vignetteFadeDuration);
        }
    }

    private void HandleWorldStateChanged(WorldState newWorldState)
    {
        exitCueReleased = false;
        HideExitCue();

        if (candlesLitAfterRitual)
        {
            StartCoroutine(ExtinguishCandlesGradually());
            candlesLitAfterRitual = false;
        }

        RestoreInteriorLighting(vignetteFadeDuration);
    }

    public void BeginRitual(float ritualDuration)
    {
        ResetRitualLights();
        ritualActive = true;

        StartCoroutine(LightCandlesSequentially(ritualDuration));
        StartCoroutine(ApplyRitualVignette());
        lightController?.DimLightForRitual(RITUAL_LIGHT_DIM, vignetteFadeDuration);

        if (centerVignetteOnPlayer)
            StartCoroutine(UpdateVignetteContinuously());

        if (worldTransition != null && worldTransition.IsInInterior)
        {
            worldTransition.SetInteriorLightIntensity(0.1f, vignetteFadeDuration);
            interiorLightsDimmed = true;
        }
    }

    public void StartEndRitualEffects()
    {
        ritualActive = false;
        if (IsVignetteAvailable())
            vignetteComponent.center.value = new Vector2(0.5f, 0.5f);

        RestoreGlobalLighting(EndFadeDuration);
    }

    public void OnPlayerReleased(bool completedNightRitual)
    {
        exitCueReleased = completedNightRitual && worldTransition != null && worldTransition.IsInInterior &&
            GameFlowController.Instance?.WorldPhase == GamePhase.Night;
        candlesLitAfterRitual = exitCueReleased;
        if (!exitCueReleased)
        {
            TurnOffAllCandles(playSound: true);
            RestoreInteriorLighting(vignetteFadeDuration);
        }

        UpdateExitCue();
    }

    private void LateUpdate()
    {
        // A tutorial step can grant door permission after the player has been released.
        if (exitCueReleased) UpdateExitCue();
    }

    public void ForceStopAndRestore()
    {
        ResetRitualLights();
        RestoreVignetteImmediate();
        RestoreGlobalLighting(0.5f);
        RestoreInteriorLighting(0.5f);
    }

    private void ResetRitualLights()
    {
        StopAllCoroutines();
        activeDoorLightCoroutine = null;
        ritualActive = false;
        exitCueReleased = false;
        HideExitCue();
        TurnOffAllCandles();
    }

    private void RestoreGlobalLighting(float duration)
    {
        GamePhase phase = GameFlowController.Instance?.WorldPhase ?? GamePhase.Day;
        lightController?.RestoreLightAfterRitual(phase, duration);
    }

    private void RestoreInteriorLighting(float duration)
    {
        if (!interiorLightsDimmed) return;

        worldTransition?.RestoreInteriorLightIntensity(duration);
        interiorLightsDimmed = false;
    }

    private IEnumerator LightCandlesSequentially(float ritualDuration)
    {
        if (candleBaseIntensities == null) yield break;

        int count = 0;
        foreach (Light2D candle in candleLights)
            if (candle != null) count++;
        if (count == 0) yield break;

        // Each candle gets one share of the meditation, with the first lit on entry.
        float started = Time.time;
        float interval = Mathf.Max(0f, ritualDuration) / count;
        int lit = 0;
        for (int i = 0; i < candleLights.Length; i++)
        {
            Light2D candle = candleLights[i];
            if (candle == null) continue;

            while (ritualActive && Time.time < started + lit * interval) yield return null;
            if (!ritualActive) yield break;

            candle.color = candleColor;
            candle.gameObject.SetActive(true);
            SoundManager.Instance?.Play("CandleOn", SoundSourceType.Localized, candle.transform);
            StartCoroutine(FlickerCandle(candle, i));
            lit++;
        }
    }

    private IEnumerator FlickerCandle(Light2D candle, int index)
    {
        float started = Time.time;
        while (candle != null && candle.gameObject.activeSelf)
        {
            float phase = (Time.time - started) * candleFlickerSpeed + index * 0.5f;
            float variation = Mathf.Sin(phase) * candleFlickerAmount;
            candle.intensity = candleBaseIntensities[index] * (1f + variation);
            yield return null;
        }
    }

    private IEnumerator ExtinguishCandlesGradually()
    {
        if (candleBaseIntensities == null) yield break;

        yield return new WaitForSeconds(1f);
        bool playSound = true;
        for (int i = candleLights.Length - 1; i >= 0; i--)
        {
            if (!TurnOffCandle(i, playSound)) continue;
            playSound = false;
            yield return new WaitForSeconds(candleExtinguishInterval);
        }
    }

    private void TurnOffAllCandles(bool playSound = false)
    {
        candlesLitAfterRitual = false;
        if (candleBaseIntensities == null) return;

        for (int i = 0; i < candleLights.Length; i++)
            if (TurnOffCandle(i, playSound)) playSound = false;
    }

    private bool TurnOffCandle(int index, bool playSound)
    {
        Light2D candle = candleLights[index];
        if (candle == null || !candle.gameObject.activeSelf) return false;

        if (playSound)
            SoundManager.Instance?.Play("CandleOff", SoundSourceType.Localized, transform);

        candle.gameObject.SetActive(false);
        candle.intensity = candleBaseIntensities[index];
        return true;
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

        while (ritualActive && elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            vignetteComponent.intensity.value = Mathf.Lerp(from, to, t);
            yield return null;
        }

        if (ritualActive) vignetteComponent.intensity.value = to;
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

    private void UpdateExitCue()
    {
        if (DoorLight == null) return;

        // Match HouseDoor's tutorial and interior transition gates.
        var tutorial = TutorialManager.Instance;
        bool canShow = isActiveAndEnabled && exitCueReleased && worldTransition != null &&
            worldTransition.IsInInterior && !worldTransition.IsTransitioning &&
            GameFlowController.Instance?.CurrentPhase == GamePhase.Night &&
            TutorialManager.Allows(TutorialAction.HouseDoor) &&
            (tutorial == null || tutorial.UsesActionLock || !tutorial.IsPlayerGated());

        if (!canShow)
        {
            if (DoorLight.gameObject.activeSelf) HideExitCue();
        }
        else if (!DoorLight.gameObject.activeSelf)
        {
            DoorLight.gameObject.SetActive(true);
            activeDoorLightCoroutine = StartCoroutine(FadeInExitCue());
        }
    }

    private void HideExitCue()
    {
        if (activeDoorLightCoroutine != null)
        {
            StopCoroutine(activeDoorLightCoroutine);
            activeDoorLightCoroutine = null;
        }

        if (DoorLight == null) return;

        DoorLight.intensity = 0f;
        DoorLight.gameObject.SetActive(false);
    }

    private IEnumerator FadeInExitCue()
    {
        float elapsed = 0f;
        while (elapsed < EXIT_CUE_FADE_DURATION)
        {
            elapsed += Time.deltaTime;
            DoorLight.intensity = Mathf.SmoothStep(0f, doorLightOriginalIntensity, elapsed / EXIT_CUE_FADE_DURATION);
            yield return null;
        }

        DoorLight.intensity = doorLightOriginalIntensity;
        activeDoorLightCoroutine = null;
    }
}
