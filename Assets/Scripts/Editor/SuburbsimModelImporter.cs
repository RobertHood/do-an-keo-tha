using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Adds the converted BlenderKit models (Assets/Models/BlenderKit/&lt;Category&gt;/&lt;model&gt;/) to the item catalog:
/// - web-friendly import settings for each FBX and its textures (Crunch compression, size limit, normal maps),
/// - a price catalog entry per model (category tab, Vietnamese name, source credit; price left at 0 = "Chưa có giá"),
/// - thumbnails and the spawner list (via Refresh Catalog), and Assets/Models/BlenderKit/CREDITS.md.
/// Models are converted beforehand by Tools/Blender/convert_all.py, which writes info.json next to each FBX.
/// Editor: Tools > Suburbsim > Import Models     CLI: -executeMethod SuburbsimModelImporter.ImportFromCommandLine
/// </summary>
public static class SuburbsimModelImporter
{
    private const string ModelsRoot = "Assets/Models/BlenderKit";
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";

#pragma warning disable 0649 // Filled by JsonUtility from info.json
    [Serializable]
    private class ModelInfo
    {
        public string id;
        public string name;
        public string author;
        public string license;
        public string url;
        public string category;
        public string displayName;
        public string description;

        // Phong thủy (optional): type ID ("nha", "cong"...), front direction and, for houses, the main door
        public string loai;
        public float frontYawOffset;
        public bool hasDoor;
        public Vector3 doorOffset;
    }
#pragma warning restore 0649

    [MenuItem("Tools/Suburbsim/Import Models")]
    public static void ImportFromMenu()
    {
        StringBuilder report = new StringBuilder();
        Import(report);
        EditorUtility.DisplayDialog("Suburbsim Models", report.ToString(), "OK");
    }

    public static void ImportFromCommandLine()
    {
        EditorSceneManager.OpenScene(ScenePath);
        StringBuilder report = new StringBuilder();
        Import(report);
        Debug.Log("[SuburbsimModelImporter]\n" + report);
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
    }

    public static void Import(StringBuilder report)
    {
        if (!AssetDatabase.IsValidFolder(ModelsRoot))
        {
            report.AppendLine($"No models found: {ModelsRoot} does not exist.");
            return;
        }

        PriceCatalog catalog = FindCatalog();
        if (catalog == null)
        {
            report.AppendLine("No Price Catalog asset found.");
            return;
        }

        List<(string folder, ModelInfo info)> models = Directory.GetFiles(ModelsRoot, "info.json", SearchOption.AllDirectories)
            .Select(p => (folder: Path.GetDirectoryName(p).Replace('\\', '/'), info: JsonUtility.FromJson<ModelInfo>(File.ReadAllText(p))))
            .Where(m => m.info != null)
            .OrderBy(m => m.folder)
            .ToList();

        int added = 0, updated = 0, skipped = 0;
        Undo.RecordObject(catalog, "Import Models");

        foreach ((string folder, ModelInfo info) in models)
        {
            string modelName = Path.GetFileName(folder);
            string fbxPath = $"{folder}/{modelName}.fbx";
            if (!File.Exists(fbxPath))
            {
                report.AppendLine($"- {modelName}: FBX missing, skipped.");
                skipped++;
                continue;
            }

            if (!Enum.TryParse(info.category, out ModelCategory category)) category = ModelCategory.Uncategorized;
            int maxTexture = category == ModelCategory.House || category == ModelCategory.PathWater ? 1024 : 512;

            ConfigureTextures($"{folder}/Textures", maxTexture);
            ConfigureModel(fbxPath);

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (prefab == null)
            {
                report.AppendLine($"- {modelName}: could not load the FBX, skipped.");
                skipped++;
                continue;
            }

            PriceCatalogEntry entry = catalog.entries.FirstOrDefault(e => e != null && e.prefab == prefab);
            if (entry == null)
            {
                entry = new PriceCatalogEntry { prefab = prefab, listedPrice = 0 };
                catalog.entries.Add(entry);
                added++;
            }
            else
            {
                updated++;
            }

            entry.category = category;
            ApplyFengShui(entry, info);
            if (string.IsNullOrEmpty(entry.displayName)) entry.displayName = string.IsNullOrEmpty(info.displayName) ? info.name : info.displayName;
            if (string.IsNullOrEmpty(entry.description)) entry.description = $"{info.name} · BlenderKit, {FormatLicense(info.license)}" +
                                                                             (string.IsNullOrEmpty(info.author) ? "" : $", tác giả {info.author}");
        }

        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();

        WriteCredits(models);
        report.AppendLine($"- Models: {added} added to the catalog, {updated} already there, {skipped} skipped.");
        report.AppendLine($"- Credits written to {ModelsRoot}/CREDITS.md.");

        SuburbsimCatalogTool.Refresh(report, false);
    }

    /// <summary>
    /// Copies the phong thủy data from info.json. Values already set in the Price Catalog are kept,
    /// so edits made in the Inspector survive re-importing.
    /// </summary>
    private static void ApplyFengShui(PriceCatalogEntry entry, ModelInfo info)
    {
        if (entry.loai == FengShuiType.Auto && !string.IsNullOrEmpty(info.loai))
        {
            foreach (FengShuiType type in Enum.GetValues(typeof(FengShuiType)))
            {
                if (type != FengShuiType.Auto && FengShuiTypes.Id(type) == info.loai) entry.loai = type;
            }
        }

        bool frontUnset = Mathf.Approximately(entry.frontYawOffset, 0f) && !entry.hasDoor;
        if (frontUnset)
        {
            entry.frontYawOffset = info.frontYawOffset;
            entry.hasDoor = info.hasDoor;
            entry.doorOffset = info.doorOffset;
        }
    }

    private static void ConfigureModel(string path)
    {
        if (!(AssetImporter.GetAtPath(path) is ModelImporter importer)) return;

        bool changed = importer.importCameras || importer.importLights || importer.importAnimation ||
                       importer.animationType != ModelImporterAnimationType.None ||
                       importer.meshCompression != ModelImporterMeshCompression.Medium ||
                       importer.isReadable || !Mathf.Approximately(importer.globalScale, 1f) || !importer.useFileScale;
        if (!changed) return;

        importer.importCameras = false;
        importer.importLights = false;
        importer.importAnimation = false;
        importer.animationType = ModelImporterAnimationType.None;
        importer.meshCompression = ModelImporterMeshCompression.Medium;
        importer.isReadable = false;
        importer.globalScale = 1f;
        importer.useFileScale = true;
        importer.addCollider = false; // SpawnInModel adds a box collider when the model is placed
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
        importer.SaveAndReimport();
    }

    private static void ConfigureTextures(string folder, int maxSize)
    {
        if (!Directory.Exists(folder)) return;

        foreach (string file in Directory.GetFiles(folder).Where(f => f.EndsWith(".jpg") || f.EndsWith(".png")))
        {
            string path = file.Replace('\\', '/');
            if (!(AssetImporter.GetAtPath(path) is TextureImporter importer)) continue;

            bool normal = Path.GetFileName(path).IndexOf("normal", StringComparison.OrdinalIgnoreCase) >= 0;
            TextureImporterType type = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            if (importer.textureType == type && importer.maxTextureSize == maxSize && importer.crunchedCompression) continue;

            importer.textureType = type;
            importer.maxTextureSize = maxSize;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.crunchedCompression = true; // Several times smaller in the WebGL build
            importer.compressionQuality = 50;
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();
        }
    }

    private static void WriteCredits(List<(string folder, ModelInfo info)> models)
    {
        StringBuilder md = new StringBuilder();
        md.AppendLine("# 3D model credits");
        md.AppendLine();
        md.AppendLine("Models downloaded from [BlenderKit](https://www.blenderkit.com/) (free assets) and converted for the web with");
        md.AppendLine("`Tools/Blender/convert_all.py`. Royalty-Free models may be used in the app but not resold or redistributed");
        md.AppendLine("as standalone assets; CC0 models have no restrictions.");
        md.AppendLine();
        md.AppendLine("| Catalog name | Original name | Category | Author | License | Source |");
        md.AppendLine("|---|---|---|---|---|---|");
        foreach ((string folder, ModelInfo info) in models)
        {
            md.AppendLine($"| {info.displayName} | {info.name} | {info.category} | {info.author} | {FormatLicense(info.license)} | [link]({info.url}) |");
        }
        File.WriteAllText($"{ModelsRoot}/CREDITS.md", md.ToString());
        AssetDatabase.ImportAsset($"{ModelsRoot}/CREDITS.md");
    }

    private static string FormatLicense(string license)
    {
        switch (license)
        {
            case "cc_zero": return "CC0";
            case "royalty_free": return "Royalty-Free";
            default: return license ?? "";
        }
    }

    private static PriceCatalog FindCatalog()
    {
        ModelEstPrice prices = UnityEngine.Object.FindAnyObjectByType<ModelEstPrice>(FindObjectsInactive.Include);
        if (prices != null && prices.Catalog != null) return prices.Catalog;

        string guid = AssetDatabase.FindAssets("t:PriceCatalog").FirstOrDefault();
        return guid != null ? AssetDatabase.LoadAssetAtPath<PriceCatalog>(AssetDatabase.GUIDToAssetPath(guid)) : null;
    }
}
