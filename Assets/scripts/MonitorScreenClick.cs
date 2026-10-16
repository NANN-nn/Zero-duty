using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>While seated, click one of the three physical screens to view its live feed.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(MonitorViewManager), typeof(MonitorCameraController))]
public class MonitorScreenClick : MonoBehaviour
{
    [Tooltip("The real gameplay Camera named camera, not the Cinemachine RL_Camera or CCTV cameras.")]
    [SerializeField] private Camera viewCamera;
    [Tooltip("Left, middle, right: the colliders on CCTV | live-room preview 1/2/3.001.")]
    [SerializeField] private Collider[] clickSurfaces = new Collider[3];
    [Tooltip("Left, middle, right: the renderers on those same three objects.")]
    [SerializeField] private Renderer[] displaySurfaces = new Renderer[3];
    [SerializeField, Min(0.1f)] private float clickDistance = 6f;
    [Header("Seated overview click area")]
    [SerializeField, Range(0f, 0.2f)] private float horizontalPadding = 0.04f;
    [SerializeField, Range(0f, 0.2f)] private float verticalPadding = 0.09f;

    private MonitorViewManager view;
    private MonitorCameraController cameras;
    private Material feedMaterial;
    private GameObject feedOverlay;
    private int showingIndex = -1;
    private GameObject navigationRoot;
    private RectTransform previousArrow;
    private RectTransform nextArrow;
    private Text navigationCaption;

    public Renderer GetDisplay(int index)
        => displaySurfaces != null && index >= 0 && index < displaySurfaces.Length
            ? displaySurfaces[index] : null;

    public bool IsScreenCollider(Collider candidate)
    {
        if (candidate == null || clickSurfaces == null) return false;
        foreach (Collider surface in clickSurfaces)
            if (surface == candidate) return true;
        return false;
    }

    public bool TryGetScreenDistance(Ray ray, float maxDistance, out float nearestDistance)
    {
        nearestDistance = maxDistance;
        bool found = false;
        if (displaySurfaces == null) return false;
        foreach (Renderer display in displaySurfaces)
        {
            if (display == null || !display.enabled || !display.gameObject.activeInHierarchy)
                continue;
            if (display.bounds.IntersectRay(ray, out float distance)
                && distance >= 0f && distance <= nearestDistance)
            {
                nearestDistance = distance;
                found = true;
            }
        }
        return found;
    }

    private void Awake()
    {
        view = GetComponent<MonitorViewManager>();
        cameras = GetComponent<MonitorCameraController>();

        if (viewCamera == null)
        {
            GameObject mainView = GameObject.Find("camera");
            if (mainView != null) viewCamera = mainView.GetComponent<Camera>();
            if (viewCamera == null) viewCamera = Camera.main;
        }

        for (int i = 0; i < 3; i++)
        {
            if (clickSurfaces == null || displaySurfaces == null
                || i >= clickSurfaces.Length || i >= displaySurfaces.Length) break;
            if (clickSurfaces[i] != null && displaySurfaces[i] != null) continue;

            GameObject preview = GameObject.Find($"CCTV | live-room preview {i + 1}.001");
            GameObject screen = GameObject.Find($"CCTV {i + 1} | screen.001");
            if (clickSurfaces[i] == null)
                clickSurfaces[i] = preview != null ? preview.GetComponent<Collider>() : null;
            if (clickSurfaces[i] == null && screen != null)
                clickSurfaces[i] = screen.GetComponent<Collider>();
            if (displaySurfaces[i] == null)
                displaySurfaces[i] = preview != null ? preview.GetComponent<Renderer>() : null;
            if (displaySurfaces[i] == null && screen != null)
                displaySurfaces[i] = screen.GetComponent<Renderer>();

            if (clickSurfaces[i] == null)
                Debug.LogWarning($"CCTV {i + 1} has no click collider. Add a Box Collider to its screen.", this);
            if (displaySurfaces[i] == null)
                Debug.LogWarning($"CCTV {i + 1} has no display renderer.", this);
        }
        BuildNavigation();
    }

    private void Update()
    {
        if (showingIndex >= 0 && (view == null || !view.IsFocused
            || view.FocusedIndex != showingIndex)) RestoreScreen();

        if (view == null || !view.IsSeated || viewCamera == null
            || !Application.isFocused || Mouse.current == null
            || !Mouse.current.leftButton.wasPressedThisFrame) return;

        Vector2 pointer = Mouse.current.position.ReadValue();
        // Arrow clicks belong only to UI; do not also hit a screen behind it.
        if (navigationRoot != null && navigationRoot.activeInHierarchy
            && (RectTransformUtility.RectangleContainsScreenPoint(previousArrow, pointer)
                || RectTransformUtility.RectangleContainsScreenPoint(nextArrow, pointer))) return;
        // In the seated overview, the whole middle band of the three monitors is clickable.
        // This does not depend on the imported monitor mesh or its exact collider.
        if (!view.IsFocused && TryGetOverviewScreen(pointer, out int wideIndex))
        {
            ShowScreen(wideIndex);
            return;
        }

        Ray ray = viewCamera.ScreenPointToRay(pointer);
        RaycastHit[] hits = Physics.RaycastAll(ray, clickDistance, ~0, QueryTriggerInteraction.Collide);
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            for (int i = 0; i < 3; i++)
            {
                if (clickSurfaces == null || i >= clickSurfaces.Length
                    || hit.collider != clickSurfaces[i]) continue;
                ShowScreen(i);
                return;
            }
        }

        // Some imported screen meshes have their collider on a different layer of the model.
        // Fall back to the visible screen bounds so the left screen remains clickable.
        int closestIndex = -1;
        float closestDistance = clickDistance;
        for (int i = 0; i < 3; i++)
        {
            if (displaySurfaces == null || i >= displaySurfaces.Length || displaySurfaces[i] == null)
                continue;
            if (displaySurfaces[i].bounds.IntersectRay(ray, out float distance)
                && distance >= 0f && distance < closestDistance)
            {
                closestDistance = distance;
                closestIndex = i;
            }
        }
        if (closestIndex >= 0) ShowScreen(closestIndex);
    }

    private void LateUpdate()
    {
        if (navigationRoot == null) return;
        bool show = view != null && view.IsSeated && ShiftStartMenu.ShiftStarted
            && Time.timeScale > 0f && !ShiftAudioDirector.EndingActive;
        navigationRoot.SetActive(show);
        if (show)
            navigationCaption.text = view.IsFocused
                ? $"CAM {view.FocusedIndex + 1:00} / 03" : "SELECT A CAMERA";
    }

    private void ChangeFeed(int step)
    {
        if (view == null || !view.IsSeated || Time.timeScale <= 0f
            || ShiftAudioDirector.EndingActive) return;
        int current = view.IsFocused ? view.FocusedIndex : 1;
        ShowScreen((current + step + 3) % 3);
    }

    private void BuildNavigation()
    {
        navigationRoot = new GameObject("CCTV Camera Navigation", typeof(RectTransform),
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        navigationRoot.transform.SetParent(transform, false);
        Canvas canvas = navigationRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 115;
        CanvasScaler scaler = navigationRoot.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = .5f;
        previousArrow = NewNavigationArrow(false);
        nextArrow = NewNavigationArrow(true);
        navigationCaption = new GameObject("Camera Index", typeof(RectTransform), typeof(Text),
            typeof(Shadow)).GetComponent<Text>();
        navigationCaption.transform.SetParent(navigationRoot.transform, false);
        RectTransform captionRect = navigationCaption.rectTransform;
        captionRect.anchorMin = captionRect.anchorMax = new Vector2(.5f, 1f);
        captionRect.pivot = new Vector2(.5f, 1f);
        captionRect.anchoredPosition = new Vector2(0f, -35f);
        captionRect.sizeDelta = new Vector2(320f, 44f);
        navigationCaption.font = StationTypography.UIFont;
        navigationCaption.fontSize = 26;
        navigationCaption.alignment = TextAnchor.MiddleCenter;
        navigationCaption.color = new Color(.88f, .95f, .98f);
        navigationCaption.raycastTarget = false;
        navigationCaption.GetComponent<Shadow>().effectDistance = new Vector2(1f, -2f);
        navigationRoot.SetActive(false);
    }

    private RectTransform NewNavigationArrow(bool right)
    {
        Image face = new GameObject(right ? "Next Camera" : "Previous Camera",
            typeof(RectTransform), typeof(Image), typeof(Button), typeof(UnityEngine.UI.Outline)).GetComponent<Image>();
        face.transform.SetParent(navigationRoot.transform, false);
        RectTransform rect = face.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(right ? 1f : 0f, .5f);
        rect.anchoredPosition = new Vector2(right ? -38f : 38f, 0f);
        rect.sizeDelta = new Vector2(76f, 100f);
        face.color = new Color(.045f, .075f, .095f, .8f);
        UnityEngine.UI.Outline edge = face.GetComponent<UnityEngine.UI.Outline>();
        edge.effectColor = new Color(.6f, .72f, .78f, .65f);
        edge.effectDistance = new Vector2(1f, -1f);
        Button button = face.GetComponent<Button>();
        button.targetGraphic = face;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        ColorBlock colors = button.colors;
        colors.normalColor = colors.selectedColor = Color.white;
        colors.highlightedColor = new Color(1.8f, 1.8f, 1.8f);
        colors.pressedColor = new Color(.65f, .65f, .65f);
        button.colors = colors;
        button.onClick.AddListener(() => ChangeFeed(right ? 1 : -1));
        for (int i = 0; i < 2; i++)
        {
            Image stroke = new GameObject("Chevron Stroke", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            stroke.transform.SetParent(rect, false);
            stroke.rectTransform.sizeDelta = new Vector2(25f, 4f);
            stroke.rectTransform.anchoredPosition = new Vector2(0f, i == 0 ? 8f : -8f);
            stroke.rectTransform.localEulerAngles = new Vector3(0f, 0f,
                (right ? -1f : 1f) * (i == 0 ? 45f : -45f));
            stroke.color = new Color(.9f, .96f, 1f);
            stroke.raycastTarget = false;
        }
        return rect;
    }

    private bool TryGetOverviewScreen(Vector2 pointer, out int index)
    {
        index = -1;
        if (displaySurfaces == null || viewCamera == null) return false;

        float left = float.PositiveInfinity;
        float right = float.NegativeInfinity;
        float bottom = float.PositiveInfinity;
        float top = float.NegativeInfinity;
        float nearestX = float.PositiveInfinity;
        Vector3 viewportPointer = viewCamera.ScreenToViewportPoint(pointer);

        for (int i = 0; i < Mathf.Min(3, displaySurfaces.Length); i++)
        {
            Renderer display = displaySurfaces[i];
            if (display == null || !display.enabled || !display.gameObject.activeInHierarchy)
                continue;

            Bounds bounds = display.bounds;
            Vector3 center = viewCamera.WorldToViewportPoint(bounds.center);
            if (center.z <= 0f) continue;
            float halfWidth = 0f;
            float halfHeight = 0f;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 point = bounds.center + Vector3.Scale(bounds.extents,
                    new Vector3((corner & 1) == 0 ? -1f : 1f,
                        (corner & 2) == 0 ? -1f : 1f,
                        (corner & 4) == 0 ? -1f : 1f));
                Vector3 projected = viewCamera.WorldToViewportPoint(point);
                if (projected.z <= 0f) continue;
                halfWidth = Mathf.Max(halfWidth, Mathf.Abs(projected.x - center.x));
                halfHeight = Mathf.Max(halfHeight, Mathf.Abs(projected.y - center.y));
            }
            left = Mathf.Min(left, center.x - halfWidth);
            right = Mathf.Max(right, center.x + halfWidth);
            bottom = Mathf.Min(bottom, center.y - halfHeight);
            top = Mathf.Max(top, center.y + halfHeight);

            float horizontalDistance = Mathf.Abs(viewportPointer.x - center.x);
            if (horizontalDistance < nearestX)
            {
                nearestX = horizontalDistance;
                index = i;
            }
        }

        return index >= 0 && viewportPointer.x >= left - horizontalPadding
            && viewportPointer.x <= right + horizontalPadding
            && viewportPointer.y >= bottom - verticalPadding
            && viewportPointer.y <= top + verticalPadding;
    }

    private void ShowScreen(int index)
    {
        Camera feedCamera = cameras != null ? cameras.GetCamera(index) : null;
        RenderTexture feed = feedCamera != null ? feedCamera.targetTexture : null;
        Renderer display = displaySurfaces != null && index < displaySurfaces.Length
            ? displaySurfaces[index] : null;
        if (feed == null || display == null)
        {
            Debug.LogWarning($"Screen {index + 1} has no live feed or display renderer.", this);
            return;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Texture");
        if (shader == null)
        {
            Debug.LogError("No unlit shader is available for the CCTV feed.", this);
            return;
        }

        RestoreScreen();
        feedMaterial = new Material(shader) { name = $"CCTV Feed {index + 1} (Runtime)" };
        feedMaterial.mainTexture = feed;
        if (feedMaterial.HasProperty("_BaseMap")) feedMaterial.SetTexture("_BaseMap", feed);
        if (feedMaterial.HasProperty("_BaseColor")) feedMaterial.SetColor("_BaseColor", Color.white);
        if (feedMaterial.HasProperty("_Cull")) feedMaterial.SetFloat("_Cull", 0f);

        // Imported monitor meshes can have UVs that show only a crop of the feed.
        // A quad supplies full 0..1 UVs while leaving the original screen intact.
        MeshFilter filter = display.GetComponent<MeshFilter>();
        if (filter == null || filter.sharedMesh == null)
        {
            Debug.LogWarning($"Screen {index + 1} has no mesh for the feed overlay.", this);
            Destroy(feedMaterial);
            feedMaterial = null;
            return;
        }
        Bounds panel = filter.sharedMesh.bounds;
        float towardViewer = viewCamera != null
            ? Mathf.Sign(display.transform.InverseTransformPoint(viewCamera.transform.position).y)
            : 1f;
        if (towardViewer == 0f) towardViewer = 1f;
        feedOverlay = GameObject.CreatePrimitive(PrimitiveType.Quad);
        feedOverlay.name = $"CCTV Feed {index + 1} (Runtime)";
        Collider overlayCollider = feedOverlay.GetComponent<Collider>();
        if (overlayCollider != null)
        {
            overlayCollider.enabled = false;
            Destroy(overlayCollider);
        }
        feedOverlay.transform.SetParent(display.transform, false);
        feedOverlay.transform.localPosition = panel.center
            + Vector3.up * towardViewer * (panel.extents.y + 0.015f);
        feedOverlay.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        feedOverlay.transform.localScale = new Vector3(panel.size.x * 0.98f,
            panel.size.z * 0.98f, 1f);
        Renderer overlayRenderer = feedOverlay.GetComponent<Renderer>();
        overlayRenderer.sharedMaterial = feedMaterial;
        overlayRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        overlayRenderer.receiveShadows = false;
        showingIndex = index;
        view.FocusScreen(index, display);
        Debug.Log($"[CCTV] Opened screen {index + 1} using {feedCamera.name}.", this);
    }

    private void RestoreScreen()
    {
        if (showingIndex < 0) return;
        if (feedOverlay != null) Destroy(feedOverlay);
        if (feedMaterial != null) Destroy(feedMaterial);
        feedOverlay = null;
        feedMaterial = null;
        showingIndex = -1;
    }

    private void OnDisable()
    {
        RestoreScreen();
        if (navigationRoot != null) navigationRoot.SetActive(false);
    }
    private void OnDestroy() => RestoreScreen();
}
