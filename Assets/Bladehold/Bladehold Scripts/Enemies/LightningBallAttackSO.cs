using UnityEngine;

[CreateAssetMenu(fileName = "LightningBallAttackSO", menuName = "Scriptable Objects/LightningBallAttackSO")]
public class LightningBallAttackSO : ScriptableObject
{
    [Header("Targeting")]
    [Tooltip("Distance to the player within which the witch will start casting a lightning ball.")]
    public float attackRange = 12f;

    [Header("Damage")]
    [Tooltip("Damage dealt to the player if the ball connects.")]
    public float damage = 6f;
    [Tooltip("Type of damage dealt.")]
    public DamageType damageType = DamageType.elemental;

    [Header("Projectile")]
    [Tooltip("World-space speed of the ball. Kept slow so it's dodgeable.")]
    public float ballSpeed = 6f;
    [Tooltip("Seconds before an in-flight ball that hit nothing self-destructs.")]
    public float ballLifetime = 5f;

    [Header("Timing")]
    [Tooltip("Seconds from the start of the cast animation to the moment the ball is actually launched. Tune to match the cast clip.")]
    public float windupToApex = 0.5f;
    [Tooltip("Minimum seconds between the start of one cast and the next.")]
    public float attackCooldown = 3f;
}
