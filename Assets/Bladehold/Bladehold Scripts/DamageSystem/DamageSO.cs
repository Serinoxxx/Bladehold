using UnityEngine;

[CreateAssetMenu(fileName = "DamageSO", menuName = "Scriptable Objects/DamageSO")]
public class DamageSO : ScriptableObject
{
    public float baseDamage = 1f;
    public bool isCritical = false;
}