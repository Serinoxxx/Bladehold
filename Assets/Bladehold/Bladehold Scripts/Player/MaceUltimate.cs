using System;
using System.Collections;
using System.Collections.Generic;
using MoreMountains.Feedbacks;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
///     2H Mace Signature Ultimate: Seismic Quake / Earthshaker Slam.
///     The player slams the earth with cataclysmic blunt force, detonating a 10m radial
///     ground shatter that deals heavy damage, launches enemies, and stuns them for 2.5s.
///     Leaves an empowering seismic aura for 6 seconds where every swing emits secondary tremors.
/// </summary>
public class MaceUltimate : MonoBehaviour, IUltimateHandler
{
    [Header("Config")]
    [Tooltip("Configuration ScriptableObject defining base duration and metadata.")]
    [SerializeField] private UltimateConfigSO config;

    public float BaseDuration => config != null && config.baseDuration > 0f ? config.baseDuration : (buffDuration > 0f ? buffDuration : 6f);

    [SerializeField] private float slamRadius = 10f;
    [SerializeField] private float baseSlamDamage = 90f;
    [SerializeField] private float stunDuration = 2.5f;
    [SerializeField] private float buffDuration = 6f;
    [Tooltip("MMF_Player played at the slam centre (seismic burst, impact sound, screenshake). Its particle feedback must use the Script position mode.")]
    [SerializeField] private MMF_Player slamFeedback;

    private Player player;
    private PlayerUltimateController controller;
    private bool isRunning;
    private float buffEndTime;

    private void Awake()
    {
        FindDependencies();
    }

    private void Start()
    {
        FindDependencies();
        if (slamFeedback == null) Debug.LogError("MaceUltimate: slamFeedback is not assigned.", this);
    }

    private void FindDependencies()
    {
        Transform rootTr = transform.root;
        if (player == null)
        {
            player = rootTr.GetComponentInChildren<Player>(true) ?? GetComponentInParent<Player>() ?? GetComponentInChildren<Player>(true) ?? Player.Instance;
        }
    }

    public void Activate(PlayerUltimateController controller)
    {
        this.controller = controller;
        FindDependencies();

        if (player == null)
        {
            controller?.EndUltimate();
            return;
        }

        StartCoroutine(ExecuteSlamRoutine());
    }

    private IEnumerator ExecuteSlamRoutine()
    {
        isRunning = true;
        float duration = player != null && player.Stats != null ? player.Stats.GetValue(StatType.UltimateDurationSeconds) : BaseDuration;
        if (duration <= 0f) duration = BaseDuration;
        buffEndTime = Time.time + duration;

        Vector3 center = player.transform.position;

        if (slamFeedback != null)
        {
            slamFeedback.PlayFeedbacks(center);
        }

        // Calculate damage
        float damageMult = player.Stats != null ? player.Stats.GetValue(StatType.AllDamageMultiplier) : 1f;
        float finalDamage = baseSlamDamage * (damageMult > 0f ? damageMult : 1f);

        // Radial slam damage, stun, and knockback
        Collider[] hits = Physics.OverlapSphere(center, slamRadius);
        HashSet<Health> affected = new HashSet<Health>();

        foreach (var col in hits)
        {
            Health h = col.GetComponentInParent<Health>();
            if (h != null && !h.IsDead && h.transform.root != player.transform.root)
            {
                if (affected.Add(h))
                {
                    Damage slamDmg = new Damage
                    {
                        value = finalDamage,
                        source = player != null ? player.Health : null,
                        sourcePosition = center,
                        unparryable = true
                    };
                    h.ReceiveDamage(slamDmg);

                    // 100% Stun
                    SlowStatus slow = SlowStatus.GetOrAdd(h);
                    if (slow != null)
                    {
                        slow.ApplySlow(1.0f, stunDuration);
                    }
                    if (h.TryGetComponent<NavMeshAgent>(out var agent) && agent.isOnNavMesh)
                    {
                        agent.velocity = Vector3.zero;
                    }

                    // Upward launching knockback
                    if (h.TryGetComponent<Rigidbody>(out var rb) && !rb.isKinematic)
                    {
                        Vector3 launchDir = (h.transform.position - center).normalized;
                        launchDir.y = 0.7f;
                        rb.AddForce(launchDir * 16f, ForceMode.Impulse);
                    }
                }
            }
        }

        Debug.Log($"[MaceUltimate] Seismic Quake detonated! Hit {affected.Count} targets.");

        // Wait out buff duration
        while (Time.time < buffEndTime)
        {
            yield return null;
        }

        isRunning = false;
        controller?.EndUltimate();
    }
}
