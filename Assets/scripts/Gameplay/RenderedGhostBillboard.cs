using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// Displays the Blender-rendered ghost frames in the 3D scene without trying to
/// translate its Blender shader into a different Unity shader.
/// </summary>
public sealed class RenderedGhostBillboard : MonoBehaviour
{
    private const int FrameCount = 6;
    private const float PixelsPerUnit = 400f;
    private static Sprite[] frames;

    private Transform follow;
    private Vector3 worldOffset;
    private SpriteRenderer spriteRenderer;
    private Camera viewer;
    private float phase;

    public SpriteRenderer SpriteRenderer => spriteRenderer;

    public static RenderedGhostBillboard Create(Transform parent, float centerHeight,
        float opacity = 0.9f)
    {
        if (parent == null || !LoadFrames()) return null;

        GameObject visual = new GameObject("Rendered Ghost (Blender Frames)");
        visual.transform.SetParent(parent, false);
        RenderedGhostBillboard billboard = visual.AddComponent<RenderedGhostBillboard>();
        billboard.follow = parent;
        billboard.worldOffset = Vector3.up * centerHeight;
        billboard.phase = Random.Range(0f, FrameCount);
        billboard.spriteRenderer = visual.AddComponent<SpriteRenderer>();
        billboard.spriteRenderer.sprite = frames[0];
        billboard.spriteRenderer.color = new Color(1f, 1f, 1f, opacity);
        billboard.spriteRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        billboard.spriteRenderer.receiveShadows = false;
        billboard.UpdatePose();
        return billboard;
    }

    private static bool LoadFrames()
    {
        if (frames != null) return true;
        Sprite[] loaded = new Sprite[FrameCount];
        for (int i = 0; i < FrameCount; i++)
        {
            Texture2D texture = Resources.Load<Texture2D>($"GhostSpirit/Frame_{i:00}");
            if (texture == null)
            {
                Debug.LogWarning($"Rendered ghost frame {i:00} is missing; using the old visual.");
                return false;
            }
            loaded[i] = Sprite.Create(texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f), PixelsPerUnit, 0,
                SpriteMeshType.FullRect);
        }
        frames = loaded;
        return true;
    }

    private void LateUpdate()
    {
        if (follow == null || spriteRenderer == null || frames == null) return;
        int index = Mathf.FloorToInt(Time.time * 7f + phase) % frames.Length;
        spriteRenderer.sprite = frames[index];
        UpdatePose();
    }

    private void UpdatePose()
    {
        if (follow == null) return;
        transform.position = follow.position + worldOffset
            + Vector3.up * (0.025f * Mathf.Sin(Time.time * 2.2f + phase));

        if (viewer == null)
        {
            viewer = Camera.main;
            if (viewer == null)
            {
                CinemachineBrain brain = FindFirstObjectByType<CinemachineBrain>();
                if (brain != null) viewer = brain.GetComponent<Camera>();
            }
        }
        if (viewer != null)
        {
            Vector3 towardViewer = viewer.transform.position - transform.position;
            towardViewer.y = 0f;
            if (towardViewer.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.LookRotation(towardViewer);
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
