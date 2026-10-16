using UnityEngine;

public static class RuntimeColliderUtility
{
    public static Collider EnsureBoxCollider(GameObject root)
    {
        if (root == null) return null;
        Collider existing = root.GetComponentInChildren<Collider>(true);
        if (existing != null) return existing;

        MeshFilter best = null;
        float bestVolume = -1f;
        foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null) continue;
            Vector3 size = filter.sharedMesh.bounds.size;
            float volume = Mathf.Max(0.000001f, size.x * size.y * size.z);
            if (volume <= bestVolume) continue;
            bestVolume = volume;
            best = filter;
        }

        if (best == null) return null;
        BoxCollider box = best.gameObject.AddComponent<BoxCollider>();
        box.center = best.sharedMesh.bounds.center;
        Vector3 colliderSize = best.sharedMesh.bounds.size;
        colliderSize.x = Mathf.Max(colliderSize.x, 0.02f);
        colliderSize.y = Mathf.Max(colliderSize.y, 0.02f);
        colliderSize.z = Mathf.Max(colliderSize.z, 0.02f);
        box.size = colliderSize;
        return box;
    }
}
