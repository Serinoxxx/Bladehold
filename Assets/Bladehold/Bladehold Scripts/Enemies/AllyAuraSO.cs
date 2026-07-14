using UnityEngine;

[CreateAssetMenu(fileName = "AllyAuraSO", menuName = "Scriptable Objects/AllyAuraSO")]
public class AllyAuraSO : ScriptableObject
{
    [Tooltip("Radius of the support aura around the caster.")]
    public float radius = 6f;

    [Tooltip("Health restored to each living ally inside the aura per tick.")]
    public float healPerTick = 2f;

    [Tooltip("Seconds between aura ticks.")]
    public float tickInterval = 1f;
}
