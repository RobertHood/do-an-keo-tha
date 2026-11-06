using UnityEngine;
using UnityEngine.EventSystems;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Camera controller providing editor-like navigation in play mode:
/// - Scroll wheel: Zoom in / zoom out
/// - WASD or Arrow keys: Move left, right, forward, backward
/// - Q / E or Space / C: Move down / up
/// - Right-click + drag: Look around / rotate camera
/// - Middle-click + drag: Pan camera
/// - Left Shift: Fast movement (boost)
/// - Left Ctrl: Slow / precision movement
/// </summary>
[AddComponentMenu("Camera/Editor Camera Controller")]
public class Camera : MonoBehaviour
{
    [Header("Movement")]
    [Tooltip("Base movement speed in units per second.")]
    [SerializeField] private float moveSpeed = 15f;

    [Tooltip("Speed multiplier applied when holding Shift.")]
    [SerializeField] private float fastMoveMultiplier = 2.5f;

    [Tooltip("Speed multiplier applied when holding Ctrl.")]
    [SerializeField] private float slowMoveMultiplier = 0.3f;

    [Tooltip("If true, WASD/Arrow keys move parallel to the ground (XZ plane). If false, moves in full 3D direction of camera view.")]
    [SerializeField] private bool moveParallelToGround = false;

    [Header("Zoom")]
    [Tooltip("Zoom speed when using the scroll wheel.")]
    [SerializeField] private float zoomSpeed = 20f;

    [Header("Rotation (Mouse Drag)")]
    [Tooltip("Sensitivity of mouse rotation.")]
    [SerializeField] private float lookSensitivity = 0.15f;

    [Tooltip("Allow holding and dragging Left Mouse Button to look around as well (disabled by default so Left Click can click UI and drag models).")]
    [SerializeField] private bool allowLeftClickLook = false;

    [Tooltip("Invert vertical look direction.")]
    [SerializeField] private bool invertY = false;

    [Tooltip("Minimum vertical angle (degrees).")]
    [SerializeField] private float minPitch = -89f;

    [Tooltip("Maximum vertical angle (degrees).")]
    [SerializeField] private float maxPitch = 89f;

    [Tooltip("Lock and hide cursor while holding Mouse Button to look around.")]
    [SerializeField] private bool lockCursorOnLook = true;

    [Header("Pan (Middle-Click Drag)")]
    [Tooltip("Pan speed when dragging with the middle mouse button.")]
    [SerializeField] private float panSpeed = 10f;

    [Header("Smoothing")]
    [Tooltip("Enable smooth interpolation for movement and rotation.")]
    [SerializeField] private bool smoothMovement = true;

    [Tooltip("Smooth factor (higher is sharper, lower is softer).")]
    [SerializeField] private float smoothFactor = 15f;

    private Vector3 targetPosition;
    private float targetYaw;
    private float targetPitch;
    private bool isLooking;

    private void OnEnable()
    {
        targetPosition = transform.position;
        Vector3 euler = transform.eulerAngles;
        targetYaw = euler.y;
        targetPitch = euler.x;
        if (targetPitch > 180f)
            targetPitch -= 360f;
    }

    private void OnDisable()
    {
        ReleaseCursor();
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
        {
            ReleaseCursor();
        }
    }

    private void Update()
    {
        HandleRotation();
        HandleZoom();
        HandleMovement();
        HandlePan();
        ApplyTransform();
    }

    private void HandleRotation()
    {
        // Don't capture rotation or lock cursor if interacting with UI buttons
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            if (isLooking) ReleaseCursor();
            return;
        }

        // Don't rotate camera if currently dragging a 3D model
        if (ModelMovement.Instance != null && ModelMovement.Instance.IsDragging)
        {
            if (isLooking) ReleaseCursor();
            return;
        }

#if ENABLE_INPUT_SYSTEM
        Mouse mouse = Mouse.current;
        if (mouse == null) return;

        bool isLookPressed = mouse.rightButton.isPressed || (allowLeftClickLook && mouse.leftButton.isPressed);
        bool lookStarted = mouse.rightButton.wasPressedThisFrame || (allowLeftClickLook && mouse.leftButton.wasPressedThisFrame);

        if (lookStarted)
        {
            isLooking = true;
            if (lockCursorOnLook)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
        else if (!isLookPressed && isLooking)
        {
            ReleaseCursor();
        }

        if (isLooking && isLookPressed)
        {
            Vector2 delta = mouse.delta.ReadValue();
            targetYaw += delta.x * lookSensitivity;
            targetPitch += (invertY ? delta.y : -delta.y) * lookSensitivity;
            targetPitch = Mathf.Clamp(targetPitch, minPitch, maxPitch);
        }
#else
        bool isLookPressed = Input.GetMouseButton(1) || (allowLeftClickLook && Input.GetMouseButton(0));
        bool lookStarted = Input.GetMouseButtonDown(1) || (allowLeftClickLook && Input.GetMouseButtonDown(0));

        if (lookStarted)
        {
            isLooking = true;
            if (lockCursorOnLook)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
        else if (!isLookPressed && isLooking)
        {
            ReleaseCursor();
        }

        if (isLooking && isLookPressed)
        {
            float deltaX = Input.GetAxis("Mouse X");
            float deltaY = Input.GetAxis("Mouse Y");
            targetYaw += deltaX * lookSensitivity * 10f;
            targetPitch += (invertY ? deltaY : -deltaY) * lookSensitivity * 10f;
            targetPitch = Mathf.Clamp(targetPitch, minPitch, maxPitch);
        }
#endif
    }

    private void HandleZoom()
    {
        // Let the scroll wheel scroll UI panels instead of zooming when hovering over them
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        float scrollInput = 0f;

#if ENABLE_INPUT_SYSTEM
        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            float rawScroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(rawScroll) > 0.01f)
            {
                // Standard mouse wheels output ~120 per notch
                scrollInput = (Mathf.Abs(rawScroll) >= 120f) ? (rawScroll / 120f) : Mathf.Sign(rawScroll);
            }
        }
#else
        scrollInput = Input.GetAxis("Mouse ScrollWheel") * 10f;
#endif

        if (Mathf.Abs(scrollInput) > 0.001f)
        {
            float speedMultiplier = 1f;

#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed))
                speedMultiplier = fastMoveMultiplier;
#else
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                speedMultiplier = fastMoveMultiplier;
#endif

            targetPosition += transform.forward * (scrollInput * zoomSpeed * speedMultiplier * 0.1f);
        }
    }

    private void HandleMovement()
    {
        bool moveForward = false;
        bool moveBackward = false;
        bool moveLeft = false;
        bool moveRight = false;
        bool moveUp = false;
        bool moveDown = false;
        bool isFast = false;
        bool isSlow = false;

#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            moveForward = keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed;
            moveBackward = keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed;
            moveLeft = keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed;
            moveRight = keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed;
            moveUp = keyboard.eKey.isPressed || keyboard.spaceKey.isPressed || keyboard.pageUpKey.isPressed;
            moveDown = keyboard.qKey.isPressed || keyboard.cKey.isPressed || keyboard.pageDownKey.isPressed;

            isFast = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
            isSlow = keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed;
        }
#else
        moveForward = Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow);
        moveBackward = Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow);
        moveLeft = Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow);
        moveRight = Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow);
        moveUp = Input.GetKey(KeyCode.E) || Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.PageUp);
        moveDown = Input.GetKey(KeyCode.Q) || Input.GetKey(KeyCode.C) || Input.GetKey(KeyCode.PageDown);

        isFast = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        isSlow = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
#endif

        Vector3 moveDir = Vector3.zero;

        Vector3 forwardVec = moveParallelToGround
            ? Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized
            : transform.forward;

        Vector3 rightVec = moveParallelToGround
            ? Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized
            : transform.right;

        if (forwardVec.sqrMagnitude < 0.0001f) forwardVec = transform.forward;
        if (rightVec.sqrMagnitude < 0.0001f) rightVec = transform.right;

        if (moveForward) moveDir += forwardVec;
        if (moveBackward) moveDir -= forwardVec;
        if (moveRight) moveDir += rightVec;
        if (moveLeft) moveDir -= rightVec;
        if (moveUp) moveDir += Vector3.up;
        if (moveDown) moveDir -= Vector3.up;

        if (moveDir.sqrMagnitude > 0.0001f)
        {
            moveDir.Normalize();

            float speed = moveSpeed;
            if (isFast) speed *= fastMoveMultiplier;
            else if (isSlow) speed *= slowMoveMultiplier;

            targetPosition += moveDir * (speed * Time.deltaTime);
        }
    }

    private void HandlePan()
    {
#if ENABLE_INPUT_SYSTEM
        Mouse mouse = Mouse.current;
        Keyboard keyboard = Keyboard.current;
        if (mouse == null) return;

        if (mouse.middleButton.isPressed)
        {
            Vector2 delta = mouse.delta.ReadValue();
            float currentPanSpeed = panSpeed;
            if (keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed))
                currentPanSpeed *= fastMoveMultiplier;

            Vector3 panOffset = (-transform.right * delta.x - transform.up * delta.y) * (currentPanSpeed * 0.005f);
            targetPosition += panOffset;
        }
#else
        if (Input.GetMouseButton(2))
        {
            float deltaX = Input.GetAxis("Mouse X");
            float deltaY = Input.GetAxis("Mouse Y");
            float currentPanSpeed = panSpeed;
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                currentPanSpeed *= fastMoveMultiplier;

            Vector3 panOffset = (-transform.right * deltaX - transform.up * deltaY) * (currentPanSpeed * 0.1f);
            targetPosition += panOffset;
        }
#endif
    }

    private void ApplyTransform()
    {
        Quaternion targetRotation = Quaternion.Euler(targetPitch, targetYaw, 0f);

        if (smoothMovement)
        {
            float t = 1f - Mathf.Exp(-smoothFactor * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, targetPosition, t);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, t);
        }
        else
        {
            transform.position = targetPosition;
            transform.rotation = targetRotation;
        }
    }

    private void ReleaseCursor()
    {
        isLooking = false;
        if (lockCursorOnLook)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
