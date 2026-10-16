using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using TMPro;

/// <summary>Highlights the focused interaction; E targets also show a small key hint.</summary>
[DefaultExecutionOrder(1000)]
[DisallowMultipleComponent]
public sealed class InteractionFocusFeedback : MonoBehaviour
{
    [SerializeField] private Color outlineColor = new Color(0.96f, 0.97f, 1f, 0.86f);
    [SerializeField, Range(0.002f, 0.02f)] private float outlineWidth = 0.009f;

    private readonly List<GameObject> outlines = new List<GameObject>();
    private readonly List<Renderer> sources = new List<Renderer>();
    private readonly List<Renderer> copies = new List<Renderer>();
    private PlayerInteractor interactor;
    private MonoBehaviour target;
    private Material outlineMaterial;
    private Material silhouetteMaterial;
    private CanvasGroup hint;
    private RectTransform hintPanel;
    private RectTransform hintTextRect;
    private RectTransform keycapRect;
    private GameObject keycap;
    private TextMeshProUGUI keycapText;
    private TextMeshProUGUI hintText;
    private bool dropHintShown;
    private float dropHintUntil;

    public void NotifyItemPickedUp()
    {
        if (dropHintShown) return;
        dropHintShown = true;
        dropHintUntil = Time.unscaledTime + 4f;
    }

    public void NotifyItemReleased() => dropHintUntil = 0f;

    private void Awake()
    {
        interactor = GetComponent<PlayerInteractor>();
        Shader shader = Resources.Load<Shader>("ZeroHourAssets/Shaders/InteractableOutline");
        if (shader == null) shader = Shader.Find("ZeroDuty/InteractableOutline");
        Shader silhouetteShader = Resources.Load<Shader>("ZeroHourAssets/Shaders/InteractableSilhouetteMask");
        if (silhouetteShader == null) silhouetteShader = Shader.Find("ZeroDuty/InteractableSilhouetteMask");
        if (shader != null && silhouetteShader != null)
        {
            outlineMaterial = new Material(shader) { name = "Interaction Outline (Runtime)" };
            outlineMaterial.SetColor("_OutlineColor", outlineColor);
            outlineMaterial.SetFloat("_OutlineWidth", outlineWidth);
            silhouetteMaterial = new Material(silhouetteShader) { name = "Interaction Silhouette Mask (Runtime)" };
        }
        else Debug.LogWarning("Interaction silhouette shaders are missing; text hints will still work.", this);
        BuildHint();
    }

    private void LateUpdate()
    {
        MonoBehaviour next = interactor != null ? interactor.FeedbackTarget : null;
        bool targetChanged = next != target;
        if (targetChanged)
        {
            ClearOutline();
            target = next;
            if (target != null && outlineMaterial != null && silhouetteMaterial != null) BuildOutline(target);
        }

        if (hint != null)
        {
            bool showFirstDropHint = interactor != null && interactor.InputEnabled
                && interactor.HeldItem != null && Time.unscaledTime < dropHintUntil;
            string message = showFirstDropHint ? "Press Q to drop"
                : target is ReferencePictureView ? "Click to inspect"
                : target != null ? "Press E" : string.Empty;
            bool visible = !string.IsNullOrEmpty(message);
            hint.gameObject.SetActive(visible);
            if (visible)
            {
                hint.alpha = 1f;
                if (hintText.text != message) SetHintLayout(message);
            }
        }
        for (int i = 0; i < sources.Count; i++)
            if (copies[i] != null)
                copies[i].enabled = sources[i] != null && sources[i].enabled
                    && sources[i].gameObject.activeInHierarchy;
    }

    private void BuildOutline(MonoBehaviour focus)
    {
        Camera camera = interactor.InteractionCamera;
        if (focus is ShortMusicPlayer)
        {
            AddMusicPlayerOutline(focus);
            return;
        }
        if (focus is PressureTimingPuzzle || focus is SequencePuzzle)
        {
            // Both faults use the same physical power/gas console. Keep the
            // interaction edge on that console rather than picking a nearby
            // valve or switch from the camera-dependent proximity search.
            foreach (MeshRenderer renderer in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
                if (Usable(renderer) && renderer.name == "Power and gas | panel.001")
                {
                    AddOutline(renderer);
                    return;
                }
        }
        if (focus is DisposalUnit disposal)
        {
            foreach (Renderer renderer in disposal.VisualRenderers)
                if (Usable(renderer) && (renderer.name == "V9 Disposal | continuous tapered body.001"
                    || renderer.name == "V9 Disposal | sloped hatch surround.001"
                    || renderer.name == "V9 Disposal | smoked hatch.001"))
                    AddOutline(renderer);
            return;
        }
        if (focus is PickupItem scanner && scanner.ItemId == "scanner")
        {
            AddScannerOutline(scanner, camera);
            return;
        }
        if (focus is PickupItem heldTool && heldTool.IsHeld)
        {
            Renderer dock = FindNearbyVisual(heldTool.HomeAimPosition, camera);
            if (dock != null) AddOutline(dock);
            return;
        }
        Renderer[] renderers = focus.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            Renderer nearbyVisual = FindNearbyVisual(focus, camera);
            if (nearbyVisual != null) AddOutline(nearbyVisual);
            return;
        }
        if (renderers.Length > 18 && camera != null)
        {
            // A desk or imported assembly may contain many meshes. Highlight only
            // the mesh closest to the reticle, not the entire room-sized parent.
            Renderer best = null;
            float bestScore = float.PositiveInfinity;
            foreach (Renderer renderer in renderers)
            {
                if (!Usable(renderer)) continue;
                Vector3 point = camera.WorldToViewportPoint(renderer.bounds.center);
                if (point.z <= 0f) continue;
                float score = Mathf.Pow(point.x - 0.5f, 2f)
                    + Mathf.Pow(point.y - 0.5f, 2f) + point.z * 0.0001f;
                if (score >= bestScore) continue;
                best = renderer;
                bestScore = score;
            }
            if (best != null) AddOutline(best);
            return;
        }

        foreach (Renderer renderer in renderers)
            if (Usable(renderer)) AddOutline(renderer);
        if (outlines.Count == 0)
        {
            Renderer nearbyVisual = FindNearbyVisual(focus, camera);
            if (nearbyVisual != null) AddOutline(nearbyVisual);
        }
    }

    private void AddMusicPlayerOutline(MonoBehaviour musicPlayer)
    {
        Renderer[] parts = musicPlayer.GetComponentsInChildren<Renderer>(true);
        // Write the complete assembly silhouette first. All shifted copies are
        // then rejected inside that UNION, so latches and LEDs cannot acquire
        // their own internal white borders. Pixel offsets also work with this
        // imported mesh's split/flat normals and rotated, scaled scene parent.
        foreach (Renderer part in parts)
        {
            if (!Usable(part)) continue;
            Mesh mesh = part.GetComponent<MeshFilter>().sharedMesh;
            if (mesh != null) AddShell(part, mesh, silhouetteMaterial, "__InteractionOutlineMask");
        }
        const float pixels = 2f;
        for (int direction = 0; direction < 8; direction++)
        {
            float angle = direction * Mathf.PI / 4f;
            Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * pixels;
            foreach (Renderer part in parts)
            {
                if (!Usable(part)) continue;
                Mesh mesh = part.GetComponent<MeshFilter>().sharedMesh;
                if (mesh != null)
                    AddShell(part, mesh, outlineMaterial, "__InteractionOutlineEdge", false, offset);
            }
        }
    }

    private void AddScannerOutline(PickupItem scanner, Camera camera)
    {
        // This mesh is the scanner's continuous outer shell. Outlining every trim,
        // button and display mesh creates overlapping stencil masks, so only a
        // small fragment of the tool can remain visible as an edge.
        if (!scanner.IsHeld)
        {
            foreach (Renderer renderer in scanner.GetComponentsInChildren<Renderer>(true))
                if (Usable(renderer) && renderer.name.Equals("Scanner | continuous body.001",
                    StringComparison.OrdinalIgnoreCase))
                {
                    AddOutline(renderer, true);
                    return;
                }
        }

        Vector3 dockCenter = scanner.HomeAimPosition;
        if (!scanner.IsDropped)
            foreach (MeshRenderer renderer in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                if (!Usable(renderer) || renderer.transform.IsChildOf(scanner.transform)
                    || !renderer.name.Equals("Docked | Scanner | continuous body.001",
                        StringComparison.OrdinalIgnoreCase))
                    continue;
                AddOutline(renderer, true);
                return;
            }

        if (outlines.Count == 0)
        {
            Renderer nearbyVisual = FindNearbyVisual(dockCenter, camera);
            if (nearbyVisual != null) AddOutline(nearbyVisual);
        }
    }

    private static Renderer FindNearbyVisual(MonoBehaviour focus, Camera camera)
    {
        Collider proxy = focus.GetComponent<Collider>();
        Vector3 center = proxy != null ? proxy.bounds.center : focus.transform.position;
        return FindNearbyVisual(center, camera);
    }

    private static Renderer FindNearbyVisual(Vector3 center, Camera camera)
    {
        if (camera == null) return null;
        Renderer best = null;
        float bestScore = float.PositiveInfinity;
        foreach (MeshRenderer renderer in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
        {
            if (!Usable(renderer)) continue;
            Bounds bounds = renderer.bounds;
            if (bounds.size.magnitude > 3.5f) continue; // Never light a whole wall or room.
            float gap = Vector3.Distance(bounds.ClosestPoint(center), center);
            if (gap > 0.9f) continue;
            Vector3 point = camera.WorldToViewportPoint(bounds.center);
            if (point.z <= 0f || Mathf.Abs(point.x - 0.5f) > 0.35f
                || Mathf.Abs(point.y - 0.5f) > 0.35f) continue;
            float score = gap * 2f + Mathf.Abs(point.x - 0.5f)
                + Mathf.Abs(point.y - 0.5f) + bounds.size.magnitude * 0.04f;
            if (score >= bestScore) continue;
            best = renderer;
            bestScore = score;
        }
        return best;
    }

    private static bool Usable(Renderer renderer)
        => renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy
            && renderer is MeshRenderer && renderer.GetComponent<MeshFilter>() != null
            && !renderer.gameObject.name.StartsWith("__InteractionOutline");

    private void AddOutline(Renderer original, bool scannerOuterShell = false)
    {
        Mesh mesh = original.GetComponent<MeshFilter>().sharedMesh;
        if (mesh == null) return;
        AddShell(original, mesh, silhouetteMaterial, "__InteractionOutlineMask");
        AddShell(original, mesh, outlineMaterial, "__InteractionOutlineEdge", scannerOuterShell);
    }

    private void AddShell(Renderer original, Mesh mesh, Material material, string shellName,
        bool scannerOuterShell = false, Vector2 pixelOffset = default)
    {
        var shell = new GameObject(shellName, typeof(MeshFilter), typeof(MeshRenderer));
        shell.transform.SetParent(original.transform, false);
        if (scannerOuterShell)
        {
            // This imported mesh has unreliable vertex normals. Expand its silhouette
            // around the mesh centre instead of relying on normal extrusion.
            const float scale = 1.045f;
            shell.transform.localScale = Vector3.one * scale;
            shell.transform.localPosition = mesh.bounds.center * (1f - scale);
        }
        shell.layer = original.gameObject.layer;
        shell.GetComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer copy = shell.GetComponent<MeshRenderer>();
        int count = Mathf.Max(1, mesh.subMeshCount);
        var materials = new Material[count];
        for (int i = 0; i < count; i++) materials[i] = material;
        copy.sharedMaterials = materials;
        if (scannerOuterShell || pixelOffset.sqrMagnitude > 0f)
        {
            var properties = new MaterialPropertyBlock();
            properties.SetFloat("_OutlineWidth", 0f);
            properties.SetVector("_OutlinePixelOffset", new Vector4(pixelOffset.x, pixelOffset.y, 0f, 0f));
            copy.SetPropertyBlock(properties);
        }
        copy.shadowCastingMode = ShadowCastingMode.Off;
        copy.receiveShadows = false;
        copy.lightProbeUsage = LightProbeUsage.Off;
        copy.reflectionProbeUsage = ReflectionProbeUsage.Off;
        outlines.Add(shell);
        sources.Add(original);
        copies.Add(copy);
    }

    private void ClearOutline()
    {
        foreach (GameObject shell in outlines)
            if (shell != null) Destroy(shell);
        outlines.Clear();
        sources.Clear();
        copies.Clear();
    }

    private void BuildHint()
    {
        var root = new GameObject("Interaction Hint", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(CanvasGroup));
        root.transform.SetParent(transform, false);
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 130;
        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        hint = root.GetComponent<CanvasGroup>();

        var panel = new GameObject("Fixed Interaction Prompt", typeof(RectTransform), typeof(Image),
            typeof(UnityEngine.UI.Outline));
        panel.transform.SetParent(root.transform, false);
        RectTransform hintRect = panel.GetComponent<RectTransform>();
        hintPanel = hintRect;
        hintRect.anchorMin = hintRect.anchorMax = hintRect.pivot = new Vector2(0.5f, 0.5f);
        hintRect.anchoredPosition = new Vector2(0f, -275f);
        hintRect.sizeDelta = new Vector2(300f, 76f);
        Image background = panel.GetComponent<Image>();
        background.color = new Color(0.015f, 0.025f, 0.035f, 0.68f);
        background.raycastTarget = false;
        UnityEngine.UI.Outline border = panel.GetComponent<UnityEngine.UI.Outline>();
        border.effectColor = new Color(0.68f, 0.75f, 0.8f, 0.54f);
        border.effectDistance = new Vector2(1f, -1f);

        keycap = new GameObject("Keycap", typeof(RectTransform), typeof(Image),
            typeof(UnityEngine.UI.Outline));
        keycap.transform.SetParent(panel.transform, false);
        keycapRect = keycap.GetComponent<RectTransform>();
        keycapRect.anchorMin = keycapRect.anchorMax = keycapRect.pivot = new Vector2(0.5f, 0.5f);
        keycapRect.anchoredPosition = new Vector2(-99f, 0f);
        keycapRect.sizeDelta = new Vector2(52f, 52f);
        Image keycapBackground = keycap.GetComponent<Image>();
        keycapBackground.color = new Color(0.08f, 0.095f, 0.11f, 0.94f);
        keycapBackground.raycastTarget = false;
        UnityEngine.UI.Outline keycapBorder = keycap.GetComponent<UnityEngine.UI.Outline>();
        keycapBorder.effectColor = new Color(0.8f, 0.85f, 0.89f, 0.75f);
        keycapBorder.effectDistance = new Vector2(1f, -1f);
        var keyLetter = new GameObject("Key Letter", typeof(RectTransform), typeof(TextMeshProUGUI));
        keyLetter.transform.SetParent(keycap.transform, false);
        RectTransform keyLetterRect = keyLetter.GetComponent<RectTransform>();
        keyLetterRect.anchorMin = Vector2.zero;
        keyLetterRect.anchorMax = Vector2.one;
        keyLetterRect.offsetMin = keyLetterRect.offsetMax = Vector2.zero;
        keycapText = keyLetter.GetComponent<TextMeshProUGUI>();
        keycapText.font = StationTypography.UIAsset;
        keycapText.fontSize = 31;
        keycapText.alignment = TextAlignmentOptions.Center;
        keycapText.color = Color.white;
        keycapText.raycastTarget = false;

        var letter = new GameObject("Prompt Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        letter.transform.SetParent(panel.transform, false);
        RectTransform textRect = letter.GetComponent<RectTransform>();
        hintTextRect = textRect;
        textRect.anchorMin = textRect.anchorMax = textRect.pivot = new Vector2(0.5f, 0.5f);
        hintText = letter.GetComponent<TextMeshProUGUI>();
        hintText.font = StationTypography.UIAsset;
        hintText.fontSize = 31;
        hintText.fontStyle = FontStyles.Normal;
        hintText.characterSpacing = 0f;
        hintText.alignment = TextAlignmentOptions.Center;
        hintText.color = new Color(0.93f, 0.96f, 0.98f, 0.96f);
        hintText.text = string.Empty;
        hintText.raycastTarget = false;
        hintText.outlineWidth = 0.08f;
        hintText.outlineColor = new Color32(5, 12, 18, 210);
        SetHintLayout("Press E");
        root.SetActive(false);
    }

    private void SetHintLayout(string message)
    {
        bool showKey = message == "Press E" || message == "Press Q to drop";
        bool isDrop = message == "Press Q to drop";
        keycap.SetActive(showKey);
        if (showKey) keycapText.text = isDrop ? "Q" : "E";
        hintPanel.sizeDelta = new Vector2(isDrop ? 410f : showKey ? 300f : 350f, 76f);
        keycapRect.anchoredPosition = new Vector2(isDrop ? -154f : -99f, 0f);
        hintTextRect.anchoredPosition = new Vector2(showKey ? (isDrop ? 37f : 31f) : 0f, 0f);
        hintTextRect.sizeDelta = new Vector2(showKey ? (isDrop ? 310f : 190f) : 330f, 62f);
        hintText.text = message;
    }

    private void OnDisable()
    {
        ClearOutline();
        target = null;
        if (hint != null) hint.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        ClearOutline();
        if (outlineMaterial != null) Destroy(outlineMaterial);
        if (silhouetteMaterial != null) Destroy(silhouetteMaterial);
    }
}
