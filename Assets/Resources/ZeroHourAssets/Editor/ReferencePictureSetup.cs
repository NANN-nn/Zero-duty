using System.IO;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class ReferencePictureSetup
{
    const string Folder = "Assets/Resources/ZeroHourAssets/Props_v1/Prefabs/";
    static ReferencePictureSetup() { EditorApplication.delayCall += Connect; }
    [MenuItem("Tools/Zero Duty/Connect reference picture viewing")]
    static void Connect()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        for (int i = 1; i <= 3; i++)
        {
            string path = Folder + $"CAM_0{i}_WallReference.prefab";
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!asset || asset.GetComponent<ReferencePictureView>()) continue;
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var filter = root.GetComponent<MeshFilter>();
                var collider = root.GetComponent<BoxCollider>();
                if (!collider) collider = root.AddComponent<BoxCollider>();
                collider.center = filter.sharedMesh.bounds.center;
                collider.size = filter.sharedMesh.bounds.size;
                collider.isTrigger = false;
                root.AddComponent<ReferencePictureView>();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
