using UnityEngine;

/// <summary>Tunables for <see cref="MountStopper" />, the "halts mounts" enemy attribute.</summary>
[CreateAssetMenu(fileName = "MountStopperSO", menuName = "Scriptable Objects/Enemies/Mount Stopper")]
public class MountStopperSO : ScriptableObject
{
    [Tooltip("Half-angle in degrees, measured from the enemy's facing, within which a ridden horse is stopped. 180 = from any side.")]
    [Range(0f, 180f)]
    public float haltHalfAngle = 90f;

    [Tooltip("Also require the enemy's shield (an IShieldBlocker on it, e.g. BulwarkShield) to be up and facing the horse. A broken shield or a horse coming from behind then rides on through.")]
    public bool requireShieldBlock = true;

    [Tooltip("Seconds the horse is locked in its rear (no movement or turning) after running into this enemy.")]
    public float rearLockSeconds = 0.9f;

    [Tooltip("Damage the horse takes when it runs into this enemy hard enough to rear, at full charge speed. 0 = the stop is harmless (the Bulwark's shield wall); the Spearman's braced spears hurt.")]
    [Min(0f)]
    public float horseImpactDamage = 0f;

    [Tooltip("Fraction of Horse Impact Damage dealt even at the slowest speed that still rears the horse; scales up to the full amount at charge speed.")]
    [Range(0f, 1f)]
    public float minImpactDamageFraction = 0.5f;
}
