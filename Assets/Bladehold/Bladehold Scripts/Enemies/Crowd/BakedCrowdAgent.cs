using HighlightPlus;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
///     Runs a goblin as a baked crowd instance: its Animator and SkinnedMeshRenderer are switched off
///     and <see cref="BakedCrowdRenderer" /> draws it instanced from <see cref="BakedCrowdAnimationSO" />
///     (idle, run, attack). When the goblin needs anything else — it's hit, knocked down, cheers,
///     turns golden/impulse or gets an outline — it is <see cref="Promote" />d to the real rig: the
///     skeleton is posed from the baked frame, then the Animator and renderer come back on. Once it
///     has recovered (no damage for a while, plain locomotion, away from the camera) it rejoins the
///     crowd (<see cref="Demote" />).
///
///     Death: a real ragdoll (<see cref="EnemyRagdoll.BuildIfNeeded" /> promotes first) while the
///     global ragdoll cap has room; past the cap <see cref="KnockbackReceiver" /> calls
///     <see cref="PlayBakedDeath" />, which plays one of the ragdoll falls recorded by the baker —
///     blood impacts included — and leaves a baked corpse that costs next to nothing until it
///     despawns.
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
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private AIAttack aiAttack;
    [SerializeField] private KnockbackReceiver knockbackReceiver;
    [SerializeField] private EnemyRagdoll ragdoll;
    [SerializeField] private GoldenGoblin goldenGoblin;
    [SerializeField] private ImpulseGoblin impulseGoblin;

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

    private int clip;
    private float clipTime;
    private int fadeClip;
    private float fadeClipTime;
    private float fadeRemaining;
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
    }

    private void Start()
    {
        if (crowdData == null || crowdData.boneTexture == null || crowdData.mesh == null || crowdData.material == null)
        {
            Debug.LogError($"BakedCrowdAgent on {name}: crowdData is missing or not baked (run Bladehold/Crowd/Bake Goblin Crowd Animation).", this);
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
        if (IsBaked)
        {
            BakedCrowdRenderer.Unregister(this);
        }
    }

    private void HandleDamaged(Damage damage)
    {
        lastDamageTime = Time.time;
        // A lethal hit is left to the death path: KnockbackReceiver either ragdolls (which promotes
        // via EnemyRagdoll.BuildIfNeeded) or, past the ragdoll cap, plays a baked fall.
        if (health.CurrentHealth > 0f)
        {
            Promote();
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

    private void StartClip(int next)
    {
        if (next == clip && crowdData.clips[next].loop) return;
        fadeClip = clip;
        fadeClipTime = clipTime;
        fadeRemaining = crowdData.crossfadeSeconds;
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
        if (playingDeath)
        {
            TickDeath(deltaTime, out frameData);
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
            fadeWeight = crowdData.crossfadeSeconds > 0f ? Mathf.Clamp01(fadeRemaining / crowdData.crossfadeSeconds) : 0f;
        }
        return new Vector4(
            crowdData.FrameOf(clip, clipTime),
            fadeWeight > 0f ? crowdData.FrameOf(fadeClip, fadeClipTime) : 0f,
            fadeWeight,
            0f);
    }

    private void TickDeath(float deltaTime, out Vector4 frameData)
    {
        BakedCrowdAnimationSO.Clip fall = crowdData.clips[clip];
        float previous = clipTime;
        if (clipTime < fall.length)
        {
            clipTime += deltaTime;
            PlayImpactsBetween(previous, clipTime);
        }
        // The fall's last frame is the settled corpse; FrameOf clamps a non-looping clip there.
        frameData = FrameData(deltaTime, 1f);
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
            if (ragdoll.BloodImpactFeedback != null)
            {
                ragdoll.BloodImpactFeedback.transform.rotation = Quaternion.LookRotation(Vector3.up);
                ragdoll.BloodImpactFeedback.PlayFeedbacks(point, impact.intensity);
            }
            if (config != null)
            {
                BloodDecalManager.SpawnDecal(point, Vector3.up, impact.decalSize, config);
            }
        }
    }

    public Matrix4x4 RenderMatrix => animator.transform.localToWorldMatrix;

    /// <summary>
    ///     Plays one of the baked ragdoll falls in place of a real ragdoll (the cap is full) and stays
    ///     baked as the corpse. Works from the live rig too. <paramref name="fallDirection" /> is the flat
    ///     direction the body is thrown. Returns false when there's nothing baked to play.
    /// </summary>
    public bool PlayBakedDeath(Vector3 fallDirection)
    {
        if (anyError || crowdData.deathClips == null || crowdData.deathClips.Length == 0) return false;
        if (NeedsRealRig() && !IsBaked) return false;

        // The falls are recorded launching along the rig's -Z; turn the body to face away from the throw.
        fallDirection.y = 0f;
        if (fallDirection.sqrMagnitude > 0.0001f)
        {
            Vector3 recorded = -animator.transform.forward;
            recorded.y = 0f;
            float yaw = Vector3.SignedAngle(recorded, fallDirection, Vector3.up);
            transform.Rotate(Vector3.up, yaw, Space.World);
        }

        int fall = crowdData.deathClips[Random.Range(0, crowdData.deathClips.Length)];
        if (IsBaked)
        {
            StartClip(fall);
        }
        else
        {
            EnterCrowd(fall);
        }
        playingDeath = true;
        return true;
    }

    /// <summary>Hands the goblin back to its real Animator and SkinnedMeshRenderer, posed where the bake left it. Idempotent.</summary>
    public void Promote()
    {
        if (!IsBaked || playingDeath) return;
        IsBaked = false;
        BakedCrowdRenderer.Unregister(this);
        PoseSkeletonFromBake();
        bodyRenderer.enabled = true;
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
