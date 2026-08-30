using UnityEngine;

[CreateAssetMenu(fileName = "BubblerCasterSO", menuName = "Scriptable Objects/BubblerCasterSO")]
public class BubblerCasterSO : ScriptableObject
{
    [Tooltip("Maximum range to find an ally and cast the bubble shield in metres.")]
    public float castRange = 15f;

    [Tooltip("Distance threshold beyond which an existing channel breaks.")]
    public float breakRange = 18f;

    [Tooltip("Interval between checking for target validity and repath in seconds.")]
    public float tickInterval = 0.2f;

    [Tooltip("Distance from the player below which the Bubbler flees/backs away.")]
    public float keepDistance = 10f;

    [Tooltip("Sample distance for fleeing NavMesh positions.")]
    public float fleeSampleRadius = 6f;

    [Tooltip("Preferred stand-off distance from the shielded ally so the Bubbler stays near its ward.")]
    public float allyFollowDistance = 8f;
}
