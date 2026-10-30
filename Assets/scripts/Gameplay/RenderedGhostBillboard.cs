using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Displays a randomly chosen shadow illustration, upright and camera-facing.
/// A pose stays still between reappearances instead of cycling every frame.
/// </summary>
[DefaultExecutionOrder(10000)]
public sealed class RenderedGhostBillboard : MonoBehaviour
{
    private const int PoseCount = 3;
    private const float ArtworkHeight = 2.56f;
    private static Sprite[] poses;

    private Transform follow;
    private Vector3 worldOffset;
    private SpriteRenderer spriteRenderer;
    private Camera viewer;
    private float phase;

    public SpriteRenderer SpriteRenderer => spriteRenderer;
    public int PoseIndex { get; private set; } = -1;

    public static RenderedGhostBillboard Create(Transform parent, float centerHeight,
        float opacity = 0.9f)
    {
        if (parent == null || !LoadPoses()) return null;

        GameObject visual = new GameObject("Shadow Ghost (Random Illustration)");
        visual.layer = parent.gameObject.layer;
        visual.transform.SetParent(parent, false);
        RenderedGhostBillboard billboard = visual.AddComponent<RenderedGhostBillboard>();
        billboard.follow = parent;
        billboard.worldOffset = Vector3.up * centerHeight;
        billboard.phase = Random.Range(0f, Mathf.PI * 2f);
        billboard.spriteRenderer = visual.AddComponent<SpriteRenderer>();
        Material material = Resources.Load<Material>("GhostShadow/ShadowGhost");
        if (material != null) billboard.spriteRenderer.sharedMaterial = material;
        billboard.RandomizePose();
        billboard.spriteRenderer.color = new Color(1f, 1f, 1f, opacity);
        billboard.spriteRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        billboard.spriteRenderer.receiveShadows = false;
        billboard.UpdatePose();
        return billboard;
    }

    private static bool LoadPoses()
    {
        if (poses != null && poses[0] != null) return true;
        Sprite[] loaded = new Sprite[PoseCount];
        for (int i = 0; i < PoseCount; i++)
        {
            Texture2D texture = Resources.Load<Texture2D>($"GhostShadow/Pose_{i:00}");
            if (texture == null)
            {
                foreach (Sprite sprite in loaded)
                    if (sprite != null) Destroy(sprite);
                Debug.LogWarning($"Shadow ghost pose {i:00} is missing; using the old visual.");
                return false;
            }
            loaded[i] = Sprite.Create(texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f), texture.height / ArtworkHeight, 0,
                SpriteMeshType.FullRect);
            loaded[i].name = $"Shadow Ghost Pose {i + 1}";
        }
        poses = loaded;
        return true;
    }

    public void RandomizePose()
    {
        if (spriteRenderer == null || !LoadPoses()) return;
        // Do not repeat the previous pose at consecutive teleports/reappearances.
        int next = PoseIndex < 0 ? Random.Range(0, poses.Length)
            : Random.Range(0, poses.Length - 1);
        if (PoseIndex >= 0 && next >= PoseIndex) next++;
        PoseIndex = next;
        spriteRenderer.sprite = poses[next];
    }

    private void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering += FaceRenderCamera;
        RenderPipelineManager.endCameraRendering += RestorePlayerFacing;
        if (spriteRenderer != null)
        {
            RandomizePose();
            UpdatePose();
        }
    }

    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= FaceRenderCamera;
        RenderPipelineManager.endCameraRendering -= RestorePlayerFacing;
    }

    private void LateUpdate()
    {
        if (follow == null || spriteRenderer == null) return;
        UpdatePose();
    }

    private void FaceRenderCamera(ScriptableRenderContext context, Camera camera)
    {
        // Monitor feeds have their own viewpoints. Face each feed for its draw,
        // then restore the player's orientation so the hitbox stays player-facing.
        if (camera != null && camera.cameraType == CameraType.Game)
            FaceCamera(camera);
    }

    private void RestorePlayerFacing(ScriptableRenderContext context, Camera camera)
    {
        if (viewer != null) FaceCamera(viewer);
    }

    private void FaceCamera(Camera camera)
    {
        if (follow == null || camera == null) return;
        Vector3 towardViewer = camera.transform.position - transform.position;
        towardViewer.y = 0f;
        if (towardViewer.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(towardViewer, Vector3.up);
    }

    private void UpdatePose()
    {
        if (follow == null) return;
        transform.position = follow.position + worldOffset
            + Vector3.up * (0.025f * Mathf.Sin(Time.time * 2.2f + phase));

        if (viewer == null)
        {
            PlayerInteractor player = FindFirstObjectByType<PlayerInteractor>();
            viewer = player != null ? player.InteractionCamera : Camera.main;
            if (viewer == null)
            {
                CinemachineBrain brain = FindFirstObjectByType<CinemachineBrain>();
                if (brain != null) viewer = brain.GetComponent<Camera>();
            }
        }
        if (viewer != null)
        {
            FaceCamera(viewer);
        }
        else
        {
            // The old ghost prefab is rotated -90 degrees. Do not inherit that
            // rotation if the gameplay camera has not been found yet.
            transform.rotation = Quaternion.identity;
        }

        // The existing shootable ghost prefab has its own scale and rotation.
        // Cancel that inherited scale so this rendered silhouette stays human-sized.
        Vector3 scale = follow.lossyScale;
        transform.localScale = new Vector3(
            1f / Mathf.Max(0.001f, Mathf.Abs(scale.x)),
            1f / Mathf.Max(0.001f, Mathf.Abs(scale.y)),
            1f / Mathf.Max(0.001f, Mathf.Abs(scale.z)));
    }
}
