using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Fixture integration checks against the saved scene, without saving it.</summary>
public static class OverheadLampValidation
{
    private const string Output = "Library/OverheadLampQA";
    private static void Check(bool ok, string reason) { if (!ok) throw new Exception(reason); }
    public static void RunBatch()
    {
        int status = 0;
        try
        {
            EditorSceneManager.OpenScene("Assets/Resources/Scenes/first.unity");
            Validate();
        }
        catch (Exception error)
        {
            Directory.CreateDirectory(Output);
            File.WriteAllText(Output + "/validation.txt", "FAIL: " + error);
            Debug.LogException(error); status = 1;
        }
        finally { if (Application.isBatchMode) EditorApplication.Exit(status); }
    }

    [MenuItem("Tools/Zero Duty/Validate ceiling lamps")]
    public static void Validate()
    {
        Check(!EditorApplication.isPlayingOrWillChangePlaymode, "Exit Play before validating.");
        Directory.CreateDirectory(Output);
        var glasses = new List<Renderer>();
        var frames = new Dictionary<Renderer, Material>();
        foreach (Renderer renderer in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude))
        {
            if (renderer.name.Contains(" | overhead lamp glass.")) glasses.Add(renderer);
            if (renderer.name.Contains(" | overhead lamp frame.")) frames.Add(renderer, renderer.sharedMaterial);
        }
        Check(glasses.Count == 8, "Expected eight laboratory lamp glass meshes.");
        Light main = GameObject.Find("Directional Light").GetComponent<Light>();
        float savedIntensity = main.intensity;
        bool savedEnabled = main.enabled;
        Material original = glasses[0].sharedMaterial;
        bool originalKeyword = original.IsKeywordEnabled("_EMISSION");
        Color originalEmission = original.GetColor("_EmissionColor");
        var originalMaterials = new Dictionary<Renderer, Material>();
        foreach (Renderer renderer in glasses) originalMaterials.Add(renderer, renderer.sharedMaterial);
        GameObject root = new GameObject("Ceiling Fixture QA") { hideFlags = HideFlags.HideAndDontSave };
        OverheadLampLighting controller = root.AddComponent<OverheadLampLighting>();
        try
        {
            float normal = Mathf.Max(.01f, savedIntensity);
            main.enabled = true; main.intensity = normal;
            var labOne = new List<Renderer>();
            foreach (Renderer renderer in glasses) if (renderer.name.StartsWith("Lab01 |")) labOne.Add(renderer);
            Capture(labOne, "lab01-before.png");
            controller.Initialize(main, normal);
            controller.Initialize(main, normal);
            Check(controller.FixtureCount == 8, "Duplicate or missing fixture initialization.");
            Light[] lights = root.GetComponentsInChildren<Light>(true);
            Check(lights.Length == 8, "A fixture must have exactly one local light.");
            foreach (Renderer renderer in glasses)
            {
                Check(renderer.sharedMaterial != originalMaterials[renderer], "Shared asset was used directly.");
                Check(renderer.sharedMaterial.IsKeywordEnabled("_EMISSION"), "Emission keyword missing.");
                Check(renderer.sharedMaterial.GetColor("_EmissionColor").maxColorComponent > 1f,
                    "Powered lamp glass should visibly glow.");
            }
            foreach (Light light in lights)
                Check(light.enabled && Mathf.Abs(light.intensity - .35f) < .0001f && light.range <= 2.4f,
                    "Local light exceeds the restrained fixture brightness.");
            Capture(labOne, "lab01-powered.png");
            var labTwo = new List<Renderer>();
            foreach (Renderer renderer in glasses) if (renderer.name.StartsWith("Lab02 |")) labTwo.Add(renderer);
            Capture(labTwo, "lab02-powered.png");

            main.intensity = normal * .08f; controller.RefreshPower();
            Check(Mathf.Abs(glasses[0].sharedMaterial.GetColor("_EmissionColor").r - originalEmission.r * .08f) < .0001f,
                "Fixture emission did not follow the arrival / final-event dimming.");
            foreach (Light light in lights) Check(Mathf.Abs(light.intensity - .028f) < .0001f, "Local flicker mismatch.");

            main.enabled = false; controller.RefreshPower();
            foreach (Renderer renderer in glasses)
                Check(renderer.sharedMaterial.GetColor("_EmissionColor").maxColorComponent == 0f,
                    "A lamp stayed emissive during blackout.");
            foreach (Light light in lights) Check(!light.enabled && light.intensity == 0f, "Blackout left local lighting on.");
            Capture(labOne, "lab01-blackout.png");

            main.enabled = true; main.intensity = normal; controller.RefreshPower();
            foreach (Light light in lights) Check(light.enabled && light.intensity > .3f, "Power restoration did not relight a lamp.");
            foreach (var pair in frames) Check(pair.Key.sharedMaterial == pair.Value, "Lamp frame material changed.");
            Check(original.IsKeywordEnabled("_EMISSION") == originalKeyword
                && original.GetColor("_EmissionColor") == originalEmission, "Source material was modified.");
            controller.Release();
            Check(controller.FixtureCount == 0 && root.GetComponentsInChildren<Light>(true).Length == 0,
                "Fixture cleanup leaked lights.");
            foreach (var pair in originalMaterials) Check(pair.Key.sharedMaterial == pair.Value, "Original glass material not restored.");
            string report = "PASS: eight glass-only fixtures, independent emissive materials, restrained local lighting, "
                + "8% flicker, blackout, power restoration, unchanged frames/source assets and complete cleanup.";
            File.WriteAllText(Output + "/validation.txt", report); Debug.Log(report);
        }
        finally
        {
            controller.Release();
            main.enabled = savedEnabled; main.intensity = savedIntensity;
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void Capture(List<Renderer> lamps, string file)
    {
        Bounds bounds = lamps[0].bounds;
        foreach (Renderer lamp in lamps) bounds.Encapsulate(lamp.bounds);
        MonitorCameraController monitors = UnityEngine.Object.FindAnyObjectByType<MonitorCameraController>();
        Camera view = null;
        float distance = float.PositiveInfinity;
        for (int i = 0; monitors != null && i < 3; i++)
        {
            Camera candidate = monitors.GetCamera(i);
            if (candidate == null) continue;
            float next = Vector3.Distance(candidate.transform.position, bounds.center);
            if (next < distance) { view = candidate; distance = next; }
        }
        Check(view != null, "No monitor camera found for fixture previews.");
        Quaternion savedRotation = view.transform.rotation;
        RenderTexture savedTarget = view.targetTexture;
        RenderTexture target = new RenderTexture(1280, 720, 24);
        Texture2D snapshot = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        RenderTexture previous = RenderTexture.active;
        try
        {
            // Render the existing feed, preserving its renderer, volume and
            // clipping setup; only tilt toward the actual ceiling fixtures.
            view.transform.LookAt(bounds.center + Vector3.down * .3f);
            view.targetTexture = target; view.Render();
            RenderTexture.active = target;
            snapshot.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); snapshot.Apply();
            File.WriteAllBytes(Output + "/" + file, snapshot.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            view.transform.rotation = savedRotation; view.targetTexture = savedTarget;
            UnityEngine.Object.DestroyImmediate(snapshot);
            UnityEngine.Object.DestroyImmediate(target);
        }
    }
}
