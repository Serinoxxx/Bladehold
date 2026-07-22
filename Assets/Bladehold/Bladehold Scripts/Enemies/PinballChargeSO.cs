using UnityEngine;

[CreateAssetMenu(fileName = "PinballChargeSO", menuName = "Scriptable Objects/PinballChargeSO")]
public class PinballChargeSO : ScriptableObject
{
    [Header("Targeting")]
    [Tooltip("Distance to the player within which the golem will rev up a charge.")]
    public float triggerRange = 10f;

    [Header("Rev-up")]
    [Tooltip("Seconds of stationary rev-up telegraph before the charge launches.")]
    public float revSeconds = 1.2f;

    [Header("Charge")]
    [Tooltip("Speed of the charge in metres per second.")]
    public float chargeSpeed = 14f;

    [Tooltip("Seconds the charge lasts (bouncing off walls the whole time).")]
    public float chargeSeconds = 4f;

    [Header("Contact damage")]
    [Tooltip("Radius around the golem that deals contact damage while charging.")]
    public float contactRadius = 1.3f;

    [Tooltip("Damage dealt on contact while charging.")]
    public float damage = 12f;

    [Tooltip("Type of damage dealt. Contact with a careening machine has no swing to read — stamped unparryable regardless.")]
    public DamageType damageType = DamageType.blunt;

    [Tooltip("Seconds before the same target can be contact-hit again within one charge.")]
    public float rehitSeconds = 1f;

    [Tooltip("Knockback fling rating stamped on contact hits (enemies clipped by the pinball get flung).")]
    public float knockbackForce = 10f;

    [Header("Timing")]
    [Tooltip("Minimum seconds between charges.")]
    public float attackCooldown = 6f;
}
