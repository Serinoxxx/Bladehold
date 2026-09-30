using HighlightPlus;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
///     Runs a goblin as a baked crowd instance: its Animator and SkinnedMeshRenderer are switched off
///     and <see cref="BakedCrowdRenderer" /> draws it instanced from <see cref="BakedCrowdAnimationSO" />
///     (idle, run, attack). The moment the goblin needs anything else — it's hit, dies, is knocked
///     down, cheers, turns golden/impulse or gets an outline — it is promoted back to the real rig for
///     good: the skeleton is posed from the baked frame, then the Animator and renderer come back on.
///
///     Gameplay never changes: AI, NavMesh, attacks and colliders run as normal; only the animation
///     and skinning of the untouched bulk of the horde is skipped. The Animator keeps its state while
///     off, so triggers set meanwhile (an attack in progress) still play after promotion.
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
    [SerializeField] private GoldenGoblin goldenGoblin;
    [SerializeField] private ImpulseGoblin impulseGoblin;

    public BakedCrowdAnimationSO CrowdData => crowdData;
    public bool IsBaked { get; private set; }
    public int Layer => bodyRenderer.gameObject.layer;

    private Health playerHealth;
    private Matrix4x4[] inverseBindposes;
    private int[] boneOrderByDepth;
    private Matrix4x4[] poseScratch;
    private int checkOffset;
    private bool anyError = false;

    private int clip;
    private float clipTime;
    private int fadeClip;
    private float fadeClipTime;
    private float fadeRemaining;
    private static int nextCheckOffset;

    private void OnValidate()
    {
        if (health == null) health = GetComponent<Health>();
        // Synty rigs keep the Animator on a child model object.
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (bodyRenderer == null) bodyRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (aiAttack == null) aiAttack = GetComponent<AIAttack>();
        if (knockbackReceiver == null) knockbackReceiver = GetComponent<KnockbackReceiver>();
        if (goldenGoblin == null) goldenGoblin = GetComponent<GoldenGoblin>();
        if (impulseGoblin == null) impulseGoblin = GetComponent<ImpulseGoblin>();
    }

    private void Awake()
    {
        // Subscribed in Awake, ahead of every Start-time listener, so a lethal hit promotes (and poses
        // the skeleton) before KnockbackReceiver ragdolls it or AIAnimation fires the death trigger.
        if (health != null)
        {
            health.OnDamaged += HandleDamaged;
            health.OnDied += Promote;
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
        clip = BakedCrowdAnimationSO.ClipIdle;
        clipTime = Random.Range(0f, crowdData.clips[clip].length);

        animator.keepAnimatorStateOnDisable = true;
        animator.enabled = false;
        bodyRenderer.enabled = false;
        IsBaked = true;
        BakedCrowdRenderer.Register(this);
    }

    private void OnEnable()
    {
        if (IsBaked) BakedCrowdRenderer.Register(this);
    }

    private void OnDisable()
    {
        if (IsBaked) BakedCrowdRenderer.Unregister(this);
    }

    private void OnDestroy()
    {
        if (health != null)
        {
            health.OnDamaged -= HandleDamaged;
            health.OnDied -= Promote;
        }
        if (aiAttack != null)
        {
            aiAttack.OnAttackStarted -= HandleAttackStarted;
        }
        if (playerHealth != null)
        {
            playerHealth.OnDied -= Promote;
        }
    }

    private void HandleDamaged(Damage damage)
    {
        Promote();
    }

    private void HandleAttackStarted()
    {
        if (!IsBaked) return;
        bool moving = agent.velocity.sqrMagnitude > crowdData.runSpeedThreshold * crowdData.runSpeedThreshold;
        StartClip(moving ? BakedCrowdAnimationSO.ClipAttackMoving : BakedCrowdAnimationSO.ClipAttack);
    }

    private bool NeedsRealRig()
    {
        return (goldenGoblin != null && goldenGoblin.IsGolden)
               || (impulseGoblin != null && impulseGoblin.IsImpulse)
               || (knockbackReceiver != null && knockbackReceiver.IsIncapacitated);
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

    /// <summary>
    ///     Advances the baked clip for this frame and returns the shader's per-instance frame data
    ///     (frame A, frame B, weight of B). Returns false when the goblin promoted itself instead.
    /// </summary>
    public bool Tick(float deltaTime, out Vector4 frameData)
    {
        frameData = default;
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

        float fadeWeight = 0f;
        if (fadeRemaining > 0f)
        {
            fadeRemaining -= deltaTime;
            fadeClipTime += deltaTime * animator.speed;
            fadeWeight = crowdData.crossfadeSeconds > 0f ? Mathf.Clamp01(fadeRemaining / crowdData.crossfadeSeconds) : 0f;
        }

        frameData = new Vector4(
            crowdData.FrameOf(clip, clipTime),
            fadeWeight > 0f ? crowdData.FrameOf(fadeClip, fadeClipTime) : 0f,
            fadeWeight,
            0f);
        return true;
    }

    public Matrix4x4 RenderMatrix => animator.transform.localToWorldMatrix;

    private bool NeedsVisualPromotion()
    {
        if (NeedsRealRig()) return true;
        HighlightEffect highlight = GetComponent<HighlightEffect>();
        return highlight != null && highlight.enabled;
    }

    /// <summary>Hands the goblin back to its real Animator and SkinnedMeshRenderer, posed where the bake left it. Idempotent.</summary>
    public void Promote()
    {
        if (!IsBaked) return;
        IsBaked = false;
        BakedCrowdRenderer.Unregister(this);
        PoseSkeletonFromBake();
        bodyRenderer.enabled = true;
        animator.enabled = true;
        enabled = false;
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
