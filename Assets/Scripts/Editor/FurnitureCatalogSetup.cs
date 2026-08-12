using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class FurnitureCatalogSetup
{
    private static readonly string[] PrefabSearchPaths =
    {
        "Assets/Prefabs",
        "Assets/Assets/Darth_Artisan/Free_Trees/Prefabs"
    };

    private static readonly Color[] Palette =
    {
        new Color(0.35f, 0.60f, 0.90f, 1f),
        new Color(0.90f, 0.45f, 0.35f, 1f),
        new Color(0.45f, 0.75f, 0.40f, 1f),
        new Color(0.85f, 0.70f, 0.30f, 1f),
        new Color(0.65f, 0.50f, 0.85f, 1f),
        new Color(0.40f, 0.75f, 0.75f, 1f)
    };

    [MenuItem("Tools/Furniture Catalog/Setup Catalog UI")]
    public static void SetupCatalog()
    {
        Canvas canvas = Object.FindAnyObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("FurnitureCatalogSetup: No Canvas found in the scene. Create a Canvas with a Scroll View first.");
            return;
        }

        FurnitureCatalog catalog = Object.FindAnyObjectByType<FurnitureCatalog>();
        if (catalog == null)
        {
            Transform content = FindContent(canvas.transform);
            if (content == null)
            {
                Debug.LogError("FurnitureCatalogSetup: No 'Content' object with a GridLayoutGroup found under the Canvas. Create a Scroll View > Viewport > Content first.");
                return;
            }

            catalog = Undo.AddComponent<FurnitureCatalog>(content.gameObject);
        }

        if (catalog.buttonTemplate == null)
            catalog.buttonTemplate = catalog.GetComponentInChildren<Button>();

        if (catalog.buttonTemplate == null)
        {
            Debug.LogError("FurnitureCatalogSetup: No Button found under the catalog content. Add a button to use as the template.");
            return;
        }

        if (catalog.mainCamera == null)
            catalog.mainCamera = Camera.main;

        EnsureRoomManager();
        EnsureTotalTracker(canvas.transform);
        EnsureCartPanel(canvas);

        if (catalog.items.Count == 0)
            PopulateItemsFromPrefabs(catalog);

        EditorUtility.SetDirty(catalog);
        EditorSceneManager.MarkSceneDirty(catalog.gameObject.scene);
        Debug.Log("FurnitureCatalog setup complete. Assign sprite icons to the items if you want thumbnails.");
    }

    static Transform FindContent(Transform root)
    {
        ScrollRect scrollRect = root.GetComponentInChildren<ScrollRect>(true);
        if (scrollRect != null && scrollRect.content != null)
            return scrollRect.content;

        Transform content = root.Find("Scroll View/Viewport/Content");
        if (content != null)
            return content;

        return null;
    }

    static void EnsureCartPanel(Canvas canvas)
    {
        FurnitureCartPanel panel = Object.FindAnyObjectByType<FurnitureCartPanel>();
        if (panel == null)
        {
            panel = Undo.AddComponent<FurnitureCartPanel>(canvas.gameObject);
            panel.BuildUI();
        }

        panel.Refresh();
        EditorUtility.SetDirty(panel);
    }

    static void EnsureRoomManager()
    {
        if (RoomManager.Instance != null)
            return;

        GameObject roomManagerGo = GameObject.Find("RoomManager");
        if (roomManagerGo == null)
            roomManagerGo = new GameObject("RoomManager");

        Undo.RegisterCreatedObjectUndo(roomManagerGo, "Create RoomManager");
        Undo.AddComponent<RoomManager>(roomManagerGo);
    }

    static void EnsureTotalTracker(Transform canvas)
    {
        FurnitureTotalTracker tracker = Object.FindAnyObjectByType<FurnitureTotalTracker>();
        if (tracker == null)
        {
            GameObject trackerGo = new GameObject("FurnitureTotalTracker");
            Undo.RegisterCreatedObjectUndo(trackerGo, "Create FurnitureTotalTracker");
            tracker = Undo.AddComponent<FurnitureTotalTracker>(trackerGo);
        }

        if (tracker.totalText == null)
        {
            TextMeshProUGUI label = FindTotalLabel(canvas);
            if (label == null)
                label = CreateTotalLabel(canvas);

            tracker.totalText = label;
            EditorUtility.SetDirty(tracker);
        }
    }

    static TextMeshProUGUI FindTotalLabel(Transform canvas)
    {
        TextMeshProUGUI label = null;
        foreach (Transform child in canvas)
        {
            label = child.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null && child.name == "Total Panel")
                return label;
        }
        return null;
    }

    static TextMeshProUGUI CreateTotalLabel(Transform canvas)
    {
        GameObject panelGo = new GameObject("Total Panel");
        Undo.RegisterCreatedObjectUndo(panelGo, "Create total panel");
        panelGo.transform.SetParent(canvas, false);

        RectTransform panelRt = panelGo.AddComponent<RectTransform>();
        Image panelImage = panelGo.AddComponent<Image>();
        panelImage.color = new Color(1f, 1f, 1f, 0.9f);

        panelRt.anchorMin = new Vector2(0f, 1f);
        panelRt.anchorMax = new Vector2(0f, 1f);
        panelRt.pivot = new Vector2(0f, 1f);
        panelRt.anchoredPosition = new Vector2(16f, -16f);
        panelRt.sizeDelta = new Vector2(240f, 44f);

        GameObject labelGo = new GameObject("Total Text");
        Undo.RegisterCreatedObjectUndo(labelGo, "Create total text");
        labelGo.transform.SetParent(panelGo.transform, false);

        RectTransform labelRt = labelGo.AddComponent<RectTransform>();
        labelRt.anchorMin = Vector2.zero;
        labelRt.anchorMax = Vector2.one;
        labelRt.offsetMin = Vector2.zero;
        labelRt.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp = labelGo.AddComponent<TextMeshProUGUI>();
        TMP_FontAsset font = TMP_Settings.defaultFontAsset;
        if (font == null)
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        tmp.font = font;
        tmp.fontSize = 26f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(0.15f, 0.15f, 0.15f, 1f);
        tmp.raycastTarget = false;
        tmp.text = "Total: $0.00";

        return tmp;
    }

    static void PopulateItemsFromPrefabs(FurnitureCatalog catalog)
    {
        List<GameObject> prefabs = new List<GameObject>();
        foreach (string path in PrefabSearchPaths)
        {
            if (!Directory.Exists(path))
                continue;

            foreach (string file in Directory.GetFiles(path, "*.prefab", SearchOption.AllDirectories))
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(file);
                if (prefab != null)
                    prefabs.Add(prefab);
            }
        }

        Undo.RecordObject(catalog, "Populate furniture items");

        for (int i = 0; i < prefabs.Count; i++)
        {
            FurnitureItem item = new FurnitureItem();
            item.displayName = prefabs[i].name;
            item.prefab = prefabs[i];
            item.color = Palette[i % Palette.Length];
            item.price = Mathf.Round(Random.Range(5000f, 60000f)) / 100f;
            catalog.items.Add(item);
        }

        if (prefabs.Count == 0)
            Debug.LogWarning("FurnitureCatalogSetup: No prefabs found in the search paths. Add items manually.");
    }
}
