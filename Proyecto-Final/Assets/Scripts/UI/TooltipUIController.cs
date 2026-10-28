using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TooltipUIController : UIControllerBase
{
    [Header("Tooltip UI")]
    [SerializeField] private GameObject tooltipPanel;
    [SerializeField] private TextMeshProUGUI seedNameText;
    [SerializeField] private TextMeshProUGUI seedDescriptionText;
    [SerializeField] private Image fullyGrownImage;
    [SerializeField] private Vector2 tooltipOffset = new Vector2(0f, 120f);

    [SerializeField] private Canvas rootCanvas;
    private RectTransform canvasRect;
    private RectTransform tooltipRect;

    private bool isTooltipVisible = false;
    private int currentSlotIndex = -1;
    private float showTimer = 0f;
    private float hideTimer = 0f;
    private bool pendingShow = false;
    private bool pendingHide = false;

    private PlayerAbility currentAbilityData;

    private SpellSlot currentSpellData;
    private PlayerAbilitySystem abilitySystem;

    protected override void CacheReferences()
    {
        if (rootCanvas == null) rootCanvas = GetComponentInParent<Canvas>(true);
        if (tooltipPanel != null) tooltipRect = tooltipPanel.GetComponent<RectTransform>();
        if (rootCanvas != null) canvasRect = rootCanvas.GetComponent<RectTransform>();
        abilitySystem = FindObjectOfType<PlayerAbilitySystem>();
    }

    protected override void SetupEventListeners()
    {
        UIEvents.OnTooltipRequested += OnTooltipRequested;
        UIEvents.OnTooltipHideRequested += OnTooltipHideRequested;

        UIEvents.OnAbilityTooltipRequested += OnAbilityTooltipRequested;
        UIEvents.OnAbilityTooltipHideRequested += OnTooltipHideRequested;

        if (GameFlowController.Instance != null)
        {
            GameFlowController.Instance.OnPhaseChanged += OnPhaseChanged;
        }

        UIEvents.OnSpellTooltipRequested += OnSpellTooltipRequested;
        UIEvents.OnSpellTooltipHideRequested += OnTooltipHideRequested;

    }

    protected override void ConfigureInitialState()
    {
        if (tooltipPanel != null)
            tooltipPanel.SetActive(false);

        ResetState();
    }

    private void OnPhaseChanged(GamePhase newPhase)
    {
        if (!CanShowTooltipInPhase(newPhase))
        {
            ForceHide();
        }
    }


    private bool CanShowTooltipInPhase(GamePhase phase)
    {
        return phase == GamePhase.Day || phase == GamePhase.Night;
    }


    public override void HandleUpdate()
    {
        HandleTooltipTimers();

        if (isTooltipVisible)
        {
            UpdateTooltipPosition();
        }
    }

    private void HandleTooltipTimers()
    {
        if (pendingShow)
        {
            showTimer -= Time.deltaTime;
            if (showTimer <= 0f)
            {
                ExecuteShowTooltip();
            }
        }

        if (pendingHide)
        {
            hideTimer -= Time.deltaTime;
            if (hideTimer <= 0f)
            {
                ExecuteHideTooltip();
            }
        }
    }


    private void OnTooltipRequested(int slotIndex)
    {
        if (abilitySystem == null ||
            abilitySystem.CurrentAbility != PlayerAbility.Planting)
        {
            ForceHide();
            return;
        }

        PlantSlot slot = SeedInventory.Instance?.GetPlantSlot(slotIndex);

        if (slot == null || slot.seedCount <= 0 || slot.data == null)
        {
            ForceHide();
            return;
        }

        if (isTooltipVisible && currentSlotIndex == slotIndex)
            return;

        CancelHide();

        if (isTooltipVisible && currentSlotIndex != slotIndex)
        {
            ShowSlotTooltip(slotIndex);
            return;
        }

        ScheduleShow(slotIndex);
    }


    private void OnTooltipHideRequested()
    {
        CancelShow();

        if (isTooltipVisible)
        {
            ScheduleHide();
        }
    }

    private void ScheduleShow(int slotIndex)
    {
        currentSlotIndex = slotIndex;
        currentAbilityData = PlayerAbility.None;
        pendingShow = true;
        showTimer = 0.3f;
    }

    private void ScheduleShowAbility()
    {
        currentSlotIndex = -1;
        pendingShow = true;
        showTimer = 0.3f;
    }

    private void ScheduleHide()
    {
        pendingHide = true;
        hideTimer = 0.2f;
    }

    private void CancelShow()
    {
        if (pendingShow)
        {
            pendingShow = false;
            showTimer = 0f;
        }
    }

    private void CancelHide()
    {
        if (pendingHide)
        {
            pendingHide = false;
            hideTimer = 0f;
        }
    }

    private void ExecuteShowTooltip()
    {
        pendingShow = false;

        if (currentSlotIndex >= 0)
        {
            ShowSlotTooltip(currentSlotIndex);
        }
        else if (currentAbilityData != PlayerAbility.None)
        {
            ShowAbilityTooltip(currentAbilityData);
        }
        else if (currentSpellData != null)
        {
            ShowSpellTooltip(currentSpellData);
        }
    }


    private void ShowAbilityTooltip(PlayerAbility ability)
    {
        if (!ValidateTooltipComponents()) return;

        if (GameFlowController.Instance == null ||
            !CanShowTooltipInPhase(GameFlowController.Instance.CurrentPhase))
        {
            return;
        }

        tooltipPanel.SetActive(true);
        isTooltipVisible = true;

        seedNameText.text = GetAbilityName(ability);
        seedDescriptionText.text = GetAbilityDescription(ability);

        if (fullyGrownImage != null)
        {
            fullyGrownImage.gameObject.SetActive(false);
        }

        UpdateTooltipPosition();
    }

    private void ExecuteHideTooltip()
    {
        pendingHide = false;
        HideTooltip();
    }

    public void ShowSlotTooltip(int slotIndex)
    {
        if (abilitySystem == null || abilitySystem.CurrentAbility != PlayerAbility.Planting)
        {
            HideTooltip();
            return;
        }

        if (!ValidateTooltipComponents())
            return;

        PlantSlot slot = SeedInventory.Instance?.GetPlantSlot(slotIndex);
        if (slot == null || slot.seedCount <= 0)
        {
            HideTooltip();
            return;
        }

        PlantDataSO data = slot.data;
        if (data == null)
        {
            HideTooltip();
            return;
        }

        ShowTooltipWithData(data);
        currentSlotIndex = slotIndex;
    }

    private bool ValidateTooltipComponents()
    {
        return tooltipPanel != null && seedNameText != null && seedDescriptionText != null;
    }

    private void ShowTooltipWithData(PlantDataSO data)
    {
        tooltipPanel.SetActive(true);
        isTooltipVisible = true;

        seedNameText.text = data.plantName;
        seedDescriptionText.text = FormatPlantDescription(data);
        ConfigureTooltipImage(data);

        UpdateTooltipPosition();

        var tm = TutorialManager.Instance;
        if (tm != null && tm.IsTutorialActive() && tm.GetCurrentObjectiveType() == TutorialObjectiveType.SeedTooltipDisplayed)
        {
            TutorialEvents.InvokeSeedTooltipDisplayed();
        }
    }

    private string FormatPlantDescription(PlantDataSO data)
    {
        return $"{data.description}\n<color=#9B59B6>DAYS TO GROW: {data.daysToGrow}</color>";
    }

    private void ConfigureTooltipImage(PlantDataSO data)
    {
        if (fullyGrownImage == null) return;

        if (data.fullyGrownSprite != null)
        {
            fullyGrownImage.sprite = data.fullyGrownSprite;
            fullyGrownImage.preserveAspect = true;
            fullyGrownImage.gameObject.SetActive(true);
        }
        else
        {
            fullyGrownImage.sprite = null;
            fullyGrownImage.gameObject.SetActive(false);
        }
    }

    public void HideTooltip()
    {
        if (tooltipPanel != null)
        {
            tooltipPanel.SetActive(false);
        }

        ResetState();
    }

    private void ResetState()
    {
        isTooltipVisible = false;
        currentSlotIndex = -1;
        currentAbilityData = PlayerAbility.None;
        CancelShow();
        CancelHide();
    }

    private void UpdateTooltipPosition()
    {
        if (!isTooltipVisible || tooltipRect == null || canvasRect == null) return;

        var cam = (rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            ? rootCanvas.worldCamera
            : null;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, Input.mousePosition, cam, out Vector2 localMouse))
        {
            Vector2 target = localMouse + tooltipOffset;

            float margin = 10f;
            float halfHeight = tooltipRect.rect.height * (1f - tooltipRect.pivot.y);
            if (target.y + halfHeight + margin > canvasRect.rect.yMax)
            {
                target.y = localMouse.y - Mathf.Abs(tooltipOffset.y);
            }

            tooltipRect.anchoredPosition = ClampToCanvas(target, tooltipRect, canvasRect, margin);
        }
    }

    private static Vector2 ClampToCanvas(
        Vector2 anchored, RectTransform tooltip, RectTransform canvas, float margin)
    {
        Rect c = canvas.rect;
        Vector2 size = tooltip.rect.size;
        Vector2 pivot = tooltip.pivot;

        float minX = c.xMin + size.x * pivot.x + margin;
        float maxX = c.xMax - size.x * (1f - pivot.x) - margin;
        float minY = c.yMin + size.y * pivot.y + margin;
        float maxY = c.yMax - size.y * (1f - pivot.y) - margin;

        anchored.x = Mathf.Clamp(anchored.x, minX, maxX);
        anchored.y = Mathf.Clamp(anchored.y, minY, maxY);
        return anchored;
    }

    public void ForceHide()
    {
        CancelShow();
        CancelHide();
        HideTooltip();
    }

    private void OnAbilityTooltipRequested(PlayerAbility ability)
    {
        CancelHide();
        currentAbilityData = ability;
        currentSlotIndex = -1;

        if (isTooltipVisible)
        {
            ShowAbilityTooltip(ability);
        }
        else
        {
            ScheduleShowAbility();
        }
    }

    

    private string GetAbilityName(PlayerAbility ability)
    {
        return ability switch
        {
            PlayerAbility.Planting => "PLANT",
            PlayerAbility.Harvesting => "HARVEST",
            PlayerAbility.Digging => "DIG",
            PlayerAbility.Removing => "REMOVE",
            _ => "UNKNOWN"
        };
    }

    private string GetAbilityDescription(PlayerAbility ability)
    {
        return ability switch
        {
            PlayerAbility.Planting => "Plant seeds in prepared soil.\n<color=#9B59B6>Select a seed from inventory first</color>",
            PlayerAbility.Harvesting => "Harvest mature plants to obtain resources.\n<color=#9B59B6>Only available during daytime</color>",
            PlayerAbility.Digging => "Prepare the soil for planting.\n<color=#9B59B6>Click and hold to dig</color>",
            PlayerAbility.Removing => "Remove unwanted plants.\n<color=#9B59B6>Recover some seeds when removing</color>",
            _ => "Unknown ability"
        };
    }

    private void ShowSpellTooltip(SpellSlot spell)
    {
        if (!ValidateTooltipComponents()) return;
        if (spell == null || !spell.isUnlocked)
        {
            HideTooltip();
            return;
        }

        tooltipPanel.SetActive(true);
        isTooltipVisible = true;

        seedNameText.text = spell.spellName;
        seedDescriptionText.text = GetSpellDescription(spell);

        if (fullyGrownImage != null)
        {
            if (spell.spellIcon != null)
            {
                fullyGrownImage.sprite = spell.spellIcon;
                fullyGrownImage.gameObject.SetActive(true);
            }
            else
            {
                fullyGrownImage.gameObject.SetActive(false);
            }
        }

        UpdateTooltipPosition();
    }






    private void OnSpellTooltipRequested(SpellSlot spell)
    {
        if (spell == null) return;

        CancelHide();

        currentSpellData = spell;
        currentSlotIndex = -1;
        currentAbilityData = PlayerAbility.None;

        if (isTooltipVisible)
        {
            ShowSpellTooltip(spell);
        }
        else
        {
            pendingShow = true;
            showTimer = 0.3f;
        }
    }

    private string GetSpellDescription(SpellSlot spell)
    {
        if (spell == null) return "Unknown spell";

        return spell.spellName switch
        {
            "Range" =>
                "Shoot a fast fire projectile.\n" +
                "<color=#9B59B6>Deals damage at long range</color>\n" +
                $"<color=#3498DB>Mana Cost: {spell.manaCost}</color>",

            "Melee" =>
                "Perform a close-range sword attack.\n" +
                "<color=#9B59B6>Deals big damage to enemies hit</color>\n" +
                $"<color=#3498DB>Mana Cost: {spell.manaCost}</color>",

            "Area" =>
                "Release an explosive projectile infront of you to deal big damage.\n" +
                "<color=#9B59B6>Big area damage</color>\n" +
                $"<color=#3498DB>Mana Cost: {spell.manaCost}</color>",

            _ =>
                "An unknown spell.\n" +
                $"<color=#3498DB>Mana Cost: {spell.manaCost}</color>",
        };
    }


    protected override void CleanupEventListeners()
    {
        UIEvents.OnTooltipRequested -= OnTooltipRequested;
        UIEvents.OnTooltipHideRequested -= OnTooltipHideRequested;
        UIEvents.OnAbilityTooltipRequested -= OnAbilityTooltipRequested;
        UIEvents.OnAbilityTooltipHideRequested -= OnTooltipHideRequested;
        if (GameFlowController.Instance != null)
        {
            GameFlowController.Instance.OnPhaseChanged -= OnPhaseChanged;
        }
        UIEvents.OnSpellTooltipRequested -= OnSpellTooltipRequested;
        UIEvents.OnSpellTooltipHideRequested -= OnTooltipHideRequested;

    }

    protected override void OnDestroy()
    {
        ForceHide();
        base.OnDestroy();
    }
}
