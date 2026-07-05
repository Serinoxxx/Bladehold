using UnityEngine;

/// <summary>
///     Base tunables for the Chain Lightning buff, registered as <see cref="PlayerStats" /> bases by
///     <see cref="ChainLightningBuff" /> in <c>Start</c> (same convention as <see cref="ImpulseSO" />).
///     Gold-tree upgrades layer modifiers on top of these without ever mutating this asset.
/// </summary>
[CreateAssetMenu(fileName = "ChainLightningSO", menuName = "Scriptable Objects/ChainLightningSO")]
public class ChainLightningSO : ScriptableObject
{
    [Tooltip("Seconds of buff granted per orb before upgrades. 0 = the Chain Lightning feature is locked until the 'Chain Lightning' node is bought.")]
    public float baseOrbDurationSeconds = 0f;

    [Tooltip("Base number of additional enemies a bounce can chain to before upgrades. 0 = locked.")]
    public float baseBounces = 0f;

    [Tooltip("Base fraction of the triggering hit's damage each bounce deals before upgrades (e.g. 0.5 = 50%). 0 = locked.")]
    public float baseDamagePercent = 0f;

    [Tooltip("Base chance (0-1) each bounce crits before upgrades.")]
    public float baseCritChance = 0f;

    [Tooltip("World-space radius each hop searches for the next target.")]
    public float chainRadius = 6f;

    [Tooltip("Extra bounce damage per orb stacked beyond the first (0.10 = +10% per extra stack).")]
    public float damagePerExtraStackPercent = 0.10f;
}
