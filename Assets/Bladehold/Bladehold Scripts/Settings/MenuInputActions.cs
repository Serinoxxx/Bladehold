using UnityEngine.InputSystem;

/// <summary>
///     First-party Input System actions for the pause menu and Photo Mode's fly camera, built directly
///     through the InputAction/InputActionMap API rather than a hand-authored <c>.inputactions</c>
///     asset — hand-editing that serialized format is fragile, and either a new asset or the project's
///     existing one would still need Unity Editor wiring to reach. This needs none, and never touches
///     the vendored Synty <c>Controls</c> asset that drives gameplay (see <see cref="InputSettingsBinder" />
///     for that one). A plain reusable class, not a <c>MonoBehaviour</c> — the same style as
///     <c>LocomotionAnimator</c>.
/// </summary>
public class MenuInputActions
{
    /// <summary>Always enabled during gameplay so Esc opens the pause menu from anywhere.</summary>
    public InputActionMap Menu { get; }
    public InputAction TogglePause { get; }

    /// <summary>
    ///     Enabled only while a skill-tree screen is open (see <see cref="SkillTreePadController" />):
    ///     gamepad pan/zoom/selection over the tree plus tab switching. Gameplay is dead or frozen
    ///     whenever these screens show, so reusing gameplay's trigger/button controls can't conflict.
    /// </summary>
    public InputActionMap UiNav { get; }
    public InputAction TreePan { get; }
    public InputAction TreeZoom { get; }
    public InputAction NodeNav { get; }
    public InputAction TabPrev { get; }
    public InputAction TabNext { get; }
    public InputAction Buy { get; }

    /// <summary>Enabled only while <see cref="ScreenshotModeController" /> is active.</summary>
    public InputActionMap ScreenshotFly { get; }
    public InputAction Move { get; }
    public InputAction UpDown { get; }
    public InputAction Look { get; }
    /// <summary>Held to rotate the fly camera with <see cref="Look" /> — click-and-drag, so the cursor stays usable on the Photo Mode sliders.</summary>
    public InputAction Drag { get; }
    public InputAction Boost { get; }
    public InputAction Capture { get; }

    public MenuInputActions()
    {
        Menu = new InputActionMap("Menu");
        TogglePause = Menu.AddAction("TogglePause", InputActionType.Button);
        TogglePause.AddBinding("<Keyboard>/escape");
        TogglePause.AddBinding("<Gamepad>/start");

        UiNav = new InputActionMap("UiNav");

        TreePan = UiNav.AddAction("TreePan", InputActionType.Value);
        TreePan.AddBinding("<Gamepad>/rightStick");

        TreeZoom = UiNav.AddAction("TreeZoom", InputActionType.Value);
        TreeZoom.AddCompositeBinding("1DAxis")
            .With("Positive", "<Gamepad>/rightTrigger")
            .With("Negative", "<Gamepad>/leftTrigger");

        NodeNav = UiNav.AddAction("NodeNav", InputActionType.Value);
        NodeNav.AddBinding("<Gamepad>/leftStick");
        NodeNav.AddBinding("<Gamepad>/dpad");

        TabPrev = UiNav.AddAction("TabPrev", InputActionType.Button);
        TabPrev.AddBinding("<Gamepad>/leftShoulder");

        TabNext = UiNav.AddAction("TabNext", InputActionType.Button);
        TabNext.AddBinding("<Gamepad>/rightShoulder");

        Buy = UiNav.AddAction("Buy", InputActionType.Button);
        Buy.AddBinding("<Gamepad>/buttonSouth");

        ScreenshotFly = new InputActionMap("ScreenshotFly");

        Move = ScreenshotFly.AddAction("Move", InputActionType.Value);
        Move.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w")
            .With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a")
            .With("Right", "<Keyboard>/d");

        UpDown = ScreenshotFly.AddAction("UpDown", InputActionType.Value);
        UpDown.AddCompositeBinding("1DAxis")
            .With("Positive", "<Keyboard>/e")
            .With("Negative", "<Keyboard>/q");

        Look = ScreenshotFly.AddAction("Look", InputActionType.Value);
        Look.AddBinding("<Mouse>/delta");

        Drag = ScreenshotFly.AddAction("Drag", InputActionType.Button);
        Drag.AddBinding("<Mouse>/leftButton");

        Boost = ScreenshotFly.AddAction("Boost", InputActionType.Button);
        Boost.AddBinding("<Keyboard>/leftShift");

        Capture = ScreenshotFly.AddAction("Capture", InputActionType.Button);
        Capture.AddBinding("<Keyboard>/f12");
    }

    public void EnableMenu() => Menu.Enable();
    public void DisableMenu() => Menu.Disable();
    public void EnableUiNav() => UiNav.Enable();
    public void DisableUiNav() => UiNav.Disable();
    public void EnableScreenshotFly() => ScreenshotFly.Enable();
    public void DisableScreenshotFly() => ScreenshotFly.Disable();

    public void Dispose()
    {
        Menu.Disable();
        UiNav.Disable();
        ScreenshotFly.Disable();
        Menu.Dispose();
        UiNav.Dispose();
        ScreenshotFly.Dispose();
    }
}
