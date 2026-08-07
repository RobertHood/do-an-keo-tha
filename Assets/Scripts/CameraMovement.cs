using UnityEngine;
using UnityEngine.InputSystem;

public class CameraMovement : MonoBehaviour
{
    public float moveSpeed = 10f;
    public float fastMoveSpeed = 30f;
    public float lookSensitivity = 2f;
    public float scrollZoomSpeed = 10f;
    public float minZoom = 1f;
    public float maxZoom = 100f;

    private float yaw;
    private float pitch;

    void Start()
    {
        Vector3 euler = transform.eulerAngles;
        yaw = euler.y;
        pitch = euler.x;
    }

    void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        float currentSpeed = keyboard.leftShiftKey.isPressed ? fastMoveSpeed : moveSpeed;

        Vector3 forward = transform.forward;
        Vector3 right = transform.right;

        Vector3 move = Vector3.zero;
        if (keyboard.wKey.isPressed) move += forward;
        if (keyboard.sKey.isPressed) move -= forward;
        if (keyboard.dKey.isPressed) move += right;
        if (keyboard.aKey.isPressed) move -= right;
        if (keyboard.qKey.isPressed) move -= Vector3.up;
        if (keyboard.eKey.isPressed) move += Vector3.up;

        Vector3 newPos = transform.position + move.normalized * currentSpeed * Time.deltaTime;
        newPos.y = Mathf.Max(newPos.y, 0f);
        transform.position = newPos;

        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
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
                transform.position += forward * scroll * scrollZoomSpeed;
            }
        }
    }
}
