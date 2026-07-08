using UnityEngine;

[CreateAssetMenu(fileName = "BomberAttackSO", menuName = "Scriptable Objects/BomberAttackSO")]
public class BomberAttackSO : ScriptableObject
{
    [Header("Targeting")]
    [Tooltip("Distance to the target within which the bomber stops and lights its fuse.")]
    public float triggerRange = 8f;

    [Header("Timing")]
    [Tooltip("Seconds the bomber stands still lighting the dynamite before sprinting again.")]
    public float igniteSeconds = 0.6f;
    [Tooltip("Seconds from lighting the fuse to the explosion (the ignite pause counts toward it) — the window to kill or outrun him.")]
    public float fuseSeconds = 5f;

    [Header("Charge")]
    [Tooltip("Agent-speed multiplier while the fuse burns — the lit bomber sprints at its target.")]
    public float fuseSpeedMultiplier = 1.6f;

    [Header("Explosion")]
    [Tooltip("Radius of the explosion's damage area.")]
    public float explosionRadius = 4f;
    [Tooltip("Damage dealt to everything caught in the area — the player AND other enemies alike.")]
    public float damage = 25f;
    [Tooltip("Type of damage dealt. Elemental = fire, never parryable (the Storm Witch convention).")]
    public DamageType damageType = DamageType.elemental;

    [Header("Impulse (reuses the player's fling/resistance system)")]
    [Tooltip("Impulse rating stamped on every explosion hit, compared against each victim's impulse resistance (ImpulseReceiver): at or above resistance = ragdoll fling, within 1 below = knockdown.")]
    public float impulsePower = 3f;
    [Tooltip("Launch speed in m/s for victims the impulse flings.")]
    public float impulseForce = 12f;
}
