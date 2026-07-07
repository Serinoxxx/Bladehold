using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     Builds and owns a runtime ragdoll for an enemy. Enemy prefabs carry no ragdoll bones — the
///     first time this enemy is flung (<see cref="ImpulseReceiver" />), <see cref="BuildIfNeeded" />
///     walks the Humanoid rig via <see cref="Animator.GetBoneTransform" /> and adds Rigidbodies,
///     CharacterJoints, and colliders sized from the actual bone lengths (so the brute's 1.25 scale
///     just works). Built bodies idle kinematic with colliders disabled and are reused on later
///     flings, so enemies that are never flung cost nothing and flung ones cost nothing between
///     flings. Physical tunables live on <see cref="RagdollConfigSO" />.
///
///     The bone colliders are created after <see cref="DisableCollidersOnDeath" /> caches its list in
///     Start, which intentionally keeps them out of its death sweep — a corpse flung by a lethal hit
///     must keep its bone colliders to land with. <see cref="FreezeCorpse" /> disables them once the
///     corpse settles, and an early cap-despawn (<see cref="CorpseDespawner.OnDespawnStarted" />)
///     freezes immediately so the sink can carry the (kinematic) bones down with the root.
/// </summary>
public class EnemyRagdoll : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private RagdollConfigSO config;
    [Tooltip("Optional; lets an early corpse-cap despawn freeze a still-simulating ragdoll before the sink starts.")]
    [SerializeField] private CorpseDespawner corpseDespawner;

    /// <summary>Ragdolls simulating right now, across all enemies. <see cref="ImpulseReceiver" /> caps this.</summary>
    public static int ActiveCount { get; private set; }

    /// <summary>
    ///     Global cap on simultaneous ragdolls, applied by <see cref="GameSettingsService" /> from the
    ///     player-facing "Max Ragdolls" setting (0-50). <see cref="ImpulseReceiver" /> checks
    ///     <see cref="HasCapacity" /> before starting a new ragdoll for either an Impulse fling or a
    ///     plain kill, degrading to a knockdown / normal animated death when the cap is full.
    /// </summary>
    public static int MaxActive = 12;

    /// <summary>True while another ragdoll can start without exceeding <see cref="MaxActive" />.</summary>
    public static bool HasCapacity => ActiveCount < MaxActive;

    public bool IsRagdolled { get; private set; }

    /// <summary>The hips body — the reference point for landing detection and recovery placement.</summary>
    public Rigidbody Pelvis { get; private set; }

    /// <summary>Current pelvis speed in m/s, or 0 when not ragdolled.</summary>
    public float PelvisSpeed => IsRagdolled && Pelvis != null ? Pelvis.linearVelocity.magnitude : 0f;

    // Mass fractions per body part (sum = 1.0); heavier core keeps the tumble believable.
    private const float HipsMass = 0.25f, ChestMass = 0.25f, HeadMass = 0.10f;
    private const float UpperLegMass = 0.08f, LowerLegMass = 0.06f, UpperArmMass = 0.04f, LowerArmMass = 0.02f;

    private readonly List<Rigidbody> bodies = new List<Rigidbody>();
    private readonly List<Collider> boneColliders = new List<Collider>();
    private bool isBuilt = false;
    private bool buildFailed = false;
    private bool anyError = false;

    private void OnValidate()
    {
        if (animator == null)
        {
            // Synty rigs keep the Animator on a child model object.
            animator = GetComponentInChildren<Animator>();
        }
        if (corpseDespawner == null)
        {
            corpseDespawner = GetComponent<CorpseDespawner>();
        }
    }

    private void Start()
    {
        if (animator == null)
        {
            Debug.LogError("Animator component is not assigned or found on the GameObject.");
            anyError = true;
        }
        if (config == null)
        {
            Debug.LogError("RagdollConfigSO is not assigned in the inspector.");
            anyError = true;
        }

        if (anyError)
        {
            return;
        }

        if (corpseDespawner != null)
        {
            corpseDespawner.OnDespawnStarted += FreezeCorpse;
        }
    }

    private void OnDestroy()
    {
        if (corpseDespawner != null)
        {
            corpseDespawner.OnDespawnStarted -= FreezeCorpse;
        }
        // A destroyed mid-flight ragdoll must release its simulation slot.
        if (IsRagdolled)
        {
            IsRagdolled = false;
            ActiveCount = Mathf.Max(0, ActiveCount - 1);
        }
    }

    /// <summary>
    ///     Builds the ragdoll on first use. Returns false on any failure (missing bone, unknown layer)
    ///     — the caller degrades to a knockdown and the game keeps running. Failures latch so a broken
    ///     rig logs once, not per hit.
    /// </summary>
    public bool BuildIfNeeded()
    {
        if (isBuilt)
        {
            return true;
        }
        if (anyError || buildFailed)
        {
            return false;
        }

        buildFailed = !TryBuild();
        isBuilt = !buildFailed;
        return isBuilt;
    }

    /// <summary>
    ///     Switches the built ragdoll live: bodies non-kinematic, bone colliders on, every body seeded
    ///     with <paramref name="velocity" /> (carry-over momentum + launch), a tumble spin on the
    ///     pelvis, and <paramref name="limbKickSpeed" /> m/s of extra velocity punched into one random
    ///     non-pelvis body so every fling flails differently. The caller has already disabled the
    ///     animator so the bones are free.
    /// </summary>
    public void EnterRagdoll(Vector3 velocity, Vector3 angularVelocity, float limbKickSpeed = 0f)
    {
        if (!isBuilt || IsRagdolled)
        {
            return;
        }

        foreach (Collider c in boneColliders)
        {
            c.enabled = true;
        }
        foreach (Rigidbody body in bodies)
        {
            body.isKinematic = false;
            body.linearVelocity = velocity;
        }
        Pelvis.angularVelocity = angularVelocity;

        if (limbKickSpeed > 0f && bodies.Count > 1)
        {
            // bodies[0] is the pelvis (built first) — skip it so the kick flails a limb/torso/head
            // without altering the main trajectory the launch velocity set.
            Rigidbody kicked = bodies[Random.Range(1, bodies.Count)];
            kicked.linearVelocity += Random.onUnitSphere * limbKickSpeed;
        }

        IsRagdolled = true;
        ActiveCount++;
    }

    /// <summary>Extra shove for hits taken while already airborne — no re-fling, just more velocity.</summary>
    public void AddImpulse(Vector3 velocityChange)
    {
        if (!IsRagdolled)
        {
            return;
        }

        Pelvis.linearVelocity += velocityChange;
        foreach (Rigidbody body in bodies)
        {
            if (body != Pelvis)
            {
                body.linearVelocity += velocityChange * 0.5f;
            }
        }
    }

    /// <summary>Ends the simulation for a live recovery; bones keep their landed pose until the animator retakes them.</summary>
    public void ExitRagdoll()
    {
        Deactivate();
    }

    /// <summary>
    ///     Ends the simulation for a corpse: kinematic bodies follow the root when
    ///     <see cref="CorpseDespawner" /> sinks it, and disabled bone colliders stop the settled corpse
    ///     absorbing sword swings. Idempotent — also the <see cref="CorpseDespawner.OnDespawnStarted" /> handler.
    /// </summary>
    public void FreezeCorpse()
    {
        Deactivate();
    }

    private void Deactivate()
    {
        if (!IsRagdolled)
        {
            return;
        }

        foreach (Rigidbody body in bodies)
        {
            body.isKinematic = true;
        }
        foreach (Collider c in boneColliders)
        {
            c.enabled = false;
        }

        IsRagdolled = false;
        ActiveCount = Mathf.Max(0, ActiveCount - 1);
    }

    private bool TryBuild()
    {
        int layer = LayerMask.NameToLayer(config.ragdollLayerName);
        if (layer < 0)
        {
            Debug.LogError($"Ragdoll layer '{config.ragdollLayerName}' does not exist — name it in Tags & Layers (see RagdollConfigSO).");
            return false;
        }

        Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
        Transform chest = animator.GetBoneTransform(HumanBodyBones.Chest);
        if (chest == null)
        {
            chest = animator.GetBoneTransform(HumanBodyBones.Spine);
        }
        Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
        Transform leftUpperArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        Transform leftLowerArm = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
        Transform leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
        Transform rightUpperArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
        Transform rightLowerArm = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
        Transform rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        Transform leftUpperLeg = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
        Transform leftLowerLeg = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
        Transform leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
        Transform rightUpperLeg = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
        Transform rightLowerLeg = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
        Transform rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);

        if (hips == null || chest == null || head == null
            || leftUpperArm == null || leftLowerArm == null || leftHand == null
            || rightUpperArm == null || rightLowerArm == null || rightHand == null
            || leftUpperLeg == null || leftLowerLeg == null || leftFoot == null
            || rightUpperLeg == null || rightLowerLeg == null || rightFoot == null)
        {
            Debug.LogError($"Ragdoll build failed on '{name}': the Animator's avatar is missing required Humanoid bones.");
            return false;
        }

        // Torsos: boxes spanning hips→chest and chest→head, as wide as the pelvis.
        float torsoWidth = Vector3.Distance(leftUpperLeg.position, rightUpperLeg.position) * 1.5f;
        Pelvis = AddBody(hips, HipsMass, layer);
        AddBox(hips, chest.position, torsoWidth);
        Rigidbody chestBody = AddBody(chest, ChestMass, layer);
        AddBox(chest, head.position, torsoWidth * 0.9f);
        Rigidbody headBody = AddBody(head, HeadMass, layer);
        AddSphere(head);

        // Limbs: capsules along bone→child.
        Rigidbody leftUpperArmBody = AddLimb(leftUpperArm, leftLowerArm, UpperArmMass, layer);
        Rigidbody rightUpperArmBody = AddLimb(rightUpperArm, rightLowerArm, UpperArmMass, layer);
        Rigidbody leftLowerArmBody = AddLimb(leftLowerArm, leftHand, LowerArmMass, layer);
        Rigidbody rightLowerArmBody = AddLimb(rightLowerArm, rightHand, LowerArmMass, layer);
        Rigidbody leftUpperLegBody = AddLimb(leftUpperLeg, leftLowerLeg, UpperLegMass, layer);
        Rigidbody rightUpperLegBody = AddLimb(rightUpperLeg, rightLowerLeg, UpperLegMass, layer);
        Rigidbody leftLowerLegBody = AddLimb(leftLowerLeg, leftFoot, LowerLegMass, layer);
        Rigidbody rightLowerLegBody = AddLimb(rightLowerLeg, rightFoot, LowerLegMass, layer);

        // Joints: every non-hips body hangs off its anatomical parent.
        AddJoint(chestBody, Pelvis);
        AddJoint(headBody, chestBody);
        AddJoint(leftUpperArmBody, chestBody);
        AddJoint(rightUpperArmBody, chestBody);
        AddJoint(leftLowerArmBody, leftUpperArmBody);
        AddJoint(rightLowerArmBody, rightUpperArmBody);
        AddJoint(leftUpperLegBody, Pelvis);
        AddJoint(rightUpperLegBody, Pelvis);
        AddJoint(leftLowerLegBody, leftUpperLegBody);
        AddJoint(rightLowerLegBody, rightUpperLegBody);

        // Only the fast-moving core gets speculative CCD; per-limb CCD isn't worth the cost.
        Pelvis.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        return true;
    }

    private Rigidbody AddBody(Transform bone, float massFraction, int layer)
    {
        bone.gameObject.layer = layer;
        Rigidbody body = bone.gameObject.AddComponent<Rigidbody>();
        body.mass = config.totalMass * massFraction;
        body.linearDamping = config.linearDamping;
        body.angularDamping = config.angularDamping;
        body.maxDepenetrationVelocity = config.maxDepenetrationVelocity;
        body.interpolation = RigidbodyInterpolation.None;
        body.isKinematic = true;
        bodies.Add(body);
        return body;
    }

    private Rigidbody AddLimb(Transform bone, Transform endBone, float massFraction, int layer)
    {
        Rigidbody body = AddBody(bone, massFraction, layer);

        // Size in the bone's local space so the rig's scale (brute 1.25) is inherited for free.
        Vector3 toEnd = bone.InverseTransformPoint(endBone.position);
        float length = toEnd.magnitude;

        CapsuleCollider capsule = bone.gameObject.AddComponent<CapsuleCollider>();
        capsule.direction = DominantAxis(toEnd);
        capsule.height = length;
        capsule.radius = length * config.limbRadiusRatio;
        capsule.center = toEnd * 0.5f;
        capsule.enabled = false;
        boneColliders.Add(capsule);
        return body;
    }

    private void AddBox(Transform bone, Vector3 worldEnd, float worldWidth)
    {
        Vector3 toEnd = bone.InverseTransformPoint(worldEnd);
        float scale = Mathf.Max(bone.lossyScale.x, 0.0001f);

        BoxCollider box = bone.gameObject.AddComponent<BoxCollider>();
        Vector3 size = Vector3.one * (config.torsoThickness / scale);
        size[DominantAxis(toEnd)] = toEnd.magnitude;
        // The two non-length axes: one is the width, one the thickness; use width for both horizontals.
        int lengthAxis = DominantAxis(toEnd);
        for (int axis = 0; axis < 3; axis++)
        {
            if (axis != lengthAxis && size[axis] < worldWidth / scale)
            {
                size[axis] = worldWidth / scale;
                break;
            }
        }
        box.size = size;
        box.center = toEnd * 0.5f;
        box.enabled = false;
        boneColliders.Add(box);
    }

    private void AddSphere(Transform bone)
    {
        float scale = Mathf.Max(bone.lossyScale.x, 0.0001f);
        SphereCollider sphere = bone.gameObject.AddComponent<SphereCollider>();
        sphere.radius = config.headRadius / scale;
        sphere.center = Vector3.up * sphere.radius;
        sphere.enabled = false;
        boneColliders.Add(sphere);
    }

    private void AddJoint(Rigidbody body, Rigidbody connectedTo)
    {
        CharacterJoint joint = body.gameObject.AddComponent<CharacterJoint>();
        joint.connectedBody = connectedTo;
        joint.lowTwistLimit = new SoftJointLimit { limit = config.lowTwistLimit };
        joint.highTwistLimit = new SoftJointLimit { limit = config.highTwistLimit };
        joint.swing1Limit = new SoftJointLimit { limit = config.swing1Limit };
        joint.swing2Limit = new SoftJointLimit { limit = config.swing2Limit };
        joint.enableProjection = config.enableProjection;
    }

    private static int DominantAxis(Vector3 v)
    {
        Vector3 abs = new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
        if (abs.x >= abs.y && abs.x >= abs.z) return 0;
        return abs.y >= abs.z ? 1 : 2;
    }
}
