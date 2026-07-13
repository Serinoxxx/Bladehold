using UnityEngine;

[CreateAssetMenu(fileName = "RadialBurstAttackSO", menuName = "Scriptable Objects/RadialBurstAttackSO")]
public class RadialBurstAttackSO : ScriptableObject
{
    [Header("Targeting")]
    [Tooltip("Distance to the player within which bursts fire. No line of sight or facing is required — proximity alone triggers it.")]
    public float attackRange = 16f;

    [Header("Damage")]
    [Tooltip("Damage dealt to the player by each projectile that connects.")]
    public float damage = 5f;
    [Tooltip("Type of damage dealt.")]
    public DamageType damageType = DamageType.elemental;

    [Header("Burst")]
    [Tooltip("Projectiles per burst, spread at even angles over the full 360°.")]
    public int projectileCount = 8;
    [Tooltip("World-space speed of each projectile. Kept slow so the gaps between them are walkable.")]
    public float projectileSpeed = 5f;
    [Tooltip("Seconds before an in-flight projectile that hit nothing self-destructs.")]
    public float projectileLifetime = 6f;

    [Header("Timing")]
    [Tooltip("Seconds from the start of the cast animation to the moment the burst is actually released. Tune to match the cast clip.")]
    public float windupToApex = 0.6f;
    [Tooltip("Minimum seconds between the start of one burst and the next.")]
    public float attackCooldown = 5f;
}
