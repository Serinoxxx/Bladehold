using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     Coordinates all TowerPlots across the battlefield scene.
///     Towers never leave their sector: on victory they're dismantled for a supply refund
///     (<see cref="DismantleAllForRefund" />), on player death they're simply cleared.
/// </summary>
public class TowerPlotManager : MonoBehaviour
{
    public static TowerPlotManager Instance { get; private set; }

    [SerializeField] private List<TowerPlot> plots = new List<TowerPlot>();

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
}
