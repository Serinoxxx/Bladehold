using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
///     Per-wall NavMesh routing (plan 17). Each wall plot sits on its own baked NavMesh area
///     ("WallPlot0".."WallPlot7", a <c>NavMeshModifierVolume</c> the generator lays over the bridge
///     head). While a wall stands with its door shut, its area costs <see cref="FortUpgradeConfigSO" />'s
///     wall cost, so enemies route over another bridge unless the detour is longer than the penalty; with
///     every bridge walled the cheapest path still runs through a wall and they walk up and attack it.
///
///     Costs are applied per agent (never <c>NavMesh.SetAreaCost</c>), so siege units can keep cost 1 and
///     path straight into walls. <see cref="AIMovement" /> re-applies whenever <see cref="Version" /> moves.
/// </summary>
public static class WallNavCost
{
    private static readonly Dictionary<int, float> costs = new Dictionary<int, float>();

    /// <summary>Bumped on every change; agents compare it to know when to re-apply.</summary>
    public static int Version { get; private set; }

    /// <summary>Sets one wall area's cost (1 = open/no wall).</summary>
    public static void SetCost(int area, float cost)
    {
        if (area < 0) return;
        if (costs.TryGetValue(area, out float old) && Mathf.Approximately(old, cost)) return;
        costs[area] = cost;
        Version++;
    }

    /// <summary>Forgets an area (its plot was destroyed); agents fall back to cost 1 for it.</summary>
    public static void Clear(int area)
    {
        if (costs.Remove(area)) Version++;
    }

    /// <summary>Writes every known wall-area cost onto <paramref name="agent" /> (non-siege agents only).</summary>
    public static void ApplyTo(NavMeshAgent agent)
    {
        if (agent == null) return;
        foreach (KeyValuePair<int, float> pair in costs)
        {
            agent.SetAreaCost(pair.Key, pair.Value);
        }
    }

    /// <summary>
    ///     Siege units (battering ram, troll, sapper, captains, or anything tagged with
    ///     <see cref="SiegeUnit" />) ignore wall cost and smash walls in their path.
    /// </summary>
    public static bool IsSiege(GameObject go)
    {
        if (go == null) return false;
        return go.GetComponentInParent<SiegeUnit>() != null
               || go.GetComponentInParent<TrollSlamAttack>() != null
               || go.GetComponentInParent<TowerSapper>() != null
               || go.GetComponentInParent<BatteringRam>() != null
               || go.GetComponentInParent<ICaptain>() != null;
    }
}

