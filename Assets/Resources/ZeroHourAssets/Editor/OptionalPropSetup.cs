using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class OptionalPropSetup
{
    static OptionalPropSetup() { EditorApplication.delayCall += Connect; }
    [MenuItem("Tools/Zero Duty/Connect optional prop interactions")]
    static void Connect()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        const string folder = "Assets/Resources/ZeroHourAssets/Props_v1/Prefabs";
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { folder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!asset || !OptionalPropInteraction.TryIdentify(asset.name, out var kind, out bool drink)
                || asset.GetComponent<OptionalPropInteraction>()) continue;
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                RuntimeColliderUtility.EnsureBoxCollider(root);
                root.AddComponent<OptionalPropInteraction>().Configure(kind, drink);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
