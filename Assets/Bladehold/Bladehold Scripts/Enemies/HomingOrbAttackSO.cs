using UnityEngine;

[CreateAssetMenu(fileName = "HomingOrbAttackSO", menuName = "Scriptable Objects/HomingOrbAttackSO")]
public class HomingOrbAttackSO : ScriptableObject
{
    [Header("Targeting")]
    [Tooltip("Distance to the player within which the mystic will start casting an orb.")]
    public float attackRange = 12f;

    [Header("Damage")]
    [Tooltip("Damage dealt to the player if the orb connects.")]
    public float damage = 4f;
    [Tooltip("Type of damage dealt.")]
    public DamageType damageType = DamageType.elemental;

    [Header("Projectile")]
    [Tooltip("World-space speed of the orb. Kept slow so it's outrunnable even while homing.")]
    public float orbSpeed = 4f;
    [Tooltip("Seconds before an in-flight orb that hit nothing self-destructs.")]
    public float orbLifetime = 8f;
    [Tooltip("Max degrees per second the orb can turn toward the player while homing.")]
    public float turnRateDegPerSec = 60f;
    [Tooltip("Seconds after launch the orb keeps homing; afterwards it flies straight, so a late dodge always works.")]
    public float homingSeconds = 2.5f;

    [Header("Timing")]
    [Tooltip("Seconds from the start of the cast animation to the moment the orb is actually launched. Tune to match the cast clip.")]
    public float windupToApex = 0.5f;
    [Tooltip("Minimum seconds between the start of one cast and the next.")]
    public float attackCooldown = 4f;
}
