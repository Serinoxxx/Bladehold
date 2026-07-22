using UnityEngine;

[CreateAssetMenu(fileName = "LaserBeamAttackSO", menuName = "Scriptable Objects/LaserBeamAttackSO")]
public class LaserBeamAttackSO : ScriptableObject
{
    public float triggerRange = 12f;
    public float revSeconds = 1.5f;
    public float attackCooldown = 5f;
    public float beamDuration = 2f;
    public float damage = 3f;
    public DamageType damageType = DamageType.elemental;

    [Tooltip("Width of the laser beam (BoxCast extents)")]
    public float beamWidth = 1f;

    [Tooltip("Length of the laser beam")]
    public float beamLength = 20f;

    [Tooltip("How often damage ticks while caught in the beam")]
    public float tickRate = 0.2f;

    [Tooltip("How fast the golem can turn while firing the laser (degrees per second)")]
    public float sweepTurnRate = 20f;
}
