using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class RobotoFontGenerator
{
    private const string FontsDir = "Assets/TextMesh Pro/Fonts";
    private const string OutputDir = "Assets/TextMesh Pro/Resources/Fonts & Materials";
    private const string RegularFontPath = FontsDir + "/Roboto-Regular.ttf";
    private const string BoldFontPath = FontsDir + "/Roboto-Bold.ttf";
    private const string RegularAssetPath = OutputDir + "/Roboto SDF.asset";
    private const string BoldAssetPath = OutputDir + "/Roboto SDF Bold.asset";

    [MenuItem("Tools/Fonts/Generate Roboto TMP Font Assets")]
    public static void GenerateFromMenu()
    {
        Generate();
    }

    public static void Generate()
    {
        AssetDatabase.ImportAsset(RegularFontPath, ImportAssetOptions.ForceUpdate);
        AssetDatabase.ImportAsset(BoldFontPath, ImportAssetOptions.ForceUpdate);
        AssetDatabase.Refresh();

        TMP_FontAsset regular = CreateOrUpdate(RegularFontPath, RegularAssetPath, "Roboto SDF");
        TMP_FontAsset bold = CreateOrUpdate(BoldFontPath, BoldAssetPath, "Roboto SDF Bold");

        if (regular != null)
            SetDefaultFont(regular);

        ApplyToScenes(regular, bold);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("RobotoFontGenerator: done. Generated 'Roboto SDF' and 'Roboto SDF Bold' and applied them to the scenes.");
    }

    static TMP_FontAsset CreateOrUpdate(string fontPath, string assetPath, string assetName)
    {
        Font font = AssetDatabase.LoadAssetAtPath<Font>(fontPath);
        if (font == null)
        {
            Debug.LogError("RobotoFontGenerator: font not imported at " + fontPath);
            return null;
        }

        if (File.Exists(assetPath))
            AssetDatabase.DeleteAsset(assetPath);

        TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(font);
        asset.name = assetName;

        AssetDatabase.CreateAsset(asset, assetPath);
        if (asset.atlasTextures != null && asset.atlasTextures.Length > 0)
            AssetDatabase.AddObjectToAsset(asset.atlasTextures[0], asset);
        if (asset.material != null)
            AssetDatabase.AddObjectToAsset(asset.material, asset);
        EditorUtility.SetDirty(asset);

        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(assetPath);
        return asset;
    }

    static void SetDefaultFont(TMP_FontAsset font)
    {
        if (TMP_Settings.instance == null)
        {
            Debug.LogWarning("RobotoFontGenerator: TMP Settings asset not found.");
            return;
        }

        TMP_Settings.defaultFontAsset = font;
        EditorUtility.SetDirty(TMP_Settings.instance);
    }

    static void ApplyToScenes(TMP_FontAsset regular, TMP_FontAsset bold)
    {
        string[] sceneGuids = AssetDatabase.FindAssets("t:Scene");
        foreach (string guid in sceneGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            if (!scene.IsValid())
                continue;

            int textCount = 0;
            int inputCount = 0;

            try
            {
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    foreach (TextMeshProUGUI tmp in root.GetComponentsInChildren<TextMeshProUGUI>(true))
                    {
                        bool isBold = (tmp.fontStyle & FontStyles.Bold) != 0;
                        TMP_FontAsset font = isBold ? bold : regular;
                        if (font != null)
                            tmp.font = font;
                        textCount++;
                    }

                    foreach (TMP_InputField input in root.GetComponentsInChildren<TMP_InputField>(true))
                    {
                        if (regular != null)
                            input.fontAsset = regular;
                        inputCount++;
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError("RobotoFontGenerator: failed while applying fonts in " + scene.name + ": " + e);
            }

            EditorSceneManager.SaveScene(scene);
            Debug.Log("RobotoFontGenerator: applied fonts to " + textCount + " text and " + inputCount + " input field(s) in " + scene.name + ".");
        }
    }
}
