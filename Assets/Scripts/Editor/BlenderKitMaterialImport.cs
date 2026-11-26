using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Builds URP Lit materials for the converted BlenderKit models (Assets/Models/BlenderKit) when Unity imports them:
/// - color and normal maps from the FBX,
/// - alpha cut-out and double-sided rendering when the color texture has transparency (leaves, grass, fences),
/// - a transparent surface for see-through materials (glass, water).
/// The converter (Tools/Blender/export_for_unity.py) saves color textures as PNG only when they have transparency.
/// </summary>
public class BlenderKitMaterialImport : AssetPostprocessor
{
    private const string ModelsRoot = "Assets/Models/BlenderKit/";

    public override int GetPostprocessOrder() => 100; // After URP's own FBX material setup

    private void OnPreprocessMaterialDescription(MaterialDescription description, Material material, AnimationClip[] clips)
    {
        if (!assetPath.StartsWith(ModelsRoot)) return;

        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        if (lit == null) return;
        material.shader = lit;

        bool linear = PlayerSettings.colorSpace == ColorSpace.Linear;
        Color baseColor = Color.white;
        if (description.TryGetProperty("DiffuseColor", out Vector4 diffuse))
        {
            baseColor = linear ? ((Color)diffuse).gamma : (Color)diffuse;
        }

        float opacity = 1f;
        if (description.TryGetProperty("Opacity", out float opacityValue)) opacity = opacityValue;
        else if (description.TryGetProperty("TransparencyFactor", out float transparency) && transparency > 0f) opacity = 1f - transparency;

        bool cutout = false;
        if (description.TryGetProperty("DiffuseColor", out TexturePropertyDescription colorTexture) && colorTexture.texture != null)
        {
            material.SetTexture("_BaseMap", colorTexture.texture);
            material.SetTexture("_MainTex", colorTexture.texture);
            baseColor = Color.white;
            cutout = AssetDatabase.GetAssetPath(colorTexture.texture).EndsWith(".png");
        }
        baseColor.a = opacity;
        material.SetColor("_BaseColor", baseColor);
        material.SetColor("_Color", baseColor);

        if ((description.TryGetProperty("NormalMap", out TexturePropertyDescription normal) ||
             description.TryGetProperty("Bump", out normal)) && normal.texture != null)
        {
            material.SetTexture("_BumpMap", normal.texture);
            material.SetFloat("_BumpScale", 1f);
            material.EnableKeyword("_NORMALMAP");
        }

        float metallic = 0f;
        if (description.TryGetProperty("ReflectionFactor", out float reflection)) metallic = Mathf.Clamp01(reflection);
        material.SetFloat("_Metallic", metallic);
        material.SetFloat("_Smoothness", 0.25f);

        if (cutout)
        {
            material.SetFloat("_AlphaClip", 1f);
            material.SetFloat("_Cutoff", 0.5f);
            material.EnableKeyword("_ALPHATEST_ON");
            material.SetFloat("_Cull", (float)CullMode.Off); // Leaves are single planes, show both sides
            material.renderQueue = (int)RenderQueue.AlphaTest;
        }
        else if (opacity < 0.99f)
        {
            material.SetFloat("_Surface", 1f); // Transparent
            material.SetFloat("_Blend", 0f);   // Alpha
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)RenderQueue.Transparent;
        }
    }
}
