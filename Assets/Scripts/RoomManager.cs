using UnityEngine;
using System.Collections.Generic;

public class RoomManager : MonoBehaviour
{
    public static RoomManager Instance;

    public float defaultRoomWidth = 6f;
    public float defaultRoomLength = 8f;
    public float gridSize = 1f;

    private List<Room> rooms = new List<Room>();

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        AddRoom(Vector2Int.zero, defaultRoomWidth, defaultRoomLength);
    }

    public Room AddRoom(Vector2Int gridPos, float width, float length)
    {
        GameObject roomObj = new GameObject($"Room_{gridPos.x}_{gridPos.y}");
        roomObj.transform.SetParent(transform);
        roomObj.transform.position = new Vector3(gridPos.x * width, 0, gridPos.y * length);

        Room room = roomObj.AddComponent<Room>();
        room.Initialize(width, length);
        rooms.Add(room);
        return room;
    }

    public Room GetRoomAt(Vector3 worldPos)
    {
        foreach (Room room in rooms)
        {
            Vector3 local = worldPos - room.transform.position;
            if (local.x >= 0 && local.x <= room.width && local.z >= 0 && local.z <= room.length)
                return room;
        }
        return null;
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
