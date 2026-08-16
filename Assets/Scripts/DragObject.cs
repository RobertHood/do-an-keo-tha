using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class DragObject : MonoBehaviour
{
    public bool snapToGrid = true;
    public float rotationSpeed = 45f;

    private bool isDragging;
    private bool placeOnRelease;
    private Camera mainCamera;
    private float dragY;

    void Awake()
    {
        EnsurePickupCollider();
    }

    void EnsurePickupCollider()
    {
        if (GetComponentInChildren<Collider>() != null)
            return;

        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
            return;

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        BoxCollider box = gameObject.AddComponent<BoxCollider>();
        box.center = transform.InverseTransformPoint(bounds.center);
        box.size = transform.InverseTransformVector(bounds.size);
        box.isTrigger = true;
    }

    void Update()
    {
        if (Mouse.current == null)
            return;

        if (mainCamera == null)
            mainCamera = Camera.main;
        if (mainCamera == null)
            return;

        bool pointerOverUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        if (!isDragging && Mouse.current.leftButton.wasPressedThisFrame && !pointerOverUI)
        {
            Ray ray = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (TryPickUp(ray))
            {
                isDragging = true;
                placeOnRelease = true;
                dragY = transform.position.y;
            }
        }

        if (isDragging)
        {
            if (Mouse.current.leftButton.wasPressedThisFrame)
                placeOnRelease = true;

            FollowMouse();

            if (placeOnRelease && Mouse.current.leftButton.wasReleasedThisFrame)
                StopDrag();
        }

        if (Mouse.current.rightButton.wasPressedThisFrame && !pointerOverUI)
        {
            Ray ray = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (TryPickUp(ray))
                transform.Rotate(0f, rotationSpeed, 0f, Space.Self);
        }
    }

    bool TryPickUp(Ray ray)
    {
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            if (hit.transform.IsChildOf(transform))
                return true;

            if (IsGhost())
            {
                foreach (RaycastHit candidate in Physics.RaycastAll(ray))
                {
                    if (candidate.transform.IsChildOf(transform))
                        return true;
                }
            }
        }

        return false;
    }

    bool IsGhost()
    {
        Collider[] colliders = GetComponentsInChildren<Collider>();
        if (colliders.Length == 0)
            return false;

        foreach (Collider collider in colliders)
        {
            if (!collider.isTrigger)
                return false;
        }
        return true;
    }

    void FollowMouse()
    {
        Vector2 mousePos = Mouse.current.position.ReadValue();
        Ray ray = mainCamera.ScreenPointToRay(mousePos);
        Plane plane = new Plane(Vector3.up, new Vector3(0f, dragY, 0f));
        if (!plane.Raycast(ray, out float distance))
            return;

        Vector3 target = ray.GetPoint(distance);

        if (snapToGrid && RoomManager.Instance != null)
            target = RoomManager.Instance.SnapToGrid(target);

        if (RoomManager.Instance != null && !IsGhost())
            target = RoomManager.Instance.ClampToRoom(target);

        transform.position = new Vector3(target.x, dragY, target.z);
    }

    public void StartDrag(Camera camera)
    {
        mainCamera = camera;
        isDragging = true;
        placeOnRelease = false;
        dragY = transform.position.y;
    }

    public void StopDrag()
    {
        isDragging = false;
        placeOnRelease = false;
    }
}
