using UnityEngine;

/// <summary>
/// The web app's colors (Suburban-House-Design-Simulation-FE: tailwind.config / variables.css), so the Unity UI
/// matches the website. Used by the UI theme tool and the runtime UI scripts.
/// </summary>
public static class UIPalette
{
    public static readonly Color White = Hex("#ffffff");
    public static readonly Color BrandBlack = Hex("#100101");
    public static readonly Color BrandGreen = Hex("#1b633e");
    public static readonly Color BrandGreenLight = Hex("#eaf5ee");
    public static readonly Color BrandBlue = Hex("#0969da");
    public static readonly Color BrandBlueLight = Hex("#ebf5ff");

    public static readonly Color Neutral50 = Hex("#fbfbfc");
    public static readonly Color Neutral100 = Hex("#f4f4f5");
    public static readonly Color Neutral150 = Hex("#eeeeef");
    public static readonly Color Neutral200 = Hex("#e4e4e7");
    public static readonly Color Neutral300 = Hex("#d4d4d8");
    public static readonly Color Neutral400 = Hex("#a1a1aa");
    public static readonly Color Neutral500 = Hex("#71717a");
    public static readonly Color Neutral600 = Hex("#52525b");
    public static readonly Color Neutral700 = Hex("#3f3f46");
    public static readonly Color Neutral800 = Hex("#27272a");

    public static readonly Color Red50 = Hex("#fef2f2");
    public static readonly Color Red600 = Hex("#dc2626");
    public static readonly Color Amber100 = Hex("#fef3c7");
    public static readonly Color Amber700 = Hex("#b45309");

    public static readonly Color Clear = new Color(1f, 1f, 1f, 0f);

    public static Color WithAlpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }

    public static Color Hex(string hex)
    {
        return ColorUtility.TryParseHtmlString(hex, out Color color) ? color : Color.magenta;
    }

    /// <summary>
    /// Vietnamese price format used on the website, e.g. 2000 -> "2.000 ₫".
    /// </summary>
    public static string FormatVnd(double amount)
    {
        return amount.ToString("N0", System.Globalization.CultureInfo.InvariantCulture).Replace(",", ".") + " ₫";
    }
}
