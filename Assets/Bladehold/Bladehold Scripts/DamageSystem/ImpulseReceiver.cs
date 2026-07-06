using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
///     Reacts to impulse-stamped hits (<see cref="Damage.impulsePower" /> /
///     <see cref="Damage.impulseForce" />, stamped by the sword while the player's Impulse buff is
///     active) on this enemy. Like <see cref="KnockbackReceiver" /> it subscribes to
///     <see cref="Health.OnDamaged" /> — Health stays unaware of it.
///
///     Against this enemy's impulse resistance r (per-type via the roster CSV's
///     <c>impulseResistance</c> column → <see cref="SetResistance" />, else
///     <see cref="ImpulseConfigSO.defaultResistance" />):
///     <c>power &gt;= r</c> → full ragdoll fling (skyward launch, NavMesh recovery + stand-up on
///     landing if still alive); <c>power &gt;= r-1</c> → animation-only knockdown; below → nothing
///     extra (the normal <see cref="KnockbackReceiver" /> slide still applies — it consults
///     <see cref="WouldIncapacitate" /> so the two never fight over the same hit).
///
///     Fling sequence: disable the AI (movement/animation/attack), take the agent off the NavMesh,
///     swap the root capsule for the ragdoll's bone colliders, freeze the animator, and hand the
///     bones to <see cref="EnemyRagdoll" />. On settling: alive enemies re-seat on the NavMesh
///     (<see cref="NavMesh.SamplePosition" /> + <see cref="NavMeshAgent.Warp" />), play the get-up
///     state, and resume; corpses stay ragdolled and continue through the normal corpse pipeline;
///     landings that never find the NavMesh force-kill through the damage flow so wave/coin/kill
///     accounting stays consistent. Flings beyond <see cref="ImpulseConfigSO.maxSimultaneousRagdolls" />
///     degrade to knockdowns. Tunables live on <see cref="ImpulseConfigSO" />.
/// </summary>
public class ImpulseReceiver : MonoBehaviour
{
    public enum ImpulseState
    {
        Normal,
        KnockedDown,
        Airborne,
        Recovering,
        Corpse,
    }

    [SerializeField] private Health health;
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private EnemyRagdoll ragdoll;
    [SerializeField] private Animator animator;
    [SerializeField] private CapsuleCollider rootCollider;
    [SerializeField] private AIMovement aiMovement;
    [SerializeField] private AIAnimation aiAnimation;
    [SerializeField] private AIAttack aiAttack;
    [SerializeField] private ImpulseConfigSO config;

    // Animator trigger for the knockdown reaction. Wire KnockdownEnter/Exit states driven by this.
    [SerializeField] private string knockdownTrigger = "Knockdown";

    // Animator STATE (not trigger) played directly when standing up after a ragdoll landing — the
    // animator was disabled mid-flight, which reset its state machine, so a trigger would be lost.
    [SerializeField] private string getUpStateName = "GetUp";

    // Must match AIAnimation's cheer trigger: a Cheer fired into the disabled mid-flight animator is
    // lost, so a recovered enemy re-fires it if the player died while it was airborne.
    [SerializeField] private string cheerTrigger = "Cheer";

    [Tooltip("Optional dust-puff VFX instantiated where the flung body settles (assumed to clean itself up).")]
    [SerializeField] private GameObject landingVfxPrefab;
    [Tooltip("Optional sound played where the flung body settles.")]
    [SerializeField] private AudioClip landingSfx;

    public ImpulseState State { get; private set; } = ImpulseState.Normal;

    /// <summary>True while this enemy is knocked down, airborne, or standing up.</summary>
    public bool IsIncapacitated => State != ImpulseState.Normal;

    private float? resistanceOverride;
    private Health playerHealth;
    private int knockdownTriggerHash;
    private int getUpStateHash;
    private int cheerTriggerHash;
    private bool anyError = false;

    private float Resistance => resistanceOverride ?? (config != null ? config.defaultResistance : 0f);

    /// <summary>
    ///     Per-instance resistance override (e.g. <see cref="WaveSpawner" /> applying an enemy type's
    ///     roster CSV row). Call right after Instantiate, before Start runs; the shared
    ///     <see cref="ImpulseConfigSO" /> is never mutated.
    /// </summary>
    public void SetResistance(float value)
    {
        resistanceOverride = value;
    }

    /// <summary>
    ///     True when this hit will at least knock the enemy down — <see cref="KnockbackReceiver" />
    ///     skips its ground slide for such hits so the two reactions never fight.
    /// </summary>
    public bool WouldIncapacitate(Damage damage)
    {
        return !anyError && damage.impulsePower > 0f && damage.impulsePower >= Resistance - 1f;
    }

    private void OnValidate()
    {
        if (health == null)
        {
            health = GetComponent<Health>();
        }
        if (agent == null)
        {
            agent = GetComponent<NavMeshAgent>();
        }
        if (ragdoll == null)
        {
            ragdoll = GetComponent<EnemyRagdoll>();
        }
        if (animator == null)
        {
            // Synty rigs keep the Animator on a child model object.
            animator = GetComponentInChildren<Animator>();
        }
        if (rootCollider == null)
        {
            rootCollider = GetComponent<CapsuleCollider>();
        }
        if (aiMovement == null)
        {
            aiMovement = GetComponent<AIMovement>();
        }
        if (aiAnimation == null)
        {
            aiAnimation = GetComponent<AIAnimation>();
        }
        if (aiAttack == null)
        {
            aiAttack = GetComponent<AIAttack>();
        }
    }

    private void Start()
    {
        if (health == null)
        {
            Debug.LogError("Health component is not assigned or found on the GameObject.");
            anyError = true;
        }
        if (agent == null)
        {
            Debug.LogError("NavMeshAgent component is not assigned or found on the GameObject.");
            anyError = true;
        }
        if (ragdoll == null)
        {
            Debug.LogError("EnemyRagdoll component is not assigned or found on the GameObject.");
            anyError = true;
        }
        if (animator == null)
        {
            Debug.LogError("Animator component is not assigned or found on the GameObject.");
            anyError = true;
        }
        if (rootCollider == null)
        {
            Debug.LogError("Root CapsuleCollider is not assigned or found on the GameObject.");
            anyError = true;
        }
        if (aiMovement == null || aiAnimation == null || aiAttack == null)
        {
            Debug.LogError("AIMovement/AIAnimation/AIAttack are not all assigned or found on the GameObject.");
            anyError = true;
        }
        if (config == null)
        {
            Debug.LogError("ImpulseConfigSO is not assigned in the inspector.");
            anyError = true;
        }

        if (anyError)
        {
            return;
        }

        knockdownTriggerHash = Animator.StringToHash(knockdownTrigger);
        getUpStateHash = Animator.StringToHash(getUpStateName);
        cheerTriggerHash = Animator.StringToHash(cheerTrigger);

        playerHealth = Player.Instance != null ? Player.Instance.Health : null;

        health.OnDamaged += HandleDamaged;
        health.OnDied += HandleDied;
    }

    private void OnDestroy()
    {
        if (health != null)
        {
            health.OnDamaged -= HandleDamaged;
            health.OnDied -= HandleDied;
        }
    }

    private void HandleDamaged(Damage damage)
    {
        if (anyError || damage.impulsePower <= 0f)
        {
            return;
        }

        if (State == ImpulseState.Airborne)
        {
            // Already flying: shove the ragdoll instead of re-flinging.
            if (!health.IsDead)
            {
                ragdoll.AddImpulse(LaunchDirection(damage) * damage.impulseForce * 0.5f);
            }
            return;
        }

        if (State != ImpulseState.Normal)
        {
            return; // Knockdowns/recoveries don't stack.
        }

        float resistance = Resistance;
        if (damage.impulsePower < resistance - 1f)
        {
            return; // Fully resisted; the normal knockback slide handles the hit.
        }

        // A full fling needs to beat the resistance AND fit under the horde cap AND have a buildable
        // rig — any failure degrades to the knockdown so the buff never silently does nothing.
        bool fling = damage.impulsePower >= resistance
            && EnemyRagdoll.ActiveCount < config.maxSimultaneousRagdolls
            && ragdoll.BuildIfNeeded();

        if (fling)
        {
            StartCoroutine(FlingRoutine(damage));
        }
        else if (health.CurrentHealth > 0f)
        {
            // Knockdowns are for the living; a lethal near-threshold hit just dies normally
            // (OnDied fires right after this handler and the Death animation takes over).
            StartCoroutine(KnockdownRoutine());
        }
    }

    private void HandleDied()
    {
        // Any in-flight routine sees this and hands the body to the corpse pipeline.
        if (State != ImpulseState.Normal)
        {
            State = ImpulseState.Corpse;
        }
    }

    private IEnumerator KnockdownRoutine()
    {
        State = ImpulseState.KnockedDown;

        // The agent stays enabled and on-mesh — a knockdown is purely an animation pause.
        if (agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.ResetPath();
        }
        SetAiEnabled(false);
        animator.SetTrigger(knockdownTriggerHash);

        for (float elapsed = 0f; elapsed < config.knockdownSeconds; elapsed += Time.deltaTime)
        {
            if (State == ImpulseState.Corpse || health.IsDead)
            {
                // Death mid-knockdown: the Any-State Death transition has already taken the animator
                // (AIAnimation's handler fires even while disabled); leave the AI down for the corpse.
                yield break;
            }
            yield return null;
        }

        Resume();
    }

    private IEnumerator FlingRoutine(Damage damage)
    {
        State = ImpulseState.Airborne;

        // Order matters: read the agent's momentum before touching it, take the AI and agent down,
        // swap the root capsule for bone colliders, then free the bones from the animator.
        Vector3 carried = agent.enabled && agent.isOnNavMesh ? agent.velocity : Vector3.zero;
        SetAiEnabled(false);
        agent.enabled = false;
        rootCollider.enabled = false;
        animator.enabled = false;

        Vector3 launchVelocity = LaunchDirection(damage) * damage.impulseForce;
        Vector3 spin = Random.insideUnitSphere * config.spinTorque;
        ragdoll.EnterRagdoll(carried + launchVelocity, spin, config.randomLimbKick);

        // The root transform stays at the launch point while the bones fly; everything that needs the
        // body's real position (landing VFX, NavMesh recovery) uses the pelvis.
        float airborne = 0f;
        float settled = 0f;
        while (airborne < config.airborneTimeout)
        {
            airborne += Time.deltaTime;
            if (airborne >= config.minAirTime)
            {
                settled = ragdoll.PelvisSpeed < config.settleSpeed ? settled + Time.deltaTime : 0f;
                if (settled >= config.settleTime)
                {
                    break;
                }
            }
            yield return null;
        }

        Vector3 landingPoint = ragdoll.Pelvis != null ? ragdoll.Pelvis.position : transform.position;
        PlayLandingFeedback(landingPoint);

        if (State == ImpulseState.Corpse || health.IsDead)
        {
            // Died on launch or mid-air: stay ragdolled where it fell; the corpse pipeline
            // (CorpseDespawner/CorpseManager) has been running since OnDied.
            State = ImpulseState.Corpse;
            ragdoll.FreezeCorpse();
            yield break;
        }

        State = ImpulseState.Recovering;

        // Find the NavMesh under the landed pelvis; keep retrying briefly (the body may still slide
        // off a prop onto valid ground).
        bool found = false;
        NavMeshHit navHit = default;
        for (float retryElapsed = 0f; ; retryElapsed += config.recoverRetryInterval)
        {
            if (NavMesh.SamplePosition(ragdoll.Pelvis.position, out navHit, config.recoverSampleDistance, NavMesh.AllAreas))
            {
                found = true;
                break;
            }
            if (retryElapsed >= config.recoverRetryWindow)
            {
                break;
            }
            yield return new WaitForSeconds(config.recoverRetryInterval);
            if (State == ImpulseState.Corpse || health.IsDead)
            {
                ragdoll.FreezeCorpse();
                yield break;
            }
        }

        if (!found)
        {
            // Stranded off the NavMesh: kill through the normal damage flow (the DebugAdvanceWave
            // precedent) so coins, kill stats, and wave accounting all stay consistent.
            State = ImpulseState.Corpse;
            health.ReceiveDamage(new Damage { value = 999999f, type = DamageType.blunt });
            ragdoll.FreezeCorpse();
            yield break;
        }

        // Re-seat: stop simulating (bones hold the landed pose), snap the root under the body, put
        // the agent back on the mesh, and let the get-up state retake the skeleton.
        ragdoll.ExitRagdoll();
        transform.SetPositionAndRotation(navHit.position, UprightYaw());
        rootCollider.enabled = true;
        agent.enabled = true;
        agent.Warp(navHit.position);
        agent.isStopped = false;

        animator.enabled = true;
        // Direct Play, not a trigger: disabling the animator mid-flight reset its state machine.
        animator.Play(getUpStateHash, 0, 0f);
        // Pose immediately so the one frame between the snap and the next Update can't show the
        // bones stretched between the old ragdoll pose and the new root position.
        animator.Update(0f);

        for (float elapsed = 0f; elapsed < config.getUpSeconds; elapsed += Time.deltaTime)
        {
            if (State == ImpulseState.Corpse || health.IsDead)
            {
                // Died mid-get-up (the capsule is hittable again): the Death trigger has already
                // taken the re-enabled animator; leave the AI down for the corpse.
                yield break;
            }
            yield return null;
        }

        Resume();
    }

    /// <summary>Returns control to the AI after a knockdown or a completed get-up.</summary>
    private void Resume()
    {
        State = ImpulseState.Normal;
        SetAiEnabled(true);

        if (playerHealth != null && playerHealth.IsDead)
        {
            // The AI components already know (their handlers fired while disabled), but a Cheer fired
            // into a disabled animator was lost — re-fire it, and stay put like everyone else.
            if (agent.enabled && agent.isOnNavMesh)
            {
                agent.isStopped = true;
            }
            animator.SetTrigger(cheerTriggerHash);
        }
    }

    private void SetAiEnabled(bool value)
    {
        aiMovement.enabled = value;
        aiAnimation.enabled = value;
        aiAttack.enabled = value;
    }

    private Vector3 LaunchDirection(Damage damage)
    {
        Vector3 flat = transform.position - damage.sourcePosition;
        flat.y = 0f;
        // Degenerate (hit from directly above/same spot) → launch backwards from facing.
        flat = flat.sqrMagnitude > 0.0001f ? flat.normalized : -transform.forward;

        float angle = config.launchAngleDegrees * Mathf.Deg2Rad;
        return flat * Mathf.Cos(angle) + Vector3.up * Mathf.Sin(angle);
    }

    private Quaternion UprightYaw()
    {
        Vector3 forward = ragdoll.Pelvis != null ? ragdoll.Pelvis.transform.forward : transform.forward;
        forward.y = 0f;
        return forward.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(forward.normalized) : transform.rotation;
    }

    private void PlayLandingFeedback(Vector3 position)
    {
        if (landingVfxPrefab != null)
        {
            Instantiate(landingVfxPrefab, position, Quaternion.identity);
        }
        if (landingSfx != null)
        {
            AudioSource.PlayClipAtPoint(landingSfx, position);
        }
    }
}
