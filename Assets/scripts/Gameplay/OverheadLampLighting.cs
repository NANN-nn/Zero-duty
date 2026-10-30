using System.Collections.Generic;
using UnityEngine;

/// <summary>Real fixture lights and emissive glass that follow the station's power.</summary>
[DefaultExecutionOrder(1200)]
[DisallowMultipleComponent]
public sealed class OverheadLampLighting : MonoBehaviour
{
    private const float FixtureIntensity = .35f;
    private const float FixtureRange = 2.4f;
    private static readonly int Emission = Shader.PropertyToID("_EmissionColor");
    private sealed class Fixture
    {
        public Renderer glass;
        public Material[] originalMaterials;
        public Light light;
    }
    private readonly List<Fixture> fixtures = new List<Fixture>();
    private readonly Dictionary<Material, Material> materials = new Dictionary<Material, Material>();
    private readonly Dictionary<Material, Color> emissionLevels = new Dictionary<Material, Color>();
    private Light mainLight;
    private float referenceIntensity;
    private bool configured;
    private bool initialized;

    public int FixtureCount => fixtures.Count;

    public void Initialize(Light source, float normalIntensity = -1f)
    {
        if (initialized) return;
        configured = true;
        mainLight = source != null ? source : RenderSettings.sun;
        referenceIntensity = Mathf.Max(.01f, normalIntensity > 0f ? normalIntensity
            : mainLight != null ? mainLight.intensity : 1f);
        initialized = true;
        foreach (Renderer renderer in FindObjectsByType<Renderer>(FindObjectsInactive.Exclude))
        {
            // Names identify the lamp's actual luminous surface, never its frame
            // or every other prop sharing M052. No source asset is modified.
            if (!renderer.name.Contains(" | overhead lamp glass.")) continue;
            Material[] originals = renderer.sharedMaterials;
            Material[] copies = (Material[])originals.Clone();
            bool emissive = false;
            for (int i = 0; i < copies.Length; i++)
            {
                Material original = originals[i];
                if (original == null || !original.HasProperty(Emission)) continue;
                if (!materials.TryGetValue(original, out Material copy))
                {
                    copy = new Material(original) { name = original.name + " Ceiling Lamp (Runtime)" };
                    copy.EnableKeyword("_EMISSION");
                    Color glow = original.GetColor(Emission);
                    if (Mathf.Max(glow.r, glow.g, glow.b) < .01f)
                        glow = new Color(2.52f, 2.76f, 3f);
                    glow.a = 1f;
                    copy.SetColor(Emission, glow);
                    materials.Add(original, copy);
                    emissionLevels.Add(copy, glow);
                }
                copies[i] = copy;
                emissive = true;
            }
            if (!emissive) continue;
            renderer.sharedMaterials = copies;
            var holder = new GameObject(renderer.name + " | local light");
            holder.transform.SetParent(transform, false);
            Bounds bounds = renderer.bounds;
            holder.transform.position = new Vector3(bounds.center.x, bounds.min.y - .04f, bounds.center.z);
            Light light = holder.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(.84f, .92f, 1f);
            light.range = FixtureRange;
            light.intensity = FixtureIntensity;
            light.shadows = LightShadows.None;
            if (mainLight != null) light.cullingMask = mainLight.cullingMask;
            fixtures.Add(new Fixture { glass = renderer, originalMaterials = originals, light = light });
        }
        RefreshPower();
    }

    // Run after the arrival flash, outage, pressure and final blackout systems.
    // Follow their resulting main-light state, rather than replaying signals or
    // overriding them; restoring power can therefore relight fixtures normally.
    private void LateUpdate() => RefreshPower();
    public void RefreshPower()
    {
        if (!initialized) return;
        float level = mainLight == null ? 1f : mainLight.isActiveAndEnabled
            ? Mathf.Clamp01(mainLight.intensity / referenceIntensity) : 0f;
        foreach (var pair in emissionLevels)
            if (pair.Key != null) pair.Key.SetColor(Emission, pair.Value * level);
        foreach (Fixture fixture in fixtures)
        {
            if (fixture.light == null) continue;
            fixture.light.enabled = level > .001f && fixture.glass != null
                && fixture.glass.enabled && fixture.glass.gameObject.activeInHierarchy;
            fixture.light.intensity = FixtureIntensity * level;
        }
    }

    private void OnEnable()
    {
        if (configured && !initialized) Initialize(mainLight, referenceIntensity);
    }
    private void OnDisable() => Release();
    private void OnDestroy() => Release();
    public void Release()
    {
        foreach (Fixture fixture in fixtures)
        {
            if (fixture.glass != null) fixture.glass.sharedMaterials = fixture.originalMaterials;
            if (fixture.light != null)
            {
                fixture.light.enabled = false;
                Dispose(fixture.light.gameObject);
            }
        }
        fixtures.Clear();
        foreach (Material material in materials.Values) if (material != null) Dispose(material);
        materials.Clear(); emissionLevels.Clear(); initialized = false;
    }
    private static void Dispose(Object value)
    {
        if (Application.isPlaying) Destroy(value);
        else DestroyImmediate(value);
    }
}
