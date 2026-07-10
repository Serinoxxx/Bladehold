using UnityEngine;

/// <summary>
///     The horse's animator bridge, one script for all three riding modes (knight AI, riderless,
///     player). Rather than reading whichever driver is active (NavMeshAgent, agent.Move dashes,
///     or <see cref="HorseMotor" />'s CharacterController), it samples the transform itself each
///     frame — world displacement becomes the damped <c>Speed</c> param (m/s) and the signed yaw
///     delta becomes the damped <c>Turn</c> param (-1..1) — so it stays truthful no matter what is
///     moving the horse. Drivers push the discrete states through <see cref="TriggerRear" /> and
///     <see cref="SetCharging" />; death plays via its own <see cref="Health.OnDied" /> subscription
///     (which is what makes the horse die visibly while the player is mounted).
/// </summary>
public class HorseAnimation : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private Health health;

    [Header("Animator params")]
    [SerializeField] private string speedParam = "Speed";
    [SerializeField] private string turnParam = "Turn";
    [SerializeField] private string chargeParam = "Charge";
    [SerializeField] private string rearTrigger = "Rear";
    [SerializeField] private string deathTrigger = "Death";

    [Header("Smoothing")]
    [Tooltip("Seconds of smoothing on the Speed param.")]
    [SerializeField] private float speedDamping = 0.15f;

    [Tooltip("Seconds of smoothing on the Turn param.")]
    [SerializeField] private float turnDamping = 0.15f;

    [Tooltip("Yaw rate in degrees/second that maps to a full-scale Turn of ±1.")]
    [SerializeField] private float maxTurnDegreesPerSecond = 180f;

    private int speedHash;
    private int turnHash;
    private int chargeHash;
    private int rearHash;
    private int deathHash;

    private bool hasSpeed;
    private bool hasTurn;
    private bool hasCharge;
    private bool hasRear;
    private bool hasDeath;

    private Vector3 lastPosition;
    private float lastYaw;
    private float dampedSpeed;
    private float dampedTurn;
    private bool isDead = false;
    private bool anyError = false;

    private void OnValidate()
    {
        if (animator == null)
        {
            // The Malbers horse model keeps the Animator on a child, like the Synty rigs.
            animator = GetComponentInChildren<Animator>();
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
        if (health == null)
        {
            Debug.LogError("Health component is not assigned or found on the GameObject.");
            anyError = true;
        }

        if (anyError)
        {
            return;
        }

        speedHash = Animator.StringToHash(speedParam);
        turnHash = Animator.StringToHash(turnParam);
        chargeHash = Animator.StringToHash(chargeParam);
        rearHash = Animator.StringToHash(rearTrigger);
        deathHash = Animator.StringToHash(deathTrigger);

        // Warn once per missing param instead of spamming Animator warnings every frame — the
        // controller is authored in the Editor and may lag behind the code (the PlayerBow idiom).
        hasSpeed = HasParam(speedHash, speedParam);
        hasTurn = HasParam(turnHash, turnParam);
        hasCharge = HasParam(chargeHash, chargeParam);
        hasRear = HasParam(rearHash, rearTrigger);
        hasDeath = HasParam(deathHash, deathTrigger);

        // Off-screen horses skip skeleton/skinning but keep the state machine running, so Rear/Death
        // triggers never pop (the AIAnimation rule — never CullCompletely).
        animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

        lastPosition = transform.position;
        lastYaw = transform.eulerAngles.y;

        health.OnDied += HandleDied;
    }

    private void OnDestroy()
    {
        if (health != null)
        {
            health.OnDied -= HandleDied;
        }
    }

    private bool HasParam(int hash, string name)
    {
        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.nameHash == hash)
            {
                return true;
            }
        }
        Debug.LogWarning($"Horse animator has no '{name}' parameter; that channel will not animate.", this);
        return false;
    }

    private void HandleDied()
    {
        isDead = true;
        if (hasDeath)
        {
            animator.SetTrigger(deathHash);
        }
        // The corpse holds its death pose; nothing left to drive.
        enabled = false;
    }

    /// <summary>Plays the rear (the knight's charge telegraph / mount flavor). The driver is responsible for holding the horse still for the pose.</summary>
    public void TriggerRear()
    {
        if (anyError || isDead || !hasRear) return;
        animator.SetTrigger(rearHash);
    }

    /// <summary>Holds the animator's charge state (gallop lean) while the trample window is open, independent of the damped Speed catching up.</summary>
    public void SetCharging(bool value)
    {
        if (anyError || isDead || !hasCharge) return;
        animator.SetBool(chargeHash, value);
    }

    private void Update()
    {
        if (anyError || isDead) return;

        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        Vector3 displacement = transform.position - lastPosition;
        displacement.y = 0f;
        lastPosition = transform.position;

        float yaw = transform.eulerAngles.y;
        float yawDelta = Mathf.DeltaAngle(lastYaw, yaw);
        lastYaw = yaw;

        float speed = displacement.magnitude / dt;
        float turn = maxTurnDegreesPerSecond > 0f
            ? Mathf.Clamp(yawDelta / dt / maxTurnDegreesPerSecond, -1f, 1f)
            : 0f;

        dampedSpeed = Mathf.Lerp(dampedSpeed, speed, speedDamping > 0f ? dt / speedDamping : 1f);
        dampedTurn = Mathf.Lerp(dampedTurn, turn, turnDamping > 0f ? dt / turnDamping : 1f);

        if (hasSpeed)
        {
            animator.SetFloat(speedHash, dampedSpeed);
        }
        if (hasTurn)
        {
            animator.SetFloat(turnHash, dampedTurn);
        }
    }
}
