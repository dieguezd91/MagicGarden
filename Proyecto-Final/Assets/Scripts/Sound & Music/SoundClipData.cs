using UnityEngine;

[System.Serializable]
public class SoundClipData
{
    [Header("Audio Clips")]
    public AudioClip[] clips;

    [Header("Audio Settings")]
    [Range(0f, 1f)]
    public float volume = 1f;

    [Range(0.1f, 3f)]
    public float pitch = 1f;

    [Range(0f, 0.5f)]
    public float pitchVariation = 0.1f;

    [Header("Behavior")]
    public bool loop = false;
    public bool randomizeClip = true;
    public float cooldownTime = 0f;

    private float lastPlayTime = -1f;

    public bool CanPlay()
    {
        return Time.time >= lastPlayTime + cooldownTime;
    }

    public void SetLastPlayTime()
    {
        lastPlayTime = Time.time;
    }

    public AudioClip GetClip()
    {
        if (clips == null || clips.Length == 0) return null;

        if (randomizeClip && clips.Length > 1)
        {
            return clips[Random.Range(0, clips.Length)];
        }

        return clips[0];
    }

    public float GetPitch()
    {
        return pitch + Random.Range(-pitchVariation, pitchVariation);
    }
}