using UnityEngine;

/// <summary>
///     Tunable parameters for the Assassin's 3-phase attack cycle:
///     1. Wind-up with telegraphed circle
///     2. Stationary whirlwind spin dealing multi-hit damage
///     3. Stunned / dizzy recovery window
/// </summary>
[CreateAssetMenu(fileName = "AssassinAttackSO", menuName = "Scriptable Objects/AssassinAttackSO")]
public class AssassinAttackSO : ScriptableObject
{
    [Header("Targeting")]
    [Tooltip("Distance to target within which the assassin begins the attack cycle.")]
    public float triggerRange = 3.5f;

    [Header("Area & Telegraph")]
    [Tooltip("Radius of the whirlwind spin attack and ground telegraph circle.")]
    public float spinRadius = 3.0f;
    [Tooltip("Duration of the wind-up phase and ground telegraph in seconds.")]
    public float windupSeconds = 1.0f;

    [Header("Whirlwind Spin")]
    [Tooltip("Total duration of the spinning attack in seconds.")]
    public float spinDuration = 2.0f;
    [Tooltip("Number of damage ticks during the spin.")]
    public int spinHits = 5;
    [Tooltip("Damage dealt per hit tick (overridden by Enemies.csv if set).")]
    public float damagePerHit = 5f;
    [Tooltip("Type of damage dealt.")]
    public DamageType damageType = DamageType.sharp;
    [Tooltip("Knockback force applied per hit.")]
    public float knockbackForce = 2f;
    [Tooltip("Rotation speed in degrees per second during the whirlwind spin.")]
    public float spinDegreesPerSecond = 720f;

    [Header("Stun & Recovery")]
    [Tooltip("Duration of the dizzy/stunned state following the whirlwind spin in seconds.")]
    public float stunDuration = 4.0f;
    [Tooltip("Cooldown after recovering from stun before the assassin can attack again.")]
    public float attackCooldown = 3.0f;

    [Header("Animation Triggers")]
    [Tooltip("Animator trigger to fire when starting windup.")]
    public string windupTrigger = "Attack";
    [Tooltip("Animator trigger to fire during stun/dizzy state.")]
    public string stunTrigger = "Stagger";
}
