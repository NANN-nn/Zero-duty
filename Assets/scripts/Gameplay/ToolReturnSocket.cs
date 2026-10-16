using UnityEngine;

[DisallowMultipleComponent]
public class ToolReturnSocket : MonoBehaviour, IPlayerInteractable
{
    [SerializeField] private string acceptedItemId;
    [SerializeField] private string toolDisplayName = "tool";
    [SerializeField] private Transform socketPoint;
    [SerializeField] private string returnedSignal;

    public string InteractionPrompt => $"Return {toolDisplayName}";

    private void Awake()
    {
        if (socketPoint == null) socketPoint = transform;
    }

    public bool CanInteract(PlayerInteractor interactor)
        => interactor != null && interactor.HeldItem != null
           && interactor.HeldItem.ItemId == acceptedItemId;

    public void Interact(PlayerInteractor interactor)
    {
        PickupItem item = interactor.ReleaseHeldItem(false);
        if (item == null) return;
        item.PlaceAt(socketPoint);
        GameplaySignals.Instance?.Raise(returnedSignal);
    }
}
