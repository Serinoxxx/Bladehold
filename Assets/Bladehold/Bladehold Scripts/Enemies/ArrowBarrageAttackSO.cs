using UnityEngine;

[CreateAssetMenu(fileName = "ArrowBarrageAttackSO", menuName = "Scriptable Objects/ArrowBarrageAttackSO")]
public class ArrowBarrageAttackSO : ScriptableObject
{
    public float triggerRange = 20f;
    public float revSeconds = 1.5f;
    public float attackCooldown = 6f;
    public float damage = 8f;
    public DamageType damageType = DamageType.sharp;

    [Tooltip("Radius of the arrow barrage area of effect")]
    public float zoneRadius = 4f;

    [Tooltip("How long the barrage lasts")]
    public float barrageDuration = 4f;

    [Tooltip("How often damage ticks inside the barrage zone")]
    public float tickRate = 0.5f;
}
