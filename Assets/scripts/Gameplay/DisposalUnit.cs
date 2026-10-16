using UnityEngine;

[DisallowMultipleComponent]
public class DisposalUnit : MonoBehaviour, IPlayerInteractable
{
    [SerializeField] private string prompt = "Dispose of item";
    [SerializeField, Min(0f)] private float destroyDelay = 0.15f;
    private static DisposalUnit activeUnit;
    private Transform visualRoot;

    private void Awake()
    {
        activeUnit = this;
        GameObject model = GameObject.Find("DISPOSAL_UNIT.001");
        if (model == null) return;
        visualRoot = model.transform;

        // The imported lid is visual-only. Trigger colliders make its surface
        // aimable without adding another physical obstacle to the player.
        foreach (MeshFilter mesh in model.GetComponentsInChildren<MeshFilter>(true))
        {
            string part = mesh.name;
            if (part != "V9 Disposal | continuous tapered body.001"
                && part != "V9 Disposal | sloped hatch surround.001"
                && part != "V9 Disposal | smoked hatch.001") continue;
            if (mesh.sharedMesh == null || mesh.GetComponent<Collider>() != null) continue;
            BoxCollider surface = mesh.gameObject.AddComponent<BoxCollider>();
            surface.center = mesh.sharedMesh.bounds.center;
            surface.size = mesh.sharedMesh.bounds.size;
            surface.isTrigger = true;
        }
    }

    public static DisposalUnit ForSurface(Collider surface)
    {
        DisposalUnit unit = activeUnit;
        return unit != null && unit.visualRoot != null && surface != null
            && surface.transform.IsChildOf(unit.visualRoot) ? unit : null;
    }

    private void OnDestroy()
    {
        if (activeUnit == this) activeUnit = null;
    }

    public Renderer[] VisualRenderers => visualRoot != null
        ? visualRoot.GetComponentsInChildren<Renderer>(true) : System.Array.Empty<Renderer>();

    public string InteractionPrompt => prompt;

    public bool CanInteract(PlayerInteractor interactor)
        => interactor != null && interactor.InputEnabled
            && interactor.HeldItem != null && interactor.HeldItem.Disposable;

    public void Interact(PlayerInteractor interactor)
    {
        PickupItem item = interactor.ReleaseHeldItem(false);
        if (item == null) return;
        ShiftAudioDirector.PlayDisposal();
        GameplaySignals.Instance?.Raise(item.SignalWhenDisposed);
        item.gameObject.SetActive(false);
        Destroy(item.gameObject, destroyDelay);
    }
}
