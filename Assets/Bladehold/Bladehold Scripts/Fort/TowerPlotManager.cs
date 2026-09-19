using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     Coordinates all TowerPlots across the battlefield scene.
///     Handles rehydration of defenses across runs/rounds and clears defenses on player death.
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
        RestoreSavedDefenses();

        if (Player.Instance != null && Player.Instance.Health != null)
        {
            Player.Instance.Health.OnDied += HandlePlayerDied;
        }

        if (GameLoopManager.Instance != null)
        {
            GameLoopManager.Instance.OnWaveCleared += HandleWaveCleared;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;

        if (Player.Instance != null && Player.Instance.Health != null)
        {
            Player.Instance.Health.OnDied -= HandlePlayerDied;
        }

        if (GameLoopManager.Instance != null)
        {
            GameLoopManager.Instance.OnWaveCleared -= HandleWaveCleared;
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

    public void RestoreSavedDefenses()
    {
        if (RunSession.SavedDefenses == null || RunSession.SavedDefenses.Count == 0) return;

        foreach (var saved in RunSession.SavedDefenses)
        {
            if (saved == null) continue;
            TowerPlot targetPlot = GetPlotByIndex(saved.plotIndex);
            if (targetPlot != null && !targetPlot.IsOccupied)
            {
                targetPlot.BuildDefense(saved.defenseType, saved.level, saved.currentSupply, instant: true);
                Debug.Log($"[TowerPlotManager] Restored {saved.defenseType} (Lv {saved.level}, Supply {saved.currentSupply}) at Plot {saved.plotIndex}.");
            }
        }
    }

    public void SaveActiveDefenses()
    {
        RunSession.SavedDefenses.Clear();
        foreach (TowerPlot p in plots)
        {
            if (p != null && p.CurrentDefense != null)
            {
                RunSession.SavedDefenses.Add(new RunSession.SavedDefenseData
                {
                    plotIndex = p.PlotIndex,
                    defenseType = p.CurrentDefense.DefenseType,
                    level = p.CurrentDefense.Level,
                    currentSupply = p.CurrentDefense.CurrentSupply,
                    maxSupply = p.CurrentDefense.MaxSupply
                });
            }
        }
        Debug.Log($"[TowerPlotManager] Saved {RunSession.SavedDefenses.Count} active defenses to RunSession.");
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
        RunSession.SavedDefenses.Clear();
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

    private void HandleWaveCleared(int waveNumber, string message)
    {
        SaveActiveDefenses();
    }

    private void HandlePlayerDied()
    {
        ClearAllDefenses();
    }
}
