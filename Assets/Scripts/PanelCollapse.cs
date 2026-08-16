using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static class PanelCollapse
{
    public static GameObject CreateHeader(Transform parent, string title, out Button hideButton)
    {
        GameObject header = new GameObject("Header", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        header.transform.SetParent(parent, false);

        HorizontalLayoutGroup layout = header.GetComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(0, 0, 0, 0);
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlHeight = true;
        layout.childControlWidth = false;
        layout.childForceExpandHeight = true;
        layout.childForceExpandWidth = false;

        LayoutElement headerLayout = header.AddComponent<LayoutElement>();
        headerLayout.preferredHeight = 32f;

        TextMeshProUGUI titleText = CreateText("Title", header.transform, title);
        titleText.font = CatalogUI.GetBoldFont();
        titleText.fontSize = 20f;
        titleText.fontStyle = FontStyles.Bold;
        titleText.color = new Color(0.15f, 0.15f, 0.15f, 1f);
        titleText.alignment = TextAlignmentOptions.Center;
        LayoutElement titleLayout = titleText.gameObject.AddComponent<LayoutElement>();
        titleLayout.flexibleWidth = 1f;
        titleLayout.preferredHeight = 32f;

        hideButton = CreateButton("Hide Button", header.transform, "\u2014", 34f, 32f);
        return header;
    }

    public static Button CreateButton(string name, Transform parent, string label, float width, float height)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);

        LayoutElement layout = go.GetComponent<LayoutElement>();
        layout.minWidth = width;
        layout.preferredWidth = width;
        layout.preferredHeight = height;

        Image image = go.GetComponent<Image>();
        image.color = new Color(0.82f, 0.82f, 0.82f, 1f);
        image.raycastTarget = true;

        Button button = go.GetComponent<Button>();
        button.targetGraphic = image;

        TextMeshProUGUI tmp = CreateText("Label", go.transform, label);
        tmp.fontSize = 16f;
        tmp.color = new Color(0.15f, 0.15f, 0.15f, 1f);
        tmp.alignment = TextAlignmentOptions.Center;

        RectTransform rt = tmp.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        return button;
    }

    public static void Collapse(RectTransform panelRt, RectTransform canvasRt, string panelTitle)
    {
        if (panelRt == null || canvasRt == null)
            return;

        panelRt.gameObject.SetActive(false);
        CreateRestoreButton(panelRt, canvasRt, panelTitle);
    }

    static void CreateRestoreButton(RectTransform panelRt, RectTransform canvasRt, string panelTitle)
    {
        GameObject go = new GameObject(panelTitle + " Restore Button", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(canvasRt, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = panelRt.anchorMin;
        rt.anchorMax = panelRt.anchorMax;
        rt.pivot = panelRt.pivot;
        rt.anchoredPosition = panelRt.anchoredPosition;
        rt.sizeDelta = new Vector2(150f, 44f);

        Image image = go.GetComponent<Image>();
        image.color = new Color(0.15f, 0.15f, 0.15f, 0.8f);
        image.raycastTarget = true;

        Button button = go.GetComponent<Button>();
        button.targetGraphic = image;

        TextMeshProUGUI tmp = CreateText("Label", go.transform, panelTitle + " \u25B8");
        tmp.font = CatalogUI.GetBoldFont();
        tmp.fontSize = 18f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;

        RectTransform labelRt = tmp.GetComponent<RectTransform>();
        labelRt.anchorMin = Vector2.zero;
        labelRt.anchorMax = Vector2.one;
        labelRt.offsetMin = Vector2.zero;
        labelRt.offsetMax = Vector2.zero;

        GameObject captured = go;
        button.onClick.AddListener(() =>
        {
            if (panelRt != null)
                panelRt.gameObject.SetActive(true);
            if (captured != null)
                UnityEngine.Object.Destroy(captured);
        });
    }

    public static TextMeshProUGUI CreateText(string name, Transform parent, string content = "")
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.font = CatalogUI.GetDefaultFont();
        tmp.fontSize = 16f;
        tmp.text = content;
        tmp.raycastTarget = false;
        return tmp;
    }
}
