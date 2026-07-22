using UnityEngine;

[CreateAssetMenu(fileName = "BoulderThrowAttackSO", menuName = "Scriptable Objects/BoulderThrowAttackSO")]
public class BoulderThrowAttackSO : ScriptableObject
{
    public float triggerRange = 15f;
    public float revSeconds = 1.5f;
    public float attackCooldown = 5f;
    public float damage = 15f;
    public DamageType damageType = DamageType.blunt;

    [Tooltip("Arc height multiplier for the throw trajectory")]
    public float arcHeight = 5f;

    [Tooltip("Area of effect radius upon impact")]
    public float explosionRadius = 3.5f;

    [Tooltip("Gravity applied to the boulder projectile (meters per second squared)")]
    public float gravity = 20f;
}
