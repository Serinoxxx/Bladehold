using UnityEngine;

[CreateAssetMenu(fileName = "SlayerDashAttackSO", menuName = "Scriptable Objects/SlayerDashAttackSO")]
public class SlayerDashAttackSO : ScriptableObject
{
    [Header("Targeting")]
    [Tooltip("Distance to the player within which the slayer will line up a dash.")]
    public float triggerRange = 10f;

    [Header("Lane")]
    [Tooltip("Max length of the dash lane. Pre-clamped with NavMesh.Raycast so the telegraph stays honest near walls.")]
    public float maxDashDistance = 12f;

    [Tooltip("World width of the dash lane (and the swept damage capsule's diameter).")]
    public float laneWidth = 1.6f;

    [Header("Damage")]
    [Tooltip("Damage dealt to everything caught in the swept lane.")]
    public float damage = 8f;

    [Tooltip("Type of damage dealt. The dash is stamped unparryable regardless — a whole-lane sweep has no single swing to read.")]
    public DamageType damageType = DamageType.sharp;

    [Header("Timing")]
    [Tooltip("Seconds the red lane telegraph shows before the dash executes.")]
    public float telegraphSeconds = 0.9f;

    [Tooltip("Minimum seconds between dashes.")]
    public float attackCooldown = 5f;
}
