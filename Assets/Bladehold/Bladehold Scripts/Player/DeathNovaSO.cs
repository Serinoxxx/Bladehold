using UnityEngine;

/// <summary>
///     Base tunables for the Death Nova, registered as <see cref="PlayerStats" /> bases by
///     <see cref="DeathNova" /> in <c>Start</c> (same convention as <see cref="HealthSO" />/<see cref="DamageSO" />).
///     Reincarnate-tree upgrades layer modifiers on top of these without ever mutating this asset.
/// </summary>
[CreateAssetMenu(fileName = "DeathNovaSO", menuName = "Scriptable Objects/DeathNovaSO")]
public class DeathNovaSO : ScriptableObject
{
    [Tooltip("0 = Death Nova locked. The 'Death's Reprieve' Reincarnate node grants +1 to unlock it.")]
    public float baseCharges = 0f;

    [Tooltip("Seconds before the Death Nova charge is available again after triggering.")]
    public float baseCooldownSeconds = 90f;

    [Tooltip("Fraction of max health (0-1) the player revives with when the nova triggers. 0 = blast-only, no revive, until a Reincarnate node grants some.")]
    [Range(0f, 1f)] public float baseRevivePercent = 0f;
}
