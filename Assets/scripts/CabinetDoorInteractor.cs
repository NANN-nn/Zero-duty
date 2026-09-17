using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class CabinetDoorInteractor : MonoBehaviour
{
    [Header("射线来源")]
    [SerializeField] private Camera interactionCamera;

    [Header("交互距离")]
    [SerializeField, Min(0.1f)] private float interactionDistance = 2.5f;

    [Header("可交互层")]
    [SerializeField] private LayerMask interactionLayers = ~0;

    private void Awake()
    {
        if (interactionCamera == null)
            interactionCamera = GetComponentInChildren<Camera>(true);

        if (interactionCamera == null)
            interactionCamera = Camera.main;

        if (interactionCamera == null)
        {
            Debug.LogError($"{name}: 找不到交互摄像机，请在检查器中绑定玩家摄像机。", this);
            enabled = false;
        }
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || !keyboard.eKey.wasPressedThisFrame)
            return;

        Ray ray = interactionCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        RaycastHit[] hits = Physics.RaycastAll(ray, interactionDistance,
            interactionLayers, QueryTriggerInteraction.Ignore);

        CabinetDoor nearestDoor = null;
        float nearestDistance = float.PositiveInfinity;

        foreach (RaycastHit hit in hits)
        {
            CabinetDoor door = hit.collider.GetComponentInParent<CabinetDoor>();
            if (door != null && hit.distance < nearestDistance)
            {
                nearestDoor = door;
                nearestDistance = hit.distance;
            }
        }

        if (nearestDoor != null)
            nearestDoor.Toggle();
    }
}
