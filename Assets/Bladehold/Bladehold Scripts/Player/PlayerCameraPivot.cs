using Synty.AnimationBaseLocomotion.Samples.InputSystem;
using UnityEngine;

/// <summary>
///     The look-rotation half of the camera rig, replacing the vendored
///     <see cref="Synty.AnimationBaseLocomotion.Samples.SampleCameraController" /> as the thing the
///     mouse steers: accumulates yaw/pitch from the Synty <see cref="InputReader" />'s mouse delta
///     (same raw-delta-times-sensitivity math as the vendored controller, so saved sensitivity
///     values keep meaning the same thing) and applies them to this transform every
///     <c>LateUpdate</c>, after the character has moved. The gameplay <c>CinemachineCamera</c>
///     tracking this pivot (Third Person Follow) owns everything positional — boom distance,
///     shoulder framing, damping, collision — and the <c>CinemachineBrain</c> moves the real camera,
///     so this component never touches the camera itself. Also owns the gameplay cursor lock the
///     vendored controller's <c>Start</c> used to do.
///
///     Sensitivity/invert are plain properties written by <see cref="InputSettingsBinder" /> — no
///     reflection needed anymore, and invert-X no longer has to flip the shared
///     <c>InputReader._mouseDelta</c> that other systems read. The vendored
///     <c>SampleCameraController</c> stays on the old rig <b>disabled</b>:
///     <see cref="Synty.AnimationBaseLocomotion.Samples.SamplePlayerAnimationController" /> still
///     calls its <c>GetCamera*()</c> getters for camera-relative movement, and those only read the
///     serialized main camera's transform — correct regardless of what drives the camera.
///
///     Like the vendored controller, look accumulation is per-frame mouse delta, not scaled by
///     <see cref="Time.deltaTime" /> — so <see cref="PauseMenuController" /> must keep this
///     component in its disable list to stop the angles drifting while time is frozen.
/// </summary>
public class PlayerCameraPivot : MonoBehaviour
{
    [Tooltip("The Synty input reader on the character root, read for the per-frame mouse delta.")]
    [SerializeField] private InputReader inputReader;
    [Tooltip("Transform the pivot sticks to, normally the character's SyntyPlayer_LookAt child.")]
    [SerializeField] private Transform followTarget;
    [Tooltip("Look sensitivity multiplying the raw mouse delta (the vendored _mouseSensitivity scale). Overwritten on Start by the saved setting via InputSettingsBinder.")]
    [SerializeField] private float sensitivity = 0.5f;
    [Tooltip("Gamepad look speed in degrees per second at full stick deflection. Overwritten on Start by the saved setting via InputSettingsBinder.")]
    [SerializeField] private float gamepadSensitivity = 180f;
    [Tooltip("Exponent shaping stick response: 1 = linear, 2 = quadratic (finer aim near center, same max speed).")]
    [SerializeField] private float gamepadResponseExponent = 2f;
    [Tooltip("Pitch clamp in degrees: x = furthest up (negative), y = furthest down.")]
    [SerializeField] private Vector2 tiltBounds = new Vector2(-70f, 70f);
    [Tooltip("Lock and hide the cursor on Start, like the vendored controller did.")]
    [SerializeField] private bool hideCursor = true;

    [Header("Position smoothing")]
    [Tooltip("Optional: the player's mount, so mounted smoothing can kick in. Auto-wired from parents, falling back to Player.Instance.")]
    [SerializeField] private PlayerMount mount;
    [Tooltip("SmoothDamp time on the pivot position on foot. 0 = snap to the follow target (the original behaviour).")]
    [SerializeField] private float positionSmoothTime = 0f;
    [Tooltip("SmoothDamp time on the pivot position while mounted — absorbs the riding animation's saddle bob so the camera tracks the horse's actual motion instead of jerking with the rider.")]
    [SerializeField] private float mountedPositionSmoothTime = 0.2f;

    private float yaw;
    private float pitch;
    private Vector3 smoothVelocity;
    private bool anyError = false;

    /// <summary>Look sensitivity; <see cref="InputSettingsBinder" /> is the intended writer.</summary>
    public float Sensitivity
    {
        get => sensitivity;
        set => sensitivity = value;
    }

    /// <summary>Gamepad look speed in degrees per second at full deflection; <see cref="InputSettingsBinder" /> is the intended writer.</summary>
    public float GamepadSensitivity
    {
        get => gamepadSensitivity;
        set => gamepadSensitivity = value;
    }

    /// <summary>Inverts horizontal look (no vendored equivalent existed).</summary>
    public bool InvertX { get; set; }

    /// <summary>Inverts vertical look (the vendored <c>_invertCamera</c>).</summary>
    public bool InvertY { get; set; }

    private void OnValidate()
    {
        if (inputReader == null)
        {
            inputReader = GetComponentInParent<InputReader>();
        }
        if (followTarget == null && inputReader != null)
        {
            followTarget = inputReader.transform.Find("SyntyPlayer_LookAt");
        }
        if (mount == null)
        {
            mount = GetComponentInParent<PlayerMount>();
        }
    }

    private void Start()
    {
        if (inputReader == null)
        {
            Debug.LogError("InputReader is not assigned or found in parents; the camera pivot has no look input.");
            anyError = true;
        }
        if (followTarget == null)
        {
            Debug.LogError("Follow target (SyntyPlayer_LookAt) is not assigned or found; the camera pivot has nothing to stick to.");
            anyError = true;
        }

        if (anyError)
        {
            return;
        }

        // Mounted smoothing is optional flavor — no mount component just means on-foot smoothing always.
        if (mount == null && Player.Instance != null)
        {
            mount = Player.Instance.GetComponent<PlayerMount>();
        }

        if (hideCursor)
        {
            CursorLockManager.ApplyState();
        }

        yaw = followTarget.eulerAngles.y;
        pitch = 0f;
        transform.SetPositionAndRotation(followTarget.position, Quaternion.Euler(pitch, yaw, 0f));
    }

    // LateUpdate: after the character controller has moved the follow target this frame, but before
    // the CinemachineBrain (which runs after ordinary LateUpdates) reads the pivot.
    private void LateUpdate()
    {
        if (anyError || CursorLockManager.IsCursorUnlocked)
        {
            return;
        }

        // The Look action feeds _mouseDelta from both devices, but they mean different things: a
        // mouse reports a per-frame pixel delta (already framerate-shaped — apply raw, the vendored
        // convention), while a stick reports a held ±1 deflection that must be scaled by deltaTime
        // and its own deg/sec sensitivity or turn speed varies with framerate.
        Vector2 delta = inputReader._mouseDelta;
        if (InputDeviceWatcher.GamepadActive)
        {
            float magnitude = Mathf.Clamp01(delta.magnitude);
            float shaped = Mathf.Pow(magnitude, Mathf.Max(1f, gamepadResponseExponent));
            delta = (magnitude > 0f ? delta / magnitude : Vector2.zero) * (shaped * gamepadSensitivity * Time.deltaTime);
        }
        else
        {
            delta *= sensitivity;
        }
        yaw += delta.x * (InvertX ? -1f : 1f);
        // Vendored sign convention: not inverted means mouse-up looks up (pitch toward negative).
        pitch += delta.y * (InvertY ? 1f : -1f);
        pitch = Mathf.Clamp(pitch, tiltBounds.x, tiltBounds.y);

        // While mounted the follow target bobs with the riding animation; SmoothDamp filters that
        // high-frequency motion out while still tracking the horse's real movement.
        float smoothTime = mount != null && mount.IsMounted ? mountedPositionSmoothTime : positionSmoothTime;
        Vector3 position;
        if (smoothTime > 0f)
        {
            position = Vector3.SmoothDamp(transform.position, followTarget.position, ref smoothVelocity, smoothTime);
        }
        else
        {
            position = followTarget.position;
            smoothVelocity = Vector3.zero;
        }

        transform.SetPositionAndRotation(position, Quaternion.Euler(pitch, yaw, 0f));
    }
}
