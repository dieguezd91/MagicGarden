using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemySoundBase : MonoBehaviour
{
    [Header("Sound Effects")]
    public SoundClipData steps;
    public SoundClipData spawning;
    public SoundClipData die;
    public SoundClipData hurt;
    public SoundClipData idle;
    public SoundClipData attack;
    [Tooltip("Played where this enemy's hit lands (sword cut, bite, explosion, slam).")]
    public SoundClipData impact;
    
    [Header("Additional Sounds")]
    public SoundClipData special;
    public SoundClipData roar;
    public SoundClipData cast;
    
    // Dictionary for runtime access
    private Dictionary<EnemySoundType, SoundClipData> soundDictionary;
    
    public void Initialize()
    {
        soundDictionary = new Dictionary<EnemySoundType, SoundClipData>
        {
            { EnemySoundType.Steps, steps },
            { EnemySoundType.Spawning, spawning },
            { EnemySoundType.Die, die },
            { EnemySoundType.Hurt, hurt },
            { EnemySoundType.Idle, idle },
            { EnemySoundType.Attack, attack },
            { EnemySoundType.Impact, impact },
            { EnemySoundType.Special, special },
            { EnemySoundType.Cast, cast }
        };
    }
    
    public SoundClipData GetSound(EnemySoundType soundType)
    {
        if (soundDictionary == null) Initialize();

        if (soundDictionary.TryGetValue(soundType, out SoundClipData soundData))
        {
            if (soundData == null)
            {
                Debug.LogWarning($"[{gameObject.name}] EnemySoundBase: SoundClipData for '{soundType}' is null. Assign a clip in the inspector.");
            }

            return soundData;
        }

        Debug.LogWarning($"[{gameObject.name}] EnemySoundBase: No sound entry for '{soundType}' in the sound dictionary.");
        return null;
    }
    
    /// <summary>
    /// Plays the sound for the given EnemySoundType using SoundManager's pooling system.
    /// Adds defensive logging when data or manager is missing.
    /// </summary>
    public void PlaySound(EnemySoundType soundType, SoundSourceType sourceType = SoundSourceType.Global, Transform parent = null)
    {
        var soundData = GetSound(soundType);
        if (soundData == null)
        {
            // GetSound already logged a warning explaining which clip is missing.
            return;
        }
        if (!soundData.CanPlay())
        {
            // Optional: log why it can't play (likely cooldown)
            // Keep this as a verbose/info message to avoid spamming warnings
            Debug.Log($"[{gameObject.name}] EnemySoundBase: Sound '{soundType}' cannot play right now (cooldown or conditions).", this);
            return;
        }

        if (SoundManager.Instance == null)
        {
            Debug.LogWarning($"[{gameObject.name}] EnemySoundBase: SoundManager.Instance is null. Cannot play '{soundType}'.");
            return;
        }

        SoundManager.Instance.PlayClip(soundData, sourceType, parent == null ? this.transform : parent);
        soundData.SetLastPlayTime();
    }
    
    
}
