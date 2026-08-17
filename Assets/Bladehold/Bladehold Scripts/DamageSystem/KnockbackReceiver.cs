using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
///     Reacts to knockback-stamped hits (<see cref="Damage.knockbackForce" />) on this enemy.
///     It subscribes to <see cref="Health.OnDamaged" /> — Health stays unaware of it.
///
///     Against this enemy's knockback resistance r (per-type via the roster CSV's
///     <c>knockbackResistance</c> column → <see cref="SetResistance" />, else
///     <see cref="KnockbackConfigSO.defaultResistance" />):
///     <c>force &gt;= r</c> → full ragdoll fling (skyward launch, NavMesh recovery + stand-up on landing)
///     <c>force &gt;= r-1</c> → animation-only knockdown
///     <c>force &lt; r-1</c> → slide pushback (while AI is paused)
///
///     Every plain kill (no knockback hit involved) also ragdolls instead of just playing the Death
///     animation — see <see cref="HandleDied" /> — subject to the same <see cref="EnemyRagdoll.MaxActive" />
///     cap.
/// </summary>
public class KnockbackReceiver : MonoBehaviour
{
    public enum KnockbackState
    {
        Normal,
        Sliding,
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
    [SerializeField] private KnockbackConfigSO config;

    [SerializeField] private string knockdownTrigger = "Knockdown";
    [SerializeField] private string getUpStateName = "GetUp";
    [SerializeField] private string cheerTrigger = "Cheer";

    [SerializeField] private GameObject landingVfxPrefab;
    [SerializeField] private AudioClip landingSfx;

    public KnockbackState State { get; private set; } = KnockbackState.Normal;

    public bool IsIncapacitated => State != KnockbackState.Normal;

    private float? resistanceOverride;
    private Health playerHealth;
    private int knockdownTriggerHash;
    private int getUpStateHash;
    private int cheerTriggerHash;
    private bool anyError = false;
    private Coroutine routine;

    private float Resistance => resistanceOverride ?? (config != null ? config.defaultResistance : 0f);

    public float CurrentResistance => Resistance;

    public void SetResistance(float value)
    {
        resistanceOverride = value;
    }

    private void OnValidate()
    {
        if (health == null) health = GetComponent<Health>();
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (ragdoll == null) ragdoll = GetComponent<EnemyRagdoll>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (rootCollider == null) rootCollider = GetComponent<CapsuleCollider>();
        if (aiMovement == null) aiMovement = GetComponent<AIMovement>();
        if (aiAnimation == null) aiAnimation = GetComponent<AIAnimation>();
        if (aiAttack == null) aiAttack = GetComponent<AIAttack>();
    }

    private void Start()
    {
        if (health == null || agent == null || ragdoll == null || animator == null || rootCollider == null || aiMovement == null || aiAnimation == null || aiAttack == null || config == null)
        {
            anyError = true;
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
        if (anyError || damage.knockbackForce <= 0f) return;

        float force = damage.knockbackForce;
        if (config != null)
        {
            force *= config.knockbackMultiplier;
            if (config.maxKnockbackForce > 0f)
            {
                force = Mathf.Min(force, config.maxKnockbackForce);
            }
        }

        if (State == KnockbackState.Airborne)
        {
            if (!health.IsDead)
            {
                ragdoll.AddImpulse(LaunchDirection(damage) * force * 0.5f);
            }
            return;
        }

        if (State == KnockbackState.KnockedDown || State == KnockbackState.Recovering || State == KnockbackState.Corpse)
        {
            return; // Knockdowns/recoveries don't stack.
        }

        float resistance = Resistance;
        
        bool fling = force >= resistance
            && EnemyRagdoll.HasCapacity
            && ragdoll.BuildIfNeeded();

        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }

        if (fling)
        {
            PlayFlingHitFeedback();
            routine = StartCoroutine(FlingRoutine(damage));
        }
        else if (force >= resistance - 1f)
        {
            if (health.CurrentHealth > 0f)
            {
                PlayKnockdownFeedback();
                routine = StartCoroutine(KnockdownRoutine());
            }
        }
        else
        {
            if (health.CurrentHealth > 0f && agent != null && agent.enabled && agent.isOnNavMesh)
            {
                routine = StartCoroutine(SlideRoutine(damage, force));
            }
        }
    }

    private void HandleDied()
    {
        if (State != KnockbackState.Normal && State != KnockbackState.Sliding)
        {
            State = KnockbackState.Corpse;
            return;
        }

        if (!EnemyRagdoll.HasCapacity || !ragdoll.BuildIfNeeded()) return;

        State = KnockbackState.Corpse;
        SetAiEnabled(false);
        rootCollider.enabled = false;
        animator.enabled = false;
        Vector3 flatDir = -transform.forward;
        if (playerHealth != null)
        {
            flatDir = transform.position - playerHealth.transform.position;
            flatDir.y = 0f;
            if (flatDir.sqrMagnitude < 0.0001f) flatDir = -transform.forward;
        }
        flatDir.Normalize();
        Vector3 tumbleAxis = Vector3.Cross(Vector3.up, flatDir);
        Vector3 spin = tumbleAxis * config.spinTorque + Random.insideUnitSphere * (config.spinTorque * 0.3f);
        ragdoll.EnterRagdoll(Vector3.zero, spin);
    }
    
    private IEnumerator SlideRoutine(Damage damage, float force)
    {
        State = KnockbackState.Sliding;
        agent.isStopped = true;

        Vector3 direction = transform.position - damage.sourcePosition;
        direction.y = 0f;
        direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : -transform.forward;

        float elapsed = 0f;
        while (elapsed < config.slideDuration)
        {
            if (health.IsDead || agent == null || !agent.enabled || !agent.isOnNavMesh)
            {
                routine = null;
                yield break;
            }

            float decay = 1f - (elapsed / config.slideDuration);
            agent.Move(direction * (force * decay * Time.deltaTime));

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (agent != null && agent.enabled && agent.isOnNavMesh && !health.IsDead)
        {
            agent.isStopped = false;
        }
        
        State = KnockbackState.Normal;
        routine = null;
    }

    private IEnumerator KnockdownRoutine()
    {
        State = KnockbackState.KnockedDown;

        if (agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.ResetPath();
        }
        SetAiEnabled(false);
        animator.ResetTrigger("Stagger");
        animator.SetTrigger(knockdownTriggerHash);

        var randomVariance = Random.Range(-.1f, .1f);

        for (float elapsed = 0f; elapsed < config.knockdownSeconds + randomVariance; elapsed += Time.deltaTime)
        {
            if (State == KnockbackState.Corpse || health.IsDead) yield break;
            yield return null;
        }

        animator.CrossFadeInFixedTime(getUpStateHash, 0.2f, 0);

        for (float elapsed = 0f; elapsed < config.getUpSeconds; elapsed += Time.deltaTime)
        {
            if (State == KnockbackState.Corpse || health.IsDead) yield break;
            yield return null;
        }

        Resume();
    }

    private IEnumerator FlingRoutine(Damage damage)
    {
        State = KnockbackState.Airborne;

        Vector3 carried = agent.enabled && agent.isOnNavMesh ? agent.velocity : Vector3.zero;
        SetAiEnabled(false);
        agent.enabled = false;
        rootCollider.enabled = false;
        animator.enabled = false;

        Vector3 launchVelocity = LaunchDirection(damage) * (damage.knockbackForce * (config != null ? config.knockbackMultiplier : 1f));
        if (config != null && config.maxKnockbackForce > 0f)
        {
            launchVelocity = LaunchDirection(damage) * Mathf.Min(damage.knockbackForce * config.knockbackMultiplier, config.maxKnockbackForce);
        }
        Vector3 flatDir = transform.position - damage.sourcePosition;
        flatDir.y = 0f;
        flatDir = flatDir.sqrMagnitude > 0.0001f ? flatDir.normalized : -transform.forward;
        Vector3 tumbleAxis = Vector3.Cross(Vector3.up, flatDir);
        Vector3 spin = tumbleAxis * config.spinTorque + Random.insideUnitSphere * (config.spinTorque * 0.3f);
        ragdoll.EnterRagdoll(carried + launchVelocity, spin, config.randomLimbKick);

        float airborne = 0f;
        float settled = 0f;
        while (airborne < config.airborneTimeout)
        {
            airborne += Time.deltaTime;
            if (airborne >= config.minAirTime)
            {
                settled = ragdoll.PelvisSpeed < config.settleSpeed ? settled + Time.deltaTime : 0f;
                if (settled >= config.settleTime) break;
            }
            yield return null;
        }

        Vector3 landingPoint = ragdoll.Pelvis != null ? ragdoll.Pelvis.position : transform.position;
        PlayLandingFeedback(landingPoint);

        if (State == KnockbackState.Corpse || health.IsDead)
        {
            State = KnockbackState.Corpse;
            ragdoll.FreezeCorpse();
            yield break;
        }

        State = KnockbackState.Recovering;

        bool found = false;
        NavMeshHit navHit = default;
        for (float retryElapsed = 0f; ; retryElapsed += config.recoverRetryInterval)
        {
            if (NavMesh.SamplePosition(ragdoll.Pelvis.position, out navHit, config.recoverSampleDistance, NavMesh.AllAreas))
            {
                found = true;
                break;
            }
            if (retryElapsed >= config.recoverRetryWindow) break;
            
            yield return new WaitForSeconds(config.recoverRetryInterval);
            if (State == KnockbackState.Corpse || health.IsDead)
            {
                ragdoll.FreezeCorpse();
                yield break;
            }
        }

        if (!found)
        {
            State = KnockbackState.Corpse;
            health.ReceiveDamage(new Damage { value = 999999f, type = DamageType.blunt });
            ragdoll.FreezeCorpse();
            yield break;
        }

        ragdoll.ExitRagdoll();
        transform.SetPositionAndRotation(navHit.position, UprightYaw());
        rootCollider.enabled = true;
        agent.enabled = true;
        agent.Warp(navHit.position);
        agent.isStopped = false;

        animator.enabled = true;
        animator.ResetTrigger("Stagger");
        animator.Play(getUpStateHash, 0, 0f);
        animator.Update(0f);

        for (float elapsed = 0f; elapsed < config.getUpSeconds; elapsed += Time.deltaTime)
        {
            if (State == KnockbackState.Corpse || health.IsDead) yield break;
            yield return null;
        }

        Resume();
    }

    private void Resume()
    {
        State = KnockbackState.Normal;
        SetAiEnabled(true);

        if (agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = false;
        }

        if (playerHealth != null && playerHealth.IsDead)
        {
            if (agent.enabled && agent.isOnNavMesh) agent.isStopped = true;
            animator.SetTrigger(cheerTriggerHash);
        }
        routine = null;
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
        flat = flat.sqrMagnitude > 0.0001f ? flat.normalized : -transform.forward;

        // Favor the player's facing/forward direction so ragdoll flings carry forward in front of the player
        if (Player.Instance != null)
        {
            Vector3 playerForward = Player.Instance.transform.forward;
            playerForward.y = 0f;
            if (playerForward.sqrMagnitude > 0.0001f)
            {
                playerForward.Normalize();
                // Blend favoring player forward (65% player forward, 35% radial damage direction)
                flat = Vector3.Slerp(flat, playerForward, 0.65f).normalized;
            }
        }

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
        if (landingVfxPrefab != null) Instantiate(landingVfxPrefab, position, Quaternion.identity);
        if (landingSfx != null) AudioSource.PlayClipAtPoint(landingSfx, position);
    }

    private void PlayKnockdownFeedback()
    {
        if (config == null) return;
        Vector3 spawnPos = transform.position + Vector3.up * 1f;

        if (config.knockdownVfxPrefab != null)
        {
            GameObject inst = Instantiate(config.knockdownVfxPrefab, spawnPos, Quaternion.identity);
            Destroy(inst, 3f);
        }

        if (config.knockdownSfx != null && config.knockdownSfx.Length > 0)
        {
            AudioClip clip = config.knockdownSfx[Random.Range(0, config.knockdownSfx.Length)];
            if (clip != null) AudioSource.PlayClipAtPoint(clip, spawnPos);
        }
    }

    private void PlayFlingHitFeedback()
    {
        if (config == null) return;
        Vector3 spawnPos = transform.position + Vector3.up * 1f;

        if (config.flyingVfxPrefab != null)
        {
            GameObject inst = Instantiate(config.flyingVfxPrefab, spawnPos, Quaternion.identity);
            Destroy(inst, 3f);
        }

        if (config.flyingSfx != null && config.flyingSfx.Length > 0)
        {
            AudioClip clip = config.flyingSfx[Random.Range(0, config.flyingSfx.Length)];
            if (clip != null) AudioSource.PlayClipAtPoint(clip, spawnPos);
        }

        if (config.enableFlyingLightFlash)
        {
            SpawnFlashLight(spawnPos, config.flyingLightColor, config.flyingLightIntensity, config.flyingLightRange, config.flyingLightDuration);
        }
    }

    private void SpawnFlashLight(Vector3 position, Color color, float peakIntensity, float range, float duration)
    {
        GameObject lightObj = new GameObject("KnockbackFlashLight");
        lightObj.transform.position = position;

        Light lightComp = lightObj.AddComponent<Light>();
        lightComp.type = LightType.Point;
        lightComp.color = color;
        lightComp.intensity = peakIntensity;
        lightComp.range = range;

        FlashLightDimmer dimmer = lightObj.AddComponent<FlashLightDimmer>();
        dimmer.Initialize(peakIntensity, duration);
    }
}

