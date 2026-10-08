using UnityEngine;

[CreateAssetMenu(fileName = "AIAttackSO", menuName = "Scriptable Objects/AIAttackSO")]
public class AIAttackSO : ScriptableObject
{
    [Header("Targeting")]
    [Tooltip("Distance to the player within which the goblin will start an attack.")]
    public float attackRange = 2f;
    [Tooltip("Half-angle of the cone in front of the enemy within which the player must be to start an attack (degrees).")]
    public float attackConeAngle = 45f;
    [Tooltip("Extra reach (m) when the blow lands. The attack starts at attackRange, so without leeway a target that stepped back a few centimetres during the wind-up is missed while the swing visibly connects.")]
    public float apexRangeBonus = 0.75f;
    [Tooltip("Half-angle (degrees) the target must be within when the blow lands. Turning is paused during the wind-up, so this is wider than attackConeAngle. Never narrower than it.")]
    public float apexConeAngle = 75f;

    [Header("Turning")]
    [Tooltip("Turn rate multiplier applied after an attack.")]
    public float postAttackTurnMultiplier = 0.5f;
    [Tooltip("Duration of the turn rate penalty after an attack.")]
    public float postAttackTurnPenaltyDuration = 1.0f;

    [Header("Damage")]
    [Tooltip("Damage dealt to the player if they are still in range at the attack's apex.")]
    public float damage = 10f;
    [Tooltip("Type of damage dealt.")]
    public DamageType damageType = DamageType.sharp;

    [Header("Timing")]
    [Tooltip("Seconds the goblin waits in range before starting the attack animation.")]
    public float preAttackDelay = 0.3f;
    [Tooltip("Seconds from the start of the attack animation to its apex (the moment damage is applied). Tune to match the attack clip.")]
    public float windupToApex = 0.4f;
    [Tooltip("Minimum seconds between the start of one attack and the next.")]
    public float attackCooldown = 1.5f;
    [Tooltip("Seconds the goblin must wait before attacking again after being staggered by a player hit.")]
    public float staggerCooldown = 1.0f;
}
