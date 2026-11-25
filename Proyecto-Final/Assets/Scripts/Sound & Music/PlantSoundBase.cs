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
    [Tooltip("Played instead of Hurt for fire and ice hits.")]
    public SoundClipData hurtBurn;
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
    [Tooltip("Seconds the loop takes to fade in when it starts (or comes back while fading out).")]
    public float loopFadeIn = 0.6f;
    [Tooltip("Seconds the loop takes to fade out after it is asked to stop.")]
    public float loopFadeOut = 1.5f;

    private AudioSource loopSource;
    private float loopGain;
    private float loopTarget;

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
            { PlantSoundType.Hit, hit },
            { PlantSoundType.HurtBurn, hurtBurn }
        };
    }

    public bool HasClip(PlantSoundType soundType)
    {
        var data = GetSound(soundType);
        return data != null && data.clips != null && data.clips.Length > 0;
    }

    public SoundClipData GetSound(PlantSoundType soundType)
    {
        if (soundDictionary == null) Initialize();
        soundDictionary.TryGetValue(soundType, out SoundClipData soundData);
        return soundData;
    }

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
        loopTarget = 1f;
        if (IsOwnLoop(loopSource) && loopSource.isPlaying) return;
        if (SoundManager.Instance == null || loop == null) return;
        loopSource = SoundManager.Instance.PlayClip(loop, SoundSourceType.Localized, transform);
        if (loopSource != null)
        {
            loopSource.spatialBlend = 1f;
            loopSource.rolloffMode = AudioRolloffMode.Linear;
            loopSource.minDistance = loopFullVolumeDistance;
            loopSource.maxDistance = loopSilentDistance;
            loopGain = 0f;
            loopSource.volume = 0f;
        }
    }

    public void StopLoop()
    {
        loopTarget = 0f;
    }

    // Pause, game over and the level-up screen stop time (or block gameplay) but not audio, so loops are muted
    // while that lasts. Muted rather than paused: the SoundManager pool reuses sources that are not playing.
    public static bool GameFrozen => Time.timeScale <= 0f || UpgradeRuntime.GameplayBlocked;

    private void Update()
    {
        if (loopSource == null) return;
        if (!IsOwnLoop(loopSource))
        {
            loopSource = null;
            loopGain = 0f;
            return;
        }

        if (GameFrozen)
        {
            loopSource.volume = 0f;
            return;
        }

        float seconds = loopTarget > loopGain ? loopFadeIn : loopFadeOut;
        loopGain = seconds > 0f ? Mathf.MoveTowards(loopGain, loopTarget, Time.deltaTime / seconds) : loopTarget;
        loopSource.volume = loop.volume * loopGain;

        if (loopGain <= 0f && loopTarget <= 0f)
        {
            loopSource.Stop();
            loopSource = null;
        }
    }

    private bool IsOwnLoop(AudioSource source) =>
        source != null && source.loop && loop?.clips != null && System.Array.IndexOf(loop.clips, source.clip) >= 0;

    private void OnDisable()
    {
        if (IsOwnLoop(loopSource)) loopSource.Stop();
        loopSource = null;
        loopGain = 0f;
        loopTarget = 0f;
    }
}
