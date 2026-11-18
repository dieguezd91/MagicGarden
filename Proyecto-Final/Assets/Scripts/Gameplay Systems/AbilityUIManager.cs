using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class AbilityUIManager : MonoBehaviour
{
    [Header("Ability Slots")]
    [SerializeField] private AbilitySlot[] abilitySlots = new AbilitySlot[4];

    private PlayerAbilitySystem playerAbilitySystem;

    void Start()
    {
        playerAbilitySystem = FindObjectOfType<PlayerAbilitySystem>();

        if (playerAbilitySystem == null)
        {
            Debug.LogError("PlayerAbilitySystem not found!");
            return;
        }

        playerAbilitySystem.OnAbilityChanged += OnAbilityChanged;

        InitializeAbilitySlots();
        UpdateAllSlotVisuals();
    }

    private void OnDestroy()
    {
        CleanupEventListeners();
    }

    private void InitializeAbilitySlots()
    {
        foreach (var slot in abilitySlots)
        {
            if (slot == null) continue;

            slot.Initialize();

            slot.OnAbilitySelected += OnAbilitySlotSelected;

            slot.OnAbilityHovered += OnAbilitySlotHovered;
            slot.OnAbilityUnhovered += OnAbilitySlotUnhovered;
        }
    }

    private void OnAbilitySlotSelected(PlayerAbility selectedAbility)
    {
        // HUD clicks switch tools like Q/E/scroll, so they follow the same tutorial lock.
        if (!TutorialManager.Allows(TutorialAction.CycleTool)) return;
        if (playerAbilitySystem != null)
        {
            playerAbilitySystem.SetAbility(selectedAbility);
        }
    }

    private void OnAbilityChanged(PlayerAbility newAbility)
    {
        UpdateAllSlotVisuals();
        UIManager.Instance?.InterfaceSounds?.PlaySound(InterfaceSoundType.OnAbilityChanged);
    }

    private void UpdateAllSlotVisuals()
    {
        bool isDaytime = GameFlowController.Instance?.CurrentPhase != GamePhase.Night;
        PlayerAbility currentAbility = playerAbilitySystem?.CurrentAbility ?? PlayerAbility.Planting;
        
        foreach (var slot in abilitySlots)
        {
            if (slot != null)
            {
                slot.RefreshVisualState(currentAbility, isDaytime);
            }
        }
    }

    private void OnAbilitySlotHovered(PlayerAbility ability)
    {
        UIEvents.TriggerAbilityTooltipRequested(ability);
    }

    private void OnAbilitySlotUnhovered(PlayerAbility ability)
    {
        UIEvents.TriggerAbilityTooltipHide();
    }


    private void CleanupEventListeners()
    {
        if (playerAbilitySystem != null)
        {
            playerAbilitySystem.OnAbilityChanged -= OnAbilityChanged;
        }

        foreach (var slot in abilitySlots)
        {
            if (slot != null)
            {
                slot.OnAbilitySelected -= OnAbilitySlotSelected;
                slot.OnAbilityHovered -= OnAbilitySlotHovered;
                slot.OnAbilityUnhovered -= OnAbilitySlotUnhovered;
            }
        }
    }

    public AbilitySlot GetSlotForAbility(PlayerAbility ability)
    {
        foreach (var slot in abilitySlots)
        {
            if (slot != null && slot.AbilityType == ability)
            {
                return slot;
            }
        }
        return null;
    }

    public void RefreshSlotVisuals()
    {
        UpdateAllSlotVisuals();
    }
}
