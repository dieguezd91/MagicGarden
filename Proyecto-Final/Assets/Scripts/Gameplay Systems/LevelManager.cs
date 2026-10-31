
using System.Collections.Generic;
using UnityEngine;

public enum ElementEnum
{
    Ice,
    Wind,
    Electric,
    Fire,
    Stellar,
    Lunar
}

public class LevelManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private EnemiesSpawner waveSpawner;
    [SerializeField] private List<SpawnPointAnimator> spawnpoints = new List<SpawnPointAnimator>();
    [SerializeField] private AmbienceSoundManager ambienceSoundManager;
    [SerializeField] private PlayerExperienceSystem playerExperienceSystem;
    [SerializeField] private NightToDayTransitionController nightToDayTransition;

    public AmbienceSoundManager AmbienceSoundManager => ambienceSoundManager;

    public static LevelManager Instance { get; private set; }

    private bool forceEndingNight;
    private bool bossDayStartedDuringForceEnd;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        if (GameFlowController.Instance != null)
            GameFlowController.Instance.OnPhaseChanged += HandlePhaseChanged;

        if (waveSpawner == null)
            waveSpawner = FindObjectOfType<EnemiesSpawner>();

       
        if (waveSpawner != null)
        {
            waveSpawner.onHordeEnd.RemoveListener(HandleHordeCompleted);
            waveSpawner.onHordeEnd.AddListener(HandleHordeCompleted);
        }

        if (playerExperienceSystem == null)
        {
            var player = GameObject.FindGameObjectWithTag("Player");

            if (player != null)
                playerExperienceSystem = player.GetComponent<PlayerExperienceSystem>();
        }

        if (nightToDayTransition == null)
        {
            nightToDayTransition = FindObjectOfType<NightToDayTransitionController>();
        }

        DayCycleController.Instance?.StartDay();
    }

    private void Update()
    {
        if (UpgradeRuntime.GameplayBlocked)
            return;

        if (Input.GetKeyDown(KeyCode.K))
            ForceEndNight();

        if (Input.GetKeyDown(KeyCode.L))
            InventoryManager.Instance?.AddGold(100);
    }

    private void HandleHordeCompleted()
    {
        RewardsSystem.Instance?.EvaluateAndGrantReward();

        if (playerExperienceSystem == null)
        {
            var player = GameObject.FindGameObjectWithTag("Player");

            if (player != null)
            {
                playerExperienceSystem = player.GetComponent<PlayerExperienceSystem>();
            }
        }

        playerExperienceSystem?.GrantNightCompletionExperience();

        if (nightToDayTransition != null)
        {
            nightToDayTransition.StartTransition();
        }
        else
        {
            DayCycleController.Instance?.StartDay();
        }
    }

    private void OnDestroy()
    {
        if (GameFlowController.Instance != null)
            GameFlowController.Instance.OnPhaseChanged -= HandlePhaseChanged;

        if (waveSpawner != null)
            waveSpawner.onHordeEnd.RemoveListener(HandleHordeCompleted);

        if (Instance == this)
            Instance = null;
    }

    private void HandlePhaseChanged(GamePhase newPhase)
    {
        bool isNight = newPhase == GamePhase.Night;

        foreach (var spawnpoint in spawnpoints)
        {
            if (spawnpoint != null)
                spawnpoint.SetNightMode(isNight);
        }

        if (isNight)
            RewardsSystem.Instance?.StartNightEvaluation();
    }

    public void ForceEndNight()
    {
        foreach (var altar in FindObjectsOfType<RitualAltar>())
        {
            altar.ForceStopRitual();
        }

        var spawner = waveSpawner != null
            ? waveSpawner
            : FindObjectOfType<EnemiesSpawner>();

        if (spawner != null)
        {
            spawner.EndNight();
            return;
        }

        DayCycleController.Instance?.StartDay();
    }
}
