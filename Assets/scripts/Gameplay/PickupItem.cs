using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public class PickupItem : MonoBehaviour, IPlayerInteractable
{
    [SerializeField] private string itemId = "item";
    [SerializeField] private string displayName = "Item";
    [SerializeField] private bool disposable;
    [SerializeField] private string signalWhenDisposed;
    [SerializeField] private Vector3 heldLocalPosition;
    [SerializeField] private Vector3 heldLocalEulerAngles;
    [SerializeField, Range(0.05f, 1f)] private float heldScaleMultiplier = 0.3f;
    [SerializeField] private float heldVerticalOffset = 0f;
    [Header("Move with this item (optional loose pieces)")]
    [SerializeField] private Transform[] attachedPieces;
    [Header("Pickup presentation")]
    [SerializeField, Min(0.05f)] private float pickupDuration = 0.38f;
    [SerializeField] private Vector3 pickupStartOffset = new Vector3(-0.08f, -0.24f, -0.1f);

    public string ItemId => itemId;
    public float HeldScaleMultiplier => heldScaleMultiplier;
    public Vector3 HeldLocalPosition => heldLocalPosition + Vector3.up * heldVerticalOffset;
    public Vector3 HeldLocalEulerAngles => heldLocalEulerAngles;
    public bool Disposable => disposable;
    public string SignalWhenDisposed => signalWhenDisposed;
    public bool IsHeld { get; private set; }
    public bool IsDropped { get; private set; }
    public Vector3 HomeAimPosition { get; private set; }
    public PlayerInteractor Owner { get; private set; }
    public string InteractionPrompt => $"Pick up {displayName}";
    public Vector3 PickupPresentationOffset =>
        (pickupStartOffset == Vector3.zero
            ? new Vector3(-0.08f, -0.24f, -0.1f) : pickupStartOffset)
        * (1f - PickupBlend);

    private Rigidbody body;
    private Collider[] colliders;
    private Renderer[] renderers;
    private Vector3 originalLocalScale;
    private Transform originalParent;
    private Vector3 originalLocalPosition;
    private Quaternion originalLocalRotation;
    private Vector3 settledHeldPosition;
    private float pickupStartedAt;
    private Renderer[] hiddenRenderers;
    private bool[] hiddenRendererStates;
    private bool viewHidden;
    private bool usesToolPose;

    private float PickupBlend
    {
        get
        {
            float duration = pickupDuration > 0.05f ? pickupDuration : 0.38f;
            float t = Mathf.Clamp01((Time.time - pickupStartedAt) /
                duration);
            return t * t * (3f - 2f * t);
        }
    }

    private void Awake()
    {
        // The corridor finds were configured below the camera and at only 30%
        // scale. Bring their visible centre into the hand view without changing
        // the specimen jars or the adjustable gun/scanner poses.
        if (signalWhenDisposed == "corridor_disposed")
        {
            heldVerticalOffset = Mathf.Max(0f, heldVerticalOffset);
            heldScaleMultiplier = Mathf.Max(0.7f, heldScaleMultiplier);
            if (GetComponent<CorridorItemViewmodel>() == null)
                gameObject.AddComponent<CorridorItemViewmodel>();
        }
        // Imported broken props are often several sibling objects. Once attached,
        // pickup, dropping and disposal all operate on the whole assembly.
        if (attachedPieces != null)
            foreach (Transform piece in attachedPieces)
                if (piece != null && piece != transform && !transform.IsChildOf(piece))
                    piece.SetParent(transform, true);
        originalParent = transform.parent;
        originalLocalPosition = transform.localPosition;
        originalLocalRotation = transform.localRotation;
        RuntimeColliderUtility.EnsureBoxCollider(gameObject);
        body = GetComponent<Rigidbody>();
        colliders = GetComponentsInChildren<Collider>(true);
        renderers = GetComponentsInChildren<Renderer>(true);
        usesToolPose = GetComponent<GunTool>() != null || GetComponent<ScannerTool>() != null;
        Bounds homeBounds = default;
        bool hasHomeBounds = false;
        foreach (Renderer itemRenderer in renderers)
        {
            if (itemRenderer == null || !itemRenderer.enabled) continue;
            if (!hasHomeBounds)
            {
                homeBounds = itemRenderer.bounds;
                hasHomeBounds = true;
            }
            else homeBounds.Encapsulate(itemRenderer.bounds);
        }
        HomeAimPosition = hasHomeBounds ? homeBounds.center : transform.position;
        // Keep wall pictures and docked props fixed until the player picks them up.
        body.isKinematic = true;
        body.useGravity = false;
    }

    public bool CanInteract(PlayerInteractor interactor)
        => enabled && interactor != null && interactor.CanAcceptPickup(this);

    public void Interact(PlayerInteractor interactor) => interactor.TryHold(this);

    public void SetHeldScaleMultiplier(float value)
    {
        heldScaleMultiplier = Mathf.Clamp(value, 0.05f, 1f);
        if (IsHeld) transform.localScale = originalLocalScale * heldScaleMultiplier;
    }

    public void SetHeld(PlayerInteractor owner, Transform point)
    {
        ShiftAudioDirector.PlayPickup();
        SetViewHidden(false);
        originalLocalScale = transform.localScale;
        Owner = owner;
        IsHeld = true;
        IsDropped = false;
        body.isKinematic = true;
        body.useGravity = false;
        foreach (Collider itemCollider in colliders) itemCollider.enabled = false;
        transform.SetParent(point, false);
        transform.localPosition = heldLocalPosition + Vector3.up * heldVerticalOffset;
        transform.localRotation = Quaternion.Euler(heldLocalEulerAngles);
        transform.localScale = originalLocalScale * heldScaleMultiplier;
        // Imported models often have an off-centre pivot; place their visible bounds at the hand point.
        Bounds visibleBounds = default;
        bool hasVisibleBounds = false;
        foreach (Renderer itemRenderer in renderers)
        {
            if (itemRenderer == null || !itemRenderer.enabled || !itemRenderer.gameObject.activeInHierarchy)
                continue;
            if (!hasVisibleBounds)
            {
                visibleBounds = itemRenderer.bounds;
                hasVisibleBounds = true;
            }
            else visibleBounds.Encapsulate(itemRenderer.bounds);
        }
        if (hasVisibleBounds)
            transform.position += point.TransformPoint(heldLocalPosition + Vector3.up * heldVerticalOffset)
                - visibleBounds.center;
        settledHeldPosition = transform.localPosition;
        pickupStartedAt = Time.time;
        if (signalWhenDisposed == "corridor_disposed")
            GameplaySignals.Instance?.Raise("corridor_item_picked_up");
    }

    private void LateUpdate()
    {
        if (!IsHeld || usesToolPose) return;
        transform.localPosition = settledHeldPosition + PickupPresentationOffset;
        Quaternion heldRotation = Quaternion.Euler(heldLocalEulerAngles);
        transform.localRotation = Quaternion.Slerp(
            heldRotation * Quaternion.Euler(12f, -8f, 10f), heldRotation, PickupBlend);
    }

    public void SetViewHidden(bool hidden)
    {
        if (viewHidden == hidden) return;
        viewHidden = hidden;
        if (hidden)
        {
            hiddenRenderers = GetComponentsInChildren<Renderer>(true);
            hiddenRendererStates = new bool[hiddenRenderers.Length];
            for (int i = 0; i < hiddenRenderers.Length; i++)
            {
                if (hiddenRenderers[i] == null) continue;
                hiddenRendererStates[i] = hiddenRenderers[i].enabled;
                hiddenRenderers[i].enabled = false;
            }
        }
        else
        {
            if (hiddenRenderers != null && hiddenRendererStates != null)
                for (int i = 0; i < hiddenRenderers.Length; i++)
                    if (hiddenRenderers[i] != null)
                        hiddenRenderers[i].enabled = hiddenRendererStates[i];
            hiddenRenderers = null;
            hiddenRendererStates = null;
        }
    }

    public void Release(bool dropInWorld)
    {
        if (dropInWorld) ShiftAudioDirector.PlayDrop();
        SetViewHidden(false);
        GetComponent<CorridorItemViewmodel>()?.StopOverlay();
        Transform previousPoint = transform.parent;
        transform.SetParent(null, true);
        transform.localScale = originalLocalScale;
        if (dropInWorld && previousPoint != null)
            transform.position = previousPoint.position + previousPoint.forward * 0.35f;
        foreach (Collider itemCollider in colliders)
        {
            // Unity does not allow a non-convex mesh collider on a dropped dynamic Rigidbody.
            // The broken-jar groups have their own root BoxCollider for the dropped assembly.
            bool unsupportedMesh = dropInWorld && GetComponent<BoxCollider>() != null &&
                itemCollider is MeshCollider mesh && !mesh.convex;
            itemCollider.enabled = !unsupportedMesh;
        }
        body.isKinematic = !dropInWorld;
        body.useGravity = dropInWorld;
        Owner = null;
        IsHeld = false;
        IsDropped = dropInWorld;
    }

    public void PlaceAt(Transform socket)
    {
        SetViewHidden(false);
        GetComponent<CorridorItemViewmodel>()?.StopOverlay();
        transform.SetParent(socket, false);
        transform.localPosition = socket == originalParent ? originalLocalPosition : Vector3.zero;
        transform.localRotation = socket == originalParent ? originalLocalRotation : Quaternion.identity;
        transform.localScale = originalLocalScale;
        foreach (Collider itemCollider in colliders) itemCollider.enabled = true;
        body.isKinematic = true;
        body.useGravity = false;
        Owner = null;
        IsHeld = false;
        IsDropped = false;
    }

    public void ReturnHome()
    {
        PlaceAt(originalParent);
    }
}
