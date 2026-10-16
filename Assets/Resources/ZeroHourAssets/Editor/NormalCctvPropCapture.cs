using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class NormalCctvPropCapture
{
    static string Output => Path.GetFullPath(Path.Combine(Application.dataPath, "../ArtSource/Props_v1/Textures"));
    static NormalCctvPropCapture() { EditorApplication.delayCall += AutoCapture; }
    static void AutoCapture()
    {
        if (File.Exists(Path.Combine(Output, "capture_complete.txt")) || SessionState.GetBool("NormalCctvPropCapture.done", false)) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
        SessionState.SetBool("NormalCctvPropCapture.done", true);
        Capture();
    }
    [MenuItem("Tools/Zero Duty/Export normal CCTV reference photos")]
    public static void Capture()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("Exit Play before exporting normal CCTV photos."); return; }
        Directory.CreateDirectory(Output);
        Scene preview = default;
        RenderTexture previous = RenderTexture.active;
        try
        {
            preview = EditorSceneManager.OpenPreviewScene("Assets/Resources/Scenes/first.unity");
            var behaviours = preview.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>(true)).Where(b => b != null).ToArray();
            foreach (var b in behaviours)
            {
                var so = new SerializedObject(b);
                string type = b.GetType().Name;
                if (type == "RandomAnomalySelector") SetObjects(so.FindProperty("choices"), false);
                if (type == "ActivateOnSignal") SetObjects(so.FindProperty("targets"), false);
                if (type == "ScannableAnomaly")
                {
                    SetObject(so.FindProperty("brokenVisual"), false);
                    bool visible = !so.FindProperty("hideUntilActivated").boolValue;
                    SetObject(so.FindProperty("intactVisual"), visible);
                    SetObjects(so.FindProperty("intactParts"), visible);
                }
                if (type == "PressureTimingPuzzle" || type == "SequencePuzzle") SetObjects(so.FindProperty("faultVisuals"), false);
                if (type == "CabinetDoor")
                {
                    Vector3 rotation = b.transform.localEulerAngles;
                    rotation[so.FindProperty("rotationAxis").enumValueIndex] = so.FindProperty("closedAngle").floatValue;
                    b.transform.localEulerAngles = rotation;
                }
                if (type == "StationMoodLighting") b.GetType().GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(b, null);
            }
            var control = behaviours.OfType<MonitorCameraController>().First();
            string report = "Source: Assets/Resources/Scenes/first.unity (saved scene, normal-state preview)\n";
            for (int i = 0; i < 3; i++)
            {
                Camera camera = control.GetCamera(i);
                if (camera == null) throw new Exception("Missing CCTV camera " + (i + 1));
                camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(preview);
                camera.aspect = 16f / 9f;
                var rt = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32);
                rt.Create();
                try
                {
                    camera.targetTexture = rt;
                    RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = rt });
                    RenderTexture.active = rt;
                    var image = new Texture2D(1600, 900, TextureFormat.RGB24, false);
                    image.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
                    image.Apply();
                    File.WriteAllBytes(Path.Combine(Output, $"CAM_0{i + 1}_Normal.png"), image.EncodeToPNG());
                    UnityEngine.Object.DestroyImmediate(image);
                    report += $"CAM_0{i + 1}: {camera.name}; position={camera.transform.position:F5}; rotation={camera.transform.eulerAngles:F5}; FOV={camera.fieldOfView}; aspect=16:9\n";
                }
                finally { camera.targetTexture = null; RenderTexture.active = previous; rt.Release(); UnityEngine.Object.DestroyImmediate(rt); }
            }
            File.WriteAllText(Path.Combine(Output, "capture_complete.txt"), report);
            Debug.Log("[Props] Normal CCTV reference photos exported to " + Output);
        }
        catch (Exception e) { File.WriteAllText(Path.Combine(Output, "capture_error.txt"), e.ToString()); Debug.LogException(e); }
        finally { RenderTexture.active = previous; if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview); }
    }
    static void SetObject(SerializedProperty p, bool active) { if (p?.objectReferenceValue is GameObject go) go.SetActive(active); }
    static void SetObjects(SerializedProperty p, bool active) { if (p == null || !p.isArray) return; for (int i = 0; i < p.arraySize; i++) SetObject(p.GetArrayElementAtIndex(i), active); }
}
