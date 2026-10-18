// Optional lifecycle for interactions with a scoped completion owner.
// Legacy interactables retain their existing notification path.
public interface IInteractionLifecycle : IInteractable
{
    event System.Action InteractionCompleted;
    bool TryInteract();
    void CancelInteraction();
}

public interface IInteractable
{
    void Interact();

    bool CanInteract();
}