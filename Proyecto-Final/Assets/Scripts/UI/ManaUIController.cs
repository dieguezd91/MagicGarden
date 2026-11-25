using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class ManaUIController : UIControllerBase
{
    [Header("Mana UI")]
    [SerializeField] private Slider manaBar;
    [SerializeField] private Image manaFillImage;
    [SerializeField] private Gradient manaGradient;
    [SerializeField] private TextMeshProUGUI manaText;

    [Header("Spend Pulse (mana decreases)")]
    [Tooltip("Master switch: when off, spending mana plays no pulse.")]
    [SerializeField] private bool spendPulseEnabled = true;
    [Tooltip("Transform that gets the punch. Empty uses the mana bar.")]
    [SerializeField] private Transform spendPunchTarget;
    [Tooltip("Extra scale of the spend punch (0.08 = grows 8% and returns). 0 disables the punch.")]
    [SerializeField, Min(0f)] private float spendPunchStrength = 0.08f;
    [Tooltip("Seconds the spend punch lasts (unscaled time).")]
    [SerializeField, Min(0f)] private float spendPunchDuration = 0.25f;
    [Tooltip("Color the mana fill flashes towards when mana is spent.")]
    [SerializeField] private Color spendFlashColor = Color.white;
    [Tooltip("Seconds the fill flash lasts (unscaled time). 0 disables the flash.")]
    [SerializeField, Min(0f)] private float spendFlashDuration = 0.2f;
    [Tooltip("Minimum seconds between two pulses, so continuous spending cannot chain them.")]
    [SerializeField, Min(0f)] private float spendPulseMinInterval = 0.08f;

    private ManaSystem manaSystem;
    private float lastMana = -1f;
    private float lastMax = -1f;
    private float lastPulseTime = float.NegativeInfinity;
    private Tween punchTween;
    private Vector3 punchBaseScale;
    private Tween flashTween;
    private float flashAmount;
    private Color fillBaseColor;

    private void OnDisable()
    {
        KillPulse();
    }

    protected override void OnDestroy()
    {
        KillPulse();
        base.OnDestroy();
    }

    protected override void CacheReferences()
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
            manaSystem = player.GetComponent<ManaSystem>();
    }

    protected override void SetupEventListeners()
    {
        if (manaSystem != null)
        {
            manaSystem.OnManaChanged += HandleManaChanged;
        }
    }

    protected override void CleanupEventListeners()
    {
        if (manaSystem != null)
        {
            manaSystem.OnManaChanged -= HandleManaChanged;
        }
    }

    protected override void ConfigureInitialState()
    {
        if (manaSystem != null && manaBar != null)
        {
            manaBar.maxValue = manaSystem.GetMaxMana();
            manaBar.value = manaSystem.GetCurrentMana();
            UpdateManaDisplay();
        }
    }

    private void HandleManaChanged(float current, float max)
    {
        // Only a real spend pulses: regeneration, refills and max-mana changes do not.
        bool spent = lastMana >= 0f && current < lastMana - 0.0001f && Mathf.Approximately(max, lastMax);
        lastMana = current;
        lastMax = max;

        if (manaBar != null)
        {
            manaBar.maxValue = max;
            manaBar.value = current;
        }

        float percentage = max > 0f ? Mathf.Clamp01(current / max) : 0f;
        UpdateManaFillColor(percentage);
        UpdateManaText(current, max);
        if (spent) TrySpendPulse();
    }

    public void UpdateMana()
    {
        if (manaBar == null || manaSystem == null) return;

        float current = manaSystem.GetCurrentMana();
        float max = manaSystem.GetMaxMana();
        float percentage = Mathf.Clamp01(current / max);

        manaBar.maxValue = max;
        manaBar.value = current;
        lastMana = current;
        lastMax = max;

        UpdateManaFillColor(percentage);
        UpdateManaText(current, max);
    }

    private void UpdateManaDisplay()
    {
        UpdateMana();
    }

    private void UpdateManaFillColor(float percentage)
    {
        if (manaFillImage != null && manaGradient != null)
        {
            fillBaseColor = manaGradient.Evaluate(percentage);
            manaFillImage.color = flashTween != null ? Color.Lerp(fillBaseColor, spendFlashColor, flashAmount) : fillBaseColor;
        }
    }

    private void TrySpendPulse()
    {
        if (!spendPulseEnabled || !isActiveAndEnabled || !Application.isPlaying) return;
        if (Time.unscaledTime - lastPulseTime < spendPulseMinInterval) return;
        lastPulseTime = Time.unscaledTime;
        PulseCount++;
        StartPunch();
        StartFlash();
    }

    /// <summary>How many spend pulses have started (diagnostics and tests).</summary>
    public int PulseCount { get; private set; }

    private void StartPunch()
    {
        Transform target = spendPunchTarget != null ? spendPunchTarget : (manaBar != null ? manaBar.transform : null);
        if (target == null || spendPunchStrength <= 0f || spendPunchDuration <= 0f) return;
        KillPunch();
        punchBaseScale = target.localScale;
        punchTween = target.DOPunchScale(Vector3.one * spendPunchStrength, spendPunchDuration, 1, 1f)
            .SetUpdate(true).SetLink(gameObject)
            .OnComplete(() => { if (target != null) target.localScale = punchBaseScale; punchTween = null; });
    }

    private void StartFlash()
    {
        if (manaFillImage == null || spendFlashDuration <= 0f) return;
        KillFlash();
        if (manaGradient == null) fillBaseColor = manaFillImage.color;
        flashAmount = 1f;
        manaFillImage.color = Color.Lerp(fillBaseColor, spendFlashColor, flashAmount);
        flashTween = DOVirtual.Float(1f, 0f, spendFlashDuration, v =>
            {
                flashAmount = v;
                if (manaFillImage != null) manaFillImage.color = Color.Lerp(fillBaseColor, spendFlashColor, v);
            })
            .SetEase(Ease.OutQuad).SetUpdate(true).SetLink(gameObject)
            .OnComplete(() => { EndFlash(); flashTween = null; });
    }

    private void KillPunch()
    {
        if (punchTween == null) return;
        punchTween.Kill();
        punchTween = null;
        Transform target = spendPunchTarget != null ? spendPunchTarget : (manaBar != null ? manaBar.transform : null);
        if (target != null) target.localScale = punchBaseScale;
    }

    private void KillFlash()
    {
        if (flashTween == null) return;
        flashTween.Kill();
        flashTween = null;
        EndFlash();
    }

    private void EndFlash()
    {
        flashAmount = 0f;
        if (manaFillImage != null) manaFillImage.color = fillBaseColor;
    }

    private void KillPulse()
    {
        KillPunch();
        KillFlash();
    }

    private void UpdateManaText(float current, float max)
    {
        if (manaText != null)
            manaText.text = $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";
    }

    public IEnumerator AnimateRespawnRecovery(float duration)
    {
        if (manaSystem == null) yield break;

        float startMana = 0f;
        float endMana = manaSystem.GetMaxMana();
        float time = 0f;

        while (time < duration)
        {
            float t = time / duration;
            float currentMana = Mathf.Lerp(startMana, endMana, t);

            manaSystem.SetMana(currentMana);

            time += Time.deltaTime;
            yield return null;
        }

        manaSystem.SetMana(endMana);
    }
}

