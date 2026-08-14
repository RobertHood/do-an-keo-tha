using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using static UnityEngine.Object;

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
        Canvas canvas = UnityEngine.Object.FindAnyObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("FurnitureCatalogSetup: No Canvas found in the scene. Create a Canvas with a Scroll View first.");
            return;
        }

        FurnitureCatalog catalog = UnityEngine.Object.FindAnyObjectByType<FurnitureCatalog>();
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
        EnsurePlotPanel(canvas);

        PopulateItemsFromPrefabs(catalog);
        EnsureCategoryTabs(catalog);

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
        FurnitureCartPanel panel = UnityEngine.Object.FindAnyObjectByType<FurnitureCartPanel>();
        if (panel == null)
        {
            panel = Undo.AddComponent<FurnitureCartPanel>(canvas.gameObject);
            panel.BuildUI();
        }

        panel.Refresh();
        EditorUtility.SetDirty(panel);
    }

    static void EnsurePlotPanel(Canvas canvas)
    {
        PlotPanel panel = UnityEngine.Object.FindAnyObjectByType<PlotPanel>();
        if (panel == null)
        {
            panel = Undo.AddComponent<PlotPanel>(canvas.gameObject);
            panel.BuildUI();
            panel.SyncDefaults();
        }

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
        FurnitureTotalTracker tracker = UnityEngine.Object.FindAnyObjectByType<FurnitureTotalTracker>();
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
                if (prefab == null || prefab.GetComponent<RectTransform>() != null)
                    continue;

                prefabs.Add(prefab);
            }
        }

        Undo.RecordObject(catalog, "Refresh furniture items");

        int added = 0;
        foreach (GameObject prefab in prefabs)
        {
            if (catalog.items.Exists(item => item.prefab == prefab))
                continue;

            FurnitureItem item = new FurnitureItem();
            item.displayName = prefab.name;
            item.prefab = prefab;
            item.category = ClassifyCategory(prefab.name);
            item.color = Palette[catalog.items.Count % Palette.Length];
            item.price = GetSamplePrice(prefab.name);
            catalog.items.Add(item);
            added++;
        }

        if (added > 0)
            Debug.Log($"FurnitureCatalogSetup: Added {added} new item(s) to the catalog from prefabs.");
        else if (prefabs.Count == 0)
            Debug.LogWarning("FurnitureCatalogSetup: No prefabs found in the search paths. Add items manually.");
    }

    static string ClassifyCategory(string name)
    {
        string n = name.ToLowerInvariant();

        if (ContainsAny(n, "tree", "oak", "fir", "pine", "palm", "poplar", "plant", "flower", "bush", "shrub", "grass", "fern", "ivy", "moss", "cactus", "bamboo"))
            return FurnitureCatalog.DefaultCategory;

        if (ContainsAny(n, "lamp", "light", "lantern", "candle", "chandelier", "sconce", "torch"))
            return "Lighting";

        if (ContainsAny(n, "rug", "carpet", "cushion", "pillow", "vase", "clock", "frame", "art", "statue", "decor", "mirror"))
            return "Decor";

        if (ContainsAny(n, "rock", "stone", "fence", "path", "fountain", "pavilion", "arch", "deck", "shed", "gazebo", "bridge", "wall"))
            return "Structures";

        return "Furniture";
    }

    static bool ContainsAny(string text, params string[] keys)
    {
        foreach (string key in keys)
        {
            if (text.Contains(key))
                return true;
        }
        return false;
    }

    static float GetSamplePrice(string name)
    {
        switch (name)
        {
            case "Chair": return 49.99f;
            case "Table": return 89.99f;
            case "Couch": return 249.99f;
            case "Bed": return 399.99f;
            case "Bookshelf": return 119.99f;
            case "Desk": return 179.99f;
            case "Lamp": return 39.99f;
            case "Vase": return 24.99f;
            default: return 0f;
        }
    }

    static void EnsureCategoryTabs(FurnitureCatalog catalog)
    {
        List<string> order = new List<string>();
        foreach (FurnitureItem item in catalog.items)
        {
            string category = string.IsNullOrEmpty(item.category) ? FurnitureCatalog.DefaultCategory : item.category.Trim();
            if (category.Length > 0 && !order.Contains(category))
                order.Add(category);
        }

        if (order.Count == 0)
            order.Add(FurnitureCatalog.DefaultCategory);

        catalog.categoryTabs.Clear();
        catalog.categoryTabs.AddRange(order);
        EditorUtility.SetDirty(catalog);
    }

    [MenuItem("Tools/Furniture Catalog/Generate Sample Furniture Prefabs")]
    static void GenerateSampleFurniturePrefabs()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
            AssetDatabase.CreateFolder("Assets", "Prefabs");
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs/Furniture"))
            AssetDatabase.CreateFolder("Assets/Prefabs", "Furniture");

        string dir = "Assets/Prefabs/Furniture";

        BuildPrefab(dir + "/Chair.prefab", "Chair", BuildChair);
        BuildPrefab(dir + "/Table.prefab", "Table", BuildTable);
        BuildPrefab(dir + "/Couch.prefab", "Couch", BuildCouch);
        BuildPrefab(dir + "/Bed.prefab", "Bed", BuildBed);
        BuildPrefab(dir + "/Bookshelf.prefab", "Bookshelf", BuildBookshelf);
        BuildPrefab(dir + "/Desk.prefab", "Desk", BuildDesk);
        BuildPrefab(dir + "/Lamp.prefab", "Lamp", BuildLamp);
        BuildPrefab(dir + "/Vase.prefab", "Vase", BuildVase);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        FurnitureCatalog catalog = UnityEngine.Object.FindAnyObjectByType<FurnitureCatalog>();
        if (catalog == null)
        {
            Debug.Log("Sample furniture prefabs created at " + dir + ". Run 'Setup Catalog UI' to add them to the catalog.");
            return;
        }

        PopulateItemsFromPrefabs(catalog);
        EnsureCategoryTabs(catalog);
        EditorUtility.SetDirty(catalog);
        EditorSceneManager.MarkSceneDirty(catalog.gameObject.scene);
        Debug.Log("Sample furniture generated and added to the catalog. Play the scene to see the new tabs.");
    }

    static void BuildPrefab(string path, string name, Action<GameObject> build)
    {
        GameObject root = new GameObject(name);
        build(root);
        PrefabUtility.SaveAsPrefabAsset(root, path);
        UnityEngine.Object.DestroyImmediate(root);
    }

    static GameObject AddPrimitive(GameObject parent, PrimitiveType type, string childName, Vector3 position, Vector3 scale)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = childName;
        go.transform.SetParent(parent.transform, false);
        go.transform.localPosition = position;
        go.transform.localScale = scale;
        return go;
    }

    static void BuildChair(GameObject root)
    {
        AddPrimitive(root, PrimitiveType.Cube, "Seat", new Vector3(0f, 0.45f, 0f), new Vector3(0.5f, 0.08f, 0.5f));
        AddPrimitive(root, PrimitiveType.Cube, "Backrest", new Vector3(0f, 0.8f, -0.21f), new Vector3(0.5f, 0.55f, 0.08f));
        AddPrimitive(root, PrimitiveType.Cube, "Leg Front Left", new Vector3(-0.2f, 0.2f, 0.2f), new Vector3(0.06f, 0.4f, 0.06f));
        AddPrimitive(root, PrimitiveType.Cube, "Leg Front Right", new Vector3(0.2f, 0.2f, 0.2f), new Vector3(0.06f, 0.4f, 0.06f));
        AddPrimitive(root, PrimitiveType.Cube, "Leg Back Left", new Vector3(-0.2f, 0.2f, -0.2f), new Vector3(0.06f, 0.4f, 0.06f));
        AddPrimitive(root, PrimitiveType.Cube, "Leg Back Right", new Vector3(0.2f, 0.2f, -0.2f), new Vector3(0.06f, 0.4f, 0.06f));
    }

    static void BuildTable(GameObject root)
    {
        AddPrimitive(root, PrimitiveType.Cube, "Top", new Vector3(0f, 0.75f, 0f), new Vector3(0.9f, 0.06f, 0.5f));
        AddPrimitive(root, PrimitiveType.Cube, "Leg Front Left", new Vector3(-0.4f, 0.35f, 0.2f), new Vector3(0.05f, 0.7f, 0.05f));
        AddPrimitive(root, PrimitiveType.Cube, "Leg Front Right", new Vector3(0.4f, 0.35f, 0.2f), new Vector3(0.05f, 0.7f, 0.05f));
        AddPrimitive(root, PrimitiveType.Cube, "Leg Back Left", new Vector3(-0.4f, 0.35f, -0.2f), new Vector3(0.05f, 0.7f, 0.05f));
        AddPrimitive(root, PrimitiveType.Cube, "Leg Back Right", new Vector3(0.4f, 0.35f, -0.2f), new Vector3(0.05f, 0.7f, 0.05f));
    }

    static void BuildCouch(GameObject root)
    {
        AddPrimitive(root, PrimitiveType.Cube, "Seat", new Vector3(0f, 0.3f, 0f), new Vector3(1.4f, 0.35f, 0.6f));
        AddPrimitive(root, PrimitiveType.Cube, "Backrest", new Vector3(0f, 0.62f, -0.22f), new Vector3(1.4f, 0.5f, 0.15f));
        AddPrimitive(root, PrimitiveType.Cube, "Arm Left", new Vector3(-0.72f, 0.4f, 0f), new Vector3(0.15f, 0.45f, 0.6f));
        AddPrimitive(root, PrimitiveType.Cube, "Arm Right", new Vector3(0.72f, 0.4f, 0f), new Vector3(0.15f, 0.45f, 0.6f));
    }

    static void BuildBed(GameObject root)
    {
        AddPrimitive(root, PrimitiveType.Cube, "Base", new Vector3(0f, 0.15f, 0f), new Vector3(1.5f, 0.3f, 1.9f));
        AddPrimitive(root, PrimitiveType.Cube, "Headboard", new Vector3(0f, 0.55f, -0.95f), new Vector3(1.5f, 0.5f, 0.1f));
    }

    static void BuildBookshelf(GameObject root)
    {
        AddPrimitive(root, PrimitiveType.Cube, "Side Left", new Vector3(-0.45f, 0.7f, 0f), new Vector3(0.08f, 1.4f, 0.35f));
        AddPrimitive(root, PrimitiveType.Cube, "Side Right", new Vector3(0.45f, 0.7f, 0f), new Vector3(0.08f, 1.4f, 0.35f));
        AddPrimitive(root, PrimitiveType.Cube, "Top", new Vector3(0f, 1.32f, 0f), new Vector3(0.9f, 0.08f, 0.35f));
        AddPrimitive(root, PrimitiveType.Cube, "Bottom", new Vector3(0f, 0.08f, 0f), new Vector3(0.9f, 0.08f, 0.35f));
        AddPrimitive(root, PrimitiveType.Cube, "Shelf 1", new Vector3(0f, 0.55f, 0f), new Vector3(0.9f, 0.06f, 0.35f));
        AddPrimitive(root, PrimitiveType.Cube, "Shelf 2", new Vector3(0f, 1f, 0f), new Vector3(0.9f, 0.06f, 0.35f));
    }

    static void BuildDesk(GameObject root)
    {
        AddPrimitive(root, PrimitiveType.Cube, "Top", new Vector3(0f, 0.75f, 0f), new Vector3(1.2f, 0.06f, 0.6f));
        AddPrimitive(root, PrimitiveType.Cube, "Side Left", new Vector3(-0.55f, 0.35f, 0f), new Vector3(0.06f, 0.7f, 0.55f));
        AddPrimitive(root, PrimitiveType.Cube, "Side Right", new Vector3(0.55f, 0.35f, 0f), new Vector3(0.06f, 0.7f, 0.55f));
        AddPrimitive(root, PrimitiveType.Cube, "Back Panel", new Vector3(0f, 0.35f, -0.27f), new Vector3(1.08f, 0.7f, 0.05f));
    }

    static void BuildLamp(GameObject root)
    {
        AddPrimitive(root, PrimitiveType.Cylinder, "Base", new Vector3(0f, 0.03f, 0f), new Vector3(0.4f, 0.04f, 0.4f));
        AddPrimitive(root, PrimitiveType.Cylinder, "Pole", new Vector3(0f, 0.66f, 0f), new Vector3(0.03f, 0.6f, 0.03f));
        AddPrimitive(root, PrimitiveType.Cylinder, "Shade", new Vector3(0f, 1.32f, 0f), new Vector3(0.28f, 0.12f, 0.28f));
    }

    static void BuildVase(GameObject root)
    {
        AddPrimitive(root, PrimitiveType.Cylinder, "Body", new Vector3(0f, 0.3f, 0f), new Vector3(0.22f, 0.3f, 0.22f));
        AddPrimitive(root, PrimitiveType.Cylinder, "Neck", new Vector3(0f, 0.58f, 0f), new Vector3(0.12f, 0.1f, 0.12f));
    }
}
