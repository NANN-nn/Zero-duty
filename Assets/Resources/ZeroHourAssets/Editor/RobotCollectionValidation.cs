using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>Read-only scene geometry report for the handbook desk display.</summary>
[InitializeOnLoad]
public static class RobotCollectionValidation
{
    private const string Request = "Temp/RobotCollectionValidation.request";
    static RobotCollectionValidation() => EditorApplication.delayCall += RunPending;
    private static void RunPending()
    {
        if (!File.Exists(Request) || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(Request);
        InspectDesk();
        ValidateDisplay();
    }

    [Serializable] private class Entry
    {
        public string name;
        public Vector3 position, size, rotation, meshCenter, meshSize;
    }
    [Serializable] private class Report
    {
        public string error;
        public Entry handbook;
        public List<Entry> nearby = new List<Entry>();
    }
    [MenuItem("Tools/Zero Duty/Inspect robot collection desk")]
    public static void InspectDesk()
    {
        var report = new Report();
        GameObject book = GameObject.Find("ShiftLog_Open1");
        if (book == null) report.error = "Required handbook not found.";
        else
        {
            Renderer bookRenderer = book.GetComponentInChildren<Renderer>();
            report.handbook = new Entry { name = book.name, position = book.transform.position,
                rotation = book.transform.eulerAngles, size = bookRenderer != null ? bookRenderer.bounds.size : Vector3.zero };
            foreach (Renderer renderer in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (renderer == null || Vector3.Distance(renderer.bounds.ClosestPoint(book.transform.position), book.transform.position) > 1.4f) continue;
                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                report.nearby.Add(new Entry { name = renderer.name, position = renderer.bounds.center,
                    size = renderer.bounds.size, rotation = renderer.transform.eulerAngles,
                    meshCenter = filter != null && filter.sharedMesh != null ? filter.sharedMesh.bounds.center : Vector3.zero,
                    meshSize = filter != null && filter.sharedMesh != null ? filter.sharedMesh.bounds.size : Vector3.zero });
            }
        }
        Directory.CreateDirectory("Temp/RobotCollectionQA");
        File.WriteAllText("Temp/RobotCollectionQA/desk-geometry.json", JsonUtility.ToJson(report, true));
        Debug.Log("[Robot collection] Desk geometry report written to Temp/RobotCollectionQA.");
    }

    [MenuItem("Tools/Zero Duty/Validate robot collection display")]
    public static void ValidateDisplay()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        GameObject root = new GameObject("Robot Collection QA") { hideFlags = HideFlags.HideAndDontSave };
        GameObject cameraRoot = null;
        RenderTexture texture = null;
        Texture2D snapshot = null;
        RenderTexture previous = RenderTexture.active;
        try
        {
            var display = root.AddComponent<RobotDeskCollection>();
            if (!display.InitializeDisplay()) throw new Exception("Could not initialize the real desk layout.");
            display.RecordCollected(1);
            CheckVisible(root, 1);
            display.RecordCollected(3);
            CheckVisible(root, 3);
            display.RecordCollected(5);
            CheckVisible(root, 5);
            if (root.GetComponentsInChildren<OptionalPropInteraction>(true).Length != 0
                || root.GetComponentsInChildren<Collider>(true).Length != 0)
                throw new Exception("Display copies must not have interaction scripts or colliders.");
            GameObject book = GameObject.Find("ShiftLog_Open1");
            Bounds bookBounds = book.GetComponent<Renderer>().bounds;
            float desktopY = float.NaN;
            foreach (Renderer candidate in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                if (candidate.name == "Shift cabinet | top.001") desktopY = candidate.bounds.max.y;
            foreach (Renderer robot in root.GetComponentsInChildren<Renderer>())
            {
                if (robot.bounds.Intersects(bookBounds)) throw new Exception("Robot display overlaps the handbook.");
                if (Mathf.Abs(robot.bounds.min.y - desktopY - .002f) > .002f)
                    throw new Exception("Robot plinth is not resting on the handbook desktop.");
            }
            if (!display.TryGetShowcasePose(0f, out Vector3 position, out Quaternion rotation, out float fov))
                throw new Exception("Reward camera pose is missing.");
            cameraRoot = new GameObject("Robot QA Camera") { hideFlags = HideFlags.HideAndDontSave };
            Camera camera = cameraRoot.AddComponent<Camera>();
            cameraRoot.transform.SetPositionAndRotation(position, rotation);
            camera.fieldOfView = fov;
            camera.aspect = 16f / 9f;
            camera.nearClipPlane = .025f;
            camera.farClipPlane = 50f;
            camera.allowHDR = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.01f, .015f, .02f);
            foreach (Renderer robot in root.GetComponentsInChildren<Renderer>())
            {
                Vector3 p = camera.WorldToViewportPoint(robot.bounds.center);
                if (p.z <= 0 || p.x < .05f || p.x > .95f || p.y < .05f || p.y > .95f)
                    throw new Exception("One of the five robots falls outside the reward shot.");
            }
            texture = new RenderTexture(1600, 900, 24);
            texture.Create();
            camera.targetTexture = texture;
            snapshot = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            Directory.CreateDirectory("Temp/RobotCollectionQA");
            foreach (bool eyesOn in new[] { false, true })
            {
                display.SetEyes(eyesOn ? 4.28f : .08f);
                camera.Render();
                RenderTexture.active = texture;
                snapshot.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
                snapshot.Apply();
                File.WriteAllBytes("Temp/RobotCollectionQA/" + (eyesOn ? "five-robots-eyes.png" : "five-robots.png"), snapshot.EncodeToPNG());
            }
            Vector3 firstPosition = position;
            for (int step = 0; step <= 32; step++)
            {
                display.TryGetShowcasePose(step / 32f, out position, out rotation, out fov);
                cameraRoot.transform.SetPositionAndRotation(position, rotation);
                camera.fieldOfView = fov;
                foreach (Renderer robot in root.GetComponentsInChildren<Renderer>())
                    for (int corner = 0; corner < 8; corner++)
                    {
                        Bounds bounds = robot.bounds;
                        Vector3 world = bounds.center + Vector3.Scale(bounds.extents, new Vector3(
                            (corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
                        Vector3 point = camera.WorldToViewportPoint(world);
                        if (point.z <= 0 || point.x < .025f || point.x > .975f || point.y < .025f || point.y > .975f)
                            throw new Exception("The moving reward camera crops a robot at step " + step);
                    }
                if (step == 0 || step == 16 || step == 32)
                {
                    display.SetEyes(step == 16 ? 4.28f : .08f);
                    camera.Render();
                    RenderTexture.active = texture;
                    snapshot.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
                    snapshot.Apply();
                    File.WriteAllBytes("Temp/RobotCollectionQA/reward-camera-" + step + ".png", snapshot.EncodeToPNG());
                }
            }
            if (Vector3.Distance(firstPosition, position) < .15f)
                throw new Exception("Reward camera should have a visible but restrained push-in.");
            string result = "PASS: 1/3/5 robots, handbook clearance, no repeat interaction; all five models stay fully in frame along 33 dolly samples.";
            File.WriteAllText("Temp/RobotCollectionQA/display-validation.txt", result);
            Debug.Log("[Robot collection] " + result);
        }
        catch (Exception error)
        {
            Directory.CreateDirectory("Temp/RobotCollectionQA");
            File.WriteAllText("Temp/RobotCollectionQA/display-validation.txt", "FAIL: " + error);
            Debug.LogException(error);
        }
        finally
        {
            RenderTexture.active = previous;
            if (cameraRoot != null) UnityEngine.Object.DestroyImmediate(cameraRoot);
            if (texture != null) { texture.Release(); UnityEngine.Object.DestroyImmediate(texture); }
            if (snapshot != null) UnityEngine.Object.DestroyImmediate(snapshot);
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void CheckVisible(GameObject root, int expected)
    {
        if (root.GetComponentsInChildren<MeshRenderer>().Length != expected)
            throw new Exception("Unexpected number of display robots: " + expected);
    }

    [MenuItem("Tools/Zero Duty/Test robot collection reward in Play")]
    public static void TestRewardInPlay()
    {
        if (!Application.isPlaying || !ShiftStartMenu.ShiftStarted)
        {
            Debug.Log("Start a shift in Play first, then use this command to collect the five robots.");
            return;
        }
        PlayerInteractor actor = UnityEngine.Object.FindAnyObjectByType<PlayerInteractor>();
        if (actor == null || !actor.InputEnabled || OptionalPropActivities.Busy
            || MonitorViewManager.ActiveSession != null || ReferencePictureView.ActiveView != null)
        { Debug.LogWarning("Stand up and close any reading or equipment view before testing."); return; }
        var activities = OptionalPropActivities.Instance;
        if (activities.RobotsCollected != 0)
        { Debug.LogWarning("Use a fresh Play session for the full five-robot test."); return; }
        float initialTimeScale = Time.timeScale;
        Camera camera = actor.InteractionCamera;
        var brain = camera != null ? camera.GetComponent<Unity.Cinemachine.CinemachineBrain>() : null;
        bool initialBrain = brain != null && brain.enabled;
        double started = EditorApplication.timeSinceStartup;
        foreach (OptionalPropInteraction prop in UnityEngine.Object.FindObjectsByType<OptionalPropInteraction>(FindObjectsSortMode.None))
            if (prop.Kind == OptionalPropInteraction.PropKind.Robot && prop.CanInteract(actor)) prop.Interact(actor);
        bool observedReward = false, observedFrozenTime = false;
        EditorApplication.CallbackFunction check = null;
        check = () =>
        {
            if (!Application.isPlaying) { EditorApplication.update -= check; return; }
            observedReward |= RobotDeskCollection.ShowcasePlaying;
            observedFrozenTime |= RobotDeskCollection.ShowcasePlaying && Time.timeScale == 0f;
            if (observedReward && !RobotDeskCollection.ShowcasePlaying)
            {
                bool pass = activities.RobotsCollected == 5 && observedFrozenTime && actor.InputEnabled
                    && Mathf.Approximately(Time.timeScale, initialTimeScale)
                    && (brain == null || brain.enabled == initialBrain);
                string result = pass ? "PASS: five pickups trigger one reward shot; anomaly time pauses; camera brain, input and time scale restore."
                    : "FAIL: reward did not restore its captured state.";
                File.WriteAllText("Temp/RobotCollectionQA/play-validation.txt", result);
                Debug.Log("[Robot collection] " + result);
                EditorApplication.update -= check;
            }
            else if (EditorApplication.timeSinceStartup - started > 15)
            {
                File.WriteAllText("Temp/RobotCollectionQA/play-validation.txt", "FAIL: reward did not finish within fifteen seconds.");
                Debug.LogError("[Robot collection] Reward test timed out.");
                EditorApplication.update -= check;
            }
        };
        EditorApplication.update += check;
    }
}
