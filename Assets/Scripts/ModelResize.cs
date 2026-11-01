using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Allows resizing a selected 3D model with a fixed aspect ratio (uniform scale).
/// Features:
/// - Displays a rectangular bounding box around the selected model using a LineRenderer.
/// - Places 8 interactive corner handle buttons around the bounding box.
/// - Click-and-drag any corner handle to scale the model up or down smoothly.
/// - Keeps the bottom of the model flush on the ground while scaling.
/// - Procedural: automatically builds the visual lines and handle meshes if not assigned.
/// </summary>
[AddComponentMenu("Interaction/Model Resize")]
public class ModelResize : MonoBehaviour
{
    public static ModelResize Instance { get; private set; }

    [Header("Target Model")]
    [Tooltip("The currently active model to resize. If empty, uses the last selected model from ModelMovement.")]
    [SerializeField] private Transform targetModel;

    [Tooltip("Whether resize mode is currently active.")]
    [SerializeField] private bool isResizeModeActive = false;

    [Header("Bounding Box Visuals")]
    [Tooltip("Color of the bounding box lines.")]
    [SerializeField] private Color boxColor = new Color(0.2f, 0.75f, 1f, 0.9f);

    [Tooltip("Thickness of the bounding box lines.")]
    [SerializeField] private float lineWidth = 0.04f;

    [Tooltip("Optional custom material for the lines. If empty, a clean unlit material is created.")]
    [SerializeField] private Material lineMaterial;

    [Header("Corner Handles")]
    [Tooltip("Size of the corner handle cubes.")]
    [SerializeField] private float handleSize = 0.18f;

    [Tooltip("Normal color of the corner handles.")]
    [SerializeField] private Color handleColor = Color.white;

    [Tooltip("Color of the handle when hovered or dragged.")]
    [SerializeField] private Color handleHighlightColor = new Color(1f, 0.85f, 0.2f, 1f);

    [Tooltip("Optional custom material for handles. If empty, a clean material is generated.")]
    [SerializeField] private Material handleMaterial;

    [Header("Scale Constraints")]
    [Tooltip("Minimum allowed uniform scale multiplier relative to original scale.")]
    [SerializeField] private float minScaleRatio = 0.2f;

    [Tooltip("Maximum allowed uniform scale multiplier relative to original scale.")]
    [SerializeField] private float maxScaleRatio = 5.0f;

    [Header("Camera Reference")]
    [Tooltip("Main camera. If empty, automatically assigned.")]
    [SerializeField] private UnityEngine.Camera targetCamera;

    // Procedural Rig Elements
    private GameObject rigRoot;
    private LineRenderer lineRenderer;
    private readonly Transform[] cornerHandles = new Transform[8];
    private readonly Material[] handleMatInstances = new Material[8];

    // Drag tracking state
    private bool isDraggingHandle = false;
    private int activeHandleIndex = -1;
    private Plane dragPlane;
    private Vector3 initialCenter;
    private Vector3 initialScale;
    private float initialGrabDist;
    private float initialBottomY;
    private Bounds localBounds;

    public bool IsResizeModeActive => isResizeModeActive;
    public bool IsDraggingHandle => isDraggingHandle;
    public Transform TargetModel => targetModel;

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
        // Auto-sync target with ModelMovement if target is null
        if (targetModel == null && ModelMovement.Instance != null && ModelMovement.Instance.SelectedModel != null)
        {
            targetModel = ModelMovement.Instance.SelectedModel;
        }

        if (!isResizeModeActive || targetModel == null)
        {
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
        else
        {
            UpdateBoundingBoxTransforms();
        }
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
            // If target is missing, check ModelMovement
            if (targetModel == null && ModelMovement.Instance != null)
            {
                targetModel = ModelMovement.Instance.SelectedModel;
            }

            if (targetModel != null)
            {
                RecalculateLocalBounds();
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
            isDraggingHandle = false;
            activeHandleIndex = -1;
            SetRigVisibility(false);
            Debug.Log("[ModelResize] Resize mode disabled.");
        }
    }

    /// <summary>
    /// Sets the active model to be resized.
    /// </summary>
    public void SetTarget(Transform model)
    {
        targetModel = model;
        if (targetModel != null)
        {
            RecalculateLocalBounds();
            if (isResizeModeActive)
            {
                SetRigVisibility(true);
                UpdateBoundingBoxTransforms();
            }
        }
    }

    private void HandleInput()
    {
        bool pointerDown = false;
        bool pointerUp = false;
        Vector2 pointerPos = Vector2.zero;

#if ENABLE_INPUT_SYSTEM
        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            pointerDown = mouse.leftButton.wasPressedThisFrame;
            pointerUp = mouse.leftButton.wasReleasedThisFrame;
            pointerPos = mouse.position.ReadValue();
        }
#else
        pointerDown = Input.GetMouseButtonDown(0);
        pointerUp = Input.GetMouseButtonUp(0);
        pointerPos = Input.mousePosition;
#endif

        if (pointerDown)
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            TryGrabHandle(pointerPos);
        }

        if (pointerUp && isDraggingHandle)
        {
            StopHandleDrag();
        }
    }

    private void TryGrabHandle(Vector2 screenPos)
    {
        UnityEngine.Camera cam = targetCamera != null ? targetCamera : UnityEngine.Camera.main;
        if (cam == null) return;

        Ray ray = cam.ScreenPointToRay(screenPos);
        if (Physics.Raycast(ray, out RaycastHit hit, 500f))
        {
            for (int i = 0; i < 8; i++)
            {
                if (cornerHandles[i] != null && hit.transform == cornerHandles[i])
                {
                    StartHandleDrag(i);
                    return;
                }
            }
        }
    }

    private void StartHandleDrag(int handleIndex)
    {
        isDraggingHandle = true;
        activeHandleIndex = handleIndex;

        initialScale = targetModel.localScale;
        initialCenter = targetModel.TransformPoint(localBounds.center);
        initialBottomY = GetBottomWorldY(targetModel);

        Transform handle = cornerHandles[handleIndex];
        initialGrabDist = Vector3.Distance(handle.position, initialCenter);
        if (initialGrabDist < 0.001f) initialGrabDist = 0.001f;

        // Create a dragging plane facing the camera through the handle
        UnityEngine.Camera cam = targetCamera != null ? targetCamera : UnityEngine.Camera.main;
        Vector3 planeNormal = cam != null ? -cam.transform.forward : Vector3.up;
        dragPlane = new Plane(planeNormal, handle.position);

        // Highlight active handle
        if (handleMatInstances[handleIndex] != null)
        {
            handleMatInstances[handleIndex].color = handleHighlightColor;
        }

        Debug.Log($"[ModelResize] Dragging corner handle #{handleIndex}");
    }

    private void UpdateHandleDrag()
    {
        if (targetModel == null || activeHandleIndex < 0) return;

        UnityEngine.Camera cam = targetCamera != null ? targetCamera : UnityEngine.Camera.main;
        if (cam == null) return;

        Vector2 pointerPos = GetPointerPos();
        Ray ray = cam.ScreenPointToRay(pointerPos);

        if (dragPlane.Raycast(ray, out float enter))
        {
            Vector3 currentPoint = ray.GetPoint(enter);
            float currentDist = Vector3.Distance(currentPoint, initialCenter);

            // Calculate uniform scale ratio (preserves 1:1:1 aspect ratio)
            float ratio = currentDist / initialGrabDist;
            ratio = Mathf.Clamp(ratio, minScaleRatio, maxScaleRatio);

            // Apply uniform scale
            targetModel.localScale = initialScale * ratio;

            // Re-align bottom flush with ground so scaling expands upwards and outwards
            float currentBottomY = GetBottomWorldY(targetModel);
            float yDiff = initialBottomY - currentBottomY;
            targetModel.position += new Vector3(0f, yDiff, 0f);

            // Update visuals to match new size
            UpdateBoundingBoxTransforms();
        }
    }

    private void StopHandleDrag()
    {
        if (activeHandleIndex >= 0 && handleMatInstances[activeHandleIndex] != null)
        {
            handleMatInstances[activeHandleIndex].color = handleColor;
        }

        isDraggingHandle = false;
        activeHandleIndex = -1;
        Debug.Log($"[ModelResize] Resizing completed. Final scale: {targetModel.localScale}");
    }

    /// <summary>
    /// Computes the model's bounds in its own local coordinate space to handle any rotation cleanly.
    /// </summary>
    private void RecalculateLocalBounds()
    {
        if (targetModel == null) return;

        Renderer[] renderers = targetModel.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            localBounds = new Bounds(Vector3.zero, Vector3.one);
            return;
        }

        // Initialize local bounds from first renderer
        Bounds firstBounds = renderers[0].bounds;
        Vector3 min = targetModel.InverseTransformPoint(firstBounds.min);
        Vector3 max = targetModel.InverseTransformPoint(firstBounds.max);

        Bounds combinedLocal = new Bounds(min, Vector3.zero);
        combinedLocal.Encapsulate(min);
        combinedLocal.Encapsulate(max);

        for (int i = 0; i < renderers.Length; i++)
        {
            Bounds b = renderers[i].bounds;
            Vector3[] worldCorners = GetBoundsCorners(b);
            for (int c = 0; c < 8; c++)
            {
                combinedLocal.Encapsulate(targetModel.InverseTransformPoint(worldCorners[c]));
            }
        }

        localBounds = combinedLocal;
    }

    /// <summary>
    /// Updates the LineRenderer and 8 corner handles to hug the model's current transform.
    /// </summary>
    private void UpdateBoundingBoxTransforms()
    {
        if (targetModel == null) return;

        Vector3 center = localBounds.center;
        Vector3 extents = localBounds.extents;

        // 8 Corners in target's local space
        Vector3[] localCorners = new Vector3[8]
        {
            center + new Vector3(-extents.x, -extents.y, -extents.z), // 0: Bottom Back Left
            center + new Vector3( extents.x, -extents.y, -extents.z), // 1: Bottom Back Right
            center + new Vector3( extents.x, -extents.y,  extents.z), // 2: Bottom Front Right
            center + new Vector3(-extents.x, -extents.y,  extents.z), // 3: Bottom Front Left
            center + new Vector3(-extents.x,  extents.y, -extents.z), // 4: Top Back Left
            center + new Vector3( extents.x,  extents.y, -extents.z), // 5: Top Back Right
            center + new Vector3( extents.x,  extents.y,  extents.z), // 6: Top Front Right
            center + new Vector3(-extents.x,  extents.y,  extents.z)  // 7: Top Front Left
        };

        // Convert corners to world space
        Vector3[] worldCorners = new Vector3[8];
        for (int i = 0; i < 8; i++)
        {
            worldCorners[i] = targetModel.TransformPoint(localCorners[i]);

            // Position each corner handle
            if (cornerHandles[i] != null)
            {
                cornerHandles[i].position = worldCorners[i];
                cornerHandles[i].rotation = targetModel.rotation;
                cornerHandles[i].localScale = Vector3.one * handleSize;
            }
        }

        // 16 continuous points to draw all 12 edges of a box with a single LineRenderer
        Vector3[] linePoints = new Vector3[16]
        {
            worldCorners[0], worldCorners[1], worldCorners[2], worldCorners[3], worldCorners[0], // Bottom loop
            worldCorners[4], worldCorners[5], worldCorners[6], worldCorners[7], worldCorners[4], // Top loop
            worldCorners[7], worldCorners[3],                                                    // Vertical edge 7-3
            worldCorners[2], worldCorners[6],                                                    // Vertical edge 2-6
            worldCorners[5], worldCorners[1]                                                     // Vertical edge 5-1
        };

        if (lineRenderer != null)
        {
            lineRenderer.positionCount = 16;
            lineRenderer.SetPositions(linePoints);
        }
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
            Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
            if (unlitShader == null) unlitShader = Shader.Find("Sprites/Default");
            lineMaterial = new Material(unlitShader != null ? unlitShader : Shader.Find("Hidden/Internal-Colored"));
        }

        lineRenderer.material = lineMaterial;
        lineRenderer.startColor = boxColor;
        lineRenderer.endColor = boxColor;
        lineRenderer.startWidth = lineWidth;
        lineRenderer.endWidth = lineWidth;
        lineRenderer.useWorldSpace = true;
        lineRenderer.loop = false;
        lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lineRenderer.receiveShadows = false;

        // 2. Create 8 Corner Handles
        if (handleMaterial == null)
        {
            Shader handleShader = Shader.Find("Universal Render Pipeline/Unlit");
            if (handleShader == null) handleShader = Shader.Find("Sprites/Default");
            handleMaterial = new Material(handleShader != null ? handleShader : Shader.Find("Hidden/Internal-Colored"));
        }

        for (int i = 0; i < 8; i++)
        {
            GameObject handleObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            handleObj.name = $"CornerHandle_{i}";
            handleObj.transform.SetParent(rigRoot.transform, false);

            Material matInst = new Material(handleMaterial);
            matInst.color = handleColor;
            handleMatInstances[i] = matInst;
            handleObj.GetComponent<Renderer>().material = matInst;

            // Ensure collider is enabled for mouse clicks
            BoxCollider col = handleObj.GetComponent<BoxCollider>();
            col.isTrigger = false;

            cornerHandles[i] = handleObj.transform;
        }
    }

    private float GetBottomWorldY(Transform model)
    {
        Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return model.position.y;

        float lowestY = float.MaxValue;
        foreach (var r in renderers)
        {
            if (r.bounds.min.y < lowestY) lowestY = r.bounds.min.y;
        }
        return lowestY;
    }

    private Vector3[] GetBoundsCorners(Bounds b)
    {
        Vector3 min = b.min;
        Vector3 max = b.max;
        return new Vector3[8]
        {
            new Vector3(min.x, min.y, min.z),
            new Vector3(max.x, min.y, min.z),
            new Vector3(max.x, min.y, max.z),
            new Vector3(min.x, min.y, max.z),
            new Vector3(min.x, max.y, min.z),
            new Vector3(max.x, max.y, min.z),
            new Vector3(max.x, max.y, max.z),
            new Vector3(min.x, max.y, max.z)
        };
    }

    private Vector2 GetPointerPos()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
#else
        return Input.mousePosition;
#endif
    }
}
