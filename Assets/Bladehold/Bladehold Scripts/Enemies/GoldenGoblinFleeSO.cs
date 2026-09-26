using UnityEngine;

[CreateAssetMenu(fileName = "GoldenGoblinFleeSO", menuName = "Scriptable Objects/Enemies/Golden Goblin/GoldenGoblinFleeSO")]
public class GoldenGoblinFleeSO : ScriptableObject
{
    [Header("Pathfinding & Fleeing")]
    [Tooltip("Target distance to maintain away from the player.")]
    public float fleeDistance = 15f;

    [Tooltip("Distance ahead on the NavMesh to sample flee positions.")]
    public float fleeSampleRadius = 8f;

    [Tooltip("Seconds between destination re-calculations.")]
    public float repathInterval = 0.2f;

}
