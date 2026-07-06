using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
///     Scene singleton owning the pause/freeze/cursor state, entered by pressing Esc via the code-built
///     <see cref="MenuInputActions.TogglePause" /> action (see that class for why this isn't a
///     hand-authored <c>.inputactions</c> asset). Pausing sets <see cref="Time.timeScale" /> to 0,
///     unlocks the cursor, and disables an inspector-assigned list of player control components — the
///     same "disable a list of components" pattern <see cref="PlayerDeath" /> already uses for death.
///
///     Disabling the Synty <c>InputReader</c> specifically is load-bearing, not cosmetic: the vendored
///     camera controller accumulates its look angles every <c>Update</c> uncapped by
///     <see cref="Time.deltaTime" />, and only the Lerp toward that accumulated value is
///     timescale-scaled — so at <c>timeScale = 0</c> the camera looks frozen, but if the reader kept
///     receiving mouse input the accumulator would drift and snap violently on resume. Disabling it
///     stops that drift at the source.
///
///     Photo Mode (<see cref="ScreenshotModeController" />) is reached from inside this pause menu and
///     reuses this paused state rather than a second competing one — pressing Esc while it's active
///     exits Photo Mode instead of resuming play.
/// </summary>
public class PauseMenuController : MonoBehaviour
{
    public static PauseMenuController Instance;

    [Tooltip("Components disabled while paused to freeze the character and camera look (e.g. InputReader, SampleCameraController).")]
    [SerializeField] private MonoBehaviour[] componentsToDisable;
    [SerializeField] private ScreenshotModeController screenshotMode;

    public bool IsPaused { get; private set; }

    /// <summary>The shared code-built action set (see <see cref="MenuInputActions" />) — Photo Mode enables/disables its fly map through this.</summary>
    public MenuInputActions Actions { get; private set; }

    /// <summary>Raised whenever the pause state changes, carrying the new state.</summary>
    public event Action<bool> OnPauseChanged;

    private void OnValidate()
    {
        if (screenshotMode == null)
        {
            screenshotMode = GetComponent<ScreenshotModeController>();
        }
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        Actions = new MenuInputActions();
        Actions.TogglePause.performed += HandleTogglePerformed;
    }

    private void OnEnable()
    {
        Actions?.EnableMenu();
    }

    private void OnDisable()
    {
        Actions?.DisableMenu();
    }

    private void OnDestroy()
    {
        if (Actions != null)
        {
            Actions.TogglePause.performed -= HandleTogglePerformed;
            Actions.Dispose();
        }
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void HandleTogglePerformed(InputAction.CallbackContext context)
    {
        if (screenshotMode != null && screenshotMode.IsActive)
        {
            screenshotMode.Exit();
            return;
        }

        if (!IsPaused && Player.Instance != null && Player.Instance.Health != null && Player.Instance.Health.IsDead)
        {
            return;
        }

        SetPaused(!IsPaused);
    }

    public void SetPaused(bool paused)
    {
        if (IsPaused == paused)
        {
            return;
        }

        if (!paused && screenshotMode != null && screenshotMode.IsActive)
        {
            screenshotMode.Exit();
        }

        IsPaused = paused;
        Time.timeScale = paused ? 0f : 1f;
        Cursor.lockState = paused ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = paused;

        foreach (MonoBehaviour component in componentsToDisable)
        {
            if (component != null)
            {
                component.enabled = !paused;
            }
        }

        OnPauseChanged?.Invoke(paused);
    }

    /// <summary>
    ///     Suspends/resumes the Esc-toggle itself, e.g. while a control is being interactively rebound —
    ///     that operation also cancels through Esc, and without this it would close the whole pause menu
    ///     instead of just cancelling the rebind.
    /// </summary>
    public void SetToggleEnabled(bool enabled)
    {
        if (enabled)
        {
            Actions.TogglePause.Enable();
        }
        else
        {
            Actions.TogglePause.Disable();
        }
    }
}
