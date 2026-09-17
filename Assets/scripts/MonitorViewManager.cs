using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(-50)]
[DisallowMultipleComponent]
[RequireComponent(typeof(MonitorCameraController))]
public class MonitorViewManager : MonoBehaviour
{
    public enum ViewMode { Standing, Overview, Focused }
    public static MonitorViewManager ActiveSession { get; private set; }
    public ViewMode Mode { get; private set; } = ViewMode.Standing;
    public bool IsSeated => Mode != ViewMode.Standing;
    public bool IsFocused => Mode == ViewMode.Focused;
    public int FocusedIndex { get; private set; } = -1;

    [Header("Cinemachine 摄像机（不是三个监控 Camera）")]
    [SerializeField] private CinemachineCamera playerCamera;
    [SerializeField] private CinemachineCamera seatedCamera;
    [SerializeField] private int seatedPriority = 100;
    [Header("固定视点：位置 + X/Z 旋转；世界 Y 固定 90°")]
    [SerializeField] private Transform overviewPose;
    [Tooltip("固定顺序：左、中、右。摆在屏幕前；视点的 Y 旋转会被忽略，统一朝世界 Y=90°。")]
    [SerializeField] private Transform[] focusPoses = new Transform[3];
    [SerializeField, Range(1f, 179f)] private float overviewFieldOfView = 60f;
    [SerializeField, Range(1f, 179f)] private float focusFieldOfView = 40f;
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
            Debug.LogError($"{name}: 监控引用不完整。请检查两台 CinemachineCamera、四个视点、三个监控 Camera 和人物控制脚本。", this);
            return false;
        }
        ActiveSession = this;
        activePlayer = player;
        activeLook = look;
        savedPlayerInput = player.InputEnabled;
        savedLookInput = look.InputEnabled;
        player.InputEnabled = false;
        look.InputEnabled = false;
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
            || overviewPose == null || focusPoses == null || focusPoses.Length != 3
            || cameraControl == null || !cameraControl.isActiveAndEnabled || !cameraControl.HasValidCameras) return false;
        if (overviewPose == seatedCamera.transform || overviewPose.IsChildOf(seatedCamera.transform)) return false;
        foreach (Transform pose in focusPoses)
            if (pose == null || pose == seatedCamera.transform || pose.IsChildOf(seatedCamera.transform)) return false;
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
                if (IsFocused) { Mode = ViewMode.Overview; FocusedIndex = -1; }
                else EndSession();
            }
            else if (keyboard.zKey.wasPressedThisFrame) Focus(IsFocused ? (FocusedIndex + 2) % 3 : 0);
            else if (keyboard.cKey.wasPressedThisFrame) Focus(IsFocused ? (FocusedIndex + 1) % 3 : 2);
            else if (keyboard.xKey.wasPressedThisFrame) Focus(1);
        }
        if (IsSeated) UpdateView();
    }

    private void Focus(int index)
    {
        FocusedIndex = index;
        Mode = ViewMode.Focused;
    }

    private void UpdateView()
    {
        Transform pose = IsFocused ? focusPoses[FocusedIndex] : overviewPose;
        float t = 1f - Mathf.Exp(-viewSmoothing * Time.deltaTime);
        viewPosition = Vector3.Lerp(viewPosition, pose.position, t);
        // Smooth pitch/roll independently so changing poses never changes the fixed world yaw.
        viewPitch = Mathf.LerpAngle(viewPitch, pose.eulerAngles.x, t);
        viewRoll = Mathf.LerpAngle(viewRoll, pose.eulerAngles.z, t);
        Quaternion viewRotation = Quaternion.Euler(viewPitch, SeatedYaw, viewRoll);
        seatedCamera.Lens.FieldOfView = Mathf.Lerp(seatedCamera.Lens.FieldOfView,
            IsFocused ? focusFieldOfView : overviewFieldOfView, t);
        swayTime += Time.deltaTime * swayFrequency * Mathf.PI * 2f;
        float amount = IsFocused ? focusedSwayMultiplier : 1f;
        Vector3 offset = new Vector3(Mathf.Sin(swayTime) * swayPosition.x,
            Mathf.Sin(swayTime * 1.37f) * swayPosition.y, 0f) * amount;
        Vector3 angles = new Vector3(Mathf.Sin(swayTime * 1.13f) * swayDegrees.x,
            Mathf.Sin(swayTime) * swayDegrees.y, Mathf.Sin(swayTime * 0.73f) * swayDegrees.z) * amount;
        seatedCamera.transform.SetPositionAndRotation(viewPosition + viewRotation * offset,
            Quaternion.Euler(viewPitch + angles.x, SeatedYaw + angles.y, viewRoll + angles.z));
    }

    public void EndSession()
    {
        if (!IsSeated) return;
        Mode = ViewMode.Standing;
        FocusedIndex = -1;
        if (seatedCamera != null)
        {
            seatedCamera.enabled = false;
            seatedCamera.Priority = savedPriority;
            seatedCamera.transform.SetPositionAndRotation(savedCameraPosition, savedCameraRotation);
            seatedCamera.Lens.FieldOfView = savedFieldOfView;
        }
        if (activePlayer != null) activePlayer.InputEnabled = savedPlayerInput;
        if (activeLook != null) activeLook.InputEnabled = savedLookInput;
        activePlayer = null;
        activeLook = null;
        if (ActiveSession == this) ActiveSession = null;
    }

    private void OnDisable() => EndSession();
}
