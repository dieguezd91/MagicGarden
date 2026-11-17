using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// Plays one intro + loop pair per game state. The loop is scheduled on the audio clock to start on the
// exact sample the intro ends, so the join is gapless. Changing state crossfades whatever is playing
// into the intro of a randomly chosen variation of the new state's music.
public class MusicManager : MonoBehaviour
{
    [System.Serializable]
    public class MusicTrack
    {
        public AudioClip intro;
        public AudioClip loop;
    }

    private enum MusicState { None, Menu, Day, Night, Boss, SniperBoss }

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
        public void SetVolume(float v) { intro.volume = v; loop.volume = v; }
        public void Stop() { intro.Stop(); loop.Stop(); intro.clip = null; loop.clip = null; }
    }

    private Deck current, fading;
    private Coroutine crossfade;
    private MusicState state = MusicState.None;
    private MusicTrack lastTrack;
    private BossNightManager bossNight;

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

        current = CreateDeck();
        fading = CreateDeck();

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
        UpdateMusic();
    }

    private void Update()
    {
        UpdateMusic();
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

        if (GameFlowController.Instance == null)
            return MusicState.Day;

        switch (GameFlowController.Instance.CurrentPhase)
        {
            case GamePhase.Day:
                return MusicState.Day;
            case GamePhase.Night:
                if (bossNight == null)
                    bossNight = FindFirstObjectByType<BossNightManager>();
                if (bossNight != null && bossNight.IsBossActive)
                    return bossNight.CurrentBoss.GetComponent<SniperBoss>() != null ? MusicState.SniperBoss : MusicState.Boss;
                return MusicState.Night;
            default:
                return MusicState.None; // ritual, level-up, game over: keep what is playing
        }
    }

    private void Play(MusicState next)
    {
        MusicTrack track = PickVariation(TracksFor(next));
        state = next;
        if (track == null || track.loop == null)
        {
            Debug.LogWarning($"[MusicManager] No music assigned for {next}.");
            return;
        }
        lastTrack = track;

        (current, fading) = (fading, current);
        current.Stop();
        current.intro.clip = track.intro;
        current.loop.clip = track.loop;
        current.SetVolume(0f);

        double start = AudioSettings.dspTime + 0.1; // small lead so both sources are ready
        if (track.intro != null)
        {
            current.intro.PlayScheduled(start);
            start += (double)track.intro.samples / track.intro.frequency;
        }
        current.loop.PlayScheduled(start);

        if (crossfade != null) StopCoroutine(crossfade);
        crossfade = StartCoroutine(Crossfade(fading, current));
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

    private IEnumerator Crossfade(Deck from, Deck to)
    {
        float fromStart = from.intro.volume;

        // unscaled: pause and level-up set timeScale to 0
        for (float t = 0f; t < crossfadeSeconds; t += Time.unscaledDeltaTime)
        {
            float k = t / crossfadeSeconds;
            to.SetVolume(musicVolume * Mathf.Sqrt(k)); // equal-power, no dip in the middle
            from.SetVolume(fromStart * Mathf.Sqrt(1f - k));
            yield return null;
        }

        to.SetVolume(musicVolume);
        from.Stop();
        from.SetVolume(0f);
        crossfade = null;
    }

    private Deck CreateDeck()
    {
        return new Deck { intro = CreateSource(false), loop = CreateSource(true) };
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
