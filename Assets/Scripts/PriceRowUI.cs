using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// One clickable row in the price panels (an item in the item list, or a store in the market prices list).
/// Put this on a Button prefab and assign its text/image children. Only the Button is required;
/// every other reference is optional.
/// </summary>
[AddComponentMenu("UI/Price Row")]
public class PriceRowUI : MonoBehaviour
{
    [Tooltip("The row's Button. If empty, uses the Button on this GameObject.")]
    [SerializeField] private Button button;

    [Tooltip("Item name or store name.")]
    [SerializeField] private TMP_Text titleText;

    [Tooltip("Item description, or a tag like 'Cheapest' for store rows.")]
    [SerializeField] private TMP_Text subtitleText;

    [SerializeField] private TMP_Text priceText;

    [SerializeField] private Image iconImage;

    [Tooltip("Shown when this row is the selected item / chosen store (e.g. a highlight background or checkmark).")]
    [SerializeField] private GameObject selectedIndicator;

    public void Set(string title, string subtitle, string price, Sprite icon, bool selected, UnityAction onClick)
    {
        if (titleText != null) titleText.text = title;

        if (subtitleText != null)
        {
            subtitleText.text = subtitle;
            subtitleText.gameObject.SetActive(!string.IsNullOrEmpty(subtitle));
        }

        if (priceText != null) priceText.text = price;

        if (iconImage != null)
        {
            iconImage.sprite = icon;
            iconImage.gameObject.SetActive(icon != null);
        }

        if (selectedIndicator != null) selectedIndicator.SetActive(selected);

        if (button == null) button = GetComponent<Button>();
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            if (onClick != null) button.onClick.AddListener(onClick);
        }
    }
}
