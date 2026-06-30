using UnityEngine;

[CreateAssetMenu(fileName = "EnemySO", menuName = "Scriptable Objects/EnemySO")]
public class EnemySO : ScriptableObject
{
    [Header("Coin Drop")]
    [Tooltip("Minimum coins dropped on death (inclusive).")]
    public int minCoinDrop = 2;
    [Tooltip("Maximum coins dropped on death (inclusive). Tougher enemies can drop more.")]
    public int maxCoinDrop = 5;

    /// <summary>Returns a random coin amount in the inclusive range [minCoinDrop, maxCoinDrop].</summary>
    public int RollCoinDrop()
    {
        // Random.Range(int, int) is max-exclusive, so add one to make maxCoinDrop reachable.
        return Random.Range(minCoinDrop, maxCoinDrop + 1);
    }
}
