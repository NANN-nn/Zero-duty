using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(-60)]
[DisallowMultipleComponent]
public class MonitorInteraction : MonoBehaviour
{
    [Header("交互引用")]
    [SerializeField] private playerController player;
    [SerializeField] private FirstPersonCameraController playerLook;
    [SerializeField] private MonitorViewManager viewManager;
    [Tooltip("不填时使用本物体的位置。距离从人物根节点计算。")]
    [SerializeField] private Transform interactionPoint;
    [SerializeField, Min(0.1f)] private float interactionDistance = 2f;
    [Header("屏幕提示（Canvas 下的 TextMeshProUGUI）")]
    [SerializeField] private TMP_Text promptText;
    [SerializeField, Min(0.01f)] private float fadeSpeed = 6f;

    private void Awake()
    {
        if (interactionPoint == null) interactionPoint = transform;
        if (playerLook == null && player != null) playerLook = player.GetComponentInChildren<FirstPersonCameraController>();
        if (promptText != null)
        {
            promptText.text = "按F看监控";
            promptText.alpha = 0f;
            promptText.raycastTarget = false;
        }
        else Debug.LogWarning($"{name}: 未绑定 Prompt Text，交互提示将不可见。", this);
    }

    private void Update()
    {
        bool available = player != null && playerLook != null && viewManager != null
            && player.isActiveAndEnabled && player.InputEnabled && playerLook.isActiveAndEnabled
            && viewManager.isActiveAndEnabled && MonitorViewManager.ActiveSession == null
            && interactionPoint != null && Application.isFocused
            && (player.transform.position - interactionPoint.position).sqrMagnitude <= interactionDistance * interactionDistance;
        if (available && Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
        {
            if (viewManager.BeginSession(player, playerLook)) available = false;
        }
        if (promptText != null)
            promptText.alpha = Mathf.MoveTowards(promptText.alpha, available ? 1f : 0f, fadeSpeed * Time.unscaledDeltaTime);
    }

    private void OnDisable()
    {
        if (promptText != null) promptText.alpha = 0f;
        if (viewManager != null) viewManager.EndSession();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(interactionPoint != null ? interactionPoint.position : transform.position, interactionDistance);
    }
}
