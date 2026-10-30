using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Play-mode checks against both real ghost routes. Never saves the scene.</summary>
[InitializeOnLoad]
public static class GhostShadowValidation
{
    private const string Running = "ZeroDuty.ShadowGhostQA.Running";
    private const string Output = "F:/Codex/2026-10-25/黑影接入验证-8b7f/work";
    private static readonly List<string> Report = new List<string>();
    private static int startFrame = -1;
    private static bool signalled;
    private static double deadline;

    static GhostShadowValidation()
    {
        if (SessionState.GetBool(Running, false)) BeginWaiting();
    }

    public static void RunBatch()
    {
        Directory.CreateDirectory(Output);
        try
        {
            GhostShadowAssetSetup.EnsureAssets();
            AssetDatabase.Refresh();
            ValidateImports();
            EditorSceneManager.OpenScene("Assets/Resources/Scenes/first.unity");
            SessionState.SetBool(Running, true);
            BeginWaiting();
            EditorApplication.EnterPlaymode();
        }
        catch (Exception error) { Finish(error); }
    }

    private static void BeginWaiting()
    {
        deadline = EditorApplication.timeSinceStartup + 150;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > deadline)
                throw new Exception("Play-mode ghost validation timed out.");
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            if (startFrame < 0) startFrame = Time.frameCount;
            int elapsed = Time.frameCount - startFrame;
            if (elapsed >= 8 && !signalled)
            {
                ShiftStartMenu menu = Object.FindFirstObjectByType<ShiftStartMenu>();
                if (menu != null) Call(menu, "BeginGameplay");
                GameplaySignals signals = GameplaySignals.Instance;
                Check(signals != null, "Saved scene has no GameplaySignals.");
                signals.Raise("gas_restored");
                signals.Raise("gas_interlude_complete");
                signalled = true;
            }
            if (elapsed < 16 || !signalled) return;
            EditorApplication.update -= Tick;
            ValidateRuntime();
            Finish(null);
        }
        catch (Exception error) { Finish(error); }
    }

    private static void ValidateImports()
    {
        for (int i = 0; i < 3; i++)
        {
            string path = $"Assets/Resources/GhostShadow/Pose_{i:00}.png";
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            Check(texture != null && texture.width == 1024 && texture.height == 1536,
                "Artwork must retain its original portrait resolution: " + path);
            Check(importer != null && importer.alphaIsTransparency
                && importer.npotScale == TextureImporterNPOTScale.None
                && importer.wrapMode == TextureWrapMode.Clamp, "Unsafe transparent texture import: " + path);
        }
        Material material = Resources.Load<Material>("GhostShadow/ShadowGhost");
        Check(material != null && material.shader != null && material.shader.isSupported,
            "The persistent unlit ghost material must load in Resources.");
    }

    private static void ValidateRuntime()
    {
        // Stop deadline/flicker motion only in this transient test session.
        Time.timeScale = 0f;
        LaterAnomalyPressure later = Object.FindFirstObjectByType<LaterAnomalyPressure>();
        if (later != null) later.enabled = false;
        CorridorPressureSequence pressure = Object.FindFirstObjectByType<CorridorPressureSequence>();
        Check(pressure != null, "Pressure ghost route is missing.");
        pressure.enabled = false;

        GhostFlickerTeleport shooting = Object.FindFirstObjectByType<GhostFlickerTeleport>();
        Check(shooting != null && shooting.gameObject.activeInHierarchy,
            "The actual shooting ghost was not activated by its normal stage signal.");
        shooting.StopAllCoroutines();
        shooting.enabled = false;
        RenderedGhostBillboard shootingArt = shooting.GetComponentInChildren<RenderedGhostBillboard>(true);
        Check(shootingArt != null, "Shooting ghost still uses only the old mesh.");
        shootingArt.SpriteRenderer.enabled = true;
        BoxCollider hitbox = shootingArt.GetComponent<BoxCollider>();
        Check(hitbox != null && hitbox.enabled && hitbox.size.y > 2f, "New upright shooting hitbox is missing.");
        foreach (Renderer renderer in shooting.GetComponentsInChildren<Renderer>(true))
            Check(renderer == shootingArt.SpriteRenderer || !renderer.enabled, "An old ghost mesh remains visible.");
        foreach (Collider collider in shooting.GetComponentsInChildren<Collider>(true))
            Check(collider == hitbox || !collider.enabled, "An old sideways collider remains active.");
        AssertArtwork(shootingArt);
        AssertRandomPoses(shootingArt);
        for (int i = 0; i < 8; i++)
        {
            int previous = shootingArt.PoseIndex;
            Call(shooting, "MoveToAnotherPoint");
            Check(shootingArt.PoseIndex != previous, "A teleport did not reroll the shooting ghost pose.");
        }
        Call(shootingArt, "UpdatePose");
        Physics.SyncTransforms();
        PlayerInteractor player = Object.FindFirstObjectByType<PlayerInteractor>();
        Camera playerCamera = player != null ? player.InteractionCamera : Camera.main;
        Check(playerCamera != null, "Player camera is missing.");
        AssertFacing(shootingArt, playerCamera);
        Vector3 direction = playerCamera.transform.position - hitbox.bounds.center;
        direction.y = 0f; direction.Normalize();
        Ray testShot = new Ray(hitbox.bounds.center + direction * 3f, -direction);
        Check(hitbox.Raycast(testShot, out RaycastHit hit, 5f)
            && hit.collider.GetComponentInParent<ShootableTarget>() != null,
            "A shot at the illustration does not reach its ShootableTarget.");
        Report.Add("PASS shooting: real stage activation, new artwork only, upright player-facing hitbox, teleports reroll poses, ray hits resolve to ShootableTarget.");

        // Capture the real room as viewed by its actual monitor camera.
        MonitorCameraController feeds = Object.FindFirstObjectByType<MonitorCameraController>();
        if (feeds != null && feeds.GetCamera(2) != null)
            Capture(feeds.GetCamera(2), "shooting-room-monitor.png", 1280, 720);

        pressure.BeginExternalPressure();
        Call(pressure, "BuildShadowVisual");
        GameObject warningRoot = Get<GameObject>(pressure, "shadowVisual");
        Check(warningRoot != null, "Deadline warning ghost was not built.");
        warningRoot.SetActive(true);
        RenderedGhostBillboard warningArt = warningRoot.GetComponentInChildren<RenderedGhostBillboard>(true);
        BoxCollider warningHitbox = warningArt != null ? warningArt.GetComponent<BoxCollider>() : null;
        Check(warningArt != null && warningHitbox != null && warningHitbox.isTrigger
            && warningRoot.GetComponent<PressureGhostTarget>() != null
            && warningRoot.GetComponent<ShootableTarget>() == null,
            "Deadline ghost needs a non-blocking hit trigger, separate from the task target.");
        AssertArtwork(warningArt);
        Call(warningArt, "UpdatePose");
        AssertFacing(warningArt, playerCamera);
        int oldPose = warningArt.PoseIndex;
        Call(pressure, "ResetGhostApproach");
        Check(warningArt.PoseIndex != oldPose, "A new pressure encounter did not reroll its pose.");
        oldPose = warningArt.PoseIndex;
        warningRoot.SetActive(false);
        warningRoot.SetActive(true);
        Check(warningArt.PoseIndex != oldPose, "A new warning glimpse did not reroll its pose.");
        AssertRandomPoses(warningArt);
        if (feeds != null && feeds.GetCamera(1) != null)
            Capture(feeds.GetCamera(1), "corridor-warning-monitor.png", 1280, 720);
        Report.Add("PASS deadline warning: shares the three illustrations, rerolls at a fresh encounter/reveal, stays upright, has a non-blocking gun-hit trigger.");

        ValidateWarningShot(pressure, warningRoot, warningArt, later, player);

        ValidateOrbitAndRendering();
        ValidateApproach();

        ShootableTarget target = shooting.GetComponent<ShootableTarget>();
        Check(target != null && target.enabled, "Actual target was not enabled after monitor discovery.");
        int hitPoints = Get<int>(target, "hitPoints");
        Check(hitPoints == 3, "Expected the existing three-hit shooting task.");
        for (int i = 0; i < 3; i++)
        {
            target.Hit();
            Check(shooting.gameObject.activeSelf == (i < 2), "Shooting task changed its three-hit completion rule.");
        }
        Check(GameplaySignals.Instance.HasRaised("ghost_destroyed"), "The original completion signal was lost.");
        Report.Add("PASS shooting completion: three hits still hide the ghost and raise ghost_destroyed.");
    }

    private static void ValidateWarningShot(CorridorPressureSequence pressure, GameObject warning,
        RenderedGhostBillboard art, LaterAnomalyPressure later, PlayerInteractor player)
    {
        GunTool gun = Object.FindFirstObjectByType<GunTool>();
        Check(gun != null && player != null && later != null, "Actual gun/player/countdown missing.");
        PickupItem item = gun.GetComponent<PickupItem>();
        Check(player.TryHold(item), "Could not hold the actual gun for the warning-ghost test.");
        Camera camera = player.InteractionCamera;
        Vector3 savedPosition = camera.transform.position;
        Quaternion savedRotation = camera.transform.rotation;
        GameObject ignoredTrigger = new GameObject("Shadow QA Unrelated Trigger");
        try
        {
            // Isolate the real production gun ray from unrelated scene walls.
            warning.transform.position = new Vector3(100f, 1.2f, 100f);
            camera.transform.position = warning.transform.position + Vector3.forward * 3f;
            camera.transform.LookAt(warning.transform.position);
            Call(art, "UpdatePose");
            ignoredTrigger.transform.position = warning.transform.position + Vector3.forward * 1.5f;
            ignoredTrigger.AddComponent<BoxCollider>().isTrigger = true;
            Physics.SyncTransforms();

            float corridorRemaining = Get<float>(pressure, "remaining");
            float stageRemaining = Get<float>(later, "remaining");
            int stage = Get<int>(later, "activeStage");
            Check(stage >= 0 && stageRemaining > 0f, "No active deadline during test.");
            Check(!GameplaySignals.Instance.HasRaised("ghost_destroyed"), "Task was already completed.");
            Call(gun, "Fire");
            Check(!warning.activeSelf && Get<bool>(pressure, "shadowDispelled"),
                "The actual gun did not disperse the warning through an unrelated trigger.");
            Check(!GameplaySignals.Instance.HasRaised("ghost_destroyed")
                && Get<int>(later, "activeStage") == stage && Get<bool>(pressure, "externalPressureActive"),
                "Dispersing a scare incorrectly completed/stopped the anomaly.");
            Check(Get<float>(pressure, "remaining") == corridorRemaining
                && Get<float>(later, "remaining") == stageRemaining,
                "A shot reset or extended one of the failure clocks.");
            Call(pressure, "UpdateShadowPulse", .99f);
            Check(!warning.activeSelf, "A dispersed ghost immediately reappeared in the same anomaly.");
            Time.timeScale = 1f;
            Call(later, "Update");
            Time.timeScale = 0f;
            Check(Get<float>(later, "remaining") <= stageRemaining
                && Get<int>(later, "activeStage") == stage, "The clock stopped or gained time after dispersal.");

            pressure.BeginExternalPressure();
            Check(!Get<bool>(pressure, "shadowDispelled"), "A fresh anomaly kept the previous kill flag.");
            Call(pressure, "UpdateShadowPulse", .99f);
            Check(warning.activeSelf, "A fresh anomaly could not reveal its warning ghost.");
            Report.Add("PASS actual gun hit: warning ghost disperses in one shot, unrelated triggers are skipped, player is not blocked, no ghost_destroyed signal, both clocks unchanged by shot, active deadline continues, fresh encounter can reveal again.");
        }
        finally
        {
            camera.transform.SetPositionAndRotation(savedPosition, savedRotation);
            player.ReleaseHeldItem(true);
            ignoredTrigger.SetActive(false);
            Object.Destroy(ignoredTrigger);
        }
    }

    private static void ValidateOrbitAndRendering()
    {
        GameObject root = new GameObject("Shadow QA Rotated Parent");
        root.layer = 31;
        root.transform.position = new Vector3(100f, 1.2f, 100f);
        root.transform.rotation = Quaternion.Euler(-90f, 0f, 25f);
        RenderedGhostBillboard art = RenderedGhostBillboard.Create(root.transform, 0f);
        Check(art != null, "Isolated illustration could not be created.");
        GameObject viewerRoot = new GameObject("Shadow QA Player Camera");
        Camera viewer = viewerRoot.AddComponent<Camera>(); viewer.enabled = false;
        viewer.transform.position = root.transform.position + Vector3.forward * 4f;
        Set(art, "viewer", viewer);
        GameObject renderRoot = new GameObject("Shadow QA Independent Feed");
        Camera renderCamera = renderRoot.AddComponent<Camera>(); renderCamera.enabled = false;
        renderCamera.cullingMask = 1 << 31;
        renderCamera.clearFlags = CameraClearFlags.SolidColor;
        renderCamera.backgroundColor = new Color(.11f, .13f, .16f);
        renderCamera.fieldOfView = 44f;
        renderCamera.nearClipPlane = .1f;
        renderCamera.farClipPlane = 20f;
        for (int i = 0; i < 8; i++)
        {
            float angle = i * Mathf.PI / 4f;
            viewer.transform.position = root.transform.position
                + new Vector3(Mathf.Sin(angle) * 4f, i % 2 == 0 ? 1f : -.6f, Mathf.Cos(angle) * 4f);
            Call(art, "UpdatePose");
            AssertFacing(art, viewer);
            Check(Vector3.Dot(art.transform.up, Vector3.up) > .9999f,
                "Parent rotation or camera height tipped the sprite sideways.");
            int pose = art.PoseIndex;
            for (int j = 0; j < 5; j++) Call(art, "LateUpdate");
            Check(art.PoseIndex == pose, "Static pose is incorrectly cycling like animation frames.");
        }
        viewer.transform.position = root.transform.position + Vector3.forward * 4f;
        bool[] captured = new bool[3];
        for (int attempt = 0; attempt < 60; attempt++)
        {
            art.RandomizePose();
            if (captured[art.PoseIndex]) continue;
            Call(art, "UpdatePose");
            // A second camera views it from the side/back of the player-facing plane.
            float angle = (art.PoseIndex + 1) * Mathf.PI * .5f;
            renderCamera.transform.position = root.transform.position
                + new Vector3(Mathf.Sin(angle) * 4f, .15f, Mathf.Cos(angle) * 4f);
            renderCamera.transform.LookAt(art.transform.position);
            Capture(renderCamera, $"pose-{art.PoseIndex + 1}-independent-camera.png", 720, 1080);
            AssertFacing(art, viewer);
            captured[art.PoseIndex] = true;
            if (captured[0] && captured[1] && captured[2]) break;
        }
        Check(captured[0] && captured[1] && captured[2], "Not all three poses were rendered.");
        Object.Destroy(root); Object.Destroy(viewerRoot); Object.Destroy(renderRoot);
        Report.Add("PASS facing/rendering: eight orbit directions, high/low player cameras, inherited -90-degree prefab rotation, independent side/back monitor cameras, player-facing restore, stable poses between rerolls.");
    }

    private static void AssertArtwork(RenderedGhostBillboard art)
    {
        Check(art.SpriteRenderer.sprite != null && art.SpriteRenderer.sprite.texture.name.StartsWith("Pose_"),
            "The old Blender frames are still in use.");
        Check(Mathf.Abs(art.SpriteRenderer.sprite.bounds.size.y - 2.56f) < .001f,
            "New resolution changed the ghost's world height.");
    }

    private static void AssertRandomPoses(RenderedGhostBillboard art)
    {
        bool[] seen = new bool[3];
        for (int i = 0; i < 80; i++)
        {
            int previous = art.PoseIndex;
            art.RandomizePose();
            Check(art.PoseIndex >= 0 && art.PoseIndex < 3 && art.PoseIndex != previous,
                "Invalid or consecutively repeated pose index.");
            seen[art.PoseIndex] = true;
        }
        Check(seen[0] && seen[1] && seen[2], "One of the three poses was never selected.");
    }

    private static void AssertFacing(RenderedGhostBillboard art, Camera camera)
    {
        Vector3 toward = camera.transform.position - art.transform.position;
        toward.y = 0f;
        Check(Vector3.Dot(toward.normalized, art.transform.forward) > .999f,
            "Illustration is not facing the player after rendering.");
    }

    private static void ValidateApproach()
    {
        float distance = 12f;
        for (int frame = 0; frame < 1800; frame++)
        {
            float progress = .75f + .25f * (frame + 1) / 1800f;
            distance -= GhostApproachProfile.Step(distance, 12f, progress, 1.1f, 3f, 1f / 60f);
        }
        Check(distance >= 1.099f && distance < 1.2f, "Deadline-based approach was changed.");
        Check(GhostApproachProfile.Step(12f, 12f, 1f, 1.1f, 3f, 0f) == 0f, "Pause no longer freezes approach.");
        Report.Add("PASS approach regression: closes a 12m gap to 1.1m as deadline nears, respects stop radius and paused time.");
    }

    private static void Capture(Camera camera, string name, int width, int height)
    {
        RenderTexture previousTarget = camera.targetTexture;
        RenderTexture previousActive = RenderTexture.active;
        float previousAspect = camera.aspect;
        RenderTexture target = new RenderTexture(width, height, 24);
        Texture2D snapshot = new Texture2D(width, height, TextureFormat.RGB24, false);
        try
        {
            camera.aspect = (float)width / height;
            camera.targetTexture = target; camera.Render();
            RenderTexture.active = target;
            snapshot.ReadPixels(new Rect(0, 0, width, height), 0, 0); snapshot.Apply();
            File.WriteAllBytes(Output + "/" + name, snapshot.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = previousTarget; camera.aspect = previousAspect;
            RenderTexture.active = previousActive;
            Object.Destroy(target); Object.Destroy(snapshot);
        }
    }

    private static object Call(object target, string name, params object[] arguments) => target.GetType()
        .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, arguments);
    private static T Get<T>(object target, string name) => (T)target.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    private static void Set(object target, string name, object value) => target.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    private static void Check(bool ok, string reason) { if (!ok) throw new Exception(reason); }

    private static void Finish(Exception error)
    {
        SessionState.SetBool(Running, false);
        EditorApplication.update -= Tick;
        Directory.CreateDirectory(Output);
        if (error != null) { Report.Add("FAIL: " + error); Debug.LogException(error); }
        else Report.Add("PASS: all checks completed in the saved first scene's Play session; scene was not saved.");
        File.WriteAllLines(Output + "/validation.txt", Report);
        if (Application.isBatchMode) EditorApplication.Exit(error == null ? 0 : 1);
        else EditorApplication.ExitPlaymode();
    }
}
