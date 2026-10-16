using UnityEngine;
using UnityEngine.UI;

/// <summary>A reading light and small direction marker until the required handbook is read.</summary>
[DisallowMultipleComponent]
public sealed class HandbookReadCue : MonoBehaviour
{
    private Light bookLight;
    private Renderer[] bookRenderers;
    private Canvas markerCanvas;
    private RectTransform marker;
    private RectTransform chevron;
    private Camera playerCamera;

    private void Start()
    {
        bookRenderers = GetComponentsInChildren<Renderer>();
        GameObject lightRoot = new GameObject("Handbook reading light");
        lightRoot.transform.SetParent(transform, false);
        bookLight = lightRoot.AddComponent<Light>();
        bookLight.type = LightType.Point;
        bookLight.color = new Color(0.8f, 0.9f, 1f);
        bookLight.range = 0.85f;
        bookLight.shadows = LightShadows.None;
        BuildMarker();
    }

    private void LateUpdate()
    {
        if (bookLight == null) return;
        GameplaySignals signals = GameplaySignals.Instance;
        if (signals != null && signals.HasRaised("handbook_read"))
        {
            if (bookLight != null) bookLight.enabled = false;
            if (markerCanvas != null) markerCanvas.enabled = false;
            enabled = false;
            return;
        }
        bool show = ShiftStartMenu.ShiftStarted && Time.timeScale > 0f
            && !OptionalPropActivities.Busy && ReferencePictureView.ActiveView == null
            && !SequencePuzzle.IsPanelOpen && MonitorViewManager.ActiveSession == null
            && !ShiftAudioDirector.EndingActive;
        if (markerCanvas != null) markerCanvas.enabled = show;
        if (bookLight != null)
        {
            bookLight.enabled = show;
            bookLight.intensity = 0.55f + 0.2f * Mathf.Sin(Time.time * 2.5f);
        }
        if (!show) return;
        Bounds bounds = new Bounds(transform.position, Vector3.zero);
        bool first = true;
        foreach (Renderer renderer in bookRenderers)
        {
            if (renderer == null || !renderer.enabled) continue;
            if (first) { bounds = renderer.bounds; first = false; }
            else bounds.Encapsulate(renderer.bounds);
        }
        if (bookLight != null) bookLight.transform.position = bounds.center + Vector3.up * 0.22f;
        UpdateMarker(bounds.center + Vector3.up * .15f);
    }

    private void BuildMarker()
    {
        GameObject root = new GameObject("Required Handbook Direction", typeof(RectTransform),
            typeof(Canvas), typeof(CanvasScaler));
        root.transform.SetParent(transform, false);
        markerCanvas = root.GetComponent<Canvas>();
        markerCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        markerCanvas.sortingOrder = 96;
        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = .5f;
        marker = new GameObject("Handbook Marker", typeof(RectTransform)).GetComponent<RectTransform>();
        marker.SetParent(root.transform, false);
        marker.sizeDelta = new Vector2(150f, 64f);
        GameObject caption = new GameObject("Handbook Label", typeof(RectTransform), typeof(Text), typeof(Shadow));
        caption.transform.SetParent(marker, false);
        RectTransform rect = caption.GetComponent<RectTransform>();
        rect.anchoredPosition = new Vector2(0f, 24f);
        rect.sizeDelta = new Vector2(150f, 32f);
        Text text = caption.GetComponent<Text>();
        text.font = StationTypography.UIFont;
        text.text = "HANDBOOK";
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

    private void UpdateMarker(Vector3 position)
    {
        if (playerCamera == null)
        {
            PlayerInteractor player = FindAnyObjectByType<PlayerInteractor>();
            playerCamera = player != null ? player.InteractionCamera : Camera.main;
        }
        if (playerCamera == null || markerCanvas == null) return;
        RectTransform canvasRect = markerCanvas.transform as RectTransform;
        Vector3 screen = playerCamera.WorldToScreenPoint(position);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out Vector2 point);
        Vector2 limit = canvasRect.rect.size * .5f - new Vector2(110f, 120f);
        bool onScreen = screen.z > 0f && Mathf.Abs(point.x) < limit.x && Mathf.Abs(point.y) < limit.y;
        if (onScreen)
        {
            marker.anchoredPosition = point;
            chevron.localRotation = Quaternion.identity;
            return;
        }
        // Keep the marker at the screen edge if the book is behind the player.
        Vector3 local = playerCamera.transform.InverseTransformPoint(position);
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
        if (bookLight != null) bookLight.enabled = false;
        if (markerCanvas != null) markerCanvas.enabled = false;
    }

}
