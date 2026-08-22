
using UnityEngine;

[CreateAssetMenu(fileName = "HealthSO", menuName = "Scriptable Objects/HealthSO")]
public class HealthSO : ScriptableObject
{
    public float maxHealth;
    [Tooltip("When true, any character or object using this HealthSO is immune to player-owned damage.")]
    public bool immuneToPlayerDamage;
}

