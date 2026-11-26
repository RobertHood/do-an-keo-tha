using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The item catalog: one tab per model category (Nhà ở, Cây xanh, Bàn ghế, Trang trí, Chiếu sáng, Hàng rào, Lối đi)
/// and one card per model in the selected tab. Clicking a card spawns that model.
/// The models come from the price catalog (the model library), so adding an entry there with a category adds a
/// card automatically - no buttons to wire by hand.
/// Setup: Tools > Suburbsim > Apply UI Theme wires this up (templates, heading, empty state).
/// </summary>
[AddComponentMenu("UI/UI Catalog")]
public class UICatalog : MonoBehaviour
{
    [Header("Data")]
    [Tooltip("The model library. If empty, the catalog assigned to ModelEstPrice is used.")]
    [SerializeField] private PriceCatalog catalog;

    [Tooltip("If empty, found automatically.")]
    [SerializeField] private SpawnInModel spawner;

    [Tooltip("Tab shown first. If it has no models yet, the first tab that has models is shown instead.")]
    [SerializeField] private ModelCategory startCategory = ModelCategory.House;

    [Header("Tabs")]
    [SerializeField] private RectTransform tabsContainer;

    [Tooltip("A tab Button (with a TMP label) that is cloned for every category. Hidden at runtime.")]
    [SerializeField] private Button tabTemplate;

    [Header("Cards")]
    [SerializeField] private RectTransform cardsContainer;

    [Tooltip("A model card that is cloned for every model. Hidden at runtime.")]
    [SerializeField] private CatalogCard cardTemplate;

    [SerializeField] private ScrollRect scrollRect;

    [Tooltip("Optional. Shows e.g. 'Bàn ghế sân vườn · 5 mô hình'.")]
    [SerializeField] private TMP_Text sectionTitle;

    [Tooltip("Optional. Shown when the selected tab has no models.")]
    [SerializeField] private TMP_Text emptyState;

    [SerializeField] private string emptyText = "Chưa có mô hình nào trong mục này.";
    [SerializeField] private string countFormat = "{0} · {1} mô hình";

    private class Tab
    {
        public ModelCategory category;
        public Button button;
        public Image background;
        public TMP_Text label;
    }

    private readonly List<Tab> tabs = new List<Tab>();
    private readonly List<CatalogCard> cards = new List<CatalogCard>();
    private ModelCategory current;
    private bool built;

    public ModelCategory CurrentCategory => current;

    private void Start()
    {
        if (spawner == null) spawner = FindAnyObjectByType<SpawnInModel>();
        if (catalog == null && ModelEstPrice.Instance != null) catalog = ModelEstPrice.Instance.Catalog;

        BuildTabs();
        Select(PickStartCategory());

        // A new, empty design starts by picking the house
        bool emptyScene = spawner == null || spawner.ModelsContainer == null || spawner.ModelsContainer.childCount == 0;
        if (emptyScene && CountModels(ModelCategory.House) > 0)
        {
            UINotification.ShowTipOnce(NotificationTips.ChooseHouseKey, NotificationTips.ChooseHouse);
        }
    }

    /// <summary>
    /// Shows a category. (Can be connected to a UI Button OnClick with the category number, see ModelCategory.)
    /// </summary>
    public void SelectCategory(int category)
    {
        Select((ModelCategory)category);
    }

    public void Select(ModelCategory category)
    {
        current = category;
        if (!built) BuildTabs();

        foreach (Tab tab in tabs) StyleTab(tab);
        BuildCards();

        if (scrollRect != null) scrollRect.verticalNormalizedPosition = 1f;
    }

    /// <summary>
    /// Rebuilds the cards (e.g. after the catalog changed at runtime).
    /// </summary>
    public void Refresh()
    {
        foreach (Tab tab in tabs) StyleTab(tab);
        BuildCards();
    }

    public List<PriceCatalogEntry> GetModels(ModelCategory category)
    {
        if (catalog == null || catalog.entries == null) return new List<PriceCatalogEntry>();
        return catalog.entries.Where(e => e != null && e.prefab != null && e.category == category).ToList();
    }

    private int CountModels(ModelCategory category) => GetModels(category).Count;

    private ModelCategory PickStartCategory()
    {
        if (CountModels(startCategory) > 0) return startCategory;
        foreach (ModelCategory category in ModelCategories.Ordered)
        {
            if (CountModels(category) > 0) return category;
        }
        return startCategory;
    }

    private void BuildTabs()
    {
        built = true;
        if (tabTemplate == null || tabsContainer == null) return;

        tabTemplate.gameObject.SetActive(false);
        foreach (ModelCategory category in ModelCategories.Ordered)
        {
            Button button = Instantiate(tabTemplate, tabsContainer);
            button.name = "Tab " + category;
            button.gameObject.SetActive(true);
            button.onClick = new Button.ButtonClickedEvent(); // Drop the template's own OnClick calls
            ModelCategory captured = category;
            button.onClick.AddListener(() => Select(captured));

            Tab tab = new Tab
            {
                category = category,
                button = button,
                background = button.targetGraphic as Image,
                label = button.GetComponentInChildren<TMP_Text>(true),
            };
            if (tab.label != null) tab.label.text = ModelCategories.ShortName(category);
            tabs.Add(tab);
        }

        // Other buttons in the tab bar (e.g. a collapse button) stay after the tabs
        foreach (Transform child in tabsContainer.Cast<Transform>().ToList())
        {
            if (child != tabTemplate.transform && tabs.All(t => t.button.transform != child)) child.SetAsLastSibling();
        }
    }

    private void StyleTab(Tab tab)
    {
        bool active = tab.category == current;
        bool hasModels = CountModels(tab.category) > 0;

        if (tab.background != null) tab.background.color = active ? UIPalette.BrandBlack : UIPalette.Clear;
        if (tab.label != null)
        {
            tab.label.color = active ? UIPalette.White : hasModels ? UIPalette.Neutral600 : UIPalette.Neutral400;
        }
    }

    private void BuildCards()
    {
        List<PriceCatalogEntry> models = GetModels(current);

        if (cardTemplate != null && cardsContainer != null)
        {
            cardTemplate.gameObject.SetActive(false);

            for (int i = 0; i < models.Count; i++)
            {
                CatalogCard card = GetCard(i);
                PriceCatalogEntry entry = models[i];
                card.name = "Card " + entry.Id;
                card.Setup(entry);

                Button button = card.Button;
                if (button != null)
                {
                    button.onClick = new Button.ButtonClickedEvent(); // Drop the template's own OnClick calls
                    GameObject prefab = entry.prefab;
                    button.onClick.AddListener(() => Spawn(prefab));
                }
            }

            for (int i = models.Count; i < cards.Count; i++)
            {
                cards[i].gameObject.SetActive(false);
            }
        }

        if (sectionTitle != null)
        {
            sectionTitle.text = string.Format(countFormat, ModelCategories.FullName(current), models.Count);
        }

        if (emptyState != null)
        {
            emptyState.text = emptyText;
            emptyState.gameObject.SetActive(models.Count == 0);
        }
    }

    private CatalogCard GetCard(int index)
    {
        while (cards.Count <= index)
        {
            cards.Add(Instantiate(cardTemplate, cardsContainer));
        }

        CatalogCard card = cards[index];
        card.gameObject.SetActive(true);
        card.transform.SetSiblingIndex(index + 1);
        return card;
    }

    private void Spawn(GameObject prefab)
    {
        if (spawner == null || prefab == null) return;
        spawner.Spawn(prefab);
    }
}
