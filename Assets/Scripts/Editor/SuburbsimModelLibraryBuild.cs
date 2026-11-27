using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Player builds load their 3D models from the remote model library (ModelLibrary + glTFast), so:
/// - Every scene in the build drops its references to the local models (PriceCatalog assets and SpawnInModel's
///   Model Prefabs list). Nothing else references the FBX models, prefabs and catalog thumbnails, so Unity leaves
///   them out of the build. The scene files themselves are not changed; Play mode still uses the local prefabs.
/// - glTFast creates its materials at runtime from shader graphs, and a build only keeps the shader variants that
///   some material in the build uses. A set of materials covering the glTF material types (opaque, alpha cut-out,
///   transparent, emissive, occlusion, texture transform, transmission, unlit) is kept in a Resources folder so the
///   models render instead of showing magenta. Rebuilt before each build if missing, or from the menu.
/// To build with the local models instead, add SUBURBSIM_LOCAL_MODELS to the Scripting Define Symbols.
/// </summary>
public class SuburbsimModelLibraryBuild : IPreprocessBuildWithReport, IProcessSceneWithReport
{
    private const string VariantsFolder = "Assets/ModelLibrary/Resources/GltfShaderVariants";
    private const string ShaderRoot = "Packages/com.unity.cloud.gltfast/Runtime/Shader/";

    public int callbackOrder => 0;

    private static bool KeepLocalModels
    {
        get
        {
#if SUBURBSIM_LOCAL_MODELS
            return true;
#else
            return false;
#endif
        }
    }

    public void OnPreprocessBuild(BuildReport report)
    {
        if (KeepLocalModels) return;
        if (!AssetDatabase.IsValidFolder(VariantsFolder) || AssetDatabase.FindAssets("t:Material", new[] { VariantsFolder }).Length == 0)
        {
            BuildShaderVariantMaterials();
        }
    }

    public void OnProcessScene(Scene scene, BuildReport report)
    {
        if (report == null || KeepLocalModels) return; // report is null when entering Play mode

        int changedComponents = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour == null) continue;

                SerializedObject so = new SerializedObject(behaviour);
                bool changed = false;

                SerializedProperty property = so.GetIterator();
                while (property.Next(true))
                {
                    if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue is PriceCatalog)
                    {
                        property.objectReferenceValue = null;
                        changed = true;
                    }
                }

                if (behaviour is SpawnInModel)
                {
                    SerializedProperty prefabs = so.FindProperty("modelPrefabs");
                    if (prefabs != null && prefabs.isArray && prefabs.arraySize > 0)
                    {
                        prefabs.ClearArray();
                        changed = true;
                    }
                }

                if (changed)
                {
                    so.ApplyModifiedPropertiesWithoutUndo();
                    changedComponents++;
                }
            }
        }

        if (changedComponents > 0)
        {
            Debug.Log($"[SuburbsimModelLibraryBuild] '{scene.name}': left the local models out of the build ({changedComponents} component(s)). Models load from the remote model library.");
        }
    }

    [MenuItem("Tools/Suburbsim/Model Library/Rebuild Shader Variant Materials")]
    public static void BuildFromMenu()
    {
        int count = BuildShaderVariantMaterials();
        EditorUtility.DisplayDialog("Model Library",
            count > 0
                ? $"{count} materials written to {VariantsFolder}. They keep the glTFast shader variants in the build."
                : "glTFast is not installed (Packages/com.unity.cloud.gltfast). Let Unity finish importing packages and try again.",
            "OK");
    }

    /// <summary>
    /// Writes one material per glTF material type into VariantsFolder. Returns how many were written.
    /// </summary>
    public static int BuildShaderVariantMaterials()
    {
        Shader metallic = AssetDatabase.LoadAssetAtPath<Shader>(ShaderRoot + "glTF-pbrMetallicRoughness.shadergraph");
        Shader unlit = AssetDatabase.LoadAssetAtPath<Shader>(ShaderRoot + "glTF-unlit.shadergraph");
        if (metallic == null)
        {
            Debug.LogError("[SuburbsimModelLibraryBuild] glTFast shaders not found. Is com.unity.cloud.gltfast installed?");
            return 0;
        }

        EnsureFolder(VariantsFolder);
        foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { VariantsFolder }))
        {
            AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(guid));
        }

        int count = 0;
        string[] alphaNames = { "Opaque", "Mask", "Blend" };
        for (int alpha = 0; alpha < 3; alpha++)
        {
            for (int flags = 0; flags < 16; flags++)
            {
                bool emissive = (flags & 1) != 0;
                bool occlusion = (flags & 2) != 0;
                bool textureTransform = (flags & 4) != 0;
                bool transmission = (flags & 8) != 0;
                if (transmission && alpha != 2) continue; // glTFast renders transmission as transparent

                List<string> keywords = new List<string>();
                if (emissive) keywords.Add("_EMISSIVE");
                if (occlusion) keywords.Add("_OCCLUSION");
                if (textureTransform) keywords.Add("_TEXTURE_TRANSFORM");
                if (transmission) keywords.Add("_TRANSMISSION");

                count += Write(metallic, "Metallic " + alphaNames[alpha] + Suffix(keywords), alpha, keywords);
            }
        }

        if (unlit != null)
        {
            for (int alpha = 0; alpha < 3; alpha++)
            {
                count += Write(unlit, "Unlit " + alphaNames[alpha], alpha, new List<string>());
                count += Write(unlit, "Unlit " + alphaNames[alpha] + " _TEXTURE_TRANSFORM", alpha, new List<string> { "_TEXTURE_TRANSFORM" });
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[SuburbsimModelLibraryBuild] Wrote {count} glTFast shader variant materials to {VariantsFolder}.");
        return count;
    }

    private static string Suffix(List<string> keywords) => keywords.Count == 0 ? "" : " " + string.Join(" ", keywords);

    // Same settings as glTFast's UniversalRPMaterialGenerator for each alpha mode
    private static int Write(Shader shader, string name, int alphaMode, List<string> keywords)
    {
        Material material = new Material(shader) { name = name };
        foreach (string keyword in keywords) material.EnableKeyword(keyword);

        if (alphaMode == 1)
        {
            material.SetFloat("_AlphaCutoff", 0.5f);
            material.SetFloat("_AlphaClip", 1f);
            material.EnableKeyword("_ALPHATEST_ON");
            material.SetOverrideTag("RenderType", "TransparentCutout");
            material.SetFloat("_ZTestGBuffer", 3f);
            material.renderQueue = 2450;
        }
        else if (alphaMode == 2)
        {
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.EnableKeyword("_DISABLE_SSR_TRANSPARENT");
            material.EnableKeyword("_ENABLE_FOG_ON_TRANSPARENT");
            material.SetShaderPassEnabled("DepthOnly", false);
            material.SetFloat("_SrcBlend", 5f);
            material.SetFloat("_DstBlend", 10f);
            material.SetFloat("_ZTestGBuffer", 3f);
            material.SetFloat("_AlphaDstBlend", 10f);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_ZWrite", 0f);
            material.renderQueue = 3000;
        }

        material.SetFloat("_Cull", 0f); // Blender exports double-sided materials
        AssetDatabase.CreateAsset(material, VariantsFolder + "/" + name + ".mat");
        return 1;
    }

    private static void EnsureFolder(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}
