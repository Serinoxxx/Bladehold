using System.Collections;
using MoreMountains.Feedbacks;
using UnityEngine;

/// <summary>
///     Lets an enemy (the Pig Butcher's <see cref="HookProjectile" />) drag the player toward it.
///     <see cref="Pull" /> disables an inspector-assigned list of control components (the
///     <see cref="PlayerMount" />/<see cref="PlayerDeath" /> disable-list idiom) but keeps the
///     <see cref="CharacterController" /> <b>enabled</b> and <c>Move()</c>s the player toward the
///     puller each frame — so walls interrupt the drag for free (a side collision ends it early).
///
///     No-op while mounted (<see cref="PlayerMount" /> owns the controller) or dead; and if the
///     player dies mid-drag, controls are deliberately NOT restored — <see cref="PlayerDeath" />'s
///     disable list owns them from that moment (the PlayerMount dead-player guard).
/// </summary>
public class PlayerPullReceiver : MonoBehaviour
{
    [SerializeField] private Health health;
    [SerializeField] private CharacterController characterController;
    [Tooltip("Optional; pulls are refused while mounted (PlayerMount owns the controller).")]
    [SerializeField] private PlayerMount mount;
    [Tooltip("Control components disabled for the drag (the Synty controller, CombatFacing, ... — the PlayerMount list idiom). Do NOT list InputReader or this component.")]
    [SerializeField] private MonoBehaviour[] componentsToDisableWhilePulled;
    [Tooltip("Optional feedback when a drag starts (a yank + grunt).")]
    [SerializeField] private MMF_Player pulledFeedback;

    private Coroutine activePull;
    private bool controlsDisabled;
    private bool anyError = false;

    /// <summary>True while a drag is in progress — for anything that wants to read the state (camera, telemetry).</summary>
    public bool IsBeingPulled => activePull != null;

    private void OnValidate()
    {
        if (health == null)
        {
            health = GetComponent<Health>();
        }
        if (characterController == null)
        {
            characterController = GetComponent<CharacterController>();
        }
        if (mount == null)
        {
            mount = GetComponent<PlayerMount>();
        }
    }

    private void Start()
    {
        if (health == null)
        {
            Debug.LogError("Health component is not assigned or found on the GameObject.");
            anyError = true;
        }
        if (characterController == null)
        {
            Debug.LogError("CharacterController component is not assigned or found on the GameObject.");
            anyError = true;
        }
        if (componentsToDisableWhilePulled == null || componentsToDisableWhilePulled.Length == 0)
        {
            // Degrades rather than errors: the drag still moves the player, the disabled-controls
            // window just isn't enforced (the controller will fight the pull).
            Debug.LogWarning("PlayerPullReceiver has no control components assigned to disable — the drag will fight the movement controller.");
        }
    }

    /// <summary>
    ///     Drags the player toward <paramref name="target" /> for <paramref name="seconds" />,
    ///     stopping early within <paramref name="stopDistance" /> of it, on a wall hit, or on death.
    ///     A second hook mid-drag restarts the timer toward the new puller.
    /// </summary>
    public void Pull(Transform target, float seconds, float stopDistance)
    {
        if (anyError || target == null || health.IsDead)
        {
            return;
        }
        if (mount != null && mount.IsMounted)
        {
            // PlayerMount owns the controller in the saddle; a hook can't unseat anyone.
            return;
        }

        if (activePull != null)
        {
            // Replace the drag but keep the disabled-controls state — RunPull restores exactly once.
            StopCoroutine(activePull);
        }

        if (pulledFeedback != null)
        {
            pulledFeedback.PlayFeedbacks();
        }

        activePull = StartCoroutine(RunPull(target, seconds, stopDistance));
    }

    private IEnumerator RunPull(Transform target, float seconds, float stopDistance)
    {
        DisableControls();

        // Constant speed sized to close the full gap in the allotted time.
        Vector3 initialGap = target.position - transform.position;
        initialGap.y = 0f;
        float pullSpeed = initialGap.magnitude / Mathf.Max(0.05f, seconds);

        float elapsed = 0f;
        while (elapsed < seconds)
        {
            // Death mid-drag: stop moving and leave the controls to PlayerDeath's disable list.
            if (health.IsDead)
            {
                activePull = null;
                yield break;
            }
            if (target == null)
            {
                break;
            }

            Vector3 toTarget = target.position - transform.position;
            toTarget.y = 0f;
            if (toTarget.magnitude <= stopDistance)
            {
                break;
            }

            // Keep a gravity term so the drag hugs ramps instead of ending airborne.
            Vector3 motion = toTarget.normalized * pullSpeed * Time.deltaTime + Vector3.down * 2f * Time.deltaTime;
            CollisionFlags flags = characterController.Move(motion);
            if ((flags & CollisionFlags.Sides) != 0)
            {
                // A wall took the hit for us.
                break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        RestoreControls();
        activePull = null;
    }

    private void DisableControls()
    {
        if (controlsDisabled || componentsToDisableWhilePulled == null)
        {
            controlsDisabled = true;
            return;
        }
        controlsDisabled = true;

        foreach (MonoBehaviour component in componentsToDisableWhilePulled)
        {
            if (component != null)
            {
                component.enabled = false;
            }
        }
    }

    private void RestoreControls()
    {
        if (!controlsDisabled)
        {
            return;
        }
        controlsDisabled = false;

        // Never re-enable over a corpse — PlayerDeath's disable list owns a dead player's controls
        // (the PlayerMount dead-player guard).
        if (health.IsDead || componentsToDisableWhilePulled == null)
        {
            return;
        }

        foreach (MonoBehaviour component in componentsToDisableWhilePulled)
        {
            if (component != null)
            {
                component.enabled = true;
            }
        }
    }
}
