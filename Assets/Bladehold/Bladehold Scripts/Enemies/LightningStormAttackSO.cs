using UnityEngine;

[CreateAssetMenu(fileName = "LightningStormAttackSO", menuName = "Scriptable Objects/LightningStormAttackSO")]
public class LightningStormAttackSO : ScriptableObject
{
    [Header("Targeting")]
    [Tooltip("Distance to the player within which the witch will cast the storm. Generous by default — this ability is meant to apply background pressure whenever she's nearby, not a close-range attack.")]
    public float castRange = 20f;
    [Tooltip("Minimum seconds between the start of one storm cast and the next.")]
    public float castCooldown = 8f;

    [Header("Storm")]
    [Tooltip("Radius of the strike area, spawned centered on the player's position at the moment of casting.")]
    public float stormRadius = 3f;
    [Tooltip("Seconds the storm persists after being cast.")]
    public float stormDuration = 6f;
    [Tooltip("Seconds between strikes while something remains inside the storm.")]
    public float strikeInterval = 1.5f;
    [Tooltip("Damage dealt by each strike.")]
    public float strikeDamage = 4f;
    [Tooltip("Type of damage dealt.")]
    public DamageType damageType = DamageType.elemental;
}
