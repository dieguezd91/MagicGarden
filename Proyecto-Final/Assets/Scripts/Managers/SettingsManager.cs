using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

public class SettingsManager : MonoBehaviour
{
    [SerializeField] private AudioMixer _audioMixer;
    [SerializeField] private string _masterVolumeParam = "Master";
    [SerializeField] private string _musicVolumeParam = "Music";
    [SerializeField] private string _sfxVolumeParam = "Effects";
    [SerializeField] private float _masterVolume = 0.5f;
    [SerializeField] private float _musicVolume = 0.5f;
    [SerializeField] private float _sfxVolume = 0.5f;
    private const string MutedKey = "Muted";
    private const string MusicMutedKey = "MusicMuted";
    private const string SfxMutedKey = "EffectsMuted";
    private bool _muted;
    private bool _musicMuted;
    private bool _sfxMuted;

    public float GetMasterVolume() => _masterVolume;
    public float GetMusicVolume() => _musicVolume;
    public float GetSFXVolume() => _sfxVolume;
    public bool IsMuted() => _muted;
    public bool IsMusicMuted() => _musicMuted;
    public bool IsSFXMuted() => _sfxMuted;

    private void Awake()
    {
        Load();
    }

    private void Start()
    {
        ApplyMasterVolume();
        ApplyMusicVolume();
        ApplySFXVolume();
    }

    private void SetMixerVolume(string parameter, float normalizedVolume)
    {
        if (_audioMixer == null)
        {
            return;
        }
        float dB = Mathf.Log10(Mathf.Clamp(normalizedVolume, 0.0001f, 1f)) * 20f;
        _audioMixer.SetFloat(parameter, dB);
    }

    public void SetMasterVolume(float value)
    {
        _masterVolume = value;
        PlayerPrefs.SetFloat(_masterVolumeParam, value);
        ApplyMasterVolume();
    }

    public void SetMusicVolume(float value)
    {
        _musicVolume = value;
        PlayerPrefs.SetFloat(_musicVolumeParam, value);
        ApplyMusicVolume();
    }

    public void SetSFXVolume(float value)
    {
        _sfxVolume = value;
        PlayerPrefs.SetFloat(_sfxVolumeParam, value);
        ApplySFXVolume();
    }

    // Mute silences the master bus only; the saved slider values stay untouched so unmuting restores them.
    public void SetMuted(bool muted)
    {
        _muted = muted;
        PlayerPrefs.SetInt(MutedKey, muted ? 1 : 0);
        ApplyMasterVolume();
    }

    // Music / effects mutes work like the master one: the group is silenced, its slider value is kept for unmuting.
    public void SetMusicMuted(bool muted)
    {
        _musicMuted = muted;
        PlayerPrefs.SetInt(MusicMutedKey, muted ? 1 : 0);
        ApplyMusicVolume();
    }

    public void SetSFXMuted(bool muted)
    {
        _sfxMuted = muted;
        PlayerPrefs.SetInt(SfxMutedKey, muted ? 1 : 0);
        ApplySFXVolume();
    }

    private void ApplyMusicVolume()
    {
        if (_musicMuted && _audioMixer != null) _audioMixer.SetFloat(_musicVolumeParam, -80f);
        else SetMixerVolume(_musicVolumeParam, _musicVolume);
    }

    private void ApplySFXVolume()
    {
        if (_sfxMuted && _audioMixer != null) _audioMixer.SetFloat(_sfxVolumeParam, -80f);
        else SetMixerVolume(_sfxVolumeParam, _sfxVolume);
    }

    private void ApplyMasterVolume()
    {
        if (_muted && _audioMixer != null)
            _audioMixer.SetFloat(_masterVolumeParam, -80f);
        else
            SetMixerVolume(_masterVolumeParam, _masterVolume);
    }

    public void Save() => PlayerPrefs.Save();

    public void Load()
    {
        _masterVolume = PlayerPrefs.GetFloat(_masterVolumeParam, 0.5f);
        _musicVolume = PlayerPrefs.GetFloat(_musicVolumeParam, 0.5f);
        _sfxVolume = PlayerPrefs.GetFloat(_sfxVolumeParam, 0.5f);
        _muted = PlayerPrefs.GetInt(MutedKey, 0) == 1;
        _musicMuted = PlayerPrefs.GetInt(MusicMutedKey, 0) == 1;
        _sfxMuted = PlayerPrefs.GetInt(SfxMutedKey, 0) == 1;
        SetMasterVolume(_masterVolume);
        SetMusicVolume(_musicVolume);
        SetSFXVolume(_sfxVolume);
    }
}
