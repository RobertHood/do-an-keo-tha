using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

/// <summary>
/// Restyles the scene UI to match the Suburbsim website (Plus Jakarta Sans, white rounded cards, soft shadows,
/// ghost toolbar buttons, pill tabs and badges). Safe to run again: it only restyles, keeps every button's OnClick,
/// and everything it does can be undone (Edit > Undo).
/// Editor:  Tools > Suburbsim > Apply UI Theme
/// CLI:     -executeMethod SuburbsimUITheme.ApplyFromCommandLine  (opens SampleScene, applies, saves)
/// Assets used: Assets/UI/Fonts (Plus Jakarta Sans), Assets/UI/Sprites (rounded shapes), Assets/UI/Icons (white icons).
/// </summary>
public static class SuburbsimUITheme
{
    private const string UndoName = "Apply UI Theme";
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string FontsDir = "Assets/UI/Fonts";
    private const string SpritesDir = "Assets/UI/Sprites";
    private const string IconsDir = "Assets/UI/Icons";
    private const string FallbackFontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/Roboto SDF.asset";
    private const string TmpSettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";
    private const string InfoSpritePath = "Assets/Sprites/info-outline-512.png";
    private const string ErrorSpritePath = "Assets/Sprites/danger-512.png";
    private const int UiLayer = 5;

    // 9-slice border (texture pixels) of each generated shape; shapes are drawn at 2x and imported at 200 PPU
    private static readonly Dictionary<string, int> ShapeBorders = new Dictionary<string, int>
    {
        { "ui-round-8", 16 }, { "ui-round-12", 24 }, { "ui-round-16", 32 }, { "ui-round-24", 48 },
        { "ui-pill", 128 }, { "ui-outline-12", 24 }, { "ui-outline-16", 32 }, { "ui-outline-pill", 128 },
        { "ui-circle", 0 }, { "ui-shadow-16", 80 },
    };

    // Original toolbar icon -> white (tintable) copy in Assets/UI/Icons
    private static readonly Dictionary<string, string> IconMap = new Dictionary<string, string>
    {
        { "resize", "icon-resize" }, { "measure-512", "icon-measure" }, { "rotate", "icon-rotate" },
        { "lock-outline-512", "icon-lock" }, { "delete", "icon-delete" }, { "save", "icon-save" },
        { "export", "icon-export" }, { "import", "icon-import" }, { "yin-yang", "icon-yin-yang" },
    };

    private const string CatalogTitle = "Thư viện mô hình";

    private static TMP_FontAsset fontRegular, fontSemiBold, fontBold, fontExtraBold;
    private static Sprite round8, round12, round16, pill, outline16, circle, shadow16;
    private static readonly StringBuilder report = new StringBuilder();

    [MenuItem("Tools/Suburbsim/Apply UI Theme")]
    public static void ApplyFromMenu()
    {
        bool ok = Apply();
        EditorUtility.DisplayDialog("Suburbsim UI Theme", report.ToString(), "OK");
        if (ok) Debug.Log("[SuburbsimUITheme]\n" + report);
    }

    public static void ApplyFromCommandLine()
    {
        EditorSceneManager.OpenScene(ScenePath);
        bool ok = Apply();
        Debug.Log("[SuburbsimUITheme]\n" + report);
        if (!ok)
        {
            EditorApplication.Exit(1);
            return;
        }
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
    }

    private static bool Apply()
    {
        report.Clear();
        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName(UndoName);
        int undoGroup = Undo.GetCurrentGroup();

        try
        {
            ConfigureImports();
            if (!LoadAssets()) return false;

            Canvas canvas = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(c => c.isRootCanvas && c.name == "Canvas");
            if (canvas == null)
            {
                report.AppendLine("No root Canvas named 'Canvas' in the open scene.");
                return false;
            }

            SuburbsimCatalogTool.Refresh(report, false);
            ApplyFontToAllTexts(canvas.transform);

            RectTransform catalog = Find(canvas.transform, "Item Catalog");
            float catalogWidth = catalog != null ? catalog.rect.width > 0 ? catalog.rect.width : catalog.sizeDelta.x : 0f;

            if (catalog != null) StyleCatalog(catalog); else report.AppendLine("- 'Item Catalog' not found, skipped.");

            RectTransform functions = Find(canvas.transform, "Functions");
            if (functions != null) StyleToolbar(functions, catalogWidth); else report.AppendLine("- 'Functions' not found, skipped.");

            UINotification notification = Object.FindAnyObjectByType<UINotification>(FindObjectsInactive.Include);
            if (notification != null) StyleNotification(notification); else report.AppendLine("- No UI Notification in the scene, skipped.");

            StyleMeasureLabel();
            SetDefaultFont();

            EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
            report.Insert(0, "UI theme applied. Save the scene (Ctrl+S) to keep it; Edit > Undo reverts it.\n\n");
            return true;
        }
        finally
        {
            Undo.CollapseUndoOperations(undoGroup);
        }
    }

    // ---------------------------------------------------------------- assets

    private static void ConfigureImports()
    {
        foreach (KeyValuePair<string, int> shape in ShapeBorders)
        {
            ConfigureSprite($"{SpritesDir}/{shape.Key}.png", 200f, shape.Value, false);
        }
        foreach (string icon in IconMap.Values)
        {
            ConfigureSprite($"{IconsDir}/{icon}.png", 100f, 0, true);
        }
        ConfigureSprite(InfoSpritePath, 100f, 0, true);
        ConfigureSprite(ErrorSpritePath, 100f, 0, true);
    }

    private static void ConfigureSprite(string path, float pixelsPerUnit, int border, bool mipmaps)
    {
        if (!(AssetImporter.GetAtPath(path) is TextureImporter importer)) return;

        TextureImporterSettings settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);

        bool needsImport = importer.textureType != TextureImporterType.Sprite ||
                           importer.spriteImportMode != SpriteImportMode.Single ||
                           !Mathf.Approximately(importer.spritePixelsPerUnit, pixelsPerUnit) ||
                           importer.spriteBorder != new Vector4(border, border, border, border) ||
                           importer.mipmapEnabled != mipmaps ||
                           settings.spriteMeshType != SpriteMeshType.FullRect;
        if (!needsImport) return;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = pixelsPerUnit;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = mipmaps;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;

        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.spriteBorder = new Vector4(border, border, border, border);
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
    }

    private static bool LoadAssets()
    {
        fontRegular = EnsureFontAsset("Regular");
        fontSemiBold = EnsureFontAsset("SemiBold");
        fontBold = EnsureFontAsset("Bold");
        fontExtraBold = EnsureFontAsset("ExtraBold");

        round8 = LoadSprite($"{SpritesDir}/ui-round-8.png");
        round12 = LoadSprite($"{SpritesDir}/ui-round-12.png");
        round16 = LoadSprite($"{SpritesDir}/ui-round-16.png");
        pill = LoadSprite($"{SpritesDir}/ui-pill.png");
        outline16 = LoadSprite($"{SpritesDir}/ui-outline-16.png");
        circle = LoadSprite($"{SpritesDir}/ui-circle.png");
        shadow16 = LoadSprite($"{SpritesDir}/ui-shadow-16.png");

        if (fontRegular == null || fontSemiBold == null || fontBold == null || fontExtraBold == null)
        {
            report.AppendLine($"Plus Jakarta Sans fonts not found in {FontsDir}.");
            return false;
        }
        if (round12 == null || round16 == null || pill == null || outline16 == null || circle == null || shadow16 == null)
        {
            report.AppendLine($"UI shape sprites not found in {SpritesDir}.");
            return false;
        }

        // Regular asset maps the heavier weights, so <b> and Bold styles use the real bold faces
        TMP_FontWeightPair[] weights = fontRegular.fontWeightTable;
        if (weights != null && weights.Length >= 9)
        {
            weights[6].regularTypeface = fontSemiBold;
            weights[7].regularTypeface = fontBold;
            weights[8].regularTypeface = fontExtraBold;
            EditorUtility.SetDirty(fontRegular);
        }

        report.AppendLine("- Fonts: Plus Jakarta Sans Regular / SemiBold / Bold / ExtraBold (dynamic SDF, Vietnamese).");
        return true;
    }

    private static TMP_FontAsset EnsureFontAsset(string weight)
    {
        string assetPath = $"{FontsDir}/PlusJakartaSans-{weight} SDF.asset";
        TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
        if (existing != null) return existing;

        Font font = AssetDatabase.LoadAssetAtPath<Font>($"{FontsDir}/PlusJakartaSans-{weight}.ttf");
        if (font == null) return null;

        TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024,
            AtlasPopulationMode.Dynamic, true);
        asset.name = Path.GetFileNameWithoutExtension(assetPath);
        AssetDatabase.CreateAsset(asset, assetPath);

        asset.atlasTextures[0].name = asset.name + " Atlas";
        AssetDatabase.AddObjectToAsset(asset.atlasTextures[0], asset);
        asset.material.name = asset.name + " Material";
        AssetDatabase.AddObjectToAsset(asset.material, asset);

        TMP_FontAsset fallback = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FallbackFontPath);
        if (fallback != null)
        {
            if (asset.fallbackFontAssetTable == null) asset.fallbackFontAssetTable = new List<TMP_FontAsset>();
            asset.fallbackFontAssetTable.Add(fallback);
        }

        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
        return asset;
    }

    private static Sprite LoadSprite(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(path);

    private static void SetDefaultFont()
    {
        Object settings = AssetDatabase.LoadAssetAtPath<Object>(TmpSettingsPath);
        if (settings == null) return;

        SerializedObject so = new SerializedObject(settings);
        SerializedProperty prop = so.FindProperty("m_defaultFontAsset");
        if (prop == null || prop.objectReferenceValue == fontRegular) return;

        Undo.RecordObject(settings, UndoName);
        prop.objectReferenceValue = fontRegular;
        so.ApplyModifiedProperties();
        report.AppendLine("- TextMeshPro default font: Plus Jakarta Sans (used by text created at runtime).");
    }

    // ---------------------------------------------------------------- toolbar

    private static void StyleToolbar(RectTransform functions, float catalogWidth)
    {
        const float buttonWidth = 84f, buttonHeight = 76f, padding = 8f, spacing = 4f, topGap = 16f;

        Transform content = FindDeep(functions, "Content");
        if (content == null)
        {
            report.AppendLine("- Toolbar: 'Content' not found, skipped.");
            return;
        }

        // Plain row of buttons instead of the grid (dividers need their own width)
        foreach (Transform child in content.Cast<Transform>().ToList())
        {
            if (child.name == "_Divider") Undo.DestroyObjectImmediate(child.gameObject);
        }
        RemoveComponent<GridLayoutGroup>(content.gameObject);
        RemoveComponent<ContentSizeFitter>(content.gameObject);
        HorizontalLayoutGroup row = GetOrAdd<HorizontalLayoutGroup>(content.gameObject);
        Record(row);
        row.padding = new RectOffset((int)padding, (int)padding, (int)padding, (int)padding);
        row.spacing = spacing;
        row.childAlignment = TextAnchor.MiddleCenter;
        row.childControlWidth = false;
        row.childControlHeight = false;
        row.childForceExpandWidth = false;
        row.childForceExpandHeight = false;

        List<Button> buttons = content.Cast<Transform>().Select(t => t.GetComponent<Button>()).Where(b => b != null).ToList();
        int dividers = 0;
        int lastGroup = -1;
        foreach (Button button in buttons)
        {
            string method = GetFirstMethod(button.onClick);
            int group = GetToolGroup(method);
            if (lastGroup >= 0 && group != lastGroup)
            {
                CreateDivider(content, button.transform.GetSiblingIndex());
                dividers++;
            }
            lastGroup = group;
            StyleToolbarButton(button, method, buttonWidth, buttonHeight);
        }

        // White floating card centered over the 3D view (right of the catalog), hugging its buttons
        float width = padding * 2 + buttons.Count * buttonWidth + dividers + (buttons.Count + dividers - 1) * spacing;
        float height = buttonHeight + padding * 2;

        Record(functions);
        functions.anchorMin = functions.anchorMax = new Vector2(0.5f, 1f);
        functions.pivot = new Vector2(0.5f, 1f);
        functions.anchoredPosition = new Vector2(catalogWidth / 2f, -topGap);
        functions.sizeDelta = new Vector2(width, height);

        StyleImage(functions.GetComponent<Image>(), round16, UIPalette.White, true);
        CreateShadowBehind(functions, 0.10f);

        ScrollRect scroll = functions.GetComponent<ScrollRect>();
        if (scroll != null)
        {
            Record(scroll);
            if (scroll.horizontalScrollbar != null) SetActive(scroll.horizontalScrollbar.gameObject, false);
            if (scroll.verticalScrollbar != null) SetActive(scroll.verticalScrollbar.gameObject, false);
            scroll.horizontalScrollbar = null;
            scroll.verticalScrollbar = null;
            scroll.horizontal = false;
            scroll.vertical = false;
            if (scroll.viewport != null) Stretch(scroll.viewport, 0, 0, 0, 0);
        }
        Stretch((RectTransform)content, 0, 0, 0, 0);

        report.AppendLine($"- Toolbar: white card {width:0}x{height:0}, {buttons.Count} ghost buttons, {dividers} dividers, active / locked / delete states.");
    }

    private static void StyleToolbarButton(Button button, string method, float width, float height)
    {
        RectTransform rect = (RectTransform)button.transform;
        Record(rect);
        rect.sizeDelta = new Vector2(width, height);

        Image background = Or(button.targetGraphic as Image, button.GetComponent<Image>());
        StyleImage(background, round12, UIPalette.Clear, true);

        Record(button);
        button.transition = Selectable.Transition.None; // ToolbarButtonState draws hover / pressed / active
        Navigation nav = button.navigation;
        nav.mode = Navigation.Mode.None;
        button.navigation = nav;

        Image icon = button.GetComponentsInChildren<Image>(true).FirstOrDefault(i => i != background && !i.name.StartsWith("_"));
        if (icon != null)
        {
            Record(icon);
            if (icon.sprite != null && IconMap.TryGetValue(icon.sprite.name, out string white))
            {
                Sprite whiteIcon = LoadSprite($"{IconsDir}/{white}.png");
                if (whiteIcon != null) icon.sprite = whiteIcon;
            }
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            icon.color = UIPalette.Neutral700;
            SetRect((RectTransform)icon.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -10f), new Vector2(28f, 28f));
        }

        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            StyleText(label, fontSemiBold, 12f, UIPalette.Neutral700, TextAlignmentOptions.Top, true, 9f);
            Record(label);
            label.lineSpacing = -12f;
            SetRect((RectTransform)label.transform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 5f), new Vector2(-6f, 31f));
        }

        ToolbarButtonState.Tool tool = ToolbarButtonState.Tool.None;
        switch (method)
        {
            case "ToggleResizeMode": tool = ToolbarButtonState.Tool.Resize; break;
            case "ToggleRotateMode": tool = ToolbarButtonState.Tool.Rotate; break;
            case "ToggleMeasureMode": tool = ToolbarButtonState.Tool.Measure; break;
            case "ToggleSelectedModelLock": tool = ToolbarButtonState.Tool.Lock; break;
            case "DeleteSelectedModel": tool = ToolbarButtonState.Tool.Delete; break;
        }
        bool needsSelection = tool == ToolbarButtonState.Tool.Resize || tool == ToolbarButtonState.Tool.Rotate ||
                              tool == ToolbarButtonState.Tool.Lock || tool == ToolbarButtonState.Tool.Delete;

        ToolbarButtonState state = GetOrAdd<ToolbarButtonState>(button.gameObject);
        Record(state);
        state.Configure(tool, needsSelection, tool == ToolbarButtonState.Tool.Delete, background, icon, label);
        EditorUtility.SetDirty(state);
    }

    private static int GetToolGroup(string method)
    {
        switch (method)
        {
            case "ToggleResizeMode":
            case "ToggleMeasureMode":
            case "ToggleRotateMode":
                return 0;
            case "ToggleSelectedModelLock":
            case "DeleteSelectedModel":
                return 1;
            case "Save":
            case "Export":
            case "Import":
                return 2;
            default:
                return 3;
        }
    }

    private static void CreateDivider(Transform parent, int siblingIndex)
    {
        GameObject divider = CreateUIObject("_Divider", parent);
        divider.transform.SetSiblingIndex(siblingIndex);
        ((RectTransform)divider.transform).sizeDelta = new Vector2(1f, 44f);
        Image image = divider.AddComponent<Image>();
        image.color = UIPalette.Neutral200;
        image.raycastTarget = false;
    }

    // ---------------------------------------------------------------- catalog

    private const float CatalogMargin = 16f;
    private const float CatalogTitleHeight = 28f;
    private const float TabHeight = 32f;
    private const float CatalogGap = 12f;

    private static void StyleCatalog(RectTransform catalog)
    {
        float margin = CatalogMargin;
        float width = catalog.sizeDelta.x - 2 * margin;
        float tabsTop = margin + CatalogTitleHeight + CatalogGap;
        int tabRows = Mathf.CeilToInt(ModelCategories.Ordered.Length / 4f);
        float tabsHeight = 4f + tabRows * (TabHeight + 4f);
        float sectionTop = tabsTop + tabsHeight + 16f;
        float listTop = sectionTop + 20f + 10f;

        StyleImage(catalog.GetComponent<Image>(), null, UIPalette.White, true);

        RectTransform border = EnsureChild(catalog, "_BorderRight", -1);
        SetRect(border, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(1f, 0f));
        StyleImage(GetOrAdd<Image>(border.gameObject), null, UIPalette.Neutral200, false);
        IgnoreLayout(border.gameObject);

        RectTransform title = EnsureChild(catalog, "_Title", -1);
        SetRect(title, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -margin),
            new Vector2(-2 * margin, CatalogTitleHeight));
        TMP_Text titleText = GetOrAdd<TextMeshProUGUI>(title.gameObject);
        StyleText(titleText, fontBold, 18f, UIPalette.BrandBlack, TextAlignmentOptions.MidlineLeft, false, 0f);
        Record(titleText);
        titleText.text = CatalogTitle;
        titleText.raycastTarget = false;

        RectTransform section = EnsureChild(catalog, "_Section", -1);
        SetRect(section, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -sectionTop),
            new Vector2(-2 * margin, 20f));
        TMP_Text sectionText = GetOrAdd<TextMeshProUGUI>(section.gameObject);
        StyleText(sectionText, fontSemiBold, 13f, UIPalette.Neutral500, TextAlignmentOptions.MidlineLeft, false, 0f);
        Record(sectionText);
        sectionText.text = ModelCategories.FullName(ModelCategory.House) + " · 0 mô hình";
        sectionText.raycastTarget = false;

        RectTransform empty = EnsureChild(catalog, "_Empty", -1);
        SetRect(empty, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -(listTop + 24f)),
            new Vector2(-2 * margin - 32f, 60f));
        TMP_Text emptyText = GetOrAdd<TextMeshProUGUI>(empty.gameObject);
        StyleText(emptyText, fontRegular, 14f, UIPalette.Neutral500, TextAlignmentOptions.Center, true, 0f);
        Record(emptyText);
        emptyText.text = "Chưa có mô hình nào trong mục này.";
        emptyText.raycastTarget = false;

        Button tabTemplate = null;
        RectTransform tabs = Find(catalog, "Catalog Tabs");
        if (tabs != null) tabTemplate = StyleTabs(catalog, tabs, width, margin, tabsTop, tabsHeight);

        CatalogCard cardTemplate = null;
        ScrollRect scroll = catalog.GetComponent<ScrollRect>();
        if (scroll != null)
        {
            Record(scroll);
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide; // Don't let it resize the viewport
            scroll.horizontal = false;
            scroll.scrollSensitivity = 30f;

            if (scroll.viewport != null) Stretch(scroll.viewport, margin, margin, listTop, margin);

            RectTransform content = scroll.content;
            if (content != null)
            {
                SetRect(content, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero,
                    new Vector2(0f, content.sizeDelta.y));
                GridLayoutGroup grid = GetOrAdd<GridLayoutGroup>(content.gameObject);
                Record(grid);
                grid.cellSize = new Vector2(Mathf.Floor((width - CatalogGap) / 2f), 196f);
                grid.spacing = new Vector2(CatalogGap, CatalogGap);
                grid.padding = new RectOffset(0, 0, 0, (int)margin);
                grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                grid.constraintCount = 2;
                grid.childAlignment = TextAnchor.UpperLeft;
                ContentSizeFitter fitter = GetOrAdd<ContentSizeFitter>(content.gameObject);
                Record(fitter);
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

                // One card is the template UICatalog clones for every model; hand-made extra cards are removed
                PriceCatalog prices = FindPriceCatalog();
                List<Button> cards = content.Cast<Transform>().Select(t => t.GetComponent<Button>()).Where(b => b != null).ToList();
                for (int i = 0; i < cards.Count; i++)
                {
                    if (i == 0)
                    {
                        Record(cards[i].gameObject);
                        cards[i].name = "Card Template";
                        cardTemplate = StyleCard(cards[i], prices);
                    }
                    else
                    {
                        Undo.DestroyObjectImmediate(cards[i].gameObject);
                    }
                }
                if (cards.Count > 1) report.AppendLine($"- Catalog: removed {cards.Count - 1} hand-made card(s); cards are now generated from the price catalog.");
            }

            if (scroll.verticalScrollbar != null)
            {
                RectTransform bar = (RectTransform)scroll.verticalScrollbar.transform;
                Record(bar);
                bar.anchorMin = new Vector2(1f, 0f);
                bar.anchorMax = new Vector2(1f, 1f);
                bar.pivot = new Vector2(1f, 0.5f);
                bar.offsetMin = new Vector2(-9f, margin);
                bar.offsetMax = new Vector2(-4f, -listTop);
                StyleImage(bar.GetComponent<Image>(), null, UIPalette.Clear, true);
                Transform slidingArea = bar.Find("Sliding Area");
                if (slidingArea != null) Stretch((RectTransform)slidingArea, 0, 0, 0, 0);
                if (scroll.verticalScrollbar.handleRect != null)
                {
                    Record(scroll.verticalScrollbar.handleRect);
                    scroll.verticalScrollbar.handleRect.sizeDelta = Vector2.zero;
                    StyleImage(scroll.verticalScrollbar.handleRect.GetComponent<Image>(), pill, UIPalette.Neutral300, true);
                }
            }
        }

        UICatalog ui = GetOrAdd<UICatalog>(catalog.gameObject);
        SerializedObject so = new SerializedObject(ui);
        Record(ui);
        SetRef(so, "catalog", FindPriceCatalog());
        SetRef(so, "spawner", Object.FindAnyObjectByType<SpawnInModel>(FindObjectsInactive.Include));
        SetRef(so, "tabsContainer", tabs);
        SetRef(so, "tabTemplate", tabTemplate);
        SetRef(so, "cardsContainer", scroll != null ? scroll.content : null);
        SetRef(so, "cardTemplate", cardTemplate);
        SetRef(so, "scrollRect", scroll);
        SetRef(so, "sectionTitle", sectionText);
        SetRef(so, "emptyState", emptyText);
        so.ApplyModifiedProperties();

        report.AppendLine($"- Catalog: {ModelCategories.Ordered.Length} working tabs in {tabRows} rows, heading, empty state, cards generated per tab (UI Catalog).");
    }

    private static Button StyleTabs(RectTransform catalog, RectTransform tabs, float width, float margin, float top, float height)
    {
        SetRect(tabs, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -top),
            new Vector2(-2 * margin, height));
        StyleImage(tabs.GetComponent<Image>(), round16, UIPalette.Neutral100, true);

        // Two rows of equal pills (7 tabs don't fit one row in the sidebar)
        RemoveComponent<HorizontalLayoutGroup>(tabs.gameObject);
        GridLayoutGroup grid = GetOrAdd<GridLayoutGroup>(tabs.gameObject);
        Record(grid);
        grid.padding = new RectOffset(4, 4, 4, 4);
        grid.spacing = new Vector2(4f, 4f);
        grid.cellSize = new Vector2(Mathf.Floor((width - 8f - 12f) / 4f), TabHeight);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 4;
        grid.childAlignment = TextAnchor.UpperLeft;

        // The collapse button moves next to the title
        Transform hide = tabs.Cast<Transform>().FirstOrDefault(t => t.name == "Hide Panel");
        if (hide != null)
        {
            Undo.SetTransformParent(hide, catalog, UndoName);
            SetRect((RectTransform)hide, Vector2.one, Vector2.one, Vector2.one, new Vector2(-margin + 4f, -margin), new Vector2(32f, 28f));
            Button hideButton = hide.GetComponent<Button>();
            if (hideButton != null)
            {
                StyleImage(Or(hideButton.targetGraphic as Image, hide.GetComponent<Image>()), round8, UIPalette.White, true);
                SetColorTint(hideButton, 0.94f);
            }
            TMP_Text hideLabel = hide.GetComponentInChildren<TMP_Text>(true);
            if (hideLabel != null) StyleText(hideLabel, fontSemiBold, 16f, UIPalette.Neutral500, TextAlignmentOptions.Center, false, 0f);
        }

        // Keep one tab as the template UICatalog clones for every category; old fixed tabs are removed
        List<Button> buttons = tabs.Cast<Transform>().Select(t => t.GetComponent<Button>()).Where(b => b != null).ToList();
        Button template = Or(buttons.FirstOrDefault(b => b.name == "Tab Template"), buttons.FirstOrDefault());
        foreach (Button button in buttons)
        {
            if (button != template) Undo.DestroyObjectImmediate(button.gameObject);
        }
        if (template == null) return null;

        Record(template.gameObject);
        template.name = "Tab Template";
        StyleImage(Or(template.targetGraphic as Image, template.GetComponent<Image>()), pill, UIPalette.BrandBlack, true);
        SetColorTint(template, 0.9f);
        TMP_Text label = template.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            StyleText(label, fontSemiBold, 13f, UIPalette.White, TextAlignmentOptions.Center, false, 10f);
            Record(label);
            label.text = ModelCategories.ShortName(ModelCategory.House);
            Stretch((RectTransform)label.transform, 4, 4, 0, 0);
        }
        return template;
    }

    private static CatalogCard StyleCard(Button card, PriceCatalog prices)
    {
        Image background = Or(card.targetGraphic as Image, card.GetComponent<Image>());
        StyleImage(background, round16, UIPalette.White, true);
        SetColorTint(card, 0.95f);

        RectTransform outline = EnsureChild(card.transform, "_Outline", 0);
        Stretch(outline, 0, 0, 0, 0);
        StyleImage(GetOrAdd<Image>(outline.gameObject), outline16, UIPalette.Neutral200, false);

        Image thumbnail = card.GetComponentsInChildren<Image>(true)
            .FirstOrDefault(i => i != background && !i.name.StartsWith("_"));
        if (thumbnail != null)
        {
            Record(thumbnail);
            thumbnail.sprite = round12;
            thumbnail.type = Image.Type.Sliced;
            thumbnail.color = UIPalette.Neutral100;
            thumbnail.preserveAspect = false;
            thumbnail.raycastTarget = false;
            SetRect((RectTransform)thumbnail.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -8f), new Vector2(-16f, 120f));
        }

        TMP_Text nameText = card.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(t => !t.name.StartsWith("_"));
        if (nameText != null)
        {
            StyleText(nameText, fontSemiBold, 15f, UIPalette.BrandBlack, TextAlignmentOptions.MidlineLeft, false, 0f);
            Record(nameText);
            nameText.overflowMode = TextOverflowModes.Ellipsis;
            SetRect((RectTransform)nameText.transform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 38f), new Vector2(-24f, 24f));
        }

        RectTransform badge = EnsureChild(card.transform, "_PriceBadge", -1);
        SetRect(badge, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(12f, 10f), new Vector2(80f, 22f));

        // The pill must not sit on the badge itself: an Image reports its sprite size as preferred size, and the
        // Content Size Fitter would grow the badge to the whole pill sprite (a giant circle)
        RemoveComponent<Image>(badge.gameObject);
        RectTransform badgeBackground = EnsureChild(badge, "_BadgeBackground", 0);
        Stretch(badgeBackground, 0, 0, 0, 0);
        IgnoreLayout(badgeBackground.gameObject);
        Image badgeImage = GetOrAdd<Image>(badgeBackground.gameObject);
        StyleImage(badgeImage, pill, UIPalette.BrandGreenLight, false);

        HorizontalLayoutGroup badgeLayout = GetOrAdd<HorizontalLayoutGroup>(badge.gameObject);
        Record(badgeLayout);
        badgeLayout.padding = new RectOffset(10, 10, 3, 3);
        badgeLayout.childControlWidth = true;
        badgeLayout.childControlHeight = true;
        badgeLayout.childForceExpandWidth = false;
        badgeLayout.childForceExpandHeight = false;
        ContentSizeFitter badgeFitter = GetOrAdd<ContentSizeFitter>(badge.gameObject);
        Record(badgeFitter);
        badgeFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        badgeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        RectTransform priceRect = EnsureChild(badge, "_PriceText", -1);
        TMP_Text priceText = GetOrAdd<TextMeshProUGUI>(priceRect.gameObject);
        StyleText(priceText, fontBold, 12f, UIPalette.BrandGreen, TextAlignmentOptions.Center, false, 0f);
        Record(priceText);
        priceText.raycastTarget = false;

        // Preview the first catalog model on the template so it looks right in the editor too
        PriceCatalogEntry entry = prices != null && prices.entries != null
            ? prices.entries.FirstOrDefault(e => e != null && e.prefab != null && e.category != ModelCategory.Uncategorized)
            : null;
        string prefabId = entry != null ? entry.Id : (GetFirstObjectArgument(card) is GameObject prefab ? prefab.name : card.name);
        if (nameText != null) nameText.text = entry != null && !string.IsNullOrEmpty(entry.displayName) ? entry.displayName : prefabId;
        if (entry != null)
        {
            List<PriceOffer> offers = ModelEstPrice.GetSortedOffers(entry);
            priceText.text = (offers.Count > 1 ? "Từ " : "") + UIPalette.FormatVnd(offers.Count > 0 ? offers[0].price : entry.listedPrice);
        }
        else
        {
            priceText.text = "Chưa có giá";
        }

        CatalogCard script = GetOrAdd<CatalogCard>(card.gameObject);
        Record(script);
        script.Configure(prefabId, thumbnail, nameText, priceText, badgeImage);
        EditorUtility.SetDirty(script);
        return script;
    }

    private static PriceCatalog FindPriceCatalog()
    {
        ModelEstPrice prices = Object.FindAnyObjectByType<ModelEstPrice>(FindObjectsInactive.Include);
        if (prices != null && prices.Catalog != null) return prices.Catalog;

        string guid = AssetDatabase.FindAssets("t:PriceCatalog").FirstOrDefault();
        return guid != null ? AssetDatabase.LoadAssetAtPath<PriceCatalog>(AssetDatabase.GUIDToAssetPath(guid)) : null;
    }

    // ---------------------------------------------------------------- notification

    private static void StyleNotification(UINotification notification)
    {
        SerializedObject so = new SerializedObject(notification);
        GameObject box = Or(so.FindProperty("box").objectReferenceValue as GameObject, notification.gameObject);
        RectTransform rect = (RectTransform)box.transform;

        Record(rect);
        rect.anchorMin = rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(1f, 0f);
        rect.anchoredPosition = new Vector2(-24f, 24f);
        rect.sizeDelta = new Vector2(440f, 92f);

        // The box's own Image would draw over its children, so the card and shadow become children instead
        Image boxImage = box.GetComponent<Image>();
        if (boxImage != null)
        {
            StyleImage(boxImage, null, UIPalette.Clear, false);
        }

        RectTransform shadow = EnsureChild(rect, "_Shadow", 0);
        StretchOffsets(shadow, new Vector2(-24f, -30f), new Vector2(24f, 18f));
        StyleImage(GetOrAdd<Image>(shadow.gameObject), shadow16, new Color(0f, 0f, 0f, 0.16f), false);

        RectTransform card = EnsureChild(rect, "_Card", 1);
        Stretch(card, 0, 0, 0, 0);
        StyleImage(GetOrAdd<Image>(card.gameObject), round16, UIPalette.White, false);

        RectTransform outline = EnsureChild(rect, "_Outline", 2);
        Stretch(outline, 0, 0, 0, 0);
        StyleImage(GetOrAdd<Image>(outline.gameObject), outline16, UIPalette.Neutral200, false);

        RectTransform iconBg = EnsureChild(rect, "_IconBg", 3);
        SetRect(iconBg, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(16f, 0f), new Vector2(44f, 44f));
        Image iconBgImage = GetOrAdd<Image>(iconBg.gameObject);
        StyleImage(iconBgImage, circle, UIPalette.BrandBlueLight, false);

        Image icon = Or(so.FindProperty("iconImage").objectReferenceValue as Image,
            box.GetComponentsInChildren<Image>(true).FirstOrDefault(i => i.gameObject != box && !i.name.StartsWith("_") &&
                                                                         i.GetComponentInParent<Button>() == null));
        if (icon != null)
        {
            Record(icon);
            icon.color = UIPalette.White;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            icon.transform.SetSiblingIndex(4);
            SetRect((RectTransform)icon.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(38f, 0f), new Vector2(24f, 24f));
        }

        Button close = Or(so.FindProperty("closeButton").objectReferenceValue as Button, box.GetComponentInChildren<Button>(true));
        if (close == null)
        {
            Transform closeObj = box.transform.Find("Close");
            if (closeObj != null) close = Undo.AddComponent<Button>(closeObj.gameObject);
        }

        List<TMP_Text> texts = box.GetComponentsInChildren<TMP_Text>(true)
            .Where(t => close == null || !t.transform.IsChildOf(close.transform)).ToList();
        TMP_Text title = Or(so.FindProperty("titleText").objectReferenceValue as TMP_Text,
            texts.FirstOrDefault(t => t.name.IndexOf("title", System.StringComparison.OrdinalIgnoreCase) >= 0));
        TMP_Text message = Or(so.FindProperty("messageText").objectReferenceValue as TMP_Text, texts.FirstOrDefault(t => t != title));

        if (title != null)
        {
            StyleText(title, fontBold, 14f, UIPalette.BrandBlack, TextAlignmentOptions.BottomLeft, false, 0f);
            SetRect((RectTransform)title.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f),
                new Vector2(72f, -14f), new Vector2(-116f, 20f));
        }

        if (message != null)
        {
            StyleText(message, fontRegular, 14f, UIPalette.Neutral700,
                title != null ? TextAlignmentOptions.TopLeft : TextAlignmentOptions.MidlineLeft, true, 11f);
            Record(message);
            message.overflowMode = TextOverflowModes.Ellipsis;
            StretchOffsets((RectTransform)message.transform, new Vector2(72f, 16f), new Vector2(-44f, title != null ? -36f : -14f));
        }

        if (close != null)
        {
            RectTransform closeRect = (RectTransform)close.transform;
            closeRect.SetAsLastSibling();
            SetRect(closeRect, Vector2.one, Vector2.one, Vector2.one, new Vector2(-10f, -10f), new Vector2(26f, 26f));
            Image closeImage = Or(close.targetGraphic as Image, close.GetComponent<Image>());
            if (closeImage == null) closeImage = Undo.AddComponent<Image>(close.gameObject);
            StyleImage(closeImage, round8, UIPalette.White, true);
            Record(close);
            close.targetGraphic = closeImage;
            SetColorTint(close, 0.94f);
            TMP_Text closeLabel = close.GetComponentInChildren<TMP_Text>(true);
            if (closeLabel != null)
            {
                StyleText(closeLabel, fontRegular, 20f, UIPalette.Neutral500, TextAlignmentOptions.Center, false, 0f);
                Record(closeLabel);
                closeLabel.text = "×";
                Stretch((RectTransform)closeLabel.transform, 0, 0, 0, 0);
            }
        }

        Record(notification);
        so.Update();
        SetRef(so, "iconImage", icon);
        SetRef(so, "iconBackground", iconBgImage);
        SetRef(so, "messageText", message);
        SetRef(so, "titleText", title);
        SetRef(so, "closeButton", close);
        if (so.FindProperty("infoSprite").objectReferenceValue == null) SetRef(so, "infoSprite", LoadSprite(InfoSpritePath));
        if (so.FindProperty("errorSprite").objectReferenceValue == null) SetRef(so, "errorSprite", LoadSprite(ErrorSpritePath));
        SetRef(so, "timerBarSprite", pill);
        SetFloat(so, "timerBarHeight", 3f);
        SetFloat(so, "timerBarGap", -11f);
        SetFloat(so, "timerBarInset", 16f);
        so.ApplyModifiedProperties();

        report.AppendLine("- Notification: white card with shadow and border, tinted icon circle, title / message fonts, close button, inset timer bar.");
    }

    // ---------------------------------------------------------------- measure label

    private static void StyleMeasureLabel()
    {
        ModelMeasureDistance measure = Object.FindAnyObjectByType<ModelMeasureDistance>(FindObjectsInactive.Include);
        if (measure == null)
        {
            report.AppendLine("- Measure label: no Model Measure Distance in the scene (the auto-created one keeps the default look).");
            return;
        }

        SerializedObject so = new SerializedObject(measure);
        Record(measure);
        SetRef(so, "labelSprite", pill);
        SetRef(so, "labelFont", fontBold);
        so.ApplyModifiedProperties();
        report.AppendLine("- Measure label: black pill with Plus Jakarta Sans Bold.");
    }

    // ---------------------------------------------------------------- helpers

    private static void ApplyFontToAllTexts(Transform root)
    {
        int count = 0;
        foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
        {
            if (text.font == fontRegular || text.font == fontSemiBold || text.font == fontBold || text.font == fontExtraBold) continue;
            Record(text);
            text.font = fontRegular;
            count++;
        }
        if (count > 0) report.AppendLine($"- {count} text(s) switched to Plus Jakarta Sans.");
    }

    private static void StyleText(TMP_Text text, TMP_FontAsset font, float size, Color color, TextAlignmentOptions alignment,
        bool wrap, float minAutoSize)
    {
        Record(text);
        text.font = font;
        text.fontSharedMaterial = font.material;
        text.fontStyle = FontStyles.Normal;
        text.color = color;
        text.alignment = alignment;
        text.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
        text.enableAutoSizing = minAutoSize > 0f;
        if (text.enableAutoSizing)
        {
            text.fontSizeMin = minAutoSize;
            text.fontSizeMax = size;
        }
        text.fontSize = size;
        text.characterSpacing = 0f;
        text.margin = Vector4.zero;
    }

    private static void StyleImage(Image image, Sprite sprite, Color color, bool raycastTarget)
    {
        if (image == null) return;
        Record(image);
        image.sprite = sprite;
        image.type = sprite != null && sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
        image.pixelsPerUnitMultiplier = 1f;
        image.color = color;
        image.raycastTarget = raycastTarget;
    }

    private static void SetColorTint(Button button, float highlightBrightness)
    {
        Record(button);
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(highlightBrightness, highlightBrightness, highlightBrightness + 0.01f, 1f);
        colors.pressedColor = new Color(highlightBrightness - 0.05f, highlightBrightness - 0.05f, highlightBrightness - 0.04f, 1f);
        colors.selectedColor = Color.white; // Don't stay grey after a click
        colors.disabledColor = new Color(1f, 1f, 1f, 0.5f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        button.colors = colors;
    }

    private static void CreateShadowBehind(RectTransform target, float alpha)
    {
        Transform parent = target.parent;
        string shadowName = "_" + target.name + " Shadow";
        Transform existing = parent.Find(shadowName);
        RectTransform shadow = existing != null ? (RectTransform)existing : (RectTransform)CreateUIObject(shadowName, parent).transform;

        // Directly behind the target
        int targetIndex = target.GetSiblingIndex();
        int shadowIndex = shadow.GetSiblingIndex();
        if (shadowIndex > targetIndex) shadow.SetSiblingIndex(targetIndex);
        else if (shadowIndex < targetIndex - 1) shadow.SetSiblingIndex(targetIndex - 1);
        Record(shadow);
        shadow.anchorMin = target.anchorMin;
        shadow.anchorMax = target.anchorMax;
        shadow.pivot = target.pivot;
        shadow.anchoredPosition = target.anchoredPosition + new Vector2(0f, 24f - 6f);
        shadow.sizeDelta = target.sizeDelta + new Vector2(48f, 48f);
        StyleImage(GetOrAdd<Image>(shadow.gameObject), shadow16, new Color(0f, 0f, 0f, alpha), false);
        IgnoreLayout(shadow.gameObject);
    }

    private static RectTransform EnsureChild(Transform parent, string name, int siblingIndex)
    {
        Transform existing = parent.Find(name);
        RectTransform rect = existing != null ? (RectTransform)existing : (RectTransform)CreateUIObject(name, parent).transform;
        if (siblingIndex >= 0) rect.SetSiblingIndex(siblingIndex);
        else rect.SetAsLastSibling();
        return rect;
    }

    private static GameObject CreateUIObject(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = UiLayer;
        Undo.RegisterCreatedObjectUndo(go, UndoName);
        go.transform.SetParent(parent, false);
        return go;
    }

    private static void IgnoreLayout(GameObject go)
    {
        LayoutElement element = GetOrAdd<LayoutElement>(go);
        Record(element);
        element.ignoreLayout = true;
    }

    private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size)
    {
        Record(rect);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void Stretch(RectTransform rect, float left, float right, float top, float bottom)
    {
        Record(rect);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }

    private static void StretchOffsets(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
    {
        Record(rect);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    private static RectTransform Find(Transform parent, string name)
    {
        Transform found = parent.Find(name);
        if (found == null)
        {
            found = parent.Cast<Transform>().FirstOrDefault(t => t.name.Trim() == name);
        }
        return found as RectTransform;
    }

    private static Transform FindDeep(Transform parent, string name)
    {
        return parent.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);
    }

    private static T GetOrAdd<T>(GameObject go) where T : Component
    {
        T component = go.GetComponent<T>();
        return component != null ? component : Undo.AddComponent<T>(go);
    }

    private static void RemoveComponent<T>(GameObject go) where T : Component
    {
        T component = go.GetComponent<T>();
        if (component != null) Undo.DestroyObjectImmediate(component);
    }

    private static void SetActive(GameObject go, bool active)
    {
        if (go.activeSelf == active) return;
        Undo.RecordObject(go, UndoName);
        go.SetActive(active);
    }

    private static void Record(Object obj)
    {
        if (obj != null) Undo.RecordObject(obj, UndoName);
    }

    /// <summary>
    /// First value that is not null, using Unity's null check (C#'s null-coalescing misses destroyed / missing objects).
    /// </summary>
    private static T Or<T>(T first, T second) where T : Object => first != null ? first : second;

    private static void SetRef(SerializedObject so, string property, Object value)
    {
        SerializedProperty prop = so.FindProperty(property);
        if (prop != null) prop.objectReferenceValue = value;
    }

    private static void SetFloat(SerializedObject so, string property, float value)
    {
        SerializedProperty prop = so.FindProperty(property);
        if (prop != null) prop.floatValue = value;
    }

    private static string GetFirstMethod(UnityEventBase evt)
    {
        for (int i = 0; i < evt.GetPersistentEventCount(); i++)
        {
            string method = evt.GetPersistentMethodName(i);
            if (!string.IsNullOrEmpty(method)) return method;
        }
        return "";
    }

    /// <summary>
    /// The object argument of the first persistent OnClick call (e.g. the prefab a catalog card spawns).
    /// </summary>
    private static Object GetFirstObjectArgument(Button button)
    {
        SerializedObject so = new SerializedObject(button);
        SerializedProperty calls = so.FindProperty("m_OnClick.m_PersistentCalls.m_Calls");
        for (int i = 0; calls != null && i < calls.arraySize; i++)
        {
            Object arg = calls.GetArrayElementAtIndex(i).FindPropertyRelative("m_Arguments.m_ObjectArgument").objectReferenceValue;
            if (arg != null) return arg;
        }
        return null;
    }
}
