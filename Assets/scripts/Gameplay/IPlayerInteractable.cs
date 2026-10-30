/// <summary>
/// Shared contract for an E-key target. CanInteract gates the current context;
/// Interact performs the action only after the player interactor selects this target.
/// </summary>
public interface IPlayerInteractable
{
    string InteractionPrompt { get; }
    bool CanInteract(PlayerInteractor interactor);
    void Interact(PlayerInteractor interactor);
}
