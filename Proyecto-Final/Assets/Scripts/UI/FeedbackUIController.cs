using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections;

public class FeedbackUIController : UIControllerBase
{
    [Header("Damage Feedback")]
    [SerializeField] private GameObject floatingDamagePrefab;
    [SerializeField] private GameObject damagedScreen;
    [SerializeField] private float damageFadeSpeed = 2f;
    [SerializeField] private float damageFadeOutDelay = 1f;
    [SerializeField, Tooltip("Show the floating damage numbers over the player when it takes damage. Off by default: the damage screen already signals the hit.")]
    private bool showPlayerDamageNumbers = false;

    [Header("Post-Process Effects")]
    [SerializeField] private Volume bloomVolume;

    [Header("Debug")]
    [SerializeField] private bool enableRitualDebug = true;

    private CanvasGroup damagedScreenCanvasGroup;
    private float targetDamageAlpha = 0f;
    private float lastDamageTime = 0f;
    private Coroutine ritualOverlayCoroutine;

    private Bloom bloom;
    private ColorAdjustments colorAdjustments;
    private GameObject player;
    private LifeController playerLife;

    protected override void CacheReferences()
    {
        player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerLife = player.GetComponent<LifeController>();
        }
    }

    protected override void SetupEventListeners()
    {
        if (playerLife != null)
        {
            playerLife.onDamaged.AddListener(HandlePlayerDamaged);
        }
    }

    protected override void CleanupEventListeners()
    {
        if (playerLife != null)
        {
            playerLife.onDamaged.RemoveListener(HandlePlayerDamaged);
        }
    }

    private void HandlePlayerDamaged(float damage, LifeController.DamageType damageType)
    {
        if (player == null)
            return;

        ShowDamageEffect(damage, player.transform.position);
    }

    protected override void ConfigureInitialState()
    {
        SetupDamagedScreen();
        SetupPostProcessing();
        lastDamageTime = Time.time;
    }

    private void SetupDamagedScreen()
    {
        if (damagedScreen != null)
        {
            damagedScreenCanvasGroup = damagedScreen.GetComponent<CanvasGroup>()
                ?? damagedScreen.AddComponent<CanvasGroup>();

            damagedScreenCanvasGroup.alpha = 0f;
            damagedScreenCanvasGroup.interactable = false;
            damagedScreenCanvasGroup.blocksRaycasts = false;
            damagedScreen.SetActive(true);
        }
    }

    private void SetupPostProcessing()
    {
        if (bloomVolume != null && bloomVolume.profile != null)
        {
            bloomVolume.profile.TryGet(out bloom);
            bloomVolume.profile.TryGet(out colorAdjustments);
        }
    }

    public override void HandleUpdate()
    {
        UpdateDamageScreen();
    }

    private void UpdateDamageScreen()
    {
        if (damagedScreenCanvasGroup == null) return;

        if (Time.time - lastDamageTime > damageFadeOutDelay)
            targetDamageAlpha = 0f;

        damagedScreenCanvasGroup.alpha = Mathf.Lerp(
            damagedScreenCanvasGroup.alpha,
            targetDamageAlpha,
            damageFadeSpeed * Time.deltaTime
        );
    }

    public void ShowDamageEffect(float damage, Vector3 worldPosition)
    {
        if (showPlayerDamageNumbers)
            ShowFloatingDamage(damage, worldPosition);
        ShowDamageScreen(damage);
    }

    private void ShowFloatingDamage(float damage, Vector3 worldPosition)
    {
        if (floatingDamagePrefab == null || player == null) return;

        var existing = player.GetComponentInChildren<FloatingDamageText>();
        if (existing != null)
        {
            existing.AddDamage(damage);
            return;
        }

        Vector3 spawnPos = worldPosition + Vector3.up * 1f;
        var damageObj = Instantiate(floatingDamagePrefab, spawnPos, Quaternion.identity);

        var floatingDamage = damageObj.GetComponent<FloatingDamageText>();
        if (floatingDamage != null)
        {
            floatingDamage.Initialize(player.transform);
            floatingDamage.AddDamage(damage);
        }
    }

    private void ShowDamageScreen(float damage)
    {
        if (damagedScreenCanvasGroup == null) return;

        lastDamageTime = Time.time;

        float intensity = Mathf.Clamp01(damage / 50f);
        targetDamageAlpha = 0.3f + (intensity * 0.4f);

        RewardsSystem.Instance?.NotifyPlayerDamaged();
    }

    public void SetGrayscaleEffect(bool enabled)
    {
        if (bloom != null)
            bloom.active = !enabled;

        if (colorAdjustments != null)
            colorAdjustments.saturation.value = enabled ? -100f : 15f;
    }
}