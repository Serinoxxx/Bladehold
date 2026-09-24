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
}
