using UnityEngine;
using UnityEngine.AI;

/// <summary>
///     Drives the Synty goblin locomotion animator for an AI character from its
///     <see cref="NavMeshAgent" />. The animation maths lives in the reusable,
///     input-agnostic <see cref="LocomotionAnimator" />; this component just maps
///     the agent's motion onto a <see cref="LocomotionAnimator.LocomotionInput" />
///     snapshot each frame.
/// </summary>
public class AIAnimation : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private AIMovementSO movementData;
    [SerializeField] private Health health;

    // Animator trigger fired on death. The locomotion controller has no death state of its own, so
    // wire a state driven by this trigger (e.g. one of the Synty death clips) in the Animator.
    [SerializeField] private string deathTrigger = "Death";

    // Animator trigger fired when the player dies, so the goblin celebrates. Wire a cheer state
    // driven by this trigger in the Animator.
    [SerializeField] private string cheerTrigger = "Cheer";

    private LocomotionAnimator locomotion;
    private Health playerHealth;
    private int deathTriggerHash;
    private int cheerTriggerHash;
    private bool isDead = false;
    private bool isCheering = false;
    private bool anyError = false;

    private void OnValidate()
    {
        if (animator == null)
        {
            // Synty rigs keep the Animator on a child model object.
            animator = GetComponentInChildren<Animator>();
        }
        if (agent == null)
        {
            agent = GetComponent<NavMeshAgent>();
        }
        if (health == null)
        {
            health = GetComponent<Health>();
        }
    }

    private void Start()
    {
        if (animator == null)
        {
            Debug.LogError("Animator component is not assigned or found on the GameObject.");
            anyError = true;
        }
        if (agent == null)
        {
            Debug.LogError("NavMeshAgent component is not assigned or found on the GameObject.");
            anyError = true;
        }
        if (movementData == null)
        {
            Debug.LogError("AIMovementSO is not assigned in the inspector.");
            anyError = true;
        }
        if (health == null)
        {
            Debug.LogError("Health component is not assigned or found on the GameObject.");
            anyError = true;
        }

        if (anyError)
        {
            return;
        }

        locomotion = new LocomotionAnimator(
            animator,
            movementData.walkSpeed,
            movementData.runSpeed,
            movementData.sprintSpeed,
            movementData.leanCurve
        );

        deathTriggerHash = Animator.StringToHash(deathTrigger);
        cheerTriggerHash = Animator.StringToHash(cheerTrigger);

        // Animation reacts to death; Health never reaches back into this component.
        health.OnDied += HandleDied;

        // Celebrate when the player dies.
        Player player = Player.Instance;
        if (player != null && player.Health != null)
        {
            playerHealth = player.Health;
            playerHealth.OnDied += HandlePlayerDied;
        }
    }

    private void OnDestroy()
    {
        if (health != null)
        {
            health.OnDied -= HandleDied;
        }
        if (playerHealth != null)
        {
            playerHealth.OnDied -= HandlePlayerDied;
        }
    }

    private void HandleDied()
    {
        isDead = true;
        // Stop driving locomotion and let the death state take over the animator.
        animator.SetTrigger(deathTriggerHash);
    }

    private void HandlePlayerDied()
    {
        // A dead goblin stays dead; don't override its death animation with a cheer.
        if (isDead) return;

        isCheering = true;
        // Stop driving locomotion and let the cheer state take over the animator.
        animator.SetTrigger(cheerTriggerHash);
    }

    private void Update()
    {
        // Once dead or cheering, leave the animator on that state instead of writing locomotion.
        if (anyError || isDead || isCheering) return;

        // desiredVelocity points along the path even before the agent accelerates,
        // which gives the start/turn detection a stable intended direction.
        Vector3 moveDirection = agent.desiredVelocity.sqrMagnitude > 0.0001f
            ? agent.desiredVelocity.normalized
            : Vector3.zero;

        LocomotionAnimator.LocomotionInput input = new LocomotionAnimator.LocomotionInput
        {
            Velocity = agent.velocity,
            MoveDirection = moveDirection,
            Forward = transform.forward,
            // Agents on the NavMesh are always grounded; the chaser doesn't jump or fall.
            IsGrounded = true,
            IsWalking = false,
            IsCrouching = false,
            // The agent rotates to face its movement direction, so it is not strafing.
            IsStrafing = false,
        };

        locomotion.Tick(input, Time.deltaTime);
    }
}
