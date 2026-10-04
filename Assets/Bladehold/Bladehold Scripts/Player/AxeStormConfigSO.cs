using UnityEngine;

/// <summary>
///     Tunables for the throwing-axe ultimate, Axe Storm (<see cref="ThrowingAxeUltimate" />): fast
///     three-axe fan throws whose axes ricochet between enemies.
/// </summary>
[CreateAssetMenu(fileName = "AxeStormConfigSO", menuName = "Scriptable Objects/Ultimates/AxeStormConfigSO")]
public class AxeStormConfigSO : UltimateConfigSO
{
    [Tooltip("Extra enemies each axe bounces to after a hit (the UltimateAxeRicochetCount stat base).")]
    public int ricochetCount = 2;

    [Tooltip("How far an axe looks for its next ricochet target, in metres.")]
    public float ricochetRange = 9f;
}
