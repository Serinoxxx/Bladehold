using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     Controller-only aim assist for the hold-aim ranged weapons (bow, thrown axe, wand), driven by
///     <see cref="PlayerCameraPivot" /> from its look update. While a gamepad is the active device and a
///     ranged weapon is aimed, it picks the living enemy whose head sits closest to the crosshair inside the
///     settings window (with line of sight from the camera), then slows the look stick over that head and
///     gently pulls the view toward it at a capped speed. It never snaps.
///
///     The head is the enemy's <see cref="VulnerableSpot" /> (the collider that scores headshots), else the
///     Animator's head bone, else near the top of its collider.
/// </summary>
public class ControllerAimAssist : MonoBehaviour
{
    private const int MaxOverlaps = 256;

    [SerializeField] private AimAssistSO config;
    [Tooltip("Optional: the player's bow, for resolving the equipped aim weapon. Auto-found on the player.")]
    [SerializeField] private PlayerBow bow;

    private readonly Collider[] overlapBuffer = new Collider[MaxOverlaps];
    private readonly List<Health> candidates = new List<Health>();
    private readonly HashSet<Health> seen = new HashSet<Health>();
    private readonly Dictionary<Health, HeadAnchor> headCache = new Dictionary<Health, HeadAnchor>();
    private float nextScanTime;
    private Camera cam;
    private bool anyError = false;

    /// <summary>The enemy currently being assisted toward, or null.</summary>
    public Health CurrentTarget { get; private set; }

    /// <summary>World-space head point of <see cref="CurrentTarget" />.</summary>
    public Vector3 CurrentHeadPoint { get; private set; }

    private struct HeadAnchor
    {
        public Transform transform;
        public Vector3 localOffset;
        public Collider fallbackCollider;
    }

    private void Start()
    {
        if (config == null)
        {
            config = Resources.Load<AimAssistSO>("AimAssist");
        }
        if (config == null)
        {
            Debug.LogError("ControllerAimAssist has no AimAssistSO assigned; controller aim assist is off.");
            anyError = true;
        }
    }

    /// <summary>
    ///     Adjusts this frame's look deltas (degrees). <paramref name="strength" /> is 0–1, <paramref name="windowDegrees" />
    ///     the furthest a head can be from the crosshair. Call only for gamepad look.
    /// </summary>
    public void Apply(ref float yawDelta, ref float pitchDelta, float strength, float windowDegrees, bool stickHeld, float deltaTime)
    {
        CurrentTarget = null;
        if (anyError || strength <= 0f || windowDegrees <= 0f || deltaTime <= 0f || !IsAiming())
        {
            return;
        }
        if (cam == null || !cam.isActiveAndEnabled)
        {
            cam = Camera.main;
            if (cam == null) return;
        }

        if (Time.time >= nextScanTime)
        {
            nextScanTime = Time.time + config.scanInterval;
            RefreshCandidates();
        }

        if (!TryPickTarget(windowDegrees, out Health target, out Vector3 head, out _))
        {
            return;
        }
        CurrentTarget = target;
        CurrentHeadPoint = head;

        Transform camTransform = cam.transform;
        AdjustLook(config, camTransform.position, camTransform.rotation, head, strength, windowDegrees, stickHeld, deltaTime, ref yawDelta, ref pitchDelta);
    }

    /// <summary>
    ///     The assist math, pure so it can be benchmarked: slows the look deltas and pulls them toward
    ///     <paramref name="head" /> (capped degrees per second, fading to nothing at the window's edge, never
    ///     past the head). Returns false, leaving the deltas untouched, when the head is outside the window or
    ///     strength is 0.
    /// </summary>
    public static bool AdjustLook(AimAssistSO cfg, Vector3 camPosition, Quaternion camRotation, Vector3 head, float strength, float windowDegrees, bool stickHeld, float deltaTime, ref float yawDelta, ref float pitchDelta)
    {
        Vector3 dir = head - camPosition;
        float angle = Vector3.Angle(camRotation * Vector3.forward, dir);
        if (cfg == null || strength <= 0f || windowDegrees <= 0f || angle >= windowDegrees)
        {
            return false;
        }
        float falloff = 1f - angle / windowDegrees;

        float slow = 1f - cfg.maxSlowdown * strength * falloff;
        yawDelta *= slow;
        pitchDelta *= slow;

        float targetYaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
        float targetPitch = -Mathf.Atan2(dir.y, new Vector2(dir.x, dir.z).magnitude) * Mathf.Rad2Deg;
        Vector3 euler = camRotation.eulerAngles;
        Vector2 error = new Vector2(Mathf.DeltaAngle(euler.y, targetYaw), Mathf.DeltaAngle(euler.x, targetPitch));
        float errorSize = error.magnitude;
        if (errorSize < 0.01f)
        {
            return true;
        }

        float pull = cfg.maxPullDegreesPerSecond * strength * falloff * (stickHeld ? 1f + cfg.stickHeldPullBonus : 1f) * deltaTime;
        Vector2 step = error * (Mathf.Min(pull, errorSize) / errorSize);
        yawDelta += step.x;
        pitchDelta += step.y;
        return true;
    }

    private bool IsAiming()
    {
        if (bow == null && Player.Instance != null)
        {
            bow = Player.Instance.GetComponentInChildren<PlayerBow>(true);
        }
        IChargedAimWeapon weapon = AimWeaponResolver.Resolve(bow);
        return weapon != null && weapon.IsAiming;
    }

    private void RefreshCandidates()
    {
        candidates.Clear();
        seen.Clear();
        int count = Physics.OverlapSphereNonAlloc(cam.transform.position, config.range, overlapBuffer, config.enemyLayers, QueryTriggerInteraction.Collide);
        for (int i = 0; i < count; i++)
        {
            Health health = overlapBuffer[i].GetComponentInParent<Health>();
            if (health != null && !health.IsDead && seen.Add(health))
            {
                candidates.Add(health);
            }
        }

        if (headCache.Count > MaxOverlaps)
        {
            headCache.Clear();
        }
    }

    private bool TryPickTarget(float windowDegrees, out Health best, out Vector3 bestHead, out float bestAngle)
    {
        best = null;
        bestHead = default;
        bestAngle = windowDegrees;
        Transform camTransform = cam.transform;
        Vector3 camPos = camTransform.position;
        Vector3 forward = camTransform.forward;
        float rangeSqr = config.range * config.range;

        for (int i = 0; i < candidates.Count; i++)
        {
            Health health = candidates[i];
            if (health == null || health.IsDead) continue;

            Vector3 head = HeadPoint(health);
            Vector3 toHead = head - camPos;
            if (toHead.sqrMagnitude > rangeSqr) continue;

            float angle = Vector3.Angle(forward, toHead);
            if (angle >= bestAngle || !HasLineOfSight(camPos, head, health)) continue;

            best = health;
            bestHead = head;
            bestAngle = angle;
        }
        return best != null;
    }

    private bool HasLineOfSight(Vector3 from, Vector3 head, Health target)
    {
        Vector3 delta = head - from;
        float distance = delta.magnitude;
        if (distance < 0.01f) return true;
        if (!Physics.Raycast(from, delta / distance, out RaycastHit hit, distance - 0.2f, config.occluderLayers, QueryTriggerInteraction.Ignore))
        {
            return true;
        }
        return hit.collider.GetComponentInParent<Health>() == target;
    }

    private Vector3 HeadPoint(Health health)
    {
        if (!headCache.TryGetValue(health, out HeadAnchor anchor) || (anchor.transform == null && anchor.fallbackCollider == null))
        {
            anchor = ResolveHead(health);
            headCache[health] = anchor;
        }

        if (anchor.transform != null)
        {
            return anchor.transform.TransformPoint(anchor.localOffset);
        }
        if (anchor.fallbackCollider != null)
        {
            Bounds b = anchor.fallbackCollider.bounds;
            return new Vector3(b.center.x, b.min.y + b.size.y * config.fallbackHeadHeight, b.center.z);
        }
        return health.transform.position + Vector3.up * 1.6f;
    }

    private static HeadAnchor ResolveHead(Health health)
    {
        VulnerableSpot spot = health.GetComponentInChildren<VulnerableSpot>();
        if (spot != null)
        {
            SphereCollider sphere = spot.GetComponent<SphereCollider>();
            return new HeadAnchor { transform = spot.transform, localOffset = sphere != null ? sphere.center : Vector3.zero };
        }

        Animator animator = health.GetComponentInChildren<Animator>();
        if (animator != null && animator.isHuman)
        {
            Transform headBone = animator.GetBoneTransform(HumanBodyBones.Head);
            if (headBone != null)
            {
                return new HeadAnchor { transform = headBone, localOffset = Vector3.zero };
            }
        }

        Collider body = health.GetComponent<Collider>();
        if (body == null) body = health.GetComponentInChildren<Collider>();
        return new HeadAnchor { fallbackCollider = body };
    }
}
