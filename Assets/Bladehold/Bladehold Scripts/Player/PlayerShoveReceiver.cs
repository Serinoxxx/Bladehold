using UnityEngine;

/// <summary>
///     Lets the world nudge the player without taking their controls away: a <see cref="WorldEvent" /> gust
///     calls <see cref="Shove" /> each frame with a displacement, applied once in <c>LateUpdate</c> through the
///     <see cref="CharacterController" /> so walls still stop it and the player can walk against the wind.
///     Unlike <see cref="PlayerPullReceiver" /> it disables nothing. Ignored while mounted (the horse is too
///     heavy to shove, and <see cref="PlayerMount" /> owns movement) or dead.
/// </summary>
public class PlayerShoveReceiver : MonoBehaviour
{
    [SerializeField] private CharacterController characterController;
    [SerializeField] private Health health;
    [Tooltip("Optional; shoves are ignored while mounted.")]
    [SerializeField] private PlayerMount mount;

    private Vector3 pending;
    private bool anyError;

    private void OnValidate()
    {
        if (characterController == null) characterController = GetComponent<CharacterController>();
        if (health == null) health = GetComponent<Health>();
        if (mount == null) mount = GetComponent<PlayerMount>();
    }

    private void Start()
    {
        if (characterController == null)
        {
            Debug.LogError("[PlayerShoveReceiver] CharacterController is not assigned or found.", this);
            anyError = true;
        }
        if (health == null)
        {
            Debug.LogError("[PlayerShoveReceiver] Health is not assigned or found.", this);
            anyError = true;
        }
    }

    /// <summary>Adds a world-space displacement (metres) to apply this frame. Vertical is ignored.</summary>
    public void Shove(Vector3 displacement)
    {
        displacement.y = 0f;
        pending += displacement;
    }

    private void LateUpdate()
    {
        if (anyError || pending == Vector3.zero) return;
        Vector3 move = pending;
        pending = Vector3.zero;
        if (health.IsDead || !characterController.enabled) return;
        if (mount != null && mount.IsMounted) return;
        characterController.Move(move);
    }
}
