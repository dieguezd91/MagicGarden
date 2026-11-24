using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class FloatingTextController : MonoBehaviour
{
    [Header("Shared UI Panel")]
    [SerializeField] private Transform pickupPanel;
    [SerializeField] private GameObject pickupEntryPrefab;
    [SerializeField] private TMP_SpriteAsset itemSpriteAsset;
    [SerializeField] private float pickupDisplayTime = 1.5f;
    [SerializeField] private float fadeDuration = 0.25f;
    [SerializeField] private Color warningColor;
    [SerializeField] private Color defaultColor = Color.white;

    [Header("Sprite Name Mapping")]
    [SerializeField] private List<SpriteMapping> spriteMappings = new List<SpriteMapping>();

    [Header("Gold Feedback")]
    [SerializeField] private Transform goldRewardPanel;
    [SerializeField] private GameObject goldEntryPrefab;
    [SerializeField] private float goldFeedbackDisplayTime = 6f;
    [SerializeField] private Color goldRewardColor = new Color(1f, 0.8f, 0.2f);
    [SerializeField] private Color goldTotalColor = new Color(1f, 0.9f, 0.3f);
    private const string DefaultGoldEntryFormat = "<sprite name=\"GoldIcon\"> {0}\n+{1} gold";
    [Tooltip("Line shown for each night reward. {0} = message, {1} = gold amount. Empty or invalid uses the default")]
    [TextArea(2, 4)]
    [SerializeField] private string goldEntryFormat = DefaultGoldEntryFormat;
    [Tooltip("Shown when the night earned no reward")]
    [SerializeField] private string noRewardMessage = "The shadows watch in silence...";
    [SerializeField] private Color noRewardColor = new Color(0.6f, 0.2f, 0.2f);

    [Header("Gold Feedback Animation (unscaled time)")]
    [Tooltip("Seconds between one reward line appearing and the next")]
    [SerializeField, Min(0f)] private float goldLineStagger = 0.35f;
    [Tooltip("Seconds each line takes to pop in (scale and fade)")]
    [SerializeField, Min(0f)] private float goldLineEnterDuration = 0.3f;
    [Tooltip("Scale a line starts from before it bounces to its normal size (1 = no bounce)")]
    [SerializeField, Min(0f)] private float goldLineStartScale = 0.4f;
    [Tooltip("Easing of the line pop. OutBack overshoots, which gives the bounce")]
    [SerializeField] private Ease goldLineEnterEase = Ease.OutBack;
    [Tooltip("Seconds the gold amount of a line takes to count from 0 up to its value")]
    [SerializeField, Min(0f)] private float goldCountDuration = 0.6f;
    [Tooltip("Name of the SoundManager sound played as the gold counts up. Empty = no sound")]
    [SerializeField] private string goldTickSound = "PickUp";
    [Tooltip("Minimum seconds between two coin ticks")]
    [SerializeField, Min(0f)] private float goldTickInterval = 0.06f;

    public static FloatingTextController Instance { get; private set; }

    private float panelTimer;
    private Dictionary<string, (int amount, GameObject entry)> pickups = new();
    private Dictionary<Transform, FloatingDamageText> damageTexts = new();
    private CanvasGroup pickupPanelCanvasGroup;
    private Dictionary<string, string> spriteNameMap;

    private List<GameObject> goldFeedbackEntries = new List<GameObject>();
    private bool isShowingGoldFeedback = false;
    private readonly List<Sequence> goldSequences = new List<Sequence>();
    private float lastGoldTickTime = float.NegativeInfinity;

    [Serializable]
    public class SpriteMapping
    {
        public string originalSpriteName;
        public string tmpSpriteName;
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (pickupPanel != null)
            pickupPanelCanvasGroup = pickupPanel.GetComponent<CanvasGroup>();

        SetPanelAlpha(pickupPanelCanvasGroup, 0f);

        InitializeSpriteMapping();
    }

    private void InitializeSpriteMapping()
    {
        spriteNameMap = new Dictionary<string, string>();

        foreach (var mapping in spriteMappings)
        {
            if (!string.IsNullOrEmpty(mapping.originalSpriteName) &&
                !string.IsNullOrEmpty(mapping.tmpSpriteName))
            {
                spriteNameMap[mapping.originalSpriteName] = mapping.tmpSpriteName;
            }
        }
    }

    private void OnDestroy()
    {
        KillGoldSequences();
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        if (panelTimer > 0f)
        {
            panelTimer -= Time.deltaTime;
            if (panelTimer <= 0f)
            {
                if (isShowingGoldFeedback)
                {
                    StartCoroutine(FadeOutPanel(pickupPanelCanvasGroup, ClearGoldFeedback));
                }
                else
                {
                    StartCoroutine(FadeOutPanel(pickupPanelCanvasGroup, ClearPickups));
                }
            }
        }
    }

    public void ShowPickup(string materialName, int amount, Sprite icon)
    {
        if (isShowingGoldFeedback) return;
        if (pickupPanel == null || pickupEntryPrefab == null) return;

        StartCoroutine(FadeInPanel(pickupPanelCanvasGroup));
        panelTimer = pickupDisplayTime;

        string tmpSpriteName = GetTMPSpriteName(icon);
        string spriteTag = !string.IsNullOrEmpty(tmpSpriteName) ? $"<sprite name=\"{tmpSpriteName}\"> " : "";

        if (pickups.TryGetValue(materialName, out var data))
        {
            int newAmt = data.amount + amount;
            pickups[materialName] = (newAmt, data.entry);

            var txt = data.entry.GetComponent<TextMeshProUGUI>();
            if (txt != null)
            {
                txt.text = $"{spriteTag}+{newAmt} {materialName}";
            }
        }
        else
        {
            var entry = Instantiate(pickupEntryPrefab, pickupPanel);
            var txt = entry.GetComponent<TextMeshProUGUI>();

            if (txt != null)
            {
                txt.color = warningColor;
                txt.text = $"{spriteTag}+{amount} {materialName}";

                if (txt.spriteAsset == null && itemSpriteAsset != null)
                {
                    txt.spriteAsset = itemSpriteAsset;
                }
            }

            pickups.Add(materialName, (amount, entry));
        }
    }

    private string GetTMPSpriteName(Sprite originalSprite)
    {
        if (originalSprite == null || itemSpriteAsset == null)
            return null;

        if (spriteNameMap.TryGetValue(originalSprite.name, out string tmpName))
        {
            return tmpName;
        }

        for (int i = 0; i < itemSpriteAsset.spriteInfoList.Count; i++)
        {
            if (itemSpriteAsset.spriteInfoList[i].name == originalSprite.name)
            {
                return originalSprite.name;
            }
        }

        return null;
    }

    [Serializable]
    public struct GoldReward
    {
        public int goldAmount;
        public string message;
        public Color textColor;
    }

    public void ShowGoldFeedbackSequence(List<GoldReward> rewards, int totalGold)
    {
        if (goldRewardPanel == null || goldEntryPrefab == null)
        {
            return;
        }

        ClearPickups();

        isShowingGoldFeedback = true;
        StartCoroutine(FadeInPanel(pickupPanelCanvasGroup));
        // The last line only starts at GoldLineDelay(lastIndex): it still gets the full display time.
        panelTimer = goldFeedbackDisplayTime + GoldLineDelay(Mathf.Max(rewards.Count, 1) - 1);

        if (rewards.Count == 0)
        {
            ShowGoldFeedbackEntry(noRewardMessage, 0, noRewardColor, 0);
        }
        else
        {
            for (int i = 0; i < rewards.Count; i++)
            {
                ShowGoldFeedbackEntry(rewards[i].message, rewards[i].goldAmount, rewards[i].textColor, i);
            }
        }
    }

    private void ShowGoldFeedbackEntry(string message, int goldAmount, Color textColor, int index)
    {
        var entry = Instantiate(pickupEntryPrefab, goldRewardPanel);
        var txt = entry.GetComponent<TextMeshProUGUI>();

        if (txt != null)
        {
            if (txt.spriteAsset == null && itemSpriteAsset != null)
            {
                txt.spriteAsset = itemSpriteAsset;
            }

            if (goldAmount > 0) txt.fontSize *= 1.1f;
            // In Play Mode AnimateGoldEntry writes gold lines (resolving the format once); otherwise show the final text now.
            if (!Application.isPlaying || goldAmount <= 0) txt.text = FormatGoldEntry(message, goldAmount);
            txt.color = textColor;
        }

        goldFeedbackEntries.Add(entry);

        // Outside Play Mode (or without text) the line simply shows its final state.
        if (txt != null && Application.isPlaying) AnimateGoldEntry(entry, txt, message, goldAmount, textColor, index);
    }

    // Line pops in after the previous one (scale + fade only: the panel's layout group owns the position)
    // while its gold counts up from 0. Unscaled time, because a level-up can pause the game meanwhile.
    private void AnimateGoldEntry(GameObject entry, TextMeshProUGUI txt, string message, int goldAmount, Color textColor, int index)
    {
        Vector3 baseScale = entry.transform.localScale;
        Color hidden = textColor;
        hidden.a = 0f;
        entry.transform.localScale = baseScale * goldLineStartScale;
        txt.color = hidden;
        // Resolved once per line so an invalid designer format warns once, not on every counter step.
        string lineFormat = goldAmount > 0 ? TextFormat.Resolve(goldEntryFormat, DefaultGoldEntryFormat, message, goldAmount) : null;
        if (goldAmount > 0) txt.text = TextFormat.Apply(lineFormat, DefaultGoldEntryFormat, message, 0);

        int shownGold = 0;
        var sequence = DOTween.Sequence().SetUpdate(true).SetLink(entry);
        sequence.AppendInterval(GoldLineDelay(index));
        sequence.AppendCallback(() => { if (goldAmount > 0) PlayGoldTick(); });
        sequence.Append(entry.transform.DOScale(baseScale, goldLineEnterDuration).SetEase(goldLineEnterEase));
        sequence.Join(DOVirtual.Float(0f, 1f, goldLineEnterDuration, progress =>
        {
            if (txt == null) return;
            Color c = textColor;
            c.a = textColor.a * progress;
            txt.color = c;
        }));
        if (goldAmount > 0)
        {
            sequence.Join(DOVirtual.Float(0f, 1f, goldCountDuration, progress =>
            {
                int counted = CountedGold(goldAmount, progress);
                if (txt == null || counted == shownGold) return;
                shownGold = counted;
                txt.text = TextFormat.Apply(lineFormat, DefaultGoldEntryFormat, message, counted);
                PlayGoldTick();
            }).SetEase(Ease.OutQuad));
        }
        sequence.OnComplete(() =>
        {
            if (entry == null || txt == null) return;
            entry.transform.localScale = baseScale;
            txt.color = textColor;
            txt.text = goldAmount > 0 ? TextFormat.Apply(lineFormat, DefaultGoldEntryFormat, message, goldAmount) : message;
        });
        goldSequences.Add(sequence);
    }

    private void PlayGoldTick()
    {
        if (string.IsNullOrEmpty(goldTickSound) || SoundManager.Instance == null) return;
        if (Time.unscaledTime - lastGoldTickTime < goldTickInterval) return;
        lastGoldTickTime = Time.unscaledTime;
        SoundManager.Instance.PlayOneShot(goldTickSound);
    }

    // Seconds after the feedback starts at which reward line [index] begins to appear.
    public float GoldLineDelay(int index) => Mathf.Max(0, index) * Mathf.Max(0f, goldLineStagger);

    // The gold amount shown while counting: 0 at progress 0, the full amount at progress 1.
    public int CountedGold(int goldAmount, float progress) => Mathf.RoundToInt(goldAmount * Mathf.Clamp01(progress));

    // The text of one night reward line: the gold format when it pays gold, otherwise the message alone.
    public string FormatGoldEntry(string message, int goldAmount) =>
        goldAmount > 0 ? FormatGoldCount(message, goldAmount) : message;

    // The gold format with the amount shown so far (also 0, so the counter starts at "+0 gold").
    public string FormatGoldCount(string message, int shownGold) =>
        TextFormat.Apply(goldEntryFormat, DefaultGoldEntryFormat, message, shownGold);

    private void KillGoldSequences()
    {
        foreach (var sequence in goldSequences)
        {
            if (sequence != null && sequence.IsActive()) sequence.Kill();
        }
        goldSequences.Clear();
    }

    private void ClearGoldFeedback()
    {
        KillGoldSequences();
        foreach (var entry in goldFeedbackEntries)
        {
            if (entry != null) Destroy(entry);
        }
        goldFeedbackEntries.Clear();
        isShowingGoldFeedback = false;
        SetPanelAlpha(pickupPanelCanvasGroup, 0f);
    }

    private void ClearPickups()
    {
        foreach (Transform t in pickupPanel)
        {
            if (t.gameObject != null) Destroy(t.gameObject);
        }
        pickups.Clear();

        if (!isShowingGoldFeedback)
        {
            SetPanelAlpha(pickupPanelCanvasGroup, 0f);
        }
    }

    private void SetPanelAlpha(CanvasGroup cg, float alpha)
    {
        if (cg != null)
            cg.alpha = alpha;
    }

    private IEnumerator FadeInPanel(CanvasGroup cg)
    {
        if (cg == null) yield break;
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            cg.alpha = Mathf.Lerp(cg.alpha, 1f, t / fadeDuration);
            yield return null;
        }
        cg.alpha = 1f;
    }

    private IEnumerator FadeOutPanel(CanvasGroup cg, System.Action onComplete)
    {
        if (cg == null) yield break;
        float startAlpha = cg.alpha;
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            cg.alpha = Mathf.Lerp(startAlpha, 0f, t / fadeDuration);
            yield return null;
        }
        cg.alpha = 0f;
        onComplete?.Invoke();
    }
}