using UnityEngine;

/// <summary>
///     Tunables for the mounted knight: the horse-back AI (<see cref="MountedKnightBrain" />'s
///     standoff/rear/charge cycle) and the unseating rules (<see cref="MountedKnightRider" />).
///     Horse-side numbers shared with the player (trample hitbox, impulse stamp, rear length) live
///     on <see cref="HorseSO" />; the knight's on-foot phase uses the ordinary goblin components and
///     their own SOs.
/// </summary>
[CreateAssetMenu(fileName = "MountedKnightSO", menuName = "Scriptable Objects/MountedKnightSO")]
public class MountedKnightSO : ScriptableObject
{
    [Header("Reposition")]
    [Tooltip("Preferred distance from the player the horse circles at between charges.")]
    public float standoffDistance = 12f;

    [Tooltip("Below this distance the knight won't start a charge (no room to build up) and retreats instead.")]
    public float minChargeRange = 6f;

    [Tooltip("NavMeshAgent speed while repositioning (the charge itself uses Charge Speed).")]
    public float repositionSpeed = 6f;

    [Tooltip("Seconds between destination updates while repositioning.")]
    public float repathInterval = 0.4f;

    [Header("Rear (the charge telegraph)")]
    [Tooltip("Degrees per second the horse turns toward the player before locking the charge lane.")]
    public float aimTurnSpeed = 240f;

    [Tooltip("How closely (degrees) the horse must face the player before the rear starts.")]
    public float aimToleranceDegrees = 8f;

    [Tooltip("Seconds the horse rears in place before charging — the player's dodge window.")]
    public float rearSeconds = 1.2f;

    [Header("Charge")]
    [Tooltip("Dash speed in m/s along the locked lane.")]
    public float chargeSpeed = 14f;

    [Tooltip("Longest possible charge lane; NavMesh.Raycast clamps it further near arena edges.")]
    public float maxChargeDistance = 25f;

    [Tooltip("Extra metres run past the aimed point so the horse blows through the player's position rather than stopping on it.")]
    public float overshootDistance = 3f;

    [Tooltip("Seconds spent decelerating out of the charge.")]
    public float decelSeconds = 0.6f;

    [Tooltip("Seconds between charges.")]
    public float chargeCooldown = 7f;

    [Tooltip("The charge's damage as a multiple of the knight's roster damage value (on-foot melee 4 × 4 = 16 per trample).")]
    public float chargeDamageMultiplier = 4f;

    [Header("Dismount")]
    [Tooltip("The knight is unseated when his health fraction drops to or below this (0.5 = half health).")]
    [Range(0f, 1f)]
    public float dismountHealthFraction = 0.5f;

    [Tooltip("Metres to the horse's side where the knight lands.")]
    public float dismountSideOffset = 1.5f;

    [Tooltip("Search radius for NavMesh.SamplePosition when placing the unseated knight.")]
    public float navMeshSampleDistance = 3f;
}
