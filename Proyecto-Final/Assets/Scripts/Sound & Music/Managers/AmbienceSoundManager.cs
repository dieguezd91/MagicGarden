using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class AmbienceSoundManager : MonoBehaviour
{
    [Header("Weather Audio Sources")]
    [SerializeField] private List<AmbienceWeatherAudioSource> _weatherAudioSources = new();
    [SerializeField] private float crossfadeSeconds = 2.5f;

    private AmbienceWeatherAudioSource currentPlayingSource;
    private AmbienceWeather currentWeather = AmbienceWeather.Clear;
    private bool hasWeather;
    private GameFlowController flow;

    private void Awake() {
        if (_weatherAudioSources == null || _weatherAudioSources.Count == 0)
            _weatherAudioSources = GetComponentsInChildren<AmbienceWeatherAudioSource>().ToList();
    }

    private void Start() {
        flow = GameFlowController.Instance;
        if (flow != null) flow.OnPhaseChanged += HandlePhaseChanged;
    }

    private void OnDestroy() {
        if (flow != null) flow.OnPhaseChanged -= HandlePhaseChanged;
    }

    private void HandlePhaseChanged(GamePhase phase) {
        if (!hasWeather) return;
        if (phase == GamePhase.Day) StartAmbience(AmbienceType.Forest, currentWeather);
        else if (phase == GamePhase.Night) StartAmbience(AmbienceType.Infernum, currentWeather);
    }

    private AmbienceWeatherAudioSource GetWeatherSource(AmbienceType type, AmbienceWeather weather) {
        return _weatherAudioSources.FirstOrDefault(s => s.AmbienceType == type && s.AmbienceWeather == weather);
    }

    private void StartAmbience(AmbienceType type, AmbienceWeather weather) {
        var source = GetWeatherSource(type, weather);
        if (source == null) {
            return;
        }

        if (source == currentPlayingSource) return;

        if (currentPlayingSource != null)
            currentPlayingSource.FadeOut(crossfadeSeconds);

        source.FadeIn(crossfadeSeconds);
        currentPlayingSource = source;
    }

    public void TransitionAmbience(AmbienceType target, AmbienceWeather weather) {
        currentWeather = weather;
        hasWeather = true;
        bool night = GameFlowController.Instance != null && GameFlowController.Instance.WorldPhase == GamePhase.Night;
        StartAmbience(night ? AmbienceType.Infernum : target, weather);
    }
}
