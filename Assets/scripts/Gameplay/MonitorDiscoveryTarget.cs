using System;
using UnityEngine;

[DisallowMultipleComponent]
public class MonitorDiscoveryTarget : MonoBehaviour
{
    [SerializeField] private MonitorViewManager monitorView;
    [SerializeField] private MonitorCameraController monitorCameras;
    [SerializeField] private Renderer[] targetRenderers;
    [SerializeField, Min(0.05f)] private float visibleSeconds = 0.6f;
    [SerializeField] private string discoveredSignal;
    [Tooltip("发现前保持关闭，例如拾取脚本或射击目标脚本。")]
    [SerializeField] private Behaviour[] enableWhenDiscovered;

    public bool Discovered { get; private set; }
    private float visibleTimer;

    private void Awake()
    {
        if (targetRenderers == null || targetRenderers.Length == 0)
            targetRenderers = GetComponentsInChildren<Renderer>(true);
        SetBehaviours(false);
    }

    public void RefreshRenderers()
    {
        targetRenderers = GetComponentsInChildren<Renderer>(true);
    }

    private void Update()
    {
        if (Discovered || monitorView == null || monitorCameras == null || !monitorView.IsFocused)
            return;

        Camera camera = monitorCameras.GetCamera(monitorView.FocusedIndex);
        bool visible = camera != null && IsVisibleFrom(camera);
        visibleTimer = visible ? visibleTimer + Time.deltaTime : 0f;
        if (visibleTimer >= visibleSeconds) Discover();
    }

    private bool IsVisibleFrom(Camera camera)
    {
        Plane[] planes = GeometryUtility.CalculateFrustumPlanes(camera);
        foreach (Renderer targetRenderer in targetRenderers)
            if (targetRenderer != null && targetRenderer.enabled
                && targetRenderer.gameObject.activeInHierarchy
                && GeometryUtility.TestPlanesAABB(planes, targetRenderer.bounds)
                && HasClearView(camera, targetRenderer.bounds.center))
                return true;
        return false;
    }

    private bool HasClearView(Camera camera, Vector3 targetPoint)
    {
        Vector3 direction = targetPoint - camera.transform.position;
        float distance = direction.magnitude;
        if (distance < 0.01f) return false;

        RaycastHit[] hits = Physics.RaycastAll(camera.transform.position,
            direction / distance, distance + 0.05f, camera.cullingMask,
            QueryTriggerInteraction.Ignore);
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.transform.IsChildOf(transform)) return true;
            // Ignore invisible gameplay volumes; visible geometry blocks the feed.
            Renderer blocker = hit.collider.GetComponent<Renderer>();
            if (blocker == null) blocker = hit.collider.GetComponentInParent<Renderer>();
            if (blocker != null && blocker.enabled && blocker.gameObject.activeInHierarchy)
                return false;
        }
        return false;
    }

    public void Discover()
    {
        if (Discovered) return;
        Discovered = true;
        SetBehaviours(true);
        GameplaySignals.Instance?.Raise(discoveredSignal);
    }

    private void SetBehaviours(bool value)
    {
        if (enableWhenDiscovered == null) return;
        foreach (Behaviour behaviour in enableWhenDiscovered)
            if (behaviour != null) behaviour.enabled = value;
    }
}
