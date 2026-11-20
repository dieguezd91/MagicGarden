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
        // If the UI is hidden mid-typing the typewriter coroutine dies without finishing,
        // so make sure nothing keeps playing.
        Debug.Log($"[TypeSFX] t={Time.unscaledTime:F3} f={Time.frameCount}  OnDisable -> StopKeypressSounds");
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
        
        var source = SoundManager.Instance.PlayClip(soundData, SoundSourceType.Global, mainCamera?.transform);
        Debug.Log($"[TypeSFX] t={Time.unscaledTime:F3} f={Time.frameCount}  PlayKeypressSound clip={(source != null ? source.clip?.name : "null (skipped by SoundManager)")} len={(source != null && source.clip != null ? source.clip.length : 0f):F3}s pitch={(source != null ? source.pitch : 0f):F2} source={(source != null ? source.name : "-")}");
        lastPlayedIndex = KeypressSounds.IndexOf(soundData);
        isPlaying = true;
    }
    
    public void StopKeypressSounds()
    {
        if (!isPlaying)
        {
            Debug.Log($"[TypeSFX] t={Time.unscaledTime:F3} f={Time.frameCount}  StopKeypressSounds SKIPPED (isPlaying=false)");
            return;
        }
        isPlaying = false;
        Debug.Log($"[TypeSFX] t={Time.unscaledTime:F3} f={Time.frameCount}  StopKeypressSounds STOPPING");

        if (SoundManager.Instance == null || KeypressSounds == null) return;

        // Stop every pooled source holding any keypress clip, so the tail of the last
        // keypress is cut off as soon as the text finishes.
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
