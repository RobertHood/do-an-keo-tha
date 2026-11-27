using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Estimates the price of every item placed in the blueprint. Data only: the price panel itself is shown
/// by the web page (WebBridge sends it the item list and forwards its store / custom price choices here).
/// Features:
/// - Items default to their cheapest store offer (or the catalog's listed price if there are no offers).
/// - Houses with building materials in the catalog are priced as the sum of their materials: quantity (scaled with
///   the model) times unit price, each material with its own store / custom unit price choice.
/// - A store can be chosen per item, or a custom price typed in, which overrides market prices.
/// - Price choices are stored on each model's DraggableModel and saved with the blueprint.
/// - Refreshes automatically when models are spawned, deleted or imported, and raises Changed.
/// </summary>
[AddComponentMenu("Blueprint/Model Est Price")]
public class ModelEstPrice : MonoBehaviour
{
    public static ModelEstPrice Instance { get; private set; }

    [Header("Price Data")]
    [Tooltip("Prices per prefab (Assets > Create > Blueprint > Price Catalog).")]
    [SerializeField] private PriceCatalog catalog;

    [Tooltip("SpawnInModel in the scene. If empty, found automatically.")]
    [SerializeField] private SpawnInModel spawner;

    private readonly List<DraggableModel> items = new List<DraggableModel>();
    private bool isDirty = true;

    /// <summary>
    /// Raised after the item list or any price changed (once per frame at most).
    /// </summary>
    public event Action Changed;

    public PriceCatalog Catalog
    {
        get => catalog;
        set { catalog = value; isDirty = true; }
    }

    public double TotalPrice { get; private set; }
    public int ItemCount => items.Count;

    /// <summary>
    /// Placed items, in the order they appear in the models container.
    /// </summary>
    public IReadOnlyList<DraggableModel> Items => items;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(this);
            return;
        }

        if (spawner == null)
        {
            spawner = FindAnyObjectByType<SpawnInModel>();
        }

        if (catalog == null)
        {
            Debug.LogWarning("[ModelEstPrice] No Price Catalog assigned. Items will show as unpriced.");
        }
    }

    private void OnEnable()
    {
        DraggableModel.ModelsChanged += MarkDirty;
        isDirty = true; // Catch up on anything that changed while disabled
    }

    private void OnDisable()
    {
        DraggableModel.ModelsChanged -= MarkDirty;
    }

    private void LateUpdate()
    {
        if (isDirty)
        {
            isDirty = false;
            Refresh();
        }
    }

    public void MarkDirty()
    {
        isDirty = true;
    }

    /// <summary>
    /// Re-collects the placed items and recalculates the total.
    /// </summary>
    public void Refresh()
    {
        CollectItems();

        TotalPrice = 0;
        foreach (DraggableModel model in items)
        {
            if (TryGetPrice(model, out double price)) TotalPrice += price;
        }

        Changed?.Invoke();
    }

    /// <summary>
    /// Uses a store's price for the model (clears any custom price).
    /// </summary>
    public void SelectOffer(DraggableModel model, string storeName)
    {
        if (model == null) return;
        model.selectedStore = storeName;
        model.useCustomPrice = false;
        isDirty = true;
    }

    public void SetCustomPrice(DraggableModel model, double price)
    {
        if (model == null) return;
        model.useCustomPrice = true;
        model.customPrice = Math.Max(0, price);
        isDirty = true;
    }

    /// <summary>
    /// Goes back to market prices (the chosen store, or the cheapest / listed price).
    /// </summary>
    public void ClearCustomPrice(DraggableModel model)
    {
        if (model == null) return;
        model.useCustomPrice = false;
        isDirty = true;
    }

    /// <summary>
    /// The price used for this model in the total. Returns false if it has no price at all.
    /// Order: custom price, then chosen store (or cheapest), then the catalog's listed price.
    /// </summary>
    public bool TryGetPrice(DraggableModel model, out double price)
    {
        price = 0;
        if (model == null) return false;

        if (model.useCustomPrice)
        {
            price = model.customPrice;
            return true;
        }

        PriceCatalogEntry entry = FindEntry(model);
        if (entry == null) return false;

        if (entry.HasMaterials)
        {
            foreach (BuildingMaterial material in entry.materials)
            {
                if (material != null) price += GetMaterialCost(model, material);
            }
            return true;
        }

        PriceOffer chosen = GetChosenOffer(model, entry);
        price = chosen != null ? chosen.price : entry.listedPrice;
        return true;
    }

    // ------------------------------------------------------------------ building materials

    /// <summary>
    /// Uses a store's unit price for one material of the house (clears its custom unit price).
    /// </summary>
    public void SelectMaterialOffer(DraggableModel model, string materialId, string storeName)
    {
        MaterialChoice choice = GetOrAddChoice(model, materialId);
        if (choice == null) return;
        choice.selectedStore = storeName;
        choice.useCustomPrice = false;
        isDirty = true;
    }

    public void SetMaterialCustomPrice(DraggableModel model, string materialId, double unitPrice)
    {
        MaterialChoice choice = GetOrAddChoice(model, materialId);
        if (choice == null) return;
        choice.useCustomPrice = true;
        choice.customPrice = Math.Max(0, unitPrice);
        isDirty = true;
    }

    public void ClearMaterialCustomPrice(DraggableModel model, string materialId)
    {
        MaterialChoice choice = FindChoice(model, materialId);
        if (choice == null) return;
        choice.useCustomPrice = false;
        isDirty = true;
    }

    public static MaterialChoice FindChoice(DraggableModel model, string materialId)
    {
        if (model == null || model.materialChoices == null || string.IsNullOrEmpty(materialId)) return null;
        return model.materialChoices.Find(c => c != null &&
            string.Equals(c.materialId, materialId, StringComparison.OrdinalIgnoreCase));
    }

    private static MaterialChoice GetOrAddChoice(DraggableModel model, string materialId)
    {
        if (model == null || string.IsNullOrEmpty(materialId)) return null;

        MaterialChoice choice = FindChoice(model, materialId);
        if (choice != null) return choice;

        if (model.materialChoices == null) model.materialChoices = new List<MaterialChoice>();
        choice = new MaterialChoice { materialId = materialId };
        model.materialChoices.Add(choice);
        return choice;
    }

    /// <summary>
    /// The store chosen for this material, or its cheapest offer. Null if it has no store offers.
    /// </summary>
    public static PriceOffer GetChosenOffer(DraggableModel model, BuildingMaterial material)
    {
        List<PriceOffer> sorted = GetSortedOffers(material);
        if (sorted.Count == 0) return null;

        MaterialChoice choice = FindChoice(model, material.id);
        if (choice != null && !string.IsNullOrEmpty(choice.selectedStore))
        {
            PriceOffer match = sorted.Find(o => o.storeName == choice.selectedStore);
            if (match != null) return match;
        }

        return sorted[0];
    }

    /// <summary>
    /// Price per unit: custom unit price, then the chosen store (or cheapest), then the listed unit price.
    /// </summary>
    public static double GetMaterialUnitPrice(DraggableModel model, BuildingMaterial material)
    {
        MaterialChoice choice = FindChoice(model, material.id);
        if (choice != null && choice.useCustomPrice) return choice.customPrice;

        PriceOffer chosen = GetChosenOffer(model, material);
        return chosen != null ? chosen.price : material.listedPrice;
    }

    /// <summary>
    /// Amount the placed house uses, scaled with the model (see BuildingMaterial).
    /// </summary>
    public static double GetMaterialQuantity(DraggableModel model, BuildingMaterial material)
    {
        Vector3 scale = model != null ? model.transform.localScale : Vector3.one;
        return material.quantity * QuantityScale(scale, material.unit);
    }

    public static double GetMaterialCost(DraggableModel model, BuildingMaterial material)
    {
        return GetMaterialQuantity(model, material) * GetMaterialUnitPrice(model, material);
    }

    /// <summary>
    /// How much a quantity in this unit grows for a model at this scale. Uses the average scale, so a house
    /// stretched on one axis is estimated as if scaled evenly.
    /// </summary>
    public static double QuantityScale(Vector3 scale, string unit)
    {
        double s = Math.Pow(Math.Abs((double)scale.x * scale.y * scale.z), 1.0 / 3.0);
        switch (UnitDimension(unit))
        {
            case 1: return s;
            case 2: return s * s;
            case 3: return s * s * s;
            default: return 1;
        }
    }

    /// <summary>
    /// 1 for lengths (m, md), 2 for areas (m², m2), 3 for volumes (m³, m3), 0 for counts (tấm, bộ, cái...).
    /// </summary>
    public static int UnitDimension(string unit)
    {
        string u = (unit ?? "").Trim().ToLowerInvariant().Replace(" ", "");
        switch (u)
        {
            case "m": case "md": case "mét": case "métdài": return 1;
            case "m2": case "m²": case "m^2": return 2;
            case "m3": case "m³": case "m^3": return 3;
            default: return 0;
        }
    }

    public static List<PriceOffer> GetSortedOffers(BuildingMaterial material)
    {
        if (material == null || material.offers == null) return new List<PriceOffer>();
        return material.offers.Where(o => o != null).OrderBy(o => o.price).ToList();
    }

    /// <summary>
    /// Lowest price of a catalog model at scale 1 (cheapest offers), for the catalog cards.
    /// isFrom is true if other store choices cost more.
    /// </summary>
    public static double GetCatalogPrice(PriceCatalogEntry entry, out bool isFrom)
    {
        isFrom = false;
        if (entry == null) return 0;

        if (entry.HasMaterials)
        {
            double total = 0;
            foreach (BuildingMaterial material in entry.materials)
            {
                if (material == null) continue;
                List<PriceOffer> offers = GetSortedOffers(material);
                total += material.quantity * (offers.Count > 0 ? offers[0].price : material.listedPrice);
                if (offers.Count > 1) isFrom = true;
            }
            return total;
        }

        List<PriceOffer> sorted = GetSortedOffers(entry);
        isFrom = sorted.Count > 1;
        return sorted.Count > 0 ? sorted[0].price : entry.listedPrice;
    }

    public PriceCatalogEntry FindEntry(DraggableModel model)
    {
        return catalog != null && model != null ? catalog.FindEntry(model.prefabId) : null;
    }

    /// <summary>
    /// The store chosen for this model, or the cheapest offer if none was chosen (or the chosen store is gone).
    /// Null if the item has no store offers.
    /// </summary>
    public PriceOffer GetChosenOffer(DraggableModel model, PriceCatalogEntry entry)
    {
        List<PriceOffer> sorted = GetSortedOffers(entry);
        if (sorted.Count == 0) return null;

        if (!string.IsNullOrEmpty(model.selectedStore))
        {
            PriceOffer match = sorted.Find(o => o.storeName == model.selectedStore);
            if (match != null) return match;
        }

        return sorted[0];
    }

    /// <summary>
    /// Store offers for the entry, cheapest first.
    /// </summary>
    public static List<PriceOffer> GetSortedOffers(PriceCatalogEntry entry)
    {
        if (entry == null || entry.offers == null) return new List<PriceOffer>();
        return entry.offers.Where(o => o != null).OrderBy(o => o.price).ToList();
    }

    public static string GetDisplayName(DraggableModel model, PriceCatalogEntry entry)
    {
        if (entry != null && !string.IsNullOrEmpty(entry.displayName)) return entry.displayName;
        return !string.IsNullOrEmpty(model.prefabId) ? model.prefabId : model.name;
    }

    private void CollectItems()
    {
        items.Clear();
        if (spawner == null || spawner.ModelsContainer == null) return;

        foreach (Transform child in spawner.ModelsContainer)
        {
            if (child.TryGetComponent(out DraggableModel model))
            {
                items.Add(model);
            }
        }
    }
}
