using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>Keeps a carried corridor object visible in front of nearby walls.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PickupItem))]
public class CorridorItemViewmodel : MonoBehaviour
{
    const int ViewmodelLayer = 31;
    PickupItem item;
    Camera worldCamera;
    Camera overlayCamera;
    UniversalAdditionalCameraData worldData;
    int originalCullingMask;
    readonly Dictionary<GameObject, int> originalLayers = new Dictionary<GameObject, int>();

    void Awake() => item = GetComponent<PickupItem>();

    void LateUpdate()
    {
        if (item == null || !item.IsHeld || item.Owner == null || MonitorViewManager.ActiveSession != null)
        {
            RemoveOverlay();
            return;
        }
        if (overlayCamera == null) CreateOverlay();
        if (overlayCamera != null && worldCamera != null)
            overlayCamera.projectionMatrix = worldCamera.projectionMatrix;
    }

    void CreateOverlay()
    {
        Camera view = item.Owner.InteractionCamera;
        if (view == null) return;
        UniversalAdditionalCameraData data = view.GetUniversalAdditionalCameraData();
        if (data.cameraStack == null) return;
        worldCamera = view;
        worldData = data;
        originalCullingMask = view.cullingMask;
        GameObject overlayObject = new GameObject("Corridor Item Camera (Runtime)");
        overlayObject.transform.SetParent(view.transform, false);
        overlayCamera = overlayObject.AddComponent<Camera>();
        overlayCamera.CopyFrom(view);
        overlayCamera.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        overlayCamera.clearFlags = CameraClearFlags.Nothing;
        overlayCamera.cullingMask = 1 << ViewmodelLayer;
        overlayCamera.depth = view.depth + 1f;
        overlayCamera.GetUniversalAdditionalCameraData().renderType = CameraRenderType.Overlay;
        data.cameraStack.Add(overlayCamera);
        foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
        {
            if (renderer == null) continue;
            GameObject part = renderer.gameObject;
            if (!originalLayers.ContainsKey(part)) originalLayers.Add(part, part.layer);
            part.layer = ViewmodelLayer;
        }
        view.cullingMask &= ~(1 << ViewmodelLayer);
    }

    void RemoveOverlay()
    {
        if (worldData != null && worldData.cameraStack != null && overlayCamera != null)
            worldData.cameraStack.Remove(overlayCamera);
        if (worldCamera != null) worldCamera.cullingMask = originalCullingMask;
        foreach (var entry in originalLayers)
            if (entry.Key != null) entry.Key.layer = entry.Value;
        originalLayers.Clear();
        if (overlayCamera != null) Destroy(overlayCamera.gameObject);
        overlayCamera = null;
        worldCamera = null;
        worldData = null;
    }

    public void StopOverlay() => RemoveOverlay();

    void OnDisable() => RemoveOverlay();
    void OnDestroy() => RemoveOverlay();
}
