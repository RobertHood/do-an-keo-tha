using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Builds UI elements in code, styled like the rest of the app (UIKit sprites and fonts, UIPalette colors).
/// Used by the land panel, the compass and the labels shown in the 3D view.
/// </summary>
public static class UIBuild
{
    public static RectTransform Rect(Transform parent, string name)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = 5; // UI
        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        return rect;
    }

    /// <summary>
    /// Sets anchors, pivot, position and size in one call.
    /// </summary>
    public static RectTransform Place(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    /// <summary>
    /// Fills the parent, inset by the given margins.
    /// </summary>
    public static RectTransform Stretch(RectTransform rect, float left = 0f, float right = 0f, float top = 0f, float bottom = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
        return rect;
    }

    public static Image Image(Transform parent, string name, Sprite sprite, Color color, bool raycast = false)
    {
        Image image = Rect(parent, name).gameObject.AddComponent<Image>();
        Style(image, sprite, color, raycast);
        return image;
    }

    public static void Style(Image image, Sprite sprite, Color color, bool raycast = false)
    {
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = raycast;
        image.type = sprite != null && sprite.border.sqrMagnitude > 0f ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
        image.pixelsPerUnitMultiplier = 1f;
    }

    public enum Weight { Regular, SemiBold, Bold }

    public static TextMeshProUGUI Text(Transform parent, string name, string text, float size, Color color,
                                       Weight weight = Weight.Regular, TextAlignmentOptions alignment = TextAlignmentOptions.Left)
    {
        TextMeshProUGUI tmp = Rect(parent, name).gameObject.AddComponent<TextMeshProUGUI>();
        tmp.font = Font(weight) ?? tmp.font;
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = alignment;
        tmp.raycastTarget = false;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Overflow;
        return tmp;
    }

    public static TMP_FontAsset Font(Weight weight)
    {
        UIKit kit = UIKit.Get();
        if (kit == null) return null;
        switch (weight)
        {
            case Weight.Bold: return kit.bold != null ? kit.bold : kit.regular;
            case Weight.SemiBold: return kit.semiBold != null ? kit.semiBold : kit.regular;
            default: return kit.regular;
        }
    }

    /// <summary>
    /// A button with a background image (no color transition: the caller styles states).
    /// </summary>
    public static Button Button(Transform parent, string name, Sprite sprite, Color color, Action onClick)
    {
        Image background = Image(parent, name, sprite, color, true);
        Button button = background.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = background;
        Navigation navigation = button.navigation;
        navigation.mode = Navigation.Mode.None;
        button.navigation = navigation;
        if (onClick != null) button.onClick.AddListener(() => onClick());
        return button;
    }

    /// <summary>
    /// Numeric text field: rounded box, right-aligned number and a unit suffix ("m", "°").
    /// </summary>
    public static TMP_InputField NumberField(Transform parent, string name, string suffix, Action<string> onEndEdit)
    {
        UIKit kit = UIKit.Get();
        RectTransform root = Rect(parent, name);
        root.gameObject.SetActive(false); // Wire everything up before TMP_InputField.OnEnable runs

        Image background = root.gameObject.AddComponent<Image>();
        Style(background, kit != null ? kit.round8 : null, UIPalette.White, true);
        Image outline = Image(root, "_Outline", kit != null ? kit.outline12 : null, UIPalette.Neutral300);
        Stretch(outline.rectTransform);

        float suffixWidth = string.IsNullOrEmpty(suffix) ? 0f : 22f;
        RectTransform area = Stretch(Rect(root, "Text Area"), 10f, 8f + suffixWidth, 4f, 4f);
        area.gameObject.AddComponent<RectMask2D>();

        TextMeshProUGUI placeholder = Text(area, "Placeholder", "0", 15f, UIPalette.Neutral400, Weight.Regular, TextAlignmentOptions.MidlineRight);
        Stretch(placeholder.rectTransform);
        TextMeshProUGUI text = Text(area, "Text", "", 15f, UIPalette.BrandBlack, Weight.SemiBold, TextAlignmentOptions.MidlineRight);
        Stretch(text.rectTransform);

        if (suffixWidth > 0f)
        {
            TextMeshProUGUI unit = Text(root, "_Suffix", suffix, 13f, UIPalette.Neutral500, Weight.Regular, TextAlignmentOptions.MidlineLeft);
            Place(unit.rectTransform, new Vector2(1f, 0f), Vector2.one, new Vector2(1f, 0.5f), new Vector2(-4f, 0f), new Vector2(suffixWidth, 0f));
        }

        TMP_InputField input = root.gameObject.AddComponent<TMP_InputField>();
        input.targetGraphic = background;
        input.textViewport = area;
        input.textComponent = text;
        input.placeholder = placeholder;
        input.contentType = TMP_InputField.ContentType.DecimalNumber;
        input.lineType = TMP_InputField.LineType.SingleLine;
        input.pointSize = 15f;
        input.caretColor = UIPalette.BrandBlack;
        input.customCaretColor = true;
        input.selectionColor = UIPalette.WithAlpha(UIPalette.BrandGreen, 0.25f);
        input.transition = Selectable.Transition.None;
        input.onFocusSelectAll = true;
        Navigation navigation = input.navigation;
        navigation.mode = Navigation.Mode.None;
        input.navigation = navigation;
        if (onEndEdit != null) input.onEndEdit.AddListener(value => onEndEdit(value));

        // Green outline while typing
        root.gameObject.AddComponent<UIFocusOutline>().Setup(input, outline);

        root.gameObject.SetActive(true);
        return input;
    }

    /// <summary>
    /// Parses a number typed with either "," or "." as the decimal separator.
    /// </summary>
    public static bool TryParseNumber(string text, out float value)
    {
        return float.TryParse((text ?? "").Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    /// <summary>
    /// Vietnamese number format: "12" or "12,5" (at most one decimal).
    /// </summary>
    public static string FormatNumber(float value, int decimals = 1)
    {
        string format = decimals <= 0 ? "0" : "0." + new string('#', decimals);
        return value.ToString(format, CultureInfo.InvariantCulture).Replace('.', ',');
    }

    /// <summary>
    /// "1.250" style thousands separators for areas.
    /// </summary>
    public static string FormatThousands(float value)
    {
        return Mathf.RoundToInt(value).ToString("N0", CultureInfo.InvariantCulture).Replace(",", ".");
    }

    /// <summary>
    /// True while the user is typing into a text field (keyboard shortcuts should be ignored).
    /// </summary>
    public static bool IsTyping()
    {
        GameObject focused = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        return focused != null && focused.TryGetComponent(out TMP_InputField input) && input.isFocused;
    }

    /// <summary>
    /// Root canvas of the app's UI (the scene's "Canvas"), including inactive ones.
    /// </summary>
    public static Canvas FindMainCanvas()
    {
        Canvas best = null;
        foreach (Canvas canvas in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include))
        {
            if (!canvas.isRootCanvas || canvas.renderMode == RenderMode.WorldSpace) continue;
            if (canvas.GetComponent<CanvasScaler>() == null) continue;
            if (best == null || canvas.name == "Canvas") best = canvas;
        }
        return best;
    }

    /// <summary>
    /// An overlay canvas for labels that follow 3D positions (scaled like the main UI).
    /// sortingOrder below 0 keeps it under the app's panels.
    /// </summary>
    public static Canvas OverlayCanvas(string name, int sortingOrder)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = 5;
        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;
        CanvasScaler scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f;
        return canvas;
    }
}

/// <summary>
/// A small pill label pinned to a point in the 3D view (edge lengths, "Mặt tiền", a model's direction).
/// </summary>
public class WorldLabel
{
    private readonly RectTransform rect;
    private readonly TextMeshProUGUI text;
    private readonly Image background;
    private readonly RectTransform canvasRect;

    public WorldLabel(Canvas canvas, string name, Color backgroundColor, Color textColor, float fontSize = 13f)
    {
        canvasRect = (RectTransform)canvas.transform;
        rect = UIBuild.Rect(canvas.transform, name);
        rect.anchorMin = rect.anchorMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);

        HorizontalLayoutGroup layout = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(10, 10, 4, 4);
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        ContentSizeFitter fitter = rect.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // The pill sits on a child that layout ignores: an Image on the layout object itself would report its
        // sprite size as the preferred size and blow the label up.
        UIKit kit = UIKit.Get();
        background = UIBuild.Image(rect, "Background", kit != null ? kit.pill : null, backgroundColor);
        background.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        UIBuild.Stretch(background.rectTransform);

        text = UIBuild.Text(rect, "Text", "", fontSize, textColor, UIBuild.Weight.SemiBold, TextAlignmentOptions.Center);
        rect.gameObject.SetActive(false);
    }

    public void SetText(string value)
    {
        if (text.text != value) text.text = value;
    }

    public void SetColors(Color backgroundColor, Color textColor)
    {
        background.color = backgroundColor;
        text.color = textColor;
    }

    public void Hide()
    {
        if (rect.gameObject.activeSelf) rect.gameObject.SetActive(false);
    }

    /// <summary>
    /// Shows the label over a world position (hidden when the point is behind the camera).
    /// screenOffset is in canvas units (e.g. lift the label above the point).
    /// </summary>
    public void Show(UnityEngine.Camera cam, Vector3 worldPosition, Vector2 screenOffset = default)
    {
        if (cam == null)
        {
            Hide();
            return;
        }

        Vector3 screen = cam.WorldToScreenPoint(worldPosition);
        if (screen.z <= 0f)
        {
            Hide();
            return;
        }

        if (!rect.gameObject.activeSelf) rect.gameObject.SetActive(true);

        float scale = canvasRect.lossyScale.x > 0f ? canvasRect.lossyScale.x : 1f;
        rect.anchoredPosition = new Vector2(screen.x / scale, screen.y / scale) + screenOffset;
    }

    public void Destroy()
    {
        if (rect != null) UnityEngine.Object.Destroy(rect.gameObject);
    }
}
