using UnityEngine;

/// <summary>
///     Tunables for the sword ultimate, Moonlight Edge (<see cref="SwordMoonlightEdgeUltimate" />):
///     every swing during the ultimate also fires a <see cref="MoonlightCrescent" /> wave in a line.
/// </summary>
[CreateAssetMenu(fileName = "MoonlightEdgeConfigSO", menuName = "Scriptable Objects/Ultimates/MoonlightEdgeConfigSO")]
public class MoonlightEdgeConfigSO : UltimateConfigSO
{
    [Header("Crescent")]
    [Tooltip("Base crescent damage as a multiple of SwordDamage (the UltimateMoonlightCrescentDamage stat base).")]
    public float crescentDamageMultiplier = 1.2f;

    [Tooltip("Extra crescent damage at full charge, as a fraction of the base (1 = a fully charged swing's crescent hits twice as hard).")]
    public float fullChargeDamageBonus = 1f;

    [Tooltip("Crescent scale at full charge (1 = authored size on an uncharged swing).")]
    public float fullChargeScale = 1.6f;

    [Tooltip("Crescent travel speed in metres per second.")]
    public float speed = 22f;

    [Tooltip("How far a crescent flies before fading, in metres.")]
    public float range = 14f;

    [Tooltip("Half-width of the crescent's hit sweep at scale 1, in metres.")]
    public float halfWidth = 1.6f;

    [Tooltip("Height above the player's feet the crescent flies at.")]
    public float height = 1f;

    [Tooltip("Knockback impulse on each enemy the crescent passes through.")]
    public float knockback = 6f;

    [Tooltip("Minimum seconds between crescents, so a swing that re-activates the hitbox can't double-fire.")]
    public float minInterval = 0.15f;
}
