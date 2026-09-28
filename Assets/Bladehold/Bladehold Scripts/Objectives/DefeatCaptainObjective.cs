using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     Survivors objective: Captain Assault (plan 15). Wave 5's fixed objective, never offered in a draft
///     (<c>draftable=false</c> in <c>WaveObjectives.csv</c>). On start it has <see cref="GameLoopManager" />
///     spawn the sector's clan captain at a tier set by sector threat, and completes when the captain
///     dies. It cannot fail: the gate falling (run over) is the only way to lose.
/// </summary>
public class DefeatCaptainObjective : MonoBehaviour, ISurvivorsObjective
{
    public const string Id = "defeat_captain";

    [Header("Objective Configuration")]
    [Tooltip("Must match the WaveObjectives.csv id; the CSV row supplies the title and rule text.")]
    [SerializeField] private string objectiveId = Id;
    [Tooltip("Fallback title when WaveObjectives.csv has no row for this objective.")]
    [SerializeField] private string title = "Captain Assault";
    [Tooltip("Fallback description when WaveObjectives.csv has no row for this objective.")]
    [SerializeField] private string description = "Kill the clan captain to win the sector.";

    [Header("Waypoint Icon Configuration")]
    [Tooltip("Optional custom waypoint icon for the captain.")]
    [SerializeField] private Sprite captainWaypointIcon;
    [SerializeField] private Color captainWaypointTint = new Color(1f, 0.25f, 0.25f, 1f);
    [SerializeField] private Vector3 captainWaypointOffset = new Vector3(0f, 3f, 0f);

    private GameObject spawnedCaptain;
    private Health captainHealth;
    private string captainName;
    private bool isActive;
    private bool isComplete;

    public string ObjectiveId => objectiveId;
    public string Title => ObjectiveCsv.Title(objectiveId, Loc.Get("wave.obj.captain_assault.title", title));
    public string Description => ObjectiveCsv.Rule(objectiveId, Loc.Get("wave.obj.captain_assault.rule", description));

    /// <summary>The captain spawned for this objective (null before start or if none could spawn).</summary>
    public GameObject CurrentCaptain => spawnedCaptain;

    /// <summary>The spawned captain's Health, or null.</summary>
    public Health CaptainHealth => captainHealth;

    /// <summary>The tier the captain was (or would be) spawned at for the current sector threat.</summary>
    public BannerDifficultyTier CurrentTier => TierForThreat(SectorThreat.Current);

    public string ProgressText
    {
        get
        {
            string name = CaptainDisplayName;
            if (isComplete)
            {
                return Loc.Get("wave.obj.captain_assault.done", "{0} has fallen!").Replace("{0}", name);
            }
            if (captainHealth == null)
            {
                return Loc.Get("wave.obj.captain_assault.progress_none", "Kill {0}").Replace("{0}", name);
            }
            return Loc.Get("wave.obj.captain_assault.progress", "{0}: {1}/{2} HP")
                .Replace("{0}", name)
                .Replace("{1}", Mathf.CeilToInt(Mathf.Max(0f, captainHealth.CurrentHealth)).ToString())
                .Replace("{2}", Mathf.CeilToInt(captainHealth.MaxHealth).ToString());
        }
    }

    public float ProgressNormalized
    {
        get
        {
            if (isComplete) return 1f;
            if (captainHealth == null || captainHealth.MaxHealth <= 0f) return 0f;
            return Mathf.Clamp01(1f - (captainHealth.CurrentHealth / captainHealth.MaxHealth));
        }
    }

    public bool IsComplete => isComplete;
    public bool IsFailed => false;
    public bool IsActive => isActive;

    public event Action<ISurvivorsObjective> OnProgressChanged;
    public event Action<ISurvivorsObjective> OnCompleted;
    public event Action<ISurvivorsObjective> OnFailed;

    private string CaptainDisplayName =>
        !string.IsNullOrEmpty(captainName) ? captainName : Loc.Get("wave.obj.captain_assault.generic", "the clan captain");

    /// <summary>Sector threat → captain tier: 1-2 Enraged, 3-5 Nightmare, 6+ Omega.</summary>
    public static BannerDifficultyTier TierForThreat(int threat)
    {
        if (threat >= 6) return BannerDifficultyTier.Omega;
        if (threat >= 3) return BannerDifficultyTier.Nightmare;
        return BannerDifficultyTier.Enraged;
    }

    /// <summary>The campaign node's captain name when a campaign is active, else null (GameLoopManager coin-flips).</summary>
    public static string PreferredCaptainName()
    {
        CampaignManager campaign = CampaignManager.Instance;
        if (campaign == null || !campaign.IsCampaignActive) return null;
        string name = campaign.CurrentNode?.captainName;
        return string.IsNullOrEmpty(name) ? null : name;
    }

    public void StartObjective()
    {
        UnsubscribeCaptain();
        spawnedCaptain = null;
        captainHealth = null;
        captainName = null;
        isActive = true;
        isComplete = false;

        SpawnCaptain();
        if (isActive) OnProgressChanged?.Invoke(this);
    }

    private void SpawnCaptain()
    {
        if (GameLoopManager.Instance == null)
        {
            Debug.LogError("[DefeatCaptainObjective] No GameLoopManager in the scene; can't spawn a captain. Completing so the wave can end.", this);
            CompleteObjective();
            return;
        }

        BannerDifficultyTier tier = TierForThreat(SectorThreat.Current);
        spawnedCaptain = GameLoopManager.Instance.SpawnCaptainForWave(tier, PreferredCaptainName());
        if (spawnedCaptain == null)
        {
            Debug.LogError("[DefeatCaptainObjective] GameLoopManager.SpawnCaptainForWave returned no captain. Completing so the wave can end.", this);
            CompleteObjective();
            return;
        }

        CaptainKombustaController kombusta = spawnedCaptain.GetComponent<CaptainKombustaController>();
        CaptainEnemyController fraglob = kombusta == null ? spawnedCaptain.GetComponent<CaptainEnemyController>() : null;
        captainName = kombusta != null ? kombusta.CaptainName : fraglob != null ? fraglob.CaptainName : null;

        captainHealth = spawnedCaptain.GetComponent<Health>();
        if (captainHealth == null) captainHealth = spawnedCaptain.GetComponentInChildren<Health>();
        if (captainHealth == null)
        {
            Debug.LogError($"[DefeatCaptainObjective] Captain '{spawnedCaptain.name}' has no Health. Completing so the wave can end.", this);
            CompleteObjective();
            return;
        }

        if (captainHealth.IsDead)
        {
            CompleteObjective();
            return;
        }

        captainHealth.OnHealthChanged += HandleCaptainHealthChanged;
        captainHealth.OnDied += HandleCaptainDied;
    }

    private void HandleCaptainHealthChanged()
    {
        if (!isActive || isComplete) return;
        OnProgressChanged?.Invoke(this);
    }

    private void HandleCaptainDied()
    {
        if (!isActive || isComplete) return;
        CompleteObjective();
    }

    private void CompleteObjective()
    {
        isComplete = true;
        isActive = false;
        OnProgressChanged?.Invoke(this);
        OnCompleted?.Invoke(this);
    }

    public void UpdateObjective(float deltaTime)
    {
        // Event-driven via the captain's Health events.
    }

    private void UnsubscribeCaptain()
    {
        if (captainHealth == null) return;
        captainHealth.OnHealthChanged -= HandleCaptainHealthChanged;
        captainHealth.OnDied -= HandleCaptainDied;
    }

    public void CleanupObjective()
    {
        isActive = false;
        UnsubscribeCaptain();

        // A captain still alive when the objective is swapped out (debug force-start) goes with it;
        // a dead one stays a corpse.
        if (spawnedCaptain != null && captainHealth != null && !captainHealth.IsDead)
        {
            Destroy(spawnedCaptain);
        }
    }

    private void OnDestroy()
    {
        UnsubscribeCaptain();
    }

    public Vector3? GetObjectiveTargetPosition(Vector3 searchFromPosition)
    {
        return null;
    }

    public IDamageable GetObjectiveDamageable(Vector3 searchFromPosition)
    {
        return null;
    }

    public void GetActiveWaypointTargets(List<ObjectiveWaypointTarget> results)
    {
        if (!isActive || isComplete || results == null) return;

        if (spawnedCaptain != null && captainHealth != null && !captainHealth.IsDead)
        {
            results.Add(new ObjectiveWaypointTarget(
                spawnedCaptain.transform,
                worldOffset: captainWaypointOffset,
                customIcon: captainWaypointIcon,
                tintColor: captainWaypointTint,
                label: !string.IsNullOrEmpty(captainName) ? captainName : Loc.Get("wave.obj.captain_assault.waypoint", "Captain")));
        }
    }
}
