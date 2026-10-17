using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Non-interactive collected robots beside the handbook and a one-time reward shot.</summary>
[DisallowMultipleComponent]
public sealed class RobotDeskCollection : MonoBehaviour
{
    private const string PrefabPath = "ZeroHourAssets/Props_v1/Prefabs/MaintenanceRobot_Mascot";
    public static bool ShowcasePlaying { get; private set; }
    public int VisibleCount { get; private set; }
    private readonly GameObject[] robots = new GameObject[RobotCollectionState.Goal];
    private Mesh displayMesh;
    private Material eyeMaterial;
    private Vector3 displayCenter, facing;
    private bool initialized, presentationQueued, presentationFinished;
    private float readyAt;
    private Camera view;
    private CinemachineBrain brain;
    private PlayerInteractor interactor;
    private playerController player;
    private FirstPersonCameraController look;
    private PickupItem hiddenItem;
    private Vector3 returnPosition;
    private Quaternion returnRotation;
    private float returnFov, returnTimeScale;
    private bool returnPlayer, returnLook, returnInteractor, returnBrain, returnCursorVisible;
    private CursorLockMode returnCursor;
    private bool ownsCamera;
    private Canvas fadeCanvas;
    private Image fadeImage;
    private static readonly Color EyeColor = new Color(.035f, .55f, .78f, 1f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetState() => ShowcasePlaying = false;

    public bool InitializeDisplay()
    {
        if (initialized) return true;
        GameObject book = GameObject.Find("ShiftLog_Open1");
        GameObject prefab = Resources.Load<GameObject>(PrefabPath);
        MeshFilter sourceMesh = prefab != null ? prefab.GetComponent<MeshFilter>() : null;
        MeshRenderer sourceRenderer = prefab != null ? prefab.GetComponent<MeshRenderer>() : null;
        if (book == null || sourceMesh == null || sourceMesh.sharedMesh == null || sourceRenderer == null)
        {
            Debug.LogWarning("Robot display needs the handbook and robot prefab.", this);
            return false;
        }
        Renderer desktop = null;
        foreach (Renderer candidate in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            if (candidate.name == "Shift cabinet | top.001") { desktop = candidate; break; }
        if (desktop == null) { Debug.LogWarning("Handbook desktop was not found.", this); return false; }

        Bounds table = desktop.bounds;
        Renderer bookRenderer = book.GetComponent<Renderer>();
        Bounds bookBounds = bookRenderer != null ? bookRenderer.bounds : new Bounds(book.transform.position, Vector3.one * .3f);
        Bounds meshBounds = sourceMesh.sharedMesh.bounds;
        float scale = .16f / Mathf.Max(.001f, meshBounds.size.y);
        float width = meshBounds.size.x * scale;
        float spacing = Mathf.Max(.14f, width + .035f);
        float halfRow = (spacing * (robots.Length - 1) + width) * .5f;
        float rowX = bookBounds.max.x + .14f + halfRow;
        if (rowX + halfRow > table.max.x - .08f)
            rowX = bookBounds.min.x - .14f - halfRow;
        rowX = Mathf.Clamp(rowX, table.min.x + halfRow + .08f, table.max.x - halfRow - .08f);
        displayCenter = new Vector3(rowX, table.max.y + .002f,
            Mathf.Clamp(bookBounds.center.z, table.min.z + .14f, table.max.z - .14f));
        facing = bookBounds.center.z < table.center.z ? Vector3.back : Vector3.forward;

        // Copy only mesh and materials, never instantiate a collectible's scripts.
        // A distinct mesh name also excludes these from automatic prop discovery.
        displayMesh = Instantiate(sourceMesh.sharedMesh);
        displayMesh.name = "Collected Robot Display Mesh";
        Material[] materials = (Material[])sourceRenderer.sharedMaterials.Clone();
        for (int i = 0; i < materials.Length; i++)
        {
            if (materials[i] == null || !materials[i].name.StartsWith("ZD_Prop_06")) continue;
            if (eyeMaterial == null)
            {
                eyeMaterial = new Material(materials[i]) { name = "Collected Robot Eyes (Runtime)" };
                eyeMaterial.EnableKeyword("_EMISSION");
            }
            materials[i] = eyeMaterial;
        }
        for (int i = 0; i < robots.Length; i++)
        {
            var robot = new GameObject($"Collected Robot {i + 1}", typeof(MeshFilter), typeof(MeshRenderer));
            robot.transform.SetParent(transform, false);
            robot.transform.SetPositionAndRotation(displayCenter + Vector3.right * ((i - 2) * spacing),
                Quaternion.LookRotation(facing, Vector3.up));
            robot.transform.localScale = Vector3.one * scale;
            robot.GetComponent<MeshFilter>().sharedMesh = displayMesh;
            robot.GetComponent<MeshRenderer>().sharedMaterials = materials;
            // Rest the model's plinth on the desktop, regardless of FBX pivot.
            Vector3 offset = new Vector3(-meshBounds.center.x, -meshBounds.min.y, -meshBounds.center.z);
            robot.transform.position += robot.transform.TransformVector(offset);
            robots[i] = robot;
            robot.SetActive(false);
        }
        SetEyes(.08f);
        initialized = true;
        return true;
    }

    public void RecordCollected(int count)
    {
        if (!InitializeDisplay()) return;
        VisibleCount = Mathf.Clamp(count, 0, robots.Length);
        for (int i = 0; i < robots.Length; i++) robots[i].SetActive(i < VisibleCount);
        if (VisibleCount == robots.Length && !presentationFinished && !presentationQueued)
        {
            presentationQueued = true;
            readyAt = Time.unscaledTime + .4f; // Let the last pickup animation finish.
        }
    }

    public bool TryGetShowcasePose(out Vector3 position, out Quaternion rotation)
        => TryGetShowcasePose(0f, out position, out rotation, out _);

    public bool TryGetShowcasePose(float progress, out Vector3 position,
        out Quaternion rotation, out float fieldOfView)
    {
        position = Vector3.zero;
        rotation = Quaternion.identity;
        fieldOfView = 40f;
        if (!InitializeDisplay()) return false;
        // Ease into a small sideways dolly and push-in, then settle for the
        // final fifth of the shot. Never move the player's actual character.
        float amount = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress / .8f));
        Vector3 right = Vector3.Cross(Vector3.up, -facing).normalized;
        Vector3 target = displayCenter + Vector3.up * .085f;
        Vector3 start = displayCenter + facing * 1.04f + right * .12f + Vector3.up * .485f;
        Vector3 finish = displayCenter + facing * .86f - right * .035f + Vector3.up * .42f;
        position = Vector3.Lerp(start, finish, amount);
        rotation = Quaternion.LookRotation(target - position, Vector3.up);
        fieldOfView = Mathf.Lerp(40f, 37.5f, amount);
        return true;
    }

    public void SetEyes(float brightness)
    {
        if (eyeMaterial != null)
            eyeMaterial.SetColor("_EmissionColor", EyeColor * Mathf.Max(0f, brightness));
    }

    private void Update()
    {
        if (!Application.isPlaying || !presentationQueued || ShowcasePlaying || Time.unscaledTime < readyAt
            || Time.timeScale <= 0f || !ShiftStartMenu.ShiftStarted || ShiftAudioDirector.EndingActive
            || MonitorViewManager.ActiveSession != null || ReferencePictureView.ActiveView != null
            || OptionalPropActivities.Busy || SequencePuzzle.IsPanelOpen) return;
        interactor = FindAnyObjectByType<PlayerInteractor>();
        player = interactor != null ? interactor.GetComponentInParent<playerController>() : null;
        look = player != null ? player.GetComponentInChildren<FirstPersonCameraController>() : null;
        view = interactor != null ? interactor.InteractionCamera : null;
        if (view == null || player == null || !interactor.InputEnabled || !player.InputEnabled
            || look != null && !look.InputEnabled) return;
        presentationQueued = false;
        presentationFinished = true;
        StartCoroutine(ShowReward());
    }

    private IEnumerator ShowReward()
    {
        if (!TryGetShowcasePose(0f, out Vector3 shotPosition, out Quaternion shotRotation,
            out float shotFov)) yield break;
        returnPosition = view.transform.position;
        returnRotation = view.transform.rotation;
        returnFov = view.fieldOfView;
        brain = view.GetComponent<CinemachineBrain>();
        returnBrain = brain != null && brain.enabled;
        returnPlayer = player.InputEnabled;
        returnInteractor = interactor.InputEnabled;
        returnLook = look != null && look.InputEnabled;
        returnTimeScale = Time.timeScale;
        returnCursor = Cursor.lockState;
        returnCursorVisible = Cursor.visible;
        hiddenItem = interactor.HeldItem;
        ownsCamera = ShowcasePlaying = true;
        player.InputEnabled = interactor.InputEnabled = false;
        if (look != null) look.InputEnabled = false;
        if (brain != null) brain.enabled = false;
        if (hiddenItem != null) hiddenItem.SetViewHidden(true);
        Cursor.visible = false;
        Time.timeScale = 0f; // Do not charge anomaly time for the reward shot.
        BuildFade();
        try
        {
            // A short cut avoids flying the camera through walls from another room.
            yield return Fade(0f, 1f);
            view.transform.SetPositionAndRotation(shotPosition, shotRotation);
            view.fieldOfView = shotFov;
            yield return Fade(1f, 0f);
            const float shotSeconds = 2.6f;
            for (float elapsed = 0f; elapsed < shotSeconds; elapsed += Time.unscaledDeltaTime)
            {
                if (view == null) yield break;
                TryGetShowcasePose(elapsed / shotSeconds, out shotPosition, out shotRotation, out shotFov);
                view.transform.SetPositionAndRotation(shotPosition, shotRotation);
                view.fieldOfView = shotFov;
                float lightOn = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.45f, .9f, elapsed));
                float lightOff = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1.65f, 2.15f, elapsed));
                SetEyes(.08f + lightOn * lightOff * 4.2f);
                yield return null;
            }
            SetEyes(.08f);
            yield return Fade(0f, 1f);
            ReturnCamera();
            yield return Fade(1f, 0f);
        }
        finally { Restore(); }
    }

    private void BuildFade()
    {
        var root = new GameObject("Robot Reward Transition", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        root.transform.SetParent(transform, false);
        fadeCanvas = root.GetComponent<Canvas>();
        fadeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        fadeCanvas.sortingOrder = 1000;
        var panel = new GameObject("Fade", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(root.transform, false);
        fadeImage = panel.GetComponent<Image>();
        fadeImage.color = Color.clear;
        fadeImage.raycastTarget = false;
        fadeImage.rectTransform.anchorMin = Vector2.zero;
        fadeImage.rectTransform.anchorMax = Vector2.one;
        fadeImage.rectTransform.offsetMin = fadeImage.rectTransform.offsetMax = Vector2.zero;
    }

    private IEnumerator Fade(float from, float to)
    {
        const float seconds = .16f;
        for (float elapsed = 0; elapsed < seconds; elapsed += Time.unscaledDeltaTime)
        {
            fadeImage.color = new Color(0f, 0f, 0f, Mathf.Lerp(from, to, elapsed / seconds));
            yield return null;
        }
        fadeImage.color = new Color(0f, 0f, 0f, to);
    }

    private void ReturnCamera()
    {
        if (view != null) { view.transform.SetPositionAndRotation(returnPosition, returnRotation); view.fieldOfView = returnFov; }
        if (brain != null) brain.enabled = returnBrain;
    }

    private void Restore()
    {
        if (!ownsCamera) return;
        ownsCamera = false;
        ReturnCamera();
        if (hiddenItem != null) hiddenItem.SetViewHidden(false);
        if (!ShiftAudioDirector.EndingActive)
        {
            if (player != null) player.InputEnabled = returnPlayer;
            if (interactor != null) interactor.InputEnabled = returnInteractor;
            if (look != null) look.InputEnabled = returnLook;
            Time.timeScale = returnTimeScale;
            Cursor.lockState = returnCursor;
            Cursor.visible = returnCursorVisible;
        }
        SetEyes(.08f);
        if (fadeCanvas != null) fadeCanvas.enabled = false;
        ShowcasePlaying = false;
    }

    private void OnDisable() { StopAllCoroutines(); Restore(); }
    private void OnDestroy()
    {
        if (Application.isPlaying)
        {
            if (displayMesh != null) Destroy(displayMesh);
            if (eyeMaterial != null) Destroy(eyeMaterial);
        }
        else
        {
            if (displayMesh != null) DestroyImmediate(displayMesh);
            if (eyeMaterial != null) DestroyImmediate(eyeMaterial);
        }
    }
}
