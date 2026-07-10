using UnityEngine;
using UnityEngine.AI;

/// <summary>
///     Owns the knight's ride: seats him on the horse, routes all damage to him, and unseats him
///     when his health drops below <see cref="MountedKnightSO.dismountHealthFraction" /> — after
///     which he fights on foot as an ordinary goblin (his standard AI components ship disabled and
///     are enabled here) and the horse becomes riderless and player-mountable.
///
///     The composite prefab's ROOT is the knight (so <see cref="WaveSpawner" />'s wave tracking,
///     kill credit, and CSV overrides all bind to him); the nested Horse prefab child is detached
///     to the scene root in <c>Awake</c> so the knight's corpse pipeline
///     (<see cref="CorpseDespawner" />/<see cref="DisableCollidersOnDeath" /> walk children) can
///     never drag the surviving horse down with him. While mounted, the knight is not reparented —
///     <c>LateUpdate</c> copies the horse's RiderSeat pose onto him, so dismounting is just
///     "stop syncing".
///
///     While mounted, every hit on the HORSE is negated and re-landed on the knight via the horse
///     Health's <see cref="Health.TryBlockDamage" /> (the <see cref="DamageBlocker" /> hook), so
///     "enough damage unseats him" is unambiguous and the horse always arrives at the riderless
///     state at full health. The knight's own ImpulseReceiver/KnockbackReceiver ship disabled too:
///     he cannot be flung out of the saddle — impulse hits just unseat him faster.
/// </summary>
public class MountedKnightRider : MonoBehaviour
{
    [SerializeField] private MountedKnightSO data;
    [SerializeField] private Health health;
    [SerializeField] private MountedKnightBrain brain;

    [Header("Horse (auto-wired from the horse child at edit time)")]
    [SerializeField] private HorseAnimation horseAnimation;
    [SerializeField] private Health horseHealth;
    [SerializeField] private NavMeshAgent horseAgent;
    [SerializeField] private HorseMountable horseMountable;
    [Tooltip("The saddle transform on the horse the knight is pose-synced to while mounted.")]
    [SerializeField] private Transform riderSeat;

    [Header("Knight on-foot components (ship DISABLED; enabled at dismount)")]
    [SerializeField] private NavMeshAgent knightAgent;
    [SerializeField] private AIMovement aiMovement;
    [SerializeField] private AIAttack aiAttack;
    [SerializeField] private AIAnimation aiAnimation;
    [SerializeField] private ImpulseReceiver impulseReceiver;
    [SerializeField] private KnockbackReceiver knockbackReceiver;

    [Header("Knight animator")]
    [SerializeField] private string ridingBool = "Riding";
    [SerializeField] private string dismountTrigger = "Dismount";
    [SerializeField] private string deathTrigger = "Death";

    private Animator knightAnimator;
    private Transform horseRoot;
    private bool mounted = true;
    private bool anyError = false;

    /// <summary>True while the knight is still in the saddle.</summary>
    public bool IsMounted => mounted;

    private void OnValidate()
    {
        if (health == null)
        {
            health = GetComponent<Health>();
        }
        if (brain == null)
        {
            brain = GetComponent<MountedKnightBrain>();
        }
        if (knightAgent == null)
        {
            knightAgent = GetComponent<NavMeshAgent>();
        }
        if (aiMovement == null)
        {
            aiMovement = GetComponent<AIMovement>();
        }
        if (aiAttack == null)
        {
            aiAttack = GetComponent<AIAttack>();
        }
        if (aiAnimation == null)
        {
            aiAnimation = GetComponent<AIAnimation>();
        }
        if (impulseReceiver == null)
        {
            impulseReceiver = GetComponent<ImpulseReceiver>();
        }
        if (knockbackReceiver == null)
        {
            knockbackReceiver = GetComponent<KnockbackReceiver>();
        }
        if (horseAnimation == null)
        {
            // The horse is a child at edit time; the runtime detach doesn't break these references.
            horseAnimation = GetComponentInChildren<HorseAnimation>();
        }
        if (horseAnimation != null)
        {
            if (horseHealth == null)
            {
                horseHealth = horseAnimation.GetComponent<Health>();
            }
            if (horseAgent == null)
            {
                horseAgent = horseAnimation.GetComponent<NavMeshAgent>();
            }
            if (horseMountable == null)
            {
                horseMountable = horseAnimation.GetComponentInChildren<HorseMountable>(true);
            }
        }
    }

    private void Awake()
    {
        // Detach the horse before anything else can walk the hierarchy: the knight's corpse sink
        // and collider-disable listeners must never touch the surviving horse.
        if (horseAnimation != null)
        {
            horseRoot = horseAnimation.transform;
            horseRoot.SetParent(null, true);
        }

        if (horseAgent != null)
        {
            horseAgent.enabled = true;
        }
        if (horseMountable != null)
        {
            horseMountable.enabled = false;
            horseMountable.SetOccupied(true);
        }
    }

    private void Start()
    {
        if (data == null)
        {
            Debug.LogError("MountedKnightSO is not assigned in the inspector.");
            anyError = true;
        }
        if (health == null)
        {
            Debug.LogError("Health component is not assigned or found on the GameObject.");
            anyError = true;
        }
        if (brain == null)
        {
            Debug.LogError("MountedKnightBrain component is not assigned or found on the GameObject.");
            anyError = true;
        }
        if (horseRoot == null || horseHealth == null || horseAgent == null || riderSeat == null)
        {
            Debug.LogError("Horse references (root/Health/NavMeshAgent/RiderSeat) are not assigned or found on the horse child.");
            anyError = true;
        }
        if (knightAgent == null || aiMovement == null || aiAttack == null || aiAnimation == null)
        {
            Debug.LogError("Knight on-foot components (NavMeshAgent/AIMovement/AIAttack/AIAnimation) are not assigned or found on the GameObject.");
            anyError = true;
        }

        if (anyError)
        {
            return;
        }

        // The horse child is detached by now, so this can only find the knight's own rig.
        knightAnimator = GetComponentInChildren<Animator>();
        if (knightAnimator != null)
        {
            SetAnimatorBool(ridingBool, true);
        }

        health.OnDamaged += HandleKnightDamaged;
        health.OnDied += HandleKnightDied;
        horseHealth.TryBlockDamage += ForwardToKnight;
    }

    private void OnDestroy()
    {
        if (health != null)
        {
            health.OnDamaged -= HandleKnightDamaged;
            health.OnDied -= HandleKnightDied;
        }
        if (horseHealth != null)
        {
            horseHealth.TryBlockDamage -= ForwardToKnight;
        }
    }

    private void LateUpdate()
    {
        if (anyError || !mounted || riderSeat == null) return;
        transform.SetPositionAndRotation(riderSeat.position, riderSeat.rotation);
    }

    /// <summary>
    ///     While the knight rides, the horse takes no damage — the hit is negated and re-landed on
    ///     the knight, so player attacks on either body wear HIM down and the horse is handed over
    ///     at full health. (Health snapshots the invocation list, so a dismount triggered inside
    ///     this forward can safely unregister the handler mid-iteration.)
    /// </summary>
    private bool ForwardToKnight(Damage damage)
    {
        if (!mounted)
        {
            return false;
        }

        health.ReceiveDamage(damage);
        return true;
    }

    private void HandleKnightDamaged(Damage damage)
    {
        if (anyError || !mounted || health.IsDead) return;

        if (health.MaxHealth > 0f && health.CurrentHealth / health.MaxHealth <= data.dismountHealthFraction)
        {
            Dismount();
        }
    }

    private void HandleKnightDied()
    {
        if (!mounted) return;

        // Killed in the saddle (burst damage / DebugWipeWave): same horse handoff, but the knight
        // lands as a corpse. His AIAnimation never went live, so fire the Death trigger directly;
        // every other death listener (coins, kill credit, corpse pipeline) is already on his Health.
        ReleaseHorse();
        PlaceKnight();
        mounted = false;

        if (knightAnimator != null)
        {
            SetAnimatorBool(ridingBool, false);
            SetAnimatorTrigger(deathTrigger);
        }
    }

    /// <summary>Unseats the (living) knight: hand the horse over, land on the NavMesh, and enable the standard goblin kit.</summary>
    private void Dismount()
    {
        if (!mounted) return;

        ReleaseHorse();
        Vector3 landing = PlaceKnight();
        mounted = false;

        if (knightAnimator != null)
        {
            SetAnimatorBool(ridingBool, false);
            SetAnimatorTrigger(dismountTrigger);
        }

        // First enable runs each component's Start now: validation passes (Player.Instance is
        // live), and the roster overrides stored at spawn (SetSpeed/SetDamage/SetResistance) take
        // effect. From here the knight is exactly a goblin.
        knightAgent.enabled = true;
        if (knightAgent.isOnNavMesh || NavMesh.SamplePosition(landing, out _, data.navMeshSampleDistance, NavMesh.AllAreas))
        {
            knightAgent.Warp(landing);
        }
        aiMovement.enabled = true;
        aiAttack.enabled = true;
        aiAnimation.enabled = true;
        if (impulseReceiver != null)
        {
            impulseReceiver.enabled = true;
        }
        if (knockbackReceiver != null)
        {
            knockbackReceiver.enabled = true;
        }
    }

    /// <summary>Puts the horse into its riderless state: brain off, agent parked, damageable again, and mountable by the player.</summary>
    private void ReleaseHorse()
    {
        brain.OnDismounted();

        if (horseAgent.enabled && horseAgent.isOnNavMesh)
        {
            horseAgent.isStopped = true;
            horseAgent.ResetPath();
        }
        horseAgent.enabled = false;

        horseHealth.TryBlockDamage -= ForwardToKnight;

        if (horseMountable != null)
        {
            horseMountable.SetOccupied(false);
            horseMountable.enabled = true;
        }
    }

    /// <summary>Places the knight beside the horse on the NavMesh; returns the landing point.</summary>
    private Vector3 PlaceKnight()
    {
        Vector3 landing = horseRoot.position + horseRoot.right * data.dismountSideOffset;
        if (NavMesh.SamplePosition(landing, out NavMeshHit hit, data.navMeshSampleDistance, NavMesh.AllAreas))
        {
            landing = hit.position;
        }
        else
        {
            landing = horseRoot.position;
        }

        transform.SetPositionAndRotation(landing, Quaternion.Euler(0f, transform.eulerAngles.y, 0f));
        return landing;
    }

    private void SetAnimatorBool(string name, bool value)
    {
        if (HasAnimatorParam(name))
        {
            knightAnimator.SetBool(name, value);
        }
    }

    private void SetAnimatorTrigger(string name)
    {
        if (HasAnimatorParam(name))
        {
            knightAnimator.SetTrigger(name);
        }
    }

    private bool HasAnimatorParam(string name)
    {
        foreach (AnimatorControllerParameter parameter in knightAnimator.parameters)
        {
            if (parameter.name == name)
            {
                return true;
            }
        }
        return false;
    }
}
