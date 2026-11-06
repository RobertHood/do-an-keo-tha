using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Estimates the price of every item placed in the blueprint.
/// Features:
/// - Item list panel: one clickable row per placed item with its name, description and chosen price.
/// - Clicking a row selects that model and opens the market prices panel, listing store prices cheapest first.
/// - Clicking a store price uses it for that item; typing into the custom price field overrides market prices.
/// - Optional total price and item count texts.
/// - Items default to their cheapest offer (or the catalog's listed price if there are no offers).
/// - Price choices are stored on each model's DraggableModel and saved with the blueprint.
/// - Refreshes automatically when models are spawned, deleted or imported.
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

    [Header("Currency")]
    [SerializeField] private string currencySymbol = "$";

    [Tooltip("Show the symbol after the amount (e.g. '3,000 ₫' instead of '$3,000').")]
    [SerializeField] private bool symbolAfterAmount = false;

    [SerializeField] private int decimalPlaces = 0;

    [Header("Item List Panel")]
    [Tooltip("Parent for item rows: a Scroll View's Content (or the Scroll View itself, its Content is used).")]
    [SerializeField] private Transform itemListContainer;

    [Tooltip("Row template for each placed item (a Button with a PriceRowUI component).")]
    [SerializeField] private PriceRowUI itemRowPrefab;

    [Tooltip("Optional. Shows the total price of all items.")]
    [SerializeField] private TMP_Text totalPriceText;

    [Tooltip("{0} = formatted total price.")]
    [SerializeField] private string totalPriceFormat = "Total: {0}";

    [Tooltip("Optional. Shows how many items are in the blueprint.")]
    [SerializeField] private TMP_Text totalCountText;

    [Tooltip("{0} = number of items.")]
    [SerializeField] private string totalCountFormat = "Items: {0}";

    [Header("Market Prices Panel")]
    [Tooltip("The panel that opens when an item row is clicked. Hidden on start.")]
    [SerializeField] private GameObject offersPanel;

    [SerializeField] private TMP_Text offersTitleText;

    [SerializeField] private TMP_Text offersDescriptionText;

    [Tooltip("Parent for store price rows: a Scroll View's Content (or the Scroll View itself, its Content is used).")]
    [SerializeField] private Transform offersContainer;

    [Tooltip("Row template for each store price (a Button with a PriceRowUI component).")]
    [SerializeField] private PriceRowUI offerRowPrefab;

    [Tooltip("Optional. Lets the user type their own price for the item. Clear it to go back to market prices.")]
    [SerializeField] private TMP_InputField customPriceInput;

    [Header("Labels")]
    [SerializeField] private string noPriceLabel = "No price";
    [SerializeField] private string cheapestLabel = "Cheapest";
    [SerializeField] private string customPriceLabel = "Custom price";
    [SerializeField] private string listedPriceLabel = "Listed price";
    [SerializeField] private string noOffersDescription = "No market prices for this item yet. Enter a custom price.";

    private readonly List<DraggableModel> items = new List<DraggableModel>();
    private readonly List<PriceRowUI> itemRows = new List<PriceRowUI>();
    private readonly List<PriceRowUI> offerRows = new List<PriceRowUI>();
    private DraggableModel offersTarget;
    private Transform lastSelected;
    private bool isDirty = true;

    public PriceCatalog Catalog
    {
        get => catalog;
        set { catalog = value; isDirty = true; }
    }

    public double TotalPrice { get; private set; }
    public int ItemCount => items.Count;

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

        itemListContainer = ResolveContainer(itemListContainer);
        offersContainer = ResolveContainer(offersContainer);

        // Row templates placed in the scene (instead of prefab assets) shouldn't show up as rows themselves
        HideSceneTemplate(itemRowPrefab);
        HideSceneTemplate(offerRowPrefab);

        if (customPriceInput != null)
        {
            customPriceInput.onEndEdit.AddListener(OnCustomPriceEntered);
        }

        if (offersPanel != null) offersPanel.SetActive(false);
    }

    private void OnEnable()
    {
        DraggableModel.ModelsChanged += MarkDirty;
    }

    private void OnDisable()
    {
        DraggableModel.ModelsChanged -= MarkDirty;
    }

    private void LateUpdate()
    {
        // Follow the scene selection: highlight its row, and switch / close the market prices panel
        Transform selected = ModelMovement.Instance != null ? ModelMovement.Instance.SelectedModel : null;
        if (selected != lastSelected)
        {
            lastSelected = selected;
            OnSelectionChanged(selected);
            isDirty = true;
        }

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
    /// Rebuilds the item list, totals and (if open) the market prices panel.
    /// </summary>
    public void Refresh()
    {
        CollectItems();

        TotalPrice = 0;
        for (int i = 0; i < items.Count; i++)
        {
            DraggableModel model = items[i];
            PriceCatalogEntry entry = FindEntry(model);
            bool hasPrice = TryGetPrice(model, out double price);
            if (hasPrice) TotalPrice += price;

            PriceRowUI row = GetRow(itemRows, itemRowPrefab, itemListContainer, i);
            if (row == null) continue;

            row.Set(
                GetDisplayName(model, entry),
                entry != null ? entry.description : null,
                hasPrice ? FormatPrice(price) : noPriceLabel,
                entry != null ? entry.icon : null,
                model.transform == lastSelected,
                () => OnItemClicked(model));
        }
        HideExtraRows(itemRows, items.Count);

        if (totalPriceText != null) totalPriceText.text = string.Format(totalPriceFormat, FormatPrice(TotalPrice));
        if (totalCountText != null) totalCountText.text = string.Format(totalCountFormat, items.Count);

        if (offersTarget != null)
        {
            RefreshOffersPanel();
        }
    }

    /// <summary>
    /// Selects the model and opens its market prices panel. Called when an item row is clicked.
    /// </summary>
    public void OnItemClicked(DraggableModel model)
    {
        if (model == null) return;

        if (ModelMovement.Instance != null)
        {
            ModelMovement.Instance.SelectModel(model.transform);
        }

        ShowOffersFor(model);
    }

    public void ShowOffersFor(DraggableModel model)
    {
        offersTarget = model;
        if (offersPanel != null) offersPanel.SetActive(true);
        RefreshOffersPanel();
    }

    /// <summary>
    /// Closes the market prices panel. (Can be connected to a close Button's OnClick).
    /// </summary>
    public void CloseOffersPanel()
    {
        offersTarget = null;
        if (offersPanel != null) offersPanel.SetActive(false);
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
        model.customPrice = System.Math.Max(0, price);
        isDirty = true;
    }

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

    public string FormatPrice(double amount)
    {
        string number = amount.ToString("N" + Mathf.Max(0, decimalPlaces), CultureInfo.InvariantCulture);
        return symbolAfterAmount ? $"{number} {currencySymbol}" : currencySymbol + number;
    }

    private void RefreshOffersPanel()
    {
        DraggableModel model = offersTarget;
        if (model == null)
        {
            CloseOffersPanel();
            return;
        }

        PriceCatalogEntry entry = FindEntry(model);
        List<PriceOffer> sortedOffers = GetSortedOffers(entry);

        if (offersTitleText != null) offersTitleText.text = GetDisplayName(model, entry);
        if (offersDescriptionText != null) offersDescriptionText.text = entry != null ? entry.description : noOffersDescription;

        int rowCount = 0;
        if (sortedOffers.Count > 0)
        {
            PriceOffer chosen = model.useCustomPrice ? null : GetChosenOffer(model, entry);

            for (int i = 0; i < sortedOffers.Count; i++)
            {
                PriceOffer offer = sortedOffers[i];
                PriceRowUI row = GetRow(offerRows, offerRowPrefab, offersContainer, rowCount++);
                if (row == null) continue;

                row.Set(
                    offer.storeName,
                    i == 0 ? cheapestLabel : null,
                    FormatPrice(offer.price),
                    null,
                    offer == chosen,
                    () => SelectOffer(model, offer.storeName));
            }
        }
        else if (entry != null)
        {
            // No store offers: show the catalog's manually set price as the only option
            PriceRowUI row = GetRow(offerRows, offerRowPrefab, offersContainer, rowCount++);
            if (row != null)
            {
                row.Set(listedPriceLabel, null, FormatPrice(entry.listedPrice), null, !model.useCustomPrice,
                    () => ClearCustomPrice(model));
            }
        }

        if (model.useCustomPrice)
        {
            PriceRowUI row = GetRow(offerRows, offerRowPrefab, offersContainer, rowCount++);
            if (row != null)
            {
                row.Set(customPriceLabel, null, FormatPrice(model.customPrice), null, true, null);
            }
        }

        HideExtraRows(offerRows, rowCount);

        if (customPriceInput != null)
        {
            string text = model.useCustomPrice ? model.customPrice.ToString(CultureInfo.InvariantCulture) : "";
            customPriceInput.SetTextWithoutNotify(text);
        }
    }

    private void OnSelectionChanged(Transform selected)
    {
        if (offersTarget == null) return;

        // Keep the market prices panel on the selected model; close it when nothing is selected
        DraggableModel model = selected != null ? selected.GetComponent<DraggableModel>() : null;
        if (model != null)
        {
            if (model != offersTarget) ShowOffersFor(model);
        }
        else
        {
            CloseOffersPanel();
        }
    }

    private void OnCustomPriceEntered(string text)
    {
        if (offersTarget == null) return;

        if (string.IsNullOrWhiteSpace(text))
        {
            ClearCustomPrice(offersTarget);
            return;
        }

        string cleaned = text.Replace(currencySymbol, "").Trim();
        if (double.TryParse(cleaned, NumberStyles.Number, CultureInfo.InvariantCulture, out double price) ||
            double.TryParse(cleaned, NumberStyles.Number, CultureInfo.CurrentCulture, out price))
        {
            SetCustomPrice(offersTarget, price);
        }
        else
        {
            isDirty = true; // Invalid input: refresh puts the previous value back
        }
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

    private PriceCatalogEntry FindEntry(DraggableModel model)
    {
        return catalog != null && model != null ? catalog.FindEntry(model.prefabId) : null;
    }

    /// <summary>
    /// The store chosen for this model, or the cheapest offer if none was chosen (or the chosen store is gone).
    /// </summary>
    private PriceOffer GetChosenOffer(DraggableModel model, PriceCatalogEntry entry)
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

    private static List<PriceOffer> GetSortedOffers(PriceCatalogEntry entry)
    {
        if (entry == null || entry.offers == null) return new List<PriceOffer>();
        return entry.offers.Where(o => o != null).OrderBy(o => o.price).ToList();
    }

    private static string GetDisplayName(DraggableModel model, PriceCatalogEntry entry)
    {
        if (entry != null && !string.IsNullOrEmpty(entry.displayName)) return entry.displayName;
        return !string.IsNullOrEmpty(model.prefabId) ? model.prefabId : model.name;
    }

    /// <summary>
    /// Returns the row at index, creating it from the template if needed. Rows are reused between refreshes.
    /// </summary>
    private static PriceRowUI GetRow(List<PriceRowUI> rows, PriceRowUI template, Transform container, int index)
    {
        if (template == null || container == null) return null;

        while (rows.Count <= index)
        {
            rows.Add(Instantiate(template, container));
        }

        PriceRowUI row = rows[index];
        row.gameObject.SetActive(true);
        return row;
    }

    private static void HideExtraRows(List<PriceRowUI> rows, int usedCount)
    {
        for (int i = usedCount; i < rows.Count; i++)
        {
            rows[i].gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// If the container is a Scroll View, returns its Content so rows end up inside the scrolling list.
    /// </summary>
    private static Transform ResolveContainer(Transform container)
    {
        if (container != null && container.TryGetComponent(out ScrollRect scrollRect) && scrollRect.content != null)
        {
            return scrollRect.content;
        }
        return container;
    }

    private static void HideSceneTemplate(PriceRowUI template)
    {
        if (template != null && template.gameObject.scene.IsValid())
        {
            template.gameObject.SetActive(false);
        }
    }
}
