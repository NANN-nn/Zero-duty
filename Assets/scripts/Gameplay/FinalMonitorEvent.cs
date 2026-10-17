using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

/// <summary>Replays the resolved anomalies on CCTV, then unlocks the exit reader.</summary>
[DefaultExecutionOrder(-70)]
[DisallowMultipleComponent]
public class FinalMonitorEvent : MonoBehaviour
{
    [SerializeField] private MonitorViewManager monitorView;
    [SerializeField] private Camera playerViewCamera;
    [SerializeField] private Renderer[] screens = new Renderer[3];
    [SerializeField] private GameObject corridorEchoSource;
    [SerializeField] private GameObject specimenEchoSource;
    [SerializeField] private GameObject ghostEchoSource;
    [SerializeField] private PickupItem gun;
    [SerializeField] private PickupItem scanner;
    [SerializeField] private Light corridorGuideLight;
    [SerializeField] private Light controlRoomGuideLight;
    [SerializeField] private string[] requiredSignals =
    {
        "corridor_disposed", "gas_restored", "ghost_destroyed",
        "power_restored", "specimen_room_complete"
    };
    [SerializeField, Min(1f)] private float flickerSeconds = 4f;
    [SerializeField, Min(0.5f)] private float startFlashRate = 3.5f;
    [SerializeField, Min(0.5f)] private float endFlashRate = 9f;
    [SerializeField] private string readySignal = "final_protocol_unlocked";

    private readonly GameObject[] echoes = new GameObject[3];
    private readonly GameObject[] finalFeeds = new GameObject[3];
    private readonly Material[] finalFeedMaterials = new Material[3];
    private readonly GameObject[] interference = new GameObject[3];
    private Material interferenceMaterial;
    private Texture2D interferenceTexture;
    private Light[] sceneLights;
    private float[] originalIntensities;
    private bool[] originalLightEnabled;
    private AmbientMode originalAmbientMode;
    private Color originalAmbientColor;
    private float originalAmbientIntensity;
    private float originalReflectionIntensity;
    private bool lightingCaptured;
    private bool started;
    public bool HasStarted => started;
    private float finalWatchSeconds;

    private void Awake()
    {
        if (monitorView == null) monitorView = GetComponent<MonitorViewManager>();
        if (playerViewCamera == null) playerViewCamera = Camera.main;
        echoes[0] = MakeVisualEcho(corridorEchoSource, "Corridor Echo");
        echoes[1] = MakeVisualEcho(specimenEchoSource, "Specimen Echo");
        echoes[2] = MakeVisualEcho(ghostEchoSource, "Ghost Echo");
    }

    private void Update()
    {
#if UNITY_EDITOR
        if (!started && Application.isFocused && Keyboard.current != null
            && Keyboard.current.f7Key.wasPressedThisFrame)
        {
            if (monitorView != null && monitorView.IsSeated) StartSequence();
            else Debug.Log("Sit at the CCTV desk before pressing F7 to test the final monitor event.", this);
        }
#endif
        if (started || Time.timeScale <= 0f) return;
        finalWatchSeconds = monitorView != null && monitorView.IsSeated && AllWorkComplete()
            ? finalWatchSeconds + Time.deltaTime : 0f;
        if (finalWatchSeconds >= 10f) StartSequence();
    }

    private bool AllWorkComplete()
    {
        GameplaySignals signals = GameplaySignals.Instance;
        if (signals == null) return false;
        foreach (string signal in requiredSignals)
            if (string.IsNullOrWhiteSpace(signal) || !signals.HasRaised(signal)) return false;
        return true;
    }

    private void StartSequence()
    {
        if (started) return;
        started = true;
        monitorView?.ReturnToOverview();
        CaptureLighting();
        BuildFinalFeeds();
        BuildInterference();
        StartCoroutine(PlaySequence());
    }

    private IEnumerator PlaySequence()
    {
        ShiftAudioDirector.StartFinalFlicker(flickerSeconds);
        float finishAt = Time.time + flickerSeconds;
        Coroutine[] screenFlashes = new Coroutine[interference.Length];
        for (int i = 0; i < screenFlashes.Length; i++)
            screenFlashes[i] = StartCoroutine(FlashScreen(i, finishAt));
        Coroutine lightFlashes = StartCoroutine(FlashRoomLights(finishAt));
        while (Time.time < finishAt) yield return null;
        foreach (Coroutine flash in screenFlashes)
            if (flash != null) StopCoroutine(flash);
        if (lightFlashes != null) StopCoroutine(lightFlashes);

        SetVisible(echoes, false);
        SetVisible(interference, false);
        ShiftAudioDirector.StopFinalFlicker();
        BlackoutExceptGuideLights();
        GameplaySignals.Instance?.Raise(readySignal);
    }

    private IEnumerator FlashScreen(int index, float finishAt)
    {
        yield return WaitForBeat(index * 0.065f);
        while (Time.time < finishAt)
        {
            float progress = Mathf.Clamp01(1f - (finishAt - Time.time) / flickerSeconds);
            float rate = Mathf.Lerp(startFlashRate, endFlashRate, progress * progress);
            float cycle = Mathf.Min(Random.Range(0.78f, 1.18f) / rate, finishAt - Time.time);
            float noiseTime = cycle * Random.Range(0.22f, 0.48f);
            if (interference[index] != null) interference[index].SetActive(true);
            if (echoes[index] != null) echoes[index].SetActive(false);
            yield return WaitForBeat(noiseTime);
            if (interference[index] != null) interference[index].SetActive(false);
            if (echoes[index] != null) echoes[index].SetActive(true);
            yield return WaitForBeat(cycle - noiseTime);
        }
    }

    private IEnumerator FlashRoomLights(float finishAt)
    {
        yield return WaitForBeat(0.11f);
        while (Time.time < finishAt)
        {
            float progress = Mathf.Clamp01(1f - (finishAt - Time.time) / flickerSeconds);
            float rate = Mathf.Lerp(startFlashRate * 0.75f, endFlashRate * 0.72f,
                progress * progress);
            float cycle = Mathf.Min(Random.Range(0.76f, 1.22f) / rate, finishAt - Time.time);
            float darkTime = cycle * Random.Range(0.18f, 0.42f);
            SetLightsFactor(Random.Range(0.03f, 0.16f));
            yield return WaitForBeat(darkTime);
            SetLightsFactor(Random.Range(0.55f, 1f) * Mathf.Lerp(1f, 0.65f, progress));
            yield return WaitForBeat(cycle - darkTime);
        }
    }

    private static IEnumerator WaitForBeat(float seconds)
    {
        float elapsed = 0f;
        while (elapsed < seconds)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private void CaptureLighting()
    {
        sceneLights = FindObjectsByType<Light>(FindObjectsSortMode.None);
        originalIntensities = new float[sceneLights.Length];
        originalLightEnabled = new bool[sceneLights.Length];
        for (int i = 0; i < sceneLights.Length; i++)
        {
            originalIntensities[i] = sceneLights[i].intensity;
            originalLightEnabled[i] = sceneLights[i].enabled;
        }
        originalAmbientMode = RenderSettings.ambientMode;
        originalAmbientColor = RenderSettings.ambientLight;
        originalAmbientIntensity = RenderSettings.ambientIntensity;
        originalReflectionIntensity = RenderSettings.reflectionIntensity;
        lightingCaptured = true;
    }

    private void SetLightsFactor(float factor)
    {
        if (sceneLights == null) return;
        for (int i = 0; i < sceneLights.Length; i++)
        {
            Light light = sceneLights[i];
            if (light == null || light == corridorGuideLight || light == controlRoomGuideLight) continue;
            light.enabled = originalLightEnabled[i] && factor > 0.015f;
            light.intensity = originalIntensities[i] * factor;
        }
        RenderSettings.ambientIntensity = originalAmbientIntensity * factor;
        RenderSettings.reflectionIntensity = originalReflectionIntensity * factor;
    }

    private void BlackoutExceptGuideLights()
    {
        SetLightsFactor(0f);
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = Color.black;
        RenderSettings.ambientIntensity = 0f;
        RenderSettings.reflectionIntensity = 0f;
        if (corridorGuideLight != null) corridorGuideLight.enabled = true;
        if (controlRoomGuideLight != null) controlRoomGuideLight.enabled = true;
    }

    private static GameObject MakeVisualEcho(GameObject source, string echoName)
    {
        if (source == null) return null;
        GameObject echo = Instantiate(source, source.transform.position, source.transform.rotation);
        echo.name = echoName;
        echo.transform.localScale = source.transform.lossyScale;
        echo.SetActive(false);

        foreach (MonoBehaviour script in echo.GetComponentsInChildren<MonoBehaviour>(true))
            if (script != null) script.enabled = false;
        foreach (Collider collider in echo.GetComponentsInChildren<Collider>(true))
            if (collider != null) collider.enabled = false;
        foreach (Rigidbody body in echo.GetComponentsInChildren<Rigidbody>(true))
            if (body != null) { body.isKinematic = true; body.useGravity = false; }
        foreach (AudioSource audio in echo.GetComponentsInChildren<AudioSource>(true))
            if (audio != null) audio.enabled = false;
        foreach (Renderer renderer in echo.GetComponentsInChildren<Renderer>(true))
            if (renderer != null) renderer.enabled = true;
        return echo;
    }

    private void BuildFinalFeeds()
    {
        MonitorCameraController cameraControl = GetComponent<MonitorCameraController>();
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Texture");
        if (shader == null || cameraControl == null) return;

        for (int i = 0; i < finalFeeds.Length && i < screens.Length; i++)
        {
            Renderer screen = screens[i];
            MeshFilter filter = screen != null ? screen.GetComponent<MeshFilter>() : null;
            Camera feedCamera = cameraControl.GetCamera(i);
            RenderTexture feed = feedCamera != null ? feedCamera.targetTexture : null;
            if (filter == null || filter.sharedMesh == null || feed == null) continue;

            Material material = new Material(shader) { name = $"CCTV Final Feed {i + 1} (Runtime)" };
            material.mainTexture = feed;
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", feed);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
            if (material.HasProperty("_Cull")) material.SetFloat("_Cull", 0f);
            finalFeedMaterials[i] = material;

            Bounds panel = filter.sharedMesh.bounds;
            float side = ViewerSide(screen);
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = $"CCTV {i + 1} Live Feed (Runtime)";
            Collider collider = quad.GetComponent<Collider>();
            if (collider != null) { collider.enabled = false; Destroy(collider); }
            quad.transform.SetParent(screen.transform, false);
            quad.transform.localPosition = panel.center
                + Vector3.up * side * (panel.extents.y + 0.025f);
            quad.transform.localRotation = Quaternion.Euler(side > 0f ? -90f : 90f, 0f, 0f);
            quad.transform.localScale = new Vector3(panel.size.x * 0.98f,
                panel.size.z * 0.98f, 1f);
            Renderer quadRenderer = quad.GetComponent<Renderer>();
            quadRenderer.sharedMaterial = material;
            quadRenderer.shadowCastingMode = ShadowCastingMode.Off;
            quadRenderer.receiveShadows = false;
            finalFeeds[i] = quad;
        }
    }

    private void BuildInterference()
    {
        interferenceTexture = new Texture2D(128, 64, TextureFormat.RGBA32, false)
        {
            name = "Final CCTV Interference (Runtime)",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Repeat
        };
        Color32[] pixels = new Color32[128 * 64];
        for (int y = 0; y < 64; y++)
        {
            byte stripe = (byte)(Random.Range(0, 7) == 0 ? 170 : Random.Range(3, 35));
            for (int x = 0; x < 128; x++)
            {
                byte value = (byte)Mathf.Clamp(stripe + Random.Range(-12, 13), 0, 255);
                pixels[y * 128 + x] = new Color32(value, value, value, 255);
            }
        }
        interferenceTexture.SetPixels32(pixels);
        interferenceTexture.Apply();

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Texture");
        if (shader == null) return;
        interferenceMaterial = new Material(shader) { name = "Final CCTV Interference (Runtime)" };
        interferenceMaterial.mainTexture = interferenceTexture;
        if (interferenceMaterial.HasProperty("_BaseMap"))
            interferenceMaterial.SetTexture("_BaseMap", interferenceTexture);
        if (interferenceMaterial.HasProperty("_Cull"))
            interferenceMaterial.SetFloat("_Cull", 0f);

        for (int i = 0; i < interference.Length && i < screens.Length; i++)
        {
            Renderer screen = screens[i];
            MeshFilter filter = screen != null ? screen.GetComponent<MeshFilter>() : null;
            if (filter == null || filter.sharedMesh == null) continue;
            Bounds panel = filter.sharedMesh.bounds;
            float side = ViewerSide(screen);
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = $"CCTV {i + 1} Interference (Runtime)";
            Collider collider = quad.GetComponent<Collider>();
            if (collider != null) { collider.enabled = false; Destroy(collider); }
            quad.transform.SetParent(screen.transform, false);
            quad.transform.localPosition = panel.center + Vector3.up * side * (panel.extents.y + 0.03f);
            quad.transform.localRotation = Quaternion.Euler(side > 0f ? -90f : 90f, 0f, 0f);
            quad.transform.localScale = new Vector3(panel.size.x * 0.98f,
                panel.size.z * 0.98f, 1f);
            Renderer quadRenderer = quad.GetComponent<Renderer>();
            quadRenderer.sharedMaterial = interferenceMaterial;
            quadRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            quadRenderer.receiveShadows = false;
            quad.SetActive(false);
            interference[i] = quad;
        }
    }

    private float ViewerSide(Renderer screen)
    {
        if (playerViewCamera == null) return 1f;
        float side = Mathf.Sign(screen.transform.InverseTransformPoint(playerViewCamera.transform.position).y);
        return side == 0f ? 1f : side;
    }

    private static void SetVisible(GameObject[] objects, bool visible)
    {
        foreach (GameObject obj in objects)
            if (obj != null && obj.activeSelf != visible) obj.SetActive(visible);
    }

    private void OnDisable()
    {
        if (started) ShiftAudioDirector.StopFinalFlicker();
    }

    private void OnDestroy()
    {
        foreach (GameObject obj in echoes) if (obj != null) Destroy(obj);
        foreach (GameObject obj in finalFeeds) if (obj != null) Destroy(obj);
        foreach (GameObject obj in interference) if (obj != null) Destroy(obj);
        foreach (Material material in finalFeedMaterials)
            if (material != null) Destroy(material);
        if (interferenceMaterial != null) Destroy(interferenceMaterial);
        if (interferenceTexture != null) Destroy(interferenceTexture);
        if (!lightingCaptured) return;
        for (int i = 0; i < sceneLights.Length; i++)
        {
            if (sceneLights[i] == null) continue;
            sceneLights[i].intensity = originalIntensities[i];
            sceneLights[i].enabled = originalLightEnabled[i];
        }
        RenderSettings.ambientMode = originalAmbientMode;
        RenderSettings.ambientLight = originalAmbientColor;
        RenderSettings.ambientIntensity = originalAmbientIntensity;
        RenderSettings.reflectionIntensity = originalReflectionIntensity;
    }
}
