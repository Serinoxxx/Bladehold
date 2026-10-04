using System.Collections;
using System.Collections.Generic;
using MoreMountains.Feedbacks;
using Synty.AnimationBaseLocomotion.Samples;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
///     2H Mace ultimate: Seismic Quake. Three beats:
///     1. <b>Leap</b> toward where the camera looks (untouchable in the air).
///     2. <b>Slam</b>: a shockwave ring spreads outward from the landing point, hitting, launching and
///        stunning enemies as it reaches them.
///     3. <b>Aftershocks</b>: for the rest of the duration every mace swing sends a small tremor that
///        damages and briefly stuns.
///     Front-loaded burst and crowd control, where the axe's Whirlwind is sustained damage.
/// </summary>
public class MaceUltimate : MonoBehaviour, IUltimateHandler
{
    [SerializeField] private SeismicQuakeConfigSO config;

    [Header("Feedback")]
    [Tooltip("Played at take-off (whoosh, dust kick).")]
    [SerializeField] private MMF_Player leapFeedback;
    [Tooltip("MMF_Player played at the slam centre (seismic burst, impact sound, screenshake). Its particle feedback must use the Script position mode.")]
    [SerializeField] private MMF_Player slamFeedback;
    [Tooltip("Played where each aftershock lands (small quake burst, rumble).")]
    [SerializeField] private MMF_Player aftershockFeedback;
    [Tooltip("Optional: played on the player when the ultimate ends. Leave empty for silence.")]
    [SerializeField] private MMF_Player endFeedback;
    [Tooltip("Optional: looping seismic aura on the player while aftershocks are live.")]
    [SerializeField] private GameObject auraVisual;
    [Tooltip("Optional: stun indicator (circling stars) parented over each stunned enemy's head for the stun.")]
    [SerializeField] private GameObject stunVisualPrefab;

    [Header("Animation")]
    [Tooltip("Optional animator trigger fired on landing for a slam pose. Leave empty to keep the landing animation.")]
    [SerializeField] private string slamAnimTrigger = "";

    public float BaseDuration => config != null && config.baseDuration > 0f ? config.baseDuration : 6f;

    private static readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");
    private static readonly int IsJumpingHash = Animator.StringToHash("IsJumping");
    private static readonly int FallingDurationHash = Animator.StringToHash("FallingDuration");

    private readonly Collider[] overlapBuffer = new Collider[128];
    private readonly HashSet<Health> ringHits = new HashSet<Health>();

    private Player player;
    private CharacterController characterController;
    private SamplePlayerAnimationController locomotion;
    private Animator animator;
    private PlayerUltimateController controller;
    private DamageTrigger hookedTrigger;
    private Coroutine routine;
    private bool isRunning;
    private bool isAirborne;
    private bool aftershocksLive;
    private bool anyError;
    private bool validated;

    private void Awake()
    {
        player = transform.root.GetComponentInChildren<Player>(true);
        if (player != null && config != null && player.Stats != null)
        {
            player.Stats.SetBase(StatType.UltimateMaceAftershockDamage, config.aftershockDamage);
        }
    }

    private void Start()
    {
        Validate();
    }

    /// <summary>Resolves and checks dependencies once. Runs from Start, or from Activate when the
    /// handler is enabled and fired in the same frame (before its Start).</summary>
    private void Validate()
    {
        if (validated) return;
        validated = true;

        if (player == null) { Debug.LogError("MaceUltimate: no Player under the root.", this); anyError = true; }
        else
        {
            characterController = player.GetComponent<CharacterController>();
            locomotion = player.GetComponent<SamplePlayerAnimationController>();
            animator = player.GetComponent<Animator>();
            if (player.Stats == null) { Debug.LogError("MaceUltimate: Player has no PlayerStats.", this); anyError = true; }
            if (characterController == null) { Debug.LogError("MaceUltimate: Player has no CharacterController.", this); anyError = true; }
            if (locomotion == null) { Debug.LogError("MaceUltimate: Player has no SamplePlayerAnimationController.", this); anyError = true; }
        }
        if (config == null) { Debug.LogError("MaceUltimate: config is not assigned.", this); anyError = true; }
        if (leapFeedback == null) Debug.LogError("MaceUltimate: leapFeedback is not assigned.", this);
        if (slamFeedback == null) Debug.LogError("MaceUltimate: slamFeedback is not assigned.", this);
        if (aftershockFeedback == null) Debug.LogError("MaceUltimate: aftershockFeedback is not assigned.", this);
        if (auraVisual != null) auraVisual.SetActive(false);
    }

    public void Activate(PlayerUltimateController controller)
    {
        this.controller = controller;
        Validate();
        if (anyError)
        {
            controller?.EndUltimate();
            return;
        }

        isRunning = true;
        routine = StartCoroutine(QuakeRoutine());
    }

    private IEnumerator QuakeRoutine()
    {
        float duration = player.Stats.GetValue(StatType.UltimateDurationSeconds);
        if (duration <= 0f) duration = BaseDuration;
        float endTime = Time.time + duration;

        yield return Leap();

        Vector3 center = player.transform.position;
        if (slamFeedback != null) slamFeedback.PlayFeedbacks(center);
        if (animator != null && !string.IsNullOrEmpty(slamAnimTrigger)) animator.SetTrigger(slamAnimTrigger);

        // Aftershocks start with the slam so a swing during the ring still tremors.
        aftershocksLive = true;
        HookTrigger();
        if (PlayerWeaponManager.Instance != null) PlayerWeaponManager.Instance.OnMeleeChanged += HandleMeleeChanged;
        if (auraVisual != null) auraVisual.SetActive(true);

        yield return SpreadRing(center);

        while (Time.time < endTime && !player.Health.IsDead)
        {
            yield return null;
        }

        routine = null;
        End();
    }

    private IEnumerator Leap()
    {
        Vector3 direction = Camera.main != null ? Camera.main.transform.forward : player.transform.forward;
        direction.y = 0f;
        direction = direction.sqrMagnitude > 0.001f ? direction.normalized : player.transform.forward;
        player.transform.rotation = Quaternion.LookRotation(direction);

        if (leapFeedback != null) leapFeedback.PlayFeedbacks(player.transform.position);

        // The Synty controller would fight the arc with its own gravity, so it sits out the leap
        // (re-enabling resets its velocity, so the landing is clean).
        locomotion.enabled = false;
        isAirborne = true;
        player.Health.TryBlockDamage += BlockWhileAirborne;
        if (animator != null)
        {
            animator.SetBool(IsGroundedHash, false);
            animator.SetBool(IsJumpingHash, true);
        }

        float leapDuration = Mathf.Max(0.05f, config.leapDuration);
        float elapsed = 0f;
        float lastHeight = 0f;
        while (elapsed < leapDuration && !player.Health.IsDead)
        {
            float dt = Mathf.Min(Time.deltaTime, leapDuration - elapsed);
            elapsed += dt;
            float t = elapsed / leapDuration;
            float height = 4f * config.leapHeight * t * (1f - t);

            Vector3 delta = direction * (config.leapDistance * dt / leapDuration) + Vector3.up * (height - lastHeight);
            lastHeight = height;
            characterController.Move(delta);
            if (animator != null) animator.SetFloat(FallingDurationHash, elapsed);
            yield return null;
        }

        // Settle onto the ground before re-enabling locomotion.
        characterController.Move(Vector3.down * (lastHeight + 0.5f));
        EndLeap();
    }

    private void EndLeap()
    {
        if (!isAirborne) return;
        isAirborne = false;
        if (player != null && player.Health != null) player.Health.TryBlockDamage -= BlockWhileAirborne;
        if (animator != null)
        {
            animator.SetBool(IsJumpingHash, false);
            animator.SetBool(IsGroundedHash, true);
        }
        if (locomotion != null) locomotion.enabled = true;
    }

    private bool BlockWhileAirborne(Damage damage) => isAirborne;

    /// <summary>Grows the shockwave ring from the centre to slamRadius, hitting each enemy as the ring reaches it.</summary>
    private IEnumerator SpreadRing(Vector3 center)
    {
        ringHits.Clear();
        float allDamage = player.Stats.GetValue(StatType.AllDamageMultiplier);
        float damage = config.slamDamage * (allDamage > 0f ? allDamage : 1f);
        float expand = Mathf.Max(0.01f, config.ringExpandSeconds);

        float elapsed = 0f;
        while (true)
        {
            elapsed += Time.deltaTime;
            float radius = config.slamRadius * Mathf.Clamp01(elapsed / expand);
            HitInRadius(center, radius, damage, config.slamStunSeconds, config.launchForce, config.launchUpward, ringHits);
            if (elapsed >= expand) yield break;
            yield return null;
        }
    }

    private void HitInRadius(Vector3 center, float radius, float damage, float stunSeconds, float force, float upward, HashSet<Health> alreadyHit)
    {
        int count = Physics.OverlapSphereNonAlloc(center, radius, overlapBuffer, ~0, QueryTriggerInteraction.Collide);
        for (int i = 0; i < count; i++)
        {
            Collider col = overlapBuffer[i];
            if (col == null || col.transform.root == player.transform.root) continue;

            Health h = col.GetComponentInParent<Health>();
            if (h == null || h.IsDead || h == player.Health || !alreadyHit.Add(h)) continue;

            Vector3 away = h.transform.position - center;
            away.y = 0f;
            away = away.sqrMagnitude > 0.001f ? away.normalized : player.transform.forward;

            h.ReceiveDamage(new Damage
            {
                value = damage,
                type = DamageType.blunt,
                source = player.Damageable,
                sourcePosition = center,
                direction = away,
                hitCollider = col,
                unparryable = true,
                isPlayerDamage = true,
                elementId = RunSession.GetActiveElement("SLOT_ULTIMATE"),
            });
            if (h.IsDead) continue;

            Stun(h, stunSeconds);
            if (force > 0f && h.TryGetComponent<Rigidbody>(out var rb) && !rb.isKinematic)
            {
                rb.AddForce((away + Vector3.up * upward).normalized * force, ForceMode.Impulse);
            }
        }
    }

    private void Stun(Health h, float seconds)
    {
        if (seconds <= 0f) return;
        SlowStatus slow = SlowStatus.GetOrAdd(h);
        if (slow != null) slow.ApplySlow(1f, seconds);
        if (h.TryGetComponent<NavMeshAgent>(out var agent) && agent.isOnNavMesh) agent.velocity = Vector3.zero;

        if (stunVisualPrefab != null)
        {
            Collider c = h.GetComponentInChildren<Collider>();
            float top = c != null ? c.bounds.max.y - h.transform.position.y : 2f;
            GameObject stars = Instantiate(stunVisualPrefab, h.transform);
            stars.transform.localPosition = Vector3.up * (top + 0.3f);
            Destroy(stars, seconds);
        }
    }

    private void HandleMeleeChanged(WeaponDefinitionSO def)
    {
        UnhookTrigger();
        HookTrigger();
    }

    private void HookTrigger()
    {
        if (PlayerWeaponManager.Instance == null) return;
        hookedTrigger = PlayerWeaponManager.Instance.ActiveMeleeTrigger;
        if (hookedTrigger != null) hookedTrigger.OnActivated += HandleSwing;
    }

    private void UnhookTrigger()
    {
        if (hookedTrigger != null) hookedTrigger.OnActivated -= HandleSwing;
        hookedTrigger = null;
    }

    private void HandleSwing()
    {
        if (aftershocksLive) StartCoroutine(Aftershock());
    }

    private IEnumerator Aftershock()
    {
        yield return new WaitForSeconds(config.aftershockDelay);
        if (!aftershocksLive) yield break;

        Vector3 forward = player.transform.forward;
        forward.y = 0f;
        Vector3 point = player.transform.position + forward.normalized * config.aftershockForwardOffset;
        if (aftershockFeedback != null) aftershockFeedback.PlayFeedbacks(point);

        float allDamage = player.Stats.GetValue(StatType.AllDamageMultiplier);
        float damage = player.Stats.GetValue(StatType.UltimateMaceAftershockDamage) * (allDamage > 0f ? allDamage : 1f);
        HitInRadius(point, config.aftershockRadius, damage, config.aftershockStunSeconds, config.launchForce * 0.4f, 0.3f, new HashSet<Health>());
    }

    private void End()
    {
        if (!isRunning) return;
        isRunning = false;

        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }
        EndLeap();
        aftershocksLive = false;
        UnhookTrigger();
        if (PlayerWeaponManager.Instance != null) PlayerWeaponManager.Instance.OnMeleeChanged -= HandleMeleeChanged;
        if (auraVisual != null) auraVisual.SetActive(false);
        if (endFeedback != null && player != null) endFeedback.PlayFeedbacks(player.transform.position);

        controller?.EndUltimate();
    }

    private void OnDisable()
    {
        End();
    }
}
