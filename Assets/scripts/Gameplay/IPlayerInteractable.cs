public interface IPlayerInteractable
{
    string InteractionPrompt { get; }
    bool CanInteract(PlayerInteractor interactor);
    void Interact(PlayerInteractor interactor);
}
