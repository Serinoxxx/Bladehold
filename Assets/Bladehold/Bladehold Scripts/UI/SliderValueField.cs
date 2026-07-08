using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
///     Keeps a <see cref="TMP_InputField" /> in sync with a <see cref="Slider" /> so the settings menu's
///     sliders can also be set by typing an exact number. The slider stays the source of truth: dragging
///     it updates the field's text, and submitting text (Enter or losing focus) clamps the parsed value
///     to the slider's range and writes it back to <see cref="Slider.value" /> — which fires the
///     slider's own <c>onValueChanged</c>, so <see cref="SettingsPanelView" />'s existing listeners apply
///     it exactly as if it had been dragged. Generated per slider row by
///     <see cref="SettingsMenuGenerator" />; not itself settings-aware.
/// </summary>
public class SliderValueField : MonoBehaviour
{
    [SerializeField] private Slider slider;
    [SerializeField] private TMP_InputField inputField;
    [Tooltip("Decimal places shown/accepted. Ignored (treated as 0) when the slider's Whole Numbers is on.")]
    [SerializeField] private int decimalPlaces = 1;

    private bool anyError = false;

    private void OnValidate()
    {
        if (slider == null)
        {
            slider = GetComponentInChildren<Slider>();
        }
        if (inputField == null)
        {
            inputField = GetComponentInChildren<TMP_InputField>();
        }
    }

    private void Start()
    {
        if (slider == null || inputField == null)
        {
            Debug.LogError("SliderValueField requires both a Slider and a TMP_InputField assigned.");
            anyError = true;
            return;
        }

        inputField.contentType = slider.wholeNumbers ? TMP_InputField.ContentType.IntegerNumber : TMP_InputField.ContentType.DecimalNumber;

        RefreshText(slider.value);
        slider.onValueChanged.AddListener(RefreshText);
        inputField.onEndEdit.AddListener(HandleTextSubmitted);
    }

    private void OnDestroy()
    {
        if (slider != null) slider.onValueChanged.RemoveListener(RefreshText);
        if (inputField != null) inputField.onEndEdit.RemoveListener(HandleTextSubmitted);
    }

    private void RefreshText(float value)
    {
        int places = slider.wholeNumbers ? 0 : decimalPlaces;
        inputField.SetTextWithoutNotify(value.ToString("F" + places, CultureInfo.InvariantCulture));
    }

    private void HandleTextSubmitted(string text)
    {
        if (anyError)
        {
            return;
        }

        if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
        {
            slider.value = Mathf.Clamp(value, slider.minValue, slider.maxValue);
        }

        // Re-sync either way: reflects clamping on a valid value, or reverts unparseable input.
        RefreshText(slider.value);
    }
}
