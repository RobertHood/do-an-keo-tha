using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spawns 3D models onto the field for visualization and manipulation.
/// Features:
/// - Spawn prefabs via UI Buttons (by direct reference, by index, or by name).
/// - Smart placement: spawns in front of the camera (raycast center) when user has navigated away,
///   or defaults to the origin (0, 1, 0) when visible/preferred.
/// - Open-space collision avoidance: searches in an outward spiral so models never spawn inside each other.
/// - Auto-aligns models flush with the ground.
/// </summary>
public class SpawnInModel : MonoBehaviour
{
    public enum PlacementPreference
    {
        CameraViewCenter,   // Spawns where the camera is currently looking
        OriginIfVisible,    // Uses (0, 1, 0) if visible on screen; otherwise camera center
        AlwaysOrigin        // Always attempts (0, 1, 0) first
    }

    [Header("Prefabs")]
    [Tooltip("List of 3D model prefabs that can be spawned via buttons.")]
    [SerializeField] private List<GameObject> modelPrefabs = new List<GameObject>();

    [Header("Model Scaling")]
    [Tooltip("Multiplier applied to the model scale upon spawning (increase if models look too small).")]
    [SerializeField] private float spawnScaleMultiplier = 5f;

    [Tooltip("If enabled, automatically scales the model to match the Target Height regardless of original import scale.")]
    [SerializeField] private bool useNormalizedTargetHeight = false;

    [Tooltip("Target height in units if useNormalizedTargetHeight is enabled (e.g. 2 to 5 units).")]
    [SerializeField] private float targetHeight = 3f;

    [Header("Placement Settings")]
    [Tooltip("How to determine where new models should spawn.")]
    [SerializeField] private PlacementPreference placementMode = PlacementPreference.OriginIfVisible;

    [Tooltip("Default anchor point on the ground (e.g. center of the field).")]
    [SerializeField] private Vector3 defaultOrigin = new Vector3(0f, 0f, 0f);

    [Tooltip("Layer mask for the flat ground/floor.")]
    [SerializeField] private LayerMask groundLayer = ~0;

    [Tooltip("Layer mask for existing models/obstacles to avoid overlapping.")]
    [SerializeField] private LayerMask obstacleLayer = ~0;

    [Header("Open Space Search")]
    [Tooltip("Clearance radius required around each model.")]
    [SerializeField] private float clearanceRadius = 1.0f;

    [Tooltip("Step distance when searching outward for free space.")]
    [SerializeField] private float searchStep = 1.2f;

    [Tooltip("Maximum distance from the target point to search for open space.")]
    [SerializeField] private float maxSearchRadius = 15f;

    [Tooltip("Number of radial points checked per search ring.")]
    [SerializeField] private int pointsPerRing = 10;

    [Header("Camera Reference")]
    [Tooltip("Main camera reference. If empty, Camera.main is used.")]
    [SerializeField] private UnityEngine.Camera targetCamera;

    [Header("Parenting & Interaction")]
    [Tooltip("Optional transform parent for spawned models to keep the Hierarchy organized.")]
    [SerializeField] private Transform modelsContainer;

    [Tooltip("If true, automatically picks up the spawned model for dragging immediately.")]
    [SerializeField] private bool autoPickUpOnSpawn = false;

    public Transform ModelsContainer => modelsContainer;

    private void Awake()
    {
        if (targetCamera == null)
        {
            targetCamera = UnityEngine.Camera.main != null ? UnityEngine.Camera.main : FindAnyObjectByType<UnityEngine.Camera>();
        }

        if (modelsContainer == null)
        {
            GameObject container = GameObject.Find("SpawnedModels");
            if (container == null) container = new GameObject("SpawnedModels");
            modelsContainer = container.transform;
        }

        // Auto-ensure ModelMovement component is active in the scene
        if (FindAnyObjectByType<ModelMovement>() == null)
        {
            gameObject.AddComponent<ModelMovement>();
            Debug.Log("[SpawnInModel] Added ModelMovement component to " + gameObject.name);
        }

        // Auto-ensure ModelResize component is active in the scene
        if (FindAnyObjectByType<ModelResize>() == null)
        {
            gameObject.AddComponent<ModelResize>();
            Debug.Log("[SpawnInModel] Added ModelResize component to " + gameObject.name);
        }
    }

    /// <summary>
    /// Spawns a prefab by passing the GameObject directly (appears in Unity UI Button OnClick dropdown).
    /// </summary>
    public void SpawnPrefab(GameObject prefab)
    {
        Spawn(prefab);
    }

    /// <summary>
    /// Spawns a prefab and returns the instantiated GameObject (for code calls).
    /// </summary>
    public GameObject Spawn(GameObject prefab)
    {
        if (prefab == null)
        {
            Debug.LogWarning("[SpawnInModel] Cannot spawn null prefab.");
            return null;
        }

        Vector3 spawnPosition = CalculateSpawnPosition();
        GameObject instance = Instantiate(prefab, spawnPosition, Quaternion.identity, modelsContainer);
        instance.name = prefab.name;

        // Apply scale multiplier or target height before aligning bounds
        ApplyScale(instance, prefab);

        // Ensure the model sits flush on the ground based on its bounds
        AlignToBoundsBottom(instance, spawnPosition.y);

        // Ensure the model has a collider so mouse clicks can hit and drag it
        EnsureCollider(instance);

        // Attach DraggableModel component so ModelMovement identifies it
        if (!instance.TryGetComponent(out DraggableModel draggable))
        {
            draggable = instance.AddComponent<DraggableModel>();
        }
        draggable.prefabId = prefab.name;

        if (FindPrefab(prefab.name) == null)
        {
            Debug.LogWarning($"[SpawnInModel] '{prefab.name}' is not in the Model Prefabs list, so it can't be rebuilt when loading a blueprint. Add it to the list.");
        }

        Debug.Log($"[SpawnInModel] Successfully spawned '{prefab.name}' at: {instance.transform.position}");

        if (ModelMovement.Instance != null)
        {
            ModelMovement.Instance.SelectModel(instance.transform);
        }

        if (autoPickUpOnSpawn && ModelMovement.Instance != null)
        {
            ModelMovement.Instance.StartDragging(instance.transform, instance.transform.position);
        }

        return instance;
    }

    /// <summary>
    /// Spawns a prefab by its index in the modelPrefabs list (appears in Unity UI Button OnClick dropdown).
    /// </summary>
    public void SpawnByIndex(int index)
    {
        if (index < 0 || index >= modelPrefabs.Count)
        {
            Debug.LogWarning($"[SpawnInModel] Index {index} is out of range. List contains {modelPrefabs.Count} prefabs.");
            return;
        }

        Spawn(modelPrefabs[index]);
    }

    /// <summary>
    /// Spawns a prefab by matching its name in the modelPrefabs list (appears in Unity UI Button OnClick dropdown).
    /// </summary>
    public void SpawnByName(string prefabName)
    {
        GameObject found = FindPrefab(prefabName);
        if (found == null)
        {
            Debug.LogWarning($"[SpawnInModel] Prefab named '{prefabName}' not found in the list.");
            return;
        }

        Spawn(found);
    }

    /// <summary>
    /// Finds a prefab in the modelPrefabs list by name (case-insensitive). Returns null if not found.
    /// </summary>
    public GameObject FindPrefab(string prefabName)
    {
        if (string.IsNullOrEmpty(prefabName)) return null;
        return modelPrefabs.Find(p => p != null && p.name.Equals(prefabName, System.StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Rebuilds a model from saved blueprint data. Skips placement search, scaling and ground alignment
    /// so the model lands exactly where it was saved.
    /// </summary>
    public GameObject SpawnFromBlueprint(BlueprintModelData data)
    {
        GameObject prefab = FindPrefab(data.prefabId);
        if (prefab == null)
        {
            Debug.LogWarning($"[SpawnInModel] Cannot load '{data.prefabId}': prefab not found in the Model Prefabs list.");
            return null;
        }

        GameObject instance = Instantiate(prefab, modelsContainer);
        instance.name = prefab.name;
        instance.transform.localPosition = data.position;
        instance.transform.localRotation = data.rotation;
        instance.transform.localScale = data.scale;

        EnsureCollider(instance);

        if (!instance.TryGetComponent(out DraggableModel draggable))
        {
            draggable = instance.AddComponent<DraggableModel>();
        }
        draggable.prefabId = prefab.name;
        draggable.isLocked = data.isLocked;
        draggable.selectedStore = data.selectedStore;
        draggable.useCustomPrice = data.useCustomPrice;
        draggable.customPrice = data.customPrice;

        return instance;
    }

    /// <summary>
    /// Calculates the ideal open spawn position considering the camera and existing objects.
    /// </summary>
    private Vector3 CalculateSpawnPosition()
    {
        Vector3 candidatePos = defaultOrigin;
        UnityEngine.Camera cam = targetCamera != null ? targetCamera : UnityEngine.Camera.main;

        bool shouldUseCameraRay = false;

        switch (placementMode)
        {
            case PlacementPreference.CameraViewCenter:
                shouldUseCameraRay = true;
                break;

            case PlacementPreference.OriginIfVisible:
                // Check if the default origin is currently visible on camera
                if (cam != null)
                {
                    Vector3 viewPos = cam.WorldToViewportPoint(defaultOrigin);
                    bool isOriginVisible = viewPos.z > 0 && viewPos.x >= 0f && viewPos.x <= 1f && viewPos.y >= 0f && viewPos.y <= 1f;
                    
                    // If origin is off-screen (user moved away), use the camera raycast
                    shouldUseCameraRay = !isOriginVisible;
                }
                break;

            case PlacementPreference.AlwaysOrigin:
                shouldUseCameraRay = false;
                break;
        }

        // Attempt camera center raycast
        if (shouldUseCameraRay && cam != null)
        {
            Ray centerRay = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            if (Physics.Raycast(centerRay, out RaycastHit hit, 500f, groundLayer))
            {
                candidatePos = hit.point;
            }
            else
            {
                // If camera doesn't point at the ground, cast forward onto a flat ground plane at defaultOrigin.y
                Plane groundPlane = new Plane(Vector3.up, defaultOrigin);
                if (groundPlane.Raycast(centerRay, out float enter))
                {
                    candidatePos = centerRay.GetPoint(Mathf.Min(enter, 30f));
                }
                else
                {
                    candidatePos = defaultOrigin;
                }
            }
        }

        // Find nearest open space around candidatePos to avoid overlapping existing models
        return FindNearestOpenSpace(candidatePos);
    }

    /// <summary>
    /// Performs an outward spiral search to find a clear location free of obstacles.
    /// </summary>
    private Vector3 FindNearestOpenSpace(Vector3 center)
    {
        // 1. Check if the center position itself is already open
        if (IsOpenSpace(center))
        {
            return center;
        }

        // 2. Outward spiral rings
        float currentRadius = searchStep;
        while (currentRadius <= maxSearchRadius)
        {
            int samples = Mathf.Max(6, Mathf.RoundToInt(pointsPerRing * (currentRadius / searchStep)));
            float angleStep = 360f / samples;

            for (int i = 0; i < samples; i++)
            {
                float angle = i * angleStep * Mathf.Deg2Rad;
                Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * currentRadius;
                Vector3 testPos = center + offset;

                // Snap test position to the actual ground height via raycast
                if (Physics.Raycast(testPos + Vector3.up * 20f, Vector3.down, out RaycastHit hit, 50f, groundLayer))
                {
                    testPos = hit.point;
                }

                if (IsOpenSpace(testPos))
                {
                    return testPos;
                }
            }

            currentRadius += searchStep;
        }

        // If the entire search area is packed, fall back to center with a minor offset
        return center + new Vector3(Random.Range(-0.5f, 0.5f), 0f, Random.Range(-0.5f, 0.5f));
    }

    /// <summary>
    /// Returns true if no colliders on the obstacleLayer intersect the sphere at the given position.
    /// </summary>
    private bool IsOpenSpace(Vector3 position)
    {
        // Sphere check slightly raised above ground
        Vector3 checkPos = position + Vector3.up * (clearanceRadius * 0.5f);
        Collider[] hits = Physics.OverlapSphere(checkPos, clearanceRadius, obstacleLayer);

        foreach (var col in hits)
        {
            // Ignore ground colliders during obstacle checks
            if ((groundLayer.value & (1 << col.gameObject.layer)) == 0)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Applies scaling based on spawnScaleMultiplier or normalizes to targetHeight.
    /// </summary>
    private void ApplyScale(GameObject instance, GameObject prefab)
    {
        if (useNormalizedTargetHeight)
        {
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                Bounds combined = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                {
                    combined.Encapsulate(renderers[i].bounds);
                }

                float currentHeight = combined.size.y;
                if (currentHeight > 0.001f)
                {
                    float factor = targetHeight / currentHeight;
                    instance.transform.localScale = prefab.transform.localScale * factor;
                    return;
                }
            }
        }

        instance.transform.localScale = prefab.transform.localScale * Mathf.Max(0.01f, spawnScaleMultiplier);
    }

    /// <summary>
    /// Adjusts the spawned object's Y position so its lowest bounding point sits directly on the ground.
    /// </summary>
    private void AlignToBoundsBottom(GameObject obj, float groundY)
    {
        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;

        Bounds combinedBounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            combinedBounds.Encapsulate(renderers[i].bounds);
        }

        float bottomY = combinedBounds.min.y;
        float diff = groundY - bottomY;

        obj.transform.position += new Vector3(0f, diff, 0f);
    }

    /// <summary>
    /// Checks if the spawned object has any Colliders; if not, automatically generates a BoxCollider.
    /// </summary>
    private void EnsureCollider(GameObject instance)
    {
        Collider[] colliders = instance.GetComponentsInChildren<Collider>();
        if (colliders.Length == 0)
        {
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                Bounds combined = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                {
                    combined.Encapsulate(renderers[i].bounds);
                }

                BoxCollider box = instance.AddComponent<BoxCollider>();
                box.center = instance.transform.InverseTransformPoint(combined.center);
                Vector3 worldSize = combined.size;
                Vector3 lossy = instance.transform.lossyScale;
                box.size = new Vector3(
                    lossy.x != 0f ? Mathf.Abs(worldSize.x / lossy.x) : worldSize.x,
                    lossy.y != 0f ? Mathf.Abs(worldSize.y / lossy.y) : worldSize.y,
                    lossy.z != 0f ? Mathf.Abs(worldSize.z / lossy.z) : worldSize.z
                );
                Debug.Log($"[SpawnInModel] Auto-generated BoxCollider for '{instance.name}' to enable dragging.");
            }
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(defaultOrigin + Vector3.up * 0.5f, clearanceRadius);
    }
#endif
}
