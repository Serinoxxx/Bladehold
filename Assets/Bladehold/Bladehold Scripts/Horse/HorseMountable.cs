using UnityEngine;

/// <summary>
///     The mount trigger on a riderless horse: the player mounts by interacting with this object.
///     Gated on the horse being alive and nobody already in the saddle.
/// </summary>
public class HorseMountable : MonoBehaviour, IInteractable
{
    [SerializeField] private Health health;
    [SerializeField] private HorseMotor horseMotor;

    public string PromptText => "Mount Horse";
    public bool CanInteract => !IsOccupied && health != null && !health.IsDead;
    public Vector3 InteractionPosition => transform.position;

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
    }

    /// <summary>Marks the saddle taken (or free). Set by <c>PlayerMount</c> and <c>MountedKnightRider</c>.</summary>
    public void SetOccupied(bool value)
    {
        IsOccupied = value;
    }

    public void Interact(Player player)
    {
        if (anyError || IsOccupied || health.IsDead) return;

        PlayerMount mount = player.GetComponent<PlayerMount>();
        if (mount == null || mount.IsMounted) return;

        mount.TryMount(horseMotor);
    }
}
