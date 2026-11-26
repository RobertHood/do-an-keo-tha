using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One model card in the item catalog: thumbnail, display name and price badge (cheapest store offer,
/// or the listed price). UICatalog clones the template card and calls Setup() for each model.
/// </summary>
[AddComponentMenu("UI/Catalog Card")]
public class CatalogCard : MonoBehaviour
{
    [Tooltip("Prefab this card spawns (matches PriceCatalog entries and blueprint prefab IDs).")]
    [SerializeField] private string prefabId;

    [SerializeField] private Image thumbnail;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text priceText;

    [Tooltip("The pill behind the price; tinted grey when the item has no price.")]
    [SerializeField] private Image priceBadge;

    [SerializeField] private string noPriceLabel = "Chưa có giá";

    private Sprite placeholderSprite;
    private bool placeholderSaved;

    public string PrefabId => prefabId;

    public Button Button => GetComponent<Button>();

    public void Configure(string id, Image thumbnailImage, TMP_Text nameLabel, TMP_Text priceLabel, Image badge)
    {
        prefabId = id;
        thumbnail = thumbnailImage;
        nameText = nameLabel;
        priceText = priceLabel;
        priceBadge = badge;
    }

    /// <summary>
    /// Shows this catalog entry on the card.
    /// </summary>
    public void Setup(PriceCatalogEntry entry)
    {
        prefabId = entry != null ? entry.Id : prefabId;
        SetThumbnail(entry != null ? entry.icon : null);
        Show(entry);
    }

    private void Start()
    {
        if (string.IsNullOrEmpty(prefabId)) return;
        PriceCatalog catalog = ModelEstPrice.Instance != null ? ModelEstPrice.Instance.Catalog : null;
        Show(catalog != null ? catalog.FindEntry(prefabId) : null);
    }

    private void Show(PriceCatalogEntry entry)
    {
        if (nameText != null)
        {
            nameText.text = entry != null && !string.IsNullOrEmpty(entry.displayName)
                ? entry.displayName
                : (prefabId ?? "").Replace('_', ' ');
        }

        if (priceText == null) return;

        var offers = entry != null ? ModelEstPrice.GetSortedOffers(entry) : null;
        double price = entry == null ? 0 : offers.Count > 0 ? offers[0].price : entry.listedPrice;

        if (entry == null || (offers.Count == 0 && price <= 0))
        {
            priceText.text = noPriceLabel;
            priceText.color = UIPalette.Neutral500;
            if (priceBadge != null) priceBadge.color = UIPalette.Neutral100;
            return;
        }

        priceText.text = (offers.Count > 1 ? "Từ " : "") + UIPalette.FormatVnd(price);
        priceText.color = UIPalette.BrandGreen;
        if (priceBadge != null) priceBadge.color = UIPalette.BrandGreenLight;
    }

    private void SetThumbnail(Sprite icon)
    {
        if (thumbnail == null) return;

        if (!placeholderSaved)
        {
            placeholderSprite = thumbnail.sprite;
            placeholderSaved = true;
        }

        if (icon != null)
        {
            thumbnail.sprite = icon;
            thumbnail.type = Image.Type.Simple;
            thumbnail.preserveAspect = true;
            thumbnail.color = Color.white;
        }
        else
        {
            thumbnail.sprite = placeholderSprite;
            thumbnail.type = placeholderSprite != null && placeholderSprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            thumbnail.preserveAspect = false;
            thumbnail.color = UIPalette.Neutral100;
        }
    }
}
