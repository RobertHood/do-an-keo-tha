using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class DragObject : MonoBehaviour
{
    public bool snapToGrid = true;
    public float rotationSpeed = 45f;

    [Tooltip("When the dragged item overlaps another item, snap it flush to that item's side.")]
    public bool sideSnap = true;

    [Tooltip("Gap kept between two items when snapped side by side.")]
    public float sideSnapGap = 0.05f;

    [Tooltip("Overlap (in meters) required before the side snap kicks in, to avoid spurious snaps from resting contact.")]
    public float minOverlapThreshold = 0.02f;

    private bool isDragging;
    private Camera mainCamera;
    private Rigidbody rb;
    private Vector3 grabOffset;
    private float dragY;

    void Awake()
    {
        InitPhysics();
    }

    void InitPhysics()
    {
        mainCamera = Camera.main;
        EnsureCollider();

        rb = GetComponent<Rigidbody>();
        if (rb == null)
            rb = gameObject.AddComponent<Rigidbody>();

        rb.useGravity = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        rb.linearDamping = 2f;
        rb.angularDamping = 4f;
    }

    void FixedUpdate()
    {
        if (!isDragging || rb == null || mainCamera == null || Mouse.current == null)
            return;

        Vector2 mousePos = Mouse.current.position.ReadValue();
        Ray ray = mainCamera.ScreenPointToRay(mousePos);
        Plane plane = new Plane(Vector3.up, new Vector3(0, dragY, 0));
        if (!plane.Raycast(ray, out float distance))
            return;

        Vector3 targetPoint = ray.GetPoint(distance) + grabOffset;

        if (snapToGrid && RoomManager.Instance != null)
            targetPoint = RoomManager.Instance.SnapToGrid(targetPoint);

        Vector3 resolved = ResolveSideBySide(targetPoint);

        if (RoomManager.Instance != null)
            resolved = RoomManager.Instance.ClampToRoom(resolved);

        Vector3 nextPos = new Vector3(resolved.x, dragY, resolved.z);
        rb.MovePosition(nextPos);
        rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
    }

    void Update()
    {
        if (Mouse.current == null || mainCamera == null)
            return;

        bool pointerOverUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        if (Mouse.current.leftButton.wasPressedThisFrame && !pointerOverUI)
        {
            Vector2 mousePos = Mouse.current.position.ReadValue();
            Ray ray = mainCamera.ScreenPointToRay(mousePos);
            if (Physics.Raycast(ray, out RaycastHit hit) && hit.transform.IsChildOf(transform))
            {
                isDragging = true;
                grabOffset = transform.position - hit.point;
                grabOffset.y = 0f;
                dragY = transform.position.y;
            }
        }

        if (Mouse.current.leftButton.wasReleasedThisFrame && isDragging)
        {
            StopDrag();
        }

        if (Mouse.current.rightButton.wasPressedThisFrame && !pointerOverUI)
        {
            Vector2 mousePos = Mouse.current.position.ReadValue();
            Ray ray = mainCamera.ScreenPointToRay(mousePos);
            if (Physics.Raycast(ray, out RaycastHit hit) && hit.transform.IsChildOf(transform))
            {
                Quaternion newRot = Quaternion.Euler(transform.eulerAngles + new Vector3(0, rotationSpeed, 0));
                if (rb != null)
                    rb.MoveRotation(newRot);
                else
                    transform.rotation = newRot;
            }
        }
    }

    public void StartDrag(Camera camera)
    {
        mainCamera = camera;
        isDragging = true;
        grabOffset = Vector3.zero;
        dragY = transform.position.y;
    }

    public void StopDrag()
    {
        isDragging = false;

        if (rb == null)
            return;

        Vector3 resolved = ResolveSideBySide(transform.position);
        if (RoomManager.Instance != null)
            resolved = RoomManager.Instance.ClampToRoom(resolved);
        if (resolved != transform.position)
            rb.position = new Vector3(resolved.x, transform.position.y, resolved.z);

        rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
        rb.angularVelocity = Vector3.zero;
    }

    Vector3 ResolveSideBySide(Vector3 desiredPos)
    {
        if (!sideSnap)
            return desiredPos;

        List<PlacedItem> others = GetOtherPlacedItems();
        Vector3 result = desiredPos;

        for (int iteration = 0; iteration < 4; iteration++)
        {
            if (!TryGetBounds(transform, result, out Bounds thisBounds))
                break;

            PlacedItem overlap = null;
            Bounds overlapBounds = default;
            float overlapX = 0f;
            float overlapZ = 0f;

            foreach (PlacedItem other in others)
            {
                if (other == null || other.transform == transform)
                    continue;

                if (!TryGetBounds(other.transform, other.transform.position, out Bounds otherBounds))
                    continue;

                float ox = Mathf.Min(thisBounds.max.x, otherBounds.max.x) - Mathf.Max(thisBounds.min.x, otherBounds.min.x);
                float oz = Mathf.Min(thisBounds.max.z, otherBounds.max.z) - Mathf.Max(thisBounds.min.z, otherBounds.min.z);

                if (ox <= minOverlapThreshold || oz <= minOverlapThreshold)
                    continue;

                if (overlap == null || Mathf.Min(ox, oz) < Mathf.Min(overlapX, overlapZ))
                {
                    overlap = other;
                    overlapBounds = otherBounds;
                    overlapX = ox;
                    overlapZ = oz;
                }
            }

            if (overlap == null)
                break;

            float thisHalfX = thisBounds.size.x * 0.5f;
            float thisHalfZ = thisBounds.size.z * 0.5f;
            float otherHalfX = overlapBounds.size.x * 0.5f;
            float otherHalfZ = overlapBounds.size.z * 0.5f;

            if (overlapX <= overlapZ)
            {
                float side = result.x >= overlapBounds.center.x ? 1f : -1f;
                result.x = overlapBounds.center.x + side * (thisHalfX + otherHalfX + sideSnapGap);
            }
            else
            {
                float side = result.z >= overlapBounds.center.z ? 1f : -1f;
                result.z = overlapBounds.center.z + side * (thisHalfZ + otherHalfZ + sideSnapGap);
            }
        }

        return result;
    }

    List<PlacedItem> GetOtherPlacedItems()
    {
        if (FurnitureTotalTracker.Instance != null)
            return new List<PlacedItem>(FurnitureTotalTracker.Instance.PlacedItems);

        return new List<PlacedItem>(FindObjectsByType<PlacedItem>());
    }

    static bool TryGetBounds(Transform target, Vector3 desiredCenter, out Bounds bounds)
    {
        bounds = default;

        Collider[] colliders = target.GetComponentsInChildren<Collider>();
        if (colliders.Length == 0)
            return false;

        Bounds b = colliders[0].bounds;
        for (int i = 1; i < colliders.Length; i++)
            b.Encapsulate(colliders[i].bounds);

        Vector3 offset = desiredCenter - target.position;
        bounds = b;
        bounds.center += offset;
        return true;
    }

    void EnsureCollider()
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
    }
}
