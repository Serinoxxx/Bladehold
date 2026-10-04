using System.Collections.Generic;
using MoreMountains.Feedbacks;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
///     Blizzard (alpine / ice biome): a whiteout grade and heavy snow, then periodic gale gusts. Each gust is
///     announced by a rising howl and wind streaks pointing the way it'll blow, then shoves the player
///     (through <see cref="PlayerShoveReceiver" />; you can walk against it) and every nearby enemy along the
///     NavMesh, chilling the enemies. The player gets <see cref="StatType.ChilledDamageBonus" /> while it lasts,
///     so the gusts set up the damage bonus. Gusts can push packs to a ravine edge or into a tower's line.
/// </summary>
public class BlizzardEvent : WorldEvent
{
    private enum GustPhase { Calm, Warning, Gusting }

    [SerializeField] private BlizzardEventSO config;

    [Header("Gust visual (state, not feedback)")]
    [Tooltip("Wind-streak particles played during the warning and gust. Rotated so +Z points downwind; follows the player.")]
    [SerializeField] private Transform gustVisualRoot;

    [Header("Feedback")]
    [Tooltip("The rising howl before a gust (2D).")]
    [SerializeField] private MMF_Player gustWarningFeedback;
    [Tooltip("The gust hitting: a whoosh and a light shake (2D).")]
    [SerializeField] private MMF_Player gustFeedback;

    private readonly List<(Health health, NavMeshAgent agent)> gustAgents = new List<(Health, NavMeshAgent)>();
    private ParticleSystem[] gustSystems = new ParticleSystem[0];
    private GustPhase phase;
    private float phaseTimer;
    private Vector3 windDirection = Vector3.forward;
    private PlayerShoveReceiver playerShove;

    public override WorldEventSO Config => config;

    protected override void Awake()
    {
        base.Awake();
        if (gustVisualRoot != null)
        {
            gustSystems = gustVisualRoot.GetComponentsInChildren<ParticleSystem>(true);
            SetGustVisual(false);
        }
    }

    protected override void ValidateEvent()
    {
        if (gustVisualRoot == null) Debug.LogError($"[BlizzardEvent] {name}: gustVisualRoot is not assigned; gusts will hit without a visual cue.", this);
        if (gustWarningFeedback == null) Debug.LogError($"[BlizzardEvent] {name}: gustWarningFeedback is not assigned.", this);
        if (gustFeedback == null) Debug.LogError($"[BlizzardEvent] {name}: gustFeedback is not assigned.", this);
    }

    protected override void OnEventBegan()
    {
        float angle = Random.Range(0f, 360f);
        windDirection = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
        playerShove = Player.Instance != null ? Player.Instance.GetComponent<PlayerShoveReceiver>() : null;
        if (playerShove == null) Debug.LogError("[BlizzardEvent] The player has no PlayerShoveReceiver; gusts won't move them.", this);
    }

    protected override void OnHazardsStarted()
    {
        EnterCalm(1.5f);
    }

    protected override void TickHazards(float deltaTime)
    {
        phaseTimer -= deltaTime;
        if (gustVisualRoot != null && Player.Instance != null)
        {
            gustVisualRoot.position = Player.Instance.transform.position;
        }

        switch (phase)
        {
            case GustPhase.Calm:
                if (phaseTimer <= 0f) EnterWarning();
                break;
            case GustPhase.Warning:
                if (phaseTimer <= 0f) EnterGust();
                break;
            case GustPhase.Gusting:
                Push(deltaTime);
                if (phaseTimer <= 0f) EnterCalm(Random.Range(config.gustInterval.x, config.gustInterval.y));
                break;
        }
    }

    private void EnterCalm(float seconds)
    {
        phase = GustPhase.Calm;
        phaseTimer = seconds;
        SetGustVisual(false);
        gustAgents.Clear();
    }

    private void EnterWarning()
    {
        phase = GustPhase.Warning;
        phaseTimer = config.gustWarningSeconds;
        windDirection = Quaternion.Euler(0f, Random.Range(-config.directionWobbleDegrees, config.directionWobbleDegrees), 0f) * windDirection;
        if (gustVisualRoot != null) gustVisualRoot.rotation = Quaternion.LookRotation(windDirection, Vector3.up);
        SetGustVisual(true);
        if (gustWarningFeedback != null) gustWarningFeedback.PlayFeedbacks();
    }

    private void EnterGust()
    {
        phase = GustPhase.Gusting;
        phaseTimer = config.gustSeconds;
        if (gustFeedback != null) gustFeedback.PlayFeedbacks();

        gustAgents.Clear();
        Vector3 player = WorldEventHazards.PlayerPosition(transform.position);
        float radiusSqr = config.gustRadius * config.gustRadius;
        foreach (Health enemy in WorldEventHazards.AliveEnemies())
        {
            if (enemy == null || (enemy.transform.position - player).sqrMagnitude > radiusSqr) continue;
            if (config.gustsChillEnemies)
            {
                EnemyStatusManager status = EnemyStatusManager.GetOrAdd(enemy);
                if (status != null) status.ApplyStatus("Ice");
            }
            NavMeshAgent agent = enemy.GetComponent<NavMeshAgent>();
            if (agent != null) gustAgents.Add((enemy, agent));
        }
    }

    private void Push(float deltaTime)
    {
        // Ease the push in and out over the gust so it reads as a gust, not a conveyor belt.
        float t = 1f - Mathf.Clamp01(phaseTimer / Mathf.Max(0.01f, config.gustSeconds));
        float strength = Mathf.Sin(t * Mathf.PI);

        if (playerShove != null)
        {
            playerShove.Shove(windDirection * (config.playerShoveSpeed * strength * deltaTime));
        }

        Vector3 enemyStep = windDirection * (config.enemyShoveSpeed * strength * deltaTime);
        for (int i = gustAgents.Count - 1; i >= 0; i--)
        {
            (Health health, NavMeshAgent agent) = gustAgents[i];
            if (health == null || health.IsDead || agent == null || !agent.enabled || !agent.isOnNavMesh)
            {
                gustAgents.RemoveAt(i);
                continue;
            }
            agent.Move(enemyStep);
        }
    }

    private void SetGustVisual(bool on)
    {
        foreach (ParticleSystem ps in gustSystems)
        {
            if (ps == null) continue;
            if (on) ps.Play(true);
            else ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    protected override void OnEventEnded()
    {
        EnterCalm(0f);
    }
}
