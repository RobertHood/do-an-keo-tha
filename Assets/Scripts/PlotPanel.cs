using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlotPanel : MonoBehaviour
{
    public string panelTitle = "Room Settings";
    public float panelWidth = 280f;
    public float panelHeight = 310f;

    private RectTransform panelRt;
    private RectTransform canvasRt;
    private TMP_InputField lengthInput;
    private TMP_InputField widthInput;
    private TextMeshProUGUI statusText;
    private RoomManager.PlotShape selectedShape = RoomManager.PlotShape.Rectangle;
    private Button[] shapeButtons;

    private static readonly Color ShapeNormalColor = new Color(0.82f, 0.82f, 0.82f, 1f);
    private static readonly Color ShapeActiveColor = new Color(0.35f, 0.60f, 0.90f, 1f);

    public static PlotPanel EnsureInstance()
    {
        PlotPanel panel = FindAnyObjectByType<PlotPanel>();
        if (panel != null)
            return panel;

        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null)
            return null;

        panel = canvas.gameObject.AddComponent<PlotPanel>();
        panel.BuildUI();
        panel.SyncDefaults();
        return panel;
    }

    void Start()
    {
        RoomManager.EnsureInstance();
        BuildUI();
        SyncDefaults();
    }

    public void BuildUI()
    {
        ClearBuilt();

        canvasRt = transform as RectTransform;
        if (canvasRt == null)
        {
            Debug.LogError("PlotPanel: attach this component to the Canvas.", this);
            return;
        }

        GameObject panel = Create("Room Panel", transform);
        panelRt = panel.GetComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(1f, 0f);
        panelRt.anchorMax = new Vector2(1f, 0f);
        panelRt.pivot = new Vector2(1f, 0f);
        panelRt.anchoredPosition = new Vector2(-16f, 16f);
        float cartHeight;
        float fitHeight;
        CatalogUI.GetRightPanelHeights(canvasRt.rect.height, CatalogUI.CartPanelMaxHeight, panelHeight, out cartHeight, out fitHeight);
        panelRt.sizeDelta = new Vector2(panelWidth, fitHeight);

        Image panelBg = panel.AddComponent<Image>();
        panelBg.color = new Color(1f, 1f, 1f, 0.92f);

        VerticalLayoutGroup layout = panel.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(12, 12, 12, 12);
        layout.spacing = 8f;
        layout.childControlHeight = true;
        layout.childForceExpandHeight = false;

        Button hideButton;
        PanelCollapse.CreateHeader(panel.transform, panelTitle, out hideButton);
        hideButton.onClick.AddListener(() => PanelCollapse.Collapse(panelRt, canvasRt, panelTitle));

        lengthInput = CreateInputRow(panel.transform, "Length");
        widthInput = CreateInputRow(panel.transform, "Width");

        CreateShapeRow(panel.transform);

        CreateApplyButton(panel.transform);

        statusText = PanelCollapse.CreateText("Status", panel.transform);
        statusText.fontSize = 15f;
        statusText.color = new Color(0.3f, 0.3f, 0.3f, 1f);
        statusText.alignment = TextAlignmentOptions.Center;
        LayoutElement statusLayout = statusText.gameObject.AddComponent<LayoutElement>();
        statusLayout.preferredHeight = 22f;
    }

    public void SyncDefaults()
    {
        RoomManager rm = RoomManager.Instance;
        if (rm == null)
            return;

        Room room = rm.MainRoom;
        float length = room != null ? room.length : rm.defaultRoomLength;
        float width = room != null ? room.width : rm.defaultRoomWidth;
        selectedShape = rm.plotShape;

        if (lengthInput != null)
            lengthInput.text = length.ToString("0.##");
        if (widthInput != null)
            widthInput.text = width.ToString("0.##");

        RefreshShapeHighlight();
        UpdateStatus();
    }

    TMP_InputField CreateInputRow(Transform parent, string label)
    {
        GameObject row = Create("Row " + label, parent);
        HorizontalLayoutGroup rowLayout = row.AddComponent<HorizontalLayoutGroup>();
        rowLayout.padding = new RectOffset(0, 0, 0, 0);
        rowLayout.spacing = 8f;
        rowLayout.childControlHeight = true;
        rowLayout.childForceExpandHeight = true;
        rowLayout.childControlWidth = false;
        rowLayout.childForceExpandWidth = false;

        TextMeshProUGUI labelText = PanelCollapse.CreateText("Label", row.transform, label);
        labelText.fontSize = 17f;
        labelText.color = new Color(0.2f, 0.2f, 0.2f, 1f);
        labelText.alignment = TextAlignmentOptions.MidlineLeft;
        LayoutElement labelLayout = labelText.gameObject.AddComponent<LayoutElement>();
        labelLayout.preferredWidth = 76f;
        labelLayout.preferredHeight = 32f;

        return CreateInputField(row.transform);
    }

    TMP_InputField CreateInputField(Transform parent)
    {
        GameObject go = Create("Input Field", parent);
        Image bg = go.AddComponent<Image>();
        bg.color = new Color(1f, 1f, 1f, 1f);

        TMP_InputField input = go.AddComponent<TMP_InputField>();

        LayoutElement layout = go.AddComponent<LayoutElement>();
        layout.minWidth = 120f;
        layout.flexibleWidth = 1f;
        layout.preferredHeight = 32f;

        GameObject textArea = Create("Text Area", go.transform);
        RectTransform taRt = textArea.GetComponent<RectTransform>();
        taRt.anchorMin = Vector2.zero;
        taRt.anchorMax = Vector2.one;
        taRt.offsetMin = new Vector2(8f, 4f);
        taRt.offsetMax = new Vector2(-8f, -4f);
        textArea.AddComponent<RectMask2D>();

        TextMeshProUGUI placeholder = PanelCollapse.CreateText("Placeholder", textArea.transform, "0");
        placeholder.fontSize = 16f;
        placeholder.color = new Color(0.5f, 0.5f, 0.5f, 1f);
        placeholder.alignment = TextAlignmentOptions.MidlineLeft;
        Stretch(placeholder.GetComponent<RectTransform>());

        TextMeshProUGUI text = PanelCollapse.CreateText("Text", textArea.transform);
        text.fontSize = 16f;
        text.color = new Color(0.15f, 0.15f, 0.15f, 1f);
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.raycastTarget = true;
        Stretch(text.GetComponent<RectTransform>());

        input.textComponent = text;
        input.placeholder = placeholder;
        input.textViewport = taRt;
        input.contentType = TMP_InputField.ContentType.DecimalNumber;
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.fontAsset = CatalogUI.GetDefaultFont();
        input.text = "6";

        return input;
    }

    void CreateShapeRow(Transform parent)
    {
        GameObject row = Create("Shape Row", parent);
        HorizontalLayoutGroup rowLayout = row.AddComponent<HorizontalLayoutGroup>();
        rowLayout.padding = new RectOffset(0, 0, 0, 0);
        rowLayout.spacing = 4f;
        rowLayout.childControlHeight = true;
        rowLayout.childForceExpandHeight = true;
        rowLayout.childControlWidth = false;
        rowLayout.childForceExpandWidth = false;

        Array shapes = Enum.GetValues(typeof(RoomManager.PlotShape));
        shapeButtons = new Button[shapes.Length];

        for (int i = 0; i < shapes.Length; i++)
        {
            RoomManager.PlotShape shape = (RoomManager.PlotShape)shapes.GetValue(i);
            Button button = PanelCollapse.CreateButton(shape.ToString(), row.transform, shape.ToString(), 70f, 32f);
            LayoutElement buttonLayout = button.GetComponent<LayoutElement>();
            buttonLayout.flexibleWidth = 1f;
            shapeButtons[i] = button;

            RoomManager.PlotShape captured = shape;
            button.onClick.AddListener(() => SelectShape(captured));
        }

        RefreshShapeHighlight();
    }

    void CreateApplyButton(Transform parent)
    {
        Button apply = PanelCollapse.CreateButton("Apply Button", parent, "Apply Room", 0f, 36f);
        LayoutElement layout = apply.GetComponent<LayoutElement>();
        layout.flexibleWidth = 1f;
        apply.onClick.AddListener(ApplyPlot);

        Image image = apply.GetComponent<Image>();
        image.color = ShapeActiveColor;
        TextMeshProUGUI tmp = apply.GetComponentInChildren<TextMeshProUGUI>();
        if (tmp != null)
            tmp.color = Color.white;
    }

    void ApplyPlot()
    {
        RoomManager rm = RoomManager.EnsureInstance();
        float length = ParseInput(lengthInput, rm.defaultRoomLength);
        float width = ParseInput(widthInput, rm.defaultRoomWidth);
        rm.SetPlot(length, width, selectedShape);
        SyncDefaults();
    }

    static float ParseInput(TMP_InputField field, float fallback)
    {
        if (field != null && float.TryParse(field.text, out float value))
            return Mathf.Clamp(value, 2f, 50f);
        return fallback;
    }

    void SelectShape(RoomManager.PlotShape shape)
    {
        selectedShape = shape;
        RefreshShapeHighlight();
    }

    void RefreshShapeHighlight()
    {
        if (shapeButtons == null)
            return;

        Array shapes = Enum.GetValues(typeof(RoomManager.PlotShape));
        for (int i = 0; i < shapeButtons.Length && i < shapes.Length; i++)
        {
            Button button = shapeButtons[i];
            if (button == null)
                continue;

            RoomManager.PlotShape shape = (RoomManager.PlotShape)shapes.GetValue(i);
            Image image = button.GetComponent<Image>();
            if (image != null)
                image.color = shape == selectedShape ? ShapeActiveColor : ShapeNormalColor;
        }
    }

    void UpdateStatus()
    {
        if (statusText == null)
            return;

        RoomManager rm = RoomManager.Instance;
        if (rm != null && rm.MainRoom != null)
            statusText.text = $"Room: {rm.MainRoom.length:0.#} x {rm.MainRoom.width:0.#}  ({rm.plotShape})";
        else
            statusText.text = "Room not set";
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
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
        lengthInput = null;
        widthInput = null;
        statusText = null;
        shapeButtons = null;
    }

    GameObject Create(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }
}
