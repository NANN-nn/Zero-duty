using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Lives beside MonitorViewManager on the parent of the three CCTV cameras.</summary>
[DisallowMultipleComponent]
public class MonitorCameraController : MonoBehaviour
{
    [Serializable]
    public class CameraControl
    {
        public Camera camera;
        [Tooltip("相对初始朝向：负数向左，正数向右。")]
        public Vector2 yawLimits = new Vector2(-60f, 60f);
        [Tooltip("相对初始朝向：负数向上，正数向下。")]
        public Vector2 pitchLimits = new Vector2(-35f, 35f);
        [Min(0f)] public float degreesPerSecond = 35f;
        [NonSerialized] internal Quaternion restRotation;
        [NonSerialized] internal float yaw;
        [NonSerialized] internal float pitch;
    }

    [Tooltip("固定顺序：0 左屏、1 中屏、2 右屏。Camera 的 Target Texture 在 Inspector 中配置。")]
    [SerializeField] private CameraControl[] cameras =
    {
        new CameraControl(), new CameraControl(), new CameraControl()
    };
    private MonitorViewManager viewManager;

    public bool HasValidCameras
    {
        get
        {
            if (cameras == null || cameras.Length != 3) return false;
            for (int i = 0; i < 3; i++)
            {
                if (cameras[i] == null || cameras[i].camera == null) return false;
                for (int j = 0; j < i; j++)
                    if (cameras[i].camera == cameras[j].camera) return false;
            }
            return true;
        }
    }

    private void Awake()
    {
        viewManager = GetComponent<MonitorViewManager>();
        if (cameras == null) return;
        foreach (CameraControl control in cameras)
            if (control != null && control.camera != null)
                control.restRotation = control.camera.transform.localRotation;
    }

    private void Update()
    {
        if (viewManager == null || !viewManager.IsFocused || !Application.isFocused || !HasValidCameras) return;
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;
        CameraControl control = cameras[viewManager.FocusedIndex];
        float x = (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f);
        float y = (keyboard.sKey.isPressed ? 1f : 0f) - (keyboard.wKey.isPressed ? 1f : 0f);
        float step = Mathf.Max(0f, control.degreesPerSecond) * Time.deltaTime;
        control.yaw = Mathf.Clamp(control.yaw + x * step,
            Mathf.Min(control.yawLimits.x, control.yawLimits.y), Mathf.Max(control.yawLimits.x, control.yawLimits.y));
        control.pitch = Mathf.Clamp(control.pitch + y * step,
            Mathf.Min(control.pitchLimits.x, control.pitchLimits.y), Mathf.Max(control.pitchLimits.x, control.pitchLimits.y));
        control.camera.transform.localRotation = control.restRotation * Quaternion.Euler(control.pitch, control.yaw, 0f);
    }

    private void OnValidate()
    {
        if (cameras == null) return;
        foreach (CameraControl control in cameras)
        {
            if (control == null) continue;
            // The authored initial view is always inside the allowed interval.
            control.yawLimits = new Vector2(Mathf.Min(0f, control.yawLimits.x), Mathf.Max(0f, control.yawLimits.y));
            control.pitchLimits = new Vector2(Mathf.Min(0f, control.pitchLimits.x), Mathf.Max(0f, control.pitchLimits.y));
        }
    }
}
