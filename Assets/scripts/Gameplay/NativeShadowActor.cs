using System.Collections.Generic;
using UnityEngine;

/// <summary>A faceless, footless silhouette made from meshes, without image assets.</summary>
public sealed class NativeShadowActor : MonoBehaviour
{
    private Material surface;
    private readonly List<Mesh> meshes = new List<Mesh>();
    private readonly Transform[] upper = new Transform[2], lower = new Transform[2], hands = new Transform[2];
    private Transform head;

    public static NativeShadowActor Create(Transform parent, int layer)
    {
        var go = new GameObject("Cinematic shadow - native mesh");
        go.transform.SetParent(parent, false);
        go.layer = layer;
        var actor = go.AddComponent<NativeShadowActor>();
        actor.surface = new Material(Resources.Load<Shader>("ZeroHourAssets/Shaders/NativeShadow"));
        actor.surface.SetFloat("_FloorHeight", parent.position.y);
        actor.Build(layer);
        actor.Pose(0, 0);
        return actor;
    }

    private void Build(int layer)
    {
        // Asymmetric draped lower body, narrowing into a ragged tail rather than legs.
        float[] heights = { .02f, .25f, .65f, 1.02f, 1.40f, 1.78f, 1.98f, 2.12f };
        float[] widths = { .018f, .09f, .19f, .22f, .19f, .25f, .34f, .14f };
        const int sides = 24;
        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        for (int row = 0; row < heights.Length; row++)
        for (int i = 0; i < sides; i++)
        {
            float angle = i * Mathf.PI * 2 / sides;
            float fold = 1 + .09f * Mathf.Sin(i * 3.1f + row * 1.7f);
            vertices.Add(new Vector3(Mathf.Cos(angle) * widths[row] * fold + .025f * Mathf.Sin(row),
                heights[row] + (row < 2 ? .06f * Mathf.Sin(i * 2.4f) : 0),
                Mathf.Sin(angle) * widths[row] * .62f * fold));
            if (row == 0) continue;
            int a = row * sides + i, b = row * sides + (i + 1) % sides;
            triangles.Add(a - sides); triangles.Add(a); triangles.Add(b);
            triangles.Add(a - sides); triangles.Add(b); triangles.Add(b - sides);
        }
        var mesh = new Mesh { name = "Draped shadow mesh" };
        mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
        meshes.Add(mesh);
        var body = new GameObject("Tapered shroud", typeof(MeshFilter), typeof(MeshRenderer));
        body.layer = layer; body.transform.SetParent(transform, false);
        body.GetComponent<MeshFilter>().sharedMesh = mesh;
        body.GetComponent<Renderer>().sharedMaterial = surface;
        head = Ellipsoid("Faceless head", transform, new Vector3(.015f, 2.27f, .015f), new Vector3(.33f, .49f, .31f), layer);
        for (int i = 0; i < 2; i++)
        {
            upper[i] = Limb("Upper arm " + i, layer);
            lower[i] = Limb("Forearm " + i, layer);
            hands[i] = CreateClaw(transform, surface, layer, i == 0 ? -1 : 1);
        }
    }

    public void Pose(float reach, float time)
    {
        head.localRotation = Quaternion.Euler(4 + reach * 13, 0, -6 + Mathf.Sin(time * 1.7f) * 3);
        for (int i = 0; i < 2; i++)
        {
            float side = i == 0 ? -1 : 1;
            Vector3 shoulder = new Vector3(side * .27f, 1.99f, 0);
            Vector3 elbow = new Vector3(side * (.57f + reach * .03f), 1.53f + reach * .24f, .1f + reach * .32f);
            Vector3 wrist = new Vector3(side * (.51f - reach * .15f), 1.08f + reach * .63f, .19f + reach * .90f);
            PlaceLimb(upper[i], shoulder, elbow, .095f, .07f);
            PlaceLimb(lower[i], elbow, wrist, .073f, .045f);
            hands[i].localPosition = wrist;
            hands[i].localRotation = Quaternion.Euler(Mathf.Lerp(12, -42, reach), side * -10, side * -16);
        }
        surface.SetFloat("_FilmTime", time);
    }

    private Transform Limb(string name, int layer) => Ellipsoid(name, transform, Vector3.zero, Vector3.one, layer);

    private static void PlaceLimb(Transform limb, Vector3 from, Vector3 to, float start, float end)
    {
        limb.localPosition = (from + to) * .5f;
        limb.localRotation = Quaternion.FromToRotation(Vector3.up, to - from);
        limb.localScale = new Vector3(start * 2, Vector3.Distance(from, to) + .06f, end * 2);
    }

    private Transform Ellipsoid(string name, Transform parent, Vector3 position, Vector3 scale, int layer)
        => Ball(name, parent, position, scale, surface, layer);

    private static Transform Ball(string name, Transform parent, Vector3 position, Vector3 scale, Material material, int layer)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = name; go.layer = layer; go.transform.SetParent(parent, false);
        go.transform.localPosition = position; go.transform.localScale = scale;
        Collider collider = go.GetComponent<Collider>(); collider.enabled = false; Destroy(collider);
        go.GetComponent<Renderer>().sharedMaterial = material;
        return go.transform;
    }

    public static Transform CreateClaw(Transform parent, Material material, int layer, float side)
    {
        var root = new GameObject("Long-fingered shadow hand").transform;
        root.SetParent(parent, false); root.gameObject.layer = layer;
        Ball("Palm", root, Vector3.zero, new Vector3(.13f, .20f, .065f), material, layer);
        for (int finger = 0; finger < 5; finger++)
        {
            float x = (finger - 1.5f) * .038f;
            Vector3 a = finger == 4 ? new Vector3(-side * .065f, -.025f, 0) : new Vector3(x, .075f, 0);
            Vector3 b = finger == 4 ? a + new Vector3(-side * .07f, .03f, .1f)
                : a + new Vector3((finger - 1.5f) * .026f, .13f + .02f * Mathf.Sin(finger), .035f);
            Vector3 c = b + new Vector3(finger == 4 ? side * .025f : 0, .065f, .095f);
            Vector3 d = c + new Vector3(finger == 4 ? side * .02f : -(finger - 1.5f) * .01f, -.01f, .08f);
            Vector3[] points = { a, b, c, d };
            for (int joint = 0; joint < 3; joint++)
            {
                Transform bone = Ball("Finger " + finger + " joint " + joint, root, Vector3.zero, Vector3.one, material, layer);
                PlaceLimb(bone, points[joint], points[joint + 1], .014f - joint * .003f, .012f - joint * .002f);
            }
        }
        return root;
    }

    public Transform CreateForegroundClaw(Transform parent, int layer, float side) => CreateClaw(parent, surface, layer, side);

    private void OnDestroy()
    {
        if (surface != null) Destroy(surface);
        foreach (Mesh mesh in meshes) if (mesh != null) Destroy(mesh);
    }
}
