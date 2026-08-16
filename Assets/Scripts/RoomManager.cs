using System.Collections.Generic;
using UnityEngine;

public class RoomManager : MonoBehaviour
{
    public enum PlotShape
    {
        Rectangle,
        Square,
        Circle
    }

    public static RoomManager Instance;

    public float defaultRoomWidth = 6f;
    public float defaultRoomLength = 8f;
    public PlotShape plotShape = PlotShape.Rectangle;
    public float gridSize = 1f;

    private List<Room> rooms = new List<Room>();

    void Awake()
    {
        Instance = this;
    }

    public static RoomManager EnsureInstance()
    {
        if (Instance != null)
            return Instance;

        GameObject go = new GameObject("RoomManager");
        return go.AddComponent<RoomManager>();
    }

    public Room MainRoom => rooms.Count > 0 ? rooms[0] : null;

    public Room AddRoom(Vector2Int gridPos, float width, float length, PlotShape shape)
    {
        GameObject roomObj = new GameObject($"Room_{gridPos.x}_{gridPos.y}");
        roomObj.transform.SetParent(transform);
        roomObj.transform.position = new Vector3(gridPos.x * width, 0, gridPos.y * length);

        Room room = roomObj.AddComponent<Room>();
        room.Initialize(width, length, shape);
        rooms.Add(room);
        return room;
    }

    public void SetPlot(float length, float width, PlotShape shape)
    {
        defaultRoomWidth = Mathf.Max(1f, width);
        defaultRoomLength = Mathf.Max(1f, length);
        plotShape = shape;

        foreach (Room room in rooms)
        {
            if (room != null)
                Destroy(room.gameObject);
        }
        rooms.Clear();

        AddRoom(Vector2Int.zero, defaultRoomWidth, defaultRoomLength, plotShape);
        RemoveItemsOutsidePlot();
    }

    void RemoveItemsOutsidePlot()
    {
        if (FurnitureTotalTracker.Instance == null)
            return;

        List<PlacedItem> toRemove = new List<PlacedItem>();
        foreach (PlacedItem item in FurnitureTotalTracker.Instance.PlacedItems)
        {
            if (item != null && !IsInside(item.transform.position))
                toRemove.Add(item);
        }

        foreach (PlacedItem item in toRemove)
        {
            if (item != null)
                Destroy(item.gameObject);
        }
    }

    public Room GetRoomAt(Vector3 worldPos)
    {
        foreach (Room room in rooms)
        {
            Vector3 local = worldPos - room.transform.position;
            if (plotShape == PlotShape.Circle)
            {
                Vector2 center = new Vector2(room.width / 2f, room.length / 2f);
                float radius = Mathf.Min(room.width, room.length) / 2f;
                if (Vector2.Distance(new Vector2(local.x, local.z), center) <= radius)
                    return room;
            }
            else if (local.x >= 0 && local.x <= room.width && local.z >= 0 && local.z <= room.length)
            {
                return room;
            }
        }
        return null;
    }

    public bool IsInside(Vector3 worldPos)
    {
        Room room = MainRoom;
        if (room == null)
            return true;

        Vector3 local = worldPos - room.transform.position;
        if (plotShape == PlotShape.Circle)
        {
            Vector2 center = new Vector2(room.width / 2f, room.length / 2f);
            float radius = Mathf.Min(room.width, room.length) / 2f;
            return Vector2.Distance(new Vector2(local.x, local.z), center) <= radius;
        }

        return local.x >= 0 && local.x <= room.width && local.z >= 0 && local.z <= room.length;
    }

    public Vector3 ClampToRoom(Vector3 position)
    {
        Room room = MainRoom;
        if (room == null)
            return position;

        Vector3 local = position - room.transform.position;

        if (plotShape == PlotShape.Circle)
        {
            Vector2 center = new Vector2(room.width / 2f, room.length / 2f);
            float radius = Mathf.Min(room.width, room.length) / 2f;
            Vector2 offset = new Vector2(local.x, local.z) - center;
            float distance = offset.magnitude;
            if (distance > radius)
            {
                if (distance > 0.0001f)
                    offset = offset.normalized * radius;
                else
                    offset = Vector2.zero;
                local = new Vector3(center.x + offset.x, local.y, center.y + offset.y);
            }
            return room.transform.position + local;
        }

        local.x = Mathf.Clamp(local.x, 0f, room.width);
        local.z = Mathf.Clamp(local.z, 0f, room.length);
        return room.transform.position + local;
    }

    public Vector3 SnapToGrid(Vector3 position)
    {
        return new Vector3(
            Mathf.Round(position.x / gridSize) * gridSize,
            position.y,
            Mathf.Round(position.z / gridSize) * gridSize
        );
    }
}
