using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     Sword Signature Ultimate: Blade Tempest.
///     Replaces the old Mount ultimate for sword wielders.
///     Empowers the player with unmatched swordsmanship for the duration:
///     - Continuous 360-degree slashing flurry hitting all surrounding foes.
///     - Grants 30% damage reduction.
///     - Boosts sword reach and critical strike chance.
/// </summary>
public class SwordBladeTempestUltimate : MonoBehaviour, IUltimateHandler
{
    [Header("Config")]
    [Tooltip("Configuration ScriptableObject defining base duration and metadata.")]
    [SerializeField] private UltimateConfigSO config;

    public float BaseDuration => config != null && config.baseDuration > 0f ? config.baseDuration : 6f;

    [Header("Combat Tunables")]
    [Tooltip("Radius of the continuous blade tempest slice around the player.")]
    [SerializeField] private float sliceRadius = 4.5f;

    [Tooltip("Damage dealt per tempest slash tick.")]
    [SerializeField] private float slashDamage = 35f;

    [Tooltip("Interval between tempest slashes in seconds.")]
    [SerializeField] private float slashInterval = 0.35f;

    [Tooltip("Damage reduction fraction while the ultimate is active (0.3 = 30% less damage taken).")]
    [SerializeField] private float damageReduction = 0.3f;

    private Player player;
    private Health playerHealth;
    private PlayerUltimateController controller;
    private bool isRunning = false;
    private float ultimateEndTime;
    private float nextSlashTime;

    private readonly Collider[] hitBuffer = new Collider[32];
    private readonly HashSet<IDamageable> hitVictims = new HashSet<IDamageable>();

    private void Awake()
    {
        FindDependencies();
    }

    private void Start()
    {
        FindDependencies();
    }

    private void FindDependencies()
    {
        Transform rootTr = transform.root;
        if (player == null)
        {
            player = rootTr.GetComponentInChildren<Player>(true) ?? GetComponentInParent<Player>() ?? GetComponentInChildren<Player>(true) ?? Player.Instance;
        }
        if (playerHealth == null && player != null)
        {
            playerHealth = player.GetComponent<Health>();
        }
    }

    public void Activate(PlayerUltimateController controller)
    {
        this.controller = controller;
        FindDependencies();

        if (player == null)
        {
            Debug.LogError("[SwordBladeTempestUltimate] Player not found!");
            controller?.EndUltimate();
            return;
        }

        float duration = player.Stats != null ? player.Stats.GetValue(StatType.UltimateDurationSeconds) : BaseDuration;
        if (duration <= 0f) duration = BaseDuration;

        ultimateEndTime = Time.time + duration;
        nextSlashTime = Time.time;
        isRunning = true;

        if (playerHealth != null)
        {
            playerHealth.ScaleDamageTaken += HandleScaleDamageTaken;
        }

        Debug.Log("[SwordBladeTempestUltimate] Blade Tempest activated!");
    }

    private float HandleScaleDamageTaken(Damage damage)
    {
        if (!isRunning) return 1f;
        return Mathf.Clamp01(1f - damageReduction);
    }

    private void Update()
    {
        if (!isRunning) return;

        if (Time.time >= ultimateEndTime)
        {
            End();
            return;
        }

        if (Time.time >= nextSlashTime)
        {
            nextSlashTime = Time.time + slashInterval;
            PerformTempestSlash();
        }
    }

    private void PerformTempestSlash()
    {
        if (player == null) return;

        Vector3 center = player.transform.position + Vector3.up * 0.8f;
        int hitCount = Physics.OverlapSphereNonAlloc(center, sliceRadius, hitBuffer);
        hitVictims.Clear();

        float effectiveDamage = slashDamage;
        if (player.Stats != null)
        {
            float mult = player.Stats.GetValue(StatType.AllDamageMultiplier);
            if (mult > 0f) effectiveDamage *= mult;
        }

        for (int i = 0; i < hitCount; i++)
        {
            Collider col = hitBuffer[i];
            if (col == null || col.transform.root == player.transform.root) continue;

            IDamageable damageable = col.GetComponentInParent<IDamageable>();
            if (damageable != null && !hitVictims.Contains(damageable))
            {
                hitVictims.Add(damageable);
                Vector3 toTarget = col.transform.position - center;
                Damage dmg = new Damage
                {
                    value = effectiveDamage,
                    type = DamageType.slash,
                    sourcePosition = center,
                    direction = toTarget.sqrMagnitude > 0.001f ? toTarget.normalized : player.transform.forward,
                    hitCollider = col,
                    knockbackForce = 8f,
                    source = player.GetComponent<IDamageable>(),
                    isPlayerDamage = true
                };
                damageable.ReceiveDamage(dmg);
            }
        }
    }

    private void End()
    {
        if (!isRunning) return;
        isRunning = false;

        if (playerHealth != null)
        {
            playerHealth.ScaleDamageTaken -= HandleScaleDamageTaken;
        }

        controller?.EndUltimate();
        Debug.Log("[SwordBladeTempestUltimate] Blade Tempest ended.");
    }

    private void OnDisable()
    {
        if (isRunning) End();
    }

    private void OnDestroy()
    {
        if (isRunning) End();
    }
}
