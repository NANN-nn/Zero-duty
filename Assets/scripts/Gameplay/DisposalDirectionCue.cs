using UnityEngine;
using UnityEngine.UI;

/// <summary>Handbook-style direction arrow while carrying something disposable.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(DisposalUnit))]
public sealed class DisposalDirectionCue : MonoBehaviour
{
    private DisposalUnit unit;
    private PlayerInteractor player;
    private Renderer hatch;
    private Renderer[] unitRenderers;
    private Canvas markerCanvas;
    private RectTransform marker;
    private RectTransform chevron;

    private void Start()
    {
        unit = GetComponent<DisposalUnit>();
        unitRenderers = unit.VisualRenderers;
        foreach (Renderer part in unitRenderers)
            if (part != null && part.name == "V9 Disposal | smoked hatch.001")
            {
                hatch = part;
                break;
            }
        BuildMarker();
    }

    private void LateUpdate()
    {
        if (markerCanvas == null) return;
        if (player == null) player = FindAnyObjectByType<PlayerInteractor>();
        Camera view = player != null ? player.InteractionCamera : null;
        bool show = unit != null && unit.isActiveAndEnabled && view != null
            && ShiftStartMenu.ShiftStarted && Time.timeScale > 0f
            && unit.CanInteract(player)
            && !OptionalPropActivities.Busy && ReferencePictureView.ActiveView == null
            && !SequencePuzzle.IsPanelOpen && MonitorViewManager.ActiveSession == null
            && !ShiftAudioDirector.EndingActive;
        markerCanvas.enabled = show;
        if (!show) return;

        // Point at the actual lid, not the proxy collider's pivot or a wall sign.
        Vector3 destination = transform.position + Vector3.up * .2f;
        if (hatch != null && hatch.enabled && hatch.gameObject.activeInHierarchy)
            destination = hatch.bounds.center + Vector3.up * .16f;
        else if (unitRenderers != null)
        {
            Bounds bounds = default;
            bool found = false;
            foreach (Renderer part in unitRenderers)
            {
                if (part == null || !part.enabled || !part.gameObject.activeInHierarchy) continue;
                if (!found) { bounds = part.bounds; found = true; }
                else bounds.Encapsulate(part.bounds);
            }
            if (found) destination = bounds.center + Vector3.up * (bounds.extents.y + .16f);
        }
        UpdateMarker(view, destination);
    }

    private void BuildMarker()
    {
        GameObject root = new GameObject("Disposal Direction", typeof(RectTransform),
            typeof(Canvas), typeof(CanvasScaler));
        root.transform.SetParent(transform, false);
        markerCanvas = root.GetComponent<Canvas>();
        markerCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        markerCanvas.sortingOrder = 96;
        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = .5f;

        marker = new GameObject("Disposal Marker", typeof(RectTransform)).GetComponent<RectTransform>();
        marker.SetParent(root.transform, false);
        marker.sizeDelta = new Vector2(150f, 64f);
        GameObject caption = new GameObject("Disposal Label", typeof(RectTransform),
            typeof(Text), typeof(Shadow));
        caption.transform.SetParent(marker, false);
        RectTransform captionRect = caption.GetComponent<RectTransform>();
        captionRect.anchoredPosition = new Vector2(0f, 24f);
        captionRect.sizeDelta = new Vector2(150f, 32f);
        Text text = caption.GetComponent<Text>();
        text.font = StationTypography.UIFont;
        StationLocalization.Set(text, "DISPOSAL");
        text.fontSize = 23;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = new Color(.9f, .96f, 1f);
        text.raycastTarget = false;
        caption.GetComponent<Shadow>().effectDistance = new Vector2(1f, -2f);

        chevron = new GameObject("Direction Chevron", typeof(RectTransform)).GetComponent<RectTransform>();
        chevron.SetParent(marker, false);
        chevron.sizeDelta = new Vector2(32f, 32f);
        for (int i = 0; i < 2; i++)
        {
            Image line = new GameObject("Chevron Stroke", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            line.transform.SetParent(chevron, false);
            line.rectTransform.sizeDelta = new Vector2(17f, 3f);
            line.rectTransform.anchoredPosition = new Vector2(i == 0 ? -5.5f : 5.5f, 0f);
            line.rectTransform.localEulerAngles = new Vector3(0f, 0f, i == 0 ? -45f : 45f);
            line.color = text.color;
            line.raycastTarget = false;
        }
        markerCanvas.enabled = false;
    }

    private void UpdateMarker(Camera view, Vector3 position)
    {
        RectTransform canvasRect = markerCanvas.transform as RectTransform;
        Vector3 screen = view.WorldToScreenPoint(position);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out Vector2 point);
        Vector2 limit = canvasRect.rect.size * .5f - new Vector2(110f, 120f);
        limit = new Vector2(Mathf.Max(1f, limit.x), Mathf.Max(1f, limit.y));
        bool onScreen = screen.z > 0f && Mathf.Abs(point.x) < limit.x && Mathf.Abs(point.y) < limit.y;
        if (onScreen)
        {
            marker.anchoredPosition = point;
            chevron.localRotation = Quaternion.identity;
            return;
        }

        // Match the handbook: show a turning direction at the screen edge
        // when the destination is behind the player instead of mirroring it.
        Vector3 local = view.transform.InverseTransformPoint(position);
        Vector2 direction = screen.z <= 0f
            ? new Vector2(local.x < 0f ? -1f : 1f, 0f)
            : new Vector2(local.x, local.y);
        if (direction.sqrMagnitude < .01f) direction = Vector2.right;
        direction.Normalize();
        float distance = Mathf.Min(limit.x / Mathf.Max(.001f, Mathf.Abs(direction.x)),
            limit.y / Mathf.Max(.001f, Mathf.Abs(direction.y)));
        marker.anchoredPosition = direction * distance;
        chevron.localRotation = Quaternion.Euler(0f, 0f,
            Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + 90f);
    }

    private void OnDisable()
    {
        if (markerCanvas != null) markerCanvas.enabled = false;
    }
}
