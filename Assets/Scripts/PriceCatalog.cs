using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The model library: every spawnable prefab with its catalog tab, thumbnail, description, a listed price,
/// and market prices from different stores. The item catalog (UICatalog) lists these entries, and blueprints
/// find their prefabs here.
/// Create via Assets > Create > Blueprint > Price Catalog, then assign it to ModelEstPrice.
/// The entry classes are plain serializable data, so the same catalog can later be loaded from the web database.
/// </summary>
[CreateAssetMenu(fileName = "PriceCatalog", menuName = "Blueprint/Price Catalog")]
public class PriceCatalog : ScriptableObject
{
    public List<PriceCatalogEntry> entries = new List<PriceCatalogEntry>();

    /// <summary>
    /// Finds the entry for a prefab ID (case-insensitive). Returns null if the prefab has no entry.
    /// </summary>
    public PriceCatalogEntry FindEntry(string prefabId)
    {
        if (string.IsNullOrEmpty(prefabId)) return null;
        return entries.Find(e => e != null && string.Equals(e.Id, prefabId, StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>
/// The catalog tabs. Values are saved in the PriceCatalog asset, so don't renumber them.
/// </summary>
public enum ModelCategory
{
    Uncategorized = 0,
    House = 1,
    Plants = 2,
    GardenFurniture = 3,
    Decor = 4,
    Lighting = 5,
    FenceGate = 6,
    PathWater = 7,
}

public static class ModelCategories
{
    /// <summary>
    /// Tab order in the item catalog.
    /// </summary>
    public static readonly ModelCategory[] Ordered =
    {
        ModelCategory.House, ModelCategory.Plants, ModelCategory.GardenFurniture, ModelCategory.Decor,
        ModelCategory.Lighting, ModelCategory.FenceGate, ModelCategory.PathWater,
    };

    /// <summary>
    /// Short name for the tab.
    /// </summary>
    public static string ShortName(ModelCategory category)
    {
        switch (category)
        {
            case ModelCategory.House: return "Nhà ở";
            case ModelCategory.Plants: return "Cây xanh";
            case ModelCategory.GardenFurniture: return "Bàn ghế";
            case ModelCategory.Decor: return "Trang trí";
            case ModelCategory.Lighting: return "Chiếu sáng";
            case ModelCategory.FenceGate: return "Hàng rào";
            case ModelCategory.PathWater: return "Lối đi";
            default: return "Khác";
        }
    }

    /// <summary>
    /// Full name for the heading above the cards.
    /// </summary>
    public static string FullName(ModelCategory category)
    {
        switch (category)
        {
            case ModelCategory.GardenFurniture: return "Bàn ghế sân vườn";
            case ModelCategory.FenceGate: return "Hàng rào & cổng";
            case ModelCategory.PathWater: return "Lối đi & mặt nước";
            default: return ShortName(category);
        }
    }
}

[Serializable]
public class PriceCatalogEntry
{
    [Tooltip("The prefab this entry prices (matched by name, the same ID blueprints use).")]
    public GameObject prefab;

    [Tooltip("Catalog tab this model is listed under.")]
    public ModelCategory category = ModelCategory.Uncategorized;

    [Tooltip("Only needed if no prefab is linked (e.g. entries loaded from the web database).")]
    public string prefabId;

    [Tooltip("Name shown in the price panels. If empty, the prefab name is used.")]
    public string displayName;

    [TextArea(2, 4)]
    public string description;

    [Tooltip("Thumbnail shown on the catalog card (Tools > Suburbsim > Refresh Catalog renders one if empty).")]
    public Sprite icon;

    [Tooltip("Price used when there are no store offers (a manually set price).")]
    public double listedPrice;

    [Tooltip("Market prices from different stores. Shown cheapest first; the cheapest is used by default.")]
    public List<PriceOffer> offers = new List<PriceOffer>();

    [Header("Phong thủy")]
    [Tooltip("What the model is for the phong thủy rules. Auto = guessed from the catalog tab and prefab name.")]
    public FengShuiType loai = FengShuiType.Auto;

    [Tooltip("Where the model's front faces, in degrees clockwise (seen from above) from the model's own +Z axis: " +
             "0 = +Z, 90 = +X, 180 = -Z, 270 = -X. For a house, the front is the side with the main door.")]
    public float frontYawOffset;

    [Tooltip("Houses: the model has a main door at Door Offset.")]
    public bool hasDoor;

    [Tooltip("Houses: position of the main door in the model's own space (metres at scale 1, on the ground).")]
    public Vector3 doorOffset;

    [Tooltip("Real size in metres at scale 1 (width X, height Y, depth Z). Filled by Tools > Suburbsim > Refresh Catalog.")]
    public Vector3 baseSize;

    public string Id => prefab != null ? prefab.name : prefabId;

    public FengShuiType ResolvedType => FengShuiTypes.Resolve(loai, category, Id);
}

[Serializable]
public class PriceOffer
{
    public string storeName;
    public double price;
}
