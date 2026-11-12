using System;
using UnityEngine;

public class TilePlantInfo
{
    public bool isOccupied;
    public Plant currentPlant;
}

public class Plant : MonoBehaviour
{
    [Header("Plant Data")]
    [SerializeField] public PlantDataSO plantData;

    [SerializeField] private PlantSoundBase _soundBase;

    [Header("State")]
    [SerializeField] public int plantingDay;
    [SerializeField] private bool growthCompleted = false;

    [Header("FX")]
    [SerializeField] private GameObject preMatureParticlesPrefab;
    private GameObject activeParticlesInstance;
    private bool particlesActivated = false;

    [SerializeField] private GameObject lowHealthParticlesPrefab;
    [SerializeField][Range(0f, 1f)] private float lowHealthThreshold = 0.5f;
    private GameObject lowHealthParticlesInstance;
    private bool lowHealthParticlesActive = false;

    [Header("UI")]
    [SerializeField] private PlantHealthBar healthBar;
    [SerializeField] private float healthBarVisibilityDistance = 2f;

    protected int daysToGrow;
    protected Sprite plantSprite;

    private LifeController plantLifeController;
    [HideInInspector] public Vector3Int tilePosition;

    protected Animator animator;

    protected PlayerAbilitySystem abilitySystem;
    private Transform playerTransform;

    private Collider2D plantCollider;

    public PlantSoundBase SoundBase => _soundBase;
    protected void PlaySound(PlantSoundType type)
    {
        if (_soundBase != null) _soundBase.PlaySound(type, SoundSourceType.Localized, transform);
    }
    public string UpgradeId => plantData != null ? plantData.UpgradeLineId : null;
    public virtual MagicGarden.Target DescribeUpgradeTarget()
    {
        var target = new MagicGarden.Target { Id = UpgradeId, Name = plantData != null ? plantData.plantName : name, Capability = GetType().Name };
        var electric = GetComponent<ElectricPlantAura>();
        var rose = GetComponent<StormRoseReactiveAura>();
        if (electric != null) electric.Describe(target);
        else if (rose != null) rose.Describe(target);
        if (IsFullyGrown()) target.Name += " (mature)";
        return target;
    }
    public float Upgraded(MagicGarden.Stat stat, float basis) => UpgradeRuntime.Value(UpgradeId, stat, basis, DescribeCapability());
    public float UpgradedCooldown(float basis) => UpgradeRuntime.Cooldown(UpgradeId, basis, DescribeCapability());
    private string DescribeCapability()
    {
        if (GetType() != typeof(Plant)) return GetType().Name;
        if (GetComponent<ElectricPlantAura>() != null) return "ElectricPlantAura";
        if (GetComponent<StormRoseReactiveAura>() != null) return "StormRoseReactiveAura";
        return "Plant";
    }

    private float nextIdleSoundTime = 0f;
    [SerializeField] private float idleSoundMinTime = 5f;
    [SerializeField] private float idleSoundMaxTime = 15f;

    protected virtual void Awake()
    {
        if (_soundBase == null)
        {
            _soundBase = GetComponent<PlantSoundBase>();
        }
    }

    protected virtual void Start()
    {
        animator = GetComponent<Animator>();

        plantCollider = GetComponent<Collider2D>();
        if (plantCollider != null)
        {
            plantCollider.enabled = false;
        }

        plantingDay = DayCycleController.Instance.CurrentDay;
        GameFlowController.Instance.OnPhaseChanged += HandleGameStateChanged;

        abilitySystem = FindObjectOfType<PlayerAbilitySystem>();

        playerTransform = GameObject.FindGameObjectWithTag("Player")?.transform;

        if (healthBar == null)
        {
            healthBar = GetComponentInChildren<PlantHealthBar>();
        }
        if (healthBar != null)
        {
            healthBar.gameObject.SetActive(false);
        }


        if (plantData != null)
        {
            daysToGrow = plantData.daysToGrow;
            plantSprite = plantData.plantIcon;
        }

        plantLifeController = GetComponent<LifeController>();
        if (plantLifeController == null)
        {
            plantLifeController = gameObject.AddComponent<LifeController>();
        }

        ConfigureLifeController();

        plantLifeController.onDeath.AddListener(HandlePlantDeath);
        plantLifeController.onHealthChanged.AddListener(HandleHealthChanged);
        plantLifeController.onDamaged.AddListener(HandlePlantDamaged);

        ScheduleNextIdleSound();
    }

    protected virtual void OnDestroy()
    {
        if (LevelManager.Instance != null)
        {
            GameFlowController.Instance.OnPhaseChanged -= HandleGameStateChanged;
        }

        if (plantLifeController != null)
        {
            plantLifeController.onDeath.RemoveListener(HandlePlantDeath);
            plantLifeController.onHealthChanged.RemoveListener(HandleHealthChanged);
            plantLifeController.onDamaged.RemoveListener(HandlePlantDamaged);
        }
    }

    protected virtual void Update()
    {
        UpdateIdleSoundTimer();
        UpdateHealthBarVisibility();
    }

    private void UpdateGrowthStatus(int currentDay)
    {
        if (growthCompleted)
            return;

        int daysSincePlanting = currentDay - plantingDay;

        if (!particlesActivated && daysSincePlanting == daysToGrow - 1)
        {
            ActivatePreMatureParticles();
        }

        if (daysSincePlanting > daysToGrow)
        {
            CompleteGrowth();
            return;
        }

        SetGrowthAnimationStage(daysSincePlanting);
    }

    private void SetGrowthAnimationStage(int daysSincePlanting)
    {
        float growthProgress = Mathf.Clamp01((float)daysSincePlanting / daysToGrow);
        float blendValue = growthProgress * 2f;

        if (animator != null)
        {
            animator.SetFloat("GrowthStage", blendValue);
        }
    }

    private void ActivatePreMatureParticles()
    {
        if (!particlesActivated && preMatureParticlesPrefab != null)
        {
            activeParticlesInstance = Instantiate(preMatureParticlesPrefab, transform);
            activeParticlesInstance.transform.localPosition = Vector3.zero;
            particlesActivated = true;
        }
    }

    private void DeactivatePreMatureParticles()
    {
        if (particlesActivated)
        {
            if (activeParticlesInstance != null)
            {
                Destroy(activeParticlesInstance);
                activeParticlesInstance = null;
            }
            particlesActivated = false;
        }
    }

    private float nextHurtSoundTime;
    private static float lastGrownSoundTime = float.MinValue;

    // The hurt clip was assigned on every plant but nothing ever played it.
    private void HandlePlantDamaged(float damage, LifeController.DamageType damageType)
    {
        if (plantLifeController.currentHealth <= 0f) return; // killing blow: the die sound plays instead
        if (Time.time < nextHurtSoundTime) return;
        nextHurtSoundTime = Time.time + (damageType == LifeController.DamageType.DamageOverTime ? 0.6f : 0.3f);
        PlaySound(PlantSoundType.Hurt);
    }

    private void HandleHealthChanged(float currentHealth, float maxHealth)
    {
        if (maxHealth <= 0) return;

        float healthPercent = currentHealth / maxHealth;

        if (healthPercent <= lowHealthThreshold)
        {
            ActivateLowHealthParticles();
        }
        else
        {
            DeactivateLowHealthParticles();
        }
    }

    private void ActivateLowHealthParticles()
    {
        if (!lowHealthParticlesActive && lowHealthParticlesPrefab != null)
        {
            lowHealthParticlesInstance = Instantiate(lowHealthParticlesPrefab, transform);
            lowHealthParticlesInstance.transform.localPosition = Vector3.zero;
            lowHealthParticlesActive = true;
        }
    }

    private void DeactivateLowHealthParticles()
    {
        if (lowHealthParticlesActive)
        {
            if (lowHealthParticlesInstance != null)
            {
                Destroy(lowHealthParticlesInstance);
                lowHealthParticlesInstance = null;
            }
            lowHealthParticlesActive = false;
        }
    }

    private void UpdateHealthBarVisibility()
    {
        if (healthBar == null) return;

        if (playerTransform == null)
        {
            healthBar.gameObject.SetActive(false);
            return;
        }

        float distance = Vector3.Distance(transform.position, playerTransform.position);

        bool shouldBeVisible = growthCompleted && (distance <= healthBarVisibilityDistance);

        if (healthBar.gameObject.activeSelf != shouldBeVisible)
        {
            healthBar.gameObject.SetActive(shouldBeVisible);
        }
    }

    public void OnNewDay(int currentDay)
    {
        UpdateGrowthStatus(currentDay);
    }

    public void CompleteGrowth()
    {
        if (growthCompleted)
            return;

        growthCompleted = true;
        DeactivatePreMatureParticles();

        if (animator != null)
        {
            animator.SetBool("GrowthCompleted", true);
            animator.SetFloat("GrowthStage", 2f);
        }

        OnMature();
        // Many plants mature at once at dawn: one growth sound per 0.2 s across all plants.
        if (Time.time - lastGrownSoundTime >= 0.2f || Time.time < lastGrownSoundTime)
        {
            lastGrownSoundTime = Time.time;
            PlaySound(PlantSoundType.Grown);
        }

        if (plantCollider != null)
            plantCollider.enabled = true;
    }


    protected virtual void OnMature()
    {
        if (plantLifeController != null)
        {
            float newMaxHealth = plantLifeController.maxHealth * 1.5f;
            plantLifeController.maxHealth = newMaxHealth;
            plantLifeController.currentHealth = newMaxHealth;
            plantLifeController.onHealthChanged?.Invoke(plantLifeController.currentHealth, plantLifeController.maxHealth);
        }
    }

    public bool IsFullyGrown()
    {
        return growthCompleted;
    }

    public float GetGrowthPercentage()
    {
        if (growthCompleted)
            return 1.0f;

        int currentDay = DayCycleController.Instance.CurrentDay;
        int daysSincePlanting = currentDay - plantingDay;

        if (daysSincePlanting <= 0)
            return 0f;

        return Mathf.Clamp01((float)daysSincePlanting / daysToGrow);
    }

    private void HandlePlantDeath()
    {
        DeactivatePreMatureParticles();
        DeactivateLowHealthParticles();

        if (healthBar != null)
        {
            healthBar.gameObject.SetActive(false);
        }

        SoundBase.PlaySound(PlantSoundType.Die, SoundSourceType.Localized, transform);
        TilePlantingSystem.Instance.UnregisterPlantAt(tilePosition);
        RewardsSystem.Instance?.NotifyPlantDestroyed();
    }

    private void ConfigureLifeController()
    {
        bool hasAnimation = plantData != null && plantData.hasDeathAnimation;
        plantLifeController.ConfigureAsPlant(hasAnimation);
    }

    protected virtual void HandleGameStateChanged(GamePhase newPhase)
    {
        if (newPhase == GamePhase.Night)
        {
            UpdateGrowthStatus(DayCycleController.Instance.CurrentDay);
        }
    }

    private void UpdateIdleSoundTimer()
    {
        if (growthCompleted && Time.time >= nextIdleSoundTime)
        {
            PlayIdleSound();
            ScheduleNextIdleSound();
        }
    }

    private void ScheduleNextIdleSound()
    {
        nextIdleSoundTime = Time.time + UnityEngine.Random.Range(idleSoundMinTime, idleSoundMaxTime);
    }

    private void PlayIdleSound()
    {
        // TODO: Implement idle sound logic here for Plant
        // Example: _soundBase?.PlaySound(PlantSoundType.Idle, SoundSourceType.Localized, transform);
    }

    public PlantDataSO GetPlantData()
    {
        return plantData;
    }
}

