using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     Ragdolls the horse when it dies. The horse rig isn't Humanoid, so <see cref="EnemyRagdoll" />
///     can't build it; instead the bones to simulate are listed in <see cref="segments" /> (a capsule
///     from each bone towards its end bone). Nothing is built until death, so a live horse costs
///     PhysX nothing. A standard <see cref="Health.OnDied" /> listener: it turns the Animator off
///     (pre-empting <see cref="HorseAnimation" />'s death clip), seeds every body with the horse's
///     carried velocity plus a sideways topple so it goes down on its flank, and once the body settles
///     (or <see cref="CorpseDespawner" /> starts the sink) freezes the bones kinematic so the sink carries
///     them down with the root. If the build fails the Animator is left alone and the death clip plays.
/// </summary>
public class HorseRagdoll : MonoBehaviour
{
    [Serializable]
    public class Segment
    {
        public Transform bone;
        [Tooltip("The capsule runs from the bone to this one. Empty = a sphere on the bone.")]
        public Transform end;
        [Tooltip("Capsule / sphere radius in metres.")]
        public float radius = 0.1f;
        [Tooltip("Relative mass; all weights are normalised against totalMass.")]
        public float massWeight = 1f;
    }

    [SerializeField] private Health health;
    [SerializeField] private Animator animator;
    [SerializeField] private CorpseDespawner corpseDespawner;

    [Tooltip("Bones to simulate. Each joint hangs off the nearest listed ancestor; the first entry must be the root body (pelvis/torso).")]
    [SerializeField] private List<Segment> segments = new List<Segment>();

    [Header("Physics")]
    [SerializeField] private string ragdollLayerName = "Ragdoll";
    [SerializeField] private float totalMass = 200f;
    [SerializeField] private float linearDamping = 0.1f;
    [SerializeField] private float angularDamping = 2f;
    [SerializeField] private float maxDepenetrationVelocity = 3f;
    [SerializeField] private float twistLimit = 30f;
    [SerializeField] private float swingLimit = 40f;

    [Header("Fall")]
    [Tooltip("Fraction of the horse's last movement velocity carried into the ragdoll.")]
    [SerializeField] private float carryVelocityFactor = 1f;
    [Tooltip("Roll speed (rad/s) around the horse's forward axis so it falls onto its side instead of standing propped on stiff legs.")]
    [SerializeField] private float toppleAngularSpeed = 2.5f;
    [Tooltip("Sideways shove (m/s) in the topple direction.")]
    [SerializeField] private float toppleSideSpeed = 1f;
    [Tooltip("Seconds of simulation before the corpse is frozen kinematic.")]
    [SerializeField] private float settleTime = 5f;

    private readonly List<Rigidbody> bodies = new List<Rigidbody>();
    private readonly List<Collider> boneColliders = new List<Collider>();
    private Vector3 lastPosition;
    private Vector3 velocity;
    private bool isRagdolled = false;
    private bool anyError = false;

    private void OnValidate()
    {
        if (health == null)
        {
            health = GetComponent<Health>();
        }
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }
        if (corpseDespawner == null)
        {
            corpseDespawner = GetComponent<CorpseDespawner>();
        }
    }

    private void Start()
    {
        if (health == null)
        {
            Debug.LogError("HorseRagdoll: Health is not assigned or found on the GameObject.", this);
            anyError = true;
        }
        if (animator == null)
        {
            Debug.LogError("HorseRagdoll: Animator is not assigned or found in children.", this);
            anyError = true;
        }
        if (segments.Count == 0 || segments[0].bone == null)
        {
            Debug.LogError("HorseRagdoll: no segments assigned (the first must be the root body).", this);
            anyError = true;
        }

        if (anyError)
        {
            return;
        }

        lastPosition = transform.position;
        health.OnDied += HandleDied;
        if (corpseDespawner != null)
        {
            corpseDespawner.OnDespawnStarted += Freeze;
        }
    }

    private void OnDestroy()
    {
        if (health != null)
        {
            health.OnDied -= HandleDied;
        }
        if (corpseDespawner != null)
        {
            corpseDespawner.OnDespawnStarted -= Freeze;
        }
    }

    private void Update()
    {
        if (anyError || isRagdolled) return;

        float dt = Time.deltaTime;
        if (dt <= 0f) return;
        velocity = (transform.position - lastPosition) / dt;
        lastPosition = transform.position;
    }

    private void HandleDied()
    {
        if (anyError || isRagdolled) return;

        if (!TryBuild())
        {
            // Leave the Animator running: HorseAnimation's death clip is the fallback.
            return;
        }

        animator.enabled = false;
        foreach (SkinnedMeshRenderer meshRenderer in GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            // The bones leave the root's anchored bounds as the body falls.
            meshRenderer.updateWhenOffscreen = true;
        }

        float side = UnityEngine.Random.value < 0.5f ? -1f : 1f;
        Vector3 launch = velocity * carryVelocityFactor + transform.right * (side * toppleSideSpeed);
        foreach (Collider c in boneColliders)
        {
            c.enabled = true;
        }
        foreach (Rigidbody body in bodies)
        {
            body.isKinematic = false;
            body.linearVelocity = launch;
        }
        // Rolling towards +right is a negative spin about forward.
        bodies[0].angularVelocity = transform.forward * (-side * toppleAngularSpeed);

        isRagdolled = true;
        StartCoroutine(FreezeWhenSettled());
    }

    private IEnumerator FreezeWhenSettled()
    {
        yield return new WaitForSeconds(settleTime);
        Freeze();
    }

    /// <summary>Stops the simulation: kinematic bones follow the root when CorpseDespawner sinks it, and the corpse stops absorbing hits. Idempotent.</summary>
    private void Freeze()
    {
        if (!isRagdolled) return;

        foreach (Rigidbody body in bodies)
        {
            if (body != null) body.isKinematic = true;
        }
        foreach (Collider c in boneColliders)
        {
            if (c != null) c.enabled = false;
        }
        isRagdolled = false;
    }

    private bool TryBuild()
    {
        int layer = LayerMask.NameToLayer(ragdollLayerName);
        if (layer < 0)
        {
            Debug.LogError($"HorseRagdoll: layer '{ragdollLayerName}' does not exist.", this);
            return false;
        }

        float weightSum = 0f;
        foreach (Segment segment in segments)
        {
            if (segment.bone == null)
            {
                Debug.LogError("HorseRagdoll: a segment has no bone assigned.", this);
                return false;
            }
            weightSum += Mathf.Max(0.01f, segment.massWeight);
        }

        Dictionary<Transform, Rigidbody> bodyByBone = new Dictionary<Transform, Rigidbody>();
        foreach (Segment segment in segments)
        {
            Transform bone = segment.bone;
            bone.gameObject.layer = layer;

            Rigidbody body = bone.gameObject.AddComponent<Rigidbody>();
            body.mass = totalMass * Mathf.Max(0.01f, segment.massWeight) / weightSum;
            body.linearDamping = linearDamping;
            body.angularDamping = angularDamping;
            body.maxDepenetrationVelocity = maxDepenetrationVelocity;
            body.isKinematic = true;
            bodies.Add(body);
            bodyByBone[bone] = body;

            float scale = Mathf.Max(bone.lossyScale.x, 0.0001f);
            if (segment.end != null)
            {
                Vector3 toEnd = bone.InverseTransformPoint(segment.end.position);
                CapsuleCollider capsule = bone.gameObject.AddComponent<CapsuleCollider>();
                capsule.direction = DominantAxis(toEnd);
                capsule.radius = segment.radius / scale;
                capsule.height = toEnd.magnitude + capsule.radius * 2f;
                capsule.center = toEnd * 0.5f;
                capsule.enabled = false;
                boneColliders.Add(capsule);
            }
            else
            {
                SphereCollider sphere = bone.gameObject.AddComponent<SphereCollider>();
                sphere.radius = segment.radius / scale;
                sphere.enabled = false;
                boneColliders.Add(sphere);
            }
        }

        // The root body only needs speculative CCD; it carries most of the mass and speed.
        bodies[0].collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

        for (int i = 1; i < segments.Count; i++)
        {
            Rigidbody parent = FindParentBody(segments[i].bone, bodyByBone);
            if (parent == null) continue;

            CharacterJoint joint = segments[i].bone.gameObject.AddComponent<CharacterJoint>();
            joint.connectedBody = parent;
            joint.lowTwistLimit = new SoftJointLimit { limit = -twistLimit };
            joint.highTwistLimit = new SoftJointLimit { limit = twistLimit };
            joint.swing1Limit = new SoftJointLimit { limit = swingLimit };
            joint.swing2Limit = new SoftJointLimit { limit = swingLimit };
            joint.enableProjection = true;
        }

        return true;
    }

    private static Rigidbody FindParentBody(Transform bone, Dictionary<Transform, Rigidbody> bodyByBone)
    {
        for (Transform t = bone.parent; t != null; t = t.parent)
        {
            if (bodyByBone.TryGetValue(t, out Rigidbody body))
            {
                return body;
            }
        }
        return null;
    }

    private static int DominantAxis(Vector3 v)
    {
        Vector3 abs = new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
        if (abs.x >= abs.y && abs.x >= abs.z) return 0;
        return abs.y >= abs.z ? 1 : 2;
    }
}
