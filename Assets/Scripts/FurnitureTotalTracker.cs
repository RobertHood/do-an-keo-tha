using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class FurnitureTotalTracker : MonoBehaviour
{
    public static FurnitureTotalTracker Instance;

    [Tooltip("Optional UI text that shows the running total. Assigned by the setup tool.")]
    public TextMeshProUGUI totalText;

    public string currencySymbol = "$";
    public string labelPrefix = "Total: ";

    private readonly List<PlacedItem> items = new List<PlacedItem>();

    public event Action ItemsChanged;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        UpdateUI();
    }

    public IReadOnlyList<PlacedItem> PlacedItems => items;

    public int PlacedCount => items.Count;

    public float TotalPrice
    {
        get
        {
            float total = 0f;
            foreach (PlacedItem item in items)
                total += item.price;
            return total;
        }
    }

    public void RegisterItem(PlacedItem item)
    {
        if (item == null || items.Contains(item))
            return;

        items.Add(item);
        UpdateUI();
        ItemsChanged?.Invoke();
    }

    public void UnregisterItem(PlacedItem item)
    {
        if (items.Remove(item))
        {
            UpdateUI();
            ItemsChanged?.Invoke();
        }
    }

    void UpdateUI()
    {
        if (totalText != null)
            totalText.text = $"{labelPrefix}{currencySymbol}{TotalPrice.ToString("0.00")}";
    }
}
