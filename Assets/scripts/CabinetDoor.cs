using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public class CabinetDoor : MonoBehaviour
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

    private Quaternion closedRotation;
    private Quaternion openRotation;
    private Quaternion targetRotation;
    private bool isOpen;

    public bool IsOpen => isOpen;

    private void Awake()
    {
        EnsureDoorCollider();

        // 保留另外两个方向，只替换所选本地轴的角度。
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
        // 柜门由多个网格组成。优先把碰撞体放在面积最大的玻璃门板上，
        // 这样射线可以稳定识别整扇门，也不需要手动给每个零件加碰撞体。
        if (GetComponentInChildren<Collider>(true) != null)
            return;

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

        BoxCollider box = target.gameObject.AddComponent<BoxCollider>();
        Bounds meshBounds = target.sharedMesh.bounds;
        Vector3 colliderSize = meshBounds.size;
        colliderSize.x = Mathf.Max(colliderSize.x, 0.02f);
        colliderSize.y = Mathf.Max(colliderSize.y, 0.02f);
        colliderSize.z = Mathf.Max(colliderSize.z, 0.02f);
        box.center = meshBounds.center;
        box.size = colliderSize;
        box.isTrigger = false;
    }

    private void FixedUpdate()
    {
        transform.localRotation = Quaternion.RotateTowards(
            transform.localRotation,
            targetRotation,
            rotationSpeed * Time.fixedDeltaTime);
    }

    public void Toggle()
    {
        SetOpen(!isOpen, false);
    }

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
