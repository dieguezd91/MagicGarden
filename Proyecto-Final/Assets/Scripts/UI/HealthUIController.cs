using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using DG.Tweening;

public class HealthUIController : UIControllerBase
{
    [Header("Player Health")]
    [SerializeField] private Slider healthBar;
    [SerializeField] private Image fillImage;
    [SerializeField] private Gradient healthGradient;
    [SerializeField] private TextMeshProUGUI playerHealthText;
    [SerializeField] private Image whiteFillImage;
    [SerializeField] private float whiteBarSpeed = 1.5f;

    [Header("Player Hit Punch")]
    [Tooltip("Pops the player health bar when health goes down. Untick to disable. Nothing happens when health goes up.")]
    [SerializeField] private bool punchOnDamage = true;
    [Tooltip("Scale added by the punch (0.15 = +15%). 0 disables it.")]
    [SerializeField] private float punchScale = 0.15f;
    [Tooltip("Punch duration in real seconds (ignores slow motion). 0 disables it.")]
    [SerializeField] private float punchDuration = 0.25f;
    [Tooltip("How many times the punch oscillates.")]
    [SerializeField] private int punchVibrato = 8;
    [Tooltip("Minimum real seconds between two punches, so rapid damage ticks do not shake the bar constantly.")]
    [SerializeField] private float punchMinInterval = 0.1f;

    [Header("Home Health")]
    [SerializeField] private Slider homeHealthBar;
    [SerializeField] private Image homeFillImage;
    [SerializeField] private Gradient homeHealthGradient;
    [SerializeField] private TextMeshProUGUI homeHealthText;

    private float targetFill = 1f;
    private float lastHealth = 1f;
    private float lastCurrentHealth = float.NaN;
    private Tween punchTween;
    private Transform punchedTransform;
    private Vector3 punchBaseScale;
    private float lastPunchTime = float.NegativeInfinity;
    private Coroutine whiteBarRoutine;
    private LifeController playerLife;
    private HouseLifeController homeLife;
    private GameObject player;

    protected override void CacheReferences()
    {
        player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
            playerLife = player.GetComponent<LifeController>();

        if (GameObject.FindGameObjectWithTag("Home") != null)
            homeLife = GameObject.FindGameObjectWithTag("Home").GetComponent<HouseLifeController>();
    }

    protected override void SetupEventListeners()
    {
        if (playerLife != null)
            playerLife.onHealthChanged.AddListener(UpdatePlayerHealth);

        if (homeLife != null)
            homeLife.onHealthChanged.AddListener(UpdateHomeHealth);
    }

    protected override void ConfigureInitialState()
    {
        if (healthBar != null)
        {
            healthBar.minValue = 0;
            healthBar.maxValue = playerLife?.maxHealth ?? 100f;
            healthBar.value = playerLife?.currentHealth ?? 100f;
        }

        if (homeHealthBar != null && homeLife != null)
        {
            homeHealthBar.minValue = 0;
            homeHealthBar.maxValue = homeLife.MaxHealth;
            homeHealthBar.value = homeLife.CurrentHealth;
        }

        if (whiteFillImage != null && healthBar != null)
            whiteFillImage.fillAmount = healthBar.value / healthBar.maxValue;

        lastCurrentHealth = playerLife != null ? playerLife.currentHealth : float.NaN;
    }

    public override void HandleUpdate()
    {
        UpdateWhiteBar();
    }

    public void UpdatePlayerHealth(float currentHealth, float maxHealth)
    {
        if (healthBar == null || maxHealth <= 0f) return;

        healthBar.maxValue = maxHealth;

        float healthPercentage = Mathf.Clamp01(currentHealth / maxHealth);
        targetFill = healthPercentage;

        // Compared on absolute health so raising the max health does not count as damage.
        bool tookDamage = !float.IsNaN(lastCurrentHealth) && currentHealth < lastCurrentHealth;
        lastCurrentHealth = currentHealth;

        healthBar.value = currentHealth;
        if (tookDamage) TryPunchHealthBar();

        UpdatePlayerFillColor(healthPercentage);
        UpdatePlayerHealthText(currentHealth, maxHealth);

        if (whiteFillImage != null)
        {
            if (healthPercentage < lastHealth)
            {
                if (whiteBarRoutine != null)
                    StopCoroutine(whiteBarRoutine);

                whiteBarRoutine = StartCoroutine(BlinkWhiteBar(healthPercentage));
            }
            else
            {
                whiteFillImage.fillAmount = healthPercentage;
            }
        }

        lastHealth = healthPercentage;
    }


    // Always restarts from the stored base scale, so repeated punches never leave the bar drifted.
    private void TryPunchHealthBar()
    {
        if (!punchOnDamage || punchScale <= 0f || punchDuration <= 0f || healthBar == null) return;
        if (Time.unscaledTime - lastPunchTime < punchMinInterval) return;
        lastPunchTime = Time.unscaledTime;

        KillPunch();
        punchedTransform = healthBar.transform;
        punchBaseScale = punchedTransform.localScale;
        var target = punchedTransform;
        var baseScale = punchBaseScale;
        punchTween = target.DOPunchScale(Vector3.one * punchScale, punchDuration, Mathf.Max(1, punchVibrato), 1f)
            .SetUpdate(true).SetLink(gameObject)
            .OnComplete(() => { if (target != null) target.localScale = baseScale; });
    }

    private void KillPunch()
    {
        bool running = punchTween != null && punchTween.IsActive();
        if (running) punchTween.Kill();
        punchTween = null;
        if (running && punchedTransform != null) punchedTransform.localScale = punchBaseScale;
    }

    private void OnDisable() => KillPunch();

    protected override void OnCleanup() => KillPunch();

    private IEnumerator BlinkWhiteBar(float targetNormalized)
    {
        const int blinks = 2;
        const float interval = 0.1f;

        for (int i = 0; i < blinks; i++)
        {
            whiteFillImage.enabled = false;
            yield return new WaitForSeconds(interval);
            whiteFillImage.enabled = true;
            yield return new WaitForSeconds(interval);
        }

        while (whiteFillImage.fillAmount > targetNormalized)
        {
            whiteFillImage.fillAmount = Mathf.MoveTowards(
                whiteFillImage.fillAmount,
                targetNormalized,
                whiteBarSpeed * Time.deltaTime
            );
            yield return null;
        }

        whiteFillImage.fillAmount = targetNormalized;
        whiteBarRoutine = null;
    }

    public void UpdateHomeHealth(float currentHealth, float maxHealth)
    {
        if (homeHealthBar == null) return;

        float healthPercentage = Mathf.Clamp01(currentHealth / maxHealth);
        homeHealthBar.value = currentHealth;
        UpdateHomeFillColor(healthPercentage);
        UpdateHomeHealthText(currentHealth, maxHealth);
    }

    private void UpdatePlayerFillColor(float healthPercentage)
    {
        if (fillImage != null && healthGradient != null)
            fillImage.color = healthGradient.Evaluate(healthPercentage);
    }

    private void UpdateHomeFillColor(float healthPercentage)
    {
        if (homeFillImage != null && homeHealthGradient != null)
            homeFillImage.color = homeHealthGradient.Evaluate(healthPercentage);
    }

    private void UpdatePlayerHealthText(float current, float max)
    {
        if (playerHealthText != null)
            playerHealthText.text = $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";
    }

    private void UpdateHomeHealthText(float current, float max)
    {
        if (homeHealthText != null)
            homeHealthText.text = $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";
    }

    private void UpdateWhiteBar()
    {
        if (whiteFillImage == null || healthBar == null) return;

        float current = whiteFillImage.fillAmount;
        float target = healthBar.maxValue > 0f ? (healthBar.value / healthBar.maxValue) : 0f;

        if (current > target)
        {
            whiteFillImage.fillAmount = Mathf.MoveTowards(current, target, whiteBarSpeed * Time.deltaTime);
        }
        else
        {
            whiteFillImage.fillAmount = target;
        }
    }

    public IEnumerator AnimateRespawnRecovery(float duration)
    {
        if (playerLife == null) yield break;

        float startHealth = 0f;
        float endHealth = playerLife.maxHealth;
        float time = 0f;

        while (time < duration)
        {
            float t = time / duration;
            float currentHealth = Mathf.Lerp(startHealth, endHealth, t);

            UpdatePlayerHealth(currentHealth, playerLife.maxHealth);

            time += Time.deltaTime;
            yield return null;
        }

        UpdatePlayerHealth(endHealth, playerLife.maxHealth);
    }

    protected override void CleanupEventListeners()
    {
        if (playerLife != null)
            playerLife.onHealthChanged.RemoveListener(UpdatePlayerHealth);

        if (homeLife != null)
            homeLife.onHealthChanged.RemoveListener(UpdateHomeHealth);
    }
}
