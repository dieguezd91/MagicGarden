
using UnityEngine;
using UnityEngine.SceneManagement;

public class MusicManager : MonoBehaviour
{
    public static MusicManager Instance;

    [Header("Clips de Audio")]
    [Tooltip("Música que se reproducirá en el menú")]
    public AudioClip menuMusic;

    [Tooltip("Música de fondo durante el día en la escena de juego")]
    public AudioClip dayMusic;

    [Tooltip("Música de fondo durante la noche en la escena de juego")]
    public AudioClip nightMusic;

    [SerializeField] private AudioSource audioSource;

    private GamePhase lastOfficialGameState = GamePhase.None;

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

        audioSource.loop = true;
        audioSource.playOnAwake = false;
        audioSource.volume = 0.4f;
        audioSource.mute = false;

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void Start()
    {
        PlayMusicAccordingToSceneOrState();
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
        lastOfficialGameState = GamePhase.None;
        PlayMusicAccordingToSceneOrState();
    }

    private void Update()
    {
        string sceneName = SceneManager.GetActiveScene().name;

        if (!IsGameplayScene(sceneName))
            return;

        if (GameFlowController.Instance == null)
            return;

        GamePhase currentPhase = GameFlowController.Instance.CurrentPhase;

        if (currentPhase == GamePhase.Day)
        {
            ChangeMusic(GamePhase.Day, dayMusic);
        }
        else if (currentPhase == GamePhase.Night)
        {
            ChangeMusic(GamePhase.Night, nightMusic);
        }
    }

    private void PlayMusicAccordingToSceneOrState()
    {
        string sceneName = SceneManager.GetActiveScene().name;

        if (IsMenuScene(sceneName))
        {
            ChangeMusic(GamePhase.None, menuMusic);
            return;
        }

        if (!IsGameplayScene(sceneName))
            return;

        if (GameFlowController.Instance == null)
        {
            ChangeMusic(GamePhase.Day, dayMusic);
            return;
        }

        GamePhase currentPhase = GameFlowController.Instance.CurrentPhase;

        if (currentPhase == GamePhase.Night)
        {
            ChangeMusic(GamePhase.Night, nightMusic);
        }
        else
        {
            ChangeMusic(GamePhase.Day, dayMusic);
        }
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

    private void ChangeMusic(GamePhase state, AudioClip clip)
    {
        if (clip == null)
        {
            Debug.LogWarning($"[MusicManager] No hay un AudioClip asignado para el estado {state}.");
            return;
        }

        if (audioSource.clip != clip || !audioSource.isPlaying)
        {
            audioSource.clip = clip;
            audioSource.Play();

            Debug.Log($"[MusicManager] Cambiando música a: {state} ({clip.name})");
        }

        lastOfficialGameState = state;
    }
}
