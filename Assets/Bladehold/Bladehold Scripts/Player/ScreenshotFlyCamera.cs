using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
///     Free-fly movement for Photo Mode's detached camera. Enabled/disabled and bound to the shared
///     <see cref="MenuInputActions" /> by <see cref="ScreenshotModeController" /> on
///     enter/exit — it never reads gameplay input, only the code-built <c>ScreenshotFly</c> action map.
///     Looking is click-and-drag (<see cref="MenuInputActions.Drag" />) so the free cursor can still
///     work the Photo Mode sliders; a press that starts over UI never becomes a camera drag. Moves on
///     unscaled time since <see cref="Time.timeScale" /> is 0 for the whole time Photo Mode can be
///     active (it's only reachable from the pause menu).
/// </summary>
public class ScreenshotFlyCamera : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float boostMultiplier = 3f;
    [SerializeField] private float lookSensitivity = 0.1f;

    private MenuInputActions actions;
    private float pitch;
    private float yaw;
    private bool dragging;

    /// <summary>Called by <see cref="ScreenshotModeController" /> before enabling this component.</summary>
    public void Bind(MenuInputActions menuActions)
    {
        actions = menuActions;
    }

    private void OnEnable()
    {
        Vector3 euler = transform.eulerAngles;
        yaw = euler.y;
        pitch = euler.x;
        dragging = false;
    }

    private void Update()
    {
        if (actions == null)
        {
            return;
        }

        if (!actions.Drag.IsPressed())
        {
            dragging = false;
        }
        else if (!dragging && actions.Drag.WasPressedThisFrame())
        {
            // A press that starts over UI (sliders, buttons) stays a UI interaction for its whole
            // hold — dragging off the control mid-hold must not start spinning the camera.
            dragging = EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject();
        }

        if (dragging)
        {
            Vector2 look = actions.Look.ReadValue<Vector2>();
            yaw += look.x * lookSensitivity;
            pitch -= look.y * lookSensitivity;
            pitch = Mathf.Clamp(pitch, -89f, 89f);
            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }

        Vector2 move = actions.Move.ReadValue<Vector2>();
        float vertical = actions.UpDown.ReadValue<float>();
        bool boosting = actions.Boost.IsPressed();

        Vector3 direction = transform.right * move.x + transform.forward * move.y + Vector3.up * vertical;
        float speed = moveSpeed * (boosting ? boostMultiplier : 1f);

        transform.position += direction.normalized * speed * Time.unscaledDeltaTime;
    }
}
