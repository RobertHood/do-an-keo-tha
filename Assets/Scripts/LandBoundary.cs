using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shapes of a plot of land the user can describe in the land panel (UILandBoundary).
/// </summary>
public enum LandShape
{
    Rectangle = 0,  // Chữ nhật
    Trapezoid = 1,  // Hình thang: front and back widths differ (trước rộng sau hẹp, or the reverse)
    LShape = 2,     // Chữ L: a rectangle with one back corner cut away
    TShape = 3,     // Chữ T: a wide bar plus a narrower stem
    Triangle = 4,   // Tam giác
}

/// <summary>
/// The plot of land as the user described it. Saved in blueprints (BlueprintData.land).
/// Plot space: x = across the plot (rộng), z = from the front edge (mặt tiền, -z) to the back (mặt hậu, +z).
/// </summary>
[Serializable]
public class LandData
{
    [Tooltip("False in blueprints saved before plots existed.")]
    public bool hasLand = true;
    public LandShape shape = LandShape.Rectangle;

    [Tooltip("Width in metres (rectangle, L and triangle: overall; trapezoid: front edge; T: the wide bar).")]
    public float width = 30f;
    [Tooltip("Depth in metres, front edge to back edge.")]
    public float depth = 40f;
    [Tooltip("Trapezoid: width of the back edge (mặt hậu).")]
    public float backWidth = 20f;
    [Tooltip("L: width of the cut-away corner. T: width of the stem.")]
    public float cutWidth = 12f;
    [Tooltip("L: depth of the cut-away corner. T: depth of the wide bar.")]
    public float cutDepth = 15f;
    [Tooltip("L: cut the back-left corner instead of the back-right. T and triangle: swap front and back.")]
    public bool flip;

    [Tooltip("Compass bearing the front edge faces (0 = Bắc, 90 = Đông, 180 = Nam, 270 = Tây). +Z is North.")]
    public float frontBearing = 180f;
    public float centerX;
    public float centerZ;

    public LandData Clone() => (LandData)MemberwiseClone();
}

/// <summary>
/// The user's plot of land in the 3D view:
/// - draws its boundary (tinted ground, border line, corner posts, an arrow and a line on the front edge),
/// - edge lengths and "Mặt tiền" labels while the land panel is open,
/// - checks every placed model: one placed outside the plot flashes red, stays tinted, and the notification
///   box tells the user they are building outside their land.
/// North is fixed to +Z; the user turns the plot (front bearing) instead of a compass.
/// Created automatically if the scene doesn't have one.
/// </summary>
[AddComponentMenu("Blueprint/Land Boundary")]
public class LandBoundary : MonoBehaviour
{
    public static LandBoundary Instance { get; private set; }

    public const float MinSize = 2f;
    public const float MaxSize = 500f;

    [Header("Land")]
    [SerializeField] private LandData land = new LandData();

    [Tooltip("Models may stick out of the boundary by this much (metres) and still count as inside, " +
             "e.g. a fence standing on the boundary line.")]
    [SerializeField] private float tolerance = 0.3f;

    [Header("Visuals")]
    [SerializeField] private Color fillColor = new Color(0.106f, 0.388f, 0.243f, 0.16f);
    [SerializeField] private Color borderColor = new Color(1f, 1f, 1f, 0.95f);
    [SerializeField] private Color frontColor = new Color(0.96f, 0.62f, 0.04f, 1f);
    [SerializeField] private float borderWidth = 0.2f;
    [SerializeField] private float frontWidth = 0.35f;
    [SerializeField] private bool showCornerPosts = true;
    [SerializeField] private float postHeight = 0.9f;
    [SerializeField] private float postThickness = 0.18f;

    [Header("Models outside the plot")]
    [SerializeField] private Color outsideTint = new Color(1f, 0.12f, 0.12f, 1f);
    [Tooltip("How long a model flashes red after it is placed outside the plot.")]
    [SerializeField] private float flashDuration = 1.5f;
    [SerializeField] private int flashCount = 3;
    [Tooltip("Keep a light red tint on models that stay outside the plot.")]
    [SerializeField] private bool keepTintWhileOutside = true;
    [Range(0f, 1f)]
    [SerializeField] private float steadyTintStrength = 0.4f;

    /// <summary>
    /// Raised after the plot changes (shape, size, direction or position).
    /// </summary>
    public event Action Changed;

    public LandData Data => land.Clone();
    public LandShape Shape => land.shape;
    public float FrontBearing => CompassDirections.Normalize(land.frontBearing);
    public Vector3 Center => new Vector3(land.centerX, groundHeight, land.centerZ);
    public float Area { get; private set; }
    public float Perimeter { get; private set; }

    /// <summary>
    /// Corners of the boundary in world space (on the ground), in order around the plot.
    /// </summary>
    public IReadOnlyList<Vector3> Corners => corners;

    /// <summary>
    /// How many placed models are (partly) outside the plot.
    /// </summary>
    public int OutsideCount { get; private set; }

    private readonly List<Vector3> corners = new List<Vector3>();
    private readonly List<Vector2> outline = new List<Vector2>(); // Plot space (x, z)
    private readonly List<int> frontEdges = new List<int>();      // Edge i goes from corner i to corner i + 1
    private float groundHeight;

    private Material material;
    private Mesh fillMesh;
    private Mesh arrowMesh;
    private LineRenderer border;
    private LineRenderer frontLine;
    private Transform arrow;
    private readonly List<Transform> posts = new List<Transform>();

    private Canvas labelCanvas;
    private readonly List<WorldLabel> edgeLabels = new List<WorldLabel>();
    private WorldLabel frontLabel;
    private bool labelsVisible;

    private SpawnInModel spawner;
    private readonly Dictionary<DraggableModel, ModelState> states = new Dictionary<DraggableModel, ModelState>();
    private readonly List<DraggableModel> newlyOutside = new List<DraggableModel>();
    private readonly List<DraggableModel> staleStates = new List<DraggableModel>();
    private readonly Vector3[] footprint = new Vector3[4];
    private bool landJustChanged;
    private bool notifyLandChange;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateIfMissing()
    {
        if (FindAnyObjectByType<LandBoundary>(FindObjectsInactive.Include) == null)
        {
            new GameObject("LandBoundary").AddComponent<LandBoundary>();
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;

        material = GizmoUtility.CreateLineMaterial(); // Sprites/Default: vertex colors, transparency, no culling
        material.renderQueue = 3000;
        groundHeight = FindGroundHeight(new Vector3(land.centerX, 0f, land.centerZ));
        BuildVisuals();
        Rebuild();
    }

    private void Start()
    {
        spawner = FindAnyObjectByType<SpawnInModel>();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (labelCanvas != null) Destroy(labelCanvas.gameObject);
        if (material != null) Destroy(material);
        if (fillMesh != null) Destroy(fillMesh);
        if (arrowMesh != null) Destroy(arrowMesh);
        if (postMesh != null) Destroy(postMesh);
    }

    // ------------------------------------------------------------------ plot

    /// <summary>
    /// Replaces the plot. Values out of range are corrected; the returned text says what was corrected
    /// (Vietnamese, for the notification box), or null if everything was valid.
    /// notify: tell the user if models end up outside the new boundary (off when loading a blueprint).
    /// </summary>
    public string Apply(LandData data, bool notify = true)
    {
        if (data == null) return null;

        LandData next = data.Clone();
        next.hasLand = true;
        string adjusted = Validate(next);

        land = next;
        groundHeight = FindGroundHeight(new Vector3(land.centerX, 0f, land.centerZ));
        Rebuild();

        landJustChanged = true;
        notifyLandChange = notify;
        Changed?.Invoke();
        return adjusted;
    }

    /// <summary>
    /// Clamps sizes into range and keeps cut-outs smaller than the plot. Returns what was changed, or null.
    /// </summary>
    public static string Validate(LandData d)
    {
        List<string> notes = new List<string>();

        if (!Enum.IsDefined(typeof(LandShape), d.shape)) d.shape = LandShape.Rectangle;
        if (ClampSize(ref d.width) | ClampSize(ref d.depth) |
            (d.shape == LandShape.Trapezoid && ClampSize(ref d.backWidth)))
        {
            notes.Add($"kích thước phải từ {UIBuild.FormatNumber(MinSize)} đến {UIBuild.FormatNumber(MaxSize)} m");
        }

        if (d.shape == LandShape.LShape || d.shape == LandShape.TShape)
        {
            float maxCutWidth = d.width - 1f;
            float maxCutDepth = d.depth - 1f;
            bool changed = false;
            if (d.cutWidth < 1f) { d.cutWidth = 1f; changed = true; }
            if (d.cutDepth < 1f) { d.cutDepth = 1f; changed = true; }
            if (d.cutWidth > maxCutWidth) { d.cutWidth = maxCutWidth; changed = true; }
            if (d.cutDepth > maxCutDepth) { d.cutDepth = maxCutDepth; changed = true; }
            if (changed)
            {
                notes.Add(d.shape == LandShape.LShape
                    ? "phần khuyết phải nhỏ hơn chiều rộng và chiều sâu thửa đất ít nhất 1 m"
                    : "phần thân và phần ngang phải nhỏ hơn kích thước tổng ít nhất 1 m");
            }
        }

        d.frontBearing = CompassDirections.Normalize(d.frontBearing);
        return notes.Count > 0 ? string.Join("; ", notes) : null;
    }

    private static bool ClampSize(ref float value)
    {
        float clamped = float.IsNaN(value) ? MinSize : Mathf.Clamp(value, MinSize, MaxSize);
        bool changed = !Mathf.Approximately(clamped, value);
        value = clamped;
        return changed;
    }

    /// <summary>
    /// Outline of a plot in plot space (x across, z front-to-back), centred on its bounding box.
    /// </summary>
    public static List<Vector2> BuildOutline(LandData d)
    {
        float w = d.width * 0.5f, h = d.depth * 0.5f;
        List<Vector2> points = new List<Vector2>();

        switch (d.shape)
        {
            case LandShape.Trapezoid:
            {
                float back = d.backWidth * 0.5f;
                points.Add(new Vector2(-w, -h));
                points.Add(new Vector2(w, -h));
                points.Add(new Vector2(back, h));
                points.Add(new Vector2(-back, h));
                break;
            }
            case LandShape.LShape:
            {
                float cw = d.cutWidth, cd = d.cutDepth;
                points.Add(new Vector2(-w, -h));
                points.Add(new Vector2(w, -h));
                points.Add(new Vector2(w, h - cd));
                points.Add(new Vector2(w - cw, h - cd));
                points.Add(new Vector2(w - cw, h));
                points.Add(new Vector2(-w, h));
                if (d.flip) Mirror(points, true, false);
                break;
            }
            case LandShape.TShape:
            {
                float stem = d.cutWidth * 0.5f, bar = d.cutDepth;
                points.Add(new Vector2(-stem, -h));
                points.Add(new Vector2(stem, -h));
                points.Add(new Vector2(stem, h - bar));
                points.Add(new Vector2(w, h - bar));
                points.Add(new Vector2(w, h));
                points.Add(new Vector2(-w, h));
                points.Add(new Vector2(-w, h - bar));
                points.Add(new Vector2(-stem, h - bar));
                if (d.flip) Mirror(points, false, true);
                break;
            }
            case LandShape.Triangle:
                points.Add(new Vector2(-w, -h));
                points.Add(new Vector2(w, -h));
                points.Add(new Vector2(0f, h));
                if (d.flip) Mirror(points, false, true);
                break;
            default:
                points.Add(new Vector2(-w, -h));
                points.Add(new Vector2(w, -h));
                points.Add(new Vector2(w, h));
                points.Add(new Vector2(-w, h));
                break;
        }
        return points;
    }

    private static void Mirror(List<Vector2> points, bool x, bool z)
    {
        for (int i = 0; i < points.Count; i++)
        {
            points[i] = new Vector2(x ? -points[i].x : points[i].x, z ? -points[i].y : points[i].y);
        }
        points.Reverse(); // Keep the winding
    }

    /// <summary>
    /// Plot space to world: the plot turns so its front edge (-z) faces the front bearing.
    /// </summary>
    private Vector3 ToWorld(Vector2 p)
    {
        Quaternion turn = Quaternion.Euler(0f, land.frontBearing - 180f, 0f);
        return Center + turn * new Vector3(p.x, 0f, p.y);
    }

    private void Rebuild()
    {
        outline.Clear();
        outline.AddRange(BuildOutline(land));

        corners.Clear();
        foreach (Vector2 p in outline) corners.Add(ToWorld(p));

        // Front edges: the edges lying on the front line of the plot (lowest z in plot space)
        frontEdges.Clear();
        float minZ = float.MaxValue;
        foreach (Vector2 p in outline) minZ = Mathf.Min(minZ, p.y);
        for (int i = 0; i < outline.Count; i++)
        {
            Vector2 a = outline[i], b = outline[(i + 1) % outline.Count];
            if (Mathf.Abs(a.y - minZ) < 0.01f && Mathf.Abs(b.y - minZ) < 0.01f) frontEdges.Add(i);
        }

        Area = Mathf.Abs(SignedArea(outline));
        Perimeter = 0f;
        for (int i = 0; i < outline.Count; i++) Perimeter += Vector2.Distance(outline[i], outline[(i + 1) % outline.Count]);

        UpdateVisuals();
    }

    // ------------------------------------------------------------------ queries

    /// <summary>
    /// True if a ground point is inside the plot (or within the tolerance of its boundary).
    /// </summary>
    public bool ContainsPoint(Vector3 point) => ContainsPoint(point, tolerance);

    private bool ContainsPoint(Vector3 point, float margin)
    {
        if (PointInPolygon(point)) return true;
        return margin > 0f && DistanceToBoundary(point) <= margin;
    }

    /// <summary>
    /// True if the model's whole footprint is inside the plot.
    /// </summary>
    public bool Contains(DraggableModel model)
    {
        if (model == null || corners.Count < 3) return true;
        model.GetFootprint(footprint);

        for (int i = 0; i < 4; i++)
        {
            if (!ContainsPoint(footprint[i], tolerance)) return false;
            if (!ContainsPoint((footprint[i] + footprint[(i + 1) % 4]) * 0.5f, tolerance)) return false;
        }

        // A footprint spanning the cut-out of an L or T shape has all its corners inside but a plot corner
        // poking into it
        foreach (Vector3 corner in corners)
        {
            if (PointInQuad(corner, footprint) && DistanceToQuad(corner, footprint) > tolerance) return false;
        }
        return true;
    }

    /// <summary>
    /// True if the model was found outside the plot in the last check.
    /// </summary>
    public bool IsOutside(DraggableModel model)
    {
        return model != null && states.TryGetValue(model, out ModelState state) && state.outside;
    }

    private bool PointInPolygon(Vector3 p)
    {
        bool inside = false;
        for (int i = 0, j = corners.Count - 1; i < corners.Count; j = i++)
        {
            Vector3 a = corners[i], b = corners[j];
            if ((a.z > p.z) != (b.z > p.z) && p.x < (b.x - a.x) * (p.z - a.z) / (b.z - a.z) + a.x)
            {
                inside = !inside;
            }
        }
        return inside;
    }

    private float DistanceToBoundary(Vector3 p)
    {
        float best = float.MaxValue;
        Vector2 point = new Vector2(p.x, p.z);
        for (int i = 0; i < corners.Count; i++)
        {
            Vector3 a = corners[i], b = corners[(i + 1) % corners.Count];
            best = Mathf.Min(best, GizmoUtility.DistanceToSegment(point, new Vector2(a.x, a.z), new Vector2(b.x, b.z)));
        }
        return best;
    }

    private static bool PointInQuad(Vector3 p, Vector3[] quad)
    {
        bool inside = false;
        for (int i = 0, j = 3; i < 4; j = i++)
        {
            Vector3 a = quad[i], b = quad[j];
            if ((a.z > p.z) != (b.z > p.z) && p.x < (b.x - a.x) * (p.z - a.z) / (b.z - a.z) + a.x)
            {
                inside = !inside;
            }
        }
        return inside;
    }

    private static float DistanceToQuad(Vector3 p, Vector3[] quad)
    {
        float best = float.MaxValue;
        Vector2 point = new Vector2(p.x, p.z);
        for (int i = 0; i < 4; i++)
        {
            Vector3 a = quad[i], b = quad[(i + 1) % 4];
            best = Mathf.Min(best, GizmoUtility.DistanceToSegment(point, new Vector2(a.x, a.z), new Vector2(b.x, b.z)));
        }
        return best;
    }

    private static float SignedArea(List<Vector2> points)
    {
        float area = 0f;
        for (int i = 0; i < points.Count; i++)
        {
            Vector2 a = points[i], b = points[(i + 1) % points.Count];
            area += a.x * b.y - b.x * a.y;
        }
        return area * 0.5f;
    }

    private float FindGroundHeight(Vector3 at)
    {
        RaycastHit[] hits = Physics.RaycastAll(new Vector3(at.x, 1000f, at.z), Vector3.down, 2000f);
        float best = float.MinValue;
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.GetComponentInParent<DraggableModel>() != null) continue;
            string n = hit.collider.gameObject.name.ToLowerInvariant();
            if (!(n.Contains("plane") || n.Contains("ground") || n.Contains("field") || n.Contains("terrain"))) continue;
            best = Mathf.Max(best, hit.point.y);
        }
        return best == float.MinValue ? 0f : best;
    }

    // ------------------------------------------------------------------ visuals

    private void BuildVisuals()
    {
        fillMesh = new Mesh { name = "LandFill" };
        GameObject fill = new GameObject("_LandFill", typeof(MeshFilter), typeof(MeshRenderer));
        fill.transform.SetParent(transform, false);
        fill.GetComponent<MeshFilter>().sharedMesh = fillMesh;
        SetupRenderer(fill.GetComponent<MeshRenderer>());

        border = CreateLine("_LandBorder", borderColor, borderWidth, true);
        frontLine = CreateLine("_LandFront", frontColor, frontWidth, false);

        arrowMesh = BuildArrowMesh(frontColor);
        GameObject arrowObject = new GameObject("_LandFrontArrow", typeof(MeshFilter), typeof(MeshRenderer));
        arrowObject.transform.SetParent(transform, false);
        arrowObject.GetComponent<MeshFilter>().sharedMesh = arrowMesh;
        SetupRenderer(arrowObject.GetComponent<MeshRenderer>());
        arrow = arrowObject.transform;

        labelCanvas = UIBuild.OverlayCanvas("_LandLabels", -10);
        frontLabel = new WorldLabel(labelCanvas, "_FrontLabel", UIPalette.Amber700, UIPalette.White, 13f);
    }

    private void SetupRenderer(Renderer r)
    {
        r.sharedMaterial = material;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
    }

    private LineRenderer CreateLine(string name, Color color, float width, bool loop)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // Lie flat: the ribbon faces up instead of the camera
        LineRenderer line = go.AddComponent<LineRenderer>();
        line.sharedMaterial = material;
        line.useWorldSpace = true;
        line.loop = loop;
        line.alignment = LineAlignment.TransformZ;
        line.startColor = line.endColor = color;
        line.startWidth = line.endWidth = width;
        line.numCornerVertices = 3;
        line.numCapVertices = 2;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        return line;
    }

    private void UpdateVisuals()
    {
        if (fillMesh == null) return;

        float fillY = groundHeight + 0.03f;
        float lineY = groundHeight + 0.06f;

        // Fill
        Vector3[] vertices = new Vector3[corners.Count];
        Color[] colors = new Color[corners.Count];
        for (int i = 0; i < corners.Count; i++)
        {
            vertices[i] = new Vector3(corners[i].x, fillY, corners[i].z);
            colors[i] = fillColor;
        }
        fillMesh.Clear();
        fillMesh.vertices = vertices;
        fillMesh.colors = colors;
        fillMesh.triangles = Triangulate(outline);
        fillMesh.RecalculateBounds();

        // Border
        border.positionCount = corners.Count;
        for (int i = 0; i < corners.Count; i++) border.SetPosition(i, new Vector3(corners[i].x, lineY, corners[i].z));

        // Front edge(s) and the arrow in front of the plot
        Vector3 frontMid = Center;
        if (frontEdges.Count > 0)
        {
            frontLine.positionCount = frontEdges.Count * 2;
            Vector3 sum = Vector3.zero;
            for (int k = 0; k < frontEdges.Count; k++)
            {
                Vector3 a = corners[frontEdges[k]], b = corners[(frontEdges[k] + 1) % corners.Count];
                frontLine.SetPosition(k * 2, new Vector3(a.x, lineY + 0.01f, a.z));
                frontLine.SetPosition(k * 2 + 1, new Vector3(b.x, lineY + 0.01f, b.z));
                sum += (a + b) * 0.5f;
            }
            frontMid = sum / frontEdges.Count;
            frontLine.gameObject.SetActive(true);
        }
        else
        {
            // Triangle pointing at the road: the front is its tip
            float minZ = float.MaxValue;
            int tip = 0;
            for (int i = 0; i < outline.Count; i++) if (outline[i].y < minZ) { minZ = outline[i].y; tip = i; }
            frontMid = corners[tip];
            frontLine.gameObject.SetActive(false);
        }

        Vector3 frontDir = CompassDirections.ToDirection(land.frontBearing);
        float arrowSize = Mathf.Clamp(Mathf.Min(land.width, land.depth) * 0.12f, 1.5f, 5f);
        arrow.position = new Vector3(frontMid.x, fillY + 0.01f, frontMid.z) + frontDir * (arrowSize * 0.35f);
        arrow.rotation = Quaternion.LookRotation(frontDir, Vector3.up);
        arrow.localScale = Vector3.one * arrowSize;
        frontLabelAnchor = new Vector3(frontMid.x, groundHeight, frontMid.z) + frontDir * (arrowSize * 1.6f);

        // Corner posts
        while (posts.Count < corners.Count) posts.Add(CreatePost());
        for (int i = 0; i < posts.Count; i++)
        {
            bool used = showCornerPosts && i < corners.Count;
            posts[i].gameObject.SetActive(used);
            if (!used) continue;
            posts[i].position = new Vector3(corners[i].x, groundHeight + postHeight * 0.5f, corners[i].z);
            posts[i].localScale = new Vector3(postThickness, postHeight * 0.5f, postThickness);
        }

        // Edge length labels
        while (edgeLabels.Count < corners.Count)
        {
            edgeLabels.Add(new WorldLabel(labelCanvas, "_EdgeLabel", UIPalette.WithAlpha(UIPalette.BrandBlack, 0.85f), UIPalette.White, 13f));
        }
        for (int i = 0; i < edgeLabels.Count; i++)
        {
            if (i >= corners.Count)
            {
                edgeLabels[i].Hide();
                continue;
            }
            float length = Vector3.Distance(corners[i], corners[(i + 1) % corners.Count]);
            edgeLabels[i].SetText(UIBuild.FormatNumber(length) + " m");
        }
        frontLabel.SetText("Mặt tiền · " + CompassDirections.Name(land.frontBearing));
    }

    private Vector3 frontLabelAnchor;

    private Mesh postMesh;

    private Transform CreatePost()
    {
        if (postMesh == null)
        {
            // Primitive cylinder with white vertex colors (the Sprites/Default shader multiplies by them)
            GameObject primitive = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            postMesh = Instantiate(primitive.GetComponent<MeshFilter>().sharedMesh);
            Destroy(primitive);
            Color[] white = new Color[postMesh.vertexCount];
            for (int i = 0; i < white.Length; i++) white[i] = Color.white;
            postMesh.colors = white;
        }

        // No collider: posts must never block clicks or model dragging
        GameObject post = new GameObject("_LandPost", typeof(MeshFilter), typeof(MeshRenderer));
        post.transform.SetParent(transform, false);
        post.GetComponent<MeshFilter>().sharedMesh = postMesh;
        SetupRenderer(post.GetComponent<MeshRenderer>());
        return post.transform;
    }

    /// <summary>
    /// Flat arrow lying on the ground, pointing +Z, 1 unit long.
    /// </summary>
    private static Mesh BuildArrowMesh(Color color)
    {
        Vector3[] v =
        {
            new Vector3(-0.18f, 0f, -0.5f), new Vector3(0.18f, 0f, -0.5f), new Vector3(0.18f, 0f, 0.05f), new Vector3(-0.18f, 0f, 0.05f),
            new Vector3(-0.45f, 0f, 0.05f), new Vector3(0.45f, 0f, 0.05f), new Vector3(0f, 0f, 0.5f),
        };
        Mesh mesh = new Mesh { name = "LandArrow" };
        mesh.vertices = v;
        mesh.colors = new[] { color, color, color, color, color, color, color };
        mesh.triangles = new[] { 0, 3, 2, 0, 2, 1, 4, 6, 5 };
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>
    /// Ear-clipping triangulation of a simple polygon (works for the concave L and T shapes).
    /// </summary>
    private static int[] Triangulate(List<Vector2> points)
    {
        List<int> result = new List<int>();
        List<int> remaining = new List<int>();
        for (int i = 0; i < points.Count; i++) remaining.Add(i);
        bool ccw = SignedArea(points) > 0f;

        int guard = 0;
        while (remaining.Count > 3 && guard++ < 1000)
        {
            bool clipped = false;
            for (int i = 0; i < remaining.Count; i++)
            {
                int prev = remaining[(i + remaining.Count - 1) % remaining.Count];
                int cur = remaining[i];
                int next = remaining[(i + 1) % remaining.Count];
                Vector2 a = points[prev], b = points[cur], c = points[next];
                float cross = (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
                if (ccw ? cross <= 0f : cross >= 0f) continue; // Reflex corner

                bool contains = false;
                foreach (int other in remaining)
                {
                    if (other == prev || other == cur || other == next) continue;
                    if (InTriangle(points[other], a, b, c)) { contains = true; break; }
                }
                if (contains) continue;

                result.Add(prev); result.Add(cur); result.Add(next);
                remaining.RemoveAt(i);
                clipped = true;
                break;
            }
            if (!clipped) break;
        }
        if (remaining.Count == 3) { result.Add(remaining[0]); result.Add(remaining[1]); result.Add(remaining[2]); }
        return result.ToArray();
    }

    private static bool InTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float d1 = (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);
        float d2 = (p.x - c.x) * (b.y - c.y) - (b.x - c.x) * (p.y - c.y);
        float d3 = (p.x - a.x) * (c.y - a.y) - (c.x - a.x) * (p.y - a.y);
        bool neg = d1 < 0f || d2 < 0f || d3 < 0f;
        bool pos = d1 > 0f || d2 > 0f || d3 > 0f;
        return !(neg && pos);
    }

    /// <summary>
    /// Shows the edge lengths and the "Mặt tiền" label (the land panel turns them on while it is open).
    /// </summary>
    public void SetLabelsVisible(bool visible)
    {
        labelsVisible = visible;
        if (!visible)
        {
            foreach (WorldLabel label in edgeLabels) label.Hide();
            frontLabel.Hide();
        }
    }

    private void UpdateLabels()
    {
        if (!labelsVisible) return;
        UnityEngine.Camera cam = UnityEngine.Camera.main;
        for (int i = 0; i < corners.Count && i < edgeLabels.Count; i++)
        {
            edgeLabels[i].Show(cam, (corners[i] + corners[(i + 1) % corners.Count]) * 0.5f);
        }
        frontLabel.Show(cam, frontLabelAnchor);
    }

    // ------------------------------------------------------------------ models outside the plot

    private class ModelState
    {
        public bool seen;            // Checked at least once while nobody was moving it
        public bool outside;         // Latest check
        public bool settledOutside;  // Outside when last let go
        public float flashStart = -1f;
        public ModelTint tint;
    }

    private void LateUpdate()
    {
        UpdateLabels();
        CheckModels();
    }

    private void CheckModels()
    {
        if (spawner == null) spawner = FindAnyObjectByType<SpawnInModel>();
        Transform container = spawner != null ? spawner.ModelsContainer : null;
        if (container == null) return;

        Transform busy = GetModelBeingEdited();
        float now = Time.unscaledTime;
        newlyOutside.Clear();
        int outsideCount = 0;

        foreach (Transform child in container)
        {
            if (!child.gameObject.activeInHierarchy || !child.TryGetComponent(out DraggableModel model)) continue;

            if (!states.TryGetValue(model, out ModelState state))
            {
                state = new ModelState { tint = new ModelTint(model) };
                states.Add(model, state);
            }

            bool outside = !Contains(model);
            state.outside = outside;
            if (outside) outsideCount++;

            if (child == busy)
            {
                // Live warning while the model is being dragged, resized or rotated: pulse red
                state.tint.Apply(outside ? Pulse(now, 0.25f, 0.65f) : 0f, outsideTint);
                continue;
            }

            bool changed;
            if (!state.seen)
            {
                state.seen = true;
                changed = outside; // Just spawned or loaded
            }
            else
            {
                changed = outside != state.settledOutside;
            }

            if (changed && outside)
            {
                state.flashStart = now;
                newlyOutside.Add(model);
            }
            state.settledOutside = outside;

            float strength = 0f;
            if (outside)
            {
                float t = now - state.flashStart;
                if (state.flashStart >= 0f && t < flashDuration)
                {
                    // Blink on/off flashCount times
                    float phase = t / flashDuration * flashCount;
                    strength = phase - Mathf.Floor(phase) < 0.55f ? 0.85f : 0.1f;
                }
                else
                {
                    strength = keepTintWhileOutside ? steadyTintStrength : 0f;
                }
            }
            state.tint.Apply(strength, outsideTint);
        }

        // Forget destroyed models
        staleStates.Clear();
        foreach (KeyValuePair<DraggableModel, ModelState> pair in states) if (pair.Key == null) staleStates.Add(pair.Key);
        foreach (DraggableModel stale in staleStates) states.Remove(stale);

        OutsideCount = outsideCount;
        bool quiet = landJustChanged && !notifyLandChange; // Loading a blueprint: the models still flash, no message
        if (newlyOutside.Count > 0 && !quiet) Notify(newlyOutside, landJustChanged);
        landJustChanged = false;
    }

    private void Notify(List<DraggableModel> models, bool becauseLandChanged)
    {
        if (becauseLandChanged)
        {
            UINotification.ShowError(string.Format(NotificationTexts.LandChangedOutside, models.Count));
        }
        else if (models.Count == 1)
        {
            UINotification.ShowError(string.Format(NotificationTexts.LandOutside, ModelEstPrice.GetDisplayName(models[0], models[0].CatalogEntry)));
        }
        else
        {
            UINotification.ShowError(string.Format(NotificationTexts.LandOutsideMany, models.Count));
        }
    }

    private static float Pulse(float time, float min, float max)
    {
        return Mathf.Lerp(min, max, 0.5f + 0.5f * Mathf.Sin(time * 10f));
    }

    private static Transform GetModelBeingEdited()
    {
        if (ModelMovement.Instance != null && ModelMovement.Instance.IsDragging) return ModelMovement.Instance.CurrentDraggedModel;
        if (ModelResize.Instance != null && ModelResize.Instance.IsDraggingHandle) return ModelResize.Instance.TargetModel;
        if (ModelRotate.Instance != null && ModelRotate.Instance.IsDraggingRing) return ModelRotate.Instance.TargetModel;
        return null;
    }

    /// <summary>
    /// Tints a model's materials towards a color without touching the shared materials.
    /// </summary>
    private class ModelTint
    {
        private readonly Renderer[] renderers;
        private readonly Color[][] baseColors;
        private readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
        private float current;

        public ModelTint(DraggableModel model)
        {
            List<Renderer> list = new List<Renderer>();
            foreach (Renderer r in model.GetComponentsInChildren<Renderer>())
            {
                if (r is LineRenderer || r is TrailRenderer || r is ParticleSystemRenderer) continue;
                list.Add(r);
            }
            renderers = list.ToArray();
            baseColors = new Color[renderers.Length][];
            for (int i = 0; i < renderers.Length; i++)
            {
                Material[] materials = renderers[i].sharedMaterials;
                baseColors[i] = new Color[materials.Length];
                for (int m = 0; m < materials.Length; m++)
                {
                    Material mat = materials[m];
                    baseColors[i][m] = mat == null ? Color.white
                        : mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor")
                        : mat.HasProperty("_Color") ? mat.GetColor("_Color")
                        : Color.white;
                }
            }
        }

        public void Apply(float strength, Color tint)
        {
            if (Mathf.Abs(strength - current) < 0.01f) return;
            current = strength;

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null) continue;
                for (int m = 0; m < baseColors[i].Length; m++)
                {
                    block.Clear(); // An empty block removes the override
                    if (strength > 0.001f)
                    {
                        Color c = Color.Lerp(baseColors[i][m], tint, strength);
                        c.a = baseColors[i][m].a;
                        block.SetColor("_BaseColor", c);
                        block.SetColor("_Color", c);
                    }
                    r.SetPropertyBlock(block, m);
                }
            }
        }
    }
}
