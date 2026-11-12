using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance;
    public Sound[] sounds;

    [SerializeField] private List<AudioSource> audioSourcePool = new();
    [SerializeField] private int initialPoolSize = 5;
    [SerializeField] private GameObject audioSourcePrefab; // Prefab with AudioSource, AudioMixer, etc.

    private const int MaxSimultaneousSameSound = 3;

    [Header("Localized (positioned) sounds")]
    [Tooltip("3D share of positioned sounds. The rest (1 - blend) is heard at any distance.")]
    [SerializeField, Range(0f, 1f)] private float localizedSpatialBlend = 0.85f;
    [Tooltip("Full volume within this many units of the camera centre.")]
    [SerializeField] private float fullVolumeDistance = 4f;
    [Tooltip("The 3D part is silent beyond this (~ just past the screen edge).")]
    [SerializeField] private float silentDistance = 18f;

    private AudioListener listener;

    // Dictionary to track sounds by name for quick lookup
    private Dictionary<string, Sound> soundLookup = new();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        // Create lookup dictionary for faster sound finding
        foreach (var sound in sounds)
        {
            soundLookup[sound.name] = sound;
        }

        // First, find any existing AudioSources on this GameObject
        AudioSource[] existingAudioSources = GetComponents<AudioSource>();
        audioSourcePool.AddRange(existingAudioSources);

        // If we don't have enough AudioSources, create more to reach initialPoolSize
        int sourcesToCreate = Mathf.Max(0, initialPoolSize - audioSourcePool.Count);
        for (int i = 0; i < sourcesToCreate; i++)
        {
            CreateNewAudioSource();
        }

        // Play sounds that should play on awake
        foreach (var sound in sounds)
        {
            if (sound.playOnAwake)
            {
                Play(sound.name);
            }
        }
    }

    private AudioSource CreateNewAudioSource()
    {
        AudioSource audioSource = null;
        if (audioSourcePrefab != null)
        {
            GameObject audioObj = Instantiate(audioSourcePrefab, this.transform);
            audioObj.name = $"PooledAudioSource_{audioSourcePool.Count}";
            audioSource = audioObj.GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = audioObj.AddComponent<AudioSource>();
            }
        }
        else
        {
            GameObject audioObj = new GameObject($"PooledAudioSource_{audioSourcePool.Count}");
            audioObj.transform.parent = this.transform;
            audioSource = audioObj.AddComponent<AudioSource>();
        }
        audioSource.playOnAwake = false;
        audioSourcePool.Add(audioSource);
        return audioSource;
    }

    private AudioSource GetAvailableAudioSource()
    {
        // Try to find an available audio source (not playing)
        var availableSource = audioSourcePool.FirstOrDefault(source => !source.isPlaying);

        // If no available source, create a new one
        if (availableSource == null)
        {
            availableSource = CreateNewAudioSource();
        }
        return availableSource;
    }

    private void ApplyFalloff(AudioSource source)
    {
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = fullVolumeDistance;
        source.maxDistance = silentDistance;
        source.dopplerLevel = 0f;
    }

    /// <summary>
    /// The camera/listener sits in front of the 2D play plane (z -10). Put sources at the listener's depth
    /// so distance is the on-screen distance; otherwise every sound starts 10 units away.
    /// </summary>
    private Vector3 OnListenerPlane(Vector3 position)
    {
        if (listener == null || !listener.isActiveAndEnabled)
            listener = FindFirstObjectByType<AudioListener>();
        if (listener != null) position.z = listener.transform.position.z;
        return position;
    }

    private void ConfigureAudioSource(AudioSource source, Sound sound, float spatialBlend = 0f)
    {
        source.clip = sound.clip;
        source.volume = sound.volume;
        source.pitch = sound.pitch;
        source.mute = sound.mute;
        source.loop = sound.loop;
        source.spatialBlend = spatialBlend;
        ApplyFalloff(source);
    }

    public void PlayOneShot(string name)
    {
        if (!soundLookup.TryGetValue(name, out Sound sound))
        {
            return;
        }
        var playingSources = audioSourcePool.Where(s => s.isPlaying && s.clip == sound.clip).ToList();
        if (playingSources.Count >= MaxSimultaneousSameSound)
        {
            var sourceToRestart = playingSources[0];
            ConfigureAudioSource(sourceToRestart, sound, 0f);
            sourceToRestart.Stop();
            sourceToRestart.PlayOneShot(sound.clip);
            LogPlay(name, sourceToRestart, sound.volume);
            return;
        }
        var audioSource = GetAvailableAudioSource();
        ConfigureAudioSource(audioSource, sound, 0f);
        audioSource.PlayOneShot(sound.clip);
        LogPlay(name, audioSource, sound.volume);
    }

    public void Play(string name, SoundSourceType sourceType = SoundSourceType.Global, Transform parent = null)
    {
        if (!soundLookup.TryGetValue(name, out Sound sound))
        {
            return;
        }
        var playingSources = audioSourcePool.Where(s => s.isPlaying && s.clip == sound.clip).ToList();
        float spatialBlend = (sourceType == SoundSourceType.Global) ? 0f : localizedSpatialBlend;
        if (playingSources.Count >= MaxSimultaneousSameSound)
        {
            var sourceToRestart = playingSources[0];
            ConfigureAudioSource(sourceToRestart, sound, spatialBlend);
            sourceToRestart.Stop();
            if (sourceType == SoundSourceType.Global)
            {
                sourceToRestart.transform.position = Camera.main.transform.position;
            }
            else if (parent != null)
            {
                sourceToRestart.transform.position = OnListenerPlane(parent.position);
            }
            sourceToRestart.Play();
            LogPlay(name, sourceToRestart, sound.volume);
            return;
        }
        var audioSource = GetAvailableAudioSource();
        ConfigureAudioSource(audioSource, sound, spatialBlend);
        if (sourceType == SoundSourceType.Global)
        {
            audioSource.transform.position = Vector3.zero;
        }
        else if (parent != null)
        {
            audioSource.transform.position = OnListenerPlane(parent.position);
        }
        audioSource.Play();
        LogPlay(name, audioSource, sound.volume);
    }

    public void PlayLoop(string name)
    {
        if (!soundLookup.TryGetValue(name, out Sound sound))
        {
            return;
        }
        var playingSources = audioSourcePool.Where(s => s.isPlaying && s.clip == sound.clip && s.loop).ToList();
        if (playingSources.Count >= MaxSimultaneousSameSound)
        {
            var sourceToRestart = playingSources[0];
            ConfigureAudioSource(sourceToRestart, sound, 0f);
            sourceToRestart.Stop();
            sourceToRestart.loop = true;
            sourceToRestart.Play();
            LogPlay(name, sourceToRestart, sound.volume);
            return;
        }
        var existingSource = audioSourcePool.FirstOrDefault(source =>
            source.isPlaying && source.clip == sound.clip && source.loop);
        if (existingSource != null)
        {
            return;
        }
        var audioSource = GetAvailableAudioSource();
        ConfigureAudioSource(audioSource, sound, 0f);
        audioSource.loop = true;
        audioSource.Play();
        LogPlay(name, audioSource, sound.volume);
    }

    public void Stop(string name)
    {
        if (!soundLookup.TryGetValue(name, out Sound sound))
        {
            return;
        }

        // Stop all audio sources playing this sound
        foreach (var source in audioSourcePool)
        {
            if (source.isPlaying && source.clip == sound.clip)
            {
                source.Stop();
            }
        }
    }

    public void StopAll()
    {
        foreach (var source in audioSourcePool)
        {
            if (source.isPlaying)
                source.Stop();
        }
    }

    public void PauseAll()
    {
        foreach (var source in audioSourcePool)
        {
            if (source.isPlaying)
                source.Pause();
        }
    }

    public void ResumeAll()
    {
        foreach (var source in audioSourcePool)
        {
            if (source.clip != null && !source.isPlaying)
                source.UnPause();
        }
    }
    
    public bool IsPlaying(string name)
    {
        if (!soundLookup.TryGetValue(name, out Sound sound))
            return false;

        return audioSourcePool.Any(source => source.isPlaying && source.clip == sound.clip);
    }

    public int GetActiveSourceCount()
    {
        return audioSourcePool.Count(source => source.isPlaying);
    }

    public int GetTotalSourceCount()
    {
        return audioSourcePool.Count;
    }

    /// <summary>
    /// Plays a SoundClipData using the pooling and limiting system.
    /// </summary>
    public AudioSource PlayClip(SoundClipData data, SoundSourceType sourceType = SoundSourceType.Global, Transform parent = null)
    {
        if (data == null) return null;
        var clip = data.GetClip();
        if (clip == null) return null;
        var playingSources = audioSourcePool.Where(s => s.isPlaying && s.clip == clip).ToList();
        float spatialBlend = (sourceType == SoundSourceType.Global) ? 0f : localizedSpatialBlend;
        if (playingSources.Count >= MaxSimultaneousSameSound)
        {
            var sourceToRestart = playingSources[0];
            sourceToRestart.Stop();
            sourceToRestart.clip = clip;
            sourceToRestart.volume = data.volume;
            sourceToRestart.pitch = data.GetPitch();
            sourceToRestart.loop = data.loop;
            sourceToRestart.mute = false;
            sourceToRestart.spatialBlend = spatialBlend;
            ApplyFalloff(sourceToRestart);
            if (sourceType == SoundSourceType.Global)
            {
                sourceToRestart.transform.position = Vector3.zero;
            }
            else if (parent != null)
            {
                sourceToRestart.transform.position = OnListenerPlane(parent.position);
            }
            if (data.loop)
                sourceToRestart.Play();
            else
                sourceToRestart.PlayOneShot(clip);
            LogPlay(clip.name, sourceToRestart, data.volume);
            return sourceToRestart;
        }
        var audioSource = GetAvailableAudioSource();
        audioSource.clip = clip;
        audioSource.volume = data.volume;
        audioSource.pitch = data.GetPitch();
        audioSource.loop = data.loop;
        audioSource.mute = false;
        audioSource.spatialBlend = spatialBlend;
        ApplyFalloff(audioSource);
        if (sourceType == SoundSourceType.Global)
        {
            audioSource.transform.position = Vector3.zero;
        }
        else if (parent != null)
        {
            audioSource.transform.position = OnListenerPlane(parent.position);
        }
        if (data.loop)
            audioSource.Play();
        else
            audioSource.PlayOneShot(clip);
        LogPlay(clip.name, audioSource, data.volume);
        return audioSource;
    }
    
    /// <summary>
    /// Plays an GetAudioClip directly using the pooling and limiting system.
    /// </summary>
    public void PlayAudioClip(AudioClip clip, float volume = 0.5f, float pitch = 1f, bool loop = false)
    {
        if (clip == null) return;
        var playingSources = audioSourcePool.Where(s => s.isPlaying && s.clip == clip).ToList();
        if (playingSources.Count >= MaxSimultaneousSameSound)
        {
            var sourceToRestart = playingSources[0];
            sourceToRestart.Stop();
            sourceToRestart.clip = clip;
            sourceToRestart.volume = volume;
            sourceToRestart.pitch = pitch;
            sourceToRestart.loop = loop;
            sourceToRestart.mute = false;
            sourceToRestart.spatialBlend = 0f;
            if (loop)
                sourceToRestart.Play();
            else
                sourceToRestart.PlayOneShot(clip);
            LogPlay(clip.name, sourceToRestart, volume);
            return;
        }
        var audioSource = GetAvailableAudioSource();
        audioSource.clip = clip;
        audioSource.volume = volume;
        audioSource.pitch = pitch;
        audioSource.loop = loop;
        audioSource.mute = false;
        audioSource.spatialBlend = 0f;
        if (loop)
            audioSource.Play();
        else
            audioSource.PlayOneShot(clip);
        LogPlay(clip.name, audioSource, volume);
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    private static void LogPlay(string sound, AudioSource source, float effectiveVolume)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        AudioSessionLogger.LogPlay(sound, source, effectiveVolume);
#endif
    }

    #if UNITY_EDITOR
    [ContextMenu("Regenerate Audio Source Pool")]
    private void RegenerateAudioSourcePool()
    {
        ClearAudioSourcePool();
        // Add any AudioSources on this GameObject (should be none after clear, but just in case)
        foreach (var src in GetComponents<AudioSource>())
        {
            if (!audioSourcePool.Contains(src))
                audioSourcePool.Add(src);
        }

        // Create new sources to reach initialPoolSize
        int sourcesToCreate = Mathf.Max(0, initialPoolSize - audioSourcePool.Count);
        for (int i = 0; i < sourcesToCreate; i++)
        {
            CreateNewAudioSource();
        }
    }

    [ContextMenu("Clear Audio Source Pool")]
    private void ClearAudioSourcePool()
    {
        // Remove and destroy all pooled AudioSources except those on this GameObject
        audioSourcePool.RemoveAll(source =>
        {
            if (source == null) return true;
            if (source.gameObject != this.gameObject)
            {
                DestroyImmediate(source.gameObject);
                return true;
            }
            return false;
        });
        // Remove AudioSources from this GameObject except this component
        foreach (var src in GetComponents<AudioSource>())
        {
            if (src != null && src != this.GetComponent<AudioSource>())
                DestroyImmediate(src);
        }
        audioSourcePool.Clear();
    }
    #endif
}