using UnityEngine;
using UnityEngine.InputSystem;

public class RoomCamera : MonoBehaviour
{
    public float panSpeed = 10f;
    public float zoomSpeed = 5f;
    public float minZoom = 2f;
    public float maxZoom = 30f;
    public float rotateSpeed = 100f;

    private Camera cam;
    private Vector3 panOffset;
    private bool isPanning = false;
    private bool isRotating = false;

    void Start()
    {
        cam = GetComponent<Camera>();
        if (cam == null) cam = Camera.main;

        if (cam != null)
        {
            cam.orthographic = true;
            cam.orthographicSize = 10f;
            cam.transform.rotation = Quaternion.Euler(90, 0, 0);
        }
    }

    void Update()
    {
        if (Mouse.current == null || cam == null) return;

        HandlePan();
        HandleZoom();
        HandleRotate();
    }

    void HandlePan()
    {
        if (Mouse.current.middleButton.isPressed)
        {
            Vector2 delta = Mouse.current.delta.ReadValue();
            Vector3 move = new Vector3(-delta.x, 0, -delta.y) * panSpeed * Time.deltaTime;
            cam.transform.position += cam.transform.TransformDirection(move);
        }
    }

    void HandleZoom()
    {
        float scroll = Mouse.current.scroll.ReadValue().y;
        if (scroll != 0)
        {
            cam.orthographicSize -= scroll * zoomSpeed * Time.deltaTime;
            cam.orthographicSize = Mathf.Clamp(cam.orthographicSize, minZoom, maxZoom);
        }
    }

    void HandleRotate()
    {
        if (Mouse.current.rightButton.isPressed && Mouse.current.leftButton.isPressed)
        {
            Vector2 delta = Mouse.current.delta.ReadValue();
            cam.transform.Rotate(0, delta.x * rotateSpeed * Time.deltaTime, 0, Space.World);
        }
    }
}
