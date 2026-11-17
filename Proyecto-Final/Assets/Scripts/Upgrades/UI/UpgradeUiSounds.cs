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
}
