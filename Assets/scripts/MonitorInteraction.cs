using TMPro;
using System;
using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(-60)]
[DisallowMultipleComponent]
public class MonitorInteraction : MonoBehaviour, IPlayerInteractable
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
    private PlayerInteractor playerInteractor;
    private MonitorScreenClick screenClick;
    private Collider chairCollider;

    public string InteractionPrompt => "View security cameras";

    private void Awake()
    {
        if (interactionPoint == null) interactionPoint = transform;
        if (playerLook == null && player != null) playerLook = player.GetComponentInChildren<FirstPersonCameraController>();
        playerInteractor = FindAnyObjectByType<PlayerInteractor>();
        if (viewManager != null) screenClick = viewManager.GetComponent<MonitorScreenClick>();
        GameObject chair = GameObject.Find("COL_ASSET_Operator_Chair.001");
        if (chair != null) chairCollider = chair.GetComponent<Collider>();
        if (promptText != null)
        {
            promptText.text = string.Empty;
            promptText.alpha = 0f;
            promptText.enabled = false;
            promptText.raycastTarget = false;
        }
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame && IsAvailable())
            viewManager.BeginSession(player, playerLook);
    }

    private bool IsAvailable()
    {
        return player != null && playerLook != null && viewManager != null
            && player.isActiveAndEnabled && player.InputEnabled && playerLook.isActiveAndEnabled
            && viewManager.isActiveAndEnabled && MonitorViewManager.ActiveSession == null
            && interactionPoint != null && Application.isFocused
            && (player.transform.position - interactionPoint.position).sqrMagnitude <= interactionDistance * interactionDistance
            && (IsLookingAtComputer() || IsLookingAtChair());
    }

    private bool IsLookingAtChair()
    {
        if (chairCollider == null || !chairCollider.enabled) return false;
        Camera camera = playerInteractor != null ? playerInteractor.InteractionCamera : Camera.main;
        if (camera == null) return false;
        Ray ray = camera.ViewportPointToRay(new Vector3(0.5f, 0.5f));
        RaycastHit[] hits = Physics.RaycastAll(ray, 3f, ~0, QueryTriggerInteraction.Collide);
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.transform.IsChildOf(player.transform)) continue;
            if (hit.collider == chairCollider) return true;
            if (hit.collider.transform.IsChildOf(transform)) continue;
            if (!hit.collider.isTrigger) return false;
        }
        return false;
    }

    private bool IsLookingAtComputer()
    {
        if (screenClick == null) return false;
        Camera camera = playerInteractor != null ? playerInteractor.InteractionCamera : Camera.main;
        if (camera == null) return false;

        Ray ray = camera.ViewportPointToRay(new Vector3(0.5f, 0.5f));
        bool screenInView = screenClick.TryGetScreenDistance(ray, 3f, out float screenDistance);
        RaycastHit[] hits = Physics.RaycastAll(ray, 3f, ~0, QueryTriggerInteraction.Collide);
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.transform.IsChildOf(player.transform)) continue;
            if (screenClick.IsScreenCollider(hit.collider)) return true;
            if (hit.collider == chairCollider) continue;

            // The imported monitor frame and desk may have colliders in front of
            // the screen mesh. They are part of this same interaction, not blockers.
            if (screenInView && hit.distance > screenDistance + 0.05f) break;
            if (hit.collider.transform.IsChildOf(transform)) continue;

            // A different interactable under the crosshair takes priority.
            foreach (MonoBehaviour behaviour in hit.collider.GetComponentsInParent<MonoBehaviour>(true))
                if (behaviour is IPlayerInteractable) return false;

            // Invisible helper colliders do not hide the screens.
            Renderer renderer = hit.collider.GetComponent<Renderer>();
            if (renderer == null) renderer = hit.collider.GetComponentInParent<Renderer>();
            if (renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy)
                return false;
        }
        return screenInView;
    }

    public bool CanInteract(PlayerInteractor interactor) => IsAvailable();

    public void Interact(PlayerInteractor interactor) => viewManager.BeginSession(player, playerLook);

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
