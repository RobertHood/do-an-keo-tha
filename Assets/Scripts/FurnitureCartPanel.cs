using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class FurnitureCartPanel : MonoBehaviour
{
    public string panelTitle = "Used Assets";
    public float panelWidth = 260f;
    public float panelMaxHeight = 420f;
    public string emptyText = "Nothing placed yet";

    private RectTransform panelRt;
    private RectTransform listRt;
    private TextMeshProUGUI totalText;
    private GameObject rowTemplate;
    private bool subscribed;

    void OnEnable()
    {
        Subscribe();
    }

    void OnDisable()
    {
        Unsubscribe();
    }

    void Start()
    {
        if (FurnitureTotalTracker.Instance == null)
        {
            GameObject trackerGo = new GameObject("FurnitureTotalTracker");
            trackerGo.AddComponent<FurnitureTotalTracker>();
        }

        Subscribe();
        BuildUI();
        Refresh();
    }

    void Subscribe()
    {
        if (subscribed || FurnitureTotalTracker.Instance == null)
            return;

        FurnitureTotalTracker.Instance.ItemsChanged += Refresh;
        subscribed = true;
    }

    void Unsubscribe()
    {
        if (!subscribed || FurnitureTotalTracker.Instance == null)
            return;

        FurnitureTotalTracker.Instance.ItemsChanged -= Refresh;
        subscribed = false;
    }

    public void BuildUI()
    {
        ClearBuilt();

        RectTransform canvasRt = transform as RectTransform;
        float canvasHeight = canvasRt != null ? canvasRt.rect.height : 600f;
        float panelHeight = Mathf.Min(panelMaxHeight, canvasHeight - 40f);

        GameObject panel = Create("Cart Panel", transform);
        panelRt = panel.GetComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(1f, 0.5f);
        panelRt.anchorMax = new Vector2(1f, 0.5f);
        panelRt.pivot = new Vector2(1f, 0.5f);
        panelRt.anchoredPosition = new Vector2(-16f, 0f);
        panelRt.sizeDelta = new Vector2(panelWidth, panelHeight);

        Image panelBg = panel.AddComponent<Image>();
        panelBg.color = new Color(1f, 1f, 1f, 0.92f);

        VerticalLayoutGroup panelLayout = panel.AddComponent<VerticalLayoutGroup>();
        panelLayout.padding = new RectOffset(12, 12, 12, 12);
        panelLayout.spacing = 8f;
        panelLayout.childControlHeight = true;
        panelLayout.childForceExpandHeight = false;

        TextMeshProUGUI title = CreateText("Title", panel.transform, panelTitle);
        title.fontSize = 20f;
        title.fontStyle = FontStyles.Bold;
        title.color = new Color(0.15f, 0.15f, 0.15f, 1f);
        title.alignment = TextAlignmentOptions.Center;
        LayoutElement titleLayout = title.gameObject.AddComponent<LayoutElement>();
        titleLayout.preferredHeight = 32f;

        GameObject viewport = Create("Item List", panel.transform);
        RectTransform viewportRt = viewport.GetComponent<RectTransform>();
        Image viewportImage = viewport.AddComponent<Image>();
        viewportImage.color = new Color(0.95f, 0.95f, 0.95f, 0.6f);
        viewport.AddComponent<RectMask2D>();
        ScrollRect scroll = viewport.AddComponent<ScrollRect>();
        LayoutElement viewportLayout = viewport.AddComponent<LayoutElement>();
        viewportLayout.flexibleHeight = 1f;

        GameObject content = Create("List Content", viewport.transform);
        RectTransform contentRt = content.GetComponent<RectTransform>();
        contentRt.anchorMin = new Vector2(0f, 1f);
        contentRt.anchorMax = new Vector2(1f, 1f);
        contentRt.pivot = new Vector2(0.5f, 1f);
        contentRt.sizeDelta = new Vector2(0f, 0f);

        VerticalLayoutGroup contentLayout = content.AddComponent<VerticalLayoutGroup>();
        contentLayout.padding = new RectOffset(4, 4, 4, 4);
        contentLayout.spacing = 6f;
        contentLayout.childControlHeight = true;
        contentLayout.childForceExpandHeight = false;

        ContentSizeFitter fitter = content.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.viewport = viewportRt;
        scroll.content = contentRt;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;

        listRt = contentRt;

        rowTemplate = Create("Row Template", content.transform);
        Image rowBg = rowTemplate.AddComponent<Image>();
        rowBg.color = new Color(0.2f, 0.2f, 0.2f, 0.08f);
        LayoutElement rowLayout = rowTemplate.AddComponent<LayoutElement>();
        rowLayout.preferredHeight = 26f;

        TextMeshProUGUI rowText = CreateText("Row Text", rowTemplate.transform);
        rowText.fontSize = 15f;
        rowText.color = new Color(0.2f, 0.2f, 0.2f, 1f);
        rowText.alignment = TextAlignmentOptions.Center;
        RectTransform rowTextRt = rowText.GetComponent<RectTransform>();
        rowTextRt.anchorMin = Vector2.zero;
        rowTextRt.anchorMax = Vector2.one;
        rowTextRt.offsetMin = new Vector2(6f, 0f);
        rowTextRt.offsetMax = new Vector2(-6f, 0f);
        rowTemplate.SetActive(false);

        totalText = CreateText("Total", panel.transform);
        totalText.fontSize = 18f;
        totalText.fontStyle = FontStyles.Bold;
        totalText.color = new Color(0.1f, 0.1f, 0.1f, 1f);
        totalText.alignment = TextAlignmentOptions.Center;
        LayoutElement totalLayout = totalText.gameObject.AddComponent<LayoutElement>();
        totalLayout.preferredHeight = 28f;
    }

    public void Refresh()
    {
        if (listRt == null || totalText == null)
            return;

        FurnitureTotalTracker tracker = FurnitureTotalTracker.Instance;

        for (int i = listRt.childCount - 1; i >= 0; i--)
        {
            Transform child = listRt.GetChild(i);
            if (child == rowTemplate.transform)
                continue;
            Destroy(child.gameObject);
        }

        if (tracker != null && tracker.PlacedCount > 0)
        {
            foreach (PlacedItem item in tracker.PlacedItems)
            {
                string name = string.IsNullOrEmpty(item.itemName) ? item.name : item.itemName;
                string price = tracker.currencySymbol + item.price.ToString("0.00");
                CreateRow(name, price);
            }
        }
        else
        {
            CreateRow(emptyText, string.Empty);
        }

        string prefix = tracker != null ? tracker.labelPrefix : "Total: ";
        string symbol = tracker != null ? tracker.currencySymbol : "$";
        float total = tracker != null ? tracker.TotalPrice : 0f;
        totalText.text = $"{prefix}{symbol}{total.ToString("0.00")}";
    }

    void CreateRow(string name, string price)
    {
        if (rowTemplate == null)
            return;

        GameObject row = Instantiate(rowTemplate, listRt);
        row.SetActive(true);

        TextMeshProUGUI text = row.GetComponentInChildren<TextMeshProUGUI>();
        if (text != null)
        {
            if (string.IsNullOrEmpty(price))
                text.text = name;
            else
                text.text = $"<align=left>{name}</align><align=right>{price}</align>";
        }
    }

    void ClearBuilt()
    {
        if (panelRt == null)
            return;

        if (Application.isPlaying)
            Destroy(panelRt.gameObject);
        else
            DestroyImmediate(panelRt.gameObject);

        panelRt = null;
        listRt = null;
        totalText = null;
        rowTemplate = null;
    }

    GameObject Create(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    TextMeshProUGUI CreateText(string name, Transform parent, string content = "")
    {
        GameObject go = Create(name, parent);
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        TMP_FontAsset font = TMP_Settings.defaultFontAsset;
        if (font == null)
            font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        tmp.font = font;
        tmp.fontSize = 16f;
        tmp.text = content;
        tmp.raycastTarget = false;
        return tmp;
    }
}
