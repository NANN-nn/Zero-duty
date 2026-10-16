using UnityEngine;

/// <summary>Lets a second mesh on the same access reader open the one final-choice terminal.</summary>
[DisallowMultipleComponent]
public class FinalChoiceSurface : MonoBehaviour, IPlayerInteractable
{
    [SerializeField] private FinalChoiceTerminal terminal;

    public string InteractionPrompt => terminal != null ? terminal.InteractionPrompt : string.Empty;

    public bool CanInteract(PlayerInteractor interactor)
        => terminal != null && terminal.CanInteract(interactor);

    public void Interact(PlayerInteractor interactor)
    {
        if (terminal != null) terminal.Interact(interactor);
    }
}
