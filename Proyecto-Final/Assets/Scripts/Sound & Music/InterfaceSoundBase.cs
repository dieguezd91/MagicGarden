using System.Collections.Generic;
using UnityEngine;

public class InterfaceSoundBase : MonoBehaviour
{
    [Header("Sound Effects")]
    public SoundClipData menuButtonHover;
    public SoundClipData menuButtonDisabledHover;
    public SoundClipData menuButtonClick;
    public SoundClipData menuButtonDisabledClick;

    [Header("Menu Button Specific Sounds")]
    public SoundClipData menuButtonPlay;
    public SoundClipData menuButtonOptions;
    public SoundClipData menuButtonControls;
    public SoundClipData menuButtonExit;

    [Header("Game Interface Sounds")]
    public SoundClipData gameInventoryBookOpen;
    public SoundClipData gameInventoryBookClose;
    public SoundClipData pauseOpen;
    public SoundClipData pauseClose;

    public SoundClipData onAbilityChanged;
    public SoundClipData onSeedSelect;

    private Dictionary<InterfaceSoundType, SoundClipData> soundDictionary;

    public void Initialize()
    {
        soundDictionary = new Dictionary<InterfaceSoundType, SoundClipData>
        {
            { InterfaceSoundType.MenuButtonHover, menuButtonHover },
            { InterfaceSoundType.MenuButtonDisabledHover, menuButtonDisabledHover },
            { InterfaceSoundType.MenuButtonClick, menuButtonClick },
            { InterfaceSoundType.MenuButtonDisabledClick, menuButtonDisabledClick },
            { InterfaceSoundType.MenuButtonPlay, menuButtonPlay },
            { InterfaceSoundType.MenuButtonOptions, menuButtonOptions },
            { InterfaceSoundType.MenuButtonControls, menuButtonControls },
            { InterfaceSoundType.MenuButtonExit, menuButtonExit },
            { InterfaceSoundType.GameInventoryBookOpen, gameInventoryBookOpen },
            { InterfaceSoundType.GameInventoryBookClose, gameInventoryBookClose },
            { InterfaceSoundType.GamePauseOpen, pauseOpen},
            { InterfaceSoundType.GamePauseClose, pauseClose},
            { InterfaceSoundType.OnAbilityChanged, onAbilityChanged },
            { InterfaceSoundType.OnSeedSelect, onSeedSelect },
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