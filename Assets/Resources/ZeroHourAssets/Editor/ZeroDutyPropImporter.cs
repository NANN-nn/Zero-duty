using System;
using System.IO;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class ZeroDutyPropImporter
{
    const string Folder = "Assets/Resources/ZeroHourAssets/Props_v1";
    [Serializable] class Manifest { public MaterialEntry[] materials; public string[] models; }
    [Serializable] class MaterialEntry { public string name, label, photo; public float[] color; public float metallic, smoothness, emission; }
    static ZeroDutyPropImporter() { EditorApplication.delayCall += AutoImport; }
    static void AutoImport()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (File.Exists(Folder + "/import_manifest.json") && !File.Exists(Folder + "/import_complete.txt")) Import();
    }
    [MenuItem("Tools/Zero Duty/Import Blender props")]
    public static void Import()
    {
        var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(Folder + "/import_manifest.json"));
        Directory.CreateDirectory(Folder + "/Materials");
        Directory.CreateDirectory(Folder + "/Prefabs");
        AssetDatabase.Refresh();
        foreach (var entry in manifest.materials)
        {
            string path = Folder + "/Materials/" + entry.name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!mat) { mat = new Material(Shader.Find(string.IsNullOrEmpty(entry.photo) ? "Universal Render Pipeline/Lit" : "Universal Render Pipeline/Unlit")); AssetDatabase.CreateAsset(mat, path); }
            mat.SetColor("_BaseColor", new Color(entry.color[0], entry.color[1], entry.color[2], entry.color[3]));
            mat.SetFloat("_Cull", 0);
            if (!string.IsNullOrEmpty(entry.photo)) mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/ZeroHourAssets/CCTVReference/Textures/" + entry.photo));
            else
            {
                mat.SetFloat("_Metallic", entry.metallic);
                mat.SetFloat("_Smoothness", entry.smoothness);
                if (entry.emission > 0) { mat.EnableKeyword("_EMISSION"); mat.SetColor("_EmissionColor", mat.GetColor("_BaseColor") * entry.emission); }
            }
            EditorUtility.SetDirty(mat);
        }
        AssetDatabase.SaveAssets();
        foreach (string name in manifest.models)
        {
            string path = Folder + "/Models/" + name + ".fbx";
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (!importer) throw new InvalidOperationException("Model has not imported: " + path);
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            foreach (var entry in manifest.materials)
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), entry.name), AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Materials/" + entry.name + ".mat"));
            importer.SaveAndReimport();
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var instance = UnityEngine.Object.Instantiate(model);
            instance.name = name;
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            instance.transform.localScale = Vector3.one;
            PrefabUtility.SaveAsPrefabAsset(instance, Folder + "/Prefabs/" + name + ".prefab");
            UnityEngine.Object.DestroyImmediate(instance);
        }
        AssetDatabase.SaveAssets();
        File.WriteAllText(Folder + "/import_complete.txt", "Imported " + manifest.models.Length + " props with URP materials. Fixed normal-state CCTV photos. " + DateTime.Now);
        AssetDatabase.Refresh();
        Debug.Log("Zero Duty: imported " + manifest.models.Length + " Blender props and created prefabs.");
    }
}
