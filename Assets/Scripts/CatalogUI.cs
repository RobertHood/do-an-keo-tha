using TMPro;
using UnityEngine;

public static class CatalogUI
{
    public const float CartPanelMaxHeight = 420f;
    public const float PlotPanelMaxHeight = 310f;

    private const float RightPanelMargin = 16f;
    private const float RightPanelGap = 8f;

    public static TMP_FontAsset GetDefaultFont()
    {
        TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts & Materials/Roboto SDF");
        if (font == null)
            font = TMP_Settings.defaultFontAsset;
        return font;
    }

    public static TMP_FontAsset GetBoldFont()
    {
        TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts & Materials/Roboto SDF Bold");
        if (font == null)
            font = TMP_Settings.defaultFontAsset;
        return font;
    }

    public static void GetRightPanelHeights(float canvasHeight, float cartMaxHeight, float plotMaxHeight, out float cartHeight, out float plotHeight)
    {
        float available = Mathf.Max(0f, canvasHeight - RightPanelMargin * 2f - RightPanelGap);
        float desiredCart = Mathf.Clamp(available * 0.6f, 160f, cartMaxHeight);
        float desiredPlot = Mathf.Clamp(available * 0.4f, 120f, plotMaxHeight);

        if (desiredCart + desiredPlot > available)
        {
            float scale = desiredCart + desiredPlot > 0f ? available / (desiredCart + desiredPlot) : 1f;
            cartHeight = desiredCart * scale;
            plotHeight = desiredPlot * scale;
        }
        else
        {
            cartHeight = desiredCart;
            plotHeight = desiredPlot;
        }
    }
}
