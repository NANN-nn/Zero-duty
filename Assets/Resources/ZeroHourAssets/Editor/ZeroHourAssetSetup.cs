using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;

// Copy the package to Assets/ZeroHourAssets. This explicit menu command affects
// only generated materials/prefabs in that folder; it never edits source scenes.
public static class ZeroHourAssetSetup
{
    const string Root = "Assets/ZeroHourAssets";
    [Serializable] public class MaterialRow { public string id, source, texture, note; public float[] color, emission; public float metallic, roughness, alpha, emissionPower; public bool glass; }
    [Serializable] public class MaterialList { public MaterialRow[] materials; }
    [Serializable] public class AssetRow { public string file, source, note; public int meshCount, triangles, rootCount; public float[] sizeMeters, localMin; }
    [Serializable] public class AssetList { public AssetRow[] assets; }
    [Serializable] public class Check { public string file; public int renderers, triangles; public float[] dimensions; public bool pass; public string problem; }
    [Serializable] public class Report { public string unityVersion, pipeline; public bool allPassed; public Check[] assets; public string[] limitations; }

    static Color ColorOf(float[] a) { return new Color(a[0], a[1], a[2], a.Length > 3 ? a[3] : 1); }
    static void Set(Material m, string p, float v) { if (m.HasProperty(p)) m.SetFloat(p, v); }
    static void Transparent(Material m, bool urp)
    {
        m.SetOverrideTag("RenderType", "Transparent");
        Set(m, "_SrcBlend", (float)BlendMode.SrcAlpha); Set(m, "_DstBlend", (float)BlendMode.OneMinusSrcAlpha); Set(m, "_ZWrite", 0);
        Set(m, "_Cull", (float)CullMode.Off); m.renderQueue = (int)RenderQueue.Transparent;
        if (urp) { Set(m, "_Surface", 1); Set(m, "_Blend", 0); m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); }
        else { Set(m, "_Mode", 2); m.EnableKeyword("_ALPHABLEND_ON"); m.DisableKeyword("_ALPHATEST_ON"); m.DisableKeyword("_ALPHAPREMULTIPLY_ON"); }
    }
    [MenuItem("Tools/Zero Hour/Build Materials and Prefabs")]
    public static void Build()
    {
        if (!File.Exists(Root + "/materials.json")) throw new InvalidOperationException("Copy the package to Assets/ZeroHourAssets first.");
        var rp = GraphicsSettings.currentRenderPipeline;
        bool urp = rp != null && rp.GetType().Name.Contains("Universal");
        if (rp != null && !urp) throw new NotSupportedException("This setup supports Built-in and URP. HDRP needs separate material conversion.");
        Shader shader = Shader.Find(urp ? "Universal Render Pipeline/Lit" : "Standard");
        if (shader == null) throw new InvalidOperationException("Required shader unavailable.");
        Directory.CreateDirectory(Root + "/Materials"); Directory.CreateDirectory(Root + "/Prefabs"); AssetDatabase.Refresh();
        var records = JsonUtility.FromJson<MaterialList>(File.ReadAllText(Root + "/materials.json"));
        var mats = new Dictionary<string, Material>();
        foreach (var row in records.materials)
        {
            string path = Root + "/Materials/" + row.id + ".mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
            m.shader = shader; m.name = row.id;
            Color baseColor = ColorOf(row.color); baseColor.a = row.alpha;
            m.SetColor(urp ? "_BaseColor" : "_Color", baseColor);
            Set(m, "_Metallic", row.metallic); Set(m, "_Smoothness", 1 - row.roughness); Set(m, "_Glossiness", 1 - row.roughness);
            if (!string.IsNullOrEmpty(row.texture))
            {
                var t = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/" + row.texture);
                m.SetTexture(urp ? "_BaseMap" : "_MainTex", t);
                if (row.emissionPower > 0) m.SetTexture("_EmissionMap", t);
            }
            if (row.emissionPower > 0) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", ColorOf(row.emission) * row.emissionPower); }
            if (row.alpha < .99f) Transparent(m, urp);
            mats.Add(row.id, m); EditorUtility.SetDirty(m);
        }
        var list = JsonUtility.FromJson<AssetList>(File.ReadAllText(Root + "/manifest.json"));
        var checks = new List<Check>();
        foreach (var row in list.assets)
        {
            string path = Root + "/Models/" + row.file;
            var imported = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!imported) throw new InvalidOperationException("FBX failed to import: " + row.file);
            GameObject instance = UnityEngine.Object.Instantiate(imported); instance.name = Path.GetFileNameWithoutExtension(row.file);
            try
            {
                if (PrefabUtility.IsPartOfPrefabInstance(instance)) PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                var renderers = instance.GetComponentsInChildren<MeshRenderer>(true);
                int faces = 0; int missing = 0;
                foreach (var rend in renderers)
                {
                    rend.sharedMaterials = rend.sharedMaterials.Select(m => {
                        string key = m ? m.name.Split('.')[0] : "";
                        Material target; if (mats.TryGetValue(key, out target)) return target;
                        missing++; return m;
                    }).ToArray();
                    var filter = rend.GetComponent<MeshFilter>(); if (filter && filter.sharedMesh) faces += filter.sharedMesh.triangles.Length / 3;
                }
                Bounds bounds = renderers.Length > 0 ? renderers[0].bounds : new Bounds();
                foreach (var rend in renderers.Skip(1)) bounds.Encapsulate(rend.bounds);
                Vector3 expect = new Vector3(row.sizeMeters[0], row.sizeMeters[2], row.sizeMeters[1]);
                bool dimensions = (bounds.size - expect).magnitude < Mathf.Max(.002f, expect.magnitude * .002f);
                // Verify that moving the root translates every renderer together.
                var positions = renderers.Select(x => x.transform.position).ToArray(); Vector3 delta = new Vector3(1.7f, .3f, -.8f); instance.transform.position += delta;
                bool moves = renderers.Select((x, i) => Vector3.Distance(x.transform.position, positions[i] + delta) < .001f).All(x => x);
                instance.transform.position -= delta;
                bool pass = dimensions && moves && missing == 0 && renderers.Length == row.meshCount && faces == row.triangles;
                checks.Add(new Check { file = row.file, renderers = renderers.Length, triangles = faces, dimensions = new float[] { bounds.size.x, bounds.size.y, bounds.size.z }, pass = pass,
                    problem = pass ? "" : "dimensions=" + dimensions + ", groupMove=" + moves + ", missingMaterials=" + missing + ", expectedMeshes=" + row.meshCount + ", expectedTriangles=" + row.triangles });
                bool saved; PrefabUtility.SaveAsPrefabAsset(instance, Root + "/Prefabs/" + instance.name + ".prefab", out saved);
                if (!saved) throw new InvalidOperationException("Prefab save failed: " + row.file);
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
        }
        AssetDatabase.SaveAssets();
        var report = new Report { unityVersion = Application.unityVersion, pipeline = urp ? "URP" : "Built-in", allPassed = checks.All(c => c.pass), assets = checks.ToArray(),
            limitations = new [] { "Blender procedural detail is approximated, not fully texture-baked.", "Glass uses alpha blending rather than Cycles refraction.", "Ghost is a static unrigged mesh; smoke/dissolve/gameplay are not implemented.", "Station FBX does not carry Blender lighting drivers; lighting is rebuilt separately in Unity." } };
        File.WriteAllText(Root + "/UnityValidation.json", JsonUtility.ToJson(report, true));
        Debug.Log("ZERO_HOUR_VALIDATED " + checks.Count + " assets; allPassed=" + report.allPassed);
        if (!report.allPassed) throw new InvalidOperationException("Asset validation failures; inspect UnityValidation.json.");
    }
    public static void BuildBatch() { Build(); }
}
