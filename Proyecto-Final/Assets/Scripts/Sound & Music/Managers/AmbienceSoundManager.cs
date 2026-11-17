using System.Collections.Generic;
using UnityEngine;
using System.Linq;

// Plays the ambience bed for the current weather. The weather controller picks the weather each day; this
// manager picks the day or night version of it from the game phase and crossfades between beds.
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
        // Find all AmbienceWeatherAudioSource components in children if not assigned
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

    // Only day and night change the bed; ritual, level-up and game over keep whatever is playing.
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

    /// <summary>Called when the weather changes. The night version is used automatically if it is already night.</summary>
    public void TransitionAmbience(AmbienceType target, AmbienceWeather weather) {
        currentWeather = weather;
        hasWeather = true;
        bool night = GameFlowController.Instance != null && GameFlowController.Instance.WorldPhase == GamePhase.Night;
        StartAmbience(night ? AmbienceType.Infernum : target, weather);
    }
}
