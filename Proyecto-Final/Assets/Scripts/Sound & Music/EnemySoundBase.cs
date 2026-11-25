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

    private const float StepAudibleDistance = 18f;
    private const float MinSecondsBetweenSteps = 0.08f;
    private static float lastStepTime = float.MinValue;
    private static Camera cachedCamera;

    private static bool StepBudgetAllows(Transform at)
    {
        if (cachedCamera == null) cachedCamera = Camera.main;
        if (cachedCamera != null && Vector2.Distance(cachedCamera.transform.position, at.position) > StepAudibleDistance)
            return false;
        if (Time.time - lastStepTime < MinSecondsBetweenSteps && Time.time >= lastStepTime)
            return false;
        lastStepTime = Time.time;
        return true;
    }

    public SoundClipData GetSound(EnemySoundType soundType)
    {
        if (soundDictionary == null) Initialize();

        if (soundDictionary.TryGetValue(soundType, out SoundClipData soundData))
        {
            return soundData;
        }

        return null;
    }

    public void PlaySound(EnemySoundType soundType, SoundSourceType sourceType = SoundSourceType.Global, Transform parent = null, bool skipStepBudget = false)
    {
        var soundData = GetSound(soundType);
        if (soundData == null)
        {
            return;
        }
        if (!soundData.CanPlay())
        {
            return;
        }

        if (SoundManager.Instance == null)
        {
            return;
        }

        if ((soundType == EnemySoundType.Steps || soundType == EnemySoundType.Idle) && SoundManager.Instance.IsCrowded)
            return;

        if (soundType == EnemySoundType.Steps && !skipStepBudget && !StepBudgetAllows(parent == null ? transform : parent))
            return;

        SoundManager.Instance.PlayClip(soundData, sourceType, parent == null ? this.transform : parent);
        soundData.SetLastPlayTime();
    }
}
