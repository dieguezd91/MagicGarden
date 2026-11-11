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
    private int lastStartedNight = -1;


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
        if (GameFlowController.Instance == null)
            return;

        GamePhase currentPhase = GameFlowController.Instance.CurrentPhase;

        if (currentPhase == GamePhase.Night &&
            lastPhase != GamePhase.Night)
        {
            if (playerHasLeftHouse ||
                (dayTimerController != null &&
                 dayTimerController.NightStartedByTimer))
            {
                if (IsBossNight())
                {
                    StartBossNight();
                }
                else
                {
                    StartContinuousHorde();
                }
            }
        }

        lastPhase = currentPhase;

        if (currentPhase == GamePhase.Night && !IsBossNight())
        {
            TryCompleteNormalHorde();
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
        if (!TryRegisterNightStart())
            return;

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

        if (!TryRegisterNightStart())
            return;

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


    private IEnumerator SpawnEnemiesIndividually()
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

            bool spawnedSuccessfully = SpawnEnemy();

            if (!spawnedSuccessfully)
            {
                Debug.LogError(
                    "[EnemiesSpawner] Spawn failed. Horde spawning stopped to prevent incorrect completion."
                );

                isSpawning = false;
                continuousSpawnCoroutine = null;
                yield break;
            }

            totalEnemiesSpawned++;

            onEnemySpawned?.Invoke();

            onHordeProgress?.Invoke(
                totalEnemiesKilled,
                totalEnemiesToKill
            );

            yield return new WaitForSeconds(adjustedInterval);
        }

        isSpawning = false;
        continuousSpawnCoroutine = null;

        TryCompleteNormalHorde();
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


    private bool SpawnEnemy()
    {
        if (spawnPoints == null || spawnPoints.Count == 0)
        {
            Debug.LogError("[EnemiesSpawner] No spawn points assigned.");
            return false;
        }

        if (IsBossNight())
            return false;

        Transform spawnPoint;

        if (useRandomSpawnPoint)
        {
            if (dontSpawnWhenPlayerNearby && playerTransform != null)
            {
                List<Transform> validSpawnPoints = new List<Transform>();

                foreach (Transform point in spawnPoints)
                {
                    if (point != null &&
                        Vector3.Distance(point.position, playerTransform.position) > playerCheckRadius)
                    {
                        validSpawnPoints.Add(point);
                    }
                }

                if (validSpawnPoints.Count > 0)
                {
                    spawnPoint = validSpawnPoints[
                        Random.Range(0, validSpawnPoints.Count)
                    ];
                }
                else
                {
                    spawnPoint = spawnPoints[Random.Range(0, spawnPoints.Count)];
                }
            }
            else
            {
                spawnPoint = spawnPoints[Random.Range(0, spawnPoints.Count)];
            }
        }
        else
        {
            spawnPoint = spawnPoints[
                totalEnemiesSpawned % spawnPoints.Count
            ];
        }

        if (spawnPoint == null)
            return false;

        GameObject selectedEnemyPrefab = enemyRoulette.Roll();

        if (selectedEnemyPrefab == null)
        {
            Debug.LogWarning(
                "[EnemiesSpawner] No enemies available in the current roulette."
            );

            return false;
        }

        GameObject enemy = Instantiate(
            selectedEnemyPrefab,
            spawnPoint.position,
            spawnPoint.rotation
        );

        if (enemy == null)
            return false;

        LifeController enemyLife = enemy.GetComponent<LifeController>();

        if (enemyLife == null)
        {
            Debug.LogError(
                $"[EnemiesSpawner] Enemy {enemy.name} has no LifeController!",
                enemy
            );

            Destroy(enemy);
            return false;
        }

        enemy.transform.SetParent(transform);

        activeEnemies.Add(enemy);

        enemyLife.onDeath.AddListener(() => OnEnemyDeath(enemy));

        return true;
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

    private void OnEnemyDeath(GameObject enemy)
    {
        if (enemy == null)
            return;

        if (!activeEnemies.Remove(enemy))
        {
            Debug.LogWarning(
                $"[EnemiesSpawner] Ignored duplicate or untracked death: {enemy.name}"
            );

            return;
        }

        totalEnemiesKilled++;

        TutorialEvents.InvokeEnemyDefeated();

        activeEnemies.RemoveAll(e => e == null);

        onHordeProgress?.Invoke(
            totalEnemiesKilled,
            totalEnemiesToKill
        );

        TryCompleteNormalHorde();
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
        if (GameFlowController.Instance == null)
            return;

        GamePhase currentPhase = GameFlowController.Instance.CurrentPhase;

        if (currentPhase != GamePhase.Night &&
            currentPhase != GamePhase.OnRitual)
            return;

        if (hordeCompleted)
            return;

        Debug.Log("[EnemiesSpawner] Force ending current night.");

        if (IsBossNight() && bossNightManager != null)
        {
            bossNightManager.ForceEndBossNight();
            return;
        }

        if (continuousSpawnCoroutine != null)
        {
            StopCoroutine(continuousSpawnCoroutine);
            continuousSpawnCoroutine = null;
        }

        isSpawning = false;

        hordeCompleted = true;

        List<GameObject> enemiesCopy = new List<GameObject>(activeEnemies);

        foreach (GameObject enemy in enemiesCopy)
        {
            if (enemy == null)
                continue;

            LifeController enemyLife = enemy.GetComponent<LifeController>();

            if (enemyLife != null && enemyLife.IsAlive())
            {
                enemyLife.Kill();
            }
            else if (enemyLife == null)
            {
                Destroy(enemy);
            }
        }

        totalEnemiesSpawned = totalEnemiesToKill;
        totalEnemiesKilled = totalEnemiesToKill;

        activeEnemies.Clear();

        RitualBuffManager.Instance?.ClearBuff();

        onHordeProgress?.Invoke(
            totalEnemiesKilled,
            totalEnemiesToKill
        );

        Debug.Log("[EnemiesSpawner] All enemies removed. Horde force-completed.");

        StopContinuousHorde(true);
    }


    // Cheat: kills every living enemy; spawning continues. Bosses are skipped because killing one ends the night.
    public void KillAllAlive()
    {
        foreach (EnemyBase enemy in FindObjectsOfType<EnemyBase>())
        {
            if (enemy is Boss) continue;

            LifeController life = enemy.GetComponent<LifeController>();
            if (life != null && life.IsAlive())
                life.Kill();
        }
    }

    // Cheat: spawns the rest of tonight's horde at once and kills it, so the night completes normally with all the loot.
    public void SpawnRemainingAndKillAll()
    {
        if (GameFlowController.Instance == null ||
            GameFlowController.Instance.CurrentPhase != GamePhase.Night ||
            hordeCompleted)
            return;

        if (IsBossNight())
        {
            EndNight();
            return;
        }

        StartCoroutine(SpawnRemainingAndKillAllRoutine());
    }

    private IEnumerator SpawnRemainingAndKillAllRoutine()
    {
        // Horde not started yet (e.g. player still inside the house): start it so counts and roulette are set.
        if (DayCycleController.Instance != null &&
            lastStartedNight != DayCycleController.Instance.TotalNights)
            StartContinuousHorde();

        if (continuousSpawnCoroutine != null)
        {
            StopCoroutine(continuousSpawnCoroutine);
            continuousSpawnCoroutine = null;
        }
        isSpawning = false;

        while (totalEnemiesSpawned < totalEnemiesToKill && SpawnEnemy())
        {
            totalEnemiesSpawned++;
            onEnemySpawned?.Invoke();
        }

        if (totalEnemiesSpawned < totalEnemiesToKill)
        {
            Debug.LogError("[EnemiesSpawner] Spawn-all cheat could not spawn the full horde; force ending night instead.");
            EndNight();
            yield break;
        }

        onHordeProgress?.Invoke(totalEnemiesKilled, totalEnemiesToKill);

        // Wait a frame so the new enemies run Start() (LifeController/animator init) before being killed.
        yield return null;

        KillAllAlive();
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

    private bool TryRegisterNightStart()
    {
        if (DayCycleController.Instance == null)
            return false;

        int currentNight = DayCycleController.Instance.TotalNights;

        if (lastStartedNight == currentNight)
        {
            Debug.LogWarning(
                $"[EnemiesSpawner] Horde already started for night {currentNight}."
            );

            return false;
        }

        lastStartedNight = currentNight;
        return true;
    }

    private bool TryCompleteNormalHorde()
    {
        if (hordeCompleted)
            return false;

        if (GameFlowController.Instance == null ||
            GameFlowController.Instance.CurrentPhase != GamePhase.Night)
            return false;

        if (IsBossNight())
            return false;

        if (totalEnemiesSpawned < totalEnemiesToKill)
            return false;

        if (totalEnemiesKilled < totalEnemiesToKill)
            return false;

        if (GetTrulyAliveEnemiesCount() > 0)
            return false;

        Debug.Log(
            $"[EnemiesSpawner] Horde completed! " +
            $"Killed: {totalEnemiesKilled}/{totalEnemiesToKill}"
        );

        hordeCompleted = true;

        RitualBuffManager.Instance?.ClearBuff();

        StopContinuousHorde(true);

        return true;
    }

    public bool IsNormalHordeComplete
    {
        get
        {
            if (IsBossNight() || !hordeCompleted)
                return false;

            return totalEnemiesSpawned >= totalEnemiesToKill &&
                   totalEnemiesKilled >= totalEnemiesToKill &&
                   GetTrulyAliveEnemiesCount() == 0;
        }
    }

    private void OnDestroy()
    {
        GameplayEvents.OnPlayerExitedHouseDuringNight -= OnPlayerExit;
    }
}


