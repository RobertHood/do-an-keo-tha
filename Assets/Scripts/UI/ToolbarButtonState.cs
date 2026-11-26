using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Styles a toolbar button like the web app's ghost buttons and shows what state its tool is in:
/// - hover / pressed backgrounds,
/// - green "active" look while its mode is on (resize, rotate, measure),
/// - amber look on the lock button while the selected model is locked,
/// - red hover for destructive buttons (delete),
/// - dimmed when the tool needs a selected model and none is selected (it can still be clicked and
///   then explains, through the notification box, that a model must be selected).
/// Added and configured by Tools > Suburbsim > Apply UI Theme.
/// </summary>
[AddComponentMenu("UI/Toolbar Button State")]
[RequireComponent(typeof(Button))]
public class ToolbarButtonState : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    public enum Tool { None, Resize, Rotate, Measure, Lock, Delete }

    [SerializeField] private Tool tool = Tool.None;

    [Tooltip("Dim the button when no model is selected.")]
    [SerializeField] private bool needsSelection;

    [Tooltip("Use the red hover (destructive action).")]
    [SerializeField] private bool destructive;

    [SerializeField] private Image background;
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text label;

    [Range(0f, 1f)]
    [SerializeField] private float dimmedAlpha = 0.45f;

    private bool hovered;
    private bool pressed;

    public void Configure(Tool newTool, bool requiresSelection, bool isDestructive, Image backgroundImage, Image iconImage, TMP_Text labelText)
    {
        tool = newTool;
        needsSelection = requiresSelection;
        destructive = isDestructive;
        background = backgroundImage;
        icon = iconImage;
        label = labelText;
        Apply();
    }

    private void OnDisable()
    {
        hovered = false;
        pressed = false;
    }

    private void LateUpdate()
    {
        Apply();
    }

    public void OnPointerEnter(PointerEventData eventData) => hovered = true;
    public void OnPointerExit(PointerEventData eventData) { hovered = false; pressed = false; }
    public void OnPointerDown(PointerEventData eventData) => pressed = true;
    public void OnPointerUp(PointerEventData eventData) => pressed = false;

    private void Apply()
    {
        Transform selected = ModelMovement.Instance != null ? ModelMovement.Instance.SelectedModel : null;
        bool active = IsActive();
        bool lockedLook = tool == Tool.Lock && ModelLock.IsLocked(selected);

        Color bg = UIPalette.Clear;
        Color fg = UIPalette.Neutral700;

        if (lockedLook)
        {
            bg = UIPalette.Amber100;
            fg = UIPalette.Amber700;
        }
        else if (active)
        {
            bg = UIPalette.BrandGreenLight;
            fg = UIPalette.BrandGreen;
        }
        else if (hovered && destructive)
        {
            bg = pressed ? UIPalette.WithAlpha(UIPalette.Red600, 0.16f) : UIPalette.Red50;
            fg = UIPalette.Red600;
        }
        else if (pressed)
        {
            bg = UIPalette.Neutral150;
            fg = UIPalette.BrandBlack;
        }
        else if (hovered)
        {
            bg = UIPalette.Neutral100;
            fg = UIPalette.BrandBlack;
        }

        if (needsSelection && selected == null && !active)
        {
            fg = UIPalette.WithAlpha(fg, dimmedAlpha);
        }

        if (background != null && background.color != bg) background.color = bg;
        if (icon != null && icon.color != fg) icon.color = fg;
        if (label != null && label.color != fg) label.color = fg;
    }

    private bool IsActive()
    {
        switch (tool)
        {
            case Tool.Resize: return ModelResize.Instance != null && ModelResize.Instance.IsResizeModeActive;
            case Tool.Rotate: return ModelRotate.Instance != null && ModelRotate.Instance.IsRotateModeActive;
            case Tool.Measure: return ModelMeasureDistance.Instance != null && ModelMeasureDistance.Instance.IsMeasureModeActive;
            default: return false;
        }
    }
}
