using System;
using UnityEngine;

[Serializable]
public class FurnitureItem
{
    [Tooltip("Name shown under the catalog button.")]
    public string displayName = "Item";

    [Tooltip("Category this item belongs to. The catalog shows one tab per category (e.g. Plants, Furniture, Lighting, Decor).")]
    public string category = "Plants";

    [Tooltip("Prefab that gets spawned into the room when this item is chosen.")]
    public GameObject prefab;

    [Tooltip("Optional icon shown on the catalog button. Leave empty to show a colored placeholder.")]
    public Sprite icon;

    [Tooltip("Accent color used when no icon is assigned.")]
    public Color color = new Color(0.35f, 0.6f, 0.9f, 1f);

    [Tooltip("Estimated price of this item. Summed up for the total expenditure display.")]
    public float price = 0f;
}
