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
    private bool _muted;

    public float GetMasterVolume() => _masterVolume;
    public float GetMusicVolume() => _musicVolume;
    public float GetSFXVolume() => _sfxVolume;
    public bool IsMuted() => _muted;

    private void Awake()
    {
        Load();
    }

    private void Start()
    {
        ApplyMasterVolume();
        SetMixerVolume(_musicVolumeParam, _musicVolume);
        SetMixerVolume(_sfxVolumeParam, _sfxVolume);
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
        SetMixerVolume(_musicVolumeParam, value);
    }

    public void SetSFXVolume(float value)
    {
        _sfxVolume = value;
        PlayerPrefs.SetFloat(_sfxVolumeParam, value);
        SetMixerVolume(_sfxVolumeParam, value);
    }

    // Mute silences the master bus only; the saved slider values stay untouched so unmuting restores them.
    public void SetMuted(bool muted)
    {
        _muted = muted;
        PlayerPrefs.SetInt(MutedKey, muted ? 1 : 0);
        ApplyMasterVolume();
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
        SetMasterVolume(_masterVolume);
        SetMusicVolume(_musicVolume);
        SetSFXVolume(_sfxVolume);
    }
}
