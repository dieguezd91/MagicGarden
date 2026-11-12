using System.Collections.Generic;
using UnityEngine;

public class PlantSoundBase : MonoBehaviour
{
    [Header("Sound Effects")]
    public SoundClipData planted;
    public SoundClipData removed;
    public SoundClipData harvested;
    public SoundClipData attack;
    public SoundClipData hurt;
    public SoundClipData die;
    public SoundClipData grown;
    [Tooltip("Plant-specific hit (e.g. saw bite, electric hit). Projectile hits use the projectile's impactSound.")]
    public SoundClipData hit;
    [Tooltip("Must have Loop on. Played while the plant is active (e.g. electric hum).")]
    public SoundClipData loop;

    [Tooltip("Loop is fully heard within this many units (a character is ~1.5 units wide).")]
    public float loopFullVolumeDistance = 1.5f;
    [Tooltip("Loop is silent beyond this many units (~2-3 characters away).")]
    public float loopSilentDistance = 4f;

    private AudioSource loopSource;

    // Dictionary for runtime access
    private Dictionary<PlantSoundType, SoundClipData> soundDictionary;

    public void Initialize()
    {
        soundDictionary = new Dictionary<PlantSoundType, SoundClipData>
        {
            { PlantSoundType.Planted, planted },
            { PlantSoundType.Removed, removed },
            { PlantSoundType.Harvested, harvested },
            { PlantSoundType.Attack, attack },
            { PlantSoundType.Hurt, hurt },
            { PlantSoundType.Die, die },
            { PlantSoundType.Grown, grown },
            { PlantSoundType.Hit, hit }
        };
    }

    public SoundClipData GetSound(PlantSoundType soundType)
    {
        if (soundDictionary == null) Initialize();
        soundDictionary.TryGetValue(soundType, out SoundClipData soundData);
        return soundData;
    }

    /// <summary>
    /// Plays the sound for the given PlantSoundType using SoundManager's pooling system.
    /// </summary>
    public void PlaySound(PlantSoundType soundType, SoundSourceType sourceType = SoundSourceType.Global, Transform parent = null)
    {
        var soundData = GetSound(soundType);
        if (soundData == null) return;
        if (!soundData.CanPlay()) return;
        SoundManager.Instance.PlayClip(soundData, sourceType, parent == null ? this.transform : parent);
        soundData.SetLastPlayTime();
    }

    public void PlayLoop()
    {
        // Safe to call every frame: only (re)starts when our source stopped or was taken by another clip.
        if (IsOwnLoop(loopSource) && loopSource.isPlaying) return;
        if (SoundManager.Instance == null || loop == null) return;
        loopSource = SoundManager.Instance.PlayClip(loop, SoundSourceType.Localized, transform);
        if (loopSource != null)
        {
            // Ambient plant loops (electric hum) are a close-up detail: fully positional, short range.
            loopSource.spatialBlend = 1f;
            loopSource.rolloffMode = AudioRolloffMode.Linear;
            loopSource.minDistance = loopFullVolumeDistance;
            loopSource.maxDistance = loopSilentDistance;
        }
    }

    public void StopLoop()
    {
        // The pooled source may already have been reused for another clip.
        if (IsOwnLoop(loopSource)) loopSource.Stop();
        loopSource = null;
    }

    private bool IsOwnLoop(AudioSource source) =>
        source != null && source.loop && loop?.clips != null && System.Array.IndexOf(loop.clips, source.clip) >= 0;

    private void OnDisable() => StopLoop();
}
