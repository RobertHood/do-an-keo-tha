using UnityEngine;

/// <summary>
/// Attached to any 3D model that can be clicked and dragged around the field.
/// Automatically added by SpawnInModel, or can be manually added to prefabs in the editor.
/// </summary>
[SelectionBase]
[AddComponentMenu("Interaction/Draggable Model")]
public class DraggableModel : MonoBehaviour
{
    [Tooltip("ID of the prefab this model was spawned from (used to rebuild it when loading a blueprint).")]
    public string prefabId;

    [Tooltip("Optional custom height lift when dragging this specific model.")]
    public float liftOffset = 0f;

    [Tooltip("If true, this model cannot be moved.")]
    public bool isLocked = false;

    [Header("Pricing")]
    [Tooltip("Store chosen in the market prices panel. Empty = cheapest offer.")]
    public string selectedStore;

    [Tooltip("If true, customPrice is used instead of market prices.")]
    public bool useCustomPrice = false;

    [Tooltip("Price typed in by the user.")]
    public double customPrice;

    /// <summary>
    /// Raised whenever a model appears or disappears (spawn, delete, import), so ModelEstPrice can refresh.
    /// </summary>
    public static event System.Action ModelsChanged;

    private void OnEnable() => ModelsChanged?.Invoke();
    private void OnDisable() => ModelsChanged?.Invoke();
}

