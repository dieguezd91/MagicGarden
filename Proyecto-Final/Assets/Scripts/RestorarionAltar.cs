using System;
using MagicGarden.LoadoutUI;
using UnityEngine;

// Keep the serialized component identity and the independent restoration system/assets.
public class RestorationAltar : MonoBehaviour, IInteractionLifecycle
{
    [Header("Audio")]
    [SerializeField] private SoundClipData interactSound;
    [SerializeField] private SoundClipData completeSound;
    private DailyLoadoutPresenter activePresenter;

    public event Action InteractionCompleted;

    public void Interact() => TryInteract();

    public bool TryInteract()
    {
        if (!CanInteract()) return false;
        var presenter = UIManager.Instance.DailyLoadout;
        if (!presenter.Open()) return false;
        activePresenter = presenter;
        activePresenter.Closed += HandleClosed;
        if (interactSound != null && interactSound.CanPlay())
        {
            SoundManager.Instance?.PlayClip(interactSound);
            interactSound.SetLastPlayTime();
        }
        TutorialEvents.InvokeDailyLoadoutOpened();
        return true;
    }

    public bool CanInteract()
    {
        var ui = UIManager.Instance;
        return isActiveAndEnabled && activePresenter == null && Time.timeScale > 0f &&
            GameFlowController.Instance != null && GameFlowController.Instance.CurrentPhase == GamePhase.Day &&
            !UpgradeRuntime.GameplayBlocked && ui != null && ui.DailyLoadout != null &&
            ui.Flow != null && !ui.Flow.HasOpenModal;
    }

    private void HandleClosed()
    {
        if (activePresenter == null) return;
        activePresenter.Closed -= HandleClosed;
        activePresenter = null;
        InteractionCompleted?.Invoke();
    }

    public void CancelInteraction()
    {
        if (activePresenter != null) activePresenter.Close();
    }

    public void OnInteractionComplete()
    {
        if (completeSound != null && completeSound.CanPlay())
        {
            SoundManager.Instance?.PlayClip(completeSound);
            completeSound.SetLastPlayTime();
        }
    }

    private void OnDisable() => CancelInteraction();
    private void OnDestroy() => CancelInteraction();
}
