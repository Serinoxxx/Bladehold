using UnityEngine;

[CreateAssetMenu(fileName = "AIAttackSO", menuName = "Scriptable Objects/AIAttackSO")]
public class AIAttackSO : ScriptableObject
{
    [Header("Targeting")]
    [Tooltip("Distance to the player within which the goblin will start an attack.")]
    public float attackRange = 2f;

    [Header("Damage")]
    [Tooltip("Damage dealt to the player if they are still in range at the attack's apex.")]
    public float damage = 10f;
    [Tooltip("Type of damage dealt.")]
    public DamageType damageType = DamageType.sharp;

    [Header("Timing")]
    [Tooltip("Seconds from the start of the attack animation to its apex (the moment damage is applied). Tune to match the attack clip.")]
    public float windupToApex = 0.4f;
    [Tooltip("Minimum seconds between the start of one attack and the next.")]
    public float attackCooldown = 1.5f;
}
