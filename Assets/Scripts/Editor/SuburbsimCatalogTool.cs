using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Keeps the model library (price catalog) ready for the item catalog:
/// - renders a thumbnail for every model that has none (Assets/UI/Thumbnails),
/// - makes sure every catalog model is in SpawnInModel's prefab list and removes missing (deleted) prefabs,
/// - warns about models without a category (they don't show in any tab),
/// - measures each model's real size at scale 1 (PriceCatalogEntry.baseSize, for the phong thủy rules).
/// Editor: Tools > Suburbsim > Refresh Catalog  (also run by Apply UI Theme)
/// </summary>
public static class SuburbsimCatalogTool
{
    private const string ThumbnailsDir = "Assets/UI/Thumbnails";
    private const int ThumbnailWidth = 412;   // 2x the card's thumbnail area (206 x 120)
    private const int ThumbnailHeight = 240;
    private const int ThumbnailCornerRadius = 24;
    private static readonly Color ThumbnailBackground = new Color(0.957f, 0.957f, 0.961f, 1f); // neutral-100

    [MenuItem("Tools/Suburbsim/Refresh Catalog")]
    public static void RefreshFromMenu()
    {
        StringBuilder report = new StringBuilder();
        Refresh(report, false);
        EditorUtility.DisplayDialog("Suburbsim Catalog", report.Length > 0 ? report.ToString() : "Nothing to do.", "OK");
    }

    [MenuItem("Tools/Suburbsim/Re-render All Thumbnails")]
    public static void RerenderFromMenu()
    {
        StringBuilder report = new StringBuilder();
        Refresh(report, true);
        EditorUtility.DisplayDialog("Suburbsim Catalog", report.ToString(), "OK");
    }

    public static void Refresh(StringBuilder report, bool rerenderAll)
    {
        PriceCatalog catalog = FindCatalog();
        if (catalog == null)
        {
            report.AppendLine("- No Price Catalog asset found.");
            return;
        }

        List<PriceCatalogEntry> entries = catalog.entries.Where(e => e != null).ToList();

        int missingPrefab = entries.Count(e => e.prefab == null);
        if (missingPrefab > 0) report.AppendLine($"- {missingPrefab} catalog entr(y/ies) have no prefab (deleted?). Remove them from the Price Catalog.");

        List<string> uncategorized = entries.Where(e => e.prefab != null && e.category == ModelCategory.Uncategorized).Select(e => e.Id).ToList();
        if (uncategorized.Count > 0)
        {
            report.AppendLine($"- No category (not shown in any tab): {string.Join(", ", uncategorized)}. Set 'Category' in the Price Catalog.");
        }

        int rendered = 0;
        Undo.RecordObject(catalog, "Refresh Catalog");
        foreach (PriceCatalogEntry entry in entries.Where(e => e.prefab != null && (rerenderAll || e.icon == null)))
        {
            Sprite thumbnail = RenderThumbnail(entry.prefab);
            if (thumbnail == null) continue;
            entry.icon = thumbnail;
            rendered++;
        }
        if (rendered > 0)
        {
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            report.AppendLine($"- Rendered {rendered} thumbnail(s) into {ThumbnailsDir}.");
        }

        int measured = 0;
        foreach (PriceCatalogEntry entry in entries.Where(e => e.prefab != null))
        {
            Vector3 size = MeasurePrefab(entry.prefab);
            if ((size - entry.baseSize).sqrMagnitude < 0.0001f) continue;
            entry.baseSize = size;
            measured++;
        }
        if (measured > 0)
        {
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            report.AppendLine($"- Measured the real size of {measured} model(s).");
        }

        SyncSpawnerList(catalog, report);
    }

    /// <summary>
    /// Size in metres of the prefab's meshes at scale 1, along the prefab's own axes.
    /// </summary>
    public static Vector3 MeasurePrefab(GameObject prefab)
    {
        Transform root = prefab.transform;
        Matrix4x4 toRoot = root.worldToLocalMatrix;
        bool any = false;
        Bounds bounds = new Bounds();

        foreach (MeshFilter filter in prefab.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null) continue;
            Encapsulate(ref bounds, ref any, filter.sharedMesh.bounds, toRoot * filter.transform.localToWorldMatrix);
        }
        foreach (SkinnedMeshRenderer skinned in prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            Encapsulate(ref bounds, ref any, skinned.localBounds, toRoot * skinned.transform.localToWorldMatrix);
        }

        Vector3 size = any ? bounds.size : Vector3.zero;
        return new Vector3(Mathf.Round(size.x * 1000f) / 1000f, Mathf.Round(size.y * 1000f) / 1000f, Mathf.Round(size.z * 1000f) / 1000f);
    }

    private static void Encapsulate(ref Bounds bounds, ref bool any, Bounds local, Matrix4x4 matrix)
    {
        for (int c = 0; c < 8; c++)
        {
            Vector3 corner = new Vector3(
                (c & 1) == 0 ? local.min.x : local.max.x,
                (c & 2) == 0 ? local.min.y : local.max.y,
                (c & 4) == 0 ? local.min.z : local.max.z);
            Vector3 p = matrix.MultiplyPoint3x4(corner);
            if (!any) { bounds = new Bounds(p, Vector3.zero); any = true; }
            else bounds.Encapsulate(p);
        }
    }

    private static void SyncSpawnerList(PriceCatalog catalog, StringBuilder report)
    {
        SpawnInModel spawner = Object.FindAnyObjectByType<SpawnInModel>(FindObjectsInactive.Include);
        if (spawner == null) return;

        SerializedObject so = new SerializedObject(spawner);
        SerializedProperty list = so.FindProperty("modelPrefabs");
        if (list == null) return;

        int removed = 0;
        for (int i = list.arraySize - 1; i >= 0; i--)
        {
            if (list.GetArrayElementAtIndex(i).objectReferenceValue == null)
            {
                list.DeleteArrayElementAtIndex(i);
                removed++;
            }
        }

        HashSet<Object> present = new HashSet<Object>();
        for (int i = 0; i < list.arraySize; i++) present.Add(list.GetArrayElementAtIndex(i).objectReferenceValue);

        int added = 0;
        foreach (PriceCatalogEntry entry in catalog.entries.Where(e => e != null && e.prefab != null))
        {
            if (present.Contains(entry.prefab)) continue;
            list.arraySize++;
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = entry.prefab;
            present.Add(entry.prefab);
            added++;
        }

        if (removed == 0 && added == 0) return;
        so.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(spawner.gameObject.scene);
        report.AppendLine($"- Spawner prefab list: {added} added from the catalog, {removed} missing removed.");
    }

    /// <summary>
    /// Renders the prefab from a 3/4 view on a light background with rounded corners, saved as a sprite.
    /// </summary>
    public static Sprite RenderThumbnail(GameObject prefab)
    {
        Scene scene = EditorSceneManager.NewPreviewScene();
        RenderTexture target = RenderTexture.GetTemporary(ThumbnailWidth, ThumbnailHeight, 24, RenderTextureFormat.ARGB32);
        try
        {
            GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>()
                .Where(r => !(r is LineRenderer) && !(r is TrailRenderer) && !(r is ParticleSystemRenderer)).ToArray();
            if (renderers.Length == 0) return null;

            Bounds bounds = renderers[0].bounds;
            foreach (Renderer r in renderers) bounds.Encapsulate(r.bounds);

            GameObject cameraObj = new GameObject("Thumbnail Camera");
            SceneManager.MoveGameObjectToScene(cameraObj, scene);
            UnityEngine.Camera cam = cameraObj.AddComponent<UnityEngine.Camera>();
            cam.scene = scene;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = ThumbnailBackground;
            cam.fieldOfView = 25f;
            cam.aspect = (float)ThumbnailWidth / ThumbnailHeight;
            cam.targetTexture = target;

            Vector3 direction = new Vector3(-1f, 0.7f, -1.2f).normalized;
            float radius = bounds.extents.magnitude;
            float verticalFit = radius / Mathf.Sin(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float distance = verticalFit * 1.05f;
            cam.transform.position = bounds.center + direction * distance;
            cam.transform.LookAt(bounds.center);
            cam.nearClipPlane = Mathf.Max(0.01f, distance - radius * 2f);
            cam.farClipPlane = distance + radius * 2f;

            AddLight(scene, new Vector3(50f, -30f, 0f), 1.2f);
            AddLight(scene, new Vector3(20f, 150f, 0f), 0.5f);
            AddLight(scene, new Vector3(-60f, 60f, 0f), 0.35f);

            RenderPipeline.StandardRequest request = new RenderPipeline.StandardRequest { destination = target };
            if (RenderPipeline.SupportsRenderRequest(cam, request)) RenderPipeline.SubmitRenderRequest(cam, request);
            else cam.Render();

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            Texture2D image = new Texture2D(ThumbnailWidth, ThumbnailHeight, TextureFormat.RGBA32, false);
            image.ReadPixels(new Rect(0, 0, ThumbnailWidth, ThumbnailHeight), 0, 0);
            image.Apply();
            RenderTexture.active = previous;

            RoundCorners(image, ThumbnailCornerRadius);
            return SaveSprite(image, prefab.name);
        }
        finally
        {
            RenderTexture.ReleaseTemporary(target);
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    private static void AddLight(Scene scene, Vector3 euler, float intensity)
    {
        GameObject lightObj = new GameObject("Thumbnail Light");
        SceneManager.MoveGameObjectToScene(lightObj, scene);
        Light light = lightObj.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = intensity;
        light.transform.rotation = Quaternion.Euler(euler);
    }

    private static void RoundCorners(Texture2D image, int radius)
    {
        int w = image.width, h = image.height;
        Color32[] pixels = image.GetPixels32();
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float cx = Mathf.Clamp(x + 0.5f, radius, w - radius);
                float cy = Mathf.Clamp(y + 0.5f, radius, h - radius);
                float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                float alpha = Mathf.Clamp01(radius - dist + 0.5f);
                int i = y * w + x;
                pixels[i].a = (byte)(255 * alpha);
            }
        }
        image.SetPixels32(pixels);
        image.Apply();
    }

    private static Sprite SaveSprite(Texture2D image, string name)
    {
        Directory.CreateDirectory(ThumbnailsDir);
        string path = $"{ThumbnailsDir}/{name}.png";
        File.WriteAllBytes(path, image.EncodeToPNG());
        Object.DestroyImmediate(image);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

        if (AssetImporter.GetAtPath(path) is TextureImporter importer)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 200f;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static PriceCatalog FindCatalog()
    {
        ModelEstPrice prices = Object.FindAnyObjectByType<ModelEstPrice>(FindObjectsInactive.Include);
        if (prices != null && prices.Catalog != null) return prices.Catalog;

        string guid = AssetDatabase.FindAssets("t:PriceCatalog").FirstOrDefault();
        return guid != null ? AssetDatabase.LoadAssetAtPath<PriceCatalog>(AssetDatabase.GUIDToAssetPath(guid)) : null;
    }
}
