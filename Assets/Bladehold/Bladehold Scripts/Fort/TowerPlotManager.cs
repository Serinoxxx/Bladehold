using System.Collections.Generic;
using MoreMountains.Feedbacks;
using UnityEngine;

/// <summary>
///     Coordinates all TowerPlots across the battlefield scene.
///     Towers never leave their sector: on victory they're dismantled for a supply refund
///     (<see cref="DismantleAllForRefund" />), on player death they're simply cleared.
///
///     Also runs the two tower-wide Elemental draft effects: Tesla Spire
///     (<see cref="StatType.LightningTeslaSpireDamage" />, a lightning bolt every few seconds from the
///     built tower nearest an enemy) and Permafrost (<see cref="StatType.IcePermafrostUnlocked" />, a
///     chilling aura around every built tower). Both need at least one built tower.
/// </summary>
public class TowerPlotManager : MonoBehaviour
{
    public static TowerPlotManager Instance { get; private set; }

    [SerializeField] private List<TowerPlot> plots = new List<TowerPlot>();

    [Header("Elemental Tower Effects")]
    [SerializeField] private float teslaInterval = 5f;
    [SerializeField] private float teslaRange = 35f;
    [SerializeField] private float permafrostInterval = 1f;
    [SerializeField] private float permafrostRadius = 8f;
    [SerializeField] private float permafrostSlowAmount = 0.35f;
    [SerializeField] private float permafrostSlowDuration = 2f;
    [Tooltip("Optional: played at the struck enemy's position when Tesla Spire fires (VFX/SFX).")]
    [SerializeField] private MMF_Player teslaStrikeFeedback;

    private float nextTeslaTime;
    private float nextPermafrostTime;
    private readonly Collider[] overlapBuffer = new Collider[64];
    private readonly HashSet<Health> processedTargets = new HashSet<Health>();

    public IReadOnlyList<TowerPlot> Plots => plots;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        RefreshPlots();
    }

    private void Start()
    {
        RefreshPlots();

        if (Player.Instance != null && Player.Instance.Health != null)
        {
            Player.Instance.Health.OnDied += HandlePlayerDied;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;

        if (Player.Instance != null && Player.Instance.Health != null)
        {
            Player.Instance.Health.OnDied -= HandlePlayerDied;
        }
    }

    public void RegisterPlot(TowerPlot plot)
    {
        if (plot != null && !plots.Contains(plot))
        {
            plots.Add(plot);
        }
    }

    public void UnregisterPlot(TowerPlot plot)
    {
        if (plot != null)
        {
            plots.Remove(plot);
        }
    }

    public void RefreshPlots()
    {
        TowerPlot[] found = FindObjectsByType<TowerPlot>(FindObjectsSortMode.None);
        foreach (TowerPlot p in found)
        {
            if (p != null && !plots.Contains(p))
            {
                plots.Add(p);
            }
        }

        // Sort or assign deterministic indices if needed
        for (int i = 0; i < plots.Count; i++)
        {
            if (plots[i] != null)
            {
                plots[i].PlotIndex = i;
            }
        }
    }

    /// <summary>
    ///     Dismantles every placed tower, paying its <see cref="DefenseStructure.DismantleRefund" />
    ///     (remaining supply + upgrade spend) back into <see cref="RunSession.InRunSupply" />.
    ///     Called when the sector is won. Returns the total refunded.
    /// </summary>
    public int DismantleAllForRefund()
    {
        int total = 0;
        foreach (TowerPlot p in plots)
        {
            if (p != null && p.CurrentDefense != null)
            {
                total += p.CurrentDefense.DismantleRefund;
                p.ClearDefense();
            }
        }

        RunSession.AddInRunSupply(total);
        Debug.Log($"[TowerPlotManager] Towers dismantled: +{total} supply refunded.");
        return total;
    }

    public void ClearAllDefenses()
    {
        foreach (TowerPlot p in plots)
        {
            if (p != null)
            {
                p.ClearDefense();
            }
        }
        Debug.Log("[TowerPlotManager] All battlefield defenses cleared.");
    }

    public TowerPlot GetPlotByIndex(int index)
    {
        foreach (TowerPlot p in plots)
        {
            if (p != null && p.PlotIndex == index) return p;
        }
        return null;
    }

    private void HandlePlayerDied()
    {
        ClearAllDefenses();
    }

    private void Update()
    {
        PlayerStats stats = Player.Instance != null ? Player.Instance.Stats : null;
        if (stats == null || Player.Instance.Health == null || Player.Instance.Health.IsDead)
        {
            return;
        }

        if (Time.time >= nextTeslaTime && stats.GetValue(StatType.LightningTeslaSpireDamage) > 0f)
        {
            nextTeslaTime = Time.time + teslaInterval;
            FireTeslaSpire(stats);
        }

        if (Time.time >= nextPermafrostTime && stats.GetValue(StatType.IcePermafrostUnlocked) > 0f)
        {
            nextPermafrostTime = Time.time + permafrostInterval;
            processedTargets.Clear();
            foreach (TowerPlot p in plots)
            {
                if (p != null && p.CurrentDefense != null)
                {
                    ApplyPermafrostAura(p.CurrentDefense.transform.position);
                }
            }
        }
    }

    /// <summary>One lightning bolt from whichever built tower is nearest an enemy in range.</summary>
    private void FireTeslaSpire(PlayerStats stats)
    {
        Health target = null;
        Vector3 origin = Vector3.zero;
        float bestSqr = float.MaxValue;
        foreach (TowerPlot p in plots)
        {
            if (p == null || p.CurrentDefense == null) continue;
            Vector3 towerPos = p.CurrentDefense.transform.position;
            processedTargets.Clear();
            int count = Physics.OverlapSphereNonAlloc(towerPos, teslaRange, overlapBuffer);
            for (int i = 0; i < count; i++)
            {
                Health h = EnemyHealthFrom(overlapBuffer[i]);
                overlapBuffer[i] = null;
                if (h == null) continue;
                float sqr = (h.transform.position - towerPos).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    target = h;
                    origin = towerPos;
                }
            }
        }
        if (target == null) return;

        float damage = stats.GetValue(StatType.LightningTeslaSpireDamage);
        float allMult = stats.GetValue(StatType.AllDamageMultiplier);
        if (allMult > 0f) damage *= allMult;
        float pyreBonus = stats.GetValue(StatType.FireFortressPyreBonus);
        if (pyreBonus > 0f && target.GetComponent<EnemyStatusManager>()?.HasStatus("Fire") == true)
        {
            damage *= 1f + pyreBonus;
        }

        target.ReceiveDamage(new Damage
        {
            value = damage,
            type = DamageType.elemental,
            elementId = "Lightning",
            isPlayerDamage = true,
            sourcePosition = origin,
            source = Player.Instance.Damageable
        });
        EnemyStatusManager.GetOrAdd(target)?.ApplyStatus("Lightning");
        if (teslaStrikeFeedback != null)
        {
            teslaStrikeFeedback.PlayFeedbacks(target.transform.position);
        }
    }

    private void ApplyPermafrostAura(Vector3 center)
    {
        int count = Physics.OverlapSphereNonAlloc(center, permafrostRadius, overlapBuffer);
        for (int i = 0; i < count; i++)
        {
            Health h = EnemyHealthFrom(overlapBuffer[i]);
            overlapBuffer[i] = null;
            if (h == null) continue;
            SlowStatus.GetOrAdd(h)?.ApplySlow(permafrostSlowAmount, permafrostSlowDuration);
            EnemyStatusManager.GetOrAdd(h)?.ApplyStatus("Ice");
        }
    }

    /// <summary>The live enemy Health behind a collider, once per sweep (<see cref="processedTargets" />).</summary>
    private Health EnemyHealthFrom(Collider hit)
    {
        if (hit == null) return null;
        Health h = hit.GetComponentInParent<Health>();
        if (h == null || h.IsDead || !processedTargets.Add(h)) return null;
        if (h == Player.Instance.Health || h.CompareTag("Player")) return null;
        if (h.GetComponentInParent<DefenseStructure>() != null || h.GetComponentInParent<Gate>() != null) return null;
        return h;
    }
}
