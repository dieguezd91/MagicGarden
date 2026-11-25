using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

public class MusicManager : MonoBehaviour
{
    [System.Serializable]
    public class MusicTrack
    {
        public AudioClip intro;
        public AudioClip loop;
    }

    private enum MusicState { None, Menu, Day, Night, Boss, SniperBoss, Silence }

    public static MusicManager Instance;

    [Header("Variations per state (one is picked at random)")]
    [SerializeField] private MusicTrack[] menuMusic;
    [SerializeField] private MusicTrack[] dayMusic;
    [SerializeField] private MusicTrack[] nightMusic;
    [SerializeField] private MusicTrack[] bossMusic;
    [SerializeField] private MusicTrack[] sniperBossMusic;

    [SerializeField] private AudioSource audioSource;
    [Tooltip("Mixer group for the main music (child of Music). Falls back to the template's group.")]
    [SerializeField] private AudioMixerGroup mainGroup;
    [Tooltip("Mixer group for the night layer (child of Music), so it can get its own effects.")]
    [SerializeField] private AudioMixerGroup layerGroup;
    [SerializeField] private float musicVolume = 0.4f;
    [SerializeField] private float crossfadeSeconds = 2.5f;

    [Header("Night layer (with night and boss music, beat-locked)")]
    [SerializeField] private MusicTrack[] nightLayers;
    [Tooltip("Main music volume during night and boss states (day and menu always use 1).")]
    [SerializeField, Range(0f, 1f)] private float nightMainVolume = 0.52f;
    [Tooltip("Layer volume relative to the music volume. Above 1 makes it louder than the main track's base level.")]
    [SerializeField, Range(0f, 2f)] private float nightLayerVolume = 1.3f;
    [Tooltip("Seconds the layer takes to fade in when the night music starts. Fading out uses the normal crossfade.")]
    [SerializeField, Min(0f)] private float nightLayerFadeInSeconds = 4f;
    [Tooltip("Tempo of the night music and the layer. Used to keep the layer on the night music's bar lines.")]
    [SerializeField] private float nightBpm = 104f;

    [Header("Scene changes (menu and game)")]
    [Tooltip("Seconds the next scene's music takes to fade in. It starts just before the old music finishes fading out.")]
    [SerializeField, Min(0f)] private float sceneMusicFadeInSeconds = 1.5f;

    [Header("Muffle on pause and game over")]
    [SerializeField] private float muffledCutoff = 700f;
    [SerializeField] private float muffleSeconds = 0.35f;

    private static readonly string[] MuffleParams = { "MusicMainLowpass", "MusicLayerLowpass", "EffectsGameLowpass" };
    private readonly float[] openCutoffs = { 22000f, 8000f, 22000f };

    private class Deck
    {
        public AudioSource intro;
        public AudioSource loop;
        public float gain;
        public float target;
        public float volumeScale = 1f;
        public float fadeInSeconds = -1f;
        public float fadeOutSeconds = -1f;
        public bool Playing => intro.clip != null || loop.clip != null;
        public void SetVolume(float v) { intro.volume = v; loop.volume = v; }

        public double nextWrap;
        public double followPeriod;
        public double layerAnchor;

        public void Stop()
        {
            intro.Stop();
            loop.Stop();
            intro.clip = null;
            loop.clip = null;
            gain = 0f;
            target = 0f;
            volumeScale = 1f;
            fadeInSeconds = -1f;
            fadeOutSeconds = -1f;
            followPeriod = 0d;
            SetVolume(0f);
        }
    }

    private readonly List<Deck> decks = new List<Deck>();
    private Deck current;
    private MusicState state = MusicState.None;
    private MusicTrack lastTrack;
    private BossNightManager bossNight;
    private bool bossSeenThisNight;
    private MusicState upcomingBossState = MusicState.None;
    private bool gameOverLatch;
    private Deck currentLayer;
    private MusicTrack layerTrack;
    private AudioMixer mixer;
    private PauseController pause;
    private float muffle;
    private bool muffleApplied;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.Stop();

        mixer = mainGroup != null ? mainGroup.audioMixer
              : audioSource.outputAudioMixerGroup != null ? audioSource.outputAudioMixerGroup.audioMixer : null;
        if (mixer != null)
        {
            for (int i = 0; i < MuffleParams.Length; i++)
                if (mixer.GetFloat(MuffleParams[i], out float cutoff)) openCutoffs[i] = cutoff;
        }

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void Start()
    {
        pause = FindFirstObjectByType<PauseController>();
        UpdateMusic();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Instance = null;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        bossNight = null;
        gameOverLatch = false;
        earlyState = MusicState.None;
        pause = FindFirstObjectByType<PauseController>();
        ResetNightTracking();
        UpdateMusic();
    }

    private void Update()
    {
        UpdateMusic();
        KeepLayerOnGrid();
        UpdateFades();
        UpdateMuffle();
    }

    private void UpdateMuffle()
    {
        if (mixer == null)
            return;

        bool muffled = gameOverLatch || (pause != null && pause.IsPaused);
        float target = muffled ? 1f : 0f;
        if (!muffleApplied && target <= 0f)
            return;

        muffle = Mathf.MoveTowards(muffle, target, muffleSeconds > 0f ? Time.unscaledDeltaTime / muffleSeconds : 1f);
        if (muffle <= 0f)
        {
            foreach (string param in MuffleParams)
                mixer.ClearFloat(param);
            muffleApplied = false;
            return;
        }

        float k = Mathf.SmoothStep(0f, 1f, muffle);
        for (int i = 0; i < MuffleParams.Length; i++)
            mixer.SetFloat(MuffleParams[i], LogLerp(openCutoffs[i], Mathf.Min(muffledCutoff, openCutoffs[i]), k));
        muffleApplied = true;
    }

    private static float LogLerp(float a, float b, float t)
    {
        return Mathf.Exp(Mathf.Lerp(Mathf.Log(a), Mathf.Log(b), t));
    }

    private void UpdateMusic()
    {
        MusicState next = DesiredState();
        if (earlyState != MusicState.None)
        {
            if (earlyUntilDay && GameFlowController.Instance != null &&
                GameFlowController.Instance.WorldPhase == GamePhase.Day)
                earlyState = MusicState.None;
            else
                next = earlyState;
        }
        if (next != MusicState.None && next != state)
            Play(next);
    }

    private MusicState earlyState = MusicState.None;
    private bool earlyUntilDay;

    public float CrossfadeSeconds => crossfadeSeconds;

    public void BeginDayTransition()
    {
        earlyState = MusicState.Day;
        earlyUntilDay = true;
    }

    public void FadeOutForSceneChange(float seconds)
    {
        earlyState = MusicState.Silence;
        earlyUntilDay = false;
        state = MusicState.Silence;
        current = null;
        currentLayer = null;
        layerTrack = null;
        foreach (Deck deck in decks)
        {
            if (!deck.Playing) continue;
            deck.target = 0f;
            deck.fadeOutSeconds = Mathf.Max(0f, seconds);
        }
    }

    public void StartSceneMusic(string sceneName)
    {
        MusicState target = IsMenuScene(sceneName) ? MusicState.Menu
                          : IsGameplayScene(sceneName) ? MusicState.Day
                          : MusicState.None;
        if (target == MusicState.None) return;
        earlyState = target;
        earlyUntilDay = false;
        Play(target);
        if (current != null) current.fadeInSeconds = sceneMusicFadeInSeconds;
    }

    public void CancelEarlyTransition()
    {
        earlyState = MusicState.None;
    }

    private MusicState DesiredState()
    {
        string sceneName = SceneManager.GetActiveScene().name;

        if (IsMenuScene(sceneName))
            return MusicState.Menu;

        if (!IsGameplayScene(sceneName))
            return MusicState.None;

        if (gameOverLatch)
            return MusicState.None;

        if (GameFlowController.Instance == null)
            return MusicState.None;

        GamePhase phase = GameFlowController.Instance.WorldPhase;
        if (phase == GamePhase.Day)
            ResetNightTracking();

        switch (phase)
        {
            case GamePhase.Day:
                return MusicState.Day;
            case GamePhase.Night:
                return NightState();
            case GamePhase.GameOver:
                gameOverLatch = true;
                return MusicState.None;
            default:
                return MusicState.None;
        }
    }

    private MusicState NightState()
    {
        if (bossNight == null)
            bossNight = FindFirstObjectByType<BossNightManager>();
        if (bossNight == null)
            return MusicState.Night;

        if (bossNight.IsBossActive)
        {
            bossSeenThisNight = true;
            return StateForBoss(bossNight.CurrentBoss);
        }

        if (bossSeenThisNight)
            return MusicState.Silence;

        bool bossDay = DayCycleController.Instance != null && bossNight.IsBossNightDay(DayCycleController.Instance.CurrentDay);
        if (!bossDay)
            return MusicState.Night;
        if (upcomingBossState == MusicState.None)
        {
            GameObject upcoming = bossNight.UpcomingBossPrefab;
            upcomingBossState = upcoming != null ? StateForBoss(upcoming) : MusicState.Night;
        }
        return upcomingBossState;
    }

    private void ResetNightTracking()
    {
        bossSeenThisNight = false;
        upcomingBossState = MusicState.None;
    }

    private static MusicState StateForBoss(GameObject boss)
    {
        return boss != null && boss.GetComponent<SniperBoss>() != null ? MusicState.SniperBoss : MusicState.Boss;
    }

    private void Play(MusicState next)
    {
        state = next;

        if (current != null) current.target = 0f;
        current = null;
        if (currentLayer != null) currentLayer.target = 0f;
        currentLayer = null;

        bool nightFamily = next == MusicState.Night || next == MusicState.Boss || next == MusicState.SniperBoss;
        if (!nightFamily)
            layerTrack = null;

        if (next == MusicState.Silence)
            return;

        MusicTrack track = PickVariation(TracksFor(next));
        if (track == null || track.loop == null)
        {
            return;
        }
        lastTrack = track;

        current = PrepareDeck(track, nightFamily ? nightMainVolume : 1f, mainGroup);

        double start = AudioSettings.dspTime + 0.1;
        double musicStart = start;
        if (track.intro != null)
        {
            current.intro.PlayScheduled(start);
            start += ClipSeconds(track.intro);
        }
        current.loop.PlayScheduled(start);

        if (nightFamily)
            StartLayer(musicStart, start, ClipSeconds(track.loop));
    }

    private Deck PrepareDeck(MusicTrack track, float volumeScale, AudioMixerGroup group)
    {
        Deck deck = FreeDeck();
        AudioMixerGroup output = group != null ? group : audioSource.outputAudioMixerGroup;
        deck.intro.outputAudioMixerGroup = output;
        deck.loop.outputAudioMixerGroup = output;
        deck.intro.clip = track.intro;
        deck.loop.clip = track.loop;
        deck.intro.loop = false;
        deck.loop.loop = true;
        if (deck.intro.clip != null) deck.intro.timeSamples = 0;
        if (deck.loop.clip != null) deck.loop.timeSamples = 0;
        deck.gain = 0f;
        deck.target = 1f;
        deck.volumeScale = volumeScale;
        deck.fadeInSeconds = -1f;
        deck.fadeOutSeconds = -1f;
        deck.SetVolume(0f);
        return deck;
    }

    private void StartLayer(double musicStart, double musicLoopStart, double musicLoopSeconds)
    {
        if (layerTrack == null)
            layerTrack = PickLayer();
        if (layerTrack == null || layerTrack.loop == null)
            return;

        currentLayer = PrepareDeck(layerTrack, nightLayerVolume, layerGroup);
        currentLayer.intro.clip = null;
        currentLayer.fadeInSeconds = nightLayerFadeInSeconds;

        AudioClip loop = layerTrack.loop;
        double offset = Mod(musicStart - musicLoopStart, ClipSeconds(loop));
        currentLayer.loop.timeSamples = Mathf.Clamp((int)(offset * loop.frequency), 0, loop.samples - 1);
        currentLayer.loop.PlayScheduled(musicStart);

        currentLayer.layerAnchor = musicLoopStart;
        currentLayer.followPeriod = musicLoopSeconds;
        currentLayer.nextWrap = musicLoopStart + musicLoopSeconds;
    }

    private MusicTrack PickLayer()
    {
        if (nightLayers == null || nightLayers.Length == 0) return null;
        return nightLayers[Random.Range(0, nightLayers.Length)];
    }

    private void KeepLayerOnGrid()
    {
        Deck layer = currentLayer;
        if (layer == null || layer.followPeriod <= 0d || layer.loop.clip == null || nightBpm <= 0f)
            return;
        if (AudioSettings.dspTime < layer.nextWrap - 1.0)
            return;

        double wrap = layer.nextWrap;
        layer.nextWrap += layer.followPeriod;
        if (AudioSettings.dspTime > wrap - 0.05)
            return;

        AudioClip clip = layer.loop.clip;
        double loopSeconds = ClipSeconds(clip);
        double bar = 240.0 / nightBpm;
        double pos = Mod(wrap - layer.layerAnchor, loopSeconds);
        double onBar = System.Math.Round(pos / bar) * bar;
        if (System.Math.Abs(pos - onBar) < 0.003)
            return;

        double newPos = Mod(onBar, loopSeconds);
        AudioSource spare = layer.intro;
        spare.Stop();
        spare.clip = clip;
        spare.loop = true;
        spare.volume = layer.loop.volume;
        spare.timeSamples = Mathf.Clamp((int)(newPos * clip.frequency), 0, clip.samples - 1);
        spare.PlayScheduled(wrap);
        layer.loop.SetScheduledEndTime(wrap);

        layer.intro = layer.loop;
        layer.loop = spare;
        layer.layerAnchor = wrap - newPos;
    }

    private static double ClipSeconds(AudioClip clip)
    {
        return (double)clip.samples / clip.frequency;
    }

    private static double Mod(double a, double m)
    {
        double r = a % m;
        return r < 0 ? r + m : r;
    }

    private void UpdateFades()
    {
        float step = crossfadeSeconds > 0f ? Time.unscaledDeltaTime / crossfadeSeconds : 1f;
        foreach (Deck deck in decks)
        {
            if (!deck.Playing) continue;
            float deckStep = step;
            if (deck.target > deck.gain && deck.fadeInSeconds >= 0f)
                deckStep = deck.fadeInSeconds > 0f ? Time.unscaledDeltaTime / deck.fadeInSeconds : 1f;
            else if (deck.target < deck.gain && deck.fadeOutSeconds >= 0f)
                deckStep = deck.fadeOutSeconds > 0f ? Time.unscaledDeltaTime / deck.fadeOutSeconds : 1f;
            deck.gain = Mathf.MoveTowards(deck.gain, deck.target, deckStep);
            if (deck.gain <= 0f && deck.target <= 0f)
            {
                deck.Stop();
                continue;
            }
            deck.SetVolume(musicVolume * deck.volumeScale * Mathf.Sin(deck.gain * Mathf.PI * 0.5f));
        }
    }

    private Deck FreeDeck()
    {
        foreach (Deck deck in decks)
            if (!deck.Playing) return deck;
        var created = new Deck { intro = CreateSource(false), loop = CreateSource(true) };
        decks.Add(created);
        return created;
    }

    private MusicTrack[] TracksFor(MusicState s)
    {
        switch (s)
        {
            case MusicState.Menu: return menuMusic;
            case MusicState.Day: return dayMusic;
            case MusicState.Night: return nightMusic;
            case MusicState.Boss: return bossMusic;
            case MusicState.SniperBoss: return sniperBossMusic != null && sniperBossMusic.Length > 0 ? sniperBossMusic : bossMusic;
            default: return null;
        }
    }

    private MusicTrack PickVariation(MusicTrack[] tracks)
    {
        if (tracks == null || tracks.Length == 0) return null;
        if (tracks.Length == 1) return tracks[0];
        MusicTrack pick;
        do pick = tracks[Random.Range(0, tracks.Length)]; while (pick == lastTrack);
        return pick;
    }

    private AudioSource CreateSource(bool loop)
    {
        AudioSource s = gameObject.AddComponent<AudioSource>();
        s.outputAudioMixerGroup = audioSource.outputAudioMixerGroup;
        s.playOnAwake = false;
        s.loop = loop;
        s.volume = 0f;
        s.spatialBlend = 0f;
        s.priority = 0;
        return s;
    }

    private bool IsMenuScene(string sceneName)
    {
        return sceneName == "Menu" || sceneName == "RefactorMenu";
    }

    private bool IsGameplayScene(string sceneName)
    {
        return sceneName == "SampleScene" ||
               sceneName == "Game" ||
               sceneName == "GameScene" ||
               sceneName == "TreeScene";
    }
}
