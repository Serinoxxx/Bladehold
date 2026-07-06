using System.Reflection;
using Synty.AnimationBaseLocomotion.Samples;
using Synty.AnimationBaseLocomotion.Samples.InputSystem;
using UnityEngine;

/// <summary>
///     Starting an attack always cancels sprinting (regardless of any skill-tree state), so a swing is
///     never thrown mid-sprint. The vendored controller keeps its sprint state private and drives it from
///     <c>InputReader.onSprintActivated/Deactivated</c>, so — per the <see cref="PlayerMoveSpeedBinder" />
///     precedent — this invokes the controller's private <c>DeactivateSprint()</c> by cached reflection on
///     every attack press rather than editing Synty source (the method also restores the strafe state,
///     which writing the <c>_isSprinting</c> field directly would skip). Sprinting stays off until the
///     player presses sprint again. Degrades gracefully (logs and disables itself) if Synty ever renames
///     the method.
/// </summary>
public class AttackCancelsSprint : MonoBehaviour
{
    [Tooltip("Synty InputReader that raises the attack press event. Usually on the player root.")]
    [SerializeField] private InputReader inputReader;
    [SerializeField] private SamplePlayerAnimationController controller;

    private MethodInfo deactivateSprintMethod;
    private bool subscribed;
    private bool anyError = false;

    private void OnValidate()
    {
        if (inputReader == null)
        {
            inputReader = GetComponentInChildren<InputReader>();
        }
        if (controller == null)
        {
            controller = GetComponent<SamplePlayerAnimationController>();
            if (controller == null)
            {
                controller = GetComponentInChildren<SamplePlayerAnimationController>();
            }
        }
    }

    private void Start()
    {
        if (inputReader == null)
        {
            Debug.LogError("InputReader is not assigned or found; attack can't cancel sprinting.");
            anyError = true;
        }
        if (controller == null)
        {
            Debug.LogError("SamplePlayerAnimationController is not assigned or found on the GameObject.");
            anyError = true;
        }

        if (anyError)
        {
            return;
        }

        deactivateSprintMethod = typeof(SamplePlayerAnimationController).GetMethod("DeactivateSprint", BindingFlags.Instance | BindingFlags.NonPublic);
        if (deactivateSprintMethod == null)
        {
            Debug.LogError("AttackCancelsSprint could not find the controller's 'DeactivateSprint' method. Attack no longer cancels sprinting.");
            anyError = true;
            return;
        }

        Subscribe();
    }

    private void OnEnable()
    {
        if (!anyError && deactivateSprintMethod != null)
        {
            Subscribe();
        }
    }

    private void OnDisable()
    {
        Unsubscribe();
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
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed || inputReader == null)
        {
            return;
        }
        inputReader.onAttackActivated -= HandleAttackPressed;
        subscribed = false;
    }

    private void HandleAttackPressed()
    {
        if (anyError)
        {
            return;
        }

        deactivateSprintMethod.Invoke(controller, null);
    }
}
