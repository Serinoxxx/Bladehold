using UnityEngine;

/// <summary>
///     Free-fly movement for Photo Mode's detached camera. Enabled/disabled and bound to the shared
///     <see cref="MenuInputActions" /> by <see cref="ScreenshotModeController" /> on
///     enter/exit — it never reads gameplay input, only the code-built <c>ScreenshotFly</c> action map.
///     Moves on unscaled time since <see cref="Time.timeScale" /> is 0 for the whole time Photo Mode
///     can be active (it's only reachable from the pause menu).
/// </summary>
public class ScreenshotFlyCamera : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float boostMultiplier = 3f;
    [SerializeField] private float lookSensitivity = 0.1f;

    private MenuInputActions actions;
    private float pitch;
    private float yaw;

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
    }

    private void Update()
    {
        if (actions == null)
        {
            return;
        }

        Vector2 look = actions.Look.ReadValue<Vector2>();
        yaw += look.x * lookSensitivity;
        pitch -= look.y * lookSensitivity;
        pitch = Mathf.Clamp(pitch, -89f, 89f);
        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);

        Vector2 move = actions.Move.ReadValue<Vector2>();
        float vertical = actions.UpDown.ReadValue<float>();
        bool boosting = actions.Boost.IsPressed();

        Vector3 direction = transform.right * move.x + transform.forward * move.y + Vector3.up * vertical;
        float speed = moveSpeed * (boosting ? boostMultiplier : 1f);

        transform.position += direction.normalized * speed * Time.unscaledDeltaTime;
    }
}
