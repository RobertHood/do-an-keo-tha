using TMPro;
using UnityEngine;

public static class CatalogUI
{
    public static TMP_FontAsset GetDefaultFont()
    {
        TMP_FontAsset font = TMP_Settings.defaultFontAsset;
        if (font == null)
            font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        return font;
    }
}
