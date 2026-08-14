using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class FurnitureCatalog : MonoBehaviour
{
    public enum SpawnMode
    {
        OutsideRoom,
        RoomCenter,
        MousePosition
    }

    public enum StagingSide
    {
        East,
        North
    }

    [Tooltip("The catalog entries. Each one becomes a button in the list.")]
    public List<FurnitureItem> items = new List<FurnitureItem>();

    [Tooltip("Order of the tabs above the catalog. Leave empty to auto-derive from the item categories.")]
    public List<string> categoryTabs = new List<string>();

    [Tooltip("A button used as the visual template for every catalog entry. Usually the first button already in the list.")]
    public Button buttonTemplate;

    [Tooltip("Camera used for cursor-based spawning. Defaults to Camera.main.")]
    public Camera mainCamera;

    public SpawnMode spawnMode = SpawnMode.OutsideRoom;
    public bool snapToGrid = true;

    [Tooltip("How much bigger the spawned object should be. Use this so items are clearly visible on the field.")]
    public float spawnScale = 1.5f;

    [Tooltip("Aligns the spawned object so its base rests on the floor (y = 0).")]
    public bool snapToGround = true;

    [Tooltip("Wall of the room that staged items spawn just outside of.")]
    public StagingSide stagingSide = StagingSide.East;

    [Tooltip("Distance outside the room wall that staged items spawn at.")]
    public float stagingMargin = 1.2f;

    [Tooltip("Gap between consecutive staged items along the wall. Keep it a whole grid step (e.g. 2) so grid snapping doesn't compress the row.")]
    public float stagingSpacing = 2f;

    [Tooltip("Items are spread randomly within this ring around the room center so they don't stack.")]
    public float minDistanceFromCenter = 1f;

    public float maxDistanceFromCenter = 2.5f;

    private readonly List<Button> buttons = new List<Button>();

    public const string DefaultCategory = "Plants";
    private const float TabHeight = 40f;
    private const float TabBarMargin = 4f;

    private static readonly Color TabNormalColor = new Color(0.15f, 0.15f, 0.15f, 0.75f);
    private static readonly Color TabActiveColor = new Color(0.35f, 0.60f, 0.90f, 1f);

    private readonly List<string> activeTabs = new List<string>();
    private readonly List<Button> tabButtons = new List<Button>();
    private GameObject tabBar;
    private string selectedTab;

    private int stagingIndex;

    void Awake()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;
    }

    void Start()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;

        RoomManager.EnsureInstance();
        FurnitureTotalTracker.EnsureInstance();
        PlotPanel.EnsureInstance();

        if (buttonTemplate == null)
            buttonTemplate = GetComponentInChildren<Button>();

        SetupCategoryTabs();
        BuildTabs();
        BuildCatalog();
    }

    public void BuildCatalog()
    {
        if (buttonTemplate == null)
        {
            Debug.LogWarning("FurnitureCatalog: No button template assigned. Add a button under this catalog and assign it.", this);
            return;
        }

        foreach (Button oldButton in buttons)
        {
            if (oldButton != null)
                Destroy(oldButton.gameObject);
        }
        buttons.Clear();

        string tab = string.IsNullOrEmpty(selectedTab) ? DefaultCategory : selectedTab;

        for (int i = 0; i < items.Count; i++)
        {
            FurnitureItem item = items[i];
            if (!MatchesCategory(item, tab))
                continue;

            Button button = Instantiate(buttonTemplate, buttonTemplate.transform.parent);
            button.gameObject.SetActive(true);
            buttons.Add(button);

            ConfigureButton(button, item);
            int index = i;
            button.onClick.AddListener(() => SpawnItem(items[index]));
        }

        if (buttonTemplate.gameObject.activeSelf)
            buttonTemplate.gameObject.SetActive(false);
    }

    static string GetCategory(FurnitureItem item)
    {
        string category = item != null ? item.category : null;
        return string.IsNullOrEmpty(category) ? DefaultCategory : category.Trim();
    }

    bool MatchesCategory(FurnitureItem item, string tab)
    {
        return string.Equals(GetCategory(item), tab, StringComparison.OrdinalIgnoreCase);
    }

    void SetupCategoryTabs()
    {
        activeTabs.Clear();

        if (categoryTabs != null)
        {
            foreach (string tab in categoryTabs)
            {
                string trimmed = tab.Trim();
                if (trimmed.Length > 0 && !activeTabs.Contains(trimmed))
                    activeTabs.Add(trimmed);
            }
        }

        if (items != null)
        {
            foreach (FurnitureItem item in items)
            {
                string category = GetCategory(item);
                if (!activeTabs.Contains(category))
                    activeTabs.Add(category);
            }
        }

        if (activeTabs.Count == 0)
            activeTabs.Add(DefaultCategory);

        if (string.IsNullOrEmpty(selectedTab) || !activeTabs.Contains(selectedTab))
            selectedTab = activeTabs[0];
    }

    void BuildTabs()
    {
        if (activeTabs.Count == 0)
            return;

        ScrollRect scrollRect = GetComponentInParent<ScrollRect>();
        if (scrollRect == null)
            return;

        if (tabBar != null)
            Destroy(tabBar);

        tabBar = new GameObject("Catalog Tabs", typeof(RectTransform), typeof(Image));
        tabBar.transform.SetParent(scrollRect.transform, false);

        RectTransform barRt = tabBar.GetComponent<RectTransform>();
        barRt.anchorMin = new Vector2(0f, 1f);
        barRt.anchorMax = new Vector2(1f, 1f);
        barRt.pivot = new Vector2(0.5f, 1f);
        barRt.anchoredPosition = new Vector2(0f, -TabBarMargin);
        barRt.sizeDelta = new Vector2(0f, TabHeight);

        Image barImage = tabBar.GetComponent<Image>();
        barImage.color = new Color(0.1f, 0.1f, 0.1f, 0.5f);
        barImage.raycastTarget = false;

        HorizontalLayoutGroup layout = tabBar.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(6, 6, 6, 6);
        layout.spacing = 6f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlHeight = true;
        layout.childControlWidth = false;
        layout.childForceExpandHeight = true;
        layout.childForceExpandWidth = false;

        tabButtons.Clear();
        foreach (string category in activeTabs)
        {
            Button tabButton = CreateTabButton(category, tabBar.transform);
            tabButtons.Add(tabButton);
            string captured = category;
            tabButton.onClick.AddListener(() => SelectCategory(captured));
        }

        RefreshTabHighlight();

        Button hideButton = PanelCollapse.CreateButton("Hide Panel", tabBar.transform, "\u2014", 34f, 32f);
        RectTransform scrollRt = scrollRect.GetComponent<RectTransform>();
        RectTransform canvasRt = scrollRect.transform.parent as RectTransform;
        if (canvasRt == null)
            canvasRt = scrollRect.transform as RectTransform;
        RectTransform capturedRt = scrollRt;
        RectTransform capturedCanvas = canvasRt;
        hideButton.onClick.AddListener(() => PanelCollapse.Collapse(capturedRt, capturedCanvas, "Catalog"));

        GridLayoutGroup grid = GetComponent<GridLayoutGroup>();
        if (grid != null)
        {
            RectOffset padding = grid.padding;
            int minTop = Mathf.RoundToInt(TabHeight + TabBarMargin + 8f);
            if (padding.top < minTop)
            {
                padding.top = minTop;
                grid.padding = padding;
            }
        }
    }

    Button CreateTabButton(string category, Transform parent)
    {
        GameObject go = new GameObject(category, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);

        LayoutElement layout = go.GetComponent<LayoutElement>();
        layout.minWidth = 90f;
        layout.preferredWidth = 110f;

        Image image = go.GetComponent<Image>();
        image.color = TabNormalColor;
        image.raycastTarget = true;

        Button button = go.GetComponent<Button>();
        button.targetGraphic = image;

        CreateTabLabel(go.transform, category);

        return button;
    }

    void CreateTabLabel(Transform parent, string text)
    {
        GameObject go = new GameObject("Label", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.font = CatalogUI.GetDefaultFont();
        tmp.fontSize = 18f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        tmp.text = text;
    }

    void RefreshTabHighlight()
    {
        for (int i = 0; i < tabButtons.Count && i < activeTabs.Count; i++)
        {
            Image image = tabButtons[i].GetComponent<Image>();
            if (image != null)
                image.color = string.Equals(activeTabs[i], selectedTab, StringComparison.OrdinalIgnoreCase)
                    ? TabActiveColor
                    : TabNormalColor;
        }
    }

    void SelectCategory(string category)
    {
        if (string.Equals(selectedTab, category, StringComparison.OrdinalIgnoreCase))
            return;

        selectedTab = category;
        RefreshTabHighlight();
        BuildCatalog();

        ScrollRect scrollRect = GetComponentInParent<ScrollRect>();
        if (scrollRect != null)
            scrollRect.verticalNormalizedPosition = 1f;
    }

    void OnDestroy()
    {
        if (tabBar != null)
            Destroy(tabBar);
    }

    void ConfigureButton(Button button, FurnitureItem item)
    {
        string labelText = item.displayName;
        if (string.IsNullOrEmpty(labelText))
            labelText = item.prefab != null ? item.prefab.name : "Item";

        string priceText = FormatPrice(item.price);
        if (!string.IsNullOrEmpty(priceText))
            labelText += "\n" + priceText;

        TMP_Text tmp = button.GetComponentInChildren<TMP_Text>();
        if (tmp != null)
        {
            tmp.text = labelText;
        }
        else
        {
            Text legacy = button.GetComponentInChildren<Text>();
            if (legacy != null)
                legacy.text = labelText;
        }

        Image icon = null;
        foreach (Transform child in button.transform)
        {
            if (child.name == "Image")
            {
                icon = child.GetComponent<Image>();
                break;
            }
        }

        if (icon != null)
        {
            if (item.icon != null)
            {
                icon.sprite = item.icon;
                icon.color = Color.white;
            }
            else
            {
                icon.sprite = null;
                icon.color = item.color;
            }
        }
        else if (item.icon == null)
        {
            Image background = button.targetGraphic as Image;
            if (background != null)
                background.color = item.color;
        }
    }

    void SpawnItem(FurnitureItem item)
    {
        if (item == null || item.prefab == null)
        {
            Debug.LogWarning("FurnitureCatalog: Item has no prefab assigned.", this);
            return;
        }

        Vector3 spawnPos = GetSpawnPosition();

        if (snapToGrid && RoomManager.Instance != null)
            spawnPos = RoomManager.Instance.SnapToGrid(spawnPos);

        GameObject instance = Instantiate(item.prefab, spawnPos, Quaternion.identity);
        instance.name = item.prefab.name;
        instance.transform.localScale *= spawnScale;

        if (snapToGround)
            SnapToGround(instance);

        DragObject drag = instance.GetComponent<DragObject>();
        if (drag == null)
            drag = instance.AddComponent<DragObject>();
        drag.snapToGrid = snapToGrid;

        PlacedItem placed = instance.GetComponent<PlacedItem>();
        if (placed == null)
            placed = instance.AddComponent<PlacedItem>();
        placed.itemName = item.displayName;
        placed.price = item.price;
    }

    void SnapToGround(GameObject instance)
    {
        Collider[] colliders = instance.GetComponentsInChildren<Collider>();
        if (colliders.Length == 0)
            return;

        float minY = float.MaxValue;
        foreach (Collider collider in colliders)
        {
            if (!collider.enabled)
                continue;
            minY = Mathf.Min(minY, collider.bounds.min.y);
        }

        if (minY < float.MaxValue)
        {
            Vector3 pos = instance.transform.position;
            instance.transform.position = new Vector3(pos.x, pos.y - minY, pos.z);
        }
    }

    string FormatPrice(float price)
    {
        if (price <= 0f)
            return string.Empty;

        string symbol = "$";
        if (FurnitureTotalTracker.Instance != null)
            symbol = FurnitureTotalTracker.Instance.currencySymbol;

        return symbol + price.ToString("0.00");
    }

    Vector3 GetSpawnPosition()
    {
        if (spawnMode == SpawnMode.MousePosition)
        {
            Vector2 mousePos = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
            if (mainCamera != null)
            {
                Ray ray = mainCamera.ScreenPointToRay(mousePos);
                Plane plane = new Plane(Vector3.up, Vector3.zero);
                if (plane.Raycast(ray, out float distance))
                    return ray.GetPoint(distance);
            }
        }

        if (spawnMode == SpawnMode.OutsideRoom)
            return GetOutsideRoomSpawn();

        Vector3 center = GetRoomCenter();

        float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
        float radius = UnityEngine.Random.Range(minDistanceFromCenter, maxDistanceFromCenter);
        return center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
    }

    Vector3 GetOutsideRoomSpawn()
    {
        Room room = RoomManager.Instance != null
            ? RoomManager.Instance.MainRoom
            : null;

        float width = room != null ? room.width : 6f;
        float length = room != null ? room.length : 8f;
        Vector3 roomPos = room != null ? room.transform.position : Vector3.zero;

        Vector3 center = roomPos + new Vector3(width / 2f, 0f, length / 2f);

        int perRow = Mathf.Max(1, Mathf.FloorToInt(length / stagingSpacing));
        int row = stagingIndex / perRow;
        int col = stagingIndex % perRow;
        stagingIndex++;

        float x;
        float z;
        if (stagingSide == StagingSide.North)
        {
            x = center.x - width / 2f + stagingSpacing * 0.5f + col * stagingSpacing;
            z = center.z + length / 2f + stagingMargin + row * stagingSpacing;
        }
        else
        {
            x = center.x + width / 2f + stagingMargin + row * stagingSpacing;
            z = center.z - length / 2f + stagingSpacing * 0.5f + col * stagingSpacing;
        }

        return new Vector3(x, 0f, z);
    }

    Vector3 GetRoomCenter()
    {
        Vector3 center = new Vector3(3f, 0f, 4f);

        if (RoomManager.Instance != null)
        {
            Room room = RoomManager.Instance.MainRoom;
            if (room != null)
                center = room.transform.position + new Vector3(room.width / 2f, 0f, room.length / 2f);
        }

        return center;
    }
}
