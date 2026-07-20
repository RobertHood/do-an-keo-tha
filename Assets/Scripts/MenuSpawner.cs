using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class MenuSpawner : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public GameObject prefab;
    public Camera mainCamera;

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
        Ray ray = mainCamera.ScreenPointToRay(mousePos);

        Plane plane = new Plane(Vector3.up, Vector3.zero);
        if (plane.Raycast(ray, out float distance))
        {
            Vector3 spawnPos = ray.GetPoint(distance);
            spawnedObject = Instantiate(prefab, spawnPos, Quaternion.identity);

            DragObject drag = spawnedObject.GetComponent<DragObject>();
            if (drag == null)
                drag = spawnedObject.AddComponent<DragObject>();

            drag.StartDrag(mainCamera);
        }
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
}
