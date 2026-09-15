using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController))]
public class playerController : MonoBehaviour
{
    [Header("移动（米/秒，无跳跃）")]
    [SerializeField, Min(0f)] private float walkSpeed = 2.5f;
    [SerializeField, Min(0f)] private float sprintSpeed = 5f;
    [SerializeField, Min(0.01f)] private float gravity = 20f;
    [Tooltip("默认使用人物自身；只取水平朝向。")]
    [SerializeField] private Transform movementOrientation;
    public bool InputEnabled { get; set; } = true;
    public bool IsMoving { get; private set; }
    public bool IsSprinting { get; private set; }
    public float PlanarSpeed { get; private set; }
    public bool IsGrounded => controller != null && controller.isGrounded;
    private CharacterController controller;
    private float verticalSpeed;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        if (movementOrientation == null) movementOrientation = transform;
    }

    private void Update()
    {
        IsMoving = IsSprinting = false;
        PlanarSpeed = 0f;
        if (!controller.enabled || !InputEnabled) { verticalSpeed = 0f; return; }
        Keyboard keyboard = Keyboard.current;
        Vector2 input = Vector2.zero;
        bool sprint = false;
        if (Application.isFocused && keyboard != null)
        {
            input.x = (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f);
            input.y = (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f);
            sprint = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
        }
        input = Vector2.ClampMagnitude(input, 1f);
        Vector3 forward = Vector3.ProjectOnPlane(movementOrientation.forward, Vector3.up).normalized;
        if (forward.sqrMagnitude < 0.001f) forward = transform.forward;
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
        Vector3 velocity = (forward * input.y + right * input.x) * (sprint ? sprintSpeed : walkSpeed);
        if (controller.isGrounded && verticalSpeed < 0f) verticalSpeed = -2f;
        verticalSpeed -= gravity * Time.deltaTime;
        Vector3 before = transform.position;
        CollisionFlags flags = controller.Move((velocity + Vector3.up * verticalSpeed) * Time.deltaTime);
        if ((flags & CollisionFlags.Above) != 0 && verticalSpeed > 0f) verticalSpeed = 0f;
        if ((flags & CollisionFlags.Below) != 0) verticalSpeed = -2f;
        PlanarSpeed = Vector3.ProjectOnPlane(transform.position - before, Vector3.up).magnitude
            / Mathf.Max(Time.deltaTime, 0.0001f);
        IsMoving = input.sqrMagnitude > 0f && PlanarSpeed > 0.05f && controller.isGrounded;
        IsSprinting = IsMoving && sprint;
    }

    private void OnDisable()
    {
        IsMoving = IsSprinting = false;
        PlanarSpeed = verticalSpeed = 0f;
    }
}
