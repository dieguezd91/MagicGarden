using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.TextCore;

public class FloatingTextController : MonoBehaviour
{
    [Header("Shared UI Panel")]
    [SerializeField] private Transform pickupPanel;
    [SerializeField] private GameObject pickupEntryPrefab;
    [SerializeField] private TMP_SpriteAsset itemSpriteAsset;
    [Tooltip("Seconds pickup lines remain visible after the latest animation settles.")]
    [SerializeField] private float pickupDisplayTime = 1.5f;
    [Tooltip("Seconds the shared feedback panel takes to fade in or out.")]
    [SerializeField] private float fadeDuration = 0.35f;
    [Tooltip("Resting pickup text color, restored after each highlight flash.")]
    [SerializeField] private Color warningColor;
    [SerializeField] private Color defaultColor = Color.white;

    [Header("Sprite Name Mapping")]
    [SerializeField] private List<SpriteMapping> spriteMappings = new List<SpriteMapping>();

    [Header("Pickup Icon Fit (pickup-only, not shop or gold)")]
    [Tooltip("TMP sprite whose transparent border is removed in a private pickup copy. Empty disables trimming. Applied on the first pickup of a run.")]
    [SerializeField] private string trimmedPickupSprite = "InventorySpriteSheet_6";
    [Tooltip("Transparent source pixels to trim: left, bottom, right, top. Mana defaults are measured from its artwork; the shared sprite asset stays unchanged.")]
    [SerializeField] private Vector4 pickupIconPadding = new Vector4(3f, 4f, 4f, 3f);
    [Tooltip("Space between an icon and +amount, in font em units. Does not change the gap between different resources.")]
    [SerializeField, Min(0f)] private float pickupIconTextGap = 0.08f;

    [Header("Pickup Feel (unscaled time)")]
    [Tooltip("Scale a new pickup line starts from before it pops to its normal size (1 = no pop)")]
    [SerializeField, Min(0f)] private float pickupPopStartScale = 0.5f;
    [Tooltip("Seconds a new pickup line takes to pop in")]
    [SerializeField, Min(0f)] private float pickupPopDuration = 0.3f;
    [Tooltip("Easing of the new line pop. OutBack overshoots, which gives the bounce")]
    [SerializeField] private Ease pickupPopEase = Ease.OutBack;
    [Tooltip("How much a repeated pickup punches the line's scale (0.25 = grows 25% and returns). 0 = no punch")]
    [SerializeField, Min(0f)] private float pickupPunchScale = 0.25f;
    [Tooltip("Seconds the scale punch of a repeated pickup lasts")]
    [SerializeField, Min(0f)] private float pickupPunchDuration = 0.25f;
    [Tooltip("How many times the punch oscillates before settling")]
    [SerializeField, Min(0)] private int pickupPunchVibrato = 2;
    [Tooltip("How much the punch overshoots on its way back (0 = none, 1 = a lot)")]
    [SerializeField, Range(0f, 1f)] private float pickupPunchElasticity = 0.5f;
    [Tooltip("Seconds the number takes to count from the previous total to the new one")]
    [SerializeField, Min(0f)] private float pickupCountDuration = 0.5f;
    [Tooltip("Color the line flashes with when a pickup arrives; it then fades back to the warning color")]
    [SerializeField] private Color pickupHighlightColor = new Color(1f, 0.92f, 0.45f);
    [Tooltip("Seconds the flash takes to fade back to the normal color")]
    [SerializeField, Min(0f)] private float pickupFlashDuration = 0.35f;
    [Tooltip("Name of the SoundManager sound played once per pickup. Empty = no sound")]
    [SerializeField] private string pickupSound = "PickUp";
    [Tooltip("Minimum seconds between two pickup sounds; a pickup inside this window plays nothing")]
    [SerializeField, Min(0f)] private float pickupSoundInterval = 0.08f;

    [Header("Gold Feedback")]
    [SerializeField] private Transform goldRewardPanel;
    [SerializeField] private GameObject goldEntryPrefab;
    [Tooltip("Seconds to hold after all gold lines settle.")]
    [SerializeField, Min(0f)] private float goldFeedbackDisplayTime = 2f;
    [Tooltip("Seconds the actual gold panel takes to fade after the hold.")]
    [SerializeField, Min(0f)] private float goldFadeDuration = 0.35f;
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
    [Tooltip("Reading gap after entry and count finish, before the next line.")]
    [SerializeField, Min(0f)] private float goldLineStagger = 0.3f;
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
    private bool waitingForSettle;
    private Coroutine panelFade;
    private CanvasGroup goldPanelCanvasGroup;
    private Dictionary<string, PickupLine> pickups = new();
    private Dictionary<Transform, FloatingDamageText> damageTexts = new();
    private CanvasGroup pickupPanelCanvasGroup;
    private TMP_SpriteAsset pickupSpriteAsset;
    private Dictionary<string, string> spriteNameMap;

    private List<GameObject> goldFeedbackEntries = new List<GameObject>();
    private bool isShowingGoldFeedback = false;
    private readonly List<Sequence> goldSequences = new List<Sequence>();
    private float lastGoldTickTime = float.NegativeInfinity;
    private float lastPickupSoundTime = float.NegativeInfinity;

    // One material line of the pickup panel: its totals and the tween currently animating it.
    private class PickupLine
    {
        public GameObject entry;
        public TextMeshProUGUI text;
        public Vector3 baseScale;
        public int amount;
        public int shown;
        public Sequence sequence;
    }

    [Serializable]
    public class SpriteMapping
    {
        public string originalSpriteName;
        public string tmpSpriteName;
    }

    // Includes count-up, reading hold and fade; disabled feedback never holds the night flow.
    public bool IsShowingGoldFeedback => isActiveAndEnabled && isShowingGoldFeedback;

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
        CancelPanelFade();
        KillGoldSequences();
        KillPickupSequences();
        if (pickupSpriteAsset != null)
        {
            if (Application.isPlaying) Destroy(pickupSpriteAsset);
            else DestroyImmediate(pickupSpriteAsset);
            pickupSpriteAsset = null;
        }
        if (Instance == this) Instance = null;
    }

    private void OnDisable()
    {
        CancelPanelFade();
        panelTimer = 0f;
        waitingForSettle = false;
        ClearGoldFeedback();
        ClearPickups();
        lastPickupSoundTime = lastGoldTickTime = float.NegativeInfinity;
    }

    private void CancelPanelFade()
    {
        if (panelFade != null) StopCoroutine(panelFade);
        panelFade = null;
    }

    private bool AnimationsAreRunning()
    {
        if (isShowingGoldFeedback)
        {
            foreach (var sequence in goldSequences)
                if (sequence != null && sequence.IsActive() && !sequence.IsComplete()) return true;
        }
        else
        {
            foreach (var line in pickups.Values)
                if (line.sequence != null && line.sequence.IsActive() && !line.sequence.IsComplete()) return true;
        }
        return false;
    }

    void Update()
    {
        if (waitingForSettle)
        {
            if (AnimationsAreRunning()) return;
            waitingForSettle = false;
            panelTimer = Mathf.Max(0f, isShowingGoldFeedback ? goldFeedbackDisplayTime : pickupDisplayTime);
            if (panelTimer > 0f) return;
        }
        else if (panelTimer <= 0f) return;
        panelTimer -= Time.unscaledDeltaTime;
        if (panelTimer <= 0f)
        {
            CancelPanelFade();
            panelFade = StartCoroutine(FadeOutPanel(
                isShowingGoldFeedback ? goldPanelCanvasGroup : pickupPanelCanvasGroup,
                isShowingGoldFeedback ? (Action)ClearGoldFeedback : ClearPickups));
        }
    }

    public void ShowPickup(string materialName, int amount, Sprite icon)
    {
        PlayPickupSound();
        if (isShowingGoldFeedback) return;
        if (pickupPanel == null || pickupEntryPrefab == null) return;

        CancelPanelFade();
        panelFade = StartCoroutine(FadeInPanel(pickupPanelCanvasGroup));
        panelTimer = 0f;
        waitingForSettle = true;

        string tmpSpriteName = GetTMPSpriteName(icon);
        string spriteTag = !string.IsNullOrEmpty(tmpSpriteName)
            ? $"<sprite name=\"{tmpSpriteName}\"><space={Mathf.Max(0f, pickupIconTextGap).ToString("0.###", CultureInfo.InvariantCulture)}em>" : "";

        if (pickups.TryGetValue(materialName, out var line) && line.entry != null)
        {
            int from = line.shown;
            line.amount += amount;
            AnimatePickupLine(line, spriteTag, materialName, from, false);
        }
        else
        {
            var entry = Instantiate(pickupEntryPrefab, pickupPanel);
            var txt = entry.GetComponent<TextMeshProUGUI>();

            if (txt != null && itemSpriteAsset != null && (txt.spriteAsset == null || txt.spriteAsset == itemSpriteAsset))
            {
                txt.spriteAsset = GetPickupSpriteAsset();
            }

            line = new PickupLine { entry = entry, text = txt, baseScale = entry.transform.localScale, amount = amount };
            pickups[materialName] = line;
            AnimatePickupLine(line, spriteTag, materialName, 0, true);
        }
    }

    // New line: pops in and flashes. Repeat: punches its scale, counts from the amount shown to the new
    // total and flashes. Scale/color only (the panel's layout group owns the position), unscaled time.
    // Outside Play Mode the line just shows its final state.
    private void AnimatePickupLine(PickupLine line, string spriteTag, string materialName, int from, bool isNew)
    {
        KillPickupSequence(line);
        int total = line.amount;
        Transform t = line.entry.transform;
        var txt = line.text;
        t.localScale = line.baseScale;
        line.shown = total;
        if (txt != null)
        {
            txt.color = warningColor;
            txt.text = FormatPickupLine(spriteTag, total, materialName);
        }
        if (!Application.isPlaying || txt == null) return;

        line.shown = from;
        txt.text = FormatPickupLine(spriteTag, from, materialName);
        txt.color = pickupHighlightColor;

        var sequence = DOTween.Sequence().SetUpdate(true).SetLink(line.entry);
        if (isNew)
        {
            t.localScale = line.baseScale * pickupPopStartScale;
            sequence.Append(t.DOScale(line.baseScale, pickupPopDuration).SetEase(pickupPopEase));
        }
        else
        {
            sequence.Append(t.DOPunchScale(line.baseScale * pickupPunchScale, pickupPunchDuration, pickupPunchVibrato, pickupPunchElasticity));
        }
        sequence.Join(DOVirtual.Float(0f, 1f, pickupCountDuration, progress =>
            {
                int counted = CountedValue(from, total, progress);
                if (txt == null || counted == line.shown) return;
                line.shown = counted;
                txt.text = FormatPickupLine(spriteTag, counted, materialName);
            }).SetEase(Ease.OutQuad));
        sequence.Join(DOVirtual.Float(0f, 1f, pickupFlashDuration, progress =>
        {
            if (txt != null) txt.color = Color.Lerp(pickupHighlightColor, warningColor, progress);
        }));
        sequence.OnComplete(() =>
        {
            if (line.entry == null || txt == null) return;
            t.localScale = line.baseScale;
            txt.color = warningColor;
            line.shown = total;
            txt.text = FormatPickupLine(spriteTag, total, materialName);
        });
        line.sequence = sequence;
    }

    private void PlayPickupSound()
    {
        if (!Application.isPlaying || string.IsNullOrEmpty(pickupSound) || SoundManager.Instance == null) return;
        if (Time.unscaledTime - lastPickupSoundTime < pickupSoundInterval) return;
        lastPickupSoundTime = Time.unscaledTime;
        SoundManager.Instance.PlayOneShot(pickupSound);
    }

    // TMP's legacy sprite scaling fits the entire glyph height, including transparent pixels.
    // Trim only a private pickup glyph so mana's visible artwork fits like the other icons, without changing shop/HUD assets.
    private TMP_SpriteAsset GetPickupSpriteAsset()
    {
        if (pickupSpriteAsset != null) return pickupSpriteAsset;
        if (itemSpriteAsset == null || string.IsNullOrEmpty(trimmedPickupSprite)) return itemSpriteAsset;
        int index = itemSpriteAsset.GetSpriteIndexFromName(trimmedPickupSprite);
        if (index < 0) return itemSpriteAsset;
        var source = itemSpriteAsset.spriteCharacterTable[index].glyph;
        var rect = source.glyphRect;
        int left = Mathf.Max(0, Mathf.RoundToInt(pickupIconPadding.x));
        int bottom = Mathf.Max(0, Mathf.RoundToInt(pickupIconPadding.y));
        int right = Mathf.Max(0, Mathf.RoundToInt(pickupIconPadding.z));
        int top = Mathf.Max(0, Mathf.RoundToInt(pickupIconPadding.w));
        int width = rect.width - left - right, height = rect.height - bottom - top;
        if (width <= 0 || height <= 0 || rect.width <= 0 || rect.height <= 0) return itemSpriteAsset;

        pickupSpriteAsset = Instantiate(itemSpriteAsset);
        pickupSpriteAsset.name = itemSpriteAsset.name + " (Pickup)";
        pickupSpriteAsset.hideFlags = HideFlags.DontSave;
        var glyph = pickupSpriteAsset.spriteGlyphTable.Find(g => g.index == source.index);
        var metrics = source.metrics;
        float widthRatio = (float)width / rect.width, heightRatio = (float)height / rect.height;
        glyph.glyphRect = new GlyphRect(rect.x + left, rect.y + bottom, width, height);
        glyph.metrics = new GlyphMetrics(metrics.width * widthRatio, metrics.height * heightRatio,
            metrics.horizontalBearingX * widthRatio, metrics.horizontalBearingY * heightRatio,
            metrics.horizontalAdvance * widthRatio);
        pickupSpriteAsset.UpdateLookupTables();
        return pickupSpriteAsset;
    }

    // The number shown while counting from [from] to [to]: [from] at progress 0, [to] at progress 1.
    public int CountedValue(int from, int to, float progress) => Mathf.RoundToInt(Mathf.Lerp(from, to, Mathf.Clamp01(progress)));

    // One resource fragment: spriteTag is empty or an inline icon with its configured compact spacing.
    public string FormatPickupLine(string spriteTag, int amount, string materialName) => $"{spriteTag}+{amount} {materialName}";

    private void KillPickupSequence(PickupLine line)
    {
        if (line.sequence != null && line.sequence.IsActive()) line.sequence.Kill();
        line.sequence = null;
    }

    private void KillPickupSequences()
    {
        foreach (var line in pickups.Values) KillPickupSequence(line);
    }

    private string GetTMPSpriteName(Sprite originalSprite) =>
        originalSprite == null ? null : ResolveSpriteName(originalSprite.name);

    // The TMP sprite name for a Sprite name: an explicit mapping wins, otherwise the name itself when the
    // sprite asset has a sprite with it (TMP_SpriteAsset.spriteInfoList is legacy and empty), otherwise null.
    public string ResolveSpriteName(string originalSpriteName)
    {
        if (string.IsNullOrEmpty(originalSpriteName) || itemSpriteAsset == null)
            return null;

        if (spriteNameMap == null) InitializeSpriteMapping();

        if (spriteNameMap.TryGetValue(originalSpriteName, out string tmpName))
        {
            return tmpName;
        }

        return itemSpriteAsset.GetSpriteIndexFromName(originalSpriteName) >= 0 ? originalSpriteName : null;
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

        CancelPanelFade();
        ClearGoldFeedback();
        ClearPickups();
        isShowingGoldFeedback = true;
        if (goldPanelCanvasGroup == null)
        {
            goldPanelCanvasGroup = goldRewardPanel.GetComponent<CanvasGroup>();
            if (goldPanelCanvasGroup == null) goldPanelCanvasGroup = goldRewardPanel.gameObject.AddComponent<CanvasGroup>();
        }
        SetPanelAlpha(goldPanelCanvasGroup, 1f);
        panelTimer = 0f;
        waitingForSettle = true;

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
    // then its gold counts up from 0. Unscaled time, because a level-up can pause the game meanwhile.
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
            sequence.Append(DOVirtual.Float(0f, 1f, goldCountDuration, progress =>
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
    public float GoldLineDelay(int index) => Mathf.Max(0, index) *
        (Mathf.Max(0f, goldLineEnterDuration) + Mathf.Max(0f, goldCountDuration) + Mathf.Max(0f, goldLineStagger));

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
        SetPanelAlpha(goldPanelCanvasGroup, 0f);
    }

    private void ClearPickups()
    {
        KillPickupSequences();
        if (pickupPanel != null)
        {
            foreach (Transform t in pickupPanel)
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
            t += Time.unscaledDeltaTime;
            cg.alpha = Mathf.Lerp(cg.alpha, 1f, t / fadeDuration);
            yield return null;
        }
        cg.alpha = 1f;
    }

    private IEnumerator FadeOutPanel(CanvasGroup cg, System.Action onComplete)
    {
        float duration = Mathf.Max(0f, isShowingGoldFeedback ? goldFadeDuration : fadeDuration);
        if (cg == null)
        {
            onComplete?.Invoke();
            yield break;
        }
        float startAlpha = cg.alpha;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            cg.alpha = Mathf.Lerp(startAlpha, 0f, t / duration);
            yield return null;
        }
        cg.alpha = 0f;
        onComplete?.Invoke();
    }
}