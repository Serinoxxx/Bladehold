using System.Reflection;
using Synty.AnimationBaseLocomotion.Samples.InputSystem;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
///     Applies saved look/input settings for <see cref="GameSettingsService" /> (the only intended
///     caller). Sensitivity and invert are plain property writes on the first-party
///     <see cref="PlayerCameraPivot" /> now that the camera is Cinemachine-driven; the only
///     reflection left is into the vendored <see cref="InputReader" />'s private <c>_controls</c>
///     (the <see cref="PlayerMoveSpeedBinder" /> precedent — no vendored source is edited), to
///     expose the vendored Controls action map for button remapping.
/// </summary>
public class InputSettingsBinder : MonoBehaviour
{
    private const BindingFlags FieldFlags = BindingFlags.Instance | BindingFlags.NonPublic;

    [SerializeField] private PlayerCameraPivot cameraPivot;
    [SerializeField] private InputReader inputReader;

    private FieldInfo controlsField;

    private bool anyError = false;

    private void OnValidate()
    {
        if (cameraPivot == null)
        {
            cameraPivot = GetComponentInChildren<PlayerCameraPivot>();
            if (cameraPivot == null)
            {
                cameraPivot = FindFirstObjectByType<PlayerCameraPivot>();
            }
        }
        if (inputReader == null)
        {
            inputReader = GetComponent<InputReader>();
            if (inputReader == null)
            {
                inputReader = GetComponentInChildren<InputReader>();
            }
        }
    }

    private void Awake()
    {
        if (cameraPivot == null)
        {
            Debug.LogError("PlayerCameraPivot is not assigned or found in children.");
            anyError = true;
        }
        if (inputReader == null)
        {
            Debug.LogError("InputReader is not assigned or found on the GameObject.");
            anyError = true;
        }

        if (anyError)
        {
            return;
        }

        controlsField = typeof(InputReader).GetField("_controls", FieldFlags);
        if (controlsField == null)
        {
            Debug.LogError("InputSettingsBinder could not find the vendored InputReader's _controls field. Button remapping won't apply.");
            anyError = true;
        }
    }

    public void ApplySensitivity(float sensitivity)
    {
        if (anyError) return;
        cameraPivot.Sensitivity = sensitivity;
    }

    /// <summary>Gamepad right-stick look speed in degrees per second at full deflection.</summary>
    public void ApplyGamepadSensitivity(float degreesPerSecond)
    {
        if (anyError) return;
        cameraPivot.GamepadSensitivity = degreesPerSecond;
    }

    public void ApplyInvertY(bool invert)
    {
        if (anyError) return;
        cameraPivot.InvertY = invert;
    }

    public void ApplyInvertX(bool invert)
    {
        if (anyError) return;
        cameraPivot.InvertX = invert;
    }

    /// <summary>The vendored Controls asset's Player action map, for enumerating/rebinding bindings.</summary>
    public InputActionMap GetRebindableActionMap()
    {
        if (anyError) return null;
        Controls controls = (Controls)controlsField.GetValue(inputReader);
        return controls?.Player.Get();
    }

    public string SaveBindingOverridesToJson()
    {
        if (anyError) return "";
        Controls controls = (Controls)controlsField.GetValue(inputReader);
        return controls != null ? controls.SaveBindingOverridesAsJson() : "";
    }

    public void LoadBindingOverridesFromJson(string json)
    {
        if (anyError || string.IsNullOrEmpty(json)) return;
        Controls controls = (Controls)controlsField.GetValue(inputReader);
        controls?.LoadBindingOverridesFromJson(json);
    }

    public void ClearBindingOverrides()
    {
        if (anyError) return;
        Controls controls = (Controls)controlsField.GetValue(inputReader);
        controls?.RemoveAllBindingOverrides();
    }
}
