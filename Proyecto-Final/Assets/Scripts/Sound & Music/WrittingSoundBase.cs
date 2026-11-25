using System.Collections.Generic;
using UnityEngine;

public class WrittingSoundBase : MonoBehaviour
{
    [Header("Keypress Sounds")]
    public List<SoundClipData> KeypressSounds;

    private int lastPlayedIndex = -1;
    private Camera mainCamera;
    private bool isPlaying;

    public bool IsPlaying => isPlaying;

    private void Awake()
    {
        mainCamera = Camera.main;
    }

    private void OnDisable()
    {
        StopKeypressSounds();
    }

    public void PlayKeypressSound()
    {
        if (KeypressSounds == null || KeypressSounds.Count == 0) return;

        var clipCount = KeypressSounds.Count;
        var tempList = KeypressSounds;

        if (clipCount > 1 && lastPlayedIndex >= 0 && lastPlayedIndex < clipCount)
        {
            tempList = new List<SoundClipData>(KeypressSounds);
            tempList.RemoveAt(lastPlayedIndex);
        }

        var index = Random.Range(0, tempList.Count);
        var soundData = tempList[index];

        if (soundData == null || soundData.GetClip() == null) return;

        if (SoundManager.Instance == null) return;

        SoundManager.Instance.PlayClip(soundData, SoundSourceType.Global, mainCamera?.transform);
        lastPlayedIndex = KeypressSounds.IndexOf(soundData);
        isPlaying = true;
    }

    public void StopKeypressSounds()
    {
        if (!isPlaying)
        {
            return;
        }
        isPlaying = false;

        if (SoundManager.Instance == null || KeypressSounds == null) return;

        foreach (var soundData in KeypressSounds)
        {
            if (soundData?.clips == null) continue;
            foreach (var clip in soundData.clips)
            {
                SoundManager.Instance.StopClip(clip);
            }
        }
    }
}
