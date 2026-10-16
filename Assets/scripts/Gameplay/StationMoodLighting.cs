using UnityEngine;

/// <summary>Small practical light pools for the station's otherwise flat ambient lighting.</summary>
[DisallowMultipleComponent]
public class StationMoodLighting : MonoBehaviour
{
    public enum Zone { ControlRoom, ShootingRoom, SpecimenRoom, Corridor }

    [SerializeField] private Light mainLight;
    [SerializeField, Range(0f, 2f)] private float accentStrength = 1f;

    private struct Accent
    {
        public string name;
        public Vector3 position;
        public Color color;
        public float intensity;
        public float range;

        public Accent(string name, Vector3 position, Color color, float intensity, float range)
        {
            this.name = name;
            this.position = position;
            this.color = color;
            this.intensity = intensity;
            this.range = range;
        }
    }

    private static readonly Accent[] Layout =
    {
        new Accent("Control | cool task light", new Vector3(1.4f, 2.35f, 1.2f),
            new Color(0.63f, 0.80f, 1f), 1.25f, 4.7f),
        new Accent("West station | cool light", new Vector3(-2.8f, 2.4f, 1.9f),
            new Color(0.56f, 0.76f, 1f), 0.8f, 3.6f),
        new Accent("Lab 01 | examination light", new Vector3(6.1f, 2.4f, -5.7f),
            new Color(0.79f, 0.90f, 1f), 1.15f, 4.5f),
        new Accent("Lab 02 | examination light", new Vector3(-3.4f, 2.4f, -5.7f),
            new Color(0.69f, 0.86f, 1f), 1.15f, 4.5f),
        new Accent("Corridor | guide light", new Vector3(1.4f, 2.35f, -6.5f),
            new Color(0.62f, 0.78f, 1f), 0.7f, 3.4f),
        new Accent("Archive | warm practical light", new Vector3(4.8f, 2.2f, -10f),
            new Color(1f, 0.82f, 0.62f), 0.75f, 3.4f)
    };

    private Light[] spawnedLights;
    private readonly float[] cueStartedAt = new float[6];
    private float baselineMainIntensity;

    public void PulseZone(Zone zone)
    {
        int index = zone switch
        {
            Zone.ShootingRoom => 3,
            Zone.SpecimenRoom => 2,
            Zone.Corridor => 4,
            _ => 0
        };
        cueStartedAt[index] = Time.unscaledTime;
    }

    private void Awake()
    {
        if (GetComponent<StationWayfinding>() == null)
            gameObject.AddComponent<StationWayfinding>();
        if (mainLight == null) mainLight = GetComponent<Light>();
        baselineMainIntensity = mainLight != null ? Mathf.Max(0.01f, mainLight.intensity) : 1f;
        spawnedLights = new Light[Layout.Length];
        for (int i = 0; i < cueStartedAt.Length; i++) cueStartedAt[i] = -100f;
        for (int i = 0; i < Layout.Length; i++)
        {
            Accent accent = Layout[i];
            GameObject holder = new GameObject(accent.name);
            holder.transform.SetParent(transform, false);
            holder.transform.position = accent.position;
            Light light = holder.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = accent.color;
            light.intensity = accent.intensity * accentStrength;
            light.range = accent.range;
            light.shadows = LightShadows.None;
            spawnedLights[i] = light;
        }
    }

    private void LateUpdate()
    {
        // The existing pressure and outage systems control the main light.
        // Follow it so these new practical lights flicker and go dark too.
        bool powered = mainLight == null || mainLight.enabled;
        float mainLevel = mainLight == null ? 1f
            : Mathf.Clamp01(mainLight.intensity / baselineMainIntensity);
        for (int i = 0; i < spawnedLights.Length; i++)
        {
            Light light = spawnedLights[i];
            if (light == null) continue;
            float elapsed = Time.unscaledTime - cueStartedAt[i];
            float cue = elapsed >= 0f && elapsed < 0.18f
                || elapsed >= 0.36f && elapsed < 0.56f ? 1f : 0f;
            light.enabled = powered || cue > 0.01f;
            light.intensity = Layout[i].intensity * accentStrength * mainLevel
                + cue * 1.1f;
        }
    }
}
