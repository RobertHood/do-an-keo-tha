using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class FurniturePlacer : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public GameObject prefab;
    public Camera mainCamera;
    public bool snapToGrid = true;

    private GameObject spawnedObject;

    void Start()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (prefab == null) return;

        Vector2 mousePos = Mouse.current.position.ReadValue();
        Vector3 spawnPos = GetWorldPosition(mousePos);

        if (snapToGrid && RoomManager.Instance != null)
            spawnPos = RoomManager.Instance.SnapToGrid(spawnPos);

        spawnedObject = Instantiate(prefab, spawnPos, Quaternion.identity);
        spawnedObject.name = prefab.name;

        DragObject drag = spawnedObject.GetComponent<DragObject>();
        if (drag == null)
            drag = spawnedObject.AddComponent<DragObject>();

        drag.StartDrag(mainCamera);
    }

    public void OnDrag(PointerEventData eventData)
    {
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (spawnedObject != null)
        {
            DragObject drag = spawnedObject.GetComponent<DragObject>();
            if (drag != null)
                drag.StopDrag();
        }

        spawnedObject = null;
    }

    Vector3 GetWorldPosition(Vector2 screenPos)
    {
        Ray ray = mainCamera.ScreenPointToRay(screenPos);
        Plane plane = new Plane(Vector3.up, Vector3.zero);
        if (plane.Raycast(ray, out float distance))
            return ray.GetPoint(distance);
        return Vector3.zero;
    }
}
