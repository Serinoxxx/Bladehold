using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     Throwing Axe Ranged Ultimate: Axe Vortex / Bloodstorm Cyclone.
///     Spawns an orbital ring of 4 spinning throwing axes revolving around the player,
///     slashing enemies in proximity. Grants rapid-fire 3-way fan throws with zero wind-up.
/// </summary>
public class ThrowingAxeUltimate : MonoBehaviour, IUltimateHandler
{
    [Header("Config")]
    [Tooltip("Configuration ScriptableObject defining base duration and metadata.")]
    [SerializeField] private UltimateConfigSO config;

    public float BaseDuration => config != null && config.baseDuration > 0f ? config.baseDuration : 7f;

    [Header("Vortex Properties")]
    [SerializeField] private int orbitBladeCount = 4;
    [SerializeField] private float orbitRadius = 2.5f;
    [SerializeField] private float orbitSpeed = 540f; // degrees per second
    [SerializeField] private float damageTickInterval = 0.25f;
    [SerializeField] private float bladeDamage = 25f;
    [Tooltip("Authored visual for one orbiting blade (no collider). Positioned and spun by code.")]
    [SerializeField] private GameObject orbitBladePrefab;

    private Player player;
    private PlayerThrownAxe playerThrownAxe;
    private PlayerUltimateController controller;
    private readonly List<Transform> orbitalBlades = new List<Transform>();
    private readonly Dictionary<IDamageable, float> lastHitTimes = new Dictionary<IDamageable, float>();

    private float ultimateEndTime;
    private float currentOrbitAngle = 0f;
    private bool isRunning = false;

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
        if (player == null) player = rootTr.GetComponentInChildren<Player>(true) ?? GetComponentInParent<Player>() ?? GetComponentInChildren<Player>(true) ?? Player.Instance;
        if (playerThrownAxe == null) playerThrownAxe = rootTr.GetComponentInChildren<PlayerThrownAxe>(true) ?? GetComponentInParent<PlayerThrownAxe>() ?? GetComponentInChildren<PlayerThrownAxe>(true);
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

        float duration = player.Stats != null ? player.Stats.GetValue(StatType.UltimateDurationSeconds) : BaseDuration;
        if (duration <= 0f) duration = BaseDuration;

        ultimateEndTime = Time.time + duration;
        isRunning = true;
        currentOrbitAngle = 0f;
        lastHitTimes.Clear();

        SpawnOrbitalBlades();

        if (playerThrownAxe != null)
        {
            playerThrownAxe.IsVortexUltimateActive = true;
        }

        Debug.Log("[ThrowingAxeUltimate] Axe Vortex activated!");
    }

    private void SpawnOrbitalBlades()
    {
        ClearOrbitalBlades();

        if (orbitBladePrefab == null)
        {
            // The vortex still damages; it just has nothing to show.
            Debug.LogError("[ThrowingAxeUltimate] orbitBladePrefab is not assigned (Player.prefab → ThrowingAxeUltimate).", this);
        }

        for (int i = 0; i < orbitBladeCount; i++)
        {
            GameObject blade = orbitBladePrefab != null ? Instantiate(orbitBladePrefab) : new GameObject($"VortexBlade_{i}");
            orbitalBlades.Add(blade.transform);
        }

        UpdateOrbitalBladesPosition();
    }

    private void Update()
    {
        if (!isRunning) return;

        if (Time.time >= ultimateEndTime)
        {
            End();
            return;
        }

        currentOrbitAngle = (currentOrbitAngle + orbitSpeed * Time.deltaTime) % 360f;
        UpdateOrbitalBladesPosition();
        CheckOrbitalHits();
    }

    private void UpdateOrbitalBladesPosition()
    {
        if (player == null) return;
        Vector3 center = player.transform.position + Vector3.up * 1.1f;

        float angleStep = 360f / Mathf.Max(1, orbitalBlades.Count);
        for (int i = 0; i < orbitalBlades.Count; i++)
        {
            if (orbitalBlades[i] == null) continue;

            float angle = (currentOrbitAngle + i * angleStep) * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * orbitRadius;
            orbitalBlades[i].position = center + offset;
            orbitalBlades[i].rotation = Quaternion.Euler(0f, -currentOrbitAngle * 2f, 90f);
        }
    }

    private void CheckOrbitalHits()
    {
        if (player == null) return;

        float allDmgMult = player.Stats != null ? player.Stats.GetValue(StatType.AllDamageMultiplier) : 1f;
        if (allDmgMult <= 0f) allDmgMult = 1f;

        float finalDmg = bladeDamage * allDmgMult;

        for (int i = 0; i < orbitalBlades.Count; i++)
        {
            if (orbitalBlades[i] == null) continue;

            Collider[] hits = Physics.OverlapSphere(orbitalBlades[i].position, 0.8f);
            foreach (Collider hit in hits)
            {
                IDamageable damageable = hit.GetComponentInParent<IDamageable>();
                if (damageable == null || damageable == player.Damageable || (player.Health != null && damageable == (IDamageable)player.Health))
                {
                    continue;
                }

                if (lastHitTimes.TryGetValue(damageable, out float lastHit) && Time.time - lastHit < damageTickInterval)
                {
                    continue;
                }

                lastHitTimes[damageable] = Time.time;

                Damage d = new Damage
                {
                    value = finalDmg,
                    isCritical = false,
                    knockbackForce = 5f,
                    sourcePosition = orbitalBlades[i].position,
                    source = player.Damageable,
                    isPlayerDamage = true,
                    elementId = RunSession.ElementalSlots.GetValueOrDefault("SLOT_ULTIMATE", "")
                };

                damageable.ReceiveDamage(d);
            }
        }
    }

    private void ClearOrbitalBlades()
    {
        for (int i = 0; i < orbitalBlades.Count; i++)
        {
            if (orbitalBlades[i] != null)
            {
                Destroy(orbitalBlades[i].gameObject);
            }
        }
        orbitalBlades.Clear();
    }

    private void End()
    {
        if (!isRunning) return;
        isRunning = false;

        ClearOrbitalBlades();

        if (playerThrownAxe != null)
        {
            playerThrownAxe.IsVortexUltimateActive = false;
        }

        controller?.EndUltimate();
        Debug.Log("[ThrowingAxeUltimate] Axe Vortex ended.");
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
