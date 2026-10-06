using MoreMountains.Feedbacks;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
///     Repair spot at a castle gate (plan 15). During the prep phase the player holds [E] here to pour
///     in-run supply into the gate's <see cref="Health" /> at <see cref="WaveChoiceConfigSO.gateRepairHpPerSecond" />,
///     costing <see cref="WaveChoiceConfigSO.supplyPerGateHp" /> supply per HP. Supply spent on the gate is
///     supply not spent on towers, which is the tension the plan wants.
///
///     The interact system is press-only (<see cref="IInteractable.Interact" /> fires once on the press), so
///     the press starts a repair and the station keeps it going while the interact key (E / gamepad West)
///     is physically held, the same direct-device fallback <c>PlayerMount</c> uses for dismount. Repair
///     stops on release, leaving the radius, the end of prep, full HP, no supply, or the per-prep cap
///     (<see cref="WaveChoiceConfigSO.maxGateRepairPerPrep" />, 0 = unlimited), which resets each time
///     <see cref="GameLoopManager.IsPrepPhase" /> flips back on.
///
///     Supply is bought one unit at a time with <see cref="RunSession.TrySpendInRunSupply" />, and each unit
///     adds <c>1 / supplyPerGateHp</c> HP of prepaid budget that the per-frame heal draws from, so a
///     fractional ratio heals smoothly and the station never spends supply the player doesn't have.
///     Healing goes through <see cref="Health.Heal" />, whose <c>OnHealthChanged</c> makes <see cref="Gate" />
///     mirror the new value into <see cref="RunSession.FortressGateCurrentHealth" />.
/// </summary>
public class GateRepairStation : MonoBehaviour, IInteractable, IAffordableInteractable
{
    [Header("Gate")]
    [Tooltip("The gate this station repairs. Auto-wired from a parent Gate; falls back to the nearest gate at Start.")]
    [SerializeField] private Gate gate;
    [Tooltip("Where the player stands to repair. Defaults to this transform.")]
    [SerializeField] private Transform interactionAnchor;
    [SerializeField] private float interactionRadius = 3.5f;

    [Header("Feedback")]
    [Tooltip("Looping hammering, started when a repair starts and stopped when it ends.")]
    [SerializeField] private MMF_Player repairLoopFeedback;
    [Tooltip("Tick at the gate each time another chunk of HP (tickEveryHp) is healed, e.g. a gate-bar pulse.")]
    [SerializeField] private MMF_Player repairTickFeedback;
    [Tooltip("HP healed between repair ticks.")]
    [Min(1f)] [SerializeField] private float tickEveryHp = 10f;

    private WaveChoiceConfigSO config;
    private Health gateHealth;
    private bool anyError;

    private bool isRepairing;
    private float prepaidHp;          // HP already paid for but not yet healed (always < one supply's worth after a repair).
    private float repairedThisPrep;   // HP healed since the current prep phase started, for the per-prep cap.
    private float hpSinceTick;
    private bool wasPrepPhase;

    public bool IsRepairing => isRepairing;
    public Vector3 InteractionPosition => interactionAnchor != null ? interactionAnchor.position : transform.position;
    public float InteractionRadius => interactionRadius > 0f ? interactionRadius : 3.5f;

    private static bool IsPrepPhase => GameLoopManager.Instance == null || GameLoopManager.Instance.IsPrepPhase;

    private bool GateUsable => !anyError && gateHealth != null && !gateHealth.IsDead;
    private bool GateFull => gateHealth.CurrentHealth >= gateHealth.MaxHealth - 0.01f;
    private bool CapReached => config.maxGateRepairPerPrep > 0f && repairedThisPrep >= config.maxGateRepairPerPrep - 0.01f;
    private bool CanAffordMore => prepaidHp > 0.001f || RunSession.InRunSupply > 0;

    public bool CanInteract => GateUsable && IsPrepPhase && !GateFull;

    public bool CanAfford => CanAffordMore;

    public string PromptText
    {
        get
        {
            if (!GateUsable) return "";
            int current = Mathf.CeilToInt(gateHealth.CurrentHealth);
            int max = Mathf.RoundToInt(gateHealth.MaxHealth);
            if (CapReached)
            {
                return string.Format(Loc.Get("gate_repair.prompt_cap", "Repair limit reached this prep · {0}/{1} HP"), current, max);
            }
            if (!CanAffordMore)
            {
                return string.Format(Loc.Get("gate_repair.prompt_no_supply", "Not enough supply to repair · {0}/{1} HP"), current, max);
            }
            string key = isRepairing ? "gate_repair.prompt_active" : "gate_repair.prompt";
            string english = isRepairing ? "Repairing gate: {0} supply / HP · {1}/{2} HP" : "Hold to repair gate: {0} supply / HP · {1}/{2} HP";
            return string.Format(Loc.Get(key, english), config.supplyPerGateHp.ToString("0.##"), current, max);
        }
    }

    private void OnValidate()
    {
        if (gate == null) gate = GetComponentInParent<Gate>();
    }

    /// <summary>The scene's repair station, if any (one per gate scene).</summary>
    public static GateRepairStation Instance { get; private set; }

    private void OnEnable()
    {
        Instance = this;
        InteractableRegistry.Register(this);
    }

    private void OnDisable()
    {
        if (Instance == this) Instance = null;
        InteractableRegistry.Unregister(this);
        StopRepair();
    }

    private void Start()
    {
        config = WaveChoiceConfigSO.Load();
        if (config == null)
        {
            Debug.LogError($"[GateRepairStation] {name}: WaveChoiceConfig not found in Resources; repair disabled.", this);
            anyError = true;
        }

        if (gate == null) gate = GetComponentInParent<Gate>();
        if (gate == null) gate = Gate.NearestAlive(transform.position);
        if (gate == null)
        {
            Debug.LogError($"[GateRepairStation] {name}: no Gate assigned or found in the scene; repair disabled.", this);
            anyError = true;
        }
        else
        {
            gateHealth = gate.Damageable as Health;
            if (gateHealth == null)
            {
                Debug.LogError($"[GateRepairStation] {name}: gate {gate.name} has no Health; repair disabled.", this);
                anyError = true;
            }
        }

        // Feedback is required but never gameplay-breaking: log, don't set anyError.
        if (repairLoopFeedback == null) Debug.LogError($"[GateRepairStation] {name}: repairLoopFeedback is not assigned.", this);
        if (repairTickFeedback == null) Debug.LogError($"[GateRepairStation] {name}: repairTickFeedback is not assigned.", this);

        wasPrepPhase = IsPrepPhase;
    }

    public void Interact(Player player)
    {
        if (!CanInteract || isRepairing || CapReached || !CanAffordMore) return;

        isRepairing = true;
        hpSinceTick = 0f;
        if (repairLoopFeedback != null) repairLoopFeedback.PlayFeedbacks(TickPosition);
    }

    private void Update()
    {
        if (anyError) return;

        // A new prep phase (false -> true) resets the per-prep cap.
        bool prep = IsPrepPhase;
        if (prep && !wasPrepPhase) repairedThisPrep = 0f;
        wasPrepPhase = prep;

        if (!isRepairing) return;

        if (!prep || !GateUsable || GateFull || CapReached || !InteractHeld() || !PlayerInRange())
        {
            StopRepair();
            return;
        }

        float hpWanted = config.gateRepairHpPerSecond * Time.deltaTime;
        hpWanted = Mathf.Min(hpWanted, gateHealth.MaxHealth - gateHealth.CurrentHealth);
        if (config.maxGateRepairPerPrep > 0f)
        {
            hpWanted = Mathf.Min(hpWanted, config.maxGateRepairPerPrep - repairedThisPrep);
        }

        // Buy whole units of supply until the prepaid budget covers this frame's heal (or supply runs out).
        int supplySpent = 0;
        float hpPerSupply = 1f / Mathf.Max(0.01f, config.supplyPerGateHp);
        while (prepaidHp < hpWanted && RunSession.TrySpendInRunSupply(1))
        {
            prepaidHp += hpPerSupply;
            supplySpent++;
        }
        if (supplySpent > 0) RunTelemetry.RecordGateRepair(supplySpent);

        float heal = Mathf.Min(hpWanted, prepaidHp);
        if (heal > 0f)
        {
            prepaidHp -= heal;
            repairedThisPrep += heal;
            gateHealth.Heal(heal);

            hpSinceTick += heal;
            if (hpSinceTick >= tickEveryHp)
            {
                hpSinceTick -= tickEveryHp;
                if (repairTickFeedback != null) repairTickFeedback.PlayFeedbacks(TickPosition);
            }
        }

        if (heal < hpWanted - 0.0001f)
        {
            StopRepair(); // out of supply
        }
    }

    private void StopRepair()
    {
        if (!isRepairing) return;
        isRepairing = false;
        if (repairLoopFeedback != null) repairLoopFeedback.StopFeedbacks();
    }

    private Vector3 TickPosition => gate != null ? gate.TargetPosition : transform.position;

    private bool PlayerInRange()
    {
        Player player = Player.Instance;
        if (player == null) return false;
        float radius = InteractionRadius + 0.5f; // small slack so shuffling at the edge doesn't cut the repair
        return (player.transform.position - InteractionPosition).sqrMagnitude <= radius * radius;
    }

    /// <summary>
    ///     Whether the interact binding is held. InputReader only exposes the press, so read the devices
    ///     bound to Interact (E / gamepad West) directly.
    /// </summary>
    private static bool InteractHeld()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.eKey.isPressed) return true;
        Gamepad gamepad = Gamepad.current;
        return gamepad != null && gamepad.buttonWest.isPressed;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(InteractionPosition, InteractionRadius);
    }
#endif
}
