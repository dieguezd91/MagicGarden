
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

    [Header("Per-group Mute Buttons")]
    [Tooltip("Square button placed to the right of each slider at runtime. Its child named \"Icon\" shows the speaker.")]
    [SerializeField] private Button groupMuteButtonPrefab;
    [SerializeField] private Sprite soundOnIcon;
    [SerializeField] private Sprite soundOffIcon;
    [Tooltip("Gap between a slider and its mute button, as a fraction of the button size.")]
    [SerializeField, Range(0f, 1f)] private float groupMuteGap = 0.35f;
    private Button masterMuteButton, musicMuteButton, sfxMuteButton;

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

        var settings = GameManager.Instance.SettingsManager;
        masterMuteButton = CreateGroupMuteButton(volumeSlider, ToggleMute);
        musicMuteButton = CreateGroupMuteButton(musicSlider, () => { settings.SetMusicMuted(!settings.IsMusicMuted()); ApplyMuteVisuals(); });
        sfxMuteButton = CreateGroupMuteButton(sfxSlider, () => { settings.SetSFXMuted(!settings.IsSFXMuted()); ApplyMuteVisuals(); });

        ApplyMuteVisuals();
    }

    // A square button to the right of the slider, as tall as the slider bar; kept out of any layout group.
    private Button CreateGroupMuteButton(Slider slider, UnityEngine.Events.UnityAction onClick)
    {
        if (groupMuteButtonPrefab == null || slider == null) return null;
        var sliderRect = (RectTransform)slider.transform;
        var parent = (RectTransform)sliderRect.parent;
        Button button = Instantiate(groupMuteButtonPrefab, parent, false);
        button.name = slider.name + " Mute";
        var layout = button.GetComponent<LayoutElement>();
        if (layout == null) layout = button.gameObject.AddComponent<LayoutElement>();
        layout.ignoreLayout = true;

        var rect = (RectTransform)button.transform;
        float size = sliderRect.rect.height * sliderRect.lossyScale.y / Mathf.Max(0.0001f, parent.lossyScale.y);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.sizeDelta = new Vector2(size, size);
        // Slide the slider left by half the gap + button so the slider and its button stay centered as one row.
        sliderRect.anchoredPosition -= new Vector2(size * (1f + groupMuteGap) * 0.5f, 0f);
        Vector3 rightEdge = sliderRect.TransformPoint(new Vector3(sliderRect.rect.xMax, sliderRect.rect.center.y, 0f));
        Vector3 local = parent.InverseTransformPoint(rightEdge);
        rect.anchoredPosition = new Vector2(local.x - parent.rect.center.x + size * groupMuteGap, local.y - parent.rect.center.y);
        rect.SetSiblingIndex(sliderRect.GetSiblingIndex() + 1);
        button.onClick.AddListener(onClick);
        return button;
    }

    private void SetMuteIcon(Button button, bool muted)
    {
        if (button == null) return;
        Transform icon = button.transform.Find("Icon");
        Image image = icon != null ? icon.GetComponent<Image>() : null;
        Sprite sprite = muted ? soundOffIcon : soundOnIcon;
        if (image != null && sprite != null) image.sprite = sprite;
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
        var settings = GameManager.Instance.SettingsManager;
        SetMuteIcon(masterMuteButton, muted);
        SetMuteIcon(musicMuteButton, settings.IsMusicMuted());
        SetMuteIcon(sfxMuteButton, settings.IsSFXMuted());
        FadeSlider(volumeSlider, muted);
        FadeSlider(musicSlider, muted || settings.IsMusicMuted());
        FadeSlider(sfxSlider, muted || settings.IsSFXMuted());
    }

    private void FadeSlider(Slider slider, bool muted)
    {
        if (slider == null) return;
        slider.interactable = !muted;
        CanvasGroup group = slider.GetComponent<CanvasGroup>();
        if (group == null) group = slider.gameObject.AddComponent<CanvasGroup>();
        group.alpha = muted ? mutedSliderAlpha : 1f;
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
