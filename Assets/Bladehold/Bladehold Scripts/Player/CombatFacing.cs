using Synty.AnimationBaseLocomotion.Samples.InputSystem;
using UnityEngine;

/// <summary>
///     Turns a stationary player to face the camera heading while the attack button is held (sword
///     charge/swing) or the bow is aiming. The vendored Synty controller already rotates the whole
///     character to the camera while strafing <b>and moving</b>, but its stationary branch only feeds
///     the turn-in-place animator offset and never rotates the transform — so a standing player can
///     swing or draw at a target the camera looks at while the body still faces elsewhere. Rather
///     than editing Synty source, this yaw-slerps the player root toward the camera's flattened
///     forward (matching the controller's own slerp style and default smoothing) whenever the hold
///     is active and the <see cref="CharacterController" /> reports no horizontal movement — the
///     moment movement input resumes, the controller's own strafe rotation takes over seamlessly.
///     Attack hold is tracked straight from <see cref="InputReader" /> press/release events (the
///     <see cref="AttackCancelsSprint" /> precedent); bow aim reads <see cref="PlayerBow.IsAiming" />.
/// </summary>
public class CombatFacing : MonoBehaviour
{
    [Tooltip("Synty InputReader that raises the attack press/release events. Usually on the player root.")]
    [SerializeField] private InputReader inputReader;
    [Tooltip("Optional: the player's bow. While it is aiming, the player also faces the camera.")]
    [SerializeField] private PlayerBow bow;
    [Tooltip("The controller's CharacterController, used to detect that the player is stationary.")]
    [SerializeField] private CharacterController characterController;
    [Tooltip("Camera whose heading the player turns to. Defaults to Camera.main.")]
    [SerializeField] private Camera facingCamera;
    [Tooltip("Rotation smoothing factor — matches the Synty controller's own rotation smoothing.")]
    [SerializeField] private float rotationSmoothing = 10f;
    [Tooltip("Horizontal speed below which the player counts as stationary (moving is the controller's job).")]
    [SerializeField] private float stationarySpeedThreshold = 0.1f;

    private bool attackHeld;
    private bool subscribed;
    private bool anyError = false;

    private void OnValidate()
    {
        if (inputReader == null)
        {
            inputReader = GetComponentInChildren<InputReader>();
        }
        if (bow == null)
        {
            bow = GetComponentInChildren<PlayerBow>();
        }
        if (characterController == null)
        {
            characterController = GetComponent<CharacterController>();
        }
    }

    private void Start()
    {
        if (inputReader == null)
        {
            Debug.LogError("InputReader is not assigned or found; combat facing can't track the attack hold.");
            anyError = true;
        }
        if (characterController == null)
        {
            Debug.LogError("CharacterController is not assigned or found; combat facing can't tell when the player is stationary.");
            anyError = true;
        }
        if (facingCamera == null)
        {
            facingCamera = Camera.main;
        }
        if (facingCamera == null)
        {
            Debug.LogError("No facing Camera assigned and no Camera.main found; combat facing has no heading to turn to.");
            anyError = true;
        }

        if (anyError)
        {
            return;
        }

        Subscribe();
    }

    private void OnEnable()
    {
        // Re-subscribe if this component is toggled (e.g. re-enabled after a non-death disable).
        if (!anyError && inputReader != null)
        {
            Subscribe();
        }
    }

    private void OnDisable()
    {
        Unsubscribe();
        attackHeld = false;
    }

    private void OnDestroy()
    {
        Unsubscribe();
    }

    private void Subscribe()
    {
        if (subscribed || inputReader == null)
        {
            return;
        }
        inputReader.onAttackActivated += HandleAttackPressed;
        inputReader.onAttackDeactivated += HandleAttackReleased;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed || inputReader == null)
        {
            return;
        }
        inputReader.onAttackActivated -= HandleAttackPressed;
        inputReader.onAttackDeactivated -= HandleAttackReleased;
        subscribed = false;
    }

    private void HandleAttackPressed()
    {
        attackHeld = true;
    }

    private void HandleAttackReleased()
    {
        attackHeld = false;
    }

    private void Update()
    {
        if (anyError)
        {
            return;
        }

        bool engaged = attackHeld || (bow != null && bow.IsAiming);
        if (!engaged)
        {
            return;
        }

        // While moving, the controller's strafe rotation already faces the camera — only cover the
        // stationary gap so the two never fight over the transform.
        Vector3 velocity = characterController.velocity;
        velocity.y = 0f;
        if (velocity.sqrMagnitude > stationarySpeedThreshold * stationarySpeedThreshold)
        {
            return;
        }

        Vector3 forward = facingCamera.transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
        {
            return;
        }

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            Quaternion.LookRotation(forward.normalized),
            rotationSmoothing * Time.deltaTime
        );
    }
}
