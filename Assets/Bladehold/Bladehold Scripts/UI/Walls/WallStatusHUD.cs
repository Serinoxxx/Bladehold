using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     The row of wall icons under the gate HP bar (plan 17): one <see cref="WallStatusIcon" /> per wall
///     plot in the scene, spawned from a single prefab, so you can watch every bridge's wall at a glance
///     (fill = HP, flashing = under attack, cross = breached, gap = door open). Hidden in scenes with no
///     wall plots.
/// </summary>
public class WallStatusHUD : MonoBehaviour
{
    [SerializeField] private WallStatusIcon iconPrefab;
    [SerializeField] private Transform iconParent;
    [Tooltip("Hidden when the scene has no wall plots.")]
    [SerializeField] private GameObject root;

    private readonly Dictionary<WallPlot, WallStatusIcon> icons = new Dictionary<WallPlot, WallStatusIcon>();
    private float nextRefresh;
    private bool anyError;

    private void Start()
    {
        if (iconPrefab == null) { Debug.LogError($"[WallStatusHUD] {name}: iconPrefab is not assigned.", this); anyError = true; }
        if (iconParent == null) iconParent = transform;
        if (root == null) root = gameObject;
        Rebuild();
    }

    private void Update()
    {
        // Plots register in their own Start; pick up late ones without a per-frame cost.
        if (anyError || Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + 1f;
        Rebuild();
    }

    private void Rebuild()
    {
        if (anyError) return;
        TowerPlotManager manager = TowerPlotManager.Instance;
        IReadOnlyList<WallPlot> plots = manager != null ? manager.WallPlots : null;
        int count = plots != null ? plots.Count : 0;

        if (plots != null)
        {
            foreach (WallPlot plot in plots)
            {
                if (plot == null || icons.ContainsKey(plot)) continue;
                WallStatusIcon icon = Instantiate(iconPrefab, iconParent);
                icon.Bind(plot);
                icons.Add(plot, icon);
            }
        }

        if (root != null && root != gameObject) root.SetActive(count > 0);
        else if (count == 0 && iconParent != null) iconParent.gameObject.SetActive(false);
        else if (iconParent != null) iconParent.gameObject.SetActive(true);
    }
}
