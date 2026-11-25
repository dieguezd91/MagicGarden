using MagicGarden.LoadoutUI;
using UnityEngine.UI;

// Sounds for the level-up modals. Their buttons are plain Buttons (no ImprovedUIButton), so hover and click are added here.
public static class UpgradeUiSounds
{
    public static void AddHover(Button button)
    {
        if (button != null && button.GetComponent<LoadoutButtonHoverSound>() == null)
            button.gameObject.AddComponent<LoadoutButtonHoverSound>();
    }

    public static void Click() => UIManager.Instance?.InterfaceSounds?.PlaySound(InterfaceSoundType.MenuButtonClick);

    // Branches growing out / folding back, played with the panel's open and close tween.
    public static void PanelOpen() => UIManager.Instance?.InterfaceSounds?.PlaySound(InterfaceSoundType.LevelUpOpen);
    public static void PanelClose() => UIManager.Instance?.InterfaceSounds?.PlaySound(InterfaceSoundType.LevelUpClose);

    // Reward screen: opens, each reward row pops in, closes.
    public static void RewardOpen() => UIManager.Instance?.InterfaceSounds?.PlaySound(InterfaceSoundType.LevelUpRewardOpen);
    public static void RewardItem() => UIManager.Instance?.InterfaceSounds?.PlaySound(InterfaceSoundType.LevelUpRewardItem);
    public static void RewardClose() => UIManager.Instance?.InterfaceSounds?.PlaySound(InterfaceSoundType.LevelUpRewardClose);
}
