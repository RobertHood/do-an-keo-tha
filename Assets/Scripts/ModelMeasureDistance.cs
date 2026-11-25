using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Measures the distance between the centers of two models, in meters (1 Unity unit = 1 m).
/// Features:
/// - Turn on measure mode (UI Button -> ToggleMeasureMode(), or the M key), then click two models.
///   While measuring, clicks pick models instead of dragging them. Esc cancels.
/// - Draws a line between the two model centers with the distance shown above its middle.
/// - The measurement stays on screen after picking and updates live while either model is moved,
///   so models can be positioned at an exact distance. It is cleared when measuring again,
///   with ClearMeasurement(), or when one of the models is deleted.
/// - Center = center of the model's bounding box. By default the distance is measured along the ground
///   (height differences ignored) and the line is drawn just above the ground.
/// - Procedural: builds its line and label at runtime; created automatically if not in the scene.
/// </summary>
[AddComponentMenu("Interaction/Model Measure Distance")]
public class ModelMeasureDistance : MonoBehaviour
{
    public static ModelMeasureDistance Instance { get; private set; }

    [Header("Measuring")]
    [Tooltip("If true, measures along the ground (ignores height). If false, measures the straight 3D line between the centers.")]
    [SerializeField] private bool horizontalOnly = true;

    [Tooltip("Height of the line above the ground when measuring along the ground.")]
    [SerializeField] private float groundLineLift = 0.05f;

    [SerializeField] private int decimalPlaces = 2;

    [Tooltip("Decimal separator for the label (Vietnamese uses a comma: 3,25 m).")]
    [SerializeField] private string decimalSeparator = ",";

    [Tooltip("{0} = the distance number.")]
    [SerializeField] private string labelFormat = "{0} m";

    [Header("Look")]
    [SerializeField] private Color lineColor = new Color(1f, 0.85f, 0.1f, 1f);

    [Tooltip("Line width in screen pixels (stays the same at any zoom level).")]
    [SerializeField] private float lineWidthPixels = 4f;

    [Tooltip("When measuring along the ground, draws a thin post at each end from the line up to the model's center.")]
    [SerializeField] private bool showCenterPosts = true;

    [SerializeField] private Color labelBackground = new Color(0.07f, 0.07f, 0.07f, 0.85f);
    [SerializeField] private Color labelTextColor = Color.white;
    [SerializeField] private float labelFontSize = 26f;

    [Tooltip("How far above the line's middle the label sits, in screen pixels.")]
    [SerializeField] private float labelOffsetPixels = 28f;

    [Header("References")]
    [Tooltip("Camera used for picking. If empty, Camera.main is used.")]
    [SerializeField] private UnityEngine.Camera targetCamera;

    private bool isMeasureModeActive;
    private Transform pendingFirst;
    private Transform modelA;
    private Transform modelB;

    private LineRenderer mainLine;
    private LineRenderer postA;
    private LineRenderer postB;
    private Material lineMaterial;
    private RectTransform labelRect;
    private TextMeshProUGUI labelText;
    private Canvas overlayCanvas;
    private float lastDistance = -1f;

    /// <summary>
    /// Raised when a measurement is created, cleared, or its distance changes.
    /// </summary>
    public event Action MeasurementChanged;

    public bool IsMeasureModeActive => isMeasureModeActive;
    public bool HasMeasurement => modelA != null && modelB != null;
    public Transform ModelA => modelA;
    public Transform ModelB => modelB;

    /// <summary>
    /// Current distance in meters, or -1 if nothing is measured.
    /// </summary>
    public float Distance => HasMeasurement ? lastDistance : -1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateInstance()
    {
        if (Instance != null || FindAnyObjectByType<ModelMeasureDistance>() != null) return;
        new GameObject("ModelMeasureDistance").AddComponent<ModelMeasureDistance>();
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(this);
            return;
        }

        BuildVisuals();
        SetVisualsVisible(false);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (overlayCanvas != null) Destroy(overlayCanvas.gameObject);
    }

    private void Update()
    {
        if (WasTogglePressed())
        {
            ToggleMeasureMode();
        }

        if (!isMeasureModeActive) return;

        if (GizmoUtility.WasCancelPressed())
        {
            CancelMeasuring();
            return;
        }

        if (GizmoUtility.WasPointerPressed() && !GizmoUtility.IsPointerOverUI())
        {
            Transform picked = PickModel(GizmoUtility.GetPointerPosition());
            if (picked != null) OnModelPicked(picked);
        }
    }

    private void LateUpdate()
    {
        if (!HasMeasurement)
        {
            // One of the measured models was deleted
            if (lastDistance >= 0f) ClearMeasurement();
            return;
        }

        UpdateMeasurementVisuals();
    }

    /// <summary>
    /// Starts measuring, or cancels it if already measuring. (Can be connected to a UI Button OnClick).
    /// </summary>
    public void ToggleMeasureMode()
    {
        if (isMeasureModeActive) CancelMeasuring();
        else EnableMeasureMode(true);
    }

    public void EnableMeasureMode(bool enable)
    {
        isMeasureModeActive = enable;
        pendingFirst = null;

        if (enable)
        {
            ClearMeasurement();

            // Measuring picks models with the mouse, so the other gizmos would compete for clicks
            if (ModelRotate.Instance != null && ModelRotate.Instance.IsRotateModeActive) ModelRotate.Instance.EnableRotateMode(false);
            if (ModelResize.Instance != null && ModelResize.Instance.IsResizeModeActive) ModelResize.Instance.EnableResizeMode(false);
            if (ModelMovement.Instance != null) ModelMovement.Instance.DeselectModel();

            UINotification.ShowPrompt(NotificationTexts.MeasurePickFirst);
        }
        else
        {
            UINotification.ClearPrompt();
        }
    }

    /// <summary>
    /// Stops picking models without measuring (Esc, or the measure button pressed again).
    /// </summary>
    public void CancelMeasuring()
    {
        if (!isMeasureModeActive) return;

        if (pendingFirst != null && ModelMovement.Instance != null) ModelMovement.Instance.DeselectModel();
        EnableMeasureMode(false);
        UINotification.ShowInfo(NotificationTexts.MeasureCancelled);
    }

    /// <summary>
    /// Measures between two models directly (e.g. from code or the web page).
    /// </summary>
    public void Measure(Transform first, Transform second)
    {
        if (first == null || second == null || first == second) return;

        modelA = first;
        modelB = second;
        lastDistance = -1f;
        SetVisualsVisible(true);
        UpdateMeasurementVisuals();
    }

    /// <summary>
    /// Removes the line and label. (Can be connected to a UI Button OnClick).
    /// </summary>
    public void ClearMeasurement()
    {
        bool hadMeasurement = lastDistance >= 0f;
        modelA = null;
        modelB = null;
        lastDistance = -1f;
        SetVisualsVisible(false);
        if (hadMeasurement) MeasurementChanged?.Invoke();
    }

    /// <summary>
    /// Center of the model's bounding box in world space.
    /// </summary>
    public static Vector3 GetModelCenter(Transform model)
    {
        Bounds local = GizmoUtility.GetLocalBounds(model);
        return model.TransformPoint(local.center);
    }

    /// <summary>
    /// World height of the model's lowest point (its base).
    /// </summary>
    private static float GetModelBottom(Transform model)
    {
        float lowest = float.MaxValue;
        foreach (Renderer r in model.GetComponentsInChildren<Renderer>())
        {
            if (r is LineRenderer || r is TrailRenderer || r is ParticleSystemRenderer) continue;
            lowest = Mathf.Min(lowest, r.bounds.min.y);
        }
        return lowest == float.MaxValue ? model.position.y : lowest;
    }

    private void OnModelPicked(Transform picked)
    {
        if (pendingFirst == null)
        {
            pendingFirst = picked;
            if (ModelMovement.Instance != null) ModelMovement.Instance.SelectModel(picked); // Shows which model was picked
            UINotification.ShowPrompt(NotificationTexts.MeasurePickSecond);
            return;
        }

        if (picked == pendingFirst)
        {
            UINotification.ShowError(NotificationTexts.MeasurePickDifferent);
            return;
        }

        Transform first = pendingFirst;
        EnableMeasureMode(false);
        if (ModelMovement.Instance != null) ModelMovement.Instance.DeselectModel();
        Measure(first, picked);

        if (HasMeasurement)
        {
            UINotification.ShowInfo(string.Format(NotificationTexts.MeasureResult, FormatDistance(lastDistance)));
        }
    }

    private Transform PickModel(Vector2 screenPosition)
    {
        UnityEngine.Camera cam = GetCamera();
        if (cam == null) return null;

        RaycastHit[] hits = Physics.RaycastAll(cam.ScreenPointToRay(screenPosition), 1000f);
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            DraggableModel model = hit.transform.GetComponentInParent<DraggableModel>();
            if (model != null) return model.transform;
        }
        return null;
    }

    private void UpdateMeasurementVisuals()
    {
        UnityEngine.Camera cam = GetCamera();
        Vector3 centerA = GetModelCenter(modelA);
        Vector3 centerB = GetModelCenter(modelB);

        Vector3 start = centerA;
        Vector3 end = centerB;
        float distance;

        if (horizontalOnly)
        {
            float groundY = Mathf.Max(GetModelBottom(modelA), GetModelBottom(modelB)) + groundLineLift;
            start = new Vector3(centerA.x, groundY, centerA.z);
            end = new Vector3(centerB.x, groundY, centerB.z);
            distance = Vector2.Distance(new Vector2(centerA.x, centerA.z), new Vector2(centerB.x, centerB.z));
        }
        else
        {
            distance = Vector3.Distance(centerA, centerB);
        }

        Vector3 middle = (start + end) * 0.5f;
        float width = cam != null ? GizmoUtility.PixelsToWorld(cam, middle, lineWidthPixels) : 0.05f;

        SetLine(mainLine, start, end, width);

        bool posts = horizontalOnly && showCenterPosts;
        postA.gameObject.SetActive(posts);
        postB.gameObject.SetActive(posts);
        if (posts)
        {
            SetLine(postA, start, centerA, width * 0.6f);
            SetLine(postB, end, centerB, width * 0.6f);
        }

        if (!Mathf.Approximately(distance, lastDistance))
        {
            lastDistance = distance;
            labelText.text = FormatDistance(distance);
            MeasurementChanged?.Invoke();
        }

        PlaceLabel(cam, middle);
    }

    public string FormatDistance(float meters)
    {
        string number = meters.ToString("F" + Mathf.Max(0, decimalPlaces), CultureInfo.InvariantCulture);
        if (!string.IsNullOrEmpty(decimalSeparator)) number = number.Replace(".", decimalSeparator);
        return string.Format(labelFormat, number);
    }

    private void PlaceLabel(UnityEngine.Camera cam, Vector3 worldPoint)
    {
        if (cam == null)
        {
            labelRect.gameObject.SetActive(false);
            return;
        }

        Vector3 screen = cam.WorldToScreenPoint(worldPoint);
        bool visible = screen.z > 0f;
        labelRect.gameObject.SetActive(visible);
        if (visible)
        {
            labelRect.position = new Vector3(screen.x, screen.y + labelOffsetPixels, 0f);
        }
    }

    private static void SetLine(LineRenderer line, Vector3 a, Vector3 b, float width)
    {
        line.startWidth = width;
        line.endWidth = width;
        line.SetPosition(0, a);
        line.SetPosition(1, b);
    }

    private UnityEngine.Camera GetCamera()
    {
        if (targetCamera != null) return targetCamera;
        return UnityEngine.Camera.main != null ? UnityEngine.Camera.main : FindAnyObjectByType<UnityEngine.Camera>();
    }

    private static bool WasTogglePressed()
    {
        // Don't react to M while typing into a UI text field
        GameObject focused = UnityEngine.EventSystems.EventSystem.current != null
            ? UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject
            : null;
        if (focused != null && focused.TryGetComponent(out TMP_InputField input) && input.isFocused) return false;

#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.mKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.M);
#endif
    }

    private void SetVisualsVisible(bool visible)
    {
        if (mainLine != null) mainLine.gameObject.SetActive(visible);
        if (postA != null) postA.gameObject.SetActive(visible && horizontalOnly && showCenterPosts);
        if (postB != null) postB.gameObject.SetActive(visible && horizontalOnly && showCenterPosts);
        if (labelRect != null) labelRect.gameObject.SetActive(visible);
    }

    private void BuildVisuals()
    {
        lineMaterial = GizmoUtility.CreateLineMaterial();
        mainLine = CreateLine("_MeasureLine");
        postA = CreateLine("_MeasurePostA");
        postB = CreateLine("_MeasurePostB");

        // Screen-space overlay so the label is always readable, on top of the scene, at a constant size
        GameObject canvasObj = new GameObject("_MeasureCanvas");
        DontDestroyOnLoad(canvasObj);
        overlayCanvas = canvasObj.AddComponent<Canvas>();
        overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        overlayCanvas.sortingOrder = 500;

        labelRect = CreateBadge(canvasObj.transform, "_MeasureLabel", labelFontSize, out labelText);
    }

    private LineRenderer CreateLine(string objectName)
    {
        GameObject obj = new GameObject(objectName);
        obj.transform.SetParent(transform, false);

        LineRenderer line = obj.AddComponent<LineRenderer>();
        line.material = lineMaterial;
        line.startColor = lineColor;
        line.endColor = lineColor;
        line.positionCount = 2;
        line.useWorldSpace = true;
        line.numCapVertices = 2;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        return line;
    }

    /// <summary>
    /// A text label on a dark rounded-ish background that sizes itself to the text.
    /// </summary>
    private RectTransform CreateBadge(Transform parent, string objectName, float fontSize, out TextMeshProUGUI text)
    {
        GameObject badge = new GameObject(objectName, typeof(RectTransform));
        badge.transform.SetParent(parent, false);

        Image background = badge.AddComponent<Image>();
        background.color = labelBackground;
        background.raycastTarget = false;

        HorizontalLayoutGroup layout = badge.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(14, 14, 6, 6);
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = badge.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        GameObject textObj = new GameObject("Text", typeof(RectTransform));
        textObj.transform.SetParent(badge.transform, false);
        text = textObj.AddComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.fontStyle = FontStyles.Bold;
        text.color = labelTextColor;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;

        RectTransform rect = (RectTransform)badge.transform;
        rect.pivot = new Vector2(0.5f, 0f);
        return rect;
    }
}
