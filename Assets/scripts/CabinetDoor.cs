using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public class CabinetDoor : MonoBehaviour, IPlayerInteractable
{
    private enum RotationAxis
    {
        X,
        Y,
        Z
    }

    [Header("柜门角度")]
    [SerializeField] private RotationAxis rotationAxis = RotationAxis.Z;
    [SerializeField] private float closedAngle = -90f;
    [SerializeField] private float openAngle = 0f;

    [Header("转动速度")]
    [SerializeField, Min(1f)] private float rotationSpeed = 180f;

    [Header("开始状态")]
    [SerializeField] private bool startOpen = false;

    [Header("柜内异常（只有对应的柜门需要填写）")]
    [SerializeField] private ScannableAnomaly anomalyInside;

    private Quaternion closedRotation;
    private Quaternion openRotation;
    private Quaternion targetRotation;
    private bool isOpen;
    private Renderer interactionPanel;

    public bool IsOpen => isOpen;
    public string InteractionPrompt => isOpen ? "Close cabinet" : "Open cabinet";

    private void Awake()
    {
        EnsureDoorCollider();

        // Replace only the chosen local rotation axis; preserve the other two.
        Vector3 baseEuler = transform.localEulerAngles;
        Vector3 closedEuler = WithAxisAngle(baseEuler, closedAngle);
        Vector3 openEuler = WithAxisAngle(baseEuler, openAngle);
        closedRotation = Quaternion.Euler(closedEuler);
        openRotation = Quaternion.Euler(openEuler);

        Rigidbody body = GetComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        body.interpolation = RigidbodyInterpolation.Interpolate;

        SetOpen(startOpen, true);
    }

    private void EnsureDoorCollider()
    {
        // Prefer the glass panel, or the largest face of this multi-mesh door.
        // One broad hit surface is more reliable than adding colliders to every trim.
        MeshFilter target = null;
        float largestArea = 0f;

        foreach (MeshFilter meshFilter in GetComponentsInChildren<MeshFilter>(true))
        {
            if (meshFilter.sharedMesh == null)
                continue;

            Bounds bounds = meshFilter.sharedMesh.bounds;
            Vector3 size = bounds.size;
            float area = Mathf.Max(size.x * size.y, size.x * size.z, size.y * size.z);

            bool isGlassPanel = meshFilter.name.ToLowerInvariant().Contains("glass window");
            if (isGlassPanel)
            {
                target = meshFilter;
                break;
            }

            if (area > largestArea)
            {
                largestArea = area;
                target = meshFilter;
            }
        }

        if (target == null)
        {
            Debug.LogError($"{name}: 柜门下面找不到可添加碰撞体的网格。", this);
            return;
        }

        interactionPanel = target.GetComponent<Renderer>();

        // Colliders on the trim do not make the glass itself raycastable.
        if (target.GetComponent<BoxCollider>() != null) return;

        BoxCollider box = target.gameObject.AddComponent<BoxCollider>();
        Bounds meshBounds = target.sharedMesh.bounds;
        Vector3 colliderSize = meshBounds.size;
        colliderSize.x = Mathf.Max(colliderSize.x, 0.02f);
        colliderSize.y = Mathf.Max(colliderSize.y, 0.02f);
        colliderSize.z = Mathf.Max(colliderSize.z, 0.02f);
        box.center = meshBounds.center;
        box.size = colliderSize;
        box.isTrigger = true;
    }

    private void FixedUpdate()
    {
        transform.localRotation = Quaternion.RotateTowards(
            transform.localRotation,
            targetRotation,
            rotationSpeed * Time.fixedDeltaTime);
    }

    private void Update()
    {
        if (isOpen && anomalyInside != null)
            anomalyInside.RevealIfDetected();
    }

    public void Toggle()
    {
        SetOpen(!isOpen, false);
        ShiftAudioDirector.PlayCabinet(isOpen);
    }

    public bool CanInteract(PlayerInteractor interactor) => enabled && interactor != null;

    // Imported cabinet frames may have a solid collider just in front of the glass.
    // Use the visible door panel as a precise fallback aiming surface.
    public bool TryGetAimDistance(Ray ray, float maxDistance, out float distance)
    {
        distance = 0f;
        if (!isActiveAndEnabled || interactionPanel == null || !interactionPanel.gameObject.activeInHierarchy)
            return false;
        Bounds bounds = interactionPanel.bounds;
        foreach (Renderer part in GetComponentsInChildren<Renderer>(true))
            if (part != null && part.enabled && part.gameObject.activeInHierarchy)
                bounds.Encapsulate(part.bounds);
        bounds.Expand(0.08f);
        return bounds.IntersectRay(ray, out distance) && distance >= 0f && distance <= maxDistance;
    }

    public void Interact(PlayerInteractor interactor) => Toggle();

    public void SetOpen(bool open, bool immediate = false)
    {
        isOpen = open;
        targetRotation = isOpen ? openRotation : closedRotation;

        if (immediate)
            transform.localRotation = targetRotation;
    }

    private Vector3 WithAxisAngle(Vector3 euler, float angle)
    {
        switch (rotationAxis)
        {
            case RotationAxis.X:
                euler.x = angle;
                break;
            case RotationAxis.Y:
                euler.y = angle;
                break;
            default:
                euler.z = angle;
                break;
        }

        return euler;
    }
}
