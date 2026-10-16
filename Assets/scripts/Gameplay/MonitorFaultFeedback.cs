using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Room-specific interference on the physical screens, including close-up view.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(MonitorCameraController))]
public sealed class MonitorFaultFeedback : MonoBehaviour
{
    private readonly MonitorFaultState state = new MonitorFaultState();
    private readonly GameObject[] overlays = new GameObject[3];
    private readonly Material[] materials = new Material[3];
    private readonly float[] startedAt = new float[3];
    private GameplaySignals signals;
    private MonitorCameraController cameras;
    private MonitorScreenClick screens;
    private FinalMonitorEvent finalEvent;

    private void OnEnable() => Subscribe();

    private void Start()
    {
        cameras = GetComponent<MonitorCameraController>();
        screens = GetComponent<MonitorScreenClick>();
        finalEvent = GetComponent<FinalMonitorEvent>();
        Subscribe();
        BuildOverlays();
    }

    private void Subscribe()
    {
        GameplaySignals source = GameplaySignals.Instance;
        if (source == null || source == signals) return;
        if (signals != null) signals.SignalRaised -= OnSignal;
        signals = source;
        // Restore state if this component was enabled after an incident began.
        foreach (string signal in new[] { "corridor_anomaly_appeared", "corridor_disposed",
            "power_interlude_complete", "specimen_room_complete",
            "gas_interlude_complete", "ghost_destroyed" })
            if (signals.HasRaised(signal)) state.Apply(signal);
        signals.SignalRaised += OnSignal;
    }

    private void OnSignal(string signal)
    {
        bool firstWasActive = state.IsActive(0);
        bool secondWasActive = state.IsActive(1);
        bool thirdWasActive = state.IsActive(2);
        state.Apply(signal);
        for (int i = 0; i < 3; i++)
        {
            bool wasActive = i == 0 ? firstWasActive : i == 1 ? secondWasActive : thirdWasActive;
            if (!wasActive && state.IsActive(i)) startedAt[i] = Time.time;
        }
        if (signal == "final_protocol_unlocked" || signal == "ending_data_transfer"
            || signal == "ending_facility_lockdown") state.Clear();
    }

    private void LateUpdate()
    {
        if (signals == null) Subscribe();
        bool allow = ShiftStartMenu.ShiftStarted && !ShiftAudioDirector.EndingActive
            && (finalEvent == null || !finalEvent.HasStarted);
        for (int i = 0; i < 3; i++)
        {
            if (overlays[i] == null || materials[i] == null) continue;
            bool show = allow && state.IsActive(i);
            overlays[i].SetActive(show);
            if (!show) continue;
            float elapsed = Mathf.Max(0f, Time.time - startedAt[i]);
            // Two readable arrival glitches; then brief, irregular bursts.
            float period = Mathf.Lerp(3.4f, 1.1f, Mathf.Clamp01(elapsed / 120f));
            float phase = Mathf.Repeat(elapsed + i * .31f, period);
            bool pulse = elapsed < .18f || elapsed >= .36f && elapsed < .58f
                || phase < .16f || phase > .29f && phase < .35f;
            materials[i].SetFloat("_FaultTime", elapsed);
            materials[i].SetFloat("_CueAmount", pulse ? 1f : 0f);
        }
    }

    private void BuildOverlays()
    {
        Shader shader = Resources.Load<Shader>("ZeroHourAssets/Shaders/MonitorFault");
        if (shader == null || !shader.isSupported)
        {
            Debug.LogWarning("CCTV fault shader is unavailable; the original feeds are unchanged.", this);
            return;
        }
        Camera viewer = Camera.main;
        for (int i = 0; i < 3; i++)
        {
            Renderer screen = screens != null ? screens.GetDisplay(i) : null;
            Camera feed = cameras.GetCamera(i);
            MeshFilter filter = screen != null ? screen.GetComponent<MeshFilter>() : null;
            if (filter == null || filter.sharedMesh == null || feed == null || feed.targetTexture == null)
                continue;
            Bounds panel = filter.sharedMesh.bounds;
            float side = viewer != null
                ? Mathf.Sign(screen.transform.InverseTransformPoint(viewer.transform.position).y) : 1f;
            if (side == 0f) side = 1f;
            Material material = new Material(shader) { name = $"CCTV {i + 1} Room Fault (Runtime)" };
            material.SetTexture("_BaseMap", feed.targetTexture);
            material.SetFloat("_CueAmount", 0f);
            materials[i] = material;
            GameObject overlay = GameObject.CreatePrimitive(PrimitiveType.Quad);
            overlay.name = $"CCTV {i + 1} Room Fault (Runtime)";
            overlay.layer = screen.gameObject.layer;
            Collider collider = overlay.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);
            overlay.transform.SetParent(screen.transform, false);
            // Above normal close-up feed (.015); below final sequence (.025+).
            overlay.transform.localPosition = panel.center
                + Vector3.up * side * (panel.extents.y + .022f);
            overlay.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            overlay.transform.localScale = new Vector3(panel.size.x * .98f, panel.size.z * .98f, 1f);
            Renderer renderer = overlay.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            overlay.SetActive(false);
            overlays[i] = overlay;
        }
    }

    private void OnDisable()
    {
        if (signals != null) signals.SignalRaised -= OnSignal;
        signals = null;
        state.Clear();
        foreach (GameObject overlay in overlays) if (overlay != null) overlay.SetActive(false);
    }

    private void OnDestroy()
    {
        foreach (GameObject overlay in overlays) if (overlay != null) Destroy(overlay);
        foreach (Material material in materials) if (material != null) Destroy(material);
    }
}
