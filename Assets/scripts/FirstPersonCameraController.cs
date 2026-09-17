using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Attach to Camera_Player: vertical / horizontal / shake / RL_Camera.</summary>
[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
public class FirstPersonCameraController : MonoBehaviour
{
    [Header("层级")]
    [Tooltip("绑定人物根节点上的 playerController。鼠标左右直接旋转这个人物物体。")]
    [SerializeField] private playerController player;
    [SerializeField] private Transform vertical;
    [SerializeField] private Transform horizontal;
    [SerializeField] private Transform shake;
    [Header("鼠标（度/像素）")]
    [SerializeField, Min(0f)] private float sensitivity = 0.12f;
    [SerializeField] private bool invertY;
    [SerializeField, Range(-89f, 0f)] private float minPitch = -75f;
    [SerializeField, Range(0f, 89f)] private float maxPitch = 75f;
    [SerializeField] private bool lockCursor = true;
    [Header("走路摇晃")]
    [SerializeField, Min(0f)] private float walkFrequency = 1.7f;
    [SerializeField] private Vector2 walkAmplitude = new Vector2(0.018f, 0.025f);
    [SerializeField, Min(0f)] private float walkRoll = 0.5f;
    [Header("跑步摇晃")]
    [SerializeField, Min(0f)] private float runFrequency = 2.6f;
    [SerializeField] private Vector2 runAmplitude = new Vector2(0.035f, 0.05f);
    [SerializeField, Min(0f)] private float runRoll = 1f;
    [SerializeField, Min(0.01f)] private float shakeSmoothing = 12f;
    public bool InputEnabled { get; set; } = true;
    private Quaternion verticalRest;
    private Quaternion shakeRestRotation;
    private Vector3 shakeRestPosition;
    private float pitch;
    private float phase;
    private bool ready;
    private CursorLockMode previousCursorLock;
    private bool previousCursorVisible;

    private void Awake()
    {
        if (player == null) player = GetComponentInParent<playerController>();
        if (vertical == null) vertical = transform.Find("vertical");
        if (horizontal == null && vertical != null) horizontal = vertical.Find("horizontal");
        if (shake == null && horizontal != null) shake = horizontal.Find("shake");
        ready = player != null && vertical != null && horizontal != null && shake != null
            && transform != player.transform && transform.IsChildOf(player.transform)
            && vertical != transform && vertical.IsChildOf(transform)
            && horizontal != vertical && horizontal.IsChildOf(vertical)
            && shake != horizontal && shake.IsChildOf(horizontal);
        if (!ready)
        {
            Debug.LogError($"{name}: 请绑定人物根节点上的 Player，并将 Camera_Player 放在人物下面，检查 vertical/horizontal/shake 层级。", this);
            enabled = false;
            return;
        }
        verticalRest = vertical.localRotation;
        shakeRestRotation = shake.localRotation;
        shakeRestPosition = shake.localPosition;
    }

    private void OnEnable()
    {
        previousCursorLock = Cursor.lockState;
        previousCursorVisible = Cursor.visible;
        if (lockCursor) SetCursorLocked();
    }

    private void Update()
    {
        if (!ready || !InputEnabled || !player.InputEnabled || !Application.isFocused) return;
        Mouse mouse = Mouse.current;
        if (mouse == null) return;
        if (lockCursor && Cursor.lockState != CursorLockMode.Locked)
        {
            if (mouse.leftButton.wasPressedThisFrame) SetCursorLocked();
            return;
        }
        // Mouse delta is displacement per frame, so no deltaTime multiplier.
        Vector2 delta = mouse.delta.ReadValue() * sensitivity;
        // Turn the character itself. Its child camera follows, and WASD uses this same transform.
        player.transform.Rotate(Vector3.up, delta.x, Space.World);
        pitch = Mathf.Clamp(pitch + delta.y * (invertY ? 1f : -1f), minPitch, maxPitch);
        vertical.localRotation = verticalRest * Quaternion.Euler(pitch, 0f, 0f);
    }

    private void LateUpdate()
    {
        if (!ready) return;
        bool moving = InputEnabled && player.InputEnabled && player.IsMoving && Application.isFocused;
        Vector3 offset = Vector3.zero;
        float roll = 0f;
        if (moving)
        {
            bool running = player.IsSprinting;
            phase = Mathf.Repeat(phase + Time.deltaTime * (running ? runFrequency : walkFrequency) * Mathf.PI * 2f,
                Mathf.PI * 2f);
            Vector2 amplitude = running ? runAmplitude : walkAmplitude;
            offset = new Vector3(Mathf.Sin(phase) * amplitude.x, Mathf.Sin(phase * 2f) * amplitude.y, 0f);
            roll = Mathf.Sin(phase) * (running ? runRoll : walkRoll);
        }
        else phase = 0f;
        float t = 1f - Mathf.Exp(-shakeSmoothing * Time.deltaTime);
        shake.localPosition = Vector3.Lerp(shake.localPosition, shakeRestPosition + offset, t);
        shake.localRotation = Quaternion.Slerp(shake.localRotation, shakeRestRotation * Quaternion.Euler(0f, 0f, roll), t);
    }

    private void SetCursorLocked()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void OnDisable()
    {
        if (ready && shake != null)
        {
            shake.localPosition = shakeRestPosition;
            shake.localRotation = shakeRestRotation;
        }
        if (lockCursor)
        {
            Cursor.lockState = previousCursorLock;
            Cursor.visible = previousCursorVisible;
        }
    }
}
