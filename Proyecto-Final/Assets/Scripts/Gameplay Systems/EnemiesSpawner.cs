using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;


[System.Serializable]
public class EnemyHorde
{
    public string hordeName;
    public List<EnemyWeight> enemies = new List<EnemyWeight>();
}

[System.Serializable]
public class EnemyWeight
{
    public GameObject prefab;
    [Range(0.01f, 100f)]
    public float weight;
}

public class EnemiesSpawner : MonoBehaviour
{
    [SerializeField] private bool useContinuousHordeSystem = true;
    [SerializeField] private int baseEnemiesPerNight;
    [SerializeField] private int enemiesPerNightIncrement;
    [SerializeField] private float difficultyScalingFactor = 1.2f;

    [Header("SETTINGS")]
    [SerializeField] private float baseSpawnInterval;
    [SerializeField] private float minSpawnInterval = 0.3f;
    [SerializeField] private float spawnIntervalDecreasePerDay = 0.1f;

    [Header("SPAWN")]
    public List<Transform> spawnPoints;
    public bool useRandomSpawnPoint = true;
    public bool dontSpawnWhenPlayerNearby = true;
    public float playerCheckRadius = 5f;

    [Header("ENEMY HORDES")]
    [SerializeField] private List<EnemyHorde> enemyHordes = new List<EnemyHorde>();

    [Header("FIRST NIGHT")]
    [SerializeField] private GameObject firstNightSkeletonPrefab;

    private int currentEnemyHorde = 0;

    [Header("BOSS")]
    [SerializeField] private BossNightManager bossNightManager;

    [Header("DAY TRANSITION")]
    [SerializeField] private NightToDayTransitionController nightToDayTransition;

    [Header("EVENTS")]
    public UnityEvent onHordeStart;
    public UnityEvent onHordeEnd;
    public UnityEvent onEnemySpawned;
    public UnityEvent<int, int> onHordeProgress;

    private int totalEnemiesKilled;
    private int totalEnemiesToKill;
    private int totalEnemiesSpawned;
    private bool isSpawning = false;
    private Transform playerTransform;
    private GamePhase lastPhase = GamePhase.None;
    private Coroutine continuousSpawnCoroutine;
    private float currentHordeTime;
    private List<GameObject> activeEnemies = new List<GameObject>();
    private bool hordeCompleted = false;
    private float currentSpawnInterval;
    private bool playerHasLeftHouse = false;
    private DayTimerController dayTimerController;


    private WeightedRoulette<GameObject> enemyRoulette = new WeightedRoulette<GameObject>();

    void Start()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
        }

        if (bossNightManager == null)
        {
            bossNightManager = GetComponent<BossNightManager>();
        }

        GameplayEvents.OnPlayerExitedHouseDuringNight += OnPlayerExit;
        dayTimerController = FindObjectOfType<DayTimerController>();

        ResetHordeCounters();

        if (nightToDayTransition == null)
            nightToDayTransition = FindObjectOfType<NightToDayTransitionController>();
    }

    void Update()
    {
        if (GameFlowController.Instance.CurrentPhase == GamePhase.Night && lastPhase != GamePhase.Night)
        {
            
            if (playerHasLeftHouse || (dayTimerController != null && dayTimerController.NightStartedByTimer))
            {
                if (IsBossNight())
                {
                    StartBossNight();
                }
                else
                {
                    ConfigureEnemyRoulette();
                    StartContinuousHorde();
                }
            }
        }

        lastPhase = GameFlowController.Instance.CurrentPhase;

        if (!IsBossNight())
        {
            if (GameFlowController.Instance.CurrentPhase == GamePhase.Night &&
                totalEnemiesKilled >= totalEnemiesToKill &&
                GetTrulyAliveEnemiesCount() <= 0 &&
                !hordeCompleted)
            {
                hordeCompleted = true;
                StopContinuousHorde(true);
            }
        }
    }

    private void OnPlayerExit()
    {
        playerHasLeftHouse = true;

        if (GameFlowController.Instance.CurrentPhase == GamePhase.Night && !isSpawning)
        {
            if (IsBossNight())
            {
                StartBossNight();
            }
            else
            {
                ConfigureEnemyRoulette();
                StartContinuousHorde();
            }
        }
    }

    private bool IsBossNight()
    {
        if (bossNightManager == null) return false;

        int currentDay = DayCycleController.Instance.CurrentDay;
        return bossNightManager.IsBossNightDay(currentDay);
    }
    public bool IsBossNightActive => IsBossNight();

    private void StartBossNight()
    {
        Debug.Log("Iniciando noche de jefe");

        ResetHordeCounters();
        onHordeStart?.Invoke();

        if (bossNightManager != null)
        {
            bossNightManager.StartBossNight();
        }
    }

    public void StartContinuousHorde()
    {
        if (GameFlowController.Instance.CurrentPhase != GamePhase.Night)
            return;

        if (IsBossNight())
        {
            Debug.Log("Es noche de jefe - Cancelando spawn normal");
            return;
        }

        ResetHordeCounters();

        int totalNights = DayCycleController.Instance.TotalNights;

        totalEnemiesToKill = baseEnemiesPerNight + ((totalNights - 1) * enemiesPerNightIncrement);

        currentSpawnInterval = Mathf.Max(minSpawnInterval, baseSpawnInterval - (totalNights - 1) * spawnIntervalDecreasePerDay);

        ConfigureEnemyRoulette();

        onHordeStart?.Invoke();

        if (continuousSpawnCoroutine != null)
            StopCoroutine(continuousSpawnCoroutine);

        continuousSpawnCoroutine = StartCoroutine(SpawnEnemiesIndividually());
    }

    IEnumerator SpawnEnemiesIndividually()
    {
        isSpawning = true;
        currentHordeTime = 0f;
        hordeCompleted = false;

        yield return new WaitForSeconds(1f);

        while (GameFlowController.Instance.CurrentPhase == GamePhase.Night &&
               totalEnemiesSpawned < totalEnemiesToKill &&
               !IsBossNight())
        {
            currentHordeTime += currentSpawnInterval;

            float adjustedInterval = AdjustSpawnIntervalByTime();

            SpawnEnemy();
            totalEnemiesSpawned++;
            onEnemySpawned?.Invoke();
            onHordeProgress?.Invoke(totalEnemiesKilled, totalEnemiesToKill);

            yield return new WaitForSeconds(adjustedInterval);
        }

        isSpawning = false;
        continuousSpawnCoroutine = null;

        if (GetTrulyAliveEnemiesCount() <= 0 && !hordeCompleted && !IsBossNight())
        {
            hordeCompleted = true;
            StopContinuousHorde(true);
        }
    }

    private float AdjustSpawnIntervalByTime()
    {
        if (currentHordeTime > 60f)
        {
            return currentSpawnInterval * 0.75f;
        }
        else if (currentHordeTime > 30f)
        {
            return currentSpawnInterval * 0.9f;
        }

        return currentSpawnInterval;
    }

    void SpawnEnemy()
    {
        if (spawnPoints.Count == 0 || enemyHordes.Count == 0)
        {
            Debug.Log("No hay puntos de spawn o hordas de enemigos asignadas");
            return;
        }

        if (IsBossNight())
        {
            Debug.Log("Cancelando spawn de enemigo normal - Es noche de jefe");
            return;
        }

        Transform spawnPoint;

        if (useRandomSpawnPoint)
        {
            if (dontSpawnWhenPlayerNearby && playerTransform != null)
            {
                List<Transform> validSpawnPoints = new List<Transform>();

                foreach (Transform point in spawnPoints)
                {
                    if (Vector3.Distance(point.position, playerTransform.position) > playerCheckRadius)
                    {
                        validSpawnPoints.Add(point);
                    }
                }

                spawnPoint = (validSpawnPoints.Count > 0)
                    ? validSpawnPoints[Random.Range(0, validSpawnPoints.Count)]
                    : spawnPoints[Random.Range(0, spawnPoints.Count)];
            }
            else
            {
                spawnPoint = spawnPoints[Random.Range(0, spawnPoints.Count)];
            }
        }
        else
        {
            spawnPoint = spawnPoints[totalEnemiesSpawned % spawnPoints.Count];
        }

        GameObject selectedEnemyPrefab = enemyRoulette.Roll();

        if (selectedEnemyPrefab == null)
        {
            Debug.LogWarning("No hay enemigos disponibles en la horda actual.");
            return;
        }

        GameObject enemy = Instantiate(
            selectedEnemyPrefab,
            spawnPoint.position,
            spawnPoint.rotation
        );

        enemy.transform.SetParent(this.transform);
        activeEnemies.Add(enemy);

        LifeController enemyLife = enemy.GetComponent<LifeController>();

        if (enemyLife != null)
        {
            enemyLife.onDeath.AddListener(() => OnEnemyDeath(enemy));
        }
    }

    public void ResetHordeCounters()
    {
        totalEnemiesKilled = 0;
        totalEnemiesSpawned = 0;
        totalEnemiesToKill = baseEnemiesPerNight;
        hordeCompleted = false;
        currentHordeTime = 0f;

        activeEnemies.Clear();

        if (continuousSpawnCoroutine != null)
        {
            StopCoroutine(continuousSpawnCoroutine);
            continuousSpawnCoroutine = null;
        }

        isSpawning = false;
        playerHasLeftHouse = false;
    }

    void OnEnemyDeath(GameObject enemy)
    {
        totalEnemiesKilled++;

        TutorialEvents.InvokeEnemyDefeated();

        if (activeEnemies.Contains(enemy))
        {
            activeEnemies.Remove(enemy);
        }
        activeEnemies.RemoveAll(e => e == null);

        onHordeProgress?.Invoke(totalEnemiesKilled, totalEnemiesToKill);

        if (totalEnemiesKilled >= totalEnemiesToKill &&
            GetTrulyAliveEnemiesCount() <= 0 &&
            GameFlowController.Instance.CurrentPhase == GamePhase.Night &&
            !hordeCompleted && !IsBossNight())
        {
            hordeCompleted = true;
            RitualBuffManager.Instance?.ClearBuff();
            StopContinuousHorde(true);
        }
    }

    public void StopContinuousHorde(bool completed)
    {
        if (continuousSpawnCoroutine != null)
        {
            StopCoroutine(continuousSpawnCoroutine);
            continuousSpawnCoroutine = null;
        }

        isSpawning = false;

        TutorialEvents.InvokeNightSurvived();

        if (completed)
        {
            onHordeEnd?.Invoke();

            if (!IsBossNight())
            {
                if (nightToDayTransition != null)
                {
                    nightToDayTransition.StartTransition();
                }
                else
                {
                    Debug.LogWarning(
                        "[EnemiesSpawner] No NightToDayTransitionController assigned. Starting day immediately."
                    );

                    if (DayCycleController.Instance != null)
                        DayCycleController.Instance.StartDay();
                    else
                        GameFlowController.Instance.SetPhase(GamePhase.Day);
                }
            }
        }
    }

    public int GetRemainingEnemies()
    {
        return totalEnemiesToKill - totalEnemiesKilled;
    }

    public int GetTotalTargetEnemies()
    {
        return totalEnemiesToKill;
    }

    public int GetEnemiesKilled()
    {
        return totalEnemiesKilled;
    }

    public int GetCurrentEnemiesAlive()
    {
        return GetTrulyAliveEnemiesCount();
    }

    public float GetHordeProgress()
    {
        if (totalEnemiesToKill <= 0) return 0;
        return (float)totalEnemiesKilled / totalEnemiesToKill;
    }

    public string GetCurrentDifficultyInfo()
    {
        if (IsBossNight())
        {
            return "¡NOCHE DE JEFE!";
        }

        float adjustedInterval = AdjustSpawnIntervalByTime();
        return $"Spawn cada {adjustedInterval:F1}s";
    }

    public void EndNight()
    {
        var state = GameFlowController.Instance.CurrentPhase;

        if (state != GamePhase.Night && state != GamePhase.OnRitual)
            return;

        if (IsBossNight() && bossNightManager != null)
        {
            bossNightManager.ForceEndBossNight();

            if (GameFlowController.Instance.CurrentPhase != GamePhase.Day)
                GameFlowController.Instance.SetPhase(GamePhase.Day);

            return;
        }

        if (continuousSpawnCoroutine != null)
        {
            StopCoroutine(continuousSpawnCoroutine);
            continuousSpawnCoroutine = null;
        }

        List<GameObject> enemiesCopy = new List<GameObject>(activeEnemies);
        foreach (GameObject enemy in enemiesCopy)
        {
            if (enemy == null) continue;

            LifeController enemyLife = enemy.GetComponent<LifeController>();
            if (enemyLife != null)
                enemyLife.Kill();
            else
                OnEnemyDeath(enemy);
        }

        totalEnemiesKilled = totalEnemiesToKill;
        activeEnemies.Clear();
        hordeCompleted = true;

        StopContinuousHorde(true);

        StopAllCoroutines();
    }

    private void ConfigureEnemyRoulette()
    {
        enemyRoulette.Clear();

        if (DayCycleController.Instance != null &&
            DayCycleController.Instance.TotalNights == 1)
        {
            if (firstNightSkeletonPrefab == null)
            {
                Debug.LogError(
                    "[EnemiesSpawner] Assign the Skeleton prefab for the first night."
                );
                return;
            }

            enemyRoulette.Add(firstNightSkeletonPrefab, 1f);

            Debug.Log(
                "[EnemiesSpawner] First night: spawning Skeletons only."
            );

            return;
        }

        if (enemyHordes.Count == 0)
        {
            Debug.LogWarning(
                "[EnemiesSpawner] No enemy hordes configured."
            );
            return;
        }

        int hordeIndex = Mathf.Clamp(
            currentEnemyHorde,
            0,
            enemyHordes.Count - 1
        );

        EnemyHorde selectedHorde = enemyHordes[hordeIndex];

        foreach (var ew in selectedHorde.enemies)
        {
            if (ew.prefab != null && ew.weight > 0f)
            {
                enemyRoulette.Add(ew.prefab, ew.weight);
            }
        }

        Debug.Log(
            $"[EnemiesSpawner] Using enemy horde: {selectedHorde.hordeName}"
        );
    }

    private int GetTrulyAliveEnemiesCount()
    {
        activeEnemies.RemoveAll(e => e == null);
        return activeEnemies.Count;
    }

    public void AdvanceEnemyHorde()
    {
        if (enemyHordes.Count == 0)
            return;

        if (currentEnemyHorde < enemyHordes.Count - 1)
        {
            currentEnemyHorde++;

            Debug.Log(
                $"[EnemiesSpawner] Nueva horda de enemigos: {enemyHordes[currentEnemyHorde].hordeName}"
            );
        }
        else
        {
            Debug.Log(
                "[EnemiesSpawner] Ya se alcanzó la última horda de enemigos."
            );
        }
    }

    private void OnDestroy()
    {
        GameplayEvents.OnPlayerExitedHouseDuringNight -= OnPlayerExit;
    }
}


