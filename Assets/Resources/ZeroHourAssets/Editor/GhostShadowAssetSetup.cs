using UnityEditor;
using UnityEngine;

/// <summary>Preserve the generated cutouts' alpha and include their unlit shader in builds.</summary>
public sealed class GhostShadowAssetSetup : AssetPostprocessor
{
    private void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith("Assets/Resources/GhostShadow/Pose_")) return;
        TextureImporter texture = (TextureImporter)assetImporter;
        texture.textureType = TextureImporterType.Default;
        texture.sRGBTexture = true;
        texture.alphaSource = TextureImporterAlphaSource.FromInput;
        texture.alphaIsTransparency = true;
        texture.npotScale = TextureImporterNPOTScale.None;
        texture.mipmapEnabled = true;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        texture.maxTextureSize = 2048;
        texture.textureCompression = TextureImporterCompression.Uncompressed;
    }

    [MenuItem("Tools/Zero Duty/Prepare shadow ghost artwork")]
    public static void EnsureAssets()
    {
        const string materialPath = "Assets/Resources/GhostShadow/ShadowGhost.mat";
        if (AssetDatabase.LoadAssetAtPath<Material>(materialPath) != null) return;
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) throw new System.InvalidOperationException("Sprite unlit shader is missing.");
        Material material = new Material(shader) { name = "ShadowGhost" };
        AssetDatabase.CreateAsset(material, materialPath);
        AssetDatabase.SaveAssets();
    }
}
