using UnityEngine;

/// <summary>
/// Allows rotating a selected 3D model with a spherical bounding gizmo and interactive rotation ring.
/// Features:
/// - Displays a spherical bounding cage around the model with a prominent interactive equator ring.
/// - The ring is picked in screen space (pixels), so it's equally easy to grab from any angle or zoom level.
/// - Hover the ring to highlight it, click-and-drag around it to rotate (like Unity's rotation tool).
/// - The model spins around its visual center, and the ring stays a steady size while rotating.
/// - A pointer line shows which way the model is facing while dragging.
/// - Hold Shift to snap to absolute 15-degree angles (0, 15, 30...). Press Esc while dragging to cancel.
/// - Procedural: generates all lines and materials automatically at runtime.
/// - Integrates seamlessly with ModelMovement and ModelResize.
/// </summary>
[AddComponentMenu("Interaction/Model Rotate")]
public class ModelRotate : MonoBehaviour
{
    public static ModelRotate Instance { get; private set; }

    [Header("Target Model")]
    [Tooltip("The model to rotate. Follows the selected model from ModelMovement.")]
    [SerializeField] private Transform targetModel;

    [Tooltip("Whether rotate mode is currently active.")]
    [SerializeField] private bool isRotateModeActive = false;

    [Header("Rotation Ring (Interactive Equator)")]
    [Tooltip("Color of the interactive rotation ring.")]
    [SerializeField] private Color ringColor = new Color(0.2f, 0.9f, 0.4f, 0.95f);

    [Tooltip("Color of the ring when hovered or being dragged.")]
    [SerializeField] private Color ringHighlightColor = new Color(1f, 0.85f, 0.2f, 1f);

    [Tooltip("Width of the rotation ring line, in screen pixels.")]
    [SerializeField] private float ringLineWidthPixels = 3f;

    [Tooltip("How close (in screen pixels) the cursor must be to the ring to grab it.")]
    [SerializeField] private float ringPickTolerance = 14f;

    [Tooltip("Ring radius relative to the model's footprint (1 = touching the model's corners).")]
    [SerializeField] private float ringPadding = 1.1f;

    [Header("Spherical Bounding Cage")]
    [Tooltip("Color of the faint spherical bounding guide circles.")]
    [SerializeField] private Color sphereGuideColor = new Color(0.2f, 0.9f, 0.4f, 0.25f);

    [Tooltip("Width of the spherical guide lines, in screen pixels.")]
    [SerializeField] private float sphereGuideWidthPixels = 1.5f;

    [Tooltip("Optional custom material for lines. If empty, an unlit material is generated.")]
    [SerializeField] private Material lineMaterial;

    [Header("Rotation Settings")]
    [Tooltip("Angle snapping step in degrees when holding Shift (0 = disabled).")]
    [SerializeField] private float snapAngleStep = 15f;

    [Header("Camera Reference")]
    [Tooltip("Main camera. If empty, automatically assigned.")]
    [SerializeField] private UnityEngine.Camera targetCamera;

    // Procedural Rig Elements
    private GameObject rigRoot;
    private LineRenderer ringRenderer;
    private LineRenderer sphereMeridian1; // Vertical guide circle along the model's right axis
    private LineRenderer sphereMeridian2; // Vertical guide circle along the model's forward axis
    private LineRenderer anglePointerLine; // Radial line showing which way the model faces

    // Cached geometry (reused every frame to avoid allocations)
    private readonly Vector3[] ringPoints = new Vector3[CircleSegments + 1];
    private readonly Vector3[] circlePoints = new Vector3[CircleSegments + 1];
    private readonly Vector3[] pointerPoints = new Vector3[2];
    private Bounds localBounds;

    // State tracking
    private bool isDraggingRing = false;
    private bool isHoveringRing = false;
    private bool isRingHighlighted = false;
    private float sphereRadius = 1.5f;
    private Vector3 sphereCenter;

    // Drag state
    private bool dragUsesScreenAngle;
    private float previousAngle;
    private float accumulatedAngle;
    private Quaternion startRotation;
    private Vector3 startPosition;
    private float startYaw;

    private const int CircleSegments = 64;

    public bool IsRotateModeActive => isRotateModeActive;
    public bool IsDraggingRing => isDraggingRing;
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

        if (!isRotateModeActive || targetModel == null)
        {
            if (isDraggingRing) EndRingDrag();
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

        // Keep the ring steady while dragging; otherwise follow the model (e.g. while it's being moved)
        if (!isDraggingRing)
        {
            UpdateSphereDimensions();
        }

        UpdateRigTransforms();
        HandleInput();

        if (isDraggingRing)
        {
            UpdateRingDrag();
        }

        UpdateRingHighlight();
    }

    /// <summary>
    /// Toggles the rotation gizmo on/off. (Can be connected to a UI Button OnClick).
    /// </summary>
    public void ToggleRotateMode()
    {
        EnableRotateMode(!isRotateModeActive);
    }

    /// <summary>
    /// Explicitly enables or disables rotation mode.
    /// </summary>
    public void EnableRotateMode(bool enable)
    {
        isRotateModeActive = enable;

        if (isRotateModeActive)
        {
            // Close Resize mode if active so gizmos don't conflict
            if (ModelResize.Instance != null && ModelResize.Instance.IsResizeModeActive)
            {
                ModelResize.Instance.EnableResizeMode(false);
            }

            // Always use the current selection so the ring never opens on a previously selected model
            if (ModelMovement.Instance != null && ModelMovement.Instance.SelectedModel != null)
            {
                targetModel = ModelMovement.Instance.SelectedModel;
            }

            if (targetModel != null)
            {
                localBounds = GizmoUtility.GetLocalBounds(targetModel);
                UpdateSphereDimensions();
                SetRigVisibility(true);
                UpdateRigTransforms();
                Debug.Log($"[ModelRotate] Rotation mode enabled for: '{targetModel.name}'");
            }
            else
            {
                Debug.LogWarning("[ModelRotate] No model selected to rotate! Click on a model first.");
                isRotateModeActive = false;
                SetRigVisibility(false);
            }
        }
        else
        {
            if (isDraggingRing) EndRingDrag();
            isHoveringRing = false;
            SetRigVisibility(false);
            Debug.Log("[ModelRotate] Rotation mode disabled.");
        }
    }

    /// <summary>
    /// Sets the active model to rotate.
    /// </summary>
    public void SetTarget(Transform model)
    {
        if (isDraggingRing) EndRingDrag();

        targetModel = model;
        if (targetModel != null)
        {
            localBounds = GizmoUtility.GetLocalBounds(targetModel);
            UpdateSphereDimensions();
            if (isRotateModeActive)
            {
                SetRigVisibility(true);
                UpdateRigTransforms();
            }
        }
    }

    /// <summary>
    /// True if the cursor is over the rotation ring. ModelMovement uses this so clicking the ring
    /// never picks up or deselects the model.
    /// </summary>
    public bool IsPointerOverRing(Vector2 screenPos)
    {
        if (!isRotateModeActive || targetModel == null || rigRoot == null || !rigRoot.activeSelf) return false;
        return GetScreenDistanceToRing(screenPos) <= ringPickTolerance;
    }

    private void SyncTargetWithSelection()
    {
        if (isDraggingRing || ModelMovement.Instance == null) return;

        Transform selected = ModelMovement.Instance.SelectedModel;
        if (selected != null && selected != targetModel)
        {
            SetTarget(selected);
        }
    }

    private void HandleInput()
    {
        Vector2 pointerPos = GizmoUtility.GetPointerPosition();

        if (isDraggingRing)
        {
            if (GizmoUtility.WasCancelPressed())
            {
                CancelRingDrag();
            }
            else if (GizmoUtility.WasPointerReleased())
            {
                EndRingDrag();
                Debug.Log($"[ModelRotate] Rotation finished. Current Y angle: {targetModel.eulerAngles.y:F1}°");
            }
            return;
        }

        isHoveringRing = !GizmoUtility.IsPointerOverUI() && GetScreenDistanceToRing(pointerPos) <= ringPickTolerance;

        if (GizmoUtility.WasPointerPressed() && isHoveringRing)
        {
            StartRingDrag(pointerPos);
        }
    }

    /// <summary>
    /// Distance in pixels from the cursor to the nearest point on the drawn ring.
    /// </summary>
    private float GetScreenDistanceToRing(Vector2 screenPos)
    {
        UnityEngine.Camera cam = Cam;
        if (cam == null) return float.MaxValue;

        float best = float.MaxValue;
        Vector3 prev = cam.WorldToScreenPoint(ringPoints[0]);

        for (int i = 1; i < ringPoints.Length; i++)
        {
            Vector3 next = cam.WorldToScreenPoint(ringPoints[i]);
            if (prev.z > 0f && next.z > 0f)
            {
                best = Mathf.Min(best, GizmoUtility.DistanceToSegment(screenPos, prev, next));
            }
            prev = next;
        }

        return best;
    }

    private void StartRingDrag(Vector2 pointerPos)
    {
        UnityEngine.Camera cam = Cam;
        if (cam == null || targetModel == null) return;

        if (targetModel.TryGetComponent(out DraggableModel dm) && dm.isLocked) return;

        // Measure the cursor angle on the ring's horizontal plane so the ring tracks the cursor exactly.
        // If the camera is nearly level with the ring, that plane is unreliable, so fall back to screen space.
        Ray ray = cam.ScreenPointToRay(pointerPos);
        Plane ringPlane = new Plane(Vector3.up, sphereCenter);
        dragUsesScreenAngle = Mathf.Abs(ray.direction.y) < 0.15f || !ringPlane.Raycast(ray, out _);

        if (!TryGetPointerAngle(pointerPos, out previousAngle)) return;

        isDraggingRing = true;
        accumulatedAngle = 0f;
        startRotation = targetModel.rotation;
        startPosition = targetModel.position;
        startYaw = startRotation.eulerAngles.y;

        if (anglePointerLine != null)
        {
            anglePointerLine.enabled = true;
        }
    }

    private void UpdateRingDrag()
    {
        if (targetModel == null) return;

        if (TryGetPointerAngle(GizmoUtility.GetPointerPosition(), out float currentAngle))
        {
            accumulatedAngle += Mathf.DeltaAngle(previousAngle, currentAngle);
            previousAngle = currentAngle;
        }

        // Counter-clockwise cursor motion (seen from above) = counter-clockwise model rotation
        float desiredYaw = startYaw - accumulatedAngle;

        // Snap to absolute angles (0, 15, 30...) rather than steps relative to the start angle
        if (GizmoUtility.IsShiftHeld() && snapAngleStep > 0f)
        {
            desiredYaw = Mathf.Round(desiredYaw / snapAngleStep) * snapAngleStep;
        }

        ApplyYawDelta(desiredYaw - startYaw);
        UpdateRigTransforms();
    }

    /// <summary>
    /// Rotates the model around the ring center (its visual center), not its pivot, so it spins in place.
    /// </summary>
    private void ApplyYawDelta(float yawDelta)
    {
        Quaternion delta = Quaternion.AngleAxis(yawDelta, Vector3.up);
        Vector3 pivot = new Vector3(sphereCenter.x, startPosition.y, sphereCenter.z);

        targetModel.rotation = delta * startRotation;
        targetModel.position = pivot + delta * (startPosition - pivot);
    }

    /// <summary>
    /// Angle of the cursor around the ring center, in degrees (counter-clockwise seen from above).
    /// </summary>
    private bool TryGetPointerAngle(Vector2 pointerPos, out float angle)
    {
        angle = 0f;
        UnityEngine.Camera cam = Cam;
        if (cam == null) return false;

        if (!dragUsesScreenAngle)
        {
            Ray ray = cam.ScreenPointToRay(pointerPos);
            Plane ringPlane = new Plane(Vector3.up, sphereCenter);
            if (!ringPlane.Raycast(ray, out float enter)) return false;

            Vector3 offset = ray.GetPoint(enter) - sphereCenter;
            angle = Mathf.Atan2(offset.z, offset.x) * Mathf.Rad2Deg;
            return true;
        }

        Vector2 centerScreen = cam.WorldToScreenPoint(sphereCenter);
        Vector2 screenOffset = pointerPos - centerScreen;
        if (screenOffset.sqrMagnitude < 4f) return false; // Too close to center to read a stable angle

        angle = Mathf.Atan2(screenOffset.y, screenOffset.x) * Mathf.Rad2Deg;

        // Viewed from below, screen rotation direction is mirrored
        if (cam.transform.position.y < sphereCenter.y) angle = -angle;
        return true;
    }

    private void CancelRingDrag()
    {
        if (targetModel != null) ApplyYawDelta(0f);
        EndRingDrag();
        Debug.Log("[ModelRotate] Rotation cancelled.");
    }

    private void EndRingDrag()
    {
        isDraggingRing = false;

        if (anglePointerLine != null)
        {
            anglePointerLine.enabled = false;
        }
    }

    /// <summary>
    /// Calculates the center and radius of the ring from the model's local bounds. Unlike world bounds,
    /// these don't change as the model rotates, so the ring doesn't wobble.
    /// </summary>
    private void UpdateSphereDimensions()
    {
        if (targetModel == null) return;

        Vector3 size = Vector3.Scale(localBounds.size, targetModel.lossyScale);
        float footprintRadius = 0.5f * Mathf.Sqrt(size.x * size.x + size.z * size.z);

        sphereCenter = targetModel.TransformPoint(localBounds.center);
        sphereRadius = Mathf.Max(footprintRadius * ringPadding, 0.5f);
    }

    /// <summary>
    /// Updates all circle lines to follow the spherical bounding cage.
    /// </summary>
    private void UpdateRigTransforms()
    {
        if (targetModel == null) return;

        UnityEngine.Camera cam = Cam;

        // Model's facing direction, flattened onto the ground
        Vector3 forward = Vector3.ProjectOnPlane(targetModel.forward, Vector3.up);
        if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
        forward.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, forward);

        // Constant on-screen widths at any zoom level
        float ringWidth = GizmoUtility.PixelsToWorld(cam, sphereCenter, ringLineWidthPixels);
        float guideWidth = GizmoUtility.PixelsToWorld(cam, sphereCenter, sphereGuideWidthPixels);

        // 1. Interactive Equator Rotation Ring (Horizontal)
        FillCircle(ringPoints, sphereCenter, sphereRadius, right, forward);
        SetLine(ringRenderer, ringPoints, ringWidth);

        // 2. Spherical Bounding Meridians (Vertical, turn with the model)
        FillCircle(circlePoints, sphereCenter, sphereRadius, right, Vector3.up);
        SetLine(sphereMeridian1, circlePoints, guideWidth);

        FillCircle(circlePoints, sphereCenter, sphereRadius, forward, Vector3.up);
        SetLine(sphereMeridian2, circlePoints, guideWidth);

        // 3. Facing pointer
        if (anglePointerLine != null && anglePointerLine.enabled)
        {
            pointerPoints[0] = sphereCenter;
            pointerPoints[1] = sphereCenter + forward * sphereRadius;
            anglePointerLine.startWidth = ringWidth * 0.75f;
            anglePointerLine.endWidth = ringWidth * 0.75f;
            anglePointerLine.SetPositions(pointerPoints);
        }
    }

    private void FillCircle(Vector3[] points, Vector3 center, float radius, Vector3 axis1, Vector3 axis2)
    {
        float angleStep = 360f / CircleSegments;

        for (int i = 0; i <= CircleSegments; i++)
        {
            float rad = i * angleStep * Mathf.Deg2Rad;
            points[i] = center + (axis1 * Mathf.Cos(rad) + axis2 * Mathf.Sin(rad)) * radius;
        }
    }

    private void SetLine(LineRenderer lr, Vector3[] points, float width)
    {
        if (lr == null) return;

        lr.startWidth = width;
        lr.endWidth = width;
        lr.positionCount = points.Length;
        lr.SetPositions(points);
    }

    private void UpdateRingHighlight()
    {
        bool highlight = isDraggingRing || isHoveringRing;
        if (highlight == isRingHighlighted || ringRenderer == null) return;

        Color color = highlight ? ringHighlightColor : ringColor;
        ringRenderer.startColor = color;
        ringRenderer.endColor = color;
        isRingHighlighted = highlight;
    }

    private void SetRigVisibility(bool visible)
    {
        if (rigRoot != null)
        {
            rigRoot.SetActive(visible);
        }
    }

    /// <summary>
    /// Procedurally builds the rotation ring, spherical guide lines, and angle pointer.
    /// </summary>
    private void BuildProceduralRig()
    {
        rigRoot = new GameObject("_ModelRotateRig");
        rigRoot.transform.SetParent(transform, false);

        if (lineMaterial == null)
        {
            lineMaterial = GizmoUtility.CreateLineMaterial();
        }

        // 1. Interactive Horizontal Equator Ring
        ringRenderer = CreateLineObject("RotationRing_Equator", ringColor);

        // 2. Vertical Spherical Guides
        sphereMeridian1 = CreateLineObject("SphereMeridian_Right", sphereGuideColor);
        sphereMeridian2 = CreateLineObject("SphereMeridian_Forward", sphereGuideColor);

        // 3. Facing Pointer Line
        anglePointerLine = CreateLineObject("AnglePointerLine", ringHighlightColor);
        anglePointerLine.positionCount = 2;
        anglePointerLine.enabled = false;
    }

    private LineRenderer CreateLineObject(string name, Color color)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(rigRoot.transform, false);
        LineRenderer lr = obj.AddComponent<LineRenderer>();

        lr.material = lineMaterial;
        lr.startColor = color;
        lr.endColor = color;
        lr.useWorldSpace = true;
        lr.loop = false; // Circles are closed by their last point, so no loop needed
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;

        return lr;
    }
}
