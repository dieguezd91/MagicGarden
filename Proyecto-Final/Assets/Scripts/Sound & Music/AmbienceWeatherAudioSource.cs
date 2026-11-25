using System.Collections;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class AmbienceWeatherAudioSource : MonoBehaviour
{
    [Header("Ambience Settings")]
    public AmbienceType AmbienceType;
    public AmbienceWeather AmbienceWeather;

    [SerializeField] private AudioSource _audioSource;

    private float authoredVolume = -1f;
    private Coroutine fade;

    private void Reset()
    {
        _audioSource = GetComponent<AudioSource>();
    }

    private AudioSource Source
    {
        get
        {
            if (_audioSource == null) _audioSource = GetComponent<AudioSource>();
            if (authoredVolume < 0f) authoredVolume = _audioSource.volume;
            return _audioSource;
        }
    }

    public void Play()
    {
        StopFade();
        Source.volume = authoredVolume;
        Source.Play();
    }

    public void StopClip()
    {
        StopFade();
        if (Source.isPlaying) Source.Stop();
        Source.volume = authoredVolume;
    }

    public void FadeIn(float seconds)
    {
        AudioSource source = Source;
        StopFade();
        if (!source.isPlaying)
        {
            source.volume = 0f;
            source.Play();
        }
        fade = StartCoroutine(FadeTo(authoredVolume, seconds, stopAtEnd: false));
    }

    public void FadeOut(float seconds)
    {
        AudioSource source = Source;
        StopFade();
        if (!source.isPlaying) return;
        if (!isActiveAndEnabled) { StopClip(); return; }
        fade = StartCoroutine(FadeTo(0f, seconds, stopAtEnd: true));
    }

    private IEnumerator FadeTo(float target, float seconds, bool stopAtEnd)
    {
        float from = _audioSource.volume;
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            _audioSource.volume = Mathf.Lerp(from, target, Mathf.SmoothStep(0f, 1f, t / seconds));
            yield return null;
        }
        _audioSource.volume = target;
        if (stopAtEnd)
        {
            _audioSource.Stop();
            _audioSource.volume = authoredVolume;
        }
        fade = null;
    }

    private void StopFade()
    {
        if (fade != null) StopCoroutine(fade);
        fade = null;
    }

    public bool IsPlaying => _audioSource != null && _audioSource.isPlaying;
    public AudioClip GetAudioClip => _audioSource.clip;
}
