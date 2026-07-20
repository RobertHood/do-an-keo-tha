using UnityEngine;
using UnityEngine.InputSystem;

public class DragObject : MonoBehaviour
{
    public bool snapToGrid = true;
    public float rotationSpeed = 45f;

    private bool isDragging = false;
    private Camera mainCamera;
    private Rigidbody rb;
    private Vector3 grabOffset;
    private float dragY;

    void Start()
    {
        mainCamera = Camera.main;
        rb = GetComponent<Rigidbody>();
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
    }

    void Update()
    {
        if (Mouse.current == null) return;

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            Vector2 mousePos = Mouse.current.position.ReadValue();
            Ray ray = mainCamera.ScreenPointToRay(mousePos);
            if (Physics.Raycast(ray, out RaycastHit hit) && hit.transform == transform)
            {
                isDragging = true;
                grabOffset = transform.position - hit.point;
                dragY = transform.position.y;
            }
        }

        if (Mouse.current.leftButton.wasReleasedThisFrame)
        {
            isDragging = false;
        }

        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            Vector2 mousePos = Mouse.current.position.ReadValue();
            Ray ray = mainCamera.ScreenPointToRay(mousePos);
            if (Physics.Raycast(ray, out RaycastHit hit) && hit.transform == transform)
            {
                transform.Rotate(0, rotationSpeed, 0);
            }
        }

        if (isDragging)
        {
            Vector2 mousePos = Mouse.current.position.ReadValue();
            Ray ray = mainCamera.ScreenPointToRay(mousePos);
            Plane plane = new Plane(Vector3.up, new Vector3(0, dragY, 0));
            if (plane.Raycast(ray, out float distance))
            {
                Vector3 targetPoint = ray.GetPoint(distance) + grabOffset;

                if (snapToGrid && RoomManager.Instance != null)
                    targetPoint = RoomManager.Instance.SnapToGrid(targetPoint);

                if (rb != null)
                {
                    rb.MovePosition(targetPoint);
                }
                else
                {
                    transform.position = targetPoint;
                }
            }
        }
    }
}
