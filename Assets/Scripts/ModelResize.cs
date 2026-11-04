using UnityEngine;

/// <summary>
/// Allows resizing a selected 3D model with a fixed aspect ratio (uniform scale).
/// Features:
/// - Displays a rectangular bounding box around the selected model using a LineRenderer.
/// - Places 8 corner handles that stay the same size on screen at any zoom level.
/// - Handles are picked in screen space, so they can be grabbed even when the model is in front of them.
/// - Hover a handle to highlight it, click-and-drag to scale. The model scales around its bottom center,
///   so it stays planted on the ground and doesn't drift sideways.
/// - Hold Shift to snap to 10% steps. Press Esc while dragging to cancel.
/// - Procedural: automatically builds the visual lines and handle meshes if not assigned.
/// </summary>
[AddComponentMenu("Interaction/Model Resize")]
public class ModelResize : MonoBehaviour
{
    public static ModelResize Instance { get; private set; }

    [Header("Target Model")]
    [Tooltip("The currently active model to resize. Follows the selected model from ModelMovement.")]
    [SerializeField] private Transform targetModel;

    [Tooltip("Whether resize mode is currently active.")]
    [SerializeField] private bool isResizeModeActive = false;

    [Header("Bounding Box Visuals")]
    [Tooltip("Color of the bounding box lines.")]
    [SerializeField] private Color boxColor = new Color(0.2f, 0.75f, 1f, 0.9f);

    [Tooltip("Thickness of the bounding box lines, in screen pixels.")]
    [SerializeField] private float lineWidthPixels = 2f;

    [Tooltip("Optional custom material for the lines. If empty, a clean unlit material is created.")]
    [SerializeField] private Material lineMaterial;

    [Header("Corner Handles")]
    [Tooltip("Size of the corner handle cubes, in screen pixels.")]
    [SerializeField] private float handleSizePixels = 12f;

    [Tooltip("How close (in screen pixels) the cursor must be to a handle to grab it.")]
    [SerializeField] private float handlePickRadius = 18f;

    [Tooltip("Normal color of the corner handles.")]
    [SerializeField] private Color handleColor = Color.white;

    [Tooltip("Color of the handle when hovered or dragged.")]
    [SerializeField] private Color handleHighlightColor = new Color(1f, 0.85f, 0.2f, 1f);

    [Tooltip("Optional custom material for handles. If empty, a clean material is generated.")]
    [SerializeField] private Material handleMaterial;

    [Header("Scale Constraints")]
    [Tooltip("Smallest allowed size of the model's largest dimension, in world units.")]
    [SerializeField] private float minModelSize = 0.1f;

    [Tooltip("Largest allowed size of the model's largest dimension, in world units.")]
    [SerializeField] private float maxModelSize = 100f;

    [Tooltip("Scale snap step when holding Shift (0.1 = 10% steps, 0 = disabled).")]
    [SerializeField] private float snapScaleStep = 0.1f;

    [Header("Camera Reference")]
    [Tooltip("Main camera. If empty, automatically assigned.")]
    [SerializeField] private UnityEngine.Camera targetCamera;

    // Procedural Rig Elements
    private GameObject rigRoot;
    private LineRenderer lineRenderer;
    private readonly Transform[] cornerHandles = new Transform[8];
    private readonly Material[] handleMatInstances = new Material[8];

    // Cached geometry (reused every frame to avoid allocations)
    private readonly Vector3[] localCorners = new Vector3[8];
    private readonly Vector3[] worldCorners = new Vector3[8];
    private readonly Vector3[] linePoints = new Vector3[16];
    private Bounds localBounds;

    // Drag tracking state
    private bool isDraggingHandle = false;
    private int activeHandleIndex = -1;
    private int hoveredHandleIndex = -1;
    private int highlightedHandleIndex = -1;
    private Vector3 scalePivot;
    private Vector3 initialScale;
    private Vector3 initialPosition;
    private float initialPointerDist;
    private float initialMaxDimension;

    public bool IsResizeModeActive => isResizeModeActive;
    public bool IsDraggingHandle => isDraggingHandle;
    public Transform TargetModel => targetModel;

    private UnityEngine.Camera Cam => targetCamera != null ? targetCamera : UnityEngine.Camera.main;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(this);
            return;
        }

        if (targetCamera == null)
        {
            targetCamera = UnityEngine.Camera.main != null ? UnityEngine.Camera.main : FindAnyObjectByType<UnityEngine.Camera>();
        }

        BuildProceduralRig();
        SetRigVisibility(false);
    }

    private void Update()
    {
        SyncTargetWithSelection();

        if (!isResizeModeActive || targetModel == null)
        {
            if (isDraggingHandle) EndHandleDrag();
            if (rigRoot != null && rigRoot.activeSelf)
            {
                SetRigVisibility(false);
            }
            return;
        }

        if (!rigRoot.activeSelf)
        {
            SetRigVisibility(true);
        }

        HandleInput();

        if (isDraggingHandle)
        {
            UpdateHandleDrag();
        }

        UpdateBoundingBoxTransforms();
        UpdateHandleHighlight();
    }

    /// <summary>
    /// Toggles the resize bounding box on/off. (Can be hooked up to UI Button OnClick).
    /// </summary>
    public void ToggleResizeMode()
    {
        EnableResizeMode(!isResizeModeActive);
    }

    /// <summary>
    /// Explicitly enables or disables resize mode.
    /// </summary>
    public void EnableResizeMode(bool enable)
    {
        isResizeModeActive = enable;

        if (isResizeModeActive)
        {
            // Close Rotate mode if active so gizmos don't conflict
            if (ModelRotate.Instance != null && ModelRotate.Instance.IsRotateModeActive)
            {
                ModelRotate.Instance.EnableRotateMode(false);
            }

            // Always use the current selection so the box never opens on a previously selected model
            if (ModelMovement.Instance != null && ModelMovement.Instance.SelectedModel != null)
            {
                targetModel = ModelMovement.Instance.SelectedModel;
            }

            if (targetModel != null)
            {
                localBounds = GizmoUtility.GetLocalBounds(targetModel);
                SetRigVisibility(true);
                UpdateBoundingBoxTransforms();
                Debug.Log($"[ModelResize] Resize mode enabled for: '{targetModel.name}'");
            }
            else
            {
                Debug.LogWarning("[ModelResize] No model selected to resize! Click on a model first.");
                isResizeModeActive = false;
                SetRigVisibility(false);
            }
        }
        else
        {
            if (isDraggingHandle) EndHandleDrag();
            hoveredHandleIndex = -1;
            SetRigVisibility(false);
            Debug.Log("[ModelResize] Resize mode disabled.");
        }
    }

    /// <summary>
    /// Sets the active model to be resized.
    /// </summary>
    public void SetTarget(Transform model)
    {
        if (isDraggingHandle) EndHandleDrag();

        targetModel = model;
        if (targetModel != null)
        {
            localBounds = GizmoUtility.GetLocalBounds(targetModel);
            if (isResizeModeActive)
            {
                SetRigVisibility(true);
                UpdateBoundingBoxTransforms();
            }
        }
    }

    /// <summary>
    /// True if the cursor is over one of the corner handles. ModelMovement uses this so clicking a handle
    /// never picks up or deselects the model underneath it.
    /// </summary>
    public bool IsPointerOverHandle(Vector2 screenPos)
    {
        if (!isResizeModeActive || targetModel == null || rigRoot == null || !rigRoot.activeSelf) return false;
        return GetHandleUnderPointer(screenPos) >= 0;
    }

    private void SyncTargetWithSelection()
    {
        if (isDraggingHandle || ModelMovement.Instance == null) return;

        Transform selected = ModelMovement.Instance.SelectedModel;
        if (selected != null && selected != targetModel)
        {
            SetTarget(selected);
        }
    }

    private void HandleInput()
    {
        Vector2 pointerPos = GizmoUtility.GetPointerPosition();

        if (isDraggingHandle)
        {
            if (GizmoUtility.WasCancelPressed())
            {
                CancelHandleDrag();
            }
            else if (GizmoUtility.WasPointerReleased())
            {
                EndHandleDrag();
                Debug.Log($"[ModelResize] Resizing completed. Final scale: {targetModel.localScale}");
            }
            return;
        }

        hoveredHandleIndex = GizmoUtility.IsPointerOverUI() ? -1 : GetHandleUnderPointer(pointerPos);

        if (GizmoUtility.WasPointerPressed() && hoveredHandleIndex >= 0)
        {
            StartHandleDrag(hoveredHandleIndex, pointerPos);
        }
    }

    /// <summary>
    /// Returns the index of the handle closest to the cursor within handlePickRadius, or -1.
    /// </summary>
    private int GetHandleUnderPointer(Vector2 screenPos)
    {
        UnityEngine.Camera cam = Cam;
        if (cam == null) return -1;

        int best = -1;
        float bestDist = handlePickRadius;

        for (int i = 0; i < 8; i++)
        {
            Vector3 sp = cam.WorldToScreenPoint(worldCorners[i]);
            if (sp.z <= 0f) continue; // Behind camera

            float d = Vector2.Distance(screenPos, sp);
            if (d <= bestDist)
            {
                bestDist = d;
                best = i;
            }
        }

        return best;
    }

    private void StartHandleDrag(int handleIndex, Vector2 pointerPos)
    {
        UnityEngine.Camera cam = Cam;
        if (cam == null || targetModel == null) return;

        if (targetModel.TryGetComponent(out DraggableModel dm) && dm.isLocked) return;

        isDraggingHandle = true;
        activeHandleIndex = handleIndex;

        initialScale = targetModel.localScale;
        initialPosition = targetModel.position;

        // Scale around the bottom center so the model stays on the ground and centered in place
        scalePivot = targetModel.TransformPoint(new Vector3(localBounds.center.x, localBounds.min.y, localBounds.center.z));

        // Measure from where the user actually clicked, so the model doesn't jump on grab
        Vector2 pivotScreen = cam.WorldToScreenPoint(scalePivot);
        initialPointerDist = Mathf.Max(Vector2.Distance(pointerPos, pivotScreen), 10f);

        Vector3 worldSize = Vector3.Scale(localBounds.size, targetModel.lossyScale);
        initialMaxDimension = Mathf.Max(Mathf.Abs(worldSize.x), Mathf.Abs(worldSize.y), Mathf.Abs(worldSize.z), 0.0001f);
    }

    private void UpdateHandleDrag()
    {
        UnityEngine.Camera cam = Cam;
        if (cam == null || targetModel == null) return;

        // Ratio of how far the cursor is from the pivot now vs. when the drag started (screen space,
        // so it behaves the same from any camera angle)
        Vector2 pivotScreen = cam.WorldToScreenPoint(scalePivot);
        float ratio = Vector2.Distance(GizmoUtility.GetPointerPosition(), pivotScreen) / initialPointerDist;

        if (GizmoUtility.IsShiftHeld() && snapScaleStep > 0f)
        {
            ratio = Mathf.Max(snapScaleStep, Mathf.Round(ratio / snapScaleStep) * snapScaleStep);
        }

        float minRatio = minModelSize / initialMaxDimension;
        float maxRatio = maxModelSize / initialMaxDimension;
        ratio = Mathf.Clamp(ratio, minRatio, maxRatio);

        ApplyScaleRatio(ratio);
    }

    /// <summary>
    /// Scales uniformly around scalePivot: every point on the model moves away from / toward the pivot.
    /// </summary>
    private void ApplyScaleRatio(float ratio)
    {
        targetModel.localScale = initialScale * ratio;
        targetModel.position = scalePivot + (initialPosition - scalePivot) * ratio;
    }

    private void CancelHandleDrag()
    {
        if (targetModel != null) ApplyScaleRatio(1f);
        EndHandleDrag();
        Debug.Log("[ModelResize] Resize cancelled.");
    }

    private void EndHandleDrag()
    {
        isDraggingHandle = false;
        activeHandleIndex = -1;
    }

    /// <summary>
    /// Updates the LineRenderer and 8 corner handles to hug the model's current transform.
    /// </summary>
    private void UpdateBoundingBoxTransforms()
    {
        if (targetModel == null) return;

        Vector3 c = localBounds.center;
        Vector3 e = localBounds.extents;

        localCorners[0] = c + new Vector3(-e.x, -e.y, -e.z); // 0: Bottom Back Left
        localCorners[1] = c + new Vector3( e.x, -e.y, -e.z); // 1: Bottom Back Right
        localCorners[2] = c + new Vector3( e.x, -e.y,  e.z); // 2: Bottom Front Right
        localCorners[3] = c + new Vector3(-e.x, -e.y,  e.z); // 3: Bottom Front Left
        localCorners[4] = c + new Vector3(-e.x,  e.y, -e.z); // 4: Top Back Left
        localCorners[5] = c + new Vector3( e.x,  e.y, -e.z); // 5: Top Back Right
        localCorners[6] = c + new Vector3( e.x,  e.y,  e.z); // 6: Top Front Right
        localCorners[7] = c + new Vector3(-e.x,  e.y,  e.z); // 7: Top Front Left

        UnityEngine.Camera cam = Cam;
        int highlightIndex = isDraggingHandle ? activeHandleIndex : hoveredHandleIndex;

        for (int i = 0; i < 8; i++)
        {
            worldCorners[i] = targetModel.TransformPoint(localCorners[i]);

            if (cornerHandles[i] != null)
            {
                // Constant on-screen size; slightly larger when hovered/dragged
                float size = GizmoUtility.PixelsToWorld(cam, worldCorners[i], handleSizePixels);
                if (i == highlightIndex) size *= 1.35f;

                cornerHandles[i].position = worldCorners[i];
                cornerHandles[i].rotation = targetModel.rotation;
                cornerHandles[i].localScale = Vector3.one * size;
            }
        }

        // 16 continuous points to draw all 12 edges of a box with a single LineRenderer
        linePoints[0] = worldCorners[0]; linePoints[1] = worldCorners[1]; linePoints[2] = worldCorners[2];   // Bottom loop
        linePoints[3] = worldCorners[3]; linePoints[4] = worldCorners[0];
        linePoints[5] = worldCorners[4]; linePoints[6] = worldCorners[5]; linePoints[7] = worldCorners[6];   // Top loop
        linePoints[8] = worldCorners[7]; linePoints[9] = worldCorners[4];
        linePoints[10] = worldCorners[7]; linePoints[11] = worldCorners[3];                                  // Vertical edge 7-3
        linePoints[12] = worldCorners[2]; linePoints[13] = worldCorners[6];                                  // Vertical edge 2-6
        linePoints[14] = worldCorners[5]; linePoints[15] = worldCorners[1];                                  // Vertical edge 5-1

        if (lineRenderer != null)
        {
            float width = GizmoUtility.PixelsToWorld(cam, targetModel.TransformPoint(c), lineWidthPixels);
            lineRenderer.startWidth = width;
            lineRenderer.endWidth = width;
            lineRenderer.positionCount = 16;
            lineRenderer.SetPositions(linePoints);
        }
    }

    private void UpdateHandleHighlight()
    {
        int highlightIndex = isDraggingHandle ? activeHandleIndex : hoveredHandleIndex;
        if (highlightIndex == highlightedHandleIndex) return;

        if (highlightedHandleIndex >= 0 && handleMatInstances[highlightedHandleIndex] != null)
        {
            handleMatInstances[highlightedHandleIndex].color = handleColor;
        }
        if (highlightIndex >= 0 && handleMatInstances[highlightIndex] != null)
        {
            handleMatInstances[highlightIndex].color = handleHighlightColor;
        }

        highlightedHandleIndex = highlightIndex;
    }

    private void SetRigVisibility(bool visible)
    {
        if (rigRoot != null)
        {
            rigRoot.SetActive(visible);
        }
    }

    /// <summary>
    /// Procedurally constructs the LineRenderer and 8 corner handle GameObjects.
    /// </summary>
    private void BuildProceduralRig()
    {
        rigRoot = new GameObject("_ModelResizeRig");
        rigRoot.transform.SetParent(transform, false);

        // 1. Create LineRenderer
        GameObject lineObj = new GameObject("BoundingBoxLines");
        lineObj.transform.SetParent(rigRoot.transform, false);
        lineRenderer = lineObj.AddComponent<LineRenderer>();

        if (lineMaterial == null)
        {
            lineMaterial = GizmoUtility.CreateLineMaterial();
        }

        lineRenderer.material = lineMaterial;
        lineRenderer.startColor = boxColor;
        lineRenderer.endColor = boxColor;
        lineRenderer.useWorldSpace = true;
        lineRenderer.loop = false;
        lineRenderer.numCornerVertices = 2;
        lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lineRenderer.receiveShadows = false;

        // 2. Create 8 Corner Handles
        if (handleMaterial == null)
        {
            handleMaterial = GizmoUtility.CreateUnlitMaterial();
        }

        for (int i = 0; i < 8; i++)
        {
            GameObject handleObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            handleObj.name = $"CornerHandle_{i}";
            handleObj.transform.SetParent(rigRoot.transform, false);

            // Handles are picked in screen space, so they don't need colliders (which would also block
            // model drag raycasts and spawn overlap checks)
            Destroy(handleObj.GetComponent<Collider>());

            Renderer handleRenderer = handleObj.GetComponent<Renderer>();
            handleRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            handleRenderer.receiveShadows = false;

            Material matInst = new Material(handleMaterial);
            matInst.color = handleColor;
            handleMatInstances[i] = matInst;
            handleRenderer.material = matInst;

            cornerHandles[i] = handleObj.transform;
        }
    }
}
