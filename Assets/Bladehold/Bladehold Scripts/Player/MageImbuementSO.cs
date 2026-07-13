using UnityEngine;

/// <summary>
///     Base tunables for the Mage's elemental imbuement, registered as <see cref="PlayerStats" />
///     bases by <see cref="MageImbuement" /> in <c>Start</c> (the <see cref="ImpulseSO" /> convention).
///     Skill-tree upgrades layer modifiers on top without ever mutating this asset. The imbuement
///     works out of the box (picking up a node imbues and deals bonus elemental damage — the
///     bow-draw convention); only the fire explosion and flame zone are node-gated (their stats
///     start at 0).
/// </summary>
[CreateAssetMenu(fileName = "MageImbuementSO", menuName = "Scriptable Objects/MageImbuementSO")]
public class MageImbuementSO : ScriptableObject
{
    [Header("Imbuement (timed + charge-stacked; a pickup RESETS the timer, it never adds)")]
    [Tooltip("Seconds the imbuement lasts per refresh. Registered as the MageImbuementDuration stat base.")]
    public float imbuementDurationSeconds = 12f;

    [Tooltip("Maximum element charges the buff can hold. Registered as the MageImbuementMaxCharges stat base.")]
    public float maxCharges = 3f;

    [Tooltip("Extra elemental damage per held charge, as a fraction of the triggering hit (0.10 = +10% per charge). Registered as the MageImbuementBonusPerCharge stat base.")]
    public float bonusDamagePercentPerCharge = 0.10f;

    [Tooltip("How much stronger each charge beyond the first makes the element's effects — explosion/zone/chain damage and slow depth (0.15 = +15% per extra charge; the ImpulseBuff stack idiom).")]
    public float potencyPerExtraChargePercent = 0.15f;

    [Header("Runestones")]
    [Tooltip("Element charges granted when a runestone of a different element is blasted. Registered as the MageRunestoneCharges stat base (the 'Runic Attunement' line raises it).")]
    public float runestoneBaseCharges = 2f;

    [Header("Fire")]
    [Tooltip("Extra fraction of the triggering hit dealt while Fire-imbued, on top of the per-charge bonus. Registered as the MageFireDamagePercent stat base.")]
    public float fireBonusDamagePercent = 0.15f;

    [Tooltip("Radius in metres of the fire explosion. Registered as the MageFireExplosionRadius stat base (the explosion itself is gated on MageFireExplosionDamagePercent, base 0).")]
    public float explosionRadiusMetres = 2f;

    [Tooltip("Seconds between flame-zone damage ticks.")]
    public float flameZoneTickInterval = 0.5f;

    [Tooltip("Fraction of the triggering hit's damage each flame-zone tick deals. Registered as the MageFlameZoneDamagePercent stat base (zones are gated on MageFlameZoneDuration, base 0).")]
    public float flameZoneDamagePercent = 0.20f;

    [Tooltip("Minimum seconds between flame zones, however many fire hits land — a wide staff sweep must not carpet the arena.")]
    public float flameZoneCooldownSeconds = 2f;

    [Header("Ice")]
    [Tooltip("Fraction (0-1) Ice-imbued hits slow their target. Registered as the MageIceSlowPercent stat base.")]
    public float iceSlowFraction = 0.10f;

    [Tooltip("Seconds an Ice-imbued hit's slow lasts (SlowDurationBonusSeconds adds on top). Registered as the MageIceSlowDurationSeconds stat base.")]
    public float iceSlowDurationSeconds = 2f;
}
