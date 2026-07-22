using UnityEngine;

[CreateAssetMenu(fileName = "LeapSlamAttackSO", menuName = "Scriptable Objects/LeapSlamAttackSO")]
public class LeapSlamAttackSO : ScriptableObject
{
    [Header("Targeting")]
    [Tooltip("Distance to the player within which the demon will start a leap.")]
    public float triggerRange = 12f;

    [Header("Flight")]
    [Tooltip("Seconds of grounded wind-up (telegraph visible) before the demon leaves the ground.")]
    public float windupSeconds = 0.8f;

    [Tooltip("Seconds the parabolic flight to the locked landing spot takes (telegraph stays visible).")]
    public float flightSeconds = 0.7f;

    [Tooltip("Peak height of the leap arc above the straight start→landing line.")]
    public float arcHeight = 4f;

    [Header("Slam")]
    [Tooltip("Radius of the slam AoE at the landing spot.")]
    public float slamRadius = 3.5f;

    [Tooltip("Damage dealt to everything caught in the slam.")]
    public float damage = 15f;

    [Tooltip("Type of damage dealt. The slam is stamped unparryable regardless — a wide AoE has no single swing to read.")]
    public DamageType damageType = DamageType.blunt;

    [Tooltip("Knockback fling rating stamped on every slam hit (vs. each victim's knockback resistance).")]
    public float knockbackForce = 12f;

    [Header("Timing")]
    [Tooltip("Minimum seconds between leaps.")]
    public float attackCooldown = 6f;
}
