using System;

public static class TutorialEvents
{
    public static event Action OnTutorialStarted;
    public static event Action<TutorialStep> OnStepCompleted;
    public static event Action OnTutorialCompleted;
    public static event Action OnPlayerMoved;
    public static event Action OnGroundDug;
    public static event Action OnPlantHarvested;
    public static event Action OnSpellCasted;
    public static event Action OnNightStarted;
    public static event Action OnEnemyDefeated;
    public static event Action OnNightSurvived;
    public static event Action OnInventoryOpened;
    public static event Action OnInventoryClosed;
    public static event Action OnCraftingOpened;
    public static event Action OnRestorationOpened;
    public static event Action OnHouseEntered;
    public static event Action OnHouseExited;
    public static event Action OnRitualAltarUsed;
    public static event Action OnCraftingClosed;
    public static event Action OnRestorationClosed;
    public static event Action OnProductionPlantPlanted;
    public static event Action OnDefensivePlantPlanted;
    public static event Action OnCraftingProximity;
    public static event Action OnRestorationProximity;
    public static event Action OnRitualAltarProximity;
    public static event Action OnFirstPlantReadyToHarvest;
    public static event Action OnSeedTooltipDisplayed;
    public static event Action OnTeleportCasted;
    public static event Action OnAbilityChanged;

    public static event Action OnDailyLoadoutOpened;
    public static event Action OnDailyLoadoutProximity;
    public static void InvokeDailyLoadoutOpened() => OnDailyLoadoutOpened?.Invoke();
    public static void InvokeDailyLoadoutProximity() => OnDailyLoadoutProximity?.Invoke();

    private static bool hasFiredFirstPlantReady = false;

    public static void InvokeTutorialStarted() => OnTutorialStarted?.Invoke();
    public static void InvokeStepCompleted(TutorialStep step) => OnStepCompleted?.Invoke(step);
    public static void InvokeTutorialCompleted() => OnTutorialCompleted?.Invoke();
    public static void InvokeProductionPlantPlanted() => OnProductionPlantPlanted?.Invoke();
    public static void InvokeDefensivePlantPlanted() => OnDefensivePlantPlanted?.Invoke();
    public static void InvokePlayerMoved() => OnPlayerMoved?.Invoke();
    public static void InvokeGroundDug() => OnGroundDug?.Invoke();
    public static void InvokePlantHarvested() => OnPlantHarvested?.Invoke();
    public static void InvokeSpellCasted() => OnSpellCasted?.Invoke();
    public static void InvokeNightStarted() => OnNightStarted?.Invoke();
    public static void InvokeEnemyDefeated() => OnEnemyDefeated?.Invoke();
    public static void InvokeNightSurvived() => OnNightSurvived?.Invoke();
    public static void InvokeInventoryOpened() => OnInventoryOpened?.Invoke();
    public static void InvokeCraftingOpened() => OnCraftingOpened?.Invoke();
    public static void InvokeRestorationOpened() => OnRestorationOpened?.Invoke();
    public static void InvokeCraftingClosed() => OnCraftingClosed?.Invoke();
    public static void InvokeRestorationClosed() => OnRestorationClosed?.Invoke();
    public static void InvokeHouseEntered() => OnHouseEntered?.Invoke();
    public static void InvokeHouseExited() => OnHouseExited?.Invoke();
    public static void InvokeRitualAltarUsed() => OnRitualAltarUsed?.Invoke();
    public static void InvokeCraftingProximity() => OnCraftingProximity?.Invoke();
    public static void InvokeRestorationProximity() => OnRestorationProximity?.Invoke();
    public static void InvokeRitualAltarProximity() => OnRitualAltarProximity?.Invoke();
    public static void InvokeTeleportCasted() => OnTeleportCasted?.Invoke();
    public static void InvokeAbilityChanged() => OnAbilityChanged?.Invoke();
    public static void InvokeSeedTooltipDisplayed() => OnSeedTooltipDisplayed?.Invoke();

    public static void InvokeFirstPlantReadyToHarvest()
    {
        if (hasFiredFirstPlantReady) return;

        OnFirstPlantReadyToHarvest?.Invoke();
        hasFiredFirstPlantReady = true;
    }

    public static void ResetTutorialEventFlags()
    {
        hasFiredFirstPlantReady = false;
    }
}