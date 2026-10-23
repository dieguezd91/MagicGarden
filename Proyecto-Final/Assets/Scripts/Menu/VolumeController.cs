
using UnityEngine;
using UnityEngine.UI;

public class VolumeController : MonoBehaviour
{
    [Header("Sliders")]
    [SerializeField] private Slider volumeSlider;
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider sfxSlider;

    [Header("Mute Button")]
    [SerializeField] private Button muteButton;

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
    }

    public void SetMasterVolume(float volume)
    {
        _savedMasterVolume = volume;
        GameManager.Instance.SettingsManager.SetMasterVolume(volume);
    }

    public void SetMusicVolume(float volume)
    {
        _savedMusicVolume = volume;
        GameManager.Instance.SettingsManager.SetMusicVolume(volume);
    }

    public void SetSFXVolume(float volume)
    {
        _savedSFXVolume = volume;
        GameManager.Instance.SettingsManager.SetSFXVolume(volume);
    }

    private void ToggleMute()
    {
        bool hasAnyVolume =
            _savedMasterVolume > 0f ||
            _savedMusicVolume > 0f ||
            _savedSFXVolume > 0f;

        if (hasAnyVolume)
        {
            if (_savedMasterVolume > 0f)
            {
                SetMasterVolume(0f);

                if (volumeSlider != null)
                    volumeSlider.SetValueWithoutNotify(0f);
            }

            if (_savedMusicVolume > 0f)
            {
                SetMusicVolume(0f);

                if (musicSlider != null)
                    musicSlider.SetValueWithoutNotify(0f);
            }

            if (_savedSFXVolume > 0f)
            {
                SetSFXVolume(0f);

                if (sfxSlider != null)
                    sfxSlider.SetValueWithoutNotify(0f);
            }
        }
        else
        {
            SetMasterVolume(1f);
            SetMusicVolume(1f);
            SetSFXVolume(1f);

            if (volumeSlider != null)
                volumeSlider.SetValueWithoutNotify(1f);

            if (musicSlider != null)
                musicSlider.SetValueWithoutNotify(1f);

            if (sfxSlider != null)
                sfxSlider.SetValueWithoutNotify(1f);
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
