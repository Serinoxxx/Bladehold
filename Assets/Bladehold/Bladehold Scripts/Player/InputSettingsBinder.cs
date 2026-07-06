using System.Reflection;
using Synty.AnimationBaseLocomotion.Samples;
using Synty.AnimationBaseLocomotion.Samples.InputSystem;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
///     Reaches into the vendored <see cref="SampleCameraController" />/<see cref="InputReader" /> — the
///     same reflection precedent as <see cref="PlayerMoveSpeedBinder" />/<see cref="AttackCancelsSprint" />
///     — to apply saved sensitivity/invert settings and expose the vendored <c>Controls</c> action map for
///     button remapping. <see cref="GameSettingsService" /> is the only intended caller.
///
///     Sensitivity (<c>_mouseSensitivity</c>) is read fresh every frame by the camera controller, so
///     setting it takes effect immediately. Invert-Y is trickier: the controller only derives its usable
///     <c>_cameraInversion</c> (±1) from <c>_invertCamera</c> once, in its own <c>Start</c> — so both
///     fields are written together here to stay correct regardless of component execution order. Invert-X
///     has no equivalent in the vendored controller at all (it never inverts yaw), so it's implemented by
///     flipping the sign of the public <c>InputReader._mouseDelta.x</c> before the camera controller reads
///     it each frame — the class-level execution order guarantees that happens early enough.
/// </summary>
[DefaultExecutionOrder(-100)]
public class InputSettingsBinder : MonoBehaviour
{
    private const BindingFlags FieldFlags = BindingFlags.Instance | BindingFlags.NonPublic;

    [SerializeField] private SampleCameraController cameraController;
    [SerializeField] private InputReader inputReader;

    private FieldInfo sensitivityField;
    private FieldInfo invertCameraField;
    private FieldInfo cameraInversionField;
    private FieldInfo controlsField;

    private bool invertX;
    private bool anyError = false;

    private void OnValidate()
    {
        if (cameraController == null)
        {
            cameraController = FindFirstObjectByType<SampleCameraController>();
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
        if (cameraController == null)
        {
            Debug.LogError("SampleCameraController is not assigned or found in the scene.");
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

        System.Type cameraType = cameraController.GetType();
        sensitivityField = cameraType.GetField("_mouseSensitivity", FieldFlags);
        invertCameraField = cameraType.GetField("_invertCamera", FieldFlags);
        cameraInversionField = cameraType.GetField("_cameraInversion", FieldFlags);
        controlsField = typeof(InputReader).GetField("_controls", FieldFlags);

        if (sensitivityField == null || invertCameraField == null || cameraInversionField == null || controlsField == null)
        {
            Debug.LogError("InputSettingsBinder could not find the vendored controller's fields. Settings won't apply.");
            anyError = true;
        }
    }

    private void Update()
    {
        if (anyError || !invertX)
        {
            return;
        }

        Vector2 delta = inputReader._mouseDelta;
        delta.x = -delta.x;
        inputReader._mouseDelta = delta;
    }

    public void ApplySensitivity(float sensitivity)
    {
        if (anyError) return;
        sensitivityField.SetValue(cameraController, sensitivity);
    }

    public void ApplyInvertY(bool invert)
    {
        if (anyError) return;
        invertCameraField.SetValue(cameraController, invert);
        cameraInversionField.SetValue(cameraController, invert ? 1f : -1f);
    }

    public void ApplyInvertX(bool invert)
    {
        invertX = invert;
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
