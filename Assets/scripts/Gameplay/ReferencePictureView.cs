using System;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Click a fixed reference picture to approach it; Escape/right click returns.</summary>
[DefaultExecutionOrder(-70)]
[DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(BoxCollider))]
public class ReferencePictureView : MonoBehaviour
{
    private const string OutageSignal = "ghost_interlude_complete";
    private const string RestoreSignal = "power_restored";
    private const string FinalBlackoutSignal = "final_protocol_unlocked";
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    [Header("查看画像")]
    [SerializeField, Min(.1f)] private float clickDistance = 2.5f;
    [SerializeField, Range(20f, 70f)] private float viewingFieldOfView = 40f;
    [SerializeField, Min(.1f)] private float transitionSeconds = .45f;
    [Tooltip("可选：在画像前放一个空物体，蓝色箭头朝向画像，可自定查看位置和角度。")]
    [SerializeField] private Transform viewingPose;

    public static ReferencePictureView ActiveView { get; private set; }
    private Camera viewCamera;
    private CinemachineBrain brain;
    private playerController player;
    private FirstPersonCameraController look;
    private PlayerInteractor interactor;
    private PickupItem heldItem;
    private bool savedPlayer, savedLook, savedInteractor, savedBrain, savedCursorVisible;
    private CursorLockMode savedCursor;
    private Vector3 returnPosition, startPosition, targetPosition;
    private Quaternion returnRotation, startRotation, targetRotation;
    private float returnFov, startFov, targetFov, elapsed;
    private bool returning;
    private Renderer pictureRenderer;
    private Color[] originalMaterialColors;
    private bool[] usesBaseColor;
    private MaterialPropertyBlock pictureProperties;
    private GameplaySignals lightingSignals;

    private void OnEnable()
    {
        CapturePictureMaterials();
        SubscribeLighting();
        SyncPictureBrightness();
    }

    private void Start()
    {
        // Also cover scene objects whose signal source was initialized later.
        SubscribeLighting();
        SyncPictureBrightness();
    }

    private void CapturePictureMaterials()
    {
        pictureRenderer = GetComponent<Renderer>();
        if (pictureRenderer == null) return;
        Material[] materials = pictureRenderer.sharedMaterials;
        originalMaterialColors = new Color[materials.Length];
        usesBaseColor = new bool[materials.Length];
        for (int i = 0; i < materials.Length; i++)
        {
            Material material = materials[i];
            if (material == null) continue;
            usesBaseColor[i] = material.HasProperty(BaseColorId);
            if (usesBaseColor[i]) originalMaterialColors[i] = material.GetColor(BaseColorId);
            else if (material.HasProperty(ColorId)) originalMaterialColors[i] = material.GetColor(ColorId);
        }
        pictureProperties = new MaterialPropertyBlock();
    }

    private void SubscribeLighting()
    {
        GameplaySignals source = GameplaySignals.Instance;
        if (source == null || source == lightingSignals) return;
        if (lightingSignals != null) lightingSignals.SignalRaised -= OnLightingSignal;
        lightingSignals = source;
        lightingSignals.SignalRaised += OnLightingSignal;
    }

    private void OnLightingSignal(string signal)
    {
        if (signal == OutageSignal || signal == RestoreSignal || signal == FinalBlackoutSignal)
            SyncPictureBrightness();
    }

    private void SyncPictureBrightness()
    {
        if (pictureRenderer == null || originalMaterialColors == null) return;
        GameplaySignals source = GameplaySignals.Instance;
        bool finalBlackout = source != null && source.HasRaised(FinalBlackoutSignal);
        bool outage = source != null && source.HasRaised(OutageSignal)
            && !source.HasRaised(RestoreSignal);
        float brightness = finalBlackout ? .015f : outage ? .045f : 1f;
        Material[] materials = pictureRenderer.sharedMaterials;
        for (int i = 0; i < Mathf.Min(originalMaterialColors.Length, materials.Length); i++)
        {
            Material material = materials[i];
            if (material == null) continue;
            int colorId = usesBaseColor[i] ? BaseColorId : ColorId;
            if (!material.HasProperty(colorId)) continue;
            Color color = originalMaterialColors[i];
            pictureRenderer.GetPropertyBlock(pictureProperties, i);
            pictureProperties.SetColor(colorId,
                new Color(color.r * brightness, color.g * brightness,
                    color.b * brightness, color.a));
            pictureRenderer.SetPropertyBlock(pictureProperties, i);
        }
    }

    public bool IsAimedAt(Camera camera)
    {
        if (!isActiveAndEnabled || camera == null || ActiveView != null
            || MonitorViewManager.ActiveSession != null) return false;
        Ray ray = camera.ViewportPointToRay(new Vector3(.5f, .5f));
        RaycastHit[] hits = Physics.RaycastAll(ray, clickDistance, ~0, QueryTriggerInteraction.Ignore);
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.transform.IsChildOf(camera.transform.root)) continue;
            return hit.collider.GetComponentInParent<ReferencePictureView>() == this;
        }
        return false;
    }

    // Also supports existing scene instances dragged in before the prefab was updated.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession() => ActiveView = null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void ConnectExistingPictures()
    {
        foreach (MeshFilter filter in FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
            if (filter.sharedMesh != null && filter.sharedMesh.name.StartsWith("CAM_", StringComparison.Ordinal)
                && filter.sharedMesh.name.Contains("WallReference") && !filter.GetComponent<ReferencePictureView>())
                filter.gameObject.AddComponent<ReferencePictureView>();
    }

    private void Update()
    {
        if (!Application.isFocused) return;
        if (ActiveView == this)
        {
            if (!returning && (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame
                || Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)) StartReturn();
            return;
        }
        if (ActiveView != null || MonitorViewManager.ActiveSession != null || Mouse.current == null
            || !Mouse.current.leftButton.wasPressedThisFrame) return;
        if (interactor == null) interactor = FindAnyObjectByType<PlayerInteractor>();
        if (interactor == null || !interactor.InputEnabled) return;
        viewCamera = interactor.InteractionCamera;
        if (viewCamera == null) return;
        player = interactor.GetComponentInParent<playerController>();
        if (player == null) player = FindAnyObjectByType<playerController>();
        if (player == null || !player.InputEnabled) return;
        Ray ray = Cursor.lockState == CursorLockMode.Locked
            ? viewCamera.ViewportPointToRay(new Vector3(.5f, .5f))
            : viewCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
        var hits = Physics.RaycastAll(ray, clickDistance, ~0, QueryTriggerInteraction.Ignore);
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (var hit in hits)
        {
            if (hit.collider.transform.IsChildOf(player.transform)) continue;
            if (hit.collider.GetComponentInParent<ReferencePictureView>() == this) BeginView();
            break; // Walls and furniture block the click.
        }
    }

    private void BeginView()
    {
        ActiveView = this;
        returning = false;
        elapsed = 0;
        look = player.GetComponentInChildren<FirstPersonCameraController>();
        brain = viewCamera.GetComponent<CinemachineBrain>();
        returnPosition = startPosition = viewCamera.transform.position;
        returnRotation = startRotation = viewCamera.transform.rotation;
        returnFov = startFov = viewCamera.fieldOfView;
        savedPlayer = player.InputEnabled;
        savedInteractor = interactor.InputEnabled;
        savedLook = look != null && look.InputEnabled;
        savedBrain = brain != null && brain.enabled;
        savedCursor = Cursor.lockState;
        savedCursorVisible = Cursor.visible;
        player.InputEnabled = interactor.InputEnabled = false;
        if (look != null) look.InputEnabled = false;
        if (brain != null) brain.enabled = false;
        heldItem = interactor.HeldItem;
        if (heldItem != null) heldItem.SetViewHidden(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        targetFov = viewingFieldOfView;
        if (viewingPose != null)
        {
            targetPosition = viewingPose.position;
            targetRotation = viewingPose.rotation;
            return;
        }
        Bounds bounds = GetComponent<MeshFilter>().sharedMesh.bounds;
        Vector3 center = transform.TransformPoint(bounds.center);
        Vector3 size = bounds.size;
        // Imported source frames have XY faces; also tolerate another thin-axis orientation.
        int thin = size.x < size.y && size.x < size.z ? 0 : size.y < size.z ? 1 : 2;
        Vector3 normal = transform.TransformDirection(thin == 0 ? Vector3.right : thin == 1 ? Vector3.up : Vector3.forward).normalized;
        if (Vector3.Dot(normal, returnPosition - center) < 0) normal = -normal;
        Vector3 up = Vector3.ProjectOnPlane(Vector3.up, normal).normalized;
        if (up.sqrMagnitude < .01f) up = transform.up;
        Vector3 right = Vector3.Cross(up, normal).normalized;
        float width = 0, height = 0;
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = transform.TransformPoint(bounds.center + Vector3.Scale(bounds.extents,
                new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1))) - center;
            width = Mathf.Max(width, Mathf.Abs(Vector3.Dot(corner, right)) * 2);
            height = Mathf.Max(height, Mathf.Abs(Vector3.Dot(corner, up)) * 2);
        }
        float tangent = Mathf.Tan(viewingFieldOfView * Mathf.Deg2Rad * .5f);
        float distance = Mathf.Max(height / (2 * tangent), width / (2 * tangent * viewCamera.aspect)) * 1.15f;
        targetPosition = center + normal * Mathf.Max(.25f, distance);
        targetRotation = Quaternion.LookRotation(-normal, up);
    }

    private void StartReturn()
    {
        returning = true;
        elapsed = 0;
        startPosition = viewCamera.transform.position;
        startRotation = viewCamera.transform.rotation;
        startFov = viewCamera.fieldOfView;
        targetPosition = returnPosition;
        targetRotation = returnRotation;
        targetFov = returnFov;
    }

    private void LateUpdate()
    {
        if (ActiveView != this) return;
        if (viewCamera == null || player == null) { Restore(); return; }
        elapsed += Time.unscaledDeltaTime;
        float t = Mathf.SmoothStep(0, 1, Mathf.Clamp01(elapsed / transitionSeconds));
        viewCamera.transform.SetPositionAndRotation(Vector3.Lerp(startPosition, targetPosition, t),
            Quaternion.Slerp(startRotation, targetRotation, t));
        viewCamera.fieldOfView = Mathf.Lerp(startFov, targetFov, t);
        if (returning && elapsed >= transitionSeconds) Restore();
    }

    private void Restore()
    {
        if (ActiveView != this) return;
        if (viewCamera != null) { viewCamera.transform.SetPositionAndRotation(returnPosition, returnRotation); viewCamera.fieldOfView = returnFov; }
        if (brain != null) brain.enabled = savedBrain;
        if (player != null) player.InputEnabled = savedPlayer;
        if (look != null) look.InputEnabled = savedLook;
        if (interactor != null) interactor.InputEnabled = savedInteractor;
        if (heldItem != null) heldItem.SetViewHidden(false);
        Cursor.lockState = savedCursor;
        Cursor.visible = savedCursorVisible;
        ActiveView = null;
        if (returning) GameplaySignals.Instance?.Raise("reference_picture_viewed");
    }

    private void OnDisable()
    {
        if (lightingSignals != null) lightingSignals.SignalRaised -= OnLightingSignal;
        lightingSignals = null;
        // Property blocks affect only this picture instance, never the shared assets.
        if (pictureRenderer != null && originalMaterialColors != null)
        {
            Material[] materials = pictureRenderer.sharedMaterials;
            for (int i = 0; i < Mathf.Min(originalMaterialColors.Length, materials.Length); i++)
            {
                Material material = materials[i];
                if (material == null) continue;
                int colorId = usesBaseColor[i] ? BaseColorId : ColorId;
                if (!material.HasProperty(colorId)) continue;
                pictureRenderer.GetPropertyBlock(pictureProperties, i);
                pictureProperties.SetColor(colorId, originalMaterialColors[i]);
                pictureRenderer.SetPropertyBlock(pictureProperties, i);
            }
        }
        Restore();
    }
}
