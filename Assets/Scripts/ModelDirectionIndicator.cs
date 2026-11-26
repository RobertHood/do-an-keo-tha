using UnityEngine;

/// <summary>
/// Shows which way the selected model faces ("huong", see DraggableModel.Huong): a blue arrow on the ground in
/// front of it and a label such as "Hướng nhà · Đông Nam 135°". For houses the arrow starts at the main door,
/// which is marked with a dot. Only shown for models with a front (houses, gates, benches, statues).
/// Created automatically if the scene doesn't have one.
/// </summary>
[AddComponentMenu("Interaction/Model Direction Indicator")]
public class ModelDirectionIndicator : MonoBehaviour
{
    [SerializeField] private Color arrowColor = new Color(0.035f, 0.41f, 0.85f, 0.9f);
    [SerializeField] private Color doorColor = new Color(0.96f, 0.62f, 0.04f, 0.95f);

    private Material material;
    private Mesh arrowMesh;
    private Mesh discMesh;
    private Transform arrow;
    private Transform door;
    private Canvas labelCanvas;
    private WorldLabel label;
    private readonly Vector3[] footprint = new Vector3[4];
    private DraggableModel lastModel;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateIfMissing()
    {
        if (FindAnyObjectByType<ModelDirectionIndicator>(FindObjectsInactive.Include) == null)
        {
            new GameObject("ModelDirectionIndicator").AddComponent<ModelDirectionIndicator>();
        }
    }

    private void Awake()
    {
        material = GizmoUtility.CreateLineMaterial();
        material.renderQueue = 3001;
        arrowMesh = BuildArrow(arrowColor);
        discMesh = BuildDisc(doorColor, 20);
        arrow = CreatePart("_DirectionArrow", arrowMesh);
        door = CreatePart("_DoorMarker", discMesh);
        labelCanvas = UIBuild.OverlayCanvas("_DirectionLabels", -9);
        label = new WorldLabel(labelCanvas, "_DirectionLabel", UIPalette.BrandBlue, UIPalette.White, 13f);
        SetVisible(false);
    }

    private void OnDestroy()
    {
        if (material != null) Destroy(material);
        if (arrowMesh != null) Destroy(arrowMesh);
        if (discMesh != null) Destroy(discMesh);
        if (labelCanvas != null) Destroy(labelCanvas.gameObject);
    }

    private Transform CreatePart(string name, Mesh mesh)
    {
        GameObject go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(transform, false);
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer r = go.GetComponent<MeshRenderer>();
        r.sharedMaterial = material;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return go.transform;
    }

    private void SetVisible(bool visible)
    {
        if (arrow.gameObject.activeSelf != visible) arrow.gameObject.SetActive(visible);
        if (!visible)
        {
            if (door.gameObject.activeSelf) door.gameObject.SetActive(false);
            label.Hide();
        }
    }

    private void LateUpdate()
    {
        Transform selected = ModelMovement.Instance != null ? ModelMovement.Instance.SelectedModel : null;
        DraggableModel model = selected != null ? selected.GetComponent<DraggableModel>() : null;
        FengShuiType type = model != null ? model.Loai : FengShuiType.Khac;

        bool measuring = ModelMeasureDistance.Instance != null && ModelMeasureDistance.Instance.IsMeasureModeActive;
        if (model == null || measuring || !FengShuiTypes.HasFront(type))
        {
            SetVisible(false);
            lastModel = null;
            return;
        }

        if (model != lastModel)
        {
            lastModel = model;
            UINotification.ShowTipOnce(NotificationTips.DirectionKey, NotificationTips.Direction);
        }

        Vector3 direction = model.FrontDirection;
        model.GetFootprint(footprint);
        Vector3 center = (footprint[0] + footprint[1] + footprint[2] + footprint[3]) * 0.25f;
        float groundY = Mathf.Min(Mathf.Min(footprint[0].y, footprint[1].y), Mathf.Min(footprint[2].y, footprint[3].y));
        float frontDistance = 0f;
        float size = 0f;
        for (int i = 0; i < 4; i++)
        {
            Vector3 offset = footprint[i] - center;
            frontDistance = Mathf.Max(frontDistance, Vector3.Dot(offset, direction));
            size = Mathf.Max(size, new Vector2(offset.x, offset.z).magnitude * 2f);
        }

        // The arrow starts at the main door for houses, otherwise at the middle of the front side
        Vector3 start;
        bool hasDoor = model.TryGetDoorPosition(out Vector3 doorPosition);
        if (hasDoor)
        {
            start = doorPosition;
            door.gameObject.SetActive(true);
            door.position = new Vector3(doorPosition.x, groundY + 0.07f, doorPosition.z);
            door.localScale = Vector3.one * Mathf.Clamp(size * 0.04f, 0.5f, 1.2f);
        }
        else
        {
            start = center + direction * frontDistance;
            if (door.gameObject.activeSelf) door.gameObject.SetActive(false);
        }

        float length = Mathf.Clamp(size * 0.35f, 1.2f, 6f);
        start.y = groundY + 0.08f;
        SetVisible(true);
        arrow.position = start + direction * (0.25f + length * 0.5f);
        arrow.rotation = Quaternion.LookRotation(direction, Vector3.up);
        arrow.localScale = new Vector3(length * 0.55f, 1f, length);

        string prefix = type == FengShuiType.Nha ? "Hướng nhà" : type == FengShuiType.Cong ? "Hướng cổng" : "Hướng";
        label.SetText($"{prefix} · {CompassDirections.Format(model.Huong)}");
        label.Show(UnityEngine.Camera.main, start + direction * (0.4f + length), new Vector2(0f, 14f));
    }

    /// <summary>
    /// Flat arrow lying on the ground, pointing +Z, 1 unit long and 1 unit wide at the head.
    /// </summary>
    private static Mesh BuildArrow(Color color)
    {
        Vector3[] v =
        {
            new Vector3(-0.16f, 0f, -0.5f), new Vector3(0.16f, 0f, -0.5f), new Vector3(0.16f, 0f, 0.1f), new Vector3(-0.16f, 0f, 0.1f),
            new Vector3(-0.5f, 0f, 0.1f), new Vector3(0.5f, 0f, 0.1f), new Vector3(0f, 0f, 0.5f),
        };
        Mesh mesh = new Mesh { name = "DirectionArrow" };
        mesh.vertices = v;
        mesh.colors = new[] { color, color, color, color, color, color, color };
        mesh.triangles = new[] { 0, 3, 2, 0, 2, 1, 4, 6, 5 };
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>
    /// Flat disc of diameter 1 lying on the ground.
    /// </summary>
    private static Mesh BuildDisc(Color color, int segments)
    {
        Vector3[] v = new Vector3[segments + 1];
        Color[] c = new Color[segments + 1];
        int[] t = new int[segments * 3];
        v[0] = Vector3.zero;
        c[0] = color;
        for (int i = 0; i < segments; i++)
        {
            float a = i * Mathf.PI * 2f / segments;
            v[i + 1] = new Vector3(Mathf.Sin(a) * 0.5f, 0f, Mathf.Cos(a) * 0.5f);
            c[i + 1] = color;
            t[i * 3] = 0;
            t[i * 3 + 1] = i + 1;
            t[i * 3 + 2] = (i + 1) % segments + 1;
        }
        Mesh mesh = new Mesh { name = "DoorMarker" };
        mesh.vertices = v;
        mesh.colors = c;
        mesh.triangles = t;
        mesh.RecalculateBounds();
        return mesh;
    }
}
