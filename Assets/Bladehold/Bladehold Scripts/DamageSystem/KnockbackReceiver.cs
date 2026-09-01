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

    [Header("Per-Variant Audio")]
    [Tooltip("Per-variant list of flying screams/yells played when launched into a ragdoll fling. If empty, falls back to global KnockbackConfigSO.flyingSfx.")]
    [SerializeField] private AudioClip[] flyingScreamSfx;

    [Header("Ragdoll Options")]
    [Tooltip("If true, this enemy always ragdolls on death regardless of the global ActiveCount capacity cap (e.g. for large/special enemies).")]
    [SerializeField] private bool forceRagdollOnDeath;

    public bool ForceRagdollOnDeath
    {
        get => forceRagdollOnDeath;
        set => forceRagdollOnDeath = value;
    }

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
        if (health == null || agent == null || ragdoll == null || animator == null || rootCollider == null || aiMovement == null || aiAnimation == null || config == null)
        {
            Debug.LogError($"[KnockbackReceiver] Essential dependency missing on {gameObject.name} (health: {health != null}, agent: {agent != null}, ragdoll: {ragdoll != null}, animator: {animator != null}, rootCollider: {rootCollider != null}, aiMovement: {aiMovement != null}, aiAnimation: {aiAnimation != null}, config: {config != null}). Incapacitation and death reactions will not run.");
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
        if (State == KnockbackState.Corpse) return;

        if (State == KnockbackState.Airborne)
        {
            // Already flying in ragdoll; let AirborneRoutine settle it into a corpse
            State = KnockbackState.Corpse;
            return;
        }

        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }

        State = KnockbackState.Corpse;
        SetAiEnabled(false);
        if (rootCollider != null) rootCollider.enabled = false;

        // Try ragdoll on death
        if ((EnemyRagdoll.HasCapacity || forceRagdollOnDeath) && ragdoll != null && ragdoll.BuildIfNeeded())
        {
            if (animator != null) animator.enabled = false;

            Vector3 flatDir = -transform.forward;
            if (playerHealth != null)
            {
                flatDir = transform.position - playerHealth.transform.position;
                flatDir.y = 0f;
                if (flatDir.sqrMagnitude < 0.0001f) flatDir = -transform.forward;
            }
            flatDir.Normalize();

            float torque = config != null ? config.spinTorque : 5f;
            Vector3 tumbleAxis = Vector3.Cross(Vector3.up, flatDir);
            Vector3 spin = tumbleAxis * torque + Random.insideUnitSphere * (torque * 0.3f);

            // Pop/collapse velocity so death falls naturally instead of freezing
            Vector3 deathLaunch = flatDir * 1.5f + Vector3.up * 0.8f;
            ragdoll.EnterRagdoll(deathLaunch, spin, 1.2f);
            routine = StartCoroutine(CorpseSettleRoutine());
        }
        else
        {
            // Fallback to animated death
            if (animator != null)
            {
                animator.enabled = true;
                animator.ResetTrigger("Stagger");
                animator.ResetTrigger(knockdownTrigger);
                animator.SetTrigger(Animator.StringToHash("Death"));
            }
        }
    }

    private IEnumerator CorpseSettleRoutine()
    {
        float timer = 0f;
        float settled = 0f;
        float timeout = config != null ? config.airborneTimeout : 6f;
        float settleSpd = config != null ? config.settleSpeed : 0.5f;
        float settleDur = config != null ? config.settleTime : 0.3f;

        while (timer < timeout)
        {
            timer += Time.deltaTime;
            if (ragdoll != null && ragdoll.Pelvis != null)
            {
                settled = ragdoll.PelvisSpeed < settleSpd ? settled + Time.deltaTime : 0f;
                if (settled >= settleDur) break;
            }
            yield return null;
        }

        if (ragdoll != null)
        {
            ragdoll.FreezeCorpse();
        }
        routine = null;
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

        bool isLethal = health.IsDead || health.CurrentHealth <= 0f;
        float pinThreshold = config != null ? config.arrowPinKnockbackThreshold : Resistance;
        bool isArrowPinCandidate = isLethal
            && (damage.isProjectile || damage.canPinToWall)
            && damage.direction != Vector3.zero
            && damage.knockbackForce >= pinThreshold;

        Vector3 launchDir = isArrowPinCandidate ? damage.direction.normalized : LaunchDirection(damage);
        float forceMag = damage.knockbackForce * (config != null ? config.knockbackMultiplier : 1f);
        if (config != null && config.maxKnockbackForce > 0f)
        {
            forceMag = Mathf.Min(forceMag, config.maxKnockbackForce);
        }

        Vector3 launchVelocity = launchDir * forceMag;
        Vector3 flatDir = transform.position - damage.sourcePosition;
        flatDir.y = 0f;
        flatDir = flatDir.sqrMagnitude > 0.0001f ? flatDir.normalized : -transform.forward;
        Vector3 tumbleAxis = Vector3.Cross(Vector3.up, flatDir);
        Vector3 spin = tumbleAxis * config.spinTorque + Random.insideUnitSphere * (config.spinTorque * 0.3f);
        ragdoll.EnterRagdoll(carried + launchVelocity, spin, config.randomLimbKick);

        Rigidbody pinnedBone = isArrowPinCandidate ? ragdoll.GetBoneRigidbody(damage.hitCollider, transform.position) : null;
        LayerMask pinMask = config != null ? config.wallPinLayers : ~0;
        bool isPinned = false;

        float airborne = 0f;
        float settled = 0f;
        while (airborne < config.airborneTimeout)
        {
            airborne += Time.deltaTime;

            if (isArrowPinCandidate && !isPinned && pinnedBone != null)
            {
                Vector3 bonePos = pinnedBone.position;
                Vector3 trajDir = damage.direction.normalized;
                float checkDistance = Mathf.Max(0.5f, pinnedBone.linearVelocity.magnitude * Time.deltaTime * 2.5f);

                if (Physics.SphereCast(bonePos, 0.25f, trajDir, out RaycastHit wallHit, checkDistance, pinMask, QueryTriggerInteraction.Ignore))
                {
                    if (!wallHit.collider.isTrigger
                        && !wallHit.collider.transform.IsChildOf(transform)
                        && (Player.Instance == null || !wallHit.collider.transform.IsChildOf(Player.Instance.transform))
                        && Vector3.Dot(wallHit.normal, -trajDir) > 0.15f)
                    {
                        isPinned = true;
                        PinLimbToWall(pinnedBone, wallHit, trajDir);
                        break;
                    }
                }
            }

            if (airborne >= config.minAirTime)
            {
                settled = ragdoll.PelvisSpeed < config.settleSpeed ? settled + Time.deltaTime : 0f;
                if (settled >= config.settleTime) break;
            }
            yield return null;
        }

        if (isPinned)
        {
            float minSec = config != null ? config.minWallPinSeconds : 4.0f;
            float maxSec = config != null ? config.maxWallPinSeconds : 5.0f;
            float pinDuration = Random.Range(minSec, maxSec);

            float pinElapsed = 0f;
            while (pinElapsed < pinDuration)
            {
                if (State != KnockbackState.Corpse) yield break;
                pinElapsed += Time.deltaTime;
                yield return null;
            }

            // Unpin limb so the corpse drops to the ground under gravity
            if (pinnedBone != null)
            {
                pinnedBone.isKinematic = false;
                pinnedBone.linearVelocity += Vector3.down * 0.5f;
            }

            float dropTimer = 0f;
            float dropSettled = 0f;
            while (dropTimer < (config != null ? config.airborneTimeout : 6f))
            {
                dropTimer += Time.deltaTime;
                dropSettled = ragdoll.PelvisSpeed < (config != null ? config.settleSpeed : 0.5f) ? dropSettled + Time.deltaTime : 0f;
                if (dropSettled >= (config != null ? config.settleTime : 0.3f)) break;
                yield return null;
            }

            ragdoll.FreezeCorpse();
            yield break;
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
        if (agent != null && agent.enabled && agent.isOnNavMesh) agent.isStopped = !value;
        if (aiMovement != null) aiMovement.enabled = value;
        if (aiAnimation != null) aiAnimation.enabled = value;
        if (aiAttack != null) aiAttack.enabled = value;
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

    private void PinLimbToWall(Rigidbody hitBone, RaycastHit wallHit, Vector3 trajDir)
    {
        State = KnockbackState.Corpse;
        if (!health.IsDead)
        {
            health.ReceiveDamage(new Damage { value = 999999f, type = DamageType.sharp });
        }

        // Clean up any initial body-attached stuck arrows so there are no awkward bone-parented props
        StuckArrow[] bodyArrows = GetComponentsInChildren<StuckArrow>();
        foreach (StuckArrow ba in bodyArrows)
        {
            Destroy(ba.gameObject);
        }

        // Align pin vector directly into the wall surface along arrow flight trajectory
        Vector3 pinDir = trajDir.sqrMagnitude > 0.0001f ? trajDir.normalized : -wallHit.normal;

        Vector3 pinPos = wallHit.point - pinDir * 0.05f;
        hitBone.position = pinPos;
        hitBone.isKinematic = true;
        hitBone.linearVelocity = Vector3.zero;
        hitBone.angularVelocity = Vector3.zero;

        StuckArrow prefabToSpawn = config != null ? config.arrowPinPrefab : null;
        if (prefabToSpawn == null && Player.Instance != null)
        {
            StuckArrowSpawner spawner = Player.Instance.GetComponentInChildren<StuckArrowSpawner>();
            if (spawner != null)
            {
                prefabToSpawn = spawner.ArrowPrefab;
            }
        }

        if (prefabToSpawn != null)
        {
            StuckArrow arrowProp = Instantiate(prefabToSpawn);
            // Unparented (parent: null) so the arrow stays fixed in world space as the pin in the wall
            arrowProp.Embed(wallHit.point, pinDir, 0.25f, parent: null);
        }

        if (config != null && config.wallPinSfx != null)
        {
            AudioSource.PlayClipAtPoint(config.wallPinSfx, wallHit.point);
        }
        if (config != null && config.wallPinVfxPrefab != null)
        {
            Instantiate(config.wallPinVfxPrefab, wallHit.point, Quaternion.LookRotation(wallHit.normal));
        }

        if (ragdoll != null && ragdoll.Config != null)
        {
            BloodDecalManager.SpawnDecal(wallHit.point, wallHit.normal, 1.2f, ragdoll.Config);
        }
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

        AudioClip[] screamClips = (flyingScreamSfx != null && flyingScreamSfx.Length > 0)
            ? flyingScreamSfx
            : config.flyingSfx;

        if (screamClips != null && screamClips.Length > 0)
        {
            AudioClip clip = screamClips[Random.Range(0, screamClips.Length)];
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

