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

    private static int nextRuntimeId = 1;

    /// <summary>
    /// Unique ID for this model while the app runs (not saved). The web page uses it to select / edit a model.
    /// </summary>
    public int RuntimeId { get; private set; }

    private PriceCatalogEntry catalogEntry;
    private string catalogEntryId;
    private bool hasLocalBounds;
    private Bounds localBounds;

    /// <summary>
    /// This model's entry in the model library (type, front direction, door position), or null if it has none.
    /// </summary>
    public PriceCatalogEntry CatalogEntry
    {
        get
        {
            if (catalogEntryId != prefabId)
            {
                PriceCatalog catalog = ModelEstPrice.Instance != null ? ModelEstPrice.Instance.Catalog : null;
                catalogEntry = catalog != null ? catalog.FindEntry(prefabId) : null;
                catalogEntryId = catalogEntry != null ? prefabId : null; // Look again later if the catalog wasn't ready
            }
            return catalogEntry;
        }
    }

    /// <summary>
    /// What the model is for the phong thủy rules ("loai").
    /// </summary>
    public FengShuiType Loai
    {
        get
        {
            PriceCatalogEntry entry = CatalogEntry;
            return entry != null ? entry.ResolvedType : FengShuiTypes.Resolve(FengShuiType.Auto, ModelCategory.Uncategorized, prefabId);
        }
    }

    /// <summary>
    /// World direction the model's front faces (a house: its main door), flat on the ground.
    /// </summary>
    public Vector3 FrontDirection
    {
        get
        {
            PriceCatalogEntry entry = CatalogEntry;
            float offset = entry != null ? entry.frontYawOffset : 0f;
            Vector3 direction = transform.rotation * (Quaternion.Euler(0f, offset, 0f) * Vector3.forward);
            direction.y = 0f;
            return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
        }
    }

    /// <summary>
    /// Compass bearing of the model's front ("huong"): 0 = Bắc, 90 = Đông, 180 = Nam, 270 = Tây.
    /// </summary>
    public float Huong => CompassDirections.FromDirection(FrontDirection);

    /// <summary>
    /// Tight bounds in the model's own space (unscaled). Cached: they don't change when the model moves.
    /// </summary>
    public Bounds LocalBounds
    {
        get
        {
            if (!hasLocalBounds)
            {
                localBounds = GizmoUtility.GetLocalBounds(transform);
                hasLocalBounds = true;
            }
            return localBounds;
        }
    }

    /// <summary>
    /// Real size in metres as placed: x = width, y = height, z = depth (along the model's own axes).
    /// </summary>
    public Vector3 RealSize => Vector3.Scale(LocalBounds.size, transform.lossyScale);

    /// <summary>
    /// The 4 corners of the model's footprint on the ground, in world space (rotation and scale applied).
    /// </summary>
    public void GetFootprint(Vector3[] corners)
    {
        Bounds b = LocalBounds;
        corners[0] = transform.TransformPoint(new Vector3(b.min.x, b.min.y, b.min.z));
        corners[1] = transform.TransformPoint(new Vector3(b.max.x, b.min.y, b.min.z));
        corners[2] = transform.TransformPoint(new Vector3(b.max.x, b.min.y, b.max.z));
        corners[3] = transform.TransformPoint(new Vector3(b.min.x, b.min.y, b.max.z));
    }

    /// <summary>
    /// World position of a house's main door (from the catalog). False for models without a door.
    /// </summary>
    public bool TryGetDoorPosition(out Vector3 position)
    {
        PriceCatalogEntry entry = CatalogEntry;
        if (entry == null || !entry.hasDoor)
        {
            position = transform.position;
            return false;
        }

        position = transform.TransformPoint(entry.doorOffset);
        return true;
    }

    private void Awake() => RuntimeId = nextRuntimeId++;

    private void OnEnable() => ModelsChanged?.Invoke();
    private void OnDisable() => ModelsChanged?.Invoke();
}

