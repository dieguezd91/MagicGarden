using UnityEngine;
using System.Collections;

public class SpellSlotsUIController : UIControllerBase
{
    [Header("Spell Slots")]
    [SerializeField] private SpellSlotUI[] spellSlotUIs = new SpellSlotUI[7];
    [SerializeField] private CanvasGroup spellSlotsCanvasGroup;

    [Header("Visibility Settings")]
    [SerializeField] private float fadeDuration = 0.3f;

    private GamePhase lastPhase;

    protected override void SetupEventListeners()
    {
        if (SpellInventory.Instance != null)
        {
            SpellInventory.Instance.onSpellSlotSelected += UpdateSelectedSlotUI;
            SpellInventory.Instance.onCooldownUpdated += UpdateSlotCooldown;
            SpellInventory.Instance.OnSpellInventoryChanged += RefreshAllSlots;
        }

        if (LevelManager.Instance != null)
        {
            GameFlowController.Instance.OnPhaseChanged += OnPhaseChanged;
        }
    }

    protected override void ConfigureInitialState()
    {
        InitializeSlots();

        if (spellSlotsCanvasGroup != null)
        {
            spellSlotsCanvasGroup.alpha = 0f;
            spellSlotsCanvasGroup.interactable = false;
            spellSlotsCanvasGroup.blocksRaycasts = false;
        }
    }

    public override void HandleUpdate()
    {
        if (LevelManager.Instance != null &&
            GameFlowController.Instance.CurrentPhase == GamePhase.Night)
        {
            UpdateAllCooldownDisplays();
        }
    }

    private void InitializeSlots()
    {
        for (int i = 0; i < spellSlotUIs.Length; i++)
        {
            if (spellSlotUIs[i] == null) continue;

            spellSlotUIs[i].Initialize(i);
            spellSlotUIs[i].OnSlotClicked += OnSlotClicked;
            UpdateSlotDisplay(i);
        }

        if (SpellInventory.Instance != null)
        {
            UpdateSelectedSlotUI(SpellInventory.Instance.GetSelectedSlotIndex());
        }
    }

    private void OnPhaseChanged(GamePhase newPhase)
    {
        bool shouldShow = newPhase == GamePhase.Night;

        if (shouldShow && spellSlotsCanvasGroup != null)
        {
            ShowSpellSlots();
        }
        else if (!shouldShow && spellSlotsCanvasGroup != null)
        {
            HideSpellSlots();
        }

        lastPhase = newPhase;
    }

    private void ShowSpellSlots()
    {
        if (spellSlotsCanvasGroup != null)
        {
            spellSlotsCanvasGroup.interactable = true;
            spellSlotsCanvasGroup.blocksRaycasts = true;
        }

        if (gameObject.activeInHierarchy)
        {
            StartCoroutine(FadeCanvasGroup(1f));
        }
        else
        {
            if (spellSlotsCanvasGroup != null)
            {
                spellSlotsCanvasGroup.alpha = 1f;
            }
        }
    }

    private void HideSpellSlots()
    {
        if (spellSlotsCanvasGroup != null)
        {
            spellSlotsCanvasGroup.interactable = false;
            spellSlotsCanvasGroup.blocksRaycasts = false;
        }

        if (gameObject.activeInHierarchy)
        {
            StartCoroutine(FadeCanvasGroup(0f));
        }
        else
        {
            if (spellSlotsCanvasGroup != null)
            {
                spellSlotsCanvasGroup.alpha = 0f;
            }
        }
    }

    private IEnumerator FadeCanvasGroup(float targetAlpha)
    {
        if (spellSlotsCanvasGroup == null) yield break;

        float startAlpha = spellSlotsCanvasGroup.alpha;
        float time = 0f;

        while (time < fadeDuration)
        {
            time += Time.deltaTime;
            float t = time / fadeDuration;
            spellSlotsCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
            yield return null;
        }

        spellSlotsCanvasGroup.alpha = targetAlpha;
    }

    private void OnSlotClicked(int slotIndex)
    {
        if (SpellInventory.Instance != null)
        {
            SpellInventory.Instance.SelectSlot(slotIndex);
        }

        UIManager.Instance.InterfaceSounds?.PlaySound(InterfaceSoundType.MenuButtonHover);
    }

    private void UpdateSelectedSlotUI(int slotIndex)
    {
        for (int i = 0; i < spellSlotUIs.Length; i++)
        {
            if (spellSlotUIs[i] != null)
            {
                spellSlotUIs[i].SetSelected(i == slotIndex);
            }
        }
    }

    private void UpdateSlotDisplay(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= spellSlotUIs.Length) return;
        if (spellSlotUIs[slotIndex] == null) return;

        SpellSlot spellSlotData = SpellInventory.Instance?.GetSpellSlot(slotIndex);
        spellSlotUIs[slotIndex].UpdateSlotDisplay(spellSlotData);
    }

    private void UpdateSlotCooldown(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= spellSlotUIs.Length) return;
        if (spellSlotUIs[slotIndex] == null) return;

        SpellSlot spellSlotData = SpellInventory.Instance?.GetSpellSlot(slotIndex);
        spellSlotUIs[slotIndex].UpdateCooldownDisplay(spellSlotData);
    }

    private void UpdateAllCooldownDisplays()
    {
        if (SpellInventory.Instance == null) return;

        for (int i = 0; i < spellSlotUIs.Length; i++)
        {
            if (spellSlotUIs[i] == null) continue;

            SpellSlot spellSlotData = SpellInventory.Instance.GetSpellSlot(i);
            if (spellSlotData != null && spellSlotData.isUnlocked && spellSlotData.currentCooldown > 0f)
            {
                spellSlotUIs[i].UpdateCooldownDisplay(spellSlotData);
            }
        }
    }

    public void RefreshAllSlots()
    {
        for (int i = 0; i < spellSlotUIs.Length; i++)
        {
            UpdateSlotDisplay(i);
        }
    }

    protected override void CleanupEventListeners()
    {
        if (SpellInventory.Instance != null)
        {
            SpellInventory.Instance.onSpellSlotSelected -= UpdateSelectedSlotUI;
            SpellInventory.Instance.onCooldownUpdated -= UpdateSlotCooldown;
            SpellInventory.Instance.OnSpellInventoryChanged -= RefreshAllSlots;
        }

        if (LevelManager.Instance != null)
        {
            GameFlowController.Instance.OnPhaseChanged -= OnPhaseChanged;
        }

        for (int i = 0; i < spellSlotUIs.Length; i++)
        {
            if (spellSlotUIs[i] != null)
            {
                spellSlotUIs[i].OnSlotClicked -= OnSlotClicked;
            }
        }
    }

    protected override void CacheReferences() { }
}
