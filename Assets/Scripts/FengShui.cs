using System;
using UnityEngine;

/// <summary>
/// What a model is, for the phong thủy rules ("loai"). Saved in the PriceCatalog asset and written to blueprints
/// as the Id string, so don't renumber the values.
/// </summary>
public enum FengShuiType
{
    Auto = 0,        // Derived from the catalog tab and the prefab name (see FengShuiTypes.Resolve)
    Nha = 1,
    Cong = 2,
    HangRao = 3,
    Cay = 4,
    ChauCay = 5,
    HoNuoc = 6,
    DaiPhunNuoc = 7,
    BeBoi = 8,
    LoiDi = 9,
    Den = 10,
    Tuong = 11,
    GheBan = 12,
    Khac = 13,
}

public static class FengShuiTypes
{
    /// <summary>
    /// ID used in blueprint JSON and by the phong thủy engine.
    /// </summary>
    public static string Id(FengShuiType type)
    {
        switch (type)
        {
            case FengShuiType.Nha: return "nha";
            case FengShuiType.Cong: return "cong";
            case FengShuiType.HangRao: return "hang_rao";
            case FengShuiType.Cay: return "cay";
            case FengShuiType.ChauCay: return "chau_cay";
            case FengShuiType.HoNuoc: return "ho_nuoc";
            case FengShuiType.DaiPhunNuoc: return "dai_phun_nuoc";
            case FengShuiType.BeBoi: return "be_boi";
            case FengShuiType.LoiDi: return "loi_di";
            case FengShuiType.Den: return "den";
            case FengShuiType.Tuong: return "tuong";
            case FengShuiType.GheBan: return "ghe_ban";
            default: return "khac";
        }
    }

    /// <summary>
    /// The type for an ID from Id() (e.g. from the model database). Empty or unknown IDs give Auto.
    /// </summary>
    public static FengShuiType FromId(string id)
    {
        if (string.IsNullOrEmpty(id)) return FengShuiType.Auto;
        foreach (FengShuiType type in Enum.GetValues(typeof(FengShuiType)))
        {
            if (type != FengShuiType.Auto && Id(type) == id) return type;
        }
        return FengShuiType.Auto;
    }

    public static string Name(FengShuiType type)
    {
        switch (type)
        {
            case FengShuiType.Nha: return "Nhà";
            case FengShuiType.Cong: return "Cổng";
            case FengShuiType.HangRao: return "Hàng rào";
            case FengShuiType.Cay: return "Cây";
            case FengShuiType.ChauCay: return "Chậu cây";
            case FengShuiType.HoNuoc: return "Hồ nước";
            case FengShuiType.DaiPhunNuoc: return "Đài phun nước";
            case FengShuiType.BeBoi: return "Bể bơi";
            case FengShuiType.LoiDi: return "Lối đi";
            case FengShuiType.Den: return "Đèn";
            case FengShuiType.Tuong: return "Tượng";
            case FengShuiType.GheBan: return "Bàn ghế";
            default: return "Khác";
        }
    }

    /// <summary>
    /// True for models whose facing direction matters (a house's main door, a gate, a bench, a statue).
    /// Round things like trees, pots and lamps have no front.
    /// </summary>
    public static bool HasFront(FengShuiType type)
    {
        return type == FengShuiType.Nha || type == FengShuiType.Cong || type == FengShuiType.GheBan || type == FengShuiType.Tuong;
    }

    /// <summary>
    /// The model's type: the catalog value if set, otherwise a guess from its catalog tab and prefab name.
    /// </summary>
    public static FengShuiType Resolve(FengShuiType type, ModelCategory category, string prefabId)
    {
        if (type != FengShuiType.Auto) return type;

        string name = (prefabId ?? "").ToLowerInvariant();
        switch (category)
        {
            case ModelCategory.House: return FengShuiType.Nha;
            case ModelCategory.Plants: return FengShuiType.Cay;
            case ModelCategory.GardenFurniture: return FengShuiType.GheBan;
            case ModelCategory.Lighting: return FengShuiType.Den;
            case ModelCategory.FenceGate: return name.Contains("gate") ? FengShuiType.Cong : FengShuiType.HangRao;
            case ModelCategory.Decor:
                return name.Contains("statue") ? FengShuiType.Tuong
                     : name.Contains("pot") || name.Contains("planter") ? FengShuiType.ChauCay
                     : FengShuiType.Khac;
            case ModelCategory.PathWater:
                return name.Contains("pool") ? FengShuiType.BeBoi
                     : name.Contains("fountain") ? FengShuiType.DaiPhunNuoc
                     : name.Contains("pond") ? FengShuiType.HoNuoc
                     : FengShuiType.LoiDi;
            default:
                return name.Contains("tree") ? FengShuiType.Cay : FengShuiType.Khac;
        }
    }
}

/// <summary>
/// Compass bearings for the scene. Fixed convention: Unity +Z is North and +X is East, so a bearing is the
/// clockwise angle from +Z seen from above (0 = Bắc, 90 = Đông, 180 = Nam, 270 = Tây). Unity's yaw
/// (transform.eulerAngles.y) is already measured this way, so no quaternion maths is needed.
/// </summary>
public static class CompassDirections
{
    private static readonly string[] Names = { "Bắc", "Đông Bắc", "Đông", "Đông Nam", "Nam", "Tây Nam", "Tây", "Tây Bắc" };
    private static readonly string[] ShortNames = { "B", "ĐB", "Đ", "ĐN", "N", "TN", "T", "TB" };

    /// <summary>
    /// Wraps an angle into [0, 360).
    /// </summary>
    public static float Normalize(float degrees)
    {
        degrees %= 360f;
        if (degrees < 0f) degrees += 360f;
        return degrees >= 359.995f ? 0f : degrees;
    }

    /// <summary>
    /// Bearing of a world direction (its height is ignored).
    /// </summary>
    public static float FromDirection(Vector3 direction)
    {
        return Normalize(Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg);
    }

    /// <summary>
    /// World direction on the ground for a bearing.
    /// </summary>
    public static Vector3 ToDirection(float bearing)
    {
        float radians = bearing * Mathf.Deg2Rad;
        return new Vector3(Mathf.Sin(radians), 0f, Mathf.Cos(radians));
    }

    /// <summary>
    /// Index of the nearest of the 8 directions (0 = Bắc, 1 = Đông Bắc, ... 7 = Tây Bắc).
    /// </summary>
    public static int Sector(float bearing)
    {
        return Mathf.RoundToInt(Normalize(bearing) / 45f) % 8;
    }

    public static string Name(float bearing) => Names[Sector(bearing)];

    public static string ShortName(float bearing) => ShortNames[Sector(bearing)];

    /// <summary>
    /// e.g. "Đông Nam 135°".
    /// </summary>
    public static string Format(float bearing)
    {
        return $"{Name(bearing)} {Mathf.RoundToInt(Normalize(bearing)) % 360}°";
    }
}
