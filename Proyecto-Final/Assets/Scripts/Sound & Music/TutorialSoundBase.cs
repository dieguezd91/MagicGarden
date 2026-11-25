using System.Collections.Generic;
using UnityEngine;

public enum TutorialSoundType
{
    ShowPanel,
    HidePanel,
    StepComplete
}

public class TutorialSoundBase : MonoBehaviour
{
    [Header("Tutorial Sounds")]
    public SoundClipData showPanel;
    public SoundClipData hidePanel;
    public SoundClipData stepComplete;

    private Dictionary<TutorialSoundType, SoundClipData> soundDictionary;

    private void Awake()
    {
        InitializeDictionary();
    }

    private void InitializeDictionary()
    {
        if (soundDictionary != null) return;

        soundDictionary = new Dictionary<TutorialSoundType, SoundClipData>
        {
            { TutorialSoundType.ShowPanel, showPanel },
            { TutorialSoundType.HidePanel, hidePanel },
            { TutorialSoundType.StepComplete, stepComplete }
        };
    }

    public void PlaySound(TutorialSoundType type, Transform parent = null)
    {
        if (soundDictionary == null)
        {
            InitializeDictionary();
        }

        if (!soundDictionary.TryGetValue(type, out var soundData) || soundData == null || soundData.GetClip() == null)
        {
            return;
        }

        if (SoundManager.Instance == null)
        {
            return;
        }

        SoundManager.Instance.PlayClip(soundData, SoundSourceType.Global, Camera.main?.transform);
    }
}
