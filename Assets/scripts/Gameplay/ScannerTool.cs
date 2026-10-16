using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using System.Collections.Generic;

[DisallowMultipleComponent]
[RequireComponent(typeof(PickupItem))]
public class ScannerTool : MonoBehaviour
{
    private const int HeldToolOverlayLayer = 31;
    [Header("Hold pose - adjustable during Play")]
    [SerializeField, Range(0.05f, 0.5f)] private float heldScaleMultiplier = 0.5f;
    [SerializeField] private Vector3 heldPoseOffset = new Vector3(-0.25f, 0.08f, 0.05f);
    [SerializeField] private Vector3 heldPoseEuler;
    [SerializeField] private bool heldNowRuntime;

    [Header("Charge light")]
    [SerializeField, Min(0.05f)] private float chargeSeconds = 0.9f;
    [SerializeField] private Vector3 glowLocalPosition = new Vector3(0f, 0f, 0.18f);
    [SerializeField] private Color chargingGlowColor = new Color(1f, 0.72f, 0.12f);
    [SerializeField] private Color detectedGlowColor = new Color(1f, 0.12f, 0.08f);
    [SerializeField, Min(0f)] private float maxGlowIntensity = 4f;

    [SerializeField, Min(0.1f)] private float scanRadius = 1.6f;
    [SerializeField, Min(0.1f)] private float minimumWarningRadius = 0.9f;
    [SerializeField, Range(0.01f, 0.25f)] private float horizontalAimTolerance = 0.07f;
    [SerializeField, Range(0.01f, 0.25f)] private float verticalAimTolerance = 0.13f;
    [SerializeField, Min(0.1f)] private float glowRange = 1.8f;
    [SerializeField] private LayerMask scanLayers = ~0;

    private PickupItem item;
    private Renderer[] scannerRenderers;
    private Light chargeLight;
    private Transform chargeIndicator;
    private Material indicatorMaterial;
    private float chargeTime;
    private float redFlashUntil;
    private float nextWarningSoundTime;
    private bool wasCharging;
    private readonly Collider[] results = new Collider[32];
    private readonly Dictionary<GameObject, int> originalRendererLayers = new Dictionary<GameObject, int>();
    private Camera worldCamera;
    private Camera overlayCamera;
    private UniversalAdditionalCameraData worldCameraData;
    private int originalWorldCullingMask;
    private bool warnedNoOverlay;

    private void Awake()
    {
        // Extend depth reach from the old scene's effective 0.9 m, retaining
        // the same narrow on-screen horizontal/vertical aiming tolerances.
        if (Mathf.Approximately(scanRadius, .5f) || Mathf.Approximately(scanRadius, 1.25f))
            scanRadius = 1.6f;
        item = GetComponent<PickupItem>();
        // Older scene data still stores 0.78; keep the requested hand-held size at 0.5.
        heldScaleMultiplier = Mathf.Min(heldScaleMultiplier, 0.5f);
        item.SetHeldScaleMultiplier(heldScaleMultiplier);
        scannerRenderers = GetComponentsInChildren<Renderer>(true);
    }

    private void Start() => CreateChargeLight();

    private void Update()
    {
        if (!Mathf.Approximately(item.HeldScaleMultiplier, heldScaleMultiplier))
            item.SetHeldScaleMultiplier(heldScaleMultiplier);

        Mouse mouse = Mouse.current;
        bool charging = item.IsHeld && item.Owner != null && item.Owner.InputEnabled
            && MonitorViewManager.ActiveSession == null && Application.isFocused
            && mouse != null && mouse.leftButton.isPressed;
        ScannableAnomaly nearby = item.IsHeld && MonitorViewManager.ActiveSession == null
            ? FindNearbyAnomaly() : null;
        if (charging != wasCharging)
        {
            if (charging) ShiftAudioDirector.StartScannerCharge();
            else ShiftAudioDirector.StopScannerCharge();
            wasCharging = charging;
        }
        if (nearby != null && !nearby.Detected && Time.time >= nextWarningSoundTime)
        {
            ShiftAudioDirector.PlayScannerWarning();
            nextWarningSoundTime = Time.time + (charging ? .45f : .8f);
        }
        if (!charging)
        {
            chargeTime = 0f;
            UpdateChargeLight(nearby != null || Time.time < redFlashUntil ? 0.35f : 0f,
                nearby != null && nearby.Detected || Time.time < redFlashUntil);
            return;
        }

        chargeTime = Mathf.Min(Mathf.Max(0.05f, chargeSeconds), chargeTime + Time.deltaTime);
        float charge = chargeTime / Mathf.Max(0.05f, chargeSeconds);
        if (charge < 1f)
        {
            UpdateChargeLight(charge, nearby != null && nearby.Detected || Time.time < redFlashUntil);
            return;
        }

        if (nearby != null && !nearby.Detected)
        {
            nearby.Detect();
            ShiftAudioDirector.StopScannerCharge();
            ShiftAudioDirector.PlayScannerSuccess();
            redFlashUntil = Time.time + 2f;
        }
        UpdateChargeLight(charge, nearby != null && nearby.Detected || Time.time < redFlashUntil);
    }

    private ScannableAnomaly FindNearbyAnomaly()
    {
        Camera view = item.Owner != null ? item.Owner.InteractionCamera : null;
        if (view == null) return null;
        int count = Physics.OverlapSphereNonAlloc(transform.position,
            Mathf.Max(scanRadius, minimumWarningRadius), results,
            scanLayers, QueryTriggerInteraction.Collide);
        for (int i = 0; i < count; i++)
        {
            ScannableAnomaly anomaly = results[i].GetComponentInParent<ScannableAnomaly>();
            if (anomaly == null || anomaly.Revealed || !anomaly.CanDetect) continue;
            Vector3 screen = view.WorldToViewportPoint(results[i].bounds.center);
            if (screen.z <= 0f || Mathf.Abs(screen.x - 0.5f) > horizontalAimTolerance ||
                Mathf.Abs(screen.y - 0.5f) > verticalAimTolerance) continue;
            return anomaly;
        }
        return null;
    }

    private void CreateChargeLight()
    {
        GameObject lightObject = new GameObject("Scanner Charge Light (Runtime)");
        lightObject.transform.SetParent(transform, false);
        chargeLight = lightObject.AddComponent<Light>();
        chargeLight.type = LightType.Point;
        chargeLight.range = glowRange;
        chargeLight.shadows = LightShadows.None;
        chargeLight.enabled = false;

        GameObject indicator = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        indicator.name = "Scanner Charge Indicator (Runtime)";
        indicator.transform.SetParent(transform, false);
        chargeIndicator = indicator.transform;
        Collider indicatorCollider = indicator.GetComponent<Collider>();
        if (indicatorCollider != null) Destroy(indicatorCollider);
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        if (shader != null)
        {
            indicatorMaterial = new Material(shader);
            indicatorMaterial.color = chargingGlowColor;
            indicator.GetComponent<Renderer>().material = indicatorMaterial;
        }
        UpdateChargeLight(0f, false);
    }

    private void UpdateChargeLight(float charge, bool detected)
    {
        if (chargeLight == null) return;
        Color currentColor = detected ? detectedGlowColor : chargingGlowColor;
        chargeLight.transform.localPosition = glowLocalPosition;
        chargeLight.color = currentColor;
        chargeLight.range = glowRange;
        float flash = detected && Time.time < redFlashUntil
            ? (Mathf.Sin(Time.time * 26f) > 0f ? 1f : 0.15f) : 1f;
        chargeLight.intensity = maxGlowIntensity * charge * flash;
        chargeLight.enabled = item != null && item.IsHeld && charge > 0.01f;

        if (chargeIndicator == null) return;
        chargeIndicator.localPosition = glowLocalPosition;
        chargeIndicator.localScale = Vector3.one * Mathf.Lerp(0.015f, 0.065f, charge) * flash;
        chargeIndicator.gameObject.SetActive(item != null && item.IsHeld && charge > 0.01f);
        if (indicatorMaterial != null)
            indicatorMaterial.color = currentColor * Mathf.Lerp(0.3f, 1f, charge);
    }

    private void OnDestroy()
    {
        ShiftAudioDirector.StopScannerCharge();
        RemoveOverlayCamera();
        if (indicatorMaterial != null) Destroy(indicatorMaterial);
    }

    private void OnDisable()
    {
        ShiftAudioDirector.StopScannerCharge();
        wasCharging = false;
        RemoveOverlayCamera();
    }

    private void LateUpdate()
    {
        heldNowRuntime = item != null && item.IsHeld
            && MonitorViewManager.ActiveSession == null;
        if (!heldNowRuntime)
        {
            RemoveOverlayCamera();
            return;
        }
        EnsureOverlayCamera();
        transform.localRotation = Quaternion.Euler(item.HeldLocalEulerAngles + heldPoseEuler);
        CenterVisibleScanner();
        if (overlayCamera != null && worldCamera != null)
            overlayCamera.projectionMatrix = worldCamera.projectionMatrix;
    }

    private void EnsureOverlayCamera()
    {
        if (overlayCamera != null || item.Owner == null) return;
        Camera view = item.Owner.InteractionCamera;
        if (view == null) return;
        UniversalAdditionalCameraData baseData = view.GetUniversalAdditionalCameraData();
        List<Camera> cameraStack = baseData.cameraStack;
        if (cameraStack == null)
        {
            if (!warnedNoOverlay)
            {
                Debug.LogWarning("Scanner overlay needs a URP Base camera with camera stacking enabled.", this);
                warnedNoOverlay = true;
            }
            return;
        }

        worldCamera = view;
        worldCameraData = baseData;
        originalWorldCullingMask = view.cullingMask;

        GameObject overlayObject = new GameObject("Scanner Viewmodel Camera (Runtime)");
        overlayObject.transform.SetParent(view.transform, false);
        overlayCamera = overlayObject.AddComponent<Camera>();
        overlayCamera.CopyFrom(view);
        overlayCamera.transform.localPosition = Vector3.zero;
        overlayCamera.transform.localRotation = Quaternion.identity;
        overlayCamera.clearFlags = CameraClearFlags.Nothing;
        overlayCamera.cullingMask = 1 << HeldToolOverlayLayer;
        overlayCamera.depth = view.depth + 1f;
        UniversalAdditionalCameraData overlayData = overlayCamera.GetUniversalAdditionalCameraData();
        overlayData.renderType = CameraRenderType.Overlay;
        cameraStack.Add(overlayCamera);

        foreach (Renderer modelRenderer in GetComponentsInChildren<Renderer>(true))
        {
            GameObject part = modelRenderer.gameObject;
            if (!originalRendererLayers.ContainsKey(part))
                originalRendererLayers.Add(part, part.layer);
            part.layer = HeldToolOverlayLayer;
        }
        view.cullingMask &= ~(1 << HeldToolOverlayLayer);
    }

    private void RemoveOverlayCamera()
    {
        if (worldCameraData != null && worldCameraData.cameraStack != null && overlayCamera != null)
            worldCameraData.cameraStack.Remove(overlayCamera);
        if (worldCamera != null)
            worldCamera.cullingMask = originalWorldCullingMask;
        foreach (KeyValuePair<GameObject, int> entry in originalRendererLayers)
            if (entry.Key != null) entry.Key.layer = entry.Value;
        originalRendererLayers.Clear();
        if (overlayCamera != null) Destroy(overlayCamera.gameObject);
        overlayCamera = null;
        worldCamera = null;
        worldCameraData = null;
        if (item == null || !item.IsHeld) warnedNoOverlay = false;
    }

    private void CenterVisibleScanner()
    {
        Transform holdPoint = item.Owner != null ? item.Owner.HoldPoint : null;
        if (holdPoint == null) return;

        Bounds bounds = default;
        bool hasBounds = false;
        foreach (Renderer scannerRenderer in scannerRenderers)
        {
            if (scannerRenderer == null || !scannerRenderer.enabled || !scannerRenderer.gameObject.activeInHierarchy)
                continue;
            if (!hasBounds)
            {
                bounds = scannerRenderer.bounds;
                hasBounds = true;
            }
            else bounds.Encapsulate(scannerRenderer.bounds);
        }
        if (hasBounds)
            transform.position += holdPoint.TransformPoint(item.HeldLocalPosition
                    + heldPoseOffset + item.PickupPresentationOffset)
                - bounds.center;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, scanRadius);
    }
}
