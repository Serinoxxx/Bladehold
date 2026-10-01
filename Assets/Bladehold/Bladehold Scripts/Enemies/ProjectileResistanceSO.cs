using UnityEngine;

[CreateAssetMenu(fileName = "ProjectileResistanceSO", menuName = "Scriptable Objects/ProjectileResistanceSO")]
public class ProjectileResistanceSO : ScriptableObject
{
    [Tooltip("Multiplier on projectile hits: arrows (bow and towers), thrown axes, wand bolts. 0.5 = half damage.")]
    [Range(0f, 1f)] public float projectileMultiplier = 0.5f;

    [Tooltip("Ballista bolts (Damage.piercesShields) punch straight through at full damage.")]
    public bool pierceIgnoresResistance = true;
}
