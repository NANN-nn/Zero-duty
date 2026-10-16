using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Two short lighting dips and a screen fault when a new anomaly arrives.</summary>
[DefaultExecutionOrder(-800)]
[DisallowMultipleComponent]
public sealed class AnomalyArrivalFeedback : MonoBehaviour
{
    private GameplaySignals signals;
    private float startedAt = -100f;
    private readonly List<Light> dimmedLights = new List<Light>();
    private readonly List<float> lightLevels = new List<float>();
    private GameObject effectsRoot;
    private RawImage noise;
    private Image dip;
    private Texture2D noiseTexture;

    private void Start() => Subscribe();

    private void Subscribe()
    {
        GameplaySignals source = GameplaySignals.Instance;
        if (source == null || source == signals) return;
        if (signals != null) signals.SignalRaised -= OnSignal;
        signals = source;
        signals.SignalRaised += OnSignal;
    }

    private void OnSignal(string signal)
    {
        switch (signal)
        {
            case "corridor_anomaly_appeared":
            case "corridor_interlude_complete":
            case "gas_interlude_complete":
            case "ghost_interlude_complete":
            case "power_interlude_complete":
                startedAt = Time.time;
                if (effectsRoot == null) BuildEffects();
                break;
            case "final_protocol_unlocked":
            case "ending_data_transfer":
            case "ending_facility_lockdown":
                startedAt = -100f;
                RestoreLights();
                break;
        }
    }

    private void Update()
    {
        // Restore before the normal outage/pressure controllers run this frame.
        // Only intensity is touched: a blackout can never be re-enabled here.
        RestoreLights();
        if (signals == null) Subscribe();
    }

    private void LateUpdate()
    {
        float elapsed = Time.time - startedAt;
        bool first = elapsed >= 0.04f && elapsed < 0.16f;
        bool second = elapsed >= 0.31f && elapsed < 0.47f;
        bool flashing = first || second;
        if (flashing)
        {
            foreach (Light light in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (light == null || !light.enabled) continue;
                dimmedLights.Add(light);
                lightLevels.Add(light.intensity);
                light.intensity *= first ? 0.08f : 0.16f;
            }
        }
        if (dip != null) dip.enabled = flashing;
        if (noise != null)
        {
            noise.enabled = flashing;
            if (flashing)
                noise.uvRect = new Rect(0f, Mathf.Repeat(elapsed * 17f, 1f), 1f, 1f);
        }
    }

    private void RestoreLights()
    {
        for (int i = 0; i < dimmedLights.Count; i++)
            if (dimmedLights[i] != null) dimmedLights[i].intensity = lightLevels[i];
        dimmedLights.Clear();
        lightLevels.Clear();
    }

    private void BuildEffects()
    {
        effectsRoot = new GameObject("Anomaly Arrival Interference", typeof(RectTransform), typeof(Canvas));
        Canvas canvas = effectsRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 90;
        RectTransform dimRect = AddScreenRect("Light Dip", effectsRoot.transform);
        dip = dimRect.gameObject.AddComponent<Image>();
        dip.color = new Color(0f, 0f, 0f, 0.24f);
        dip.raycastTarget = false;
        dip.enabled = false;

        noiseTexture = new Texture2D(128, 128, TextureFormat.RGBA32, false)
        {
            name = "Arrival scanline fault", filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Repeat
        };
        Color32[] pixels = new Color32[128 * 128];
        for (int y = 0; y < 128; y++)
        {
            bool band = y % 31 < 2 || y % 47 == 0;
            for (int x = 0; x < 128; x++)
            {
                byte value = (byte)Random.Range(100, 230);
                pixels[y * 128 + x] = new Color32(value, value, value,
                    (byte)(band ? Random.Range(120, 220) : Random.Range(0, 18)));
            }
        }
        noiseTexture.SetPixels32(pixels);
        noiseTexture.Apply();
        RectTransform noiseRect = AddScreenRect("Brief signal error", effectsRoot.transform);
        noise = noiseRect.gameObject.AddComponent<RawImage>();
        noise.texture = noiseTexture;
        noise.color = new Color(.82f, .82f, .82f, .38f);
        noise.raycastTarget = false;
        noise.enabled = false;
    }

    private static RectTransform AddScreenRect(string objectName, Transform parent)
    {
        RectTransform rect = new GameObject(objectName, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }

    private void OnDisable()
    {
        RestoreLights();
        startedAt = -100f;
        if (signals != null) signals.SignalRaised -= OnSignal;
        signals = null;
        if (dip != null) dip.enabled = false;
        if (noise != null) noise.enabled = false;
    }

    private void OnDestroy()
    {
        if (effectsRoot != null) Destroy(effectsRoot);
        if (noiseTexture != null) Destroy(noiseTexture);
    }
}
