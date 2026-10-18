using System;
using UnityEngine;

public enum UIModal
{
    None,
    Inventory,
    Crafting,
    Restoration,
    LevelUp,
    DailyLoadout
}

public sealed class UIFlowController : MonoBehaviour
{
    public UIModal CurrentModal { get; private set; } = UIModal.None;

    public bool HasOpenModal => CurrentModal != UIModal.None;

    public event Action<UIModal> ModalChanged;

    public bool IsOpen(UIModal modal)
    {
        return CurrentModal == modal;
    }

    public bool Open(UIModal modal)
    {
        if (UpgradeRuntime.GameplayBlocked && modal != UIModal.LevelUp) return false;
        if (modal == UIModal.None)
            return false;

        if (CurrentModal != UIModal.None)
            return false;

        CurrentModal = modal;
        ModalChanged?.Invoke(CurrentModal);
        return true;
    }

    public void Close(UIModal modal)
    {
        if (CurrentModal != modal)
            return;

        CurrentModal = UIModal.None;
        ModalChanged?.Invoke(CurrentModal);
    }
}
