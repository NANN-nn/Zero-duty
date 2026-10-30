using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.UI;
using UnityEngine.Serialization;

/// <summary>Corridor prototype: escalating time pressure, failure, then a quiet shift interval.</summary>
[DisallowMultipleComponent]
public class CorridorPressureSequence : MonoBehaviour
{
    [SerializeField] private string appearedSignal = "corridor_anomaly_appeared";
    [SerializeField] private string resolvedSignal = "corridor_disposed";
    [SerializeField] private string interludeCompleteSignal = "corridor_interlude_complete";
    [SerializeField, Min(15f)] private float timeLimitSeconds = 120f;
    [SerializeField] private Vector2 interludeSeconds = Vector2.zero;
    [SerializeField, Min(0.1f)] private float nextAnomalyWatchSeconds = 10f;
    [SerializeField] private Renderer corridorMonitorScreen;
    [SerializeField] private Transform monitorViewerPose;
    [SerializeField] private Light mainLight;
    [SerializeField] private Light redWarningLight;
    [SerializeField] private GameObject shadowVisualSource;
    [SerializeField] private Transform shadowAnchor;
    [SerializeField] private Vector3 shadowOffset = new Vector3(0.6f, 0f, -0.8f);
    [FormerlySerializedAs("ghostApproachMeters")]
    [SerializeField, Min(.1f)] private float ghostChaseSpeed = 3f;
    [SerializeField, Min(.5f)] private float ghostFinalDistance = 1.1f;

    private GameplaySignals signals;
    private float remaining;
    private float interludeRemaining;
    private bool active;
    private bool resolved;
    private bool failed;
    private bool externalPressureActive;
    private bool interludeRunning;
    private bool interludeReady;
    private float nextAnomalyWatchTime;
    private MonitorViewManager monitorView;
    private bool monitorEscalated;
    private bool lightEscalated;
    private bool shadowEscalated;
    private bool lightPulseActive;
    private float nextMonitorPulse;
    private float monitorPulseUntil;
    private float nextScreenPulse;
    private float screenPulseUntil;
    private float nextLightPulse;
    private float lightPulseUntil;
    private float nextShadowPulse;
    private float shadowPulseUntil;
    private bool mainLightWasOn;
    private bool redLightWasOn;
    private float mainLightIntensity;
    private float redLightIntensity;
    private AmbientMode originalAmbientMode;
    private Color originalAmbientColor;
    private float originalAmbientIntensity;
    private float originalReflectionIntensity;
    private GameObject monitorNoise;
    private GameObject shadowVisual;
    private bool renderedGhostActive;
    private bool shadowDispelled;
    private Transform playerTransform;
    private Camera ghostViewer;
    private bool ghostApproachStarted;
    private float ghostInitialDistance;
    private float ghostFloorHeight;
    private Vector3 ghostAvoidanceDirection;
    private int ghostUpdatedFrame = -1;
    private Material noiseMaterial;
    private Material shadowMaterial;
    private Texture2D noiseTexture;
    private Texture2D screenGlitchTexture;
    private GameObject screenEffectsCanvas;
    private RawImage screenGlitchImage;
    private Image mainLightDipOverlay;
#if UNITY_EDITOR
    private bool editorGhostPreview;
    private float editorGhostPreviewStartedAt;
    private const float EditorGhostPreviewSeconds = 15f;
#endif

    private void Start()
    {
        // The wait now starts at the CCTV seat, not at task completion.
        interludeSeconds = Vector2.zero;
        nextAnomalyWatchSeconds = 10f;
        signals = GameplaySignals.Instance;
        if (signals == null)
        {
            Debug.LogError("CorridorPressureSequence needs GameplaySignals.", this);
            enabled = false;
            return;
        }
        signals.SignalRaised += OnSignalRaised;
        monitorView = FindAnyObjectByType<MonitorViewManager>();
        CaptureLightingBaseline();
        if (signals.HasRaised(resolvedSignal)) Resolve();
        else if (signals.HasRaised(appearedSignal)) BeginCountdown();
    }

    private void CaptureLightingBaseline()
    {
        mainLightWasOn = mainLight != null && mainLight.enabled;
        redLightWasOn = redWarningLight != null && redWarningLight.enabled;
        mainLightIntensity = mainLight != null ? mainLight.intensity : 0f;
        redLightIntensity = redWarningLight != null ? redWarningLight.intensity : 0f;
        originalAmbientMode = RenderSettings.ambientMode;
        originalAmbientColor = RenderSettings.ambientLight;
        originalAmbientIntensity = RenderSettings.ambientIntensity;
        originalReflectionIntensity = RenderSettings.reflectionIntensity;
    }

    private void OnSignalRaised(string signalId)
    {
        if (failed || resolved) return;
        if (signalId == appearedSignal) BeginCountdown();
        else if (signalId == resolvedSignal) Resolve();
    }

    private void BeginCountdown()
    {
        if (active || resolved || failed) return;
        active = true;
        remaining = timeLimitSeconds;
        Debug.Log($"[Corridor] Anomaly visible; {timeLimitSeconds:0}s until containment failure.", this);
    }

    private void Update()
    {
        if (failed || Time.timeScale <= 0f) return;

#if UNITY_EDITOR
        if (Keyboard.current != null && Keyboard.current.f12Key.wasPressedThisFrame)
        {
            editorGhostPreview = !editorGhostPreview;
            editorGhostPreviewStartedAt = Time.time;
            ResetGhostApproach();
            nextShadowPulse = 0f;
            if (shadowVisual != null) shadowVisual.SetActive(false);
            Debug.Log(editorGhostPreview
                ? "[Ghost Test] F12 preview started: watch the corridor for 15 seconds."
                : "[Ghost Test] F12 preview stopped.", this);
        }
        if (active && Keyboard.current != null && Keyboard.current.f6Key.wasPressedThisFrame)
        {
            Fail();
            return;
        }
        if (interludeRunning && Keyboard.current != null && Keyboard.current.f5Key.wasPressedThisFrame)
            interludeRemaining = 0f;
#endif

        if (interludeRunning)
        {
            interludeRemaining -= Time.deltaTime;
            if (interludeRemaining <= 0f)
            {
                interludeRunning = false;
                interludeReady = true;
            }
        }

        // Overview and close-up feeds both count. Leaving the chair resets the wait.
        if (interludeReady)
        {
            if (monitorView == null)
                monitorView = FindAnyObjectByType<MonitorViewManager>();
            nextAnomalyWatchTime = monitorView != null && monitorView.IsSeated
                ? nextAnomalyWatchTime + Time.deltaTime : 0f;
            if (nextAnomalyWatchTime >= nextAnomalyWatchSeconds)
            {
                interludeReady = false;
                signals?.Raise(interludeCompleteSignal);
            }
        }

        if (!active)
        {
#if UNITY_EDITOR
            UpdateEditorGhostPreview();
#endif
            return;
        }
        remaining -= Time.deltaTime;
        if (remaining <= 0f)
        {
            Fail();
            return;
        }
        ApplyPressure(1f - remaining / timeLimitSeconds);
#if UNITY_EDITOR
        UpdateEditorGhostPreview();
#endif
    }

#if UNITY_EDITOR
    private void UpdateEditorGhostPreview()
    {
        if (!editorGhostPreview) return;
        float elapsed = Time.time - editorGhostPreviewStartedAt;
        if (elapsed >= EditorGhostPreviewSeconds)
        {
            editorGhostPreview = false;
            if (shadowVisual != null) shadowVisual.SetActive(false);
            ResetGhostApproach();
            return;
        }
        float progress = Mathf.Lerp(0.75f, 0.999f,
            Mathf.Clamp01(elapsed / EditorGhostPreviewSeconds));
        UpdateShadowPulse(progress);
    }
#endif

    private void ApplyPressure(float progress)
    {
        ShiftAudioDirector.SetPressure(progress);
        if (progress >= 0.25f) monitorEscalated = true;
        if (progress >= 0.5f) lightEscalated = true;
        if (progress >= 0.75f) shadowEscalated = true;
        if (monitorEscalated)
        {
            // Physical CCTV faults are routed to their matching room by
            // MonitorFaultFeedback. Never flash the corridor feed for another room.
            UpdateScreenGlitch(progress);
        }
        if (lightEscalated) UpdateLightPulse(progress);
        if (shadowEscalated)
        {
            UpdateShadowPulse(progress);
            if (!lightPulseActive) DimForFinalWarning(progress);
        }
    }

    public void BeginExternalPressure()
    {
        if (failed) return;
        CaptureLightingBaseline();
        ResetGhostApproach();
        monitorEscalated = lightEscalated = shadowEscalated = false;
        nextMonitorPulse = nextScreenPulse = nextLightPulse = nextShadowPulse = 0f;
        externalPressureActive = true;
    }

    public void SetExternalPressure(float progress)
    {
        if (externalPressureActive && !failed)
            ApplyPressure(Mathf.Clamp01(progress));
    }

    public void EndExternalPressure(bool restoreLighting = true)
    {
        if (!externalPressureActive || failed) return;
        externalPressureActive = false;
        ShiftAudioDirector.ClearPressure();
        if (restoreLighting) ClearEscalation();
        else
        {
            if (monitorNoise != null) monitorNoise.SetActive(false);
            if (shadowVisual != null) shadowVisual.SetActive(false);
            if (screenGlitchImage != null) screenGlitchImage.enabled = false;
            if (mainLightDipOverlay != null) mainLightDipOverlay.enabled = false;
            lightPulseActive = false;
        }
    }

    private void UpdateMonitorPulse(float progress)
    {
        if (monitorNoise == null) BuildMonitorNoise();
        if (monitorNoise == null) return;
        float now = Time.time;
        if (now >= nextMonitorPulse)
        {
            monitorNoise.SetActive(true);
            monitorPulseUntil = now + Random.Range(0.06f, 0.16f);
            nextMonitorPulse = now + Mathf.Lerp(1.1f, 0.25f, progress)
                * Random.Range(0.7f, 1.25f);
        }
        if (now >= monitorPulseUntil) monitorNoise.SetActive(false);
    }

    private void UpdateScreenGlitch(float progress)
    {
        if (screenEffectsCanvas == null) BuildScreenEffects();
        if (screenGlitchImage == null) return;
        float now = Time.time;
        if (now >= nextScreenPulse)
        {
            screenGlitchImage.enabled = true;
            screenGlitchImage.color = new Color(.82f, .82f, .82f,
                Mathf.Lerp(0.14f, 0.26f, progress));
            screenGlitchImage.uvRect = new Rect(0f, Random.Range(0f, 1f), 1f, 1f);
            screenPulseUntil = now + Random.Range(0.07f, 0.17f);
            nextScreenPulse = now + Mathf.Lerp(3.2f, 0.65f, progress)
                * Random.Range(0.78f, 1.25f);
        }
        if (now >= screenPulseUntil) screenGlitchImage.enabled = false;
    }

    private void UpdateLightPulse(float progress)
    {
        if (screenEffectsCanvas == null) BuildScreenEffects();
        float now = Time.time;
        if (now >= nextLightPulse)
        {
            lightPulseUntil = now + Random.Range(0.13f, 0.28f);
            nextLightPulse = now + Mathf.Lerp(0.9f, 0.32f, progress)
                * Random.Range(0.75f, 1.2f);
        }
        bool pulseOn = now < lightPulseUntil;
        lightPulseActive = pulseOn;
        if (mainLight != null)
        {
            mainLight.enabled = mainLightWasOn && !pulseOn;
            float finalDim = shadowEscalated
                ? Mathf.Lerp(1f, 0.2f, Mathf.InverseLerp(0.75f, 1f, progress)) : 1f;
            mainLight.intensity = mainLightIntensity * finalDim;
        }
        if (redWarningLight != null)
        {
            redWarningLight.enabled = pulseOn;
            redWarningLight.intensity = redLightIntensity;
        }
        if (mainLightDipOverlay != null) mainLightDipOverlay.enabled = pulseOn;
        if (pulseOn)
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Color.black;
            RenderSettings.ambientIntensity = 0f;
            RenderSettings.reflectionIntensity = 0f;
        }
        else if (!shadowEscalated)
        {
            RenderSettings.ambientMode = originalAmbientMode;
            RenderSettings.ambientLight = originalAmbientColor;
            RenderSettings.ambientIntensity = originalAmbientIntensity;
            RenderSettings.reflectionIntensity = originalReflectionIntensity;
        }
    }

    private void UpdateShadowPulse(float progress)
    {
        if (shadowDispelled) return;
        if (shadowVisual == null) BuildShadowVisual();
        if (shadowVisual == null) return;

        MoveShadowTowardPlayer(progress);

        if (renderedGhostActive && shadowAnchor != null)
        {
            // A long glimpse followed by a short disappearance makes its
            // approach readable, instead of a single-frame flash in one spot.
            if (Time.time >= nextShadowPulse)
            {
                bool reveal = !shadowVisual.activeSelf;
                shadowVisual.SetActive(reveal);
                nextShadowPulse = Time.time + (reveal
                    ? Random.Range(0.65f, 1.3f) : Random.Range(0.08f, 0.18f));
            }
            return;
        }

        float now = Time.time;
        if (now >= nextShadowPulse)
        {
            shadowVisual.SetActive(true);
            shadowPulseUntil = now + Random.Range(0.09f, 0.22f);
            nextShadowPulse = now + Random.Range(0.5f, 1.1f);
        }
        if (now >= shadowPulseUntil) shadowVisual.SetActive(false);
    }

    private void ResetGhostApproach()
    {
        shadowDispelled = false;
        ghostApproachStarted = false;
        ghostAvoidanceDirection = Vector3.zero;
        ghostUpdatedFrame = -1;
        if (shadowVisual != null && shadowAnchor != null)
        {
            shadowVisual.transform.position = shadowAnchor.position + shadowOffset;
            shadowVisual.GetComponentInChildren<RenderedGhostBillboard>(true)?.RandomizePose();
        }
    }

    public bool DispelWarningGhost()
    {
        if (shadowDispelled || shadowVisual == null || !shadowVisual.activeInHierarchy)
            return false;
        shadowDispelled = true;
        shadowVisual.SetActive(false);
        // Deliberately do not change remaining, external pressure, lighting,
        // completion signals or any of the anomaly task's failure clocks.
        return true;
    }

    private void MoveShadowTowardPlayer(float progress)
    {
        // The normal countdown and editor preview may both call this in a frame.
        // Integrate movement once, rather than accidentally doubling chase speed.
        if (ghostUpdatedFrame == Time.frameCount || Time.deltaTime <= 0f) return;
        ghostUpdatedFrame = Time.frameCount;
        if (playerTransform == null)
        {
            playerController player = FindAnyObjectByType<playerController>();
            if (player != null) playerTransform = player.transform;
        }
        if (ghostViewer == null)
        {
            PlayerInteractor interactor = FindAnyObjectByType<PlayerInteractor>();
            ghostViewer = interactor != null ? interactor.InteractionCamera : Camera.main;
        }
        if (playerTransform == null && ghostViewer == null) return;
        Vector3 target = ghostViewer != null ? ghostViewer.transform.position : playerTransform.position;
        Vector3 position = shadowVisual.transform.position;
        target.y = position.y;
        Vector3 towardPlayer = target - position;
        float distance = towardPlayer.magnitude;
        if (!ghostApproachStarted)
        {
            ghostInitialDistance = distance;
            ghostFloorHeight = position.y;
            ghostApproachStarted = true;
        }
        if (distance < .001f) return;
        float step = GhostApproachProfile.Step(distance, ghostInitialDistance, progress,
            ghostFinalDistance, ghostChaseSpeed, Time.deltaTime);
        if (step <= 0f) return;
        Vector3 wanted = towardPlayer / distance * step;
        Vector3 allowed = AllowedGhostMove(position, wanted, out Vector3 wallNormal);
        if ((allowed - wanted).sqrMagnitude > .000001f)
        {
            // Follow an actual wall tangent instead of repeatedly stepping into
            // the wall. Keep the chosen side until the direct route opens, so
            // a symmetric obstacle cannot make it oscillate left/right in place.
            Vector3 tangent = Vector3.Cross(Vector3.up, wallNormal).normalized;
            if (tangent.sqrMagnitude > .01f)
            {
                Vector3 preference = ghostAvoidanceDirection.sqrMagnitude > .01f
                    ? ghostAvoidanceDirection : towardPlayer;
                if (Vector3.Dot(tangent, preference) < 0f) tangent = -tangent;
                Vector3 first = AllowedGhostMove(position, tangent * step, out _);
                Vector3 second = AllowedGhostMove(position, -tangent * step, out _);
                Vector3 slide = first.magnitude >= step * .5f || first.sqrMagnitude >= second.sqrMagnitude
                    ? first : second;
                if (slide.sqrMagnitude > allowed.sqrMagnitude)
                {
                    allowed = slide;
                    ghostAvoidanceDirection = slide.normalized;
                }
            }
        }
        else ghostAvoidanceDirection = Vector3.zero;
        position += allowed;
        position.y = ghostFloorHeight;
        shadowVisual.transform.position = position;
    }

    private Vector3 AllowedGhostMove(Vector3 position, Vector3 wanted, out Vector3 wallNormal)
    {
        wallNormal = Vector3.zero;
        float distance = wanted.magnitude;
        if (distance < .0001f) return Vector3.zero;
        float allowed = distance;
        foreach (RaycastHit hit in Physics.SphereCastAll(position, .18f, wanted / distance,
            distance + .03f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider == null || hit.collider.transform.IsChildOf(shadowVisual.transform)
                || playerTransform != null && hit.collider.transform.IsChildOf(playerTransform)) continue;
            float clearDistance = Mathf.Max(0f, hit.distance - .03f);
            if (clearDistance < allowed)
            {
                allowed = clearDistance;
                wallNormal = hit.normal;
            }
        }
        return wanted / distance * allowed;
    }

    private void DimForFinalWarning(float progress)
    {
        float dim = Mathf.InverseLerp(0.75f, 1f, progress);
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = Color.Lerp(new Color(0.06f, 0.08f, 0.11f),
            Color.black, dim);
        RenderSettings.ambientIntensity = originalAmbientIntensity * (1f - dim);
        RenderSettings.reflectionIntensity = originalReflectionIntensity * (1f - dim);
    }

    private void Resolve()
    {
        if (resolved || failed) return;
        resolved = true;
        active = false;
        ClearEscalation();
        float first = Mathf.Max(0f, Mathf.Min(interludeSeconds.x, interludeSeconds.y));
        float last = Mathf.Max(first, Mathf.Max(interludeSeconds.x, interludeSeconds.y));
        interludeRemaining = Random.Range(first, last);
        interludeRunning = true;
        Debug.Log($"[Corridor] Cleared. Quiet interval: {interludeRemaining:0}s.", this);
    }

    private void Fail()
    {
        if (failed || resolved) return;
        TriggerFailure();
    }

    public void TriggerFailure()
    {
        if (failed) return;
        failed = true;
        active = false;
        interludeRunning = false;
        if (!resolved || externalPressureActive) ClearEscalation();
        externalPressureActive = false;
        foreach (Light light in FindObjectsByType<Light>())
            if (light != null) light.enabled = false;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = Color.black;
        RenderSettings.ambientIntensity = 0f;
        RenderSettings.reflectionIntensity = 0f;
        ShiftEndingScreen.Instance.ShowFailure();
        Debug.Log("[Shift] Containment failed. The ending screen is ready.", this);
    }

    private void ClearEscalation()
    {
        ResetGhostApproach();
        ShiftAudioDirector.ClearPressure();
        if (monitorNoise != null) monitorNoise.SetActive(false);
        if (shadowVisual != null) shadowVisual.SetActive(false);
        if (screenGlitchImage != null) screenGlitchImage.enabled = false;
        if (mainLightDipOverlay != null) mainLightDipOverlay.enabled = false;
        lightPulseActive = false;
        if (mainLight != null)
        {
            mainLight.enabled = mainLightWasOn;
            mainLight.intensity = mainLightIntensity;
        }
        if (redWarningLight != null)
        {
            redWarningLight.enabled = redLightWasOn;
            redWarningLight.intensity = redLightIntensity;
        }
        RenderSettings.ambientMode = originalAmbientMode;
        RenderSettings.ambientLight = originalAmbientColor;
        RenderSettings.ambientIntensity = originalAmbientIntensity;
        RenderSettings.reflectionIntensity = originalReflectionIntensity;
    }

    private void BuildScreenEffects()
    {
        screenEffectsCanvas = new GameObject("Corridor Screen Errors (Runtime)",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        Canvas canvas = screenEffectsCanvas.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 900;
        CanvasScaler scaler = screenEffectsCanvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        screenGlitchTexture = new Texture2D(128, 128, TextureFormat.RGBA32, false)
        {
            name = "Mechanical Screen Error (Runtime)",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Repeat
        };
        Color32[] pixels = new Color32[128 * 128];
        for (int y = 0; y < 128; y++)
        {
            bool brightLine = Random.Range(0, 9) == 0;
            byte alpha = (byte)(brightLine ? Random.Range(100, 180) : Random.Range(8, 28));
            for (int x = 0; x < 128; x++)
            {
                byte value = (byte)(brightLine ? Random.Range(110, 210) : Random.Range(15, 45));
                pixels[y * 128 + x] = new Color32(value, value, value, alpha);
            }
        }
        screenGlitchTexture.SetPixels32(pixels);
        screenGlitchTexture.Apply();

        RectTransform glitchRect = new GameObject("Brief Screen Distortion",
            typeof(RectTransform), typeof(RawImage)).GetComponent<RectTransform>();
        glitchRect.SetParent(screenEffectsCanvas.transform, false);
        StretchToScreen(glitchRect);
        screenGlitchImage = glitchRect.GetComponent<RawImage>();
        screenGlitchImage.texture = screenGlitchTexture;
        screenGlitchImage.raycastTarget = false;
        screenGlitchImage.enabled = false;

        RectTransform dipRect = new GameObject("Synchronized Main-Light Dip",
            typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        dipRect.SetParent(screenEffectsCanvas.transform, false);
        StretchToScreen(dipRect);
        mainLightDipOverlay = dipRect.GetComponent<Image>();
        mainLightDipOverlay.color = new Color(0f, 0f, 0f, 0.32f);
        mainLightDipOverlay.raycastTarget = false;
        mainLightDipOverlay.enabled = false;
    }

    private static void StretchToScreen(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private void BuildMonitorNoise()
    {
        MeshFilter filter = corridorMonitorScreen != null
            ? corridorMonitorScreen.GetComponent<MeshFilter>() : null;
        if (filter == null || filter.sharedMesh == null) return;
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Texture");
        if (shader == null) return;

        noiseTexture = new Texture2D(96, 48, TextureFormat.RGBA32, false)
        {
            name = "Corridor CCTV Distortion (Runtime)", filterMode = FilterMode.Point
        };
        Color32[] pixels = new Color32[96 * 48];
        for (int y = 0; y < 48; y++)
        {
            byte stripe = (byte)(Random.Range(0, 5) == 0 ? 130 : Random.Range(8, 42));
            for (int x = 0; x < 96; x++)
                pixels[y * 96 + x] = new Color32(stripe, stripe, stripe, 255);
        }
        noiseTexture.SetPixels32(pixels);
        noiseTexture.Apply();
        noiseMaterial = new Material(shader) { name = "Corridor CCTV Distortion (Runtime)" };
        noiseMaterial.mainTexture = noiseTexture;
        if (noiseMaterial.HasProperty("_BaseMap")) noiseMaterial.SetTexture("_BaseMap", noiseTexture);
        if (noiseMaterial.HasProperty("_Cull")) noiseMaterial.SetFloat("_Cull", 0f);

        Bounds panel = filter.sharedMesh.bounds;
        float side = monitorViewerPose != null
            ? Mathf.Sign(corridorMonitorScreen.transform.InverseTransformPoint(monitorViewerPose.position).y)
            : 1f;
        if (side == 0f) side = 1f;
        monitorNoise = GameObject.CreatePrimitive(PrimitiveType.Quad);
        monitorNoise.name = "Corridor Monitor Distortion (Runtime)";
        Collider collider = monitorNoise.GetComponent<Collider>();
        if (collider != null) { collider.enabled = false; Destroy(collider); }
        monitorNoise.transform.SetParent(corridorMonitorScreen.transform, false);
        monitorNoise.transform.localPosition = panel.center
            + Vector3.up * side * (panel.extents.y + 0.035f);
        monitorNoise.transform.localRotation = Quaternion.Euler(side > 0f ? -90f : 90f, 0f, 0f);
        monitorNoise.transform.localScale = new Vector3(panel.size.x * 0.98f,
            panel.size.z * 0.98f, 1f);
        Renderer renderer = monitorNoise.GetComponent<Renderer>();
        renderer.sharedMaterial = noiseMaterial;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        monitorNoise.SetActive(false);
    }

    private void BuildShadowVisual()
    {
        if (shadowAnchor == null) return;
        GameObject renderedRoot = new GameObject("Corridor Ghost (Rendered Runtime)");
        renderedRoot.transform.position = shadowAnchor.position + shadowOffset;
        RenderedGhostBillboard rendered = RenderedGhostBillboard.Create(renderedRoot.transform, 0f, 0.85f);
        if (rendered != null)
        {
            shadowVisual = renderedRoot;
            renderedGhostActive = true;
            renderedRoot.AddComponent<PressureGhostTarget>().Initialize(this);
            BoxCollider hitbox = rendered.gameObject.AddComponent<BoxCollider>();
            hitbox.size = new Vector3(1.3f, 2.2f, 0.45f);
            // Bullets can disperse it, but it must never physically block the
            // player or the route to the still-unresolved fault.
            hitbox.isTrigger = true;
            shadowVisual.SetActive(false);
            return;
        }
        Destroy(renderedRoot);

        if (shadowVisualSource == null) return;
        shadowVisual = Instantiate(shadowVisualSource,
            shadowAnchor.position + shadowOffset, shadowVisualSource.transform.rotation);
        shadowVisual.name = "Corridor Shadow (Runtime)";
        shadowVisual.transform.localScale = shadowVisualSource.transform.lossyScale;
        shadowVisual.SetActive(false);
        foreach (MonoBehaviour behaviour in shadowVisual.GetComponentsInChildren<MonoBehaviour>(true))
            if (behaviour != null) behaviour.enabled = false;
        foreach (Collider collider in shadowVisual.GetComponentsInChildren<Collider>(true))
            if (collider != null) collider.enabled = false;
        foreach (Rigidbody body in shadowVisual.GetComponentsInChildren<Rigidbody>(true))
            if (body != null) { body.isKinematic = true; body.useGravity = false; }
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        if (shader != null)
        {
            shadowMaterial = new Material(shader) { name = "Corridor Shadow (Runtime)" };
            shadowMaterial.color = new Color(0.025f, 0.035f, 0.05f);
            if (shadowMaterial.HasProperty("_BaseColor"))
                shadowMaterial.SetColor("_BaseColor", shadowMaterial.color);
            if (shadowMaterial.HasProperty("_Cull")) shadowMaterial.SetFloat("_Cull", 0f);
        }
        foreach (Renderer renderer in shadowVisual.GetComponentsInChildren<Renderer>(true))
        {
            renderer.enabled = true;
            if (shadowMaterial != null) renderer.sharedMaterial = shadowMaterial;
        }
    }

    private void OnDestroy()
    {
        if (failed) Time.timeScale = 1f;
        if (signals != null) signals.SignalRaised -= OnSignalRaised;
        if (monitorNoise != null) Destroy(monitorNoise);
        if (shadowVisual != null) Destroy(shadowVisual);
        if (noiseMaterial != null) Destroy(noiseMaterial);
        if (shadowMaterial != null) Destroy(shadowMaterial);
        if (noiseTexture != null) Destroy(noiseTexture);
        if (screenGlitchTexture != null) Destroy(screenGlitchTexture);
        if (screenEffectsCanvas != null) Destroy(screenEffectsCanvas);
    }
}
