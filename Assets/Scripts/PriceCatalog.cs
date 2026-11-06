using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Price data for every spawnable prefab: description, a listed price, and market prices from different stores.
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

[Serializable]
public class PriceCatalogEntry
{
    [Tooltip("The prefab this entry prices (matched by name, the same ID blueprints use).")]
    public GameObject prefab;

    [Tooltip("Only needed if no prefab is linked (e.g. entries loaded from the web database).")]
    public string prefabId;

    [Tooltip("Name shown in the price panels. If empty, the prefab name is used.")]
    public string displayName;

    [TextArea(2, 4)]
    public string description;

    [Tooltip("Optional thumbnail shown in the item list.")]
    public Sprite icon;

    [Tooltip("Price used when there are no store offers (a manually set price).")]
    public double listedPrice;

    [Tooltip("Market prices from different stores. Shown cheapest first; the cheapest is used by default.")]
    public List<PriceOffer> offers = new List<PriceOffer>();

    public string Id => prefab != null ? prefab.name : prefabId;
}

[Serializable]
public class PriceOffer
{
    public string storeName;
    public double price;
}
