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
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    /// <summary>
    /// Se dispara cada vez que se carga una nueva escena.
    /// </summary>
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        PlayMusicAccordingToSceneOrState();
    }

    private void Update()
    {
        if (LevelManager.Instance == null)
            return;
        GamePhase currentPhase = GameFlowController.Instance.CurrentPhase;
        string sceneName = SceneManager.GetActiveScene().name;

        if (sceneName == "RefactorMenu")
            return;

        if (sceneName == "SampleScene" || sceneName == "GameScene" || sceneName == "TreeScene")
        {
            if (IsDaylikePhase(currentPhase))
            {
                if (lastOfficialGameState == GamePhase.Day)
                    return;

                lastOfficialGameState = GamePhase.Day;
                PlayMusic(dayMusic);
            }
            else if (currentPhase == GamePhase.Night)
            {
                if (lastOfficialGameState == GamePhase.Night)
                    return;

                lastOfficialGameState = GamePhase.Night;
                PlayMusic(nightMusic);
            }
        }
    }

    /// <summary>
    /// Reproduce la música correcta al cargar una escena o al iniciar el juego.
    /// </summary>
    private void PlayMusicAccordingToSceneOrState()
    {
        string sceneName = SceneManager.GetActiveScene().name;

        if (sceneName == "Menu")
        {
            lastOfficialGameState = GamePhase.None;
            PlayMusic(menuMusic);
            return;
        }

        if (sceneName == "SampleScene" || sceneName == "Game" || sceneName == "TreeScene")
        {
            if (LevelManager.Instance != null)
            {
                GamePhase current = GameFlowController.Instance.CurrentPhase;

                if (IsDaylikePhase(current))
                {
                    lastOfficialGameState = GamePhase.Day;
                    PlayMusic(dayMusic);
                }
                else if (current == GamePhase.Night)
                {
                    lastOfficialGameState = GamePhase.Night;
                    PlayMusic(nightMusic);
                }
                else
                {
                    lastOfficialGameState = GamePhase.Day;
                    PlayMusic(dayMusic);
                }
            }
            else
            {
                lastOfficialGameState = GamePhase.Day;
                PlayMusic(dayMusic);
            }
        }

    }

    /// <summary>
    /// Método auxiliar que devuelve true si el estado dado debe contarse como “día” a efectos de música.
    /// </summary>
    private bool IsDaylikePhase(GamePhase phase)
    {
        return phase == GamePhase.Day;
    }

    /// <summary>
    /// Asigna el clip al AudioSource y lo reproduce. Si ya era el mismo clip, no hace nada.
    /// </summary>
    private void PlayMusic(AudioClip clip)
    {
        if (clip == null)
        {
            return;
        }

        if (audioSource.clip == clip && audioSource.isPlaying)
        {
            return;
        }

        audioSource.clip = clip;
        audioSource.Play();
    }
}

