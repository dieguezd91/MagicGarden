
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Audio;
using TMPro;

public class VolumeController : MonoBehaviour
{
    [Header("Sliders")]
    [SerializeField] private Slider volumeSlider;
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider sfxSlider;

    [Header("Mute Button")]
    [SerializeField] private Button muteButton;

    [Header("Slider Tick")]
    // Own source instead of SoundManager: this prefab is also used in the main menu, which has no SoundManager.
    [SerializeField] private AudioClip sliderTickClip;
    [SerializeField] private AudioMixerGroup sliderTickMixerGroup;
    [SerializeField, Range(0f, 1f)] private float sliderTickVolume = 0.35f;
    private AudioSource tickSource;

    [Header("Mute")]
    [SerializeField, Range(0f, 1f)] private float mutedSliderAlpha = 0.4f;
    [SerializeField] private string unmuteLabel = "Unmute All";
    private TMP_Text muteLabel;
    private string muteLabelText;

    private float _savedMasterVolume;
    private float _savedMusicVolume;
    private float _savedSFXVolume;

    private void Awake()
    {
        var settings = GameManager.Instance.SettingsManager;

        _savedMasterVolume = settings.GetMasterVolume();
        _savedMusicVolume = settings.GetMusicVolume();
        _savedSFXVolume = settings.GetSFXVolume();

        Application.quitting += OnApplicationQuit;
    }

    private void Start()
    {
        if (volumeSlider != null)
        {
            volumeSlider.SetValueWithoutNotify(_savedMasterVolume);
            volumeSlider.onValueChanged.AddListener(SetMasterVolume);
        }

        if (musicSlider != null)
        {
            musicSlider.SetValueWithoutNotify(_savedMusicVolume);
            musicSlider.onValueChanged.AddListener(SetMusicVolume);
        }

        if (sfxSlider != null)
        {
            sfxSlider.SetValueWithoutNotify(_savedSFXVolume);
            sfxSlider.onValueChanged.AddListener(SetSFXVolume);
        }

        if (muteButton != null)
            muteButton.onClick.AddListener(ToggleMute);

        ApplyMuteVisuals();
    }

    public void SetMasterVolume(float volume)
    {
        PlaySliderTick();
        _savedMasterVolume = volume;
        GameManager.Instance.SettingsManager.SetMasterVolume(volume);
    }

    public void SetMusicVolume(float volume)
    {
        PlaySliderTick();
        _savedMusicVolume = volume;
        GameManager.Instance.SettingsManager.SetMusicVolume(volume);
    }

    public void SetSFXVolume(float volume)
    {
        PlaySliderTick();
        _savedSFXVolume = volume;
        GameManager.Instance.SettingsManager.SetSFXVolume(volume);
    }

    private float nextSliderTickTime;

    private void PlaySliderTick()
    {
        // onValueChanged fires every frame while dragging.
        if (Time.unscaledTime < nextSliderTickTime) return;
        nextSliderTickTime = Time.unscaledTime + 0.07f;
        if (sliderTickClip == null) return;
        if (tickSource == null)
        {
            tickSource = gameObject.AddComponent<AudioSource>();
            tickSource.playOnAwake = false;
            tickSource.ignoreListenerPause = true; // pause menu may pause the listener
            tickSource.outputAudioMixerGroup = sliderTickMixerGroup;
        }
        tickSource.pitch = 1f + Random.Range(-0.04f, 0.04f);
        tickSource.PlayOneShot(sliderTickClip, sliderTickVolume);
    }

    private void ToggleMute()
    {
        var settings = GameManager.Instance.SettingsManager;
        settings.SetMuted(!settings.IsMuted());
        ApplyMuteVisuals();
    }

    // Sliders keep their values while muted; they are just locked and faded out.
    private void ApplyMuteVisuals()
    {
        bool muted = GameManager.Instance.SettingsManager.IsMuted();
        if (muteButton != null && muteLabel == null)
        {
            muteLabel = muteButton.GetComponentInChildren<TMP_Text>(true);
            if (muteLabel != null) muteLabelText = muteLabel.text; // the prefab's label, e.g. "Mute All"
        }
        if (muteLabel != null)
            muteLabel.text = muted ? unmuteLabel : muteLabelText;
        foreach (Slider slider in new[] { volumeSlider, musicSlider, sfxSlider })
        {
            if (slider == null) continue;
            slider.interactable = !muted;
            CanvasGroup group = slider.GetComponent<CanvasGroup>();
            if (group == null) group = slider.gameObject.AddComponent<CanvasGroup>();
            group.alpha = muted ? mutedSliderAlpha : 1f;
        }
    }

    private void OnApplicationQuit()
    {
        GameManager.Instance.SettingsManager.Save();
    }

    private void OnDestroy()
    {
        if (volumeSlider != null)
            volumeSlider.onValueChanged.RemoveListener(SetMasterVolume);

        if (musicSlider != null)
            musicSlider.onValueChanged.RemoveListener(SetMusicVolume);

        if (sfxSlider != null)
            sfxSlider.onValueChanged.RemoveListener(SetSFXVolume);

        if (muteButton != null)
            muteButton.onClick.RemoveListener(ToggleMute);

        Application.quitting -= OnApplicationQuit;
    }
}
