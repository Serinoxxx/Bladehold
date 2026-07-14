using UnityEngine;

[CreateAssetMenu(fileName = "HookProjectileAttackSO", menuName = "Scriptable Objects/HookProjectileAttackSO")]
public class HookProjectileAttackSO : ScriptableObject
{
    [Header("Targeting")]
    [Tooltip("Distance to the player within which the butcher will throw the hook.")]
    public float attackRange = 12f;

    [Header("Damage")]
    [Tooltip("Damage dealt to the player if the hook connects.")]
    public float damage = 4f;
    [Tooltip("Type of damage dealt. Sharp — a single readable projectile, deliberately PARRYABLE (parrying or blocking the hook also negates the pull).")]
    public DamageType damageType = DamageType.sharp;

    [Header("Projectile")]
    [Tooltip("World-space speed of the hook.")]
    public float hookSpeed = 11f;
    [Tooltip("Seconds before an in-flight hook that hit nothing self-destructs.")]
    public float hookLifetime = 3f;

    [Header("Pull")]
    [Tooltip("Seconds the drag toward the butcher lasts after a hook lands on the player.")]
    public float pullSeconds = 0.5f;
    [Tooltip("The drag stops once the player is within this distance of the butcher (into chopping range, not inside his model).")]
    public float pullStopDistance = 2f;

    [Header("Timing")]
    [Tooltip("Seconds from the start of the throw animation to the moment the hook is actually launched. Tune to match the clip.")]
    public float windupToApex = 0.5f;
    [Tooltip("Minimum seconds between throws.")]
    public float attackCooldown = 6f;
}
