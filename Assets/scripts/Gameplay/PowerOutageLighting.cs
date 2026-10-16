using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Dims the whole station when the post-ghost power fault begins.</summary>
[DisallowMultipleComponent]
public class PowerOutageLighting : MonoBehaviour
{
    [SerializeField] private Light mainLight;
    [SerializeField] private Light emergencyLight;
    [SerializeField] private string outageSignal = "ghost_interlude_complete";
    [SerializeField] private string restoreSignal = "power_restored";
    [SerializeField] private Color outageAmbient = new Color(0.012f, 0.015f, 0.02f);
    [SerializeField, Range(0f, 1f)] private float outageReflectionIntensity = 0.03f;

    private AmbientMode originalAmbientMode;
    private Color originalAmbientColor;
    private float originalAmbientIntensity;
    private float originalReflectionIntensity;
    private bool originalMainLightEnabled;
    private bool originalEmergencyLightEnabled;
    private bool captured;

    private void OnEnable()
    {
        if (GameplaySignals.Instance != null)
            GameplaySignals.Instance.SignalRaised += OnSignalRaised;
    }

    private void Start()
    {
        if (mainLight == null)
            mainLight = RenderSettings.sun;

        originalAmbientMode = RenderSettings.ambientMode;
        originalAmbientColor = RenderSettings.ambientLight;
        originalAmbientIntensity = RenderSettings.ambientIntensity;
        originalReflectionIntensity = RenderSettings.reflectionIntensity;
        originalMainLightEnabled = mainLight != null && mainLight.enabled;
        originalEmergencyLightEnabled = emergencyLight != null && emergencyLight.enabled;
        captured = true;

        // GameplaySignals can be initialized after OnEnable.
        if (GameplaySignals.Instance != null)
        {
            GameplaySignals.Instance.SignalRaised -= OnSignalRaised;
            GameplaySignals.Instance.SignalRaised += OnSignalRaised;
        }

        bool gasRepaired = GameplaySignals.Instance != null
            && GameplaySignals.Instance.HasRaised(outageSignal);
        bool powerRepaired = GameplaySignals.Instance != null
            && GameplaySignals.Instance.HasRaised(restoreSignal);
        SetOutage(gasRepaired && !powerRepaired);

        if (mainLight == null)
            Debug.LogWarning("PowerOutageLighting: assign the scene's Directional Light.", this);
        if (emergencyLight == null)
            Debug.LogWarning("PowerOutageLighting: assign a red emergency Light.", this);
    }

    private void OnSignalRaised(string signalId)
    {
        if (!captured) return;
        if (signalId == outageSignal) SetOutage(true);
        else if (signalId == restoreSignal) SetOutage(false);
    }

    private void SetOutage(bool active)
    {
        if (mainLight != null) mainLight.enabled = active ? false : originalMainLightEnabled;
        if (emergencyLight != null)
            emergencyLight.enabled = active || originalEmergencyLightEnabled;

        RenderSettings.ambientMode = active ? AmbientMode.Flat : originalAmbientMode;
        RenderSettings.ambientLight = active ? outageAmbient : originalAmbientColor;
        RenderSettings.ambientIntensity = active ? 0f : originalAmbientIntensity;
        RenderSettings.reflectionIntensity = active
            ? outageReflectionIntensity : originalReflectionIntensity;
    }

    private void OnDisable()
    {
        if (GameplaySignals.Instance != null)
            GameplaySignals.Instance.SignalRaised -= OnSignalRaised;
        if (captured) SetOutage(false);
    }
}
