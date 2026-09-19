using UnityEngine;

[CreateAssetMenu(fileName = "BulwarkAttackSO", menuName = "Scriptable Objects/Enemies/Bulwark Attack")]
public class BulwarkAttackSO : ScriptableObject
{
    [Header("Shield Configuration")]
    [Tooltip("Maximum hit points of the physical shield before it breaks.")]
    public float shieldMaxHp = 40f;

    [Tooltip("Damage multiplier taken by the shield from projectiles (arrows, wand missiles, etc.).")]
    [Range(0f, 1f)]
    public float projectileDamageMultiplier = 0.1f;

    [Header("Slam Attack Configuration")]
    [Tooltip("Damage dealt by the ground slam on impact.")]
    public float slamDamage = 15f;

    [Tooltip("Radius of the slam impact zone.")]
    public float slamRadius = 2.5f;

    [Tooltip("Wind-up duration in seconds during which the telegraph is shown before impact.")]
    public float telegraphSeconds = 1.2f;

    [Tooltip("Minimum time between slam attacks in seconds.")]
    public float slamCooldown = 2.5f;

    [Tooltip("Forward distance offset from character root for the slam center.")]
    public float forwardOffset = 1.6f;

    [Tooltip("Trigger range for initiating regular slam if in proximity.")]
    public float triggerRange = 3f;

    [Tooltip("Knockback force applied to targets caught in the slam.")]
    public float knockbackForce = 12f;
}
