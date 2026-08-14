using UnityEngine;

public class Room : MonoBehaviour
{
    public float width = 5f;
    public float length = 5f;
    public float wallHeight = 3f;
    public float wallThickness = 0.1f;
    public RoomManager.PlotShape shape;

    public void Initialize(float width, float length, RoomManager.PlotShape shape)
    {
        this.width = width;
        this.length = length;
        this.shape = shape;
        BuildRoom();
    }

    void BuildRoom()
    {
        CreateFloor();
        CreateWalls();
    }

    void CreateFloor()
    {
        GameObject floor;

        if (shape == RoomManager.PlotShape.Circle)
        {
            floor = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            floor.name = "Floor";
            float radius = Mathf.Min(width, length) / 2f;
            floor.transform.SetParent(transform);
            floor.transform.localPosition = new Vector3(width / 2f, -0.05f, length / 2f);
            floor.transform.localScale = new Vector3(radius * 2f, 0.05f, radius * 2f);
        }
        else
        {
            floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Floor";
            floor.transform.SetParent(transform);
            floor.transform.localPosition = new Vector3(width / 2f, -0.05f, length / 2f);
            floor.transform.localScale = new Vector3(width, 0.1f, length);
        }

        floor.GetComponent<Renderer>().material.color = new Color(0.85f, 0.82f, 0.75f);
    }

    void CreateWalls()
    {
        if (shape == RoomManager.PlotShape.Circle)
        {
            CreateCircularWalls();
            return;
        }

        CreateWall("WallNorth", new Vector3(width / 2f, wallHeight / 2f, length), new Vector3(width, wallHeight, wallThickness));
        CreateWall("WallSouth", new Vector3(width / 2f, wallHeight / 2f, 0), new Vector3(width, wallHeight, wallThickness));
        CreateWall("WallEast", new Vector3(width, wallHeight / 2f, length / 2f), new Vector3(wallThickness, wallHeight, length));
        CreateWall("WallWest", new Vector3(0, wallHeight / 2f, length / 2f), new Vector3(wallThickness, wallHeight, length));
    }

    void CreateCircularWalls()
    {
        float centerX = width / 2f;
        float centerZ = length / 2f;
        float radius = Mathf.Min(width, length) / 2f;
        int segments = Mathf.Clamp(Mathf.RoundToInt(2f * Mathf.PI * radius / 0.5f), 12, 64);
        float arcLength = 2f * Mathf.PI * radius / segments;

        for (int i = 0; i < segments; i++)
        {
            float angle = (float)i / segments * 2f * Mathf.PI;
            Vector3 position = new Vector3(
                centerX + Mathf.Cos(angle) * radius,
                wallHeight / 2f,
                centerZ + Mathf.Sin(angle) * radius
            );

            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Wall_" + i;
            wall.transform.SetParent(transform);
            wall.transform.localPosition = position;
            wall.transform.localRotation = Quaternion.Euler(0f, 90f - angle * Mathf.Rad2Deg, 0f);
            wall.transform.localScale = new Vector3(arcLength, wallHeight, wallThickness);
            wall.GetComponent<Renderer>().material.color = new Color(0.95f, 0.95f, 0.95f);
        }
    }

    void CreateWall(string wallName, Vector3 position, Vector3 scale)
    {
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = wallName;
        wall.transform.SetParent(transform);
        wall.transform.localPosition = position;
        wall.transform.localScale = scale;
        wall.GetComponent<Renderer>().material.color = new Color(0.95f, 0.95f, 0.95f);
    }
}
