using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Turns a text field's outline green while the user is typing in it (UIBuild.NumberField adds it).
/// </summary>
[AddComponentMenu("UI/UI Focus Outline")]
public class UIFocusOutline : MonoBehaviour
{
    [SerializeField] private TMP_InputField input;
    [SerializeField] private Image outline;
    [SerializeField] private Color normalColor = new Color(0.831f, 0.831f, 0.847f, 1f); // neutral-300
    [SerializeField] private Color focusedColor = new Color(0.106f, 0.388f, 0.243f, 1f); // brand green

    public void Setup(TMP_InputField field, Image outlineImage)
    {
        input = field;
        outline = outlineImage;
        normalColor = UIPalette.Neutral300;
        focusedColor = UIPalette.BrandGreen;
    }

    private void LateUpdate()
    {
        if (input == null || outline == null) return;
        Color color = input.isFocused ? focusedColor : normalColor;
        if (outline.color != color) outline.color = color;
    }
}
