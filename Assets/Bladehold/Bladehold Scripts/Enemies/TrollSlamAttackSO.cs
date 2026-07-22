using UnityEngine;

[CreateAssetMenu(fileName = "TrollSlamAttackSO", menuName = "Scriptable Objects/TrollSlamAttackSO")]
public class TrollSlamAttackSO : ScriptableObject
{
    [Header("Targeting")]
    [Tooltip("Distance to the target within which the troll will start a slam.")]
    public float triggerRange = 5f;

    [Header("Area")]
    [Tooltip("Radius of the slam's damage area.")]
    public float slamRadius = 5f;
    [Tooltip("How far ahead of the troll the slam area is centred.")]
    public float forwardOffset = 2.5f;

    [Header("Timing")]
    [Tooltip("Seconds between the ground telegraph appearing and the slam landing — the dodge window.")]
    public float telegraphSeconds = 1.5f;
    [Tooltip("Minimum seconds between the start of one slam and the next.")]
    public float attackCooldown = 6f;

    [Header("Damage")]
    [Tooltip("Damage dealt to everything caught in the area — the player AND other enemies alike.")]
    public float damage = 40f;
    [Tooltip("Type of damage dealt.")]
    public DamageType damageType = DamageType.blunt;

    [Header("Knockback (reuses the player's knockback/resistance system)")]
    [Tooltip("Knockback rating stamped on every slam hit, compared against each victim's knockback resistance (KnockbackReceiver): at or above resistance = ragdoll fling, within 1 below = knockdown. High enough by default to fling every goblin type.")]
    public float knockbackForce = 12f;
}
