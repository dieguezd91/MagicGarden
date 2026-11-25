using System.Collections.Generic;
using UnityEngine;

public class InterfaceSoundBase : MonoBehaviour
{
    [Header("Sound Effects")]
    public SoundClipData menuButtonHover;
    public SoundClipData menuButtonDisabledHover;
    public SoundClipData menuButtonClick;
    public SoundClipData menuButtonDisabledClick;

    [Header("Game Interface Sounds")]
    public SoundClipData gameInventoryBookOpen;
    public SoundClipData gameInventoryBookClose;
    public SoundClipData gameInventoryBookPageFlip;
    [Tooltip("A book tab for the page that is already open: the page lifts and flops back.")]
    public SoundClipData gameInventoryBookSamePage;

    public SoundClipData onAbilityChanged;
    public SoundClipData onSeedSelect;

    [Header("Level Up Panel")]
    public SoundClipData levelUpOpen;
    public SoundClipData levelUpClose;

    [Header("Level Up Reward Screen")]
    public SoundClipData levelUpRewardOpen;
    [Tooltip("Each reward row (new seeds, new spells, plant limit) as it pops in.")]
    public SoundClipData levelUpRewardItem;
    public SoundClipData levelUpRewardClose;

    private Dictionary<InterfaceSoundType, SoundClipData> soundDictionary;

    public void Initialize()
    {
        soundDictionary = new Dictionary<InterfaceSoundType, SoundClipData>
        {
            { InterfaceSoundType.MenuButtonHover, menuButtonHover },
            { InterfaceSoundType.MenuButtonDisabledHover, menuButtonDisabledHover },
            { InterfaceSoundType.MenuButtonClick, menuButtonClick },
            { InterfaceSoundType.MenuButtonDisabledClick, menuButtonDisabledClick },
            { InterfaceSoundType.GameInventoryBookOpen, gameInventoryBookOpen },
            { InterfaceSoundType.GameInventoryBookClose, gameInventoryBookClose },
            { InterfaceSoundType.GameInventoryBookPageFlip, gameInventoryBookPageFlip },
            { InterfaceSoundType.GameInventoryBookSamePage, gameInventoryBookSamePage },
            { InterfaceSoundType.OnAbilityChanged, onAbilityChanged },
            { InterfaceSoundType.OnSeedSelect, onSeedSelect },
            { InterfaceSoundType.LevelUpOpen, levelUpOpen },
            { InterfaceSoundType.LevelUpClose, levelUpClose },
            { InterfaceSoundType.LevelUpRewardOpen, levelUpRewardOpen },
            { InterfaceSoundType.LevelUpRewardItem, levelUpRewardItem },
            { InterfaceSoundType.LevelUpRewardClose, levelUpRewardClose },
        };
    }

    public SoundClipData GetSound(InterfaceSoundType soundType)
    {
        if (soundDictionary == null) Initialize();

        soundDictionary.TryGetValue(soundType, out SoundClipData soundData);
        return soundData;
    }

    public void PlaySound(InterfaceSoundType soundType)
    {
        var soundData = GetSound(soundType);
        if (soundData == null) return;
        if (!soundData.CanPlay()) return;
        SoundManager.Instance.PlayClip(soundData, ui: IsMenuSound(soundType));
        soundData.SetLastPlayTime();
    }

    private static bool IsMenuSound(InterfaceSoundType soundType)
    {
        return soundType != InterfaceSoundType.OnAbilityChanged && soundType != InterfaceSoundType.OnSeedSelect;
    }
}