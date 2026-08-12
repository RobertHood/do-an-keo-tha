using UnityEngine;
using UnityEngine.InputSystem;

public class CameraMovement : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 10f;
    public float fastMoveMultiplier = 3f;

    [Header("Look")]
    public float lookSensitivity = 2f;

    [Header("Pan")]
    public float panSpeed = 4f;

    [Header("Zoom")]
    public float zoomStep = 0.12f;
    public float minZoomDistance = 0.5f;
    public float maxZoomDistance = 400f;

    private float yaw;
    private float pitch;
    private float zoomDistance = 25f;

    void Start()
    {
        Vector3 euler = transform.eulerAngles;
        yaw = euler.y;
        pitch = euler.x;

        Plane floor = new Plane(Vector3.up, Vector3.zero);
        Ray ray = new Ray(transform.position, transform.forward);
        if (floor.Raycast(ray, out float dist))
            zoomDistance = Mathf.Clamp(dist, minZoomDistance, maxZoomDistance);

        if (Vector3.Distance(transform.position, GetRoomCenter()) > 60f)
        {
            yaw = 45f;
            pitch = 35f;
            FocusOnRoom();
        }
    }

    void Update()
    {
        Keyboard keyboard = Keyboard.current;
        Mouse mouse = Mouse.current;
        if (keyboard == null || mouse == null) return;

        if (mouse.rightButton.isPressed)
        {
            Vector2 delta = mouse.delta.ReadValue();
            yaw += delta.x * lookSensitivity;
            pitch -= delta.y * lookSensitivity;
            pitch = Mathf.Clamp(pitch, -89f, 89f);
            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }

        float scroll = mouse.scroll.ReadValue().y;
        if (scroll != 0f)
        {
            Vector3 lookTarget = transform.position + transform.forward * zoomDistance;
            zoomDistance = Mathf.Clamp(zoomDistance * (1f - scroll * zoomStep), minZoomDistance, maxZoomDistance);
            transform.position = lookTarget - transform.forward * zoomDistance;
        }

        float speed = moveSpeed * (keyboard.leftCtrlKey.isPressed ? fastMoveMultiplier : 1f);

        Vector3 flatForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        Vector3 flatRight = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;
        if (flatForward.sqrMagnitude < 0.0001f) flatForward = Vector3.forward;
        if (flatRight.sqrMagnitude < 0.0001f) flatRight = Vector3.right;

        Vector3 move = Vector3.zero;
        if (keyboard.wKey.isPressed) move += flatForward;
        if (keyboard.sKey.isPressed) move -= flatForward;
        if (keyboard.dKey.isPressed) move += flatRight;
        if (keyboard.aKey.isPressed) move -= flatRight;
        if (keyboard.qKey.isPressed) move -= Vector3.up;
        if (keyboard.eKey.isPressed) move += Vector3.up;
        if (keyboard.spaceKey.isPressed) move += Vector3.up;
        if (keyboard.leftShiftKey.isPressed) move -= Vector3.up;

        if (mouse.middleButton.isPressed)
        {
            Vector2 delta = mouse.delta.ReadValue();
            float panFactor = panSpeed * Mathf.Max(zoomDistance * 0.01f, 0.2f) * Time.deltaTime;
            move -= flatRight * (delta.x * panFactor);
            move -= flatForward * (delta.y * panFactor);
        }

        if (move.sqrMagnitude > 0f)
        {
            Vector3 newPos = transform.position + move.normalized * speed * Time.deltaTime;
            newPos.y = Mathf.Max(newPos.y, 0.1f);
            transform.position = newPos;
        }

        if (keyboard.fKey.wasPressedThisFrame)
            FocusOnRoom();
    }

    void FocusOnRoom()
    {
        Vector3 target = GetRoomCenter();
        zoomDistance = Mathf.Max(zoomDistance, 12f);
        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 dir = transform.rotation * Vector3.forward;
        Vector3 pos = target - dir * zoomDistance;
        pos.y = Mathf.Max(pos.y, 1f);
        transform.position = pos;
    }

    Vector3 GetRoomCenter()
    {
        Vector3 center = new Vector3(3f, 0f, 4f);

        if (RoomManager.Instance != null)
        {
            Room room = RoomManager.Instance.GetRoomAt(RoomManager.Instance.transform.position);
            if (room != null)
                center = room.transform.position + new Vector3(room.width / 2f, 0f, room.length / 2f);
        }

        return center;
    }
}
