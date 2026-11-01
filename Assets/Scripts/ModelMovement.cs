using UnityEngine;
using UnityEngine.EventSystems;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Central manager for selecting, moving, rotating, and scaling 3D models across the field.
/// Features:
/// - Click-and-drag any model with a DraggableModel component or collider.
/// - Safe raycasting (never self-collides with the model being dragged).
/// - UI protection (ignores clicks over UI menus/buttons).
/// - Rotation with Scroll Wheel or R key.
/// - Scaling with [ / ] or - / + keys.
/// </summary>
[AddComponentMenu("Interaction/Model Movement")]
public class ModelMovement : MonoBehaviour
{
    public static ModelMovement Instance { get; private set; }

    [Header("Layer Masks")]
    [Tooltip("Layer mask for the flat ground/field.")]
    [SerializeField] private LayerMask groundLayer = ~0;

    [Tooltip("Layer mask for draggable models.")]
    [SerializeField] private LayerMask modelLayer = ~0;

    [Header("Drag Feel")]
    [Tooltip("If true, removes any mouse offset so the model centers directly on the cursor. If false, preserves grab point on the ground without jumping.")]
    [SerializeField] private bool snapToCursorCenter = true;

    [Tooltip("Smoothing speed for following the pointer (0 = instant snap to cursor, higher = smoother).")]
    [SerializeField] private float followSmoothing = 30f;

    [Tooltip("Height to elevate the model while moving (0 = flat on ground with zero vertical offset).")]
    [SerializeField] private float dragLiftHeight = 0f;

    [Header("Rotation & Scaling While Dragging")]
    [Tooltip("Allow rotating the model while dragging.")]
    [SerializeField] private bool allowRotation = true;

    [Tooltip("Rotation speed when scrolling.")]
    [SerializeField] private float rotationSpeed = 15f;

    [Tooltip("Allow scaling the model while dragging.")]
    [SerializeField] private bool allowScaling = true;

    [Tooltip("Scaling speed.")]
    [SerializeField] private float scaleSpeed = 1.5f;

    [Header("Horizontal Collision Avoidance (Scoot)")]
    [Tooltip("If true, scoot horizontally around other models instead of raising Y level over them.")]
    [SerializeField] private bool enableScootAvoidance = true;

    [Tooltip("Extra clearance distance when scooting away from another model.")]
    [SerializeField] private float scootPadding = 0.05f;

    [Tooltip("Number of iterations to resolve sliding smoothly around corners.")]
    [SerializeField] private int maxScootIterations = 3;

    [Header("References")]
    [Tooltip("Main camera. If empty, automatically finds camera.")]
    [SerializeField] private UnityEngine.Camera targetCamera;

    [Tooltip("Models container Transform. If set, any child object inside is recognized as a model.")]
    [SerializeField] private Transform modelsContainer;

    // Runtime state
    private Transform currentDraggedModel;
    private Transform selectedModel;
    private Vector3 dragOffset;
    private float baseGroundY;
    private Vector3 targetPosition;
    private bool isDragging;

    public bool IsDragging => isDragging;
    public Transform CurrentDraggedModel => currentDraggedModel;
    public Transform SelectedModel => selectedModel != null ? selectedModel : currentDraggedModel;

    private struct ColliderOffset
    {
        public Collider collider;
        public Vector3 localPosition;
        public Quaternion localRotation;
    }
    private readonly System.Collections.Generic.List<ColliderOffset> cachedColliderOffsets = new System.Collections.Generic.List<ColliderOffset>();

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

        if (modelsContainer == null)
        {
            GameObject container = GameObject.Find("SpawnedModels");
            if (container != null) modelsContainer = container.transform;
        }
    }

    private void Start()
    {
        Debug.Log("[ModelMovement] Initialized and ready to drag models.");
    }

    private void Update()
    {
        HandleInput();

        if (isDragging && currentDraggedModel != null)
        {
            UpdateDragPosition();
            HandleRotation();
            HandleScaling();
        }
    }

    private void HandleInput()
    {
        bool pointerDown = false;
        bool pointerUp = false;
        Vector2 pointerPosition = Vector2.zero;

#if ENABLE_INPUT_SYSTEM
        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            pointerDown = mouse.leftButton.wasPressedThisFrame;
            pointerUp = mouse.leftButton.wasReleasedThisFrame;
            pointerPosition = mouse.position.ReadValue();
        }
#else
        pointerDown = Input.GetMouseButtonDown(0);
        pointerUp = Input.GetMouseButtonUp(0);
        pointerPosition = Input.mousePosition;
#endif

        // 1. Try to select and pick up a model
        if (pointerDown)
        {
            // Do not pick up models if clicking on UI
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            // Do not pick up models if clicking on a resize handle
            if (ModelResize.Instance != null && ModelResize.Instance.IsDraggingHandle)
            {
                return;
            }

            TrySelectModel(pointerPosition);
        }

        // 2. Drop the model on mouse release
        if (pointerUp && isDragging)
        {
            DropModel();
        }
    }

    /// <summary>
    /// Searches for a draggable 3D model under the cursor.
    /// </summary>
    private void TrySelectModel(Vector2 screenPosition)
    {
        UnityEngine.Camera cam = targetCamera != null ? targetCamera : UnityEngine.Camera.main;
        if (cam == null) cam = FindAnyObjectByType<UnityEngine.Camera>();
        if (cam == null) return;

        Ray ray = cam.ScreenPointToRay(screenPosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, 1000f);

        if (hits.Length == 0) return;

        // Sort by distance (closest hit first)
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (var hit in hits)
        {
            Transform hitTransform = hit.transform;

            // Priority 1: Check if this object has the DraggableModel marker
            DraggableModel draggable = hitTransform.GetComponentInParent<DraggableModel>();
            if (draggable != null)
            {
                if (!draggable.isLocked)
                {
                    StartDragging(draggable.transform, hit.point);
                }
                return;
            }

            // Priority 2: Check if object is a child of the modelsContainer
            if (modelsContainer != null && hitTransform.IsChildOf(modelsContainer))
            {
                Transform modelRoot = FindDirectChildUnderContainer(hitTransform, modelsContainer);
                if (modelRoot != null)
                {
                    StartDragging(modelRoot, hit.point);
                }
                return;
            }

            // Priority 3: Fallback check if it's on modelLayer and not ground
            if (modelLayer != ~0 && ((1 << hit.collider.gameObject.layer) & modelLayer.value) != 0)
            {
                // Ensure it's not the flat ground or camera
                if (hitTransform.GetComponent<UnityEngine.Camera>() == null && !IsGroundObject(hit.collider))
                {
                    Transform modelRoot = FindModelRoot(hitTransform);
                    if (modelRoot != null)
                    {
                        StartDragging(modelRoot, hit.point);
                    }
                    return;
                }
            }
        }
    }

    /// <summary>
    /// Begins dragging the given model transform.
    /// </summary>
    public void StartDragging(Transform model, Vector3 initialHitPoint)
    {
        currentDraggedModel = model;
        selectedModel = model;
        isDragging = true;
        baseGroundY = model.position.y;

        if (ModelResize.Instance != null && ModelResize.Instance.IsResizeModeActive)
        {
            ModelResize.Instance.SetTarget(model);
        }

        // Calculate grab offset: if snapToCursorCenter is true, zero offset between cursor and model
        if (snapToCursorCenter)
        {
            dragOffset = Vector3.zero;
        }
        else
        {
            UnityEngine.Camera cam = targetCamera != null ? targetCamera : UnityEngine.Camera.main;
            if (cam != null)
            {
                Vector2 pointerPos = GetPointerPosition();
                Ray clickRay = cam.ScreenPointToRay(pointerPos);
                Plane groundPlane = new Plane(Vector3.up, new Vector3(0f, baseGroundY, 0f));
                if (groundPlane.Raycast(clickRay, out float enter))
                {
                    Vector3 groundClick = clickRay.GetPoint(enter);
                    dragOffset = model.position - groundClick;
                    dragOffset.y = 0f;
                }
                else
                {
                    dragOffset = Vector3.zero;
                }
            }
            else
            {
                dragOffset = Vector3.zero;
            }
        }

        // Cache all colliders and their local offsets on the dragged model
        cachedColliderOffsets.Clear();
        Collider[] colliders = model.GetComponentsInChildren<Collider>();
        foreach (var col in colliders)
        {
            if (col.isTrigger) continue;
            cachedColliderOffsets.Add(new ColliderOffset
            {
                collider = col,
                localPosition = model.InverseTransformPoint(col.transform.position),
                localRotation = Quaternion.Inverse(model.rotation) * col.transform.rotation
            });
        }

        float lift = dragLiftHeight;
        if (model.TryGetComponent<DraggableModel>(out var dm))
        {
            lift += dm.liftOffset;
        }

        targetPosition = model.position + Vector3.up * lift;
        Debug.Log($"[ModelMovement] Picked up: '{model.name}'");
    }

    private void UpdateDragPosition()
    {
        UnityEngine.Camera cam = targetCamera != null ? targetCamera : UnityEngine.Camera.main;
        if (cam == null) cam = FindAnyObjectByType<UnityEngine.Camera>();
        if (cam == null || currentDraggedModel == null) return;

        Vector2 pointerPosition = GetPointerPosition();
        Ray ray = cam.ScreenPointToRay(pointerPosition);

        // Raycast against scene to find the ground plane ONLY
        RaycastHit[] hits = Physics.RaycastAll(ray, 1000f);
        bool hitGround = false;
        RaycastHit bestGroundHit = default;
        float minDistance = float.MaxValue;

        foreach (var hit in hits)
        {
            // Ignore the model currently being moved
            if (hit.transform.IsChildOf(currentDraggedModel)) continue;

            // Crucial: Ignore ANY other models so cursor ray passes through to the floor!
            if (hit.transform.GetComponentInParent<DraggableModel>() != null) continue;
            if (modelsContainer != null && hit.transform.IsChildOf(modelsContainer)) continue;

            // If a specific ground layer mask is configured, check against it
            if (groundLayer != ~0 && ((1 << hit.collider.gameObject.layer) & groundLayer.value) == 0)
            {
                continue;
            }

            if (hit.distance < minDistance)
            {
                minDistance = hit.distance;
                bestGroundHit = hit;
                hitGround = true;
            }
        }

        float lift = dragLiftHeight;
        if (currentDraggedModel.TryGetComponent<DraggableModel>(out var dm))
        {
            lift += dm.liftOffset;
        }

        Vector3 rawGroundPos;

        if (hitGround)
        {
            rawGroundPos = bestGroundHit.point + dragOffset;
            baseGroundY = bestGroundHit.point.y;
        }
        else
        {
            // Project onto mathematical flat ground plane at baseGroundY
            Plane groundPlane = new Plane(Vector3.up, new Vector3(0f, baseGroundY, 0f));
            if (groundPlane.Raycast(ray, out float enter))
            {
                rawGroundPos = ray.GetPoint(enter) + dragOffset;
            }
            else
            {
                rawGroundPos = currentDraggedModel.position;
            }
        }

        // Set desired target at ground height + lift
        Vector3 candidatePos = new Vector3(rawGroundPos.x, baseGroundY + lift, rawGroundPos.z);

        // Scoot horizontally around other models instead of rising in Y
        targetPosition = ResolveHorizontalCollisions(candidatePos, currentDraggedModel.rotation);

        // Apply smooth position follow
        if (followSmoothing > 0f)
        {
            float t = 1f - Mathf.Exp(-followSmoothing * Time.deltaTime);
            currentDraggedModel.position = Vector3.Lerp(currentDraggedModel.position, targetPosition, t);
        }
        else
        {
            currentDraggedModel.position = targetPosition;
        }
    }

    /// <summary>
    /// Checks for collisions with other models on the field and scoots the position horizontally (left/right/around).
    /// Keeps Y position strictly locked to ground height so the model never jumps over obstacles.
    /// </summary>
    private Vector3 ResolveHorizontalCollisions(Vector3 candidatePos, Quaternion candidateRot)
    {
        if (!enableScootAvoidance || cachedColliderOffsets.Count == 0) return candidatePos;

        Vector3 resolvedPos = candidatePos;
        float lockedY = candidatePos.y;

        for (int iter = 0; iter < maxScootIterations; iter++)
        {
            bool anyPenetration = false;

            // Query colliders in the vicinity
            Collider[] nearby = Physics.OverlapSphere(resolvedPos, 10f);

            foreach (var obstacle in nearby)
            {
                if (obstacle == null || obstacle.isTrigger) continue;
                if (obstacle.transform.IsChildOf(currentDraggedModel)) continue;
                if (IsGroundObject(obstacle)) continue;

                foreach (var myOffset in cachedColliderOffsets)
                {
                    if (myOffset.collider == null) continue;

                    Vector3 myColPos = resolvedPos + (candidateRot * myOffset.localPosition);
                    Quaternion myColRot = candidateRot * myOffset.localRotation;

                    if (Physics.ComputePenetration(
                        myOffset.collider, myColPos, myColRot,
                        obstacle, obstacle.transform.position, obstacle.transform.rotation,
                        out Vector3 pushDir, out float pushDist))
                    {
                        // Project push vector onto the horizontal XZ plane
                        pushDir.y = 0f;

                        if (pushDir.sqrMagnitude < 0.0001f)
                        {
                            // If push was purely vertical, push outward horizontally away from obstacle center
                            Vector3 horizontalDiff = myColPos - obstacle.bounds.center;
                            horizontalDiff.y = 0f;
                            if (horizontalDiff.sqrMagnitude < 0.0001f)
                            {
                                horizontalDiff = currentDraggedModel.right;
                            }
                            pushDir = horizontalDiff.normalized;
                        }
                        else
                        {
                            pushDir.Normalize();
                        }

                        // Scoot the position horizontally by penetration depth + padding
                        resolvedPos += pushDir * (pushDist + scootPadding);
                        resolvedPos.y = lockedY; // Lock Y strictly to ground level
                        anyPenetration = true;
                    }
                }
            }

            if (!anyPenetration) break;
        }

        resolvedPos.y = lockedY;
        return resolvedPos;
    }

    private void HandleRotation()
    {
        if (!allowRotation || currentDraggedModel == null) return;

        float rotInput = 0f;

#if ENABLE_INPUT_SYSTEM
        Mouse mouse = Mouse.current;
        Keyboard keyboard = Keyboard.current;

        if (mouse != null)
        {
            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                rotInput = Mathf.Sign(scroll);
            }
        }

        if (keyboard != null && keyboard.rKey.isPressed)
        {
            rotInput += 1f;
        }
#else
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.01f)
        {
            rotInput = Mathf.Sign(scroll);
        }

        if (Input.GetKey(KeyCode.R))
        {
            rotInput += 1f;
        }
#endif

        if (Mathf.Abs(rotInput) > 0.01f)
        {
            currentDraggedModel.Rotate(Vector3.up, rotInput * rotationSpeed * Time.deltaTime * 60f, Space.World);
        }
    }

    private void HandleScaling()
    {
        if (!allowScaling || currentDraggedModel == null) return;

        float scaleInput = 0f;

#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.equalsKey.isPressed || keyboard.numpadPlusKey.isPressed || keyboard.rightBracketKey.isPressed)
                scaleInput += 1f;
            if (keyboard.minusKey.isPressed || keyboard.numpadMinusKey.isPressed || keyboard.leftBracketKey.isPressed)
                scaleInput -= 1f;
        }
#else
        if (Input.GetKey(KeyCode.Equals) || Input.GetKey(KeyCode.KeypadPlus) || Input.GetKey(KeyCode.RightBracket))
            scaleInput += 1f;
        if (Input.GetKey(KeyCode.Minus) || Input.GetKey(KeyCode.KeypadMinus) || Input.GetKey(KeyCode.LeftBracket))
            scaleInput -= 1f;
#endif

        if (Mathf.Abs(scaleInput) > 0.01f)
        {
            float factor = 1f + (scaleInput * scaleSpeed * Time.deltaTime);
            Vector3 newScale = currentDraggedModel.localScale * factor;
            if (newScale.x > 0.05f && newScale.x < 100f)
            {
                currentDraggedModel.localScale = newScale;
            }
        }
    }

    private void DropModel()
    {
        if (currentDraggedModel != null)
        {
            float lift = dragLiftHeight;
            if (currentDraggedModel.TryGetComponent<DraggableModel>(out var dm))
            {
                lift += dm.liftOffset;
            }

            Vector3 finalPos = currentDraggedModel.position;
            finalPos.y -= lift;
            currentDraggedModel.position = finalPos;

            Debug.Log($"[ModelMovement] Placed '{currentDraggedModel.name}' at: {finalPos}");
        }

        currentDraggedModel = null;
        isDragging = false;
    }

    private Vector2 GetPointerPosition()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
#else
        return Input.mousePosition;
#endif
    }

    private bool IsGroundObject(Collider col)
    {
        if (groundLayer != ~0 && ((1 << col.gameObject.layer) & groundLayer.value) != 0) return true;
        string name = col.gameObject.name.ToLower();
        return name.Contains("plane") || name.Contains("field") || name.Contains("ground") || name.Contains("terrain");
    }

    private Transform FindDirectChildUnderContainer(Transform child, Transform container)
    {
        Transform current = child;
        while (current != null && current.parent != null)
        {
            if (current.parent == container) return current;
            current = current.parent;
        }
        return child;
    }

    private Transform FindModelRoot(Transform hitTransform)
    {
        Transform current = hitTransform;
        while (current.parent != null && current.parent.parent != null && current.parent != modelsContainer)
        {
            current = current.parent;
        }
        return current;
    }
}
