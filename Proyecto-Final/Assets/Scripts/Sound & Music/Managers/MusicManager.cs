using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Plays one intro + loop pair per game state. The loop is scheduled on the audio clock to start on the
// exact sample the intro ends, so the join is gapless. Changing state fades the new music in while every
// track that is still audible keeps fading out from wherever it was, so a quick second change never cuts one off.
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

    [SerializeField] private AudioSource audioSource; // template for routing (mixer group)
    [SerializeField] private float musicVolume = 0.4f;
    [SerializeField] private float crossfadeSeconds = 2.5f;

    private class Deck
    {
        public AudioSource intro;
        public AudioSource loop;
        public float gain;   // 0..1 position in the fade, not the volume itself
        public float target; // 1 = fading in / playing, 0 = fading out
        public bool Playing => intro.clip != null || loop.clip != null;
        public void SetVolume(float v) { intro.volume = v; loop.volume = v; }

        public void Stop()
        {
            intro.Stop();
            loop.Stop();
            intro.clip = null;
            loop.clip = null;
            gain = 0f;
            target = 0f;
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

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void Start()
    {
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
        ResetNightTracking();
        UpdateMusic();
    }

    private void Update()
    {
        UpdateMusic();
        UpdateFades();
    }

    private void UpdateMusic()
    {
        MusicState next = DesiredState();
        if (next != MusicState.None && next != state)
            Play(next);
    }

    private MusicState DesiredState()
    {
        string sceneName = SceneManager.GetActiveScene().name;

        if (IsMenuScene(sceneName))
            return MusicState.Menu;

        if (!IsGameplayScene(sceneName))
            return MusicState.None;

        // Both game-over exits set the phase back to Day just before they load a scene. Without this the day
        // music started for a second and was replaced at once; stay quiet until the new scene decides.
        if (gameOverLatch)
            return MusicState.Silence;

        if (GameFlowController.Instance == null)
            return MusicState.None;

        GamePhase phase = GameFlowController.Instance.CurrentPhase;
        if (phase != GamePhase.Night)
            ResetNightTracking();

        switch (phase)
        {
            case GamePhase.Day:
                return MusicState.Day;
            case GamePhase.Night:
                return NightState();
            case GamePhase.GameOver:
                gameOverLatch = true; // leave room for the game-over sting
                return MusicState.Silence;
            default:
                return MusicState.None; // ritual, level-up: keep what is playing
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

        // The boss is gone: let its death and the victory sting ring out instead of restarting the calm night music.
        if (bossSeenThisNight)
            return MusicState.Silence;

        // Boss night, boss still to come: go straight to its music. Starting the calm track first meant two
        // changes a couple of seconds apart. Decided once per night, so a random boss pick cannot flip it.
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

        if (current != null) current.target = 0f; // keeps fading out from its present level
        current = null;

        if (next == MusicState.Silence)
            return;

        MusicTrack track = PickVariation(TracksFor(next));
        if (track == null || track.loop == null)
        {
            Debug.LogWarning($"[MusicManager] No music assigned for {next}.");
            return;
        }
        lastTrack = track;

        current = FreeDeck();
        current.intro.clip = track.intro;
        current.loop.clip = track.loop;
        current.gain = 0f;
        current.target = 1f;
        current.SetVolume(0f);

        double start = AudioSettings.dspTime + 0.1; // small lead so both sources are ready
        if (track.intro != null)
        {
            current.intro.PlayScheduled(start);
            start += (double)track.intro.samples / track.intro.frequency;
        }
        current.loop.PlayScheduled(start);
    }

    private void UpdateFades()
    {
        // unscaled: pause and level-up set timeScale to 0
        float step = crossfadeSeconds > 0f ? Time.unscaledDeltaTime / crossfadeSeconds : 1f;
        foreach (Deck deck in decks)
        {
            if (!deck.Playing) continue;
            deck.gain = Mathf.MoveTowards(deck.gain, deck.target, step);
            if (deck.gain <= 0f && deck.target <= 0f)
            {
                deck.Stop();
                continue;
            }
            // Sine curve: equal power across the overlap (no dip in the middle) and no sudden start or end.
            deck.SetVolume(musicVolume * Mathf.Sin(deck.gain * Mathf.PI * 0.5f));
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
        s.priority = 0; // music must never be culled by the voice limit
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
