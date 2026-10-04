using UnityEngine;

/// <summary>
///     Tunables for the mace ultimate, Seismic Quake (<see cref="MaceUltimate" />): a leap, a slam whose
///     shockwave ring spreads outward, then aftershock tremors on every mace swing for the rest of the duration.
/// </summary>
[CreateAssetMenu(fileName = "SeismicQuakeConfigSO", menuName = "Scriptable Objects/Ultimates/SeismicQuakeConfigSO")]
public class SeismicQuakeConfigSO : UltimateConfigSO
{
    [Header("Leap")]
    [Tooltip("How far the leap carries the player toward where the camera looks, in metres. Walls cut it short.")]
    public float leapDistance = 6f;

    [Tooltip("Peak height of the leap arc, in metres.")]
    public float leapHeight = 2.5f;

    [Tooltip("Seconds from take-off to landing.")]
    public float leapDuration = 0.55f;

    [Header("Slam ring")]
    [Tooltip("Radius the shockwave ring reaches, in metres.")]
    public float slamRadius = 10f;

    [Tooltip("Seconds for the ring to spread from the centre to slamRadius. Enemies are hit as it reaches them.")]
    public float ringExpandSeconds = 0.45f;

    [Tooltip("Slam damage before AllDamageMultiplier.")]
    public float slamDamage = 90f;

    [Tooltip("Seconds enemies caught by the ring are stunned.")]
    public float slamStunSeconds = 2.5f;

    [Tooltip("Outward impulse on non-kinematic bodies caught by the ring.")]
    public float launchForce = 16f;

    [Tooltip("Upward share of the launch direction (0 = flat push, 1 = 45 degrees).")]
    public float launchUpward = 0.7f;

    [Header("Aftershocks")]
    [Tooltip("Aftershock damage per mace swing before AllDamageMultiplier (the UltimateMaceAftershockDamage stat base).")]
    public float aftershockDamage = 30f;

    [Tooltip("Aftershock radius, in metres.")]
    public float aftershockRadius = 4f;

    [Tooltip("How far in front of the player the aftershock lands, in metres.")]
    public float aftershockForwardOffset = 1.5f;

    [Tooltip("Seconds after the swing starts before the aftershock lands (roughly the mace's impact frame).")]
    public float aftershockDelay = 0.2f;

    [Tooltip("Seconds aftershocks stun what they hit.")]
    public float aftershockStunSeconds = 0.6f;
}
