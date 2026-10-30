using System.Collections;
using UnityEngine;

/// <summary>Briefly flickers, then moves a ghost between scene-authored positions.</summary>
[DisallowMultipleComponent]
public class GhostFlickerTeleport : MonoBehaviour
{
    [SerializeField] private Transform[] teleportPoints;
    [SerializeField, Min(0.7f)] private float minVisibleSeconds = 2f;
    [SerializeField, Min(0.7f)] private float maxVisibleSeconds = 3f;
    [SerializeField, Range(1, 5)] private int flickersBeforeTeleport = 2;
    [SerializeField, Min(0.01f)] private float flickerOffSeconds = 0.09f;
    [SerializeField, Min(0.01f)] private float flickerOnSeconds = 0.12f;
    [SerializeField, Min(0.01f)] private float teleportHiddenSeconds = 0.18f;

    private Renderer[] ghostRenderers;
    private Collider[] ghostColliders;
    private RenderedGhostBillboard renderedGhost;
    private int lastPointIndex = -1;

    private void Start()
    {
        // Start runs after ShootableTarget.Awake has added a collider if needed.
        ghostRenderers = GetComponentsInChildren<Renderer>(true);
        ghostColliders = GetComponentsInChildren<Collider>(true);
        renderedGhost = RenderedGhostBillboard.Create(transform, 1f);
        if (renderedGhost != null)
        {
            foreach (Renderer oldRenderer in ghostRenderers)
                if (oldRenderer != null) oldRenderer.enabled = false;
            ghostRenderers = new Renderer[] { renderedGhost.SpriteRenderer };
            // The model's collider inherits a -90 degree rotation. Replace it
            // with an upright hit volume following the new illustration instead.
            foreach (Collider oldCollider in ghostColliders)
                if (oldCollider != null) oldCollider.enabled = false;
            BoxCollider hitbox = renderedGhost.gameObject.AddComponent<BoxCollider>();
            hitbox.size = new Vector3(1.3f, 2.2f, 0.45f);
            ghostColliders = new Collider[] { hitbox };
            GetComponent<MonitorDiscoveryTarget>()?.RefreshRenderers();
        }
        StartCoroutine(FlickerAndTeleport());
    }

    public void ReactToHit()
    {
        if (!isActiveAndEnabled) return;
        StopAllCoroutines();
        StartCoroutine(HitReaction());
    }

    private IEnumerator HitReaction()
    {
        SetVisible(false);
        yield return new WaitForSeconds(0.2f);
        MoveToAnotherPoint();
        SetVisible(true);
        yield return FlickerAndTeleport();
    }

    private IEnumerator FlickerAndTeleport()
    {
        while (true)
        {
            SetVisible(true);
            yield return new WaitForSeconds(Random.Range(minVisibleSeconds,
                Mathf.Max(minVisibleSeconds, maxVisibleSeconds)));

            for (int i = 0; i < flickersBeforeTeleport; i++)
            {
                SetVisible(false);
                yield return new WaitForSeconds(flickerOffSeconds);
                SetVisible(true);
                yield return new WaitForSeconds(flickerOnSeconds);
            }

            SetVisible(false);
            yield return new WaitForSeconds(teleportHiddenSeconds);
            MoveToAnotherPoint();
        }
    }

    private void SetVisible(bool visible)
    {
        foreach (Renderer ghostRenderer in ghostRenderers)
            if (ghostRenderer != null) ghostRenderer.enabled = visible;

        // An invisible ghost should not block the camera or receive a shot.
        foreach (Collider ghostCollider in ghostColliders)
            if (ghostCollider != null) ghostCollider.enabled = visible;
    }

    private void MoveToAnotherPoint()
    {
        if (teleportPoints == null || teleportPoints.Length == 0) return;

        int next = -1;
        int start = Random.Range(0, teleportPoints.Length);
        for (int offset = 0; offset < teleportPoints.Length; offset++)
        {
            int candidate = (start + offset) % teleportPoints.Length;
            if (teleportPoints[candidate] != null && candidate != lastPointIndex)
            {
                next = candidate;
                break;
            }
        }

        if (next < 0) return;
        transform.position = teleportPoints[next].position;
        lastPointIndex = next;
        renderedGhost?.RandomizePose();
    }
}
