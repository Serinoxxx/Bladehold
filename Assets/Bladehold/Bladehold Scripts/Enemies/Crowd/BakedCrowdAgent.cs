using HighlightPlus;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
///     Runs a crowd enemy (the fodder goblin, the skeletons) as a baked crowd instance: its Animator,
///     SkinnedMeshRenderer and held props are switched off and <see cref="BakedCrowdRenderer" /> draws it instanced from <see cref="BakedCrowdAnimationSO" />
///     (idle, run, attack). When the goblin needs anything else — it's hit, knocked down, cheers,
///     turns golden/impulse or gets an outline — it is <see cref="Promote" />d to the real rig: the
///     skeleton is posed from the baked frame, then the Animator and renderer come back on. Once it
///     has recovered (no damage for a while, plain locomotion, away from the camera) it rejoins the
///     crowd (<see cref="Demote" />).
///
///     Ragdolls: a real ragdoll (<see cref="EnemyRagdoll.BuildIfNeeded" /> promotes first) while the
///     global ragdoll cap has room. Past the cap <see cref="KnockbackReceiver" /> calls
///     <see cref="PlayBakedFall" />, for a death or a non-lethal fling: one of the ragdoll falls
///     recorded by the baker plays (blood impacts included) while a pooled
///     <see cref="BakedFallBody" /> sphere carries the root along the real launch. The falls are
///     baked root-relative, so the body does the travel and the clip the tumble. A death stays down
///     as a baked corpse; a fling picks a fall that lands face-up, gets up with a get-up aligned to
///     that landing (<see cref="BeginGetUp" />), then the root moves to where the body stands
///     (<see cref="FinishGetUp" />).
///
///     Held props (<see cref="propRenderers" />) are baked into the crowd mesh, so the real ones are
///     hidden while baked and shown again on promotion.
///
///     Gameplay never changes: AI, NavMesh, attacks and colliders run as normal. The Animator keeps
///     its state while off, so triggers set meanwhile (an attack in progress) still play after
///     promotion.
/// </summary>
public class BakedCrowdAgent : MonoBehaviour
{
    [SerializeField] private BakedCrowdAnimationSO crowdData;
    [SerializeField] private Health health;
    [SerializeField] private Animator animator;
    [SerializeField] private SkinnedMeshRenderer bodyRenderer;
    [Tooltip("Held props (sword, shield) on the rig's bones. The bake merges them into the crowd mesh, so they're hidden while baked.")]
    [SerializeField] private Renderer[] propRenderers;
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private AIAttack aiAttack;
    [SerializeField] private KnockbackReceiver knockbackReceiver;
    [SerializeField] private EnemyRagdoll ragdoll;
    [SerializeField] private GoldenGoblin goldenGoblin;
    [SerializeField] private ImpulseGoblin impulseGoblin;
    [Tooltip("Optional: an early corpse-cap despawn releases a still-moving fall body before the sink.")]
    [SerializeField] private CorpseDespawner corpseDespawner;

    public BakedCrowdAnimationSO CrowdData => crowdData;
    public bool IsBaked { get; private set; }
    public int Layer => bodyRenderer.gameObject.layer;

    private static readonly int StaggerStateHash = Animator.StringToHash("Stagger");
    private static readonly int KnockdownStateHash = Animator.StringToHash("Knockdown");
    private static readonly int GetUpStateHash = Animator.StringToHash("GetUp");
    private static readonly int AttackStateHash = Animator.StringToHash("Attack");

    private Health playerHealth;
    private Matrix4x4[] inverseBindposes;
    private int[] boneOrderByDepth;
    private Matrix4x4[] poseScratch;
    private int checkOffset;
    private int attackLayerIndex = -1;
    private float lastDamageTime = Mathf.NegativeInfinity;
    private float nextDemoteCheck;
    private bool playingDeath;
    private bool anyError = false;

    private enum FallPhase
    {
        None,
        Falling,
        Settled,
        GettingUp,
    }

    private FallPhase fallPhase;
    private BakedFallBody fallBody;
    private float fallElapsed;
    private float fallSettledFor;

    private int clip;
    private float clipTime;
    private int fadeClip;
    private float fadeClipTime;
    private float fadeRemaining;
    private float fadeDuration;
    private static int nextCheckOffset;
    private static readonly Plane[] frustumPlanes = new Plane[6];

    private void OnValidate()
    {
        if (health == null) health = GetComponent<Health>();
        // Synty rigs keep the Animator on a child model object.
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (bodyRenderer == null) bodyRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (aiAttack == null) aiAttack = GetComponent<AIAttack>();
        if (knockbackReceiver == null) knockbackReceiver = GetComponent<KnockbackReceiver>();
        if (ragdoll == null) ragdoll = GetComponent<EnemyRagdoll>();
        if (goldenGoblin == null) goldenGoblin = GetComponent<GoldenGoblin>();
        if (impulseGoblin == null) impulseGoblin = GetComponent<ImpulseGoblin>();
        if (corpseDespawner == null) corpseDespawner = GetComponent<CorpseDespawner>();
    }

    private void Awake()
    {
        // Subscribed in Awake, ahead of every Start-time listener, so a hit promotes (and poses the
        // skeleton) before KnockbackReceiver or AIAttack drive the Animator.
        if (health != null)
        {
            health.OnDamaged += HandleDamaged;
            health.OnDied += HandleDied;
        }
        if (aiAttack != null)
        {
            aiAttack.OnAttackStarted += HandleAttackStarted;
        }
        if (corpseDespawner == null)
        {
            corpseDespawner = GetComponent<CorpseDespawner>();
        }
        if (corpseDespawner != null)
        {
            corpseDespawner.OnDespawnStarted += HandleDespawnStarted;
        }
    }

    private void Start()
    {
        if (crowdData == null || crowdData.boneTexture == null || crowdData.mesh == null || crowdData.material == null)
        {
            Debug.LogError($"BakedCrowdAgent on {name}: crowdData is missing or not baked (run Bladehold/Crowd/Bake Crowd Animations).", this);
            anyError = true;
        }
        if (health == null)
        {
            Debug.LogError($"BakedCrowdAgent on {name}: Health is not assigned.", this);
            anyError = true;
        }
        if (animator == null)
        {
            Debug.LogError($"BakedCrowdAgent on {name}: Animator is not assigned.", this);
            anyError = true;
        }
        if (bodyRenderer == null)
        {
            Debug.LogError($"BakedCrowdAgent on {name}: bodyRenderer is not assigned.", this);
            anyError = true;
        }
        if (agent == null)
        {
            Debug.LogError($"BakedCrowdAgent on {name}: NavMeshAgent is not assigned.", this);
            anyError = true;
        }
        if (!anyError && bodyRenderer.bones.Length != crowdData.boneCount)
        {
            Debug.LogError($"BakedCrowdAgent on {name}: body has {bodyRenderer.bones.Length} bones but the bake has {crowdData.boneCount}; re-bake.", this);
            anyError = true;
        }

        if (anyError || health.IsDead || NeedsRealRig())
        {
            enabled = false;
            return;
        }

        Player player = Player.Instance;
        if (player != null && player.Health != null)
        {
            playerHealth = player.Health;
            if (playerHealth.IsDead)
            {
                enabled = false;
                return;
            }
            // The cheer is a real Animator state.
            playerHealth.OnDied += Promote;
        }

        checkOffset = nextCheckOffset++;
        attackLayerIndex = animator.GetLayerIndex("Attack");
        animator.keepAnimatorStateOnDisable = true;
        EnterCrowd(BakedCrowdAnimationSO.ClipIdle);
    }

    private void OnDestroy()
    {
        if (health != null)
        {
            health.OnDamaged -= HandleDamaged;
            health.OnDied -= HandleDied;
        }
        if (aiAttack != null)
        {
            aiAttack.OnAttackStarted -= HandleAttackStarted;
        }
        if (playerHealth != null)
        {
            playerHealth.OnDied -= Promote;
        }
        if (corpseDespawner != null)
        {
            corpseDespawner.OnDespawnStarted -= HandleDespawnStarted;
        }
        ReleaseFallBody();
        if (IsBaked)
        {
            BakedCrowdRenderer.Unregister(this);
        }
    }

    private void HandleDamaged(Damage damage)
    {
        lastDamageTime = Time.time;
        // A lethal hit is left to the death path, and a fling to KnockbackReceiver: a real ragdoll
        // promotes via EnemyRagdoll.BuildIfNeeded, and past the ragdoll cap a baked fall plays from
        // the baked pose.
        bool flings = knockbackReceiver != null && knockbackReceiver.WouldFling(damage);
        if (health.CurrentHealth > 0f && !flings)
        {
            Promote();
        }
    }

    private void HandleDespawnStarted()
    {
        // The sink moves the root now.
        ReleaseFallBody();
        if (fallPhase != FallPhase.None)
        {
            clipTime = crowdData.clips[clip].length;
            fallPhase = FallPhase.None;
        }
    }

    private void HandleDied()
    {
        // Without a KnockbackReceiver nobody picks a death visual for us; fall back to the real rig.
        if (knockbackReceiver == null)
        {
            Promote();
        }
    }

    private void HandleAttackStarted()
    {
        if (!IsBaked || playingDeath) return;
        bool moving = agent.velocity.sqrMagnitude > crowdData.runSpeedThreshold * crowdData.runSpeedThreshold;
        StartClip(moving ? BakedCrowdAnimationSO.ClipAttackMoving : BakedCrowdAnimationSO.ClipAttack);
    }

    private bool NeedsRealRig()
    {
        return (goldenGoblin != null && goldenGoblin.IsGolden)
               || (impulseGoblin != null && impulseGoblin.IsImpulse)
               || (knockbackReceiver != null && knockbackReceiver.IsIncapacitated);
    }

    private bool NeedsVisualPromotion()
    {
        if (NeedsRealRig()) return true;
        HighlightEffect highlight = GetComponent<HighlightEffect>();
        return highlight != null && highlight.enabled;
    }

    private void StartClip(int next) => StartClip(next, crowdData.crossfadeSeconds);

    private void StartClip(int next, float crossfade)
    {
        if (next == clip && crowdData.clips[next].loop) return;
        fadeClip = clip;
        fadeClipTime = clipTime;
        fadeRemaining = crossfade;
        fadeDuration = crossfade;
        clip = next;
        // Loops start at a random phase so goblins that set off together don't stride in lockstep.
        clipTime = crowdData.clips[next].loop ? Random.Range(0f, crowdData.clips[next].length) : 0f;
    }

    /// <summary>Switches the real rig off and registers with the crowd renderer, starting on <paramref name="startClip" />.</summary>
    private void EnterCrowd(int startClip)
    {
        clip = startClip;
        clipTime = crowdData.clips[startClip].loop ? Random.Range(0f, crowdData.clips[startClip].length) : 0f;
        fadeRemaining = 0f;
        animator.enabled = false;
        bodyRenderer.enabled = false;
        SetPropsVisible(false);
        IsBaked = true;
        // While baked, the renderer ticks us; Update only runs the rejoin checks of a live goblin.
        enabled = false;
        BakedCrowdRenderer.Register(this);
    }

    /// <summary>
    ///     Advances the baked clip for this frame and returns the shader's per-instance frame data
    ///     (frame A, frame B, weight of B). Returns false when the goblin promoted itself instead.
    /// </summary>
    public bool Tick(float deltaTime, out Vector4 frameData)
    {
        frameData = default;
        if (playingDeath || fallPhase != FallPhase.None)
        {
            TickFall(deltaTime, out frameData);
            return true;
        }
        if (health.IsDead || (knockbackReceiver != null && knockbackReceiver.IsIncapacitated)
            || ((Time.frameCount + checkOffset) % crowdData.visualCheckInterval == 0 && NeedsVisualPromotion()))
        {
            Promote();
            return false;
        }

        float speed = agent.velocity.magnitude;
        bool attacking = clip == BakedCrowdAnimationSO.ClipAttack || clip == BakedCrowdAnimationSO.ClipAttackMoving;
        if (attacking && clipTime >= crowdData.clips[clip].length)
        {
            attacking = false;
        }
        if (!attacking)
        {
            StartClip(speed > crowdData.runSpeedThreshold ? BakedCrowdAnimationSO.ClipRun : BakedCrowdAnimationSO.ClipIdle);
        }

        // SlowStatus scales animator.speed, which still holds its value on the disabled Animator.
        float rate = animator.speed;
        if (clip == BakedCrowdAnimationSO.ClipRun)
        {
            BakedCrowdAnimationSO.Clip run = crowdData.clips[clip];
            rate *= run.referenceSpeed > 0f ? Mathf.Max(crowdData.minRunPlaybackRate, speed / run.referenceSpeed) : 1f;
        }
        clipTime += deltaTime * rate;
        frameData = FrameData(deltaTime, animator.speed);
        return true;
    }

    private Vector4 FrameData(float deltaTime, float fadeRate)
    {
        float fadeWeight = 0f;
        if (fadeRemaining > 0f)
        {
            fadeRemaining -= deltaTime;
            fadeClipTime += deltaTime * fadeRate;
            fadeWeight = fadeDuration > 0f ? Mathf.Clamp01(fadeRemaining / fadeDuration) : 0f;
        }
        return new Vector4(
            crowdData.FrameOf(clip, clipTime),
            fadeWeight > 0f ? crowdData.FrameOf(fadeClip, fadeClipTime) : 0f,
            fadeWeight,
            0f);
    }

    private void TickFall(float deltaTime, out Vector4 frameData)
    {
        BakedCrowdAnimationSO.Clip current = crowdData.clips[clip];
        if (fallPhase == FallPhase.Falling)
        {
            if (fallBody != null)
            {
                transform.position = fallBody.transform.position - Vector3.up * crowdData.fallBodyRadius;
            }

            // A big launch is airborne longer than the recording was: hold the tumble half-way down
            // until the body lands, rather than lying flat in mid-air.
            float limit = current.length;
            if (fallBody != null && !fallBody.Grounded && current.airborneHoldTime > 0f)
            {
                limit = Mathf.Max(clipTime, Mathf.Min(limit, current.airborneHoldTime));
            }
            float previous = clipTime;
            clipTime = Mathf.Min(clipTime + deltaTime, limit);
            PlayImpactsBetween(previous, clipTime);

            fallElapsed += deltaTime;
            bool resting = fallBody == null || (fallBody.Grounded && fallBody.Speed < crowdData.fallSettleSpeed);
            fallSettledFor = resting ? fallSettledFor + deltaTime : 0f;
            if ((clipTime >= current.length && fallSettledFor >= crowdData.fallSettleSeconds) || fallElapsed >= crowdData.fallTimeout)
            {
                ReleaseFallBody();
                clipTime = current.length;
                fallPhase = playingDeath ? FallPhase.None : FallPhase.Settled;
            }
        }
        else if (fallPhase == FallPhase.GettingUp)
        {
            clipTime += deltaTime;
        }
        // A fall's last frame is the settled pose; FrameOf clamps a non-looping clip there.
        frameData = FrameData(deltaTime, 1f);
    }

    private void ReleaseFallBody()
    {
        if (fallBody == null) return;
        fallBody.Release();
        fallBody = null;
    }

    private void PlayImpactsBetween(float from, float to)
    {
        if (crowdData.impacts == null || ragdoll == null) return;
        RagdollConfigSO config = ragdoll.Config;
        Transform rig = animator.transform;
        foreach (BakedCrowdAnimationSO.ImpactEvent impact in crowdData.impacts)
        {
            if (impact.clip != clip || impact.time <= from || impact.time > to) continue;
            Vector3 point = rig.TransformPoint(impact.localPoint);
            Vector3 normal = Vector3.up;
            // The recording landed on flat ground at the root's height; find the real ground under it.
            bool grounded = Physics.Raycast(point + Vector3.up * 0.75f, Vector3.down, out RaycastHit hit, 2f,
                BakedFallBody.CollisionMask, QueryTriggerInteraction.Ignore);
            if (grounded)
            {
                point = hit.point;
                normal = hit.normal;
            }
            if (ragdoll.BloodImpactFeedback != null)
            {
                ragdoll.BloodImpactFeedback.transform.rotation = Quaternion.LookRotation(normal);
                ragdoll.BloodImpactFeedback.PlayFeedbacks(point, impact.intensity);
            }
            if (config != null && grounded)
            {
                BloodDecalManager.SpawnDecal(point, normal, impact.decalSize, config);
            }
        }
    }

    public Matrix4x4 RenderMatrix => animator.transform.localToWorldMatrix;

    /// <summary>
    ///     True when a baked fall can play now: for a death any recorded fall, for a fling (non-lethal)
    ///     one that lands face-up, and the goblin isn't already falling. Golden and impulse goblins
    ///     need their real material, so they never fall baked.
    /// </summary>
    public bool CanPlayBakedFall(bool lethal)
    {
        if (anyError || crowdData.deathClips == null || crowdData.deathClips.Length == 0) return false;
        if ((goldenGoblin != null && goldenGoblin.IsGolden) || (impulseGoblin != null && impulseGoblin.IsImpulse)) return false;
        if (lethal) return true;
        return !playingDeath && fallPhase == FallPhase.None && !health.IsDead && crowdData.HasGetUpFalls;
    }

    /// <summary>
    ///     Plays one of the baked ragdoll falls in place of a real ragdoll (the cap is full), from the
    ///     baked crowd or the live rig. <paramref name="flatDirection" /> is the way the body is thrown;
    ///     <paramref name="velocity" /> is the launch (null: the recorded one). A death stays baked as
    ///     the corpse. A fling ends <see cref="FallSettled" />, for <see cref="KnockbackReceiver" /> to
    ///     find the stand-up spot and call <see cref="BeginGetUp" />. Turns the NavMeshAgent off: the
    ///     fall body moves the root. Returns false when nothing baked can play.
    /// </summary>
    public bool PlayBakedFall(Vector3 flatDirection, Vector3? velocity, bool lethal)
    {
        if (!CanPlayBakedFall(lethal)) return false;

        int fall = PickFall(lethal);
        BakedCrowdAnimationSO.Clip recorded = crowdData.clips[fall];

        // Turn the root so the recorded throw points along the real one.
        flatDirection.y = 0f;
        if (velocity.HasValue)
        {
            Vector3 flatVelocity = new Vector3(velocity.Value.x, 0f, velocity.Value.z);
            if (flatVelocity.sqrMagnitude > 0.01f) flatDirection = flatVelocity;
        }
        Transform rig = animator.transform;
        if (flatDirection.sqrMagnitude > 0.0001f)
        {
            Vector3 thrown = rig.TransformDirection(recorded.throwDirection);
            thrown.y = 0f;
            if (thrown.sqrMagnitude < 0.0001f) thrown = -rig.forward;
            float yaw = Vector3.SignedAngle(thrown, flatDirection, Vector3.up);
            transform.Rotate(Vector3.up, yaw, Space.World);
        }

        if (IsBaked)
        {
            StartClip(fall);
        }
        else
        {
            EnterCrowd(fall);
        }
        playingDeath = lethal;
        fallPhase = FallPhase.Falling;
        fallElapsed = 0f;
        fallSettledFor = 0f;

        if (agent.enabled) agent.enabled = false;
        Vector3 launch = velocity ?? rig.TransformDirection(recorded.launchVelocity);
        ReleaseFallBody();
        // A hair above the radius so a root a touch below the ground doesn't start inside it.
        Vector3 centre = transform.position + Vector3.up * (crowdData.fallBodyRadius + 0.02f);
        fallBody = BakedFallBody.Acquire(centre, launch, crowdData.fallBodyRadius, crowdData.fallBodyFriction);
        return true;
    }

    private int PickFall(bool lethal)
    {
        int[] falls = crowdData.deathClips;
        if (lethal) return falls[Random.Range(0, falls.Length)];
        int count = 0;
        foreach (int f in falls)
        {
            if (crowdData.clips[f].getUpClip >= 0) count++;
        }
        int pick = Random.Range(0, count);
        foreach (int f in falls)
        {
            if (crowdData.clips[f].getUpClip < 0) continue;
            if (pick-- == 0) return f;
        }
        return falls[0];
    }

    /// <summary>A hit on a body that's mid-fall: nudges the fall body, as a real ragdoll takes an extra impulse.</summary>
    public void AddFallImpulse(Vector3 velocityChange)
    {
        if (fallPhase == FallPhase.Falling && fallBody != null)
        {
            fallBody.AddVelocity(velocityChange);
        }
    }

    /// <summary>
    ///     The goblin died during a baked fling: a fall in progress (or settled) just stays down as the
    ///     corpse; one already getting up falls again, as a death.
    /// </summary>
    public void MakeFallLethal(Vector3 flatDirection)
    {
        if (fallPhase == FallPhase.GettingUp)
        {
            fallPhase = FallPhase.None;
            PlayBakedFall(flatDirection, null, true);
            return;
        }
        playingDeath = true;
        if (fallPhase == FallPhase.Settled) fallPhase = FallPhase.None;
    }

    /// <summary>A baked fling has landed and stopped; it waits for <see cref="BeginGetUp" />.</summary>
    public bool FallSettled => fallPhase == FallPhase.Settled;

    /// <summary>Where (world space) the body will be standing once its get-up finishes.</summary>
    public Vector3 GetUpStandPosition
    {
        get
        {
            int getUp = crowdData.clips[clip].getUpClip;
            if (fallPhase != FallPhase.Settled || getUp < 0) return transform.position;
            return animator.transform.TransformPoint(crowdData.clips[getUp].standPosition);
        }
    }

    /// <summary>Crossfades the settled fall into its aligned get-up.</summary>
    public void BeginGetUp()
    {
        if (fallPhase != FallPhase.Settled) return;
        int getUp = crowdData.clips[clip].getUpClip;
        if (getUp < 0) return;
        StartClip(getUp, crowdData.getUpCrossfadeSeconds);
        fallPhase = FallPhase.GettingUp;
    }

    public bool GetUpFinished => fallPhase == FallPhase.GettingUp && clipTime >= crowdData.clips[clip].length;

    /// <summary>
    ///     Moves the root to where the get-up left the body standing and carries on in the crowd's idle.
    ///     The pose doesn't jump: the unaligned get-up's last frame, drawn at the new root, is the
    ///     aligned one's at the old root.
    /// </summary>
    public void FinishGetUp()
    {
        if (fallPhase != FallPhase.GettingUp) return;
        BakedCrowdAnimationSO.Clip getUp = crowdData.clips[clip];
        Transform rig = animator.transform;
        Vector3 standWorld = rig.TransformPoint(getUp.standPosition);
        transform.rotation = transform.rotation * Quaternion.Euler(0f, getUp.standYaw, 0f);
        transform.position += standWorld - rig.position;

        fallPhase = FallPhase.None;
        if (crowdData.getUpClip >= 0)
        {
            clip = crowdData.getUpClip;
            clipTime = crowdData.clips[clip].length;
        }
        StartClip(BakedCrowdAnimationSO.ClipIdle);
    }

    /// <summary>Hands the goblin back to its real Animator and SkinnedMeshRenderer, posed where the bake left it. Idempotent.</summary>
    public void Promote()
    {
        // A baked fall has no live-rig equivalent; it finishes baked.
        if (!IsBaked || playingDeath || fallPhase != FallPhase.None) return;
        IsBaked = false;
        BakedCrowdRenderer.Unregister(this);
        PoseSkeletonFromBake();
        bodyRenderer.enabled = true;
        SetPropsVisible(true);
        animator.enabled = true;
        nextDemoteCheck = Time.time + crowdData.demoteCheckInterval;
        enabled = !anyError;
    }

    private void Update()
    {
        // Only a live (promoted) goblin runs this: it looks for the moment it can rejoin the crowd.
        if (anyError || IsBaked || Time.time < nextDemoteCheck) return;
        nextDemoteCheck = Time.time + crowdData.demoteCheckInterval;
        if (CanDemote())
        {
            Demote();
        }
    }

    private bool CanDemote()
    {
        if (health.IsDead || Time.time - lastDamageTime < crowdData.demoteAfterSeconds) return false;
        if ((playerHealth != null && playerHealth.IsDead) || NeedsVisualPromotion()) return false;
        if (ragdoll != null && ragdoll.IsRagdolled) return false;
        if (animator.IsInTransition(0)) return false;

        int baseState = animator.GetCurrentAnimatorStateInfo(0).shortNameHash;
        if (baseState == StaggerStateHash || baseState == KnockdownStateHash || baseState == GetUpStateHash) return false;
        if (attackLayerIndex >= 0 && (animator.IsInTransition(attackLayerIndex)
                                      || animator.GetCurrentAnimatorStateInfo(attackLayerIndex).shortNameHash == AttackStateHash)) return false;

        // The live pose snaps to the baked one; only do it where nobody's looking closely. A frustum
        // test, not renderer.isVisible, which is also true when the body only casts into a shadow map.
        Camera camera = Camera.main;
        if (camera != null)
        {
            float minDistance = crowdData.demoteMinCameraDistance;
            bool near = (camera.transform.position - transform.position).sqrMagnitude < minDistance * minDistance;
            if (near)
            {
                GeometryUtility.CalculateFrustumPlanes(camera, frustumPlanes);
                if (GeometryUtility.TestPlanesAABB(frustumPlanes, bodyRenderer.bounds)) return false;
            }
        }
        return true;
    }

    /// <summary>Returns a recovered live goblin to the baked crowd.</summary>
    public void Demote()
    {
        if (IsBaked || anyError || health.IsDead) return;
        bool moving = agent.velocity.magnitude > crowdData.runSpeedThreshold;
        EnterCrowd(moving ? BakedCrowdAnimationSO.ClipRun : BakedCrowdAnimationSO.ClipIdle);
    }

    private void SetPropsVisible(bool visible)
    {
        if (propRenderers == null) return;
        foreach (Renderer prop in propRenderers)
        {
            if (prop != null) prop.enabled = visible;
        }
    }

    private void PoseSkeletonFromBake()
    {
        Transform[] bones = bodyRenderer.bones;
        if (inverseBindposes == null)
        {
            Matrix4x4[] bindposes = bodyRenderer.sharedMesh.bindposes;
            inverseBindposes = new Matrix4x4[bindposes.Length];
            for (int i = 0; i < bindposes.Length; i++) inverseBindposes[i] = bindposes[i].inverse;

            // Setting a parent's world pose moves its children, so bones are applied parents-first.
            int[] depth = new int[bones.Length];
            boneOrderByDepth = new int[bones.Length];
            for (int i = 0; i < bones.Length; i++)
            {
                boneOrderByDepth[i] = i;
                for (Transform t = bones[i]; t != null && t != animator.transform; t = t.parent) depth[i]++;
            }
            System.Array.Sort(boneOrderByDepth, (a, b) => depth[a].CompareTo(depth[b]));
            poseScratch = new Matrix4x4[bones.Length];
        }

        int frame = crowdData.FrameOf(clip, clipTime);
        Matrix4x4 rig = animator.transform.localToWorldMatrix;
        for (int i = 0; i < bones.Length; i++)
        {
            poseScratch[i] = rig * crowdData.SkinMatrix(frame, i) * inverseBindposes[i];
        }
        foreach (int i in boneOrderByDepth)
        {
            if (bones[i] == null) continue;
            bones[i].SetPositionAndRotation(poseScratch[i].GetPosition(), poseScratch[i].rotation);
        }
    }
}
