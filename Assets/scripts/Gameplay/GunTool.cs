using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

[DisallowMultipleComponent]
[RequireComponent(typeof(PickupItem))]
public class GunTool : MonoBehaviour
{
    private const int HeldToolOverlayLayer = 31;
    [Header("Shot")]
    [SerializeField, Min(0.1f)] private float range = 30f;
    [SerializeField] private LayerMask hitLayers = ~0;
    [SerializeField, Min(0f)] private float secondsBetweenShots = 0.25f;
    [SerializeField] private AudioSource shotAudio;
    [Header("Shot tracer")]
    [SerializeField] private Transform muzzlePoint;
    [SerializeField] private Color tracerColor = new Color(1f, 0.94f, 0.78f, 0.8f);
    [SerializeField, Min(0.005f)] private float tracerWidth = 0.012f;
    [SerializeField, Min(0.02f)] private float tracerSeconds = 0.1f;
    [SerializeField, Min(0.1f)] private float tracerSegmentLength = 0.75f;

    [Header("Hold pose - adjustable during Play")]
    [SerializeField, Range(0.05f, 1f)] private float heldScaleMultiplier = 0.6f;
    [SerializeField] private Vector3 idlePoseOffset = new Vector3(-0.3f, 0.1f, 0.08f);
    [SerializeField] private Vector3 idlePoseEuler = new Vector3(-28f, 0f, 90f);

    [Header("Charge / aim pose - adjustable during Play")]
    [SerializeField] private Vector3 aimPoseOffset = new Vector3(-0.37f, 0.18f, 0.17f);
    [SerializeField] private Vector3 aimPoseEuler = Vector3.zero;
    [SerializeField] private bool previewAimPose;
    [SerializeField, Min(0.05f)] private float minimumChargeSeconds = 0.25f;
    [SerializeField, Min(0.05f)] private float fullChargeSeconds = 0.65f;
    [SerializeField, Min(1f)] private float poseSpeed = 14f;
    [SerializeField, Min(0.01f)] private float recoilSeconds = 0.14f;

    [Header("Charge glow")]
    [SerializeField] private Vector3 glowLocalPosition = new Vector3(0f, 0f, 0.18f);
    [SerializeField] private Color glowColor = new Color(0.1f, 0.75f, 1f);
    [SerializeField, Min(0f)] private float maxGlowIntensity = 3f;

    private PickupItem item;
    private Renderer[] gunRenderers;
    private Light chargeGlow;
    private float nextShotTime;
    private float chargeStartedAt;
    private float recoilUntil;
    private bool charging;
    private bool wasHeld;
    private Camera worldCamera;
    private Camera overlayCamera;
    private UniversalAdditionalCameraData worldCameraData;
    private int originalWorldCullingMask;
    private bool warnedNoOverlay;
    private readonly Dictionary<GameObject, int> originalRendererLayers = new Dictionary<GameObject, int>();

    private void Awake()
    {
        item = GetComponent<PickupItem>();
        item.SetHeldScaleMultiplier(heldScaleMultiplier);
        gunRenderers = GetComponentsInChildren<Renderer>(true);
        GameObject glowObject = new GameObject("Charge Glow (Runtime)");
        glowObject.transform.SetParent(transform, false);
        glowObject.transform.localPosition = glowLocalPosition;
        chargeGlow = glowObject.AddComponent<Light>();
        chargeGlow.type = LightType.Point;
        chargeGlow.color = glowColor;
        chargeGlow.range = 1.25f;
        chargeGlow.shadows = LightShadows.None;
        chargeGlow.intensity = 0f;
        chargeGlow.enabled = false;
    }

    private void Update()
    {
        if (!Mathf.Approximately(item.HeldScaleMultiplier, heldScaleMultiplier))
            item.SetHeldScaleMultiplier(heldScaleMultiplier);
        if (!item.IsHeld || item.Owner == null)
        {
            charging = false;
            wasHeld = false;
            recoilUntil = 0f;
            UpdateGlow(0f);
            return;
        }

        if (!wasHeld)
        {
            transform.localRotation = Quaternion.Euler(idlePoseEuler);
            wasHeld = true;
        }

        Mouse mouse = Mouse.current;
        bool canFire = item.Owner.InputEnabled && MonitorViewManager.ActiveSession == null
            && mouse != null;
        if (!canFire)
        {
            charging = false;
            if (MonitorViewManager.ActiveSession != null && chargeGlow != null)
            {
                chargeGlow.intensity = 0f;
                chargeGlow.enabled = false;
            }
        }
        else if (mouse.leftButton.wasPressedThisFrame && Time.time >= nextShotTime)
        {
            charging = true;
            chargeStartedAt = Time.time;
        }
        else if (charging && mouse.leftButton.wasReleasedThisFrame)
        {
            float heldSeconds = Time.time - chargeStartedAt;
            charging = false;
            if (heldSeconds >= minimumChargeSeconds)
            {
                nextShotTime = Time.time + secondsBetweenShots;
                recoilUntil = Time.time + recoilSeconds;
                Fire();
            }
        }
        else if (charging && !mouse.leftButton.isPressed)
            charging = false;

        float charge = charging
            ? Mathf.Clamp01((Time.time - chargeStartedAt) / Mathf.Max(0.05f, fullChargeSeconds))
            : 0f;
        float poseBlend = previewAimPose ? 1f
            : charging ? Mathf.SmoothStep(0f, 1f, charge)
            : Time.time < recoilUntil ? 1f : 0f;
        Quaternion desiredRotation = Quaternion.Slerp(
            Quaternion.Euler(idlePoseEuler), Quaternion.Euler(aimPoseEuler), poseBlend);
        if (Time.time < recoilUntil && !charging)
            desiredRotation *= Quaternion.Euler(-6f, 0f, 0f);
        transform.localRotation = Quaternion.Slerp(transform.localRotation, desiredRotation,
            Mathf.Clamp01(Time.deltaTime * poseSpeed));
        CenterVisibleGun(Vector3.Lerp(idlePoseOffset, aimPoseOffset, poseBlend)
            + item.PickupPresentationOffset);
        UpdateGlow(previewAimPose ? 1f : charge);
    }

    private void UpdateGlow(float charge)
    {
        if (chargeGlow == null) return;
        chargeGlow.transform.localPosition = glowLocalPosition;
        chargeGlow.color = glowColor;
        chargeGlow.intensity = Mathf.MoveTowards(chargeGlow.intensity,
            maxGlowIntensity * charge * charge, Time.deltaTime * 10f);
        chargeGlow.enabled = chargeGlow.intensity > 0.01f && item.IsHeld;
    }

    private void CenterVisibleGun(Vector3 offsetFromHoldPoint)
    {
        Transform holdPoint = item.Owner.HoldPoint;
        if (holdPoint == null) return;

        Bounds bounds = default;
        bool hasBounds = false;
        foreach (Renderer gunRenderer in gunRenderers)
        {
            if (gunRenderer == null || !gunRenderer.enabled || !gunRenderer.gameObject.activeInHierarchy)
                continue;
            if (!hasBounds)
            {
                bounds = gunRenderer.bounds;
                hasBounds = true;
            }
            else bounds.Encapsulate(gunRenderer.bounds);
        }
        if (hasBounds)
            transform.position += holdPoint.TransformPoint(offsetFromHoldPoint) - bounds.center;
    }

    private void LateUpdate()
    {
        if (item == null || !item.IsHeld || item.Owner == null
            || MonitorViewManager.ActiveSession != null)
        {
            RemoveOverlayCamera();
            return;
        }
        EnsureOverlayCamera();
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
                Debug.LogWarning("Gun overlay needs a URP Base camera with camera stacking enabled.", this);
                warnedNoOverlay = true;
            }
            return;
        }

        worldCamera = view;
        worldCameraData = baseData;
        originalWorldCullingMask = view.cullingMask;

        GameObject overlayObject = new GameObject("Gun Viewmodel Camera (Runtime)");
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

        foreach (Renderer modelRenderer in gunRenderers)
        {
            if (modelRenderer == null) continue;
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

    private void OnDisable() => RemoveOverlayCamera();
    private void OnDestroy() => RemoveOverlayCamera();

    private void Fire()
    {
        if (shotAudio != null) shotAudio.Play();
        else ShiftAudioDirector.PlayGunFallback();
        Camera camera = item.Owner.InteractionCamera;
        if (camera == null) return;
        Ray ray = camera.ViewportPointToRay(new Vector3(0.5f, 0.5f));
        Vector3 endPoint = ray.GetPoint(range);
        RaycastHit[] hits = Physics.RaycastAll(ray, range, hitLayers, QueryTriggerInteraction.Collide);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.transform.IsChildOf(item.Owner.transform.root)) continue;
            IGunHitTarget target = hit.collider.GetComponentInParent<IGunHitTarget>();
            // Only a warning ghost's trigger receives bullets. Other trigger
            // volumes remain transparent to shots, just as before.
            if (hit.collider.isTrigger && !(target is PressureGhostTarget)) continue;
            endPoint = hit.point;
            if (target != null) target.Hit();
            break;
        }
        ShowTracer(camera, endPoint);
    }

    private void ShowTracer(Camera camera, Vector3 endPoint)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        if (shader == null) return;

        GameObject visual = new GameObject("Shot Tracer (Runtime)");
        LineRenderer line = visual.AddComponent<LineRenderer>();
        Material material = new Material(shader) { name = "Shot Tracer (Runtime)" };
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
        if (material.HasProperty("_Color")) material.SetColor("_Color", Color.white);
        line.sharedMaterial = material;
        line.useWorldSpace = true;
        line.positionCount = 2;
        line.widthMultiplier = tracerWidth;
        line.widthCurve = new AnimationCurve(new Keyframe(0f, 1f),
            new Keyframe(1f, 0.18f));
        line.alignment = LineAlignment.View;
        line.numCapVertices = 4;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.startColor = tracerColor;
        line.endColor = tracerColor;
        Vector3 start = muzzlePoint != null ? muzzlePoint.position
            : camera.transform.position + camera.transform.forward * 0.38f
                + camera.transform.right * 0.1f - camera.transform.up * 0.09f;
        line.SetPosition(0, start);
        line.SetPosition(1, start);
        StartCoroutine(FadeTracer(visual, line, material, start, endPoint));
    }

    private IEnumerator FadeTracer(GameObject visual, LineRenderer line, Material material,
        Vector3 start, Vector3 end)
    {
        float elapsed = 0f;
        Vector3 travel = end - start;
        float distance = travel.magnitude;
        Vector3 direction = distance > 0.001f ? travel / distance : Vector3.forward;
        while (elapsed < tracerSeconds && line != null)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / tracerSeconds);
            float traveled = distance * progress;
            line.SetPosition(0, start + direction * Mathf.Max(0f,
                traveled - tracerSegmentLength));
            line.SetPosition(1, start + direction * traveled);
            Color color = tracerColor;
            color.a *= 1f - progress * progress;
            line.startColor = color;
            line.endColor = color;
            yield return null;
        }
        if (visual != null) Destroy(visual);
        if (material != null) Destroy(material);
    }
}
