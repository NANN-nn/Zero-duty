using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(-40)]
[DisallowMultipleComponent]
public class PlayerInteractor : MonoBehaviour
{
    [Header("视线交互")]
    [SerializeField] private Camera interactionCamera;
    [SerializeField, Min(0.1f)] private float interactionDistance = 2f;
    [SerializeField] private LayerMask interactionLayers = ~0;

    [Header("手持位置")]
    [SerializeField] private Transform holdPoint;

    public Camera InteractionCamera => interactionCamera;
    public Transform HoldPoint => holdPoint;
    public PickupItem HeldItem { get; private set; }
    public PickupItem StowedTool { get; private set; }
    public bool InputEnabled { get; set; } = true;

    private IPlayerInteractable current;
    private MonitorInteraction monitorInteraction;
    public MonoBehaviour FeedbackTarget
    {
        get
        {
            if (!InputEnabled || !Application.isFocused || MonitorViewManager.ActiveSession != null)
                return null;
            if (current is OptionalPropInteraction || current is ShortMusicPlayer)
                return current as MonoBehaviour;
            if (HeldItem == null)
            {
                PickupItem nearbyTool = FindNearbyTool();
                if (nearbyTool != null) return nearbyTool;
            }
            // An aimed control or item wins over the nearby tool-return shortcut.
            if (current != null) return current as MonoBehaviour;
            if (current == null && monitorInteraction != null && monitorInteraction.CanInteract(this))
                return monitorInteraction;
            if (HeldItem != null && CanReturnHeldToolToHome()) return HeldItem;
            if (current == null)
            {
                PickupItem dropped = FindNearbyDroppedItem();
                if (dropped != null) return dropped;
            }
            if (current == null)
                foreach (ReferencePictureView picture in FindObjectsByType<ReferencePictureView>(FindObjectsSortMode.None))
                    if (picture.IsAimedAt(interactionCamera)) return picture;
            return current as MonoBehaviour;
        }
    }

    private void Awake()
    {
        // Existing scenes still serialize the old 2.5 m value.
        interactionDistance = Mathf.Min(interactionDistance, 2f);
        if (interactionCamera == null) interactionCamera = Camera.main;
        if (interactionCamera == null)
        {
            Camera[] sceneCameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
            foreach (Camera candidate in sceneCameras)
                if (candidate != null && candidate.targetTexture == null && candidate.isActiveAndEnabled)
                {
                    interactionCamera = candidate;
                    break;
                }
        }
        if (holdPoint == null && interactionCamera != null)
        {
            GameObject point = new GameObject("HoldPoint");
            holdPoint = point.transform;
            holdPoint.SetParent(interactionCamera.transform, false);
            holdPoint.localPosition = new Vector3(0.35f, -0.3f, 0.75f);
        }
        if (GetComponent<InteractionFocusFeedback>() == null)
            gameObject.AddComponent<InteractionFocusFeedback>();
        if (GetComponent<StationObjectiveAndPauseUI>() == null)
            gameObject.AddComponent<StationObjectiveAndPauseUI>();
        monitorInteraction = FindAnyObjectByType<MonitorInteraction>();
    }

    private void Update()
    {
        if (!InputEnabled || MonitorViewManager.ActiveSession != null || !Application.isFocused)
        {
            current = null;
            return;
        }

        current = FindLookedAtInteractable();

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard.eKey.wasPressedThisFrame)
        {
            // A collectible on the monitor desk must win over the desk's
            // seated-monitor interaction and nearby tool shortcuts.
            if (current is OptionalPropInteraction optionalProp && optionalProp.CanInteract(this))
            {
                optionalProp.Interact(this);
                return;
            }
            if (current != null && current.CanInteract(this))
            {
                current.Interact(this);
                return;
            }
            if (HeldItem == null && TryPickUpNearbyTool()) return;
            if (HeldItem != null && TryReturnHeldToolToHome()) return;
            TryPickUpNearbyDroppedItem();
        }

        if (keyboard.qKey.wasPressedThisFrame && HeldItem != null)
            DropHeldItem();
    }

    private IPlayerInteractable FindLookedAtInteractable()
    {
        if (interactionCamera == null) return null;
        Ray ray = interactionCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f));
        RaycastHit[] hits = Physics.RaycastAll(ray, interactionDistance, interactionLayers,
            QueryTriggerInteraction.Collide);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        // An exact aim at the disposal lid takes precedence over a nearby
        // sticky note's more forgiving screen-space selection.
        if (HeldItem != null && HeldItem.Disposable)
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.transform.IsChildOf(transform.root)) continue;
                DisposalUnit disposal = DisposalUnit.ForSurface(hit.collider);
                if (disposal != null && disposal.CanInteract(this)) return disposal;
                if (!hit.collider.isTrigger) break;
            }

        OptionalPropInteraction nearbyProp = FindLookedAtOptionalProp(ray);
        if (nearbyProp != null) return nearbyProp;
        PickupItem revealedItem = FindLookedAtRevealedItem(ray);
        if (revealedItem != null) return revealedItem;

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.transform.IsChildOf(transform.root)) continue;

            DisposalUnit visualDisposal = DisposalUnit.ForSurface(hit.collider);
            if (visualDisposal != null && visualDisposal.CanInteract(this))
                return visualDisposal;

            MonoBehaviour[] behaviours = hit.collider.GetComponentsInParent<MonoBehaviour>(true);
            foreach (MonoBehaviour behaviour in behaviours)
                if (behaviour is IPlayerInteractable interactable && interactable.CanInteract(this))
                    return interactable;

            // 实体墙面或家具挡住视线时，不能隔着它们交互。
            if (!hit.collider.isTrigger)
                return FindCabinetDoorBehindFrame(ray, hit.distance);
        }

        return FindCabinetDoorBehindFrame(ray, 0f);
    }

    private OptionalPropInteraction FindLookedAtOptionalProp(Ray ray)
    {
        const float propDistance = 2.2f;
        OptionalPropInteraction best = null;
        float bestScreenDistance = float.PositiveInfinity;
        foreach (OptionalPropInteraction prop in FindObjectsByType<OptionalPropInteraction>(FindObjectsSortMode.None))
        {
            if (prop == null || !prop.CanInteract(this))
                continue;
            Renderer visual = prop.GetComponentInChildren<Renderer>();
            if (visual == null || !visual.enabled || !visual.gameObject.activeInHierarchy) continue;
            Vector3 center = visual.bounds.center;
            bool byToolDock = prop.Kind == OptionalPropInteraction.PropKind.Robot
                && IsNearToolDock(center);
            float maxDistance = prop.Kind == OptionalPropInteraction.PropKind.Robot
                || prop.Kind == OptionalPropInteraction.PropKind.Note
                ? interactionDistance : propDistance;
            if (Vector3.Distance(interactionCamera.transform.position, center)
                > maxDistance) continue;
            Vector3 screen = interactionCamera.WorldToViewportPoint(center);
            if (screen.z <= 0f) continue;
            float dx = screen.x - 0.5f, dy = screen.y - 0.5f;
            float screenDistance = dx * dx + dy * dy;
            float horizontalTolerance = prop.Kind == OptionalPropInteraction.PropKind.Note ? 0.10f : 0.15f;
            float verticalTolerance = prop.Kind == OptionalPropInteraction.PropKind.Note ? 0.12f : 0.18f;
            if (Mathf.Abs(dx) > horizontalTolerance || Mathf.Abs(dy) > verticalTolerance
                || screenDistance >= bestScreenDistance) continue;

            // Check visibility towards the prop itself, not along the exact
            // crosshair ray. Small cans and mascots sit behind a desk edge
            // from eye height even while they are plainly visible.
            Vector3 toProp = center - ray.origin;
            Ray sight = new Ray(ray.origin, toProp.normalized);
            RaycastHit[] hits = Physics.RaycastAll(sight, toProp.magnitude + 0.05f,
                interactionLayers, QueryTriggerInteraction.Collide);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            bool blocked = false;
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.transform.IsChildOf(transform.root)) continue;
                if (hit.collider.transform.IsChildOf(prop.transform)) break;
                if (hit.collider.isTrigger) continue;

                // MonitorDesk itself is interactable, and its child desk
                // colliders must not steal E from props sitting on top.
                // The whole desk mesh may include tall monitor parts, so
                // bounds.max.y is not a reliable tabletop height.
                bool monitorDesk = hit.collider.GetComponentInParent<MonitorInteraction>() != null;
                float surfaceGap = Vector3.Distance(hit.collider.ClosestPoint(center), center);
                bool supportingFurniture = ((hit.collider.bounds.size.y < 1.75f && surfaceGap < 0.65f)
                    || (monitorDesk && surfaceGap < 1.25f))
                    && hit.collider.GetComponentInParent<CabinetDoor>() == null;
                // The mascot beside the weapons is tucked into the dock.
                // Ignore only nearby dock pieces, not room walls or doors.
                bool nearbyDockPiece = byToolDock && surfaceGap < 1.2f
                    && hit.collider.bounds.size.y < 2.2f
                    && hit.collider.GetComponentInParent<CabinetDoor>() == null;
                if (supportingFurniture || nearbyDockPiece) continue;

                blocked = true;
                break;
            }
            if (blocked) continue;
            best = prop;
            bestScreenDistance = screenDistance;
        }
        return best;
    }

    private static bool IsNearToolDock(Vector3 point)
    {
        foreach (PickupItem tool in FindObjectsByType<PickupItem>(FindObjectsSortMode.None))
            if (tool != null && (tool.ItemId == "gun" || tool.ItemId == "scanner")
                && Vector3.Distance(tool.HomeAimPosition, point) < 1.5f)
                return true;
        return false;
    }

    private PickupItem FindLookedAtRevealedItem(Ray ray)
    {
        foreach (ScannableAnomaly anomaly in FindObjectsByType<ScannableAnomaly>(FindObjectsSortMode.None))
        {
            PickupItem candidate = anomaly.RevealedItem;
            if (candidate == null || !candidate.CanInteract(this) || !candidate.gameObject.activeInHierarchy)
                continue;

            Bounds bounds = default;
            bool hasBounds = false;
            foreach (Renderer itemRenderer in candidate.GetComponentsInChildren<Renderer>())
            {
                if (!itemRenderer.enabled) continue;
                if (!hasBounds) { bounds = itemRenderer.bounds; hasBounds = true; }
                else bounds.Encapsulate(itemRenderer.bounds);
            }
            if (!hasBounds) continue;
            bounds.Expand(0.08f);
            if (!bounds.IntersectRay(ray, out float distance) || distance > interactionDistance)
                continue;

            // The cabinet frame or table edge may be just ahead of the broken model.
            // Do not allow this fallback through an unrelated wall farther away.
            RaycastHit[] hits = Physics.RaycastAll(ray, interactionDistance, interactionLayers,
                QueryTriggerInteraction.Ignore);
            float blockerDistance = float.PositiveInfinity;
            Collider blocker = null;
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.transform.IsChildOf(transform.root) || hit.distance >= blockerDistance)
                    continue;
                blockerDistance = hit.distance;
                blocker = hit.collider;
            }
            if (blocker != null && !blocker.transform.IsChildOf(candidate.transform))
            {
                bool openDoor = blocker.GetComponentInParent<CabinetDoor>()?.IsOpen == true;
                float allowedGap = openDoor ? 0.65f : 0.35f;
                if (distance - blockerDistance > allowedGap) continue;
            }
            return candidate;
        }
        return null;
    }

    private IPlayerInteractable FindCabinetDoorBehindFrame(Ray ray, float firstSolidDistance)
    {
        CabinetDoor nearest = null;
        float nearestDistance = float.PositiveInfinity;
        foreach (CabinetDoor door in FindObjectsByType<CabinetDoor>(FindObjectsSortMode.None))
        {
            if (!door.CanInteract(this) ||
                !door.TryGetAimDistance(ray, interactionDistance, out float doorDistance)) continue;
            // Only bypass the cabinet's own overlapping frame, never a wall in front of it.
            if (firstSolidDistance > 0f && doorDistance - firstSolidDistance > 0.4f) continue;
            if (doorDistance >= nearestDistance) continue;
            nearest = door;
            nearestDistance = doorDistance;
        }
        return nearest;
    }

    public bool CanAcceptPickup(PickupItem item)
    {
        if (item == null || item.IsHeld) return false;
        if (HeldItem == null) return true;
        return StowedTool == null && item.Disposable
            && (HeldItem.ItemId == "gun" || HeldItem.ItemId == "scanner");
    }

    public bool TryHold(PickupItem item)
    {
        if (!CanAcceptPickup(item) || holdPoint == null) return false;
        if (HeldItem != null)
        {
            StowedTool = HeldItem;
            StowedTool.gameObject.SetActive(false);
        }
        HeldItem = item;
        item.SetHeld(this, holdPoint);
        GetComponent<InteractionFocusFeedback>()?.NotifyItemPickedUp();
        return true;
    }

    private bool TryPickUpNearbyTool()
    {
        PickupItem best = FindNearbyTool();
        return best != null && TryHold(best);
    }

    private PickupItem FindNearbyTool()
    {
        if (interactionCamera == null) return null;

        PickupItem best = null;
        float bestScreenDistance = float.PositiveInfinity;
        foreach (PickupItem item in FindObjectsByType<PickupItem>(FindObjectsSortMode.None))
        {
            if (item == null || !item.CanInteract(this) || item.IsDropped) continue;
            // Display props mounted inside the equipment dock can be obscured by its mesh collider.
            if (item.ItemId != "gun" && item.ItemId != "scanner") continue;

            Vector3 aimPoint = item.transform.position;
            Renderer[] itemRenderers = item.GetComponentsInChildren<Renderer>(true);
            Bounds visibleBounds = default;
            bool hasBounds = false;
            foreach (Renderer itemRenderer in itemRenderers)
            {
                if (itemRenderer == null || !itemRenderer.enabled || !itemRenderer.gameObject.activeInHierarchy)
                    continue;
                if (!hasBounds)
                {
                    visibleBounds = itemRenderer.bounds;
                    hasBounds = true;
                }
                else visibleBounds.Encapsulate(itemRenderer.bounds);
            }
            if (hasBounds) aimPoint = visibleBounds.center;

            if (Vector3.Distance(interactionCamera.transform.position, aimPoint) > interactionDistance)
                continue;

            Vector3 screenPoint = interactionCamera.WorldToViewportPoint(aimPoint);
            if (screenPoint.z <= 0f) continue;
            float x = screenPoint.x - 0.5f;
            float y = screenPoint.y - 0.5f;
            float screenDistance = x * x + y * y;
            if (screenDistance > 0.15f * 0.15f || screenDistance >= bestScreenDistance)
                continue;

            best = item;
            bestScreenDistance = screenDistance;
        }

        return best;
    }

    private void TryPickUpNearbyDroppedItem()
    {
        PickupItem nearest = FindNearbyDroppedItem();
        if (nearest != null) TryHold(nearest);
    }

    private PickupItem FindNearbyDroppedItem()
    {
        PickupItem nearest = null;
        float nearestDistanceSqr = 1.5f * 1.5f;
        foreach (PickupItem item in FindObjectsByType<PickupItem>(FindObjectsSortMode.None))
        {
            if (item == null || !item.IsDropped || !item.CanInteract(this)) continue;
            float distanceSqr = (item.transform.position - transform.root.position).sqrMagnitude;
            if (distanceSqr >= nearestDistanceSqr) continue;
            nearest = item;
            nearestDistanceSqr = distanceSqr;
        }
        return nearest;
    }

    public PickupItem ReleaseHeldItem(bool dropInWorld)
    {
        PickupItem item = HeldItem;
        if (item == null) return null;
        HeldItem = null;
        item.Release(dropInWorld);
        GetComponent<InteractionFocusFeedback>()?.NotifyItemReleased();
        if (StowedTool != null)
        {
            HeldItem = StowedTool;
            StowedTool = null;
            HeldItem.gameObject.SetActive(true);
        }
        return item;
    }

    public void DropHeldItem() => ReleaseHeldItem(true);

    private bool TryReturnHeldToolToHome()
    {
        if (!CanReturnHeldToolToHome()) return false;

        PickupItem item = ReleaseHeldItem(false);
        item.ReturnHome();
        return true;
    }

    private bool CanReturnHeldToolToHome()
    {
        if (HeldItem == null || interactionCamera == null) return false;
        if (HeldItem.ItemId != "gun" && HeldItem.ItemId != "scanner") return false;

        Vector3 home = HeldItem.HomeAimPosition;
        if (Vector3.Distance(interactionCamera.transform.position, home) > interactionDistance)
            return false;
        Vector3 screenPoint = interactionCamera.WorldToViewportPoint(home);
        if (screenPoint.z <= 0f) return false;
        float x = screenPoint.x - 0.5f;
        float y = screenPoint.y - 0.5f;
        if (x * x + y * y > 0.15f * 0.15f) return false;
        return true;
    }
}
