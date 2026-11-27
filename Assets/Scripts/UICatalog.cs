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
/// The "—" button (Hide Panel) slides the catalog off the left edge; the "Thư viện mô hình" button that appears
/// in its place brings it back. That button is built into the scene by Tools > Suburbsim > Build Land Panel and
/// Compass (or at runtime if missing).
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

    [Header("Collapse")]
    [Tooltip("Hides the catalog. If empty, the child named \"Hide Panel\" is used.")]
    [SerializeField] private Button hideButton;

    [Tooltip("Shown while the catalog is hidden; brings it back. Created at runtime if empty.")]
    [SerializeField] private Button showButton;

    [SerializeField] private float slideDuration = 0.2f;

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

    public bool IsCollapsed { get; private set; }

    private RectTransform panel;
    private CanvasGroup panelGroup;
    private float expandedX;
    private Coroutine slide;

    private void OnEnable()
    {
        ModelLibrary.CatalogLoaded += OnLibraryCatalogLoaded;
        ModelLibrary.ThumbnailsChanged += Refresh;
    }

    private void OnDisable()
    {
        ModelLibrary.CatalogLoaded -= OnLibraryCatalogLoaded;
        ModelLibrary.ThumbnailsChanged -= Refresh;
    }

    private void Start()
    {
        if (spawner == null) spawner = FindAnyObjectByType<SpawnInModel>();
        if (ModelLibrary.Instance != null && ModelLibrary.Instance.IsRemote) catalog = ModelLibrary.Instance.Catalog;
        if (catalog == null && ModelEstPrice.Instance != null) catalog = ModelEstPrice.Instance.Catalog;

        BuildTabs();
        Select(PickStartCategory());
        SetupCollapse();
        ShowStartTip();
    }

    // The web page sends the model library after the scene started: show its models
    private void OnLibraryCatalogLoaded()
    {
        if (ModelLibrary.Instance == null) return;
        catalog = ModelLibrary.Instance.Catalog;
        Select(PickStartCategory());
        ShowStartTip();
    }

    private void ShowStartTip()
    {
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
        ModelLibrary library = ModelLibrary.Instance;
        return catalog.entries
            .Where(e => e != null && e.category == category && (e.prefab != null || (library != null && library.HasRemote(e.Id))))
            .ToList();
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
                    string id = entry.Id;
                    button.onClick.AddListener(() => Spawn(id));
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

    // ------------------------------------------------------------------ collapse

    private void SetupCollapse()
    {
        panel = (RectTransform)transform;
        expandedX = panel.anchoredPosition.x;
        panelGroup = GetComponent<CanvasGroup>();
        if (panelGroup == null) panelGroup = gameObject.AddComponent<CanvasGroup>();

        if (hideButton == null)
        {
            Transform hide = FindDeep(transform, "Hide Panel");
            if (hide != null) hideButton = hide.GetComponent<Button>();
        }
        if (hideButton != null)
        {
            hideButton.onClick = new Button.ButtonClickedEvent();
            hideButton.onClick.AddListener(Collapse);
        }

        if (showButton == null && transform.parent != null) showButton = BuildShowButton(transform.parent);
        if (showButton != null)
        {
            showButton.onClick = new Button.ButtonClickedEvent();
            showButton.onClick.AddListener(Expand);
            showButton.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Slides the catalog out of view to the left. (Can be connected to a UI Button OnClick.)
    /// </summary>
    public void Collapse() => SetCollapsed(true);

    /// <summary>
    /// Slides the catalog back in. (Can be connected to a UI Button OnClick.)
    /// </summary>
    public void Expand() => SetCollapsed(false);

    public void SetCollapsed(bool collapsed)
    {
        if (panel == null || collapsed == IsCollapsed) return;
        IsCollapsed = collapsed;

        // The hidden panel must not catch clicks meant for the 3D view
        panelGroup.blocksRaycasts = !collapsed;
        panelGroup.interactable = !collapsed;
        if (!collapsed && showButton != null) showButton.gameObject.SetActive(false);

        float target = collapsed ? expandedX - panel.rect.width - 8f : expandedX;
        if (slide != null) StopCoroutine(slide);
        slide = StartCoroutine(Slide(target, collapsed));
    }

    private System.Collections.IEnumerator Slide(float targetX, bool collapsed)
    {
        float startX = panel.anchoredPosition.x;
        float t = 0f;
        while (t < 1f)
        {
            t = slideDuration > 0f ? Mathf.Min(1f, t + Time.unscaledDeltaTime / slideDuration) : 1f;
            float eased = 1f - (1f - t) * (1f - t);
            panel.anchoredPosition = new Vector2(Mathf.Lerp(startX, targetX, eased), panel.anchoredPosition.y);
            yield return null;
        }
        if (collapsed && showButton != null) showButton.gameObject.SetActive(true);
        slide = null;
    }

    /// <summary>
    /// The "Thư viện mô hình" button in the top-left corner that brings the catalog back (white card like the
    /// toolbar). Used by the editor build menu, and at runtime when the scene has none.
    /// </summary>
    public static Button BuildShowButton(Transform parent)
    {
        UIKit kit = UIKit.Get();
        Button button = UIBuild.Button(parent, "Show Catalog", kit != null ? kit.round12 : null, UIPalette.White, null);
        RectTransform rect = (RectTransform)button.transform;
        UIBuild.Place(rect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -16f), new Vector2(188f, 44f));

        Image outline = UIBuild.Image(rect, "Outline", kit != null ? kit.outline12 : null, UIPalette.Neutral200);
        UIBuild.Stretch(outline.rectTransform);

        TMP_Text label = UIBuild.Text(rect, "Label", "Thư viện mô hình", 14f, UIPalette.BrandBlack, UIBuild.Weight.SemiBold, TextAlignmentOptions.MidlineLeft);
        UIBuild.Stretch(label.rectTransform, 16f, 34f, 0f, 0f);
        TMP_Text arrow = UIBuild.Text(rect, "Arrow", "›", 22f, UIPalette.Neutral500, UIBuild.Weight.SemiBold, TextAlignmentOptions.Center);
        UIBuild.Place(arrow.rectTransform, new Vector2(1f, 0f), Vector2.one, new Vector2(1f, 0.5f), new Vector2(-8f, 2f), new Vector2(24f, 0f));
        return button;
    }

    /// <summary>
    /// Builds the show button into the scene next to the catalog (editor menu). Returns false if it already exists.
    /// </summary>
    public bool BuildShowButtonInEditor()
    {
        if (showButton != null || transform.parent == null) return false;
        showButton = BuildShowButton(transform.parent);
        showButton.transform.SetSiblingIndex(transform.GetSiblingIndex() + 1);
        return true;
    }

    private static Transform FindDeep(Transform root, string name)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == name) return child;
        }
        return null;
    }

    private void Spawn(string id)
    {
        if (spawner == null || string.IsNullOrEmpty(id)) return;
        spawner.SpawnById(id);
    }
}
