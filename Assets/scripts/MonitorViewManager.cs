using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Coordinates standing, seated overview and focused-feed views without moving the player.
/// A session captures and restores input, cursor, held equipment and camera priority.
/// </summary>
[DefaultExecutionOrder(-50)]
[DisallowMultipleComponent]
[RequireComponent(typeof(MonitorCameraController))]
public class MonitorViewManager : MonoBehaviour
{
    public enum ViewMode { Standing, Overview, Focused }
    public static MonitorViewManager ActiveSession { get; private set; }
    private static int escapeHandledFrame = -1;
    public static bool HandledEscapeThisFrame => escapeHandledFrame == Time.frameCount;
    public ViewMode Mode { get; private set; } = ViewMode.Standing;
    public bool IsSeated => Mode != ViewMode.Standing;
    public bool IsFocused => Mode == ViewMode.Focused;
    public int FocusedIndex { get; private set; } = -1;
    public Transform OverviewPose => overviewPose;

    [Header("Cinemachine 摄像机（不是三个监控 Camera）")]
    [SerializeField] private CinemachineCamera playerCamera;
    [SerializeField] private CinemachineCamera seatedCamera;
    [SerializeField] private int seatedPriority = 100;
    [Header("固定坐姿视点")]
    [SerializeField] private Transform overviewPose;
    [SerializeField, Range(1f, 179f)] private float overviewFieldOfView = 60f;
    [SerializeField, Range(1f, 179f)] private float focusFieldOfView = 40f;
    [SerializeField, Range(0.5f, 4f)] private float screenViewingDistance = 1.6f;
    [SerializeField, Min(0.01f)] private float viewSmoothing = 8f;
    [Header("坐姿轻微头部晃动")]
    [SerializeField, Min(0f)] private float swayFrequency = 0.3f;
    [SerializeField] private Vector2 swayPosition = new Vector2(0.002f, 0.003f);
    [SerializeField] private Vector3 swayDegrees = new Vector3(0.15f, 0.2f, 0.1f);
    [SerializeField, Range(0f, 1f)] private float focusedSwayMultiplier = 0.25f;

    private MonitorCameraController cameraControl;
    private playerController activePlayer;
    private FirstPersonCameraController activeLook;
    private bool savedPlayerInput;
    private bool savedLookInput;
    private CursorLockMode savedCursorLock;
    private bool savedCursorVisible;
    private PrioritySettings savedPriority;
    private Vector3 savedCameraPosition;
    private Quaternion savedCameraRotation;
    private float savedFieldOfView;
    private Vector3 viewPosition;
    private const float SeatedYaw = 90f;
    private float viewPitch;
    private float viewRoll;
    private float swayTime;
    private int entryFrame;
    private Renderer focusedScreen;
    private PickupItem seatedHeldItem;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetEscapeState() => escapeHandledFrame = -1;

    private void Awake()
    {
        cameraControl = GetComponent<MonitorCameraController>();
        // Only disable our dedicated seated camera; never disable the player's camera.
        if (seatedCamera != null && seatedCamera != playerCamera) seatedCamera.enabled = false;
    }

    public bool BeginSession(playerController player, FirstPersonCameraController look)
    {
        if (!isActiveAndEnabled || IsSeated || ActiveSession != null) return false;
        if (!IsConfigured() || player == null || look == null || !player.isActiveAndEnabled || !look.isActiveAndEnabled)
        {
            Debug.LogError($"{name}: 监控引用不完整。请检查两台 CinemachineCamera、坐姿视点、三个监控 Camera 和人物控制脚本。", this);
            return false;
        }
        ActiveSession = this;
        activePlayer = player;
        activeLook = look;
        PlayerInteractor interactor = player.GetComponentInChildren<PlayerInteractor>(true);
        seatedHeldItem = interactor != null ? interactor.HeldItem : null;
        if (seatedHeldItem != null) seatedHeldItem.SetViewHidden(true);
        savedPlayerInput = player.InputEnabled;
        savedLookInput = look.InputEnabled;
        savedCursorLock = Cursor.lockState;
        savedCursorVisible = Cursor.visible;
        player.InputEnabled = false;
        look.InputEnabled = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        savedPriority = seatedCamera.Priority;
        savedCameraPosition = seatedCamera.transform.position;
        savedCameraRotation = seatedCamera.transform.rotation;
        savedFieldOfView = seatedCamera.Lens.FieldOfView;
        viewPosition = overviewPose.position;
        viewPitch = overviewPose.eulerAngles.x;
        viewRoll = overviewPose.eulerAngles.z;
        seatedCamera.transform.SetPositionAndRotation(viewPosition, Quaternion.Euler(viewPitch, SeatedYaw, viewRoll));
        seatedCamera.Lens.FieldOfView = overviewFieldOfView;
        seatedCamera.Priority = Mathf.Max(seatedPriority, playerCamera.Priority.Value + 1);
        seatedCamera.enabled = true;
        seatedCamera.Prioritize();
        Mode = ViewMode.Overview;
        FocusedIndex = -1;
        swayTime = 0f;
        entryFrame = Time.frameCount;
        return true;
    }

    private bool IsConfigured()
    {
        if (playerCamera == null || seatedCamera == null || playerCamera == seatedCamera
            || !playerCamera.isActiveAndEnabled || !seatedCamera.gameObject.activeInHierarchy
            || overviewPose == null
            || cameraControl == null || !cameraControl.isActiveAndEnabled || !cameraControl.HasValidCameras) return false;
        if (overviewPose == seatedCamera.transform || overviewPose.IsChildOf(seatedCamera.transform)) return false;
        return true;
    }

    private void Update()
    {
        if (!IsSeated) return;
        if (activePlayer == null || activeLook == null || !activePlayer.isActiveAndEnabled
            || !activeLook.isActiveAndEnabled || !IsConfigured() || !seatedCamera.enabled)
        {
            EndSession();
            return;
        }
        Keyboard keyboard = Keyboard.current;
        if (Application.isFocused && keyboard != null && Time.frameCount != entryFrame)
        {
            // Consume only one action per frame so Escape never skips the overview.
            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                escapeHandledFrame = Time.frameCount;
                if (IsFocused) { Mode = ViewMode.Overview; FocusedIndex = -1; focusedScreen = null; }
                else EndSession();
            }
        }
        if (IsSeated) UpdateView();
    }

    private void Focus(int index)
    {
        FocusedIndex = index;
        Mode = ViewMode.Focused;
    }

    public void FocusScreen(int index, Renderer screen)
    {
        if (!IsSeated || screen == null || index < 0 || index >= 3) return;
        focusedScreen = screen;
        Focus(index);
        ShiftAudioDirector.PlaySwitchCamera();
    }

    public void ReturnToOverview()
    {
        if (!IsSeated) return;
        Mode = ViewMode.Overview;
        FocusedIndex = -1;
        focusedScreen = null;
    }

    private void UpdateView()
    {
        // Give the move into a chosen feed a little more weight than the return.
        float smoothing = IsFocused ? viewSmoothing * 0.7f : viewSmoothing;
        float t = 1f - Mathf.Exp(-smoothing * Time.deltaTime);
        Vector3 targetPosition = overviewPose.position;
        Quaternion targetRotation = Quaternion.Euler(overviewPose.eulerAngles.x, SeatedYaw,
            overviewPose.eulerAngles.z);
        if (IsFocused && focusedScreen != null)
        {
            Vector3 screenCenter = focusedScreen.bounds.center;
            Vector3 towardSeat = overviewPose.position - screenCenter;
            towardSeat.y = 0f;
            if (towardSeat.sqrMagnitude < 0.01f) towardSeat = Vector3.left;
            // The imported monitor surface is a thin X/Z plane, so its local up is
            // the screen normal. Face each side monitor head-on, toward the seat.
            Vector3 screenNormal = focusedScreen.transform.up;
            if (Mathf.Abs(Vector3.Dot(screenNormal.normalized, towardSeat.normalized)) < 0.2f)
                screenNormal = towardSeat.normalized;
            else if (Vector3.Dot(screenNormal, towardSeat) < 0f)
                screenNormal = -screenNormal;
            targetPosition = screenCenter + screenNormal.normalized * screenViewingDistance;
            targetRotation = Quaternion.LookRotation(screenCenter - targetPosition, Vector3.up);
        }
        viewPosition = Vector3.Lerp(viewPosition, targetPosition, t);
        Quaternion viewRotation = Quaternion.Slerp(seatedCamera.transform.rotation,
            targetRotation, t);
        seatedCamera.Lens.FieldOfView = Mathf.Lerp(seatedCamera.Lens.FieldOfView,
            IsFocused ? focusFieldOfView : overviewFieldOfView, t);
        swayTime += Time.deltaTime * swayFrequency * Mathf.PI * 2f;
        float amount = IsFocused ? focusedSwayMultiplier : 1f;
        Vector3 offset = new Vector3(Mathf.Sin(swayTime) * swayPosition.x,
            Mathf.Sin(swayTime * 1.37f) * swayPosition.y, 0f) * amount;
        Vector3 angles = new Vector3(Mathf.Sin(swayTime * 1.13f) * swayDegrees.x,
            Mathf.Sin(swayTime) * swayDegrees.y, Mathf.Sin(swayTime * 0.73f) * swayDegrees.z) * amount;
        seatedCamera.transform.SetPositionAndRotation(viewPosition + viewRotation * offset,
            viewRotation * Quaternion.Euler(angles));
    }

    public void EndSession()
    {
        if (!IsSeated) return;
        if (seatedHeldItem != null) seatedHeldItem.SetViewHidden(false);
        seatedHeldItem = null;
        Mode = ViewMode.Standing;
        FocusedIndex = -1;
        focusedScreen = null;
        if (seatedCamera != null)
        {
            seatedCamera.enabled = false;
            seatedCamera.Priority = savedPriority;
            seatedCamera.transform.SetPositionAndRotation(savedCameraPosition, savedCameraRotation);
            seatedCamera.Lens.FieldOfView = savedFieldOfView;
        }
        if (activePlayer != null) activePlayer.InputEnabled = savedPlayerInput;
        if (activeLook != null) activeLook.InputEnabled = savedLookInput;
        Cursor.lockState = savedCursorLock;
        Cursor.visible = savedCursorVisible;
        activePlayer = null;
        activeLook = null;
        if (ActiveSession == this) ActiveSession = null;
    }

    private void OnDisable() => EndSession();
}
