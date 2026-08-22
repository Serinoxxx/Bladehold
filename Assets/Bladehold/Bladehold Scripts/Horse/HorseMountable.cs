using UnityEngine;

/// <summary>
///     The mount trigger on a riderless horse: the player mounts by JUMPING into this trigger
///     collider (a grounded walk-through does nothing), which snaps them to the saddle via
///     <c>PlayerMount.TryMount</c>. Gated on the riding skill (<c>PlayerMount.CanRide</c>), the horse
///     being alive, and nobody already in the saddle — the mounted-knight prefab ships this
///     component disabled and <c>MountedKnightRider</c> enables it when the knight is unseated.
///     Uses the pickup idiom (trigger-enter + <c>GetComponentInParent</c>) to resolve the player.
/// </summary>
[RequireComponent(typeof(Collider))]
public class HorseMountable : MonoBehaviour
{
    [SerializeField] private Health health;
    [SerializeField] private HorseMotor horseMotor;

    /// <summary>True while anyone (knight or player) sits in the saddle; the trigger ignores the player until it clears.</summary>
    public bool IsOccupied { get; private set; }

    private bool anyError = false;

    private void OnValidate()
    {
        if (health == null)
        {
            health = GetComponentInParent<Health>();
        }
        if (health != null)
        {
            health.ImmuneToPlayerDamage = true;
        }
        if (horseMotor == null)
        {
            horseMotor = GetComponentInParent<HorseMotor>();
        }
    }

    private void Awake()
    {
        if (health == null)
        {
            health = GetComponentInParent<Health>();
        }
        if (health != null)
        {
            health.ImmuneToPlayerDamage = true;
        }
    }

    private void Start()
    {
        if (health == null)
        {
            Debug.LogError("Health component is not assigned or found on a parent.");
            anyError = true;
        }
        if (horseMotor == null)
        {
            Debug.LogError("HorseMotor component is not assigned or found on a parent.");
            anyError = true;
        }

        Collider triggerCollider = GetComponent<Collider>();
        if (triggerCollider != null && !triggerCollider.isTrigger)
        {
            Debug.LogWarning("HorseMountable's collider is not a trigger; mounting will not detect the player.", this);
        }
    }

    /// <summary>Marks the saddle taken (or free). Set by <c>PlayerMount</c> and <c>MountedKnightRider</c>.</summary>
    public void SetOccupied(bool value)
    {
        IsOccupied = value;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (anyError || IsOccupied || health.IsDead) return;

        Player player = other.GetComponentInParent<Player>();
        if (player == null) return;

        PlayerMount mount = player.GetComponent<PlayerMount>();
        if (mount == null || mount.IsMounted) return; // || !mount.CanRide) return;

        // Mounting is a deliberate jump into the saddle, not a walk-by.
        if (mount.CharacterController != null && mount.CharacterController.isGrounded) return;

        mount.TryMount(horseMotor);
    }
}
