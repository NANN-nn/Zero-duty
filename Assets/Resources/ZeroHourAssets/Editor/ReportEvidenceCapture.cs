using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Exports reproducible report evidence from the saved scene and production UI builders.
/// Uses disposable preview objects, never saves the scene, and preserves language settings.
/// Run in a separate editor process: -batchmode -executeMethod ReportEvidenceCapture.RunBatch.
/// Set EOS_REPORT_CAPTURE_DIR to choose an output directory outside Assets.
/// </summary>
public static class ReportEvidenceCapture
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly List<GameObject> Owned = new List<GameObject>();
    private static string Output;

    private static T Field<T>(object owner, string name)
        => (T)owner.GetType().GetField(name, Private).GetValue(owner);
    private static void Set(object owner, string name, object value)
        => owner.GetType().GetField(name, Private).SetValue(owner, value);
    private static void Call(object owner, string name, params object[] arguments)
        => owner.GetType().GetMethod(name, Private).Invoke(owner, arguments);
    private static GameObject Root(string name)
    {
        var root = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
        Owned.Add(root);
        return root;
    }

    public static void RunBatch()
    {
        int status = 0;
        Output = Environment.GetEnvironmentVariable("EOS_REPORT_CAPTURE_DIR");
        if (string.IsNullOrEmpty(Output)) Output = "Library/ReportEvidence";
        Directory.CreateDirectory(Output);
        StationLocalization.Language language = StationLocalization.Current;
        bool hadLanguage = PlayerPrefs.HasKey("ZeroDuty.Language");
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Capture requires Edit mode.");
            EditorSceneManager.OpenScene("Assets/Resources/Scenes/first.unity", OpenSceneMode.Single);
            StationLocalization.SetLanguage(StationLocalization.Language.English);
            PrepareNormalScene();
            CaptureRooms();
            CaptureMenu();
            CaptureNotes();
            CapturePuzzles();
            CaptureRobots();
            CaptureResults();
            File.WriteAllText(Path.Combine(Output, "capture-manifest.txt"),
                "Production scene: Assets/Resources/Scenes/first.unity\n"
                + "Room images: saved scene with normal incident visibility and runtime lighting.\n"
                + "UI images: production UI builders with representative state, rendered in Edit mode.\n"
                + "Robot image: production desktop placement and showcase camera pose.\n"
                + "No scene or imported asset was saved or rewritten.\n");
            Debug.Log("Report evidence capture completed: " + Output);
        }
        catch (Exception error)
        {
            status = 1;
            File.WriteAllText(Path.Combine(Output, "capture-error.txt"), error.ToString());
            Debug.LogException(error);
        }
        finally
        {
            foreach (GameObject root in Owned)
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
            Owned.Clear();
            StationLocalization.SetLanguage(language);
            if (!hadLanguage) { PlayerPrefs.DeleteKey("ZeroDuty.Language"); PlayerPrefs.Save(); }
            if (Application.isBatchMode) EditorApplication.Exit(status);
        }
    }

    private static void PrepareNormalScene()
    {
        foreach (MonoBehaviour component in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
            FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (component == null) continue;
            var serialized = new SerializedObject(component);
            if (component is RandomAnomalySelector) SetObjects(serialized.FindProperty("choices"), false);
            if (component is ActivateOnSignal) SetObjects(serialized.FindProperty("targets"), false);
            if (component is ScannableAnomaly)
            {
                SetObject(serialized.FindProperty("brokenVisual"), false);
                bool visible = !serialized.FindProperty("hideUntilActivated").boolValue;
                SetObject(serialized.FindProperty("intactVisual"), visible);
                SetObjects(serialized.FindProperty("intactParts"), visible);
            }
        }
        var lighting = UnityEngine.Object.FindAnyObjectByType<StationMoodLighting>();
        if (lighting != null) Call(lighting, "Awake");
    }
    private static void SetObject(SerializedProperty property, bool active)
    { if (property?.objectReferenceValue is GameObject target) target.SetActive(active); }
    private static void SetObjects(SerializedProperty property, bool active)
    {
        if (property == null || !property.isArray) return;
        for (int i = 0; i < property.arraySize; i++) SetObject(property.GetArrayElementAtIndex(i), active);
    }

    private static void CaptureRooms()
    {
        var monitors = UnityEngine.Object.FindAnyObjectByType<MonitorCameraController>();
        for (int i = 0; i < 3; i++) CaptureCamera(monitors.GetCamera(i), "room-" + (i + 1) + ".png");
    }

    private static void CaptureMenu()
    {
        var menu = Root("Report Menu").AddComponent<ShiftStartMenu>();
        Call(menu, "Awake");
        Canvas canvas = Field<Canvas>(menu, "canvas");
        canvas.enabled = true;
        Set(menu, "nextColdFlicker", 100f); Set(menu, "nextWarmFlicker", 100f);
        Call(menu, "UpdateLightFlicker", 1f);
        Call(menu, "ShowPage", Field<GameObject>(menu, "mainPage"));
        CaptureCanvas(canvas, "start-menu.png");
        Call(menu, "ShowPage", Field<GameObject>(menu, "settingsPage"));
        CaptureCanvas(canvas, "settings.png");
        canvas.enabled = false;
    }

    private static void CaptureNotes()
    {
        var activity = Root("Report Notes").AddComponent<OptionalPropActivities>();
        Call(activity, "Awake");
        var prop = Root("ShiftLog_Open1").AddComponent<OptionalPropInteraction>();
        prop.Configure(OptionalPropInteraction.PropKind.Note);
        Set(activity, "currentNote", prop);
        Call(activity, "ConfigureNoteLayout", OptionalPropInteraction.NotePaperStyle.Book);
        Field<GameObject>(activity, "notePanel").SetActive(true);
        Call(activity, "ConfigureHandbookPages");
        Canvas canvas = activity.GetComponentInChildren<Canvas>(true);
        CaptureCanvas(canvas, "handbook.png");
        prop.gameObject.name = "FoldedPersonalNote7";
        prop.EnsureNoteContent();
        Call(activity, "ConfigureNoteLayout", prop.NoteStyle);
        StationLocalization.Set(Field<Text>(activity, "noteTitle"), prop.NoteTitle);
        StationLocalization.Set(Field<Text>(activity, "noteBody"), prop.NoteBody);
        CaptureCanvas(canvas, "story-note.png");
        Field<GameObject>(activity, "notePanel").SetActive(false);
    }

    private static void CapturePuzzles()
    {
        GameObject root = Root("Report Puzzle Canvas");
        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        var gasRoot = new GameObject("Gas panel", typeof(RectTransform), typeof(CanvasGroup));
        gasRoot.transform.SetParent(root.transform, false);
        PuzzlePanelSkin gas = gasRoot.AddComponent<PuzzlePanelSkin>();
        gas.ConfigureGas(gasRoot.GetComponent<CanvasGroup>(), null);
        gas.SetGasNeedlePosition(.5f); gas.SetGasProgress(1); gas.SetGasStability(.8f, 1.5f, true);
        Call(gas, "LateUpdate");
        CaptureCanvas(canvas, "gas-panel.png");
        gasRoot.SetActive(false);
        var powerRoot = new GameObject("Power panel", typeof(RectTransform), typeof(CanvasGroup));
        powerRoot.transform.SetParent(root.transform, false);
        var group = powerRoot.GetComponent<CanvasGroup>();
        PuzzlePanelSkin power = powerRoot.AddComponent<PuzzlePanelSkin>();
        power.ConfigurePower(group, null);
        CircuitRoutingUI routing = powerRoot.AddComponent<CircuitRoutingUI>();
        routing.Configure(group, _ => { }); routing.BeginRound();
        CaptureCanvas(canvas, "power-panel.png");
        root.SetActive(false);
    }

    private static void CaptureRobots()
    {
        var display = Root("Report Collected Robots").AddComponent<RobotDeskCollection>();
        display.RecordCollected(5); display.SetEyes(1.5f);
        if (!display.TryGetShowcasePose(.7f, out Vector3 position, out Quaternion rotation, out float fov))
            throw new InvalidOperationException("Robot reward camera pose unavailable.");
        var monitors = UnityEngine.Object.FindAnyObjectByType<MonitorCameraController>();
        Camera camera = monitors.GetCamera(0);
        Vector3 savedPosition = camera.transform.position;
        Quaternion savedRotation = camera.transform.rotation;
        float savedFov = camera.fieldOfView;
        try
        {
            camera.transform.SetPositionAndRotation(position, rotation); camera.fieldOfView = fov;
            CaptureCamera(camera, "robot-reward.png");
        }
        finally { camera.transform.SetPositionAndRotation(savedPosition, savedRotation); camera.fieldOfView = savedFov; }
    }

    private static void CaptureResults()
    {
        var result = Root("Report Result Screen").AddComponent<ShiftEndingScreen>();
        Call(result, "BuildUI");
        var group = Field<CanvasGroup>(result, "group");
        group.alpha = 1f;
        RawImage art = Field<RawImage>(result, "endingImage");
        art.gameObject.SetActive(true);
        Field<Image>(result, "failureCard").gameObject.SetActive(false);
        Call(result, "PositionCaptions", true);
        string[] paths = { "DataTransmitted", "FacilitySealed", "ShiftFailed-v1" };
        string[] titles = { "DATA TRANSMITTED", "FACILITY SEALED", "SHIFT FAILED" };
        string[] captions = { "The truth got out. So might the anomaly.", "Outside is safe. You're locked in with it.", "You didn't finish your shift." };
        Canvas canvas = result.GetComponentInChildren<Canvas>(true);
        for (int i = 0; i < paths.Length; i++)
        {
            art.texture = Resources.Load<Texture2D>("EndingArt/" + paths[i]);
            StationLocalization.Set(Field<TextMeshProUGUI>(result, "titleText"), titles[i]);
            StationLocalization.Set(Field<TextMeshProUGUI>(result, "descriptionText"), captions[i]);
            CaptureCanvas(canvas, "result-" + (i + 1) + ".png");
        }
    }

    private static void CaptureCanvas(Canvas canvas, string file)
    {
        var cameraRoot = new GameObject("Report UI Camera") { hideFlags = HideFlags.HideAndDontSave };
        Camera camera = cameraRoot.AddComponent<Camera>();
        camera.transform.position = new Vector3(0, 0, -10);
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
        camera.cullingMask = 1 << 31;
        var layers = new Dictionary<GameObject, int>();
        Camera previousCamera = canvas.worldCamera;
        RenderMode previousMode = canvas.renderMode;
        try
        {
            foreach (Transform child in canvas.GetComponentsInChildren<Transform>(true))
            { layers.Add(child.gameObject, child.gameObject.layer); child.gameObject.layer = 31; }
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1f;
            // Assign the target before layout so a batch editor's window size cannot shift the UI.
            using (var frame = new CaptureFrame(camera))
            {
                Canvas.ForceUpdateCanvases();
                // The headless editor window is smaller than the 1920x1080 target.
                // Preview the production art at its reference-canvas size, not at
                // the editor window's additional Screen-based scale.
                foreach (PuzzlePanelSkin skin in canvas.GetComponentsInChildren<PuzzlePanelSkin>())
                    Field<RectTransform>(skin, "panel").localScale = Vector3.one;
                var notes = canvas.GetComponentInParent<OptionalPropActivities>();
                if (notes != null && Field<Text>(notes, "noteRightBody").enabled)
                {
                    Call(notes, "FitHandbookText", Field<Text>(notes, "noteBody"));
                    Call(notes, "FitHandbookText", Field<Text>(notes, "noteRightBody"));
                }
                foreach (TMP_Text label in canvas.GetComponentsInChildren<TMP_Text>()) label.ForceMeshUpdate();
                frame.Save(file);
            }
        }
        finally
        {
            canvas.worldCamera = previousCamera; canvas.renderMode = previousMode;
            foreach (var entry in layers) if (entry.Key != null) entry.Key.layer = entry.Value;
            UnityEngine.Object.DestroyImmediate(cameraRoot);
        }
    }
    private static void CaptureCamera(Camera camera, string file)
    { using (var frame = new CaptureFrame(camera)) frame.Save(file); }

    /// <summary>Restores the camera target and global render target even if PNG export fails.</summary>
    private sealed class CaptureFrame : IDisposable
    {
        private readonly Camera camera;
        private readonly RenderTexture target, savedTarget, savedActive;
        public CaptureFrame(Camera camera)
        {
            this.camera = camera;
            savedTarget = camera.targetTexture; savedActive = RenderTexture.active;
            target = new RenderTexture(1920, 1080, 24);
            camera.targetTexture = target;
        }
        public void Save(string file)
        {
            camera.Render(); RenderTexture.active = target;
            var pixels = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            try
            {
                pixels.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); pixels.Apply();
                File.WriteAllBytes(Path.Combine(Output, file), pixels.EncodeToPNG());
            }
            finally { UnityEngine.Object.DestroyImmediate(pixels); }
        }
        public void Dispose()
        {
            camera.targetTexture = savedTarget; RenderTexture.active = savedActive;
            target.Release(); UnityEngine.Object.DestroyImmediate(target);
        }
    }
}
