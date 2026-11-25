using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Estimates the price of every item placed in the blueprint. Data only: the price panel itself is shown
/// by the web page (WebBridge sends it the item list and forwards its store / custom price choices here).
/// Features:
/// - Items default to their cheapest store offer (or the catalog's listed price if there are no offers).
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

        PriceOffer chosen = GetChosenOffer(model, entry);
        price = chosen != null ? chosen.price : entry.listedPrice;
        return true;
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
