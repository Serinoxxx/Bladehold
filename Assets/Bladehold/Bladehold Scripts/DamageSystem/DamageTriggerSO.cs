using UnityEngine;

[CreateAssetMenu(fileName = "DamageTriggerSO", menuName = "Scriptable Objects/DamageTriggerSO")]
public class DamageTriggerSO : ScriptableObject
{
    public float radius = 2f;
    public float duration = 0.25f;
    public int maxHits = 1;
}