#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Dev-only audio session recorder for balancing. Starts automatically in editor play mode; F8 toggles recording.
/// Writes a CSV per session to persistentDataPath/AudioSessions:
///  - "play" rows for every sound SoundManager starts (volume, blend, distance, estimated heard gain)
///  - "snap" rows once per second for every playing AudioSource in the scene (music, ambience, own sources)
/// Analyze with Tools/Audio/analyze_audio_session.py.
/// </summary>
public class AudioSessionLogger : MonoBehaviour
{
    private const KeyCode ToggleKey = KeyCode.F8;
    private const float SnapshotInterval = 1f;

    private static AudioSessionLogger instance;
    private StreamWriter writer;
    private string path;
    private float nextSnapshot;
    private int playCount;
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static bool Recording => instance != null && instance.writer != null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (instance != null) return;
        var go = new GameObject(nameof(AudioSessionLogger));
        DontDestroyOnLoad(go);
        instance = go.AddComponent<AudioSessionLogger>();
#if UNITY_EDITOR
        // Balancing phase: record every editor play session so a forgotten F8 doesn't lose a session.
        instance.StartRecording();
#endif
    }

    private void Update()
    {
        if (Input.GetKeyDown(ToggleKey))
        {
            if (writer == null) StartRecording();
            else StopRecording();
        }

        if (writer != null && Time.unscaledTime >= nextSnapshot)
        {
            nextSnapshot = Time.unscaledTime + SnapshotInterval;
            Snapshot();
        }
    }

    private void StartRecording()
    {
        string dir = Path.Combine(Application.persistentDataPath, "AudioSessions");
        Directory.CreateDirectory(dir);
        path = Path.Combine(dir, $"audio_session_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
        writer = new StreamWriter(path, false, new UTF8Encoding(false));
        writer.WriteLine("type,time,scene,sound,source,volume,pitch,spatialBlend,loop,rolloff,minDist,maxDist,dist2D,dist3D,heardGain,activeVoices,poolSize,listenerX,listenerY,listenerZ,srcX,srcY,srcZ,mixerGroup");
        playCount = 0;
        Debug.Log($"[AudioSessionLogger] Recording to {path}");
    }

    private void StopRecording()
    {
        writer.Flush();
        writer.Dispose();
        writer = null;
        Debug.Log($"[AudioSessionLogger] Saved {playCount} sound events to {path}");
    }

    private void OnApplicationQuit()
    {
        if (writer != null) StopRecording();
    }

    /// <summary>Called by SoundManager right after it starts a sound.</summary>
    public static void LogPlay(string sound, AudioSource source, float volume)
    {
        if (!Recording || source == null) return;
        instance.playCount++;
        instance.WriteRow("play", sound, source, volume);
    }

    private void Snapshot()
    {
        foreach (var src in FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
        {
            if (!src.isPlaying) continue;
            WriteRow("snap", src.clip != null ? src.clip.name : "(none)", src, src.volume);
        }
        writer.Flush();
    }

    private void WriteRow(string type, string sound, AudioSource src, float volume)
    {
        var listener = FindListener();
        Vector3 lp = listener != null ? listener.transform.position : Vector3.zero;
        Vector3 sp = src.transform.position;
        float d3 = Vector3.Distance(lp, sp);
        float d2 = Vector2.Distance(lp, sp);
        float heard = volume * ((1f - src.spatialBlend) + src.spatialBlend * Attenuation(src, d3));
        var sb = new StringBuilder(256);
        void Add(string v) { if (sb.Length > 0) sb.Append(','); sb.Append(v.Contains(",") ? "\"" + v + "\"" : v); }
        void F(float v) => Add(v.ToString("0.###", Inv));
        Add(type); F(Time.unscaledTime); Add(SceneManager.GetActiveScene().name); Add(sound); Add(src.gameObject.name);
        F(volume); F(src.pitch); F(src.spatialBlend); Add(src.loop ? "1" : "0"); Add(src.rolloffMode.ToString());
        F(src.minDistance); F(src.maxDistance); F(d2); F(d3); F(heard);
        F(SoundManager.Instance != null ? SoundManager.Instance.GetActiveSourceCount() : -1);
        F(SoundManager.Instance != null ? SoundManager.Instance.GetTotalSourceCount() : -1);
        F(lp.x); F(lp.y); F(lp.z); F(sp.x); F(sp.y); F(sp.z);
        Add(src.outputAudioMixerGroup != null ? src.outputAudioMixerGroup.name : "(none)");
        writer.WriteLine(sb.ToString());
    }

    private AudioListener cachedListener;
    private AudioListener FindListener()
    {
        if (cachedListener == null || !cachedListener.isActiveAndEnabled)
            cachedListener = FindFirstObjectByType<AudioListener>();
        return cachedListener;
    }

    /// <summary>Distance attenuation of the source's 3D part (Unity's built-in curves; custom curves read from the source).</summary>
    private static float Attenuation(AudioSource s, float d)
    {
        float min = Mathf.Max(s.minDistance, 0.0001f), max = Mathf.Max(s.maxDistance, min);
        switch (s.rolloffMode)
        {
            case AudioRolloffMode.Logarithmic:
                return d <= min ? 1f : Mathf.Clamp01(min / d);
            case AudioRolloffMode.Linear:
                return Mathf.Clamp01(1f - (d - min) / Mathf.Max(max - min, 0.0001f));
            default:
                var curve = s.GetCustomCurve(AudioSourceCurveType.CustomRolloff);
                return curve != null ? Mathf.Clamp01(curve.Evaluate(d / max)) : 1f;
        }
    }

    private void OnGUI()
    {
        if (writer == null) return;
        var style = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold };
        style.normal.textColor = Color.red;
        GUI.Label(new Rect(10, Screen.height - 28, 400, 24), $"● REC audio ({playCount} sounds) — F8 to stop", style);
    }
}
#endif
