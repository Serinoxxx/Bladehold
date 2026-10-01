using System;
using UnityEngine;

/// <summary>
///     Per-scene enemy list for sector waves (the Graveyard: skeletons only). When a scene has one, its
///     <see cref="enemyIds" /> replace the roster's threat gating (<c>minThreat</c>), so a row with
///     <c>minThreat</c> 0 can still spawn here. Each row's <c>unlockWave</c>, <c>spawnChance</c> and
///     <c>maxConcurrent</c> still apply, and the fodder floor uses <see cref="fodderEnemyId" /> instead of
///     the pacing asset's goblin. A scene without one uses the normal threat curve. Scene singleton, read
///     by <see cref="SurvivorsSpawner" />; placed by the defense scene generator from the spec.
/// </summary>
public class SceneEnemyRoster : MonoBehaviour
{
    [Tooltip("Roster CSV ids that spawn in this scene's sector waves. Empty = use the normal threat curve.")]
    [SerializeField] private string[] enemyIds = Array.Empty<string>();
    [Tooltip("Roster id used for the fodder floor (60% of spawns). Empty = the pacing asset's fodder.")]
    [SerializeField] private string fodderEnemyId = "";

    public static SceneEnemyRoster Instance { get; private set; }

    /// <summary>True when the current scene restricts which enemies spawn.</summary>
    public static bool Active => Instance != null && Instance.enemyIds != null && Instance.enemyIds.Length > 0;

    /// <summary>This scene's fodder id, or null to use the pacing asset's.</summary>
    public static string FodderOverride =>
        Instance != null && !string.IsNullOrEmpty(Instance.fodderEnemyId) ? Instance.fodderEnemyId : null;

    public static bool Allows(string id)
    {
        if (!Active || string.IsNullOrEmpty(id)) return false;
        foreach (string allowed in Instance.enemyIds)
        {
            if (string.Equals(allowed, id, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError("[SceneEnemyRoster] More than one in the scene; keeping the first.", this);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

#if UNITY_EDITOR
    /// <summary>Editor-only setter for the scene generator.</summary>
    public void EditorSet(string[] ids, string fodder)
    {
        enemyIds = ids ?? Array.Empty<string>();
        fodderEnemyId = fodder ?? "";
    }
#endif
}
