using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The land panel: the user picks the shape of their plot, types its approximate size in metres and which way
/// its front (mặt tiền) faces. Changes show up on the plot in the 3D view straight away (LandBoundary).
/// - The panel (Canvas child "Land Settings") and the "Thửa đất" toolbar button are normally built into the scene
///   with Tools > Suburbsim > Build Land Panel and Compass, so they can be arranged in the editor. This script only
///   connects them. If they are missing, it builds them when Play starts.
/// Created automatically if the scene doesn't have one.
/// </summary>
[AddComponentMenu("UI/UI Land Boundary")]
public class UILandBoundary : MonoBehaviour
{
    public static UILandBoundary Instance { get; private set; }

    [Tooltip("Panel holding the land settings. If empty, the Canvas child named \"Land Settings\" is used.")]
    [SerializeField] private RectTransform panel;

    [Tooltip("Add a \"Thửa đất\" button to the toolbar if the scene doesn't have one.")]
    [SerializeField] private bool addToolbarButton = true;

    [Tooltip("Place the panel at the bottom of the screen when it opens. Turned off when the panel is built into " +
             "the scene, so the position you give it in the editor is kept.")]
    [SerializeField] private bool autoPlacePanel = true;

    [SerializeField] private Vector2 panelSize = new Vector2(900f, 196f);

    [Header("Parts (filled by the build menu)")]
    [SerializeField] private Button toolbarButton;
    [SerializeField] private TextMeshProUGUI summary;
    [SerializeField] private Button closeButton;
    [SerializeField] private List<Button> shapeButtons = new List<Button>();
    [SerializeField] private Button flipButton;
    [SerializeField] private TextMeshProUGUI flipLabel;
    [SerializeField] private List<FieldView> fields = new List<FieldView>();
    [SerializeField] private Button bearingLeft;
    [SerializeField] private Button bearingRight;
    [SerializeField] private TMP_InputField bearingInput;
    [SerializeField] private TextMeshProUGUI bearingName;

    public bool IsOpen => panel != null && panel.gameObject.activeSelf;

    private bool IsBuilt => panel != null && summary != null && closeButton != null && shapeButtons.Count == ShapeNames.Length &&
                            flipButton != null && fields.Count == 4 && bearingInput != null && bearingLeft != null && bearingRight != null;

    private enum Param { Width, Depth, BackWidth, CutWidth, CutDepth }

    private struct FieldSpec
    {
        public Param param;
        public string label;
        public FieldSpec(Param p, string l) { param = p; label = l; }
    }

    private static readonly string[] ShapeNames = { "Chữ nhật", "Hình thang", "Chữ L", "Chữ T", "Tam giác" };

    [Serializable]
    private class FieldView
    {
        public RectTransform root;
        public TextMeshProUGUI label;
        public TMP_InputField input;
        [NonSerialized] public Param param;
    }

    private LandBoundary land;
    private Canvas canvas;
    private bool wired;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateIfMissing()
    {
        if (FindAnyObjectByType<UILandBoundary>(FindObjectsInactive.Include) == null)
        {
            new GameObject("UILandBoundary").AddComponent<UILandBoundary>();
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
    }

    private void Start()
    {
        land = LandBoundary.Instance;
        canvas = UIBuild.FindMainCanvas();
        if (canvas == null)
        {
            Debug.LogWarning("[UILandBoundary] No UI Canvas found.");
            return;
        }

        if (!IsBuilt) Build(canvas);
        if (addToolbarButton && toolbarButton == null) toolbarButton = AddToolbarButton(canvas);

        Wire();
        if (land != null) land.Changed += Refresh;
        Refresh();
        SetOpen(false);
    }

    private void OnDestroy()
    {
        if (land != null) land.Changed -= Refresh;
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (IsOpen && GizmoUtility.WasCancelPressed() && !UIBuild.IsTyping() &&
            (ModelResize.Instance == null || !ModelResize.Instance.IsResizeModeActive) &&
            (ModelRotate.Instance == null || !ModelRotate.Instance.IsRotateModeActive) &&
            (ModelMeasureDistance.Instance == null || !ModelMeasureDistance.Instance.IsMeasureModeActive))
        {
            SetOpen(false);
        }
    }

    // ------------------------------------------------------------------ open / close

    public void Toggle() => SetOpen(!IsOpen);

    public void SetOpen(bool open)
    {
        if (panel == null) return;
        if (open && autoPlacePanel) PlacePanel(canvas);
        panel.gameObject.SetActive(open);
        if (land != null) land.SetLabelsVisible(open);
        if (open)
        {
            Refresh();
            UINotification.ShowTipOnce(NotificationTips.LandKey, NotificationTips.Land);
        }
    }

    /// <summary>
    /// Bottom of the screen, between the item catalog on the left and the notification box on the right.
    /// </summary>
    private void PlacePanel(Canvas target)
    {
        if (target == null || panel == null) return;
        RectTransform canvasRect = (RectTransform)target.transform;
        float left = 24f;
        RectTransform catalog = FindChild(target.transform, "Item Catalog");
        if (catalog != null && catalog.gameObject.activeInHierarchy && Mathf.Approximately(catalog.anchorMin.x, 0f))
        {
            left += catalog.rect.width;
        }

        float available = canvasRect.rect.width - left - 24f;
        float width = Mathf.Clamp(available, 640f, panelSize.x);
        UIBuild.Place(panel, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(left, 24f), new Vector2(width, panelSize.y));
    }

    // ------------------------------------------------------------------ building

    /// <summary>
    /// Builds the panel and the toolbar button into the open scene (called by the editor menu, outside Play mode).
    /// Afterwards the panel keeps the position it is given in the editor.
    /// </summary>
    public void BuildInEditor(Canvas target)
    {
        canvas = target;
        Build(target);
        PlacePanel(target);
        panel.gameObject.SetActive(true); // Visible in the editor; hidden again when Play starts
        autoPlacePanel = false;
        if (toolbarButton == null) toolbarButton = AddToolbarButton(target);
    }

    private void Build(Canvas target)
    {
        if (panel == null) panel = FindChild(target.transform, "Land Settings");
        if (panel == null)
        {
            panel = UIBuild.Rect(target.transform, "Land Settings");
        }
        else
        {
            // The scene panel was a plain scroll view: hide its old parts (kept, in case they are needed again)
            if (panel.TryGetComponent(out ScrollRect scroll)) scroll.enabled = false;
            foreach (Transform child in panel) child.gameObject.SetActive(false);
        }

        if (panel.TryGetComponent(out Image panelImage)) panelImage.enabled = false;
        UIKit kit = UIKit.Get();

        // Card: shadow, white background, outline (same look as the notification box)
        Image shadow = UIBuild.Image(panel, "Shadow", kit != null ? kit.shadow16 : null, new Color(0f, 0f, 0f, 0.14f));
        UIBuild.Stretch(shadow.rectTransform, -24f, -24f, -18f, -30f);
        Image card = UIBuild.Image(panel, "Card", kit != null ? kit.round16 : null, UIPalette.White, true);
        UIBuild.Stretch(card.rectTransform);
        Image outline = UIBuild.Image(panel, "Outline", kit != null ? kit.outline16 : null, UIPalette.Neutral200);
        UIBuild.Stretch(outline.rectTransform);

        RectTransform content = UIBuild.Stretch(UIBuild.Rect(panel, "Content"), 20f, 20f, 16f, 16f);

        // Row 1: title, area / perimeter, close
        RectTransform header = UIBuild.Rect(content, "Header");
        UIBuild.Place(header, new Vector2(0f, 1f), Vector2.one, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 28f));
        TextMeshProUGUI title = UIBuild.Text(header, "Title", "Thửa đất", 18f, UIPalette.BrandBlack, UIBuild.Weight.Bold, TextAlignmentOptions.MidlineLeft);
        UIBuild.Place(title.rectTransform, Vector2.zero, new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(110f, 0f));

        summary = UIBuild.Text(header, "Summary", "", 13f, UIPalette.Neutral500, UIBuild.Weight.Regular, TextAlignmentOptions.MidlineLeft);
        UIBuild.Place(summary.rectTransform, Vector2.zero, Vector2.one, new Vector2(0f, 0.5f), new Vector2(112f, 0f), new Vector2(-160f, 0f));

        closeButton = UIBuild.Button(header, "Close", kit != null ? kit.round8 : null, UIPalette.Neutral100, null);
        UIBuild.Place((RectTransform)closeButton.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(28f, 28f));
        TextMeshProUGUI x = UIBuild.Text(closeButton.transform, "Label", "×", 20f, UIPalette.Neutral600, UIBuild.Weight.Regular, TextAlignmentOptions.Center);
        UIBuild.Stretch(x.rectTransform, 0f, 0f, 0f, 2f);

        // Row 2: shape picker + flip
        RectTransform shapeRow = UIBuild.Rect(content, "Shape Row");
        UIBuild.Place(shapeRow, new Vector2(0f, 1f), Vector2.one, new Vector2(0.5f, 1f), new Vector2(0f, -38f), new Vector2(0f, 36f));
        Image shapeBar = UIBuild.Image(shapeRow, "Shapes", kit != null ? kit.round12 : null, UIPalette.Neutral100);
        UIBuild.Place(shapeBar.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(5 * 108f + 8f, 36f));
        shapeButtons.Clear();
        for (int i = 0; i < ShapeNames.Length; i++)
        {
            Button button = UIBuild.Button(shapeBar.transform, ShapeNames[i], kit != null ? kit.pill : null, UIPalette.Clear, null);
            UIBuild.Place((RectTransform)button.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                          new Vector2(4f + i * 108f, 0f), new Vector2(108f, 28f));
            TextMeshProUGUI label = UIBuild.Text(button.transform, "Label", ShapeNames[i], 13f, UIPalette.Neutral700, UIBuild.Weight.SemiBold, TextAlignmentOptions.Center);
            UIBuild.Stretch(label.rectTransform);
            shapeButtons.Add(button);
        }

        flipButton = UIBuild.Button(shapeRow, "Flip", kit != null ? kit.pill : null, UIPalette.Neutral100, null);
        UIBuild.Place((RectTransform)flipButton.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                      new Vector2(5 * 108f + 20f, 0f), new Vector2(170f, 28f));
        flipLabel = UIBuild.Text(flipButton.transform, "Label", "Khuyết bên trái", 13f, UIPalette.Neutral700, UIBuild.Weight.SemiBold, TextAlignmentOptions.Center);
        UIBuild.Stretch(flipLabel.rectTransform, 8f, 8f, 0f, 0f);

        // Row 3: size fields (up to 4) and the front direction
        RectTransform fieldRow = UIBuild.Rect(content, "Field Row");
        UIBuild.Place(fieldRow, new Vector2(0f, 1f), Vector2.one, new Vector2(0.5f, 1f), new Vector2(0f, -88f), new Vector2(0f, 60f));
        fields.Clear();
        string[] defaultLabels = { "Chiều rộng", "Chiều sâu", "Khuyết · rộng", "Khuyết · sâu" };
        for (int i = 0; i < 4; i++)
        {
            FieldView view = new FieldView();
            view.root = UIBuild.Rect(fieldRow, "Field " + (i + 1));
            UIBuild.Place(view.root, Vector2.zero, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(i * 128f, 0f), new Vector2(116f, 0f));
            view.label = UIBuild.Text(view.root, "Label", defaultLabels[i], 12f, UIPalette.Neutral500, UIBuild.Weight.SemiBold);
            UIBuild.Place(view.label.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(0f, 1f), Vector2.zero, new Vector2(0f, 18f));
            view.input = UIBuild.NumberField(view.root, "Input", "m", null);
            UIBuild.Place((RectTransform)view.input.transform, Vector2.zero, new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, 36f));
            view.input.SetTextWithoutNotify("30");
            fields.Add(view);
        }

        RectTransform direction = UIBuild.Rect(fieldRow, "Direction");
        UIBuild.Place(direction, Vector2.zero, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(4 * 128f + 16f, 0f), new Vector2(330f, 0f));
        TextMeshProUGUI directionLabel = UIBuild.Text(direction, "Label", "Mặt tiền hướng", 12f, UIPalette.Neutral500, UIBuild.Weight.SemiBold);
        UIBuild.Place(directionLabel.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(0f, 1f), Vector2.zero, new Vector2(0f, 18f));

        bearingLeft = StepButton(direction, "Turn Left", "‹", 0f);
        bearingInput = UIBuild.NumberField(direction, "Bearing", "°", null);
        UIBuild.Place((RectTransform)bearingInput.transform, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(40f, 0f), new Vector2(84f, 36f));
        bearingInput.SetTextWithoutNotify("180");
        bearingRight = StepButton(direction, "Turn Right", "›", 128f);
        bearingName = UIBuild.Text(direction, "Direction Name", "Nam", 15f, UIPalette.Amber700, UIBuild.Weight.Bold, TextAlignmentOptions.MidlineLeft);
        UIBuild.Place(bearingName.rectTransform, Vector2.zero, new Vector2(1f, 0f), Vector2.zero, new Vector2(172f, 0f), new Vector2(-172f, 36f));
    }

    private static Button StepButton(RectTransform parent, string name, string glyph, float x)
    {
        UIKit kit = UIKit.Get();
        Button button = UIBuild.Button(parent, name, kit != null ? kit.round8 : null, UIPalette.Neutral100, null);
        UIBuild.Place((RectTransform)button.transform, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(x, 0f), new Vector2(36f, 36f));
        TextMeshProUGUI label = UIBuild.Text(button.transform, "Label", glyph, 22f, UIPalette.Neutral700, UIBuild.Weight.SemiBold, TextAlignmentOptions.Center);
        UIBuild.Stretch(label.rectTransform, 0f, 0f, 0f, 3f);
        return button;
    }

    /// <summary>
    /// Clones a toolbar button into a "Thửa đất" button at the start of the toolbar and widens the toolbar to fit.
    /// Returns the existing one if the toolbar already has it.
    /// </summary>
    private static Button AddToolbarButton(Canvas target)
    {
        RectTransform toolbar = FindChild(target.transform, "Functions");
        Transform row = toolbar != null ? toolbar.Find("Viewport/Content") : null;
        if (row == null) return null;

        Transform existing = row.Find("LandButton");
        if (existing != null) return existing.GetComponent<Button>();

        ToolbarButtonState template = row.GetComponentInChildren<ToolbarButtonState>(true);
        if (template == null) return null;

        GameObject clone = Instantiate(template.gameObject, row);
        clone.name = "LandButton";
        clone.transform.SetSiblingIndex(0);

        Button button = clone.GetComponent<Button>();
        button.onClick = new Button.ButtonClickedEvent(); // Drop the template's action; Wire() adds Toggle

        TMP_Text label = clone.GetComponentInChildren<TMP_Text>(true);
        if (label != null) label.text = "Thửa đất";

        Image icon = null;
        foreach (Image image in clone.GetComponentsInChildren<Image>(true))
        {
            if (image.gameObject != clone) { icon = image; break; }
        }
        UIKit kit = UIKit.Get();
        if (icon != null && kit != null && kit.landIcon != null) icon.sprite = kit.landIcon;

        clone.GetComponent<ToolbarButtonState>().Configure(ToolbarButtonState.Tool.Land, false, false, clone.GetComponent<Image>(), icon, label);

        // Divider after it, like the other button groups
        HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();
        float spacing = layout != null ? layout.spacing : 4f;
        float added = ((RectTransform)clone.transform).sizeDelta.x + spacing;
        Transform divider = null;
        foreach (Transform child in row) if (child.name == "_Divider") { divider = child; break; }
        if (divider != null)
        {
            Transform newDivider = Instantiate(divider.gameObject, row).transform;
            newDivider.name = "_Divider";
            newDivider.SetSiblingIndex(1);
            added += ((RectTransform)newDivider).sizeDelta.x + spacing;
        }

        toolbar.sizeDelta += new Vector2(added, 0f);
        RectTransform shadow = FindChild(target.transform, "_Functions Shadow");
        if (shadow != null) shadow.sizeDelta += new Vector2(added, 0f);
        return button;
    }

    /// <summary>
    /// Connects the buttons and text fields (at runtime; the scene objects carry no saved listeners).
    /// </summary>
    private void Wire()
    {
        if (wired || !IsBuilt) return;
        wired = true;

        if (toolbarButton != null)
        {
            toolbarButton.onClick = new Button.ButtonClickedEvent();
            toolbarButton.onClick.AddListener(Toggle);
        }
        closeButton.onClick.AddListener(() => SetOpen(false));
        for (int i = 0; i < shapeButtons.Count; i++)
        {
            LandShape shape = (LandShape)i;
            if (shapeButtons[i] != null) shapeButtons[i].onClick.AddListener(() => SetShape(shape));
        }
        flipButton.onClick.AddListener(ToggleFlip);
        foreach (FieldView view in fields)
        {
            FieldView captured = view;
            if (view.input != null) view.input.onEndEdit.AddListener(value => OnFieldEdited(captured, value));
        }
        bearingInput.onEndEdit.AddListener(OnBearingEdited);
        bearingLeft.onClick.AddListener(() => StepBearing(-1));
        bearingRight.onClick.AddListener(() => StepBearing(1));
    }

    // ------------------------------------------------------------------ state

    private static FieldSpec[] FieldsFor(LandData d)
    {
        switch (d.shape)
        {
            case LandShape.Trapezoid:
                return new[] { new FieldSpec(Param.Width, "Rộng mặt tiền"), new FieldSpec(Param.BackWidth, "Rộng mặt hậu"), new FieldSpec(Param.Depth, "Chiều sâu") };
            case LandShape.LShape:
                return new[] { new FieldSpec(Param.Width, "Chiều rộng"), new FieldSpec(Param.Depth, "Chiều sâu"),
                               new FieldSpec(Param.CutWidth, "Khuyết · rộng"), new FieldSpec(Param.CutDepth, "Khuyết · sâu") };
            case LandShape.TShape:
                return new[] { new FieldSpec(Param.Width, "Rộng phần ngang"), new FieldSpec(Param.CutDepth, "Sâu phần ngang"),
                               new FieldSpec(Param.CutWidth, "Rộng phần thân"), new FieldSpec(Param.Depth, "Sâu tổng") };
            case LandShape.Triangle:
                return new[] { new FieldSpec(Param.Width, "Rộng đáy"), new FieldSpec(Param.Depth, "Chiều sâu") };
            default:
                return new[] { new FieldSpec(Param.Width, "Chiều rộng"), new FieldSpec(Param.Depth, "Chiều sâu") };
        }
    }

    private static string FlipText(LandShape shape)
    {
        switch (shape)
        {
            case LandShape.LShape: return "Khuyết bên trái";
            case LandShape.TShape: return "Phần ngang ở trước";
            case LandShape.Triangle: return "Đáy ở phía sau";
            default: return null;
        }
    }

    private static float Get(LandData d, Param p)
    {
        switch (p)
        {
            case Param.Depth: return d.depth;
            case Param.BackWidth: return d.backWidth;
            case Param.CutWidth: return d.cutWidth;
            case Param.CutDepth: return d.cutDepth;
            default: return d.width;
        }
    }

    private static void Set(LandData d, Param p, float value)
    {
        switch (p)
        {
            case Param.Depth: d.depth = value; break;
            case Param.BackWidth: d.backWidth = value; break;
            case Param.CutWidth: d.cutWidth = value; break;
            case Param.CutDepth: d.cutDepth = value; break;
            default: d.width = value; break;
        }
    }

    /// <summary>
    /// Shows the plot's current values in the panel.
    /// </summary>
    private void Refresh()
    {
        if (!IsBuilt || land == null) return;
        LandData d = land.Data;

        summary.text = $"{ShapeNames[(int)d.shape]} · Diện tích {UIBuild.FormatThousands(land.Area)} m² · Chu vi {UIBuild.FormatNumber(land.Perimeter)} m";

        for (int i = 0; i < shapeButtons.Count; i++)
        {
            if (shapeButtons[i] == null) continue;
            bool active = i == (int)d.shape;
            shapeButtons[i].targetGraphic.color = active ? UIPalette.BrandBlack : UIPalette.Clear;
            TextMeshProUGUI label = shapeButtons[i].GetComponentInChildren<TextMeshProUGUI>();
            if (label != null) label.color = active ? UIPalette.White : UIPalette.Neutral700;
        }

        FieldSpec[] specs = FieldsFor(d);
        for (int i = 0; i < fields.Count; i++)
        {
            FieldView view = fields[i];
            if (view.root == null) continue;
            bool used = i < specs.Length;
            view.root.gameObject.SetActive(used);
            if (!used) continue;
            view.param = specs[i].param;
            if (view.label != null) view.label.text = specs[i].label;
            if (view.input != null && !view.input.isFocused) view.input.SetTextWithoutNotify(UIBuild.FormatNumber(Get(d, view.param)));
        }

        string flip = FlipText(d.shape);
        flipButton.gameObject.SetActive(flip != null);
        if (flip != null)
        {
            if (flipLabel != null)
            {
                flipLabel.text = flip;
                flipLabel.color = d.flip ? UIPalette.BrandGreen : UIPalette.Neutral700;
            }
            flipButton.targetGraphic.color = d.flip ? UIPalette.BrandGreenLight : UIPalette.Neutral100;
        }

        if (!bearingInput.isFocused) bearingInput.SetTextWithoutNotify(Mathf.RoundToInt(d.frontBearing).ToString());
        if (bearingName != null) bearingName.text = CompassDirections.Name(d.frontBearing);
    }

    private void Apply(LandData data)
    {
        if (land == null) return;
        string adjusted = land.Apply(data);
        if (!string.IsNullOrEmpty(adjusted)) UINotification.ShowError(string.Format(NotificationTexts.LandAdjusted, adjusted));
        Refresh();
    }

    private void SetShape(LandShape shape)
    {
        if (land == null) return;
        LandData d = land.Data;
        if (d.shape == shape) return;
        d.shape = shape;
        if (shape == LandShape.Trapezoid && d.backWidth >= d.width) d.backWidth = Mathf.Max(LandBoundary.MinSize, d.width * 0.6f);
        Apply(d);
    }

    private void ToggleFlip()
    {
        if (land == null) return;
        LandData d = land.Data;
        d.flip = !d.flip;
        Apply(d);
    }

    private void OnFieldEdited(FieldView view, string text)
    {
        if (land == null) return;
        LandData d = land.Data;
        if (!UIBuild.TryParseNumber(text, out float value))
        {
            if (!string.IsNullOrWhiteSpace(text)) UINotification.ShowError(NotificationTexts.LandInvalidNumber);
            Refresh();
            return;
        }
        if (Mathf.Approximately(Get(d, view.param), value))
        {
            Refresh();
            return;
        }
        Set(d, view.param, value);
        Apply(d);
    }

    private void OnBearingEdited(string text)
    {
        if (land == null) return;
        if (!UIBuild.TryParseNumber(text, out float value))
        {
            if (!string.IsNullOrWhiteSpace(text)) UINotification.ShowError(NotificationTexts.LandInvalidNumber);
            Refresh();
            return;
        }
        LandData d = land.Data;
        d.frontBearing = value;
        Apply(d);
    }

    /// <summary>
    /// Turns the front to the next of the 8 compass directions.
    /// </summary>
    private void StepBearing(int step)
    {
        if (land == null) return;
        LandData d = land.Data;
        float snapped = Mathf.Round(d.frontBearing / 45f) * 45f;
        d.frontBearing = Mathf.Approximately(snapped, d.frontBearing)
            ? snapped + step * 45f
            : (step > 0 ? Mathf.Ceil(d.frontBearing / 45f) : Mathf.Floor(d.frontBearing / 45f)) * 45f;
        Apply(d);
    }

    private static RectTransform FindChild(Transform root, string name)
    {
        foreach (RectTransform rect in root.GetComponentsInChildren<RectTransform>(true))
        {
            if (rect.name == name) return rect;
        }
        return null;
    }
}
