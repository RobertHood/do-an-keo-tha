using UnityEngine;

public class PlacedItem : MonoBehaviour
{
    public string itemName;
    public float price;

    void Start()
    {
        if (FurnitureTotalTracker.Instance != null)
            FurnitureTotalTracker.Instance.RegisterItem(this);
    }

    void OnDestroy()
    {
        if (FurnitureTotalTracker.Instance != null)
            FurnitureTotalTracker.Instance.UnregisterItem(this);
    }
}
